using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class QueryScopeRetentionTests
{
    [Fact]
    public void Below_the_cap_nothing_is_discarded()
    {
        var scope = new QueryScope(new QueryBudgetOptions { MaxRecordedQueries = 10 });
        Record(scope, 5);

        scope.Snapshot().Should().HaveCount(5);
        scope.Totals.ExecutionCount.Should().Be(5);
        scope.Totals.DiscardedQueryCount.Should().Be(0);
    }

    [Fact]
    public void Above_the_cap_only_the_first_queries_are_retained()
    {
        var scope = new QueryScope(new QueryBudgetOptions { MaxRecordedQueries = 3 });
        Record(scope, 10);

        var retained = scope.Snapshot();
        retained.Should().HaveCount(3);
        retained.Select(q => q.CommandText)
            .Should().Equal("SELECT 0", "SELECT 1", "SELECT 2");
        scope.Totals.DiscardedQueryCount.Should().Be(7);
    }

    [Fact]
    public void Totals_cover_every_command_not_just_the_retained_ones()
    {
        // The test that stops a budget from passing on a scope it never fully saw.
        var scope = new QueryScope(new QueryBudgetOptions
        {
            MaxRecordedQueries = 3,
            SlowQueryThreshold = TimeSpan.FromMilliseconds(50)
        });

        Record(scope, 10, i => TimeSpan.FromMilliseconds(10 * (i + 1)));

        scope.Snapshot().Should().HaveCount(3);
        scope.Totals.ExecutionCount.Should().Be(10);
        scope.Totals.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(550));
        scope.Totals.MaximumDuration.Should().Be(TimeSpan.FromMilliseconds(100));
        scope.Totals.SlowQueryCount.Should().Be(6);
    }

    [Fact]
    public void A_null_cap_retains_everything()
    {
        var scope = new QueryScope(new QueryBudgetOptions { MaxRecordedQueries = null });
        Record(scope, 50);

        scope.Snapshot().Should().HaveCount(50);
        scope.Totals.DiscardedQueryCount.Should().Be(0);
    }

    [Fact]
    public void A_repeated_command_id_counts_as_a_duplicate_capture_not_as_a_discard()
    {
        var scope = new QueryScope(new QueryBudgetOptions { MaxRecordedQueries = 10 });
        var commandId = Guid.NewGuid();

        scope.Record(Query("SELECT 1", commandId: commandId));
        scope.Record(Query("SELECT 1", commandId: commandId));

        scope.Snapshot().Should().HaveCount(1);
        scope.DuplicateCaptureCount.Should().Be(1);
        scope.Totals.ExecutionCount.Should().Be(1);
        scope.Totals.DiscardedQueryCount.Should().Be(0);
    }

    [Fact]
    public void The_budget_is_evaluated_against_everything_that_ran()
    {
        var scope = new QueryScope(new QueryBudgetOptions { MaxRecordedQueries = 3 });
        Record(scope, 10);

        var metrics = new QueryMetricsCalculator().Calculate(
            scope.Snapshot(),
            new QueryBudgetOptions(),
            scope.Totals);

        metrics.QueryCount.Should().Be(10);
        metrics.DiscardedQueryCount.Should().Be(7);

        var result = new QueryBudgetEvaluator().Evaluate(
            new QueryBudgetOptions { MaxQueries = 5 },
            metrics);

        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void The_report_says_what_was_not_retained()
    {
        var scope = new QueryScope(new QueryBudgetOptions { MaxRecordedQueries = 3 });
        Record(scope, 10);

        var metrics = new QueryMetricsCalculator().Calculate(
            scope.Snapshot(),
            new QueryBudgetOptions(),
            scope.Totals);
        var result = new QueryBudgetEvaluator().Evaluate(
            new QueryBudgetOptions { MaxQueries = 5 },
            metrics);

        new DefaultQueryReportFormatter().Format(result)
            .Should().Contain("7 query(s) ran but were not retained");
    }

    private static void Record(QueryScope scope, int count, Func<int, TimeSpan>? duration = null)
    {
        for (var i = 0; i < count; i++)
        {
            scope.Record(Query($"SELECT {i}", duration?.Invoke(i) ?? TimeSpan.FromMilliseconds(1)));
        }
    }

    private static RecordedQuery Query(
        string sql,
        TimeSpan? duration = null,
        Guid commandId = default)
    {
        return new RecordedQuery
        {
            CommandText = sql,
            Duration = duration ?? TimeSpan.FromMilliseconds(1),
            CommandId = commandId,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
