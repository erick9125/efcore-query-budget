using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class DetectorSemanticsTests
{
    [Fact]
    public void A_bulk_insert_is_not_a_repeated_pattern()
    {
        // 50 entities through SaveChanges: the same INSERT shape with different values, which is
        // the exact signature of an N+1 and is not a defect.
        var metrics = Calculate(Batch("INSERT INTO posts (title) VALUES (@p)", 50));

        metrics.RepeatedPatternCount.Should().Be(0);
        metrics.MaximumPatternExecutions.Should().Be(0);
    }

    [Fact]
    public void The_same_shape_as_a_read_is_a_repeated_pattern()
    {
        // Same shape, same counts, only the operation differs.
        var metrics = Calculate(Batch("SELECT * FROM posts WHERE author_id = @p", 50));

        metrics.RepeatedPatternCount.Should().Be(1);
        metrics.MaximumPatternExecutions.Should().Be(50);
    }

    [Fact]
    public void A_bulk_insert_is_still_reported_but_never_called_an_n_plus_one()
    {
        var report = Report(Batch("INSERT INTO posts (title) VALUES (@p)", 50));

        report.Should().Contain("Repeated write (not counted against the budget)");
        report.Should().Contain("INSERT INTO posts");
        report.Should().NotContain("Possible N+1");
    }

    [Fact]
    public void Repeated_identical_writes_are_not_exact_duplicates()
    {
        var writes = Repeat("UPDATE counters SET n = n + 1 WHERE id = @p", 6, ("@p", 1));

        Calculate(writes).ExactDuplicateCount.Should().Be(0);
    }

    [Fact]
    public void Repeated_identical_reads_are_exact_duplicates()
    {
        var reads = Repeat("SELECT n FROM counters WHERE id = @p", 6, ("@p", 1));

        Calculate(reads).ExactDuplicateCount.Should().Be(5);
    }

    [Fact]
    public void Max_executions_per_pattern_bounds_the_size_of_the_worst_group()
    {
        var budget = new QueryBudgetOptions
        {
            MaxRepeatedPatterns = null,
            MaxExecutionsPerPattern = 10
        };

        Evaluate(Batch("SELECT * FROM posts WHERE author_id = @p", 50), budget)
            .Passed.Should().BeFalse();

        Evaluate(Batch("SELECT * FROM posts WHERE author_id = @p", 8), budget)
            .Passed.Should().BeTrue();
    }

    [Fact]
    public void Max_executions_per_pattern_names_the_actual_count()
    {
        var result = Evaluate(
            Batch("SELECT * FROM posts WHERE author_id = @p", 50),
            new QueryBudgetOptions { MaxExecutionsPerPattern = 10 });

        var violation = result.Violations
            .Should().ContainSingle(v => v.Type == QueryBudgetViolationType.PatternExecutionsExceeded)
            .Subject;

        violation.Actual.Should().Be(50);
        violation.Budget.Should().Be(10);
    }

    [Fact]
    public void The_largest_pattern_ignores_writes()
    {
        var queries = Batch("SELECT * FROM posts WHERE author_id = @p", 6)
            .Concat(Batch("INSERT INTO posts (title) VALUES (@p)", 50))
            .ToArray();

        Calculate(queries).MaximumPatternExecutions.Should().Be(6);
    }

    private static QueryMetrics Calculate(RecordedQuery[] queries)
        => new QueryMetricsCalculator().Calculate(queries, new QueryBudgetOptions());

    private static QueryBudgetResult Evaluate(RecordedQuery[] queries, QueryBudgetOptions budget)
        => new QueryBudgetEvaluator().Evaluate(budget, Calculate(queries));

    private static string Report(RecordedQuery[] queries)
    {
        var budget = new QueryBudgetOptions { MaxQueries = 0 };
        var result = new QueryBudgetEvaluator().Evaluate(
            budget,
            new QueryMetricsCalculator().Calculate(queries, budget));

        return new DefaultQueryReportFormatter().Format(result);
    }

    private static RecordedQuery[] Batch(string sql, int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => Query(sql, ("@p", i)))
            .ToArray();
    }

    private static RecordedQuery[] Repeat(
        string sql,
        int count,
        params (string Name, object? Value)[] parameters)
    {
        return Enumerable.Range(0, count)
            .Select(_ => Query(sql, parameters))
            .ToArray();
    }

    private static RecordedQuery Query(
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        return new RecordedQuery
        {
            CommandText = sql,
            Parameters = parameters.ToDictionary(p => p.Name, p => p.Value),
            Duration = TimeSpan.FromMilliseconds(1),
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
