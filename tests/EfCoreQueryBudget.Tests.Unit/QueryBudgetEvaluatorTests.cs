using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class QueryBudgetEvaluatorTests
{
    private readonly QueryBudgetEvaluator _evaluator = new();
    private readonly QueryMetricsCalculator _calculator = new();

    [Fact]
    public void Passes_when_within_limits()
    {
        var result = Evaluate(
            new QueryBudgetOptions { MaxQueries = 5 },
            Query("SELECT 1", TimeSpan.FromMilliseconds(10)));

        result.Passed.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Fails_when_query_count_exceeded()
    {
        var result = Evaluate(
            new QueryBudgetOptions { MaxQueries = 1 },
            Query("SELECT 1", TimeSpan.FromMilliseconds(1)),
            Query("SELECT 2", TimeSpan.FromMilliseconds(1)));

        result.Passed.Should().BeFalse();
        result.Violations.Should().ContainSingle(v =>
            v.Type == QueryBudgetViolationType.QueryCountExceeded);
    }

    [Fact]
    public void Fails_when_duplicates_exceeded()
    {
        var result = Evaluate(
            new QueryBudgetOptions { MaxExactDuplicates = 0 },
            Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(1), ("@id", 1)),
            Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(1), ("@id", 1)));

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

        var result = Evaluate(
            new QueryBudgetOptions
            {
                MaxRepeatedPatterns = 0,
                RepeatedPatternThreshold = 5
            },
            queries);

        result.Passed.Should().BeFalse();
        result.Violations.Should().Contain(v =>
            v.Type == QueryBudgetViolationType.RepeatedPatternsExceeded);
    }

    [Fact]
    public void Fails_when_duration_exceeded()
    {
        var result = Evaluate(
            new QueryBudgetOptions { MaxTotalDuration = TimeSpan.FromMilliseconds(100) },
            Query("SELECT 1", TimeSpan.FromMilliseconds(80)),
            Query("SELECT 2", TimeSpan.FromMilliseconds(80)));

        result.Passed.Should().BeFalse();
        result.Violations.Should().Contain(v =>
            v.Type == QueryBudgetViolationType.TotalDurationExceeded);
    }

    [Fact]
    public void Collects_multiple_violations()
    {
        var result = Evaluate(
            new QueryBudgetOptions
            {
                MaxQueries = 1,
                MaxExactDuplicates = 0,
                MaxSlowQueries = 0,
                SlowQueryThreshold = TimeSpan.FromMilliseconds(50)
            },
            Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(120), ("@id", 1)),
            Query("SELECT * FROM t WHERE id = @id", TimeSpan.FromMilliseconds(120), ("@id", 1)));

        result.Violations.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void The_slow_threshold_that_shaped_the_metrics_is_the_one_evaluated()
    {
        // 80 ms is slow under this budget and would not be under the default one. Before the
        // budget travelled with the metrics, the threshold used to compute them and the threshold
        // the result reported could be two different values.
        var budget = new QueryBudgetOptions
        {
            SlowQueryThreshold = TimeSpan.FromMilliseconds(50),
            MaxSlowQueries = 0
        };

        var result = Evaluate(budget, Query("SELECT 1", TimeSpan.FromMilliseconds(80)));

        result.Metrics.SlowQueryCount.Should().Be(1);
        result.Budget.Should().BeSameAs(budget);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void Metrics_include_slow_and_duration()
    {
        var metrics = _calculator.Calculate(
            [
                Query("SELECT 1", TimeSpan.FromMilliseconds(10)),
                Query("SELECT 2", TimeSpan.FromMilliseconds(150))
            ],
            new QueryBudgetOptions());

        metrics.QueryCount.Should().Be(2);
        metrics.SlowQueryCount.Should().Be(1);
        metrics.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(160));
        metrics.MaximumDuration.Should().Be(TimeSpan.FromMilliseconds(150));
    }

    private QueryBudgetResult Evaluate(QueryBudgetOptions budget, params RecordedQuery[] queries)
        => _evaluator.Evaluate(_calculator.Calculate(queries, budget));

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
