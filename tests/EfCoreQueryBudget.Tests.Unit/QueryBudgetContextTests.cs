using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class QueryBudgetContextTests
{
    [Fact]
    public void Rejects_nested_scopes()
    {
        using var outer = QueryBudgetContext.Begin();
        var act = () => QueryBudgetContext.Begin();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nested*");
    }

    [Fact]
    public void Ignores_queries_outside_scope()
    {
        QueryBudgetContext.Record(new RecordedQuery
        {
            CommandText = "SELECT 1",
            Timestamp = DateTimeOffset.UtcNow
        });

        using var handle = QueryBudgetContext.Begin();
        QueryBudgetContext.Current!.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Cleans_up_after_exception()
    {
        try
        {
            using var scope = QueryBudgetContext.Begin();
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException)
        {
        }

        QueryBudgetContext.Current.Should().BeNull();
        QueryBudgetContext.HasActiveScope.Should().BeFalse();
    }

    [Fact]
    public async Task Async_flow_preserves_scope()
    {
        using var handle = QueryBudgetContext.Begin();
        var scope = QueryBudgetContext.Current!;

        await Task.Yield();
        QueryBudgetContext.Record(new RecordedQuery
        {
            CommandText = "SELECT 1",
            Timestamp = DateTimeOffset.UtcNow
        });

        scope.Snapshot().Should().ContainSingle();
    }
}

public class QueryBudgetApiTests
{
    [Fact]
    public async Task MeasureAsync_captures_recorded_queries()
    {
        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            QueryBudgetContext.Record(new RecordedQuery
            {
                CommandText = "SELECT 1",
                Duration = TimeSpan.FromMilliseconds(4),
                Timestamp = DateTimeOffset.UtcNow
            });
            await Task.Yield();
            return 42;
        });

        measurement.Value.Should().Be(42);
        measurement.Metrics.QueryCount.Should().Be(1);
        measurement.Result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task AssertAsync_throws_with_actionable_message()
    {
        var act = async () => await QueryBudget.AssertAsync(
            new QueryBudgetOptions
            {
                MaxQueries = 0,
                ScopeLabel = "unit-test"
            },
            async () =>
            {
                QueryBudgetContext.Record(new RecordedQuery
                {
                    CommandText = "SELECT 1",
                    Duration = TimeSpan.FromMilliseconds(1),
                    Timestamp = DateTimeOffset.UtcNow
                });
                await Task.Yield();
            });

        var exception = await act.Should().ThrowAsync<QueryBudgetExceededException>();
        exception.Which.Message.Should().Contain("EF Core query budget exceeded");
        exception.Which.Message.Should().Contain("Scope: unit-test");
        exception.Which.Message.Should().Contain("Query count");
        exception.Which.Result.Metrics.QueryCount.Should().Be(1);
    }

    [Fact]
    public void Report_hides_parameters_by_default()
    {
        var queries = Enumerable.Range(0, 5)
            .Select(i => new RecordedQuery
            {
                CommandText = "SELECT * FROM users WHERE email = @email",
                Parameters = new Dictionary<string, object?>
                {
                    ["@email"] = $"user{i}@example.com"
                },
                Duration = TimeSpan.FromMilliseconds(1),
                Timestamp = DateTimeOffset.UtcNow
            })
            .ToArray();

        var metrics = new QueryMetricsCalculator().Calculate(
            queries,
            new QueryBudgetOptions { RepeatedPatternThreshold = 5 });
        var result = new QueryBudgetEvaluator().Evaluate(
            new QueryBudgetOptions
            {
                MaxRepeatedPatterns = 0,
                RepeatedPatternThreshold = 5
            },
            metrics);

        var report = new DefaultQueryReportFormatter().Format(result);
        report.Should().Contain("Distinct parameter sets: 5");
        report.Should().Contain("Possible N+1 query pattern");
        report.Should().NotContain("@example.com");
        report.Should().Contain("Parameter values are hidden");
    }
}
