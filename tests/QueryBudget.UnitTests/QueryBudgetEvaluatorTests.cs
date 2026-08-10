using ErickMorales.EntityFrameworkCore.QueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.UnitTests;

public class QueryBudgetEvaluatorTests
{
    private readonly QueryBudgetEvaluator _evaluator = new();
    private readonly QueryMetricsCalculator _calculator = new();

    [Fact]
    public void Passes_when_within_limits()
    {
        var metrics = Metrics(
            Query("SELECT 1", TimeSpan.FromMilliseconds(10)));

        var result = _evaluator.Evaluate(
            new QueryBudgetOptions { MaxQueries = 5 },
            metrics);

        result.Passed.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Fails_when_query_count_exceeded()
    {
        var metrics = Metrics(
            Query("SELECT 1", TimeSpan.FromMilliseconds(1)),
            Query("SELECT 2", TimeSpan.FromMilliseconds(1)));

        var result = _evaluator.Evaluate(
            new QueryBudgetOptions { MaxQueries = 1 },
            metrics);

        result.Passed.Should().BeFalse();
        result.Violations.Should().ContainSingle(v =>
            v.Type == QueryBudgetViolationType.QueryCountExceeded);
    }

    [Fact]
    public void Fails_when_duplicates_exceeded()
    {
        var metrics = Metrics(
            Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(1), ("@id", 1)),
            Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(1), ("@id", 1)));

        var result = _evaluator.Evaluate(
            new QueryBudgetOptions { MaxExactDuplicates = 0 },
            metrics);

        result.Passed.Should().BeFalse();
        result.Violations.Should().Contain(v =>
            v.Type == QueryBudgetViolationType.ExactDuplicatesExceeded);
    }

    [Fact]
    public void Fails_when_patterns_exceeded()
    {
        var queries = Enumerable.Range(1, 5)
            .Select(i => Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(1), ("@id", i)))
            .ToArray();
        var metrics = Metrics(queries);

        var result = _evaluator.Evaluate(
            new QueryBudgetOptions
            {
                MaxRepeatedPatterns = 0,
                RepeatedPatternThreshold = 5
            },
            metrics);

        result.Passed.Should().BeFalse();
        result.Violations.Should().Contain(v =>
            v.Type == QueryBudgetViolationType.RepeatedPatternsExceeded);
    }

    [Fact]
    public void Fails_when_duration_exceeded()
    {
        var metrics = Metrics(
            Query("SELECT 1", TimeSpan.FromMilliseconds(80)),
            Query("SELECT 2", TimeSpan.FromMilliseconds(80)));

        var result = _evaluator.Evaluate(
            new QueryBudgetOptions
            {
                MaxTotalDuration = TimeSpan.FromMilliseconds(100)
            },
            metrics);

        result.Passed.Should().BeFalse();
        result.Violations.Should().Contain(v =>
            v.Type == QueryBudgetViolationType.TotalDurationExceeded);
    }

    [Fact]
    public void Collects_multiple_violations()
    {
        var metrics = Metrics(
            Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(120), ("@id", 1)),
            Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(120), ("@id", 1)));

        var result = _evaluator.Evaluate(
            new QueryBudgetOptions
            {
                MaxQueries = 1,
                MaxExactDuplicates = 0,
                MaxSlowQueries = 0,
                SlowQueryThreshold = TimeSpan.FromMilliseconds(50)
            },
            metrics);

        result.Violations.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Metrics_include_slow_and_duration()
    {
        var metrics = Metrics(
            Query("SELECT 1", TimeSpan.FromMilliseconds(10)),
            Query("SELECT 2", TimeSpan.FromMilliseconds(150)));

        metrics.QueryCount.Should().Be(2);
        metrics.SlowQueryCount.Should().Be(1);
        metrics.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(160));
        metrics.MaximumDuration.Should().Be(TimeSpan.FromMilliseconds(150));
    }

    private QueryMetrics Metrics(params RecordedQuery[] queries)
        => _calculator.Calculate(queries, new QueryBudgetOptions());

    private static RecordedQuery Query(
        string sql,
        TimeSpan duration,
        params (string Name, object? Value)[] parameters)
    {
        return new RecordedQuery
        {
            CommandText = sql,
            Parameters = parameters.ToDictionary(p => p.Name, p => p.Value),
            Duration = duration,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
