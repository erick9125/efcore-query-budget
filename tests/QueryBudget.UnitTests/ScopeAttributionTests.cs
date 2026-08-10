using ErickMorales.EntityFrameworkCore.QueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.UnitTests;

public class ScopeAttributionTests
{
    [Fact]
    public void AsyncLocalOnly_ignores_commands_from_a_flow_without_a_scope()
    {
        using var handle = QueryBudgetContext.Begin();
        var scope = QueryBudgetContext.Current!;

        RecordFromDetachedFlow(ScopeAttributionMode.AsyncLocalOnly);

        scope.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void SingleActiveScopeFallback_claims_commands_from_a_flow_without_a_scope()
    {
        using var handle = QueryBudgetContext.Begin();
        var scope = QueryBudgetContext.Current!;

        RecordFromDetachedFlow(ScopeAttributionMode.SingleActiveScopeFallback);

        scope.Snapshot().Should().ContainSingle();
    }

    [Fact]
    public async Task SingleActiveScopeFallback_stands_down_when_several_scopes_are_active()
    {
        var secondScopeStarted = new TaskCompletionSource();
        var recorded = new TaskCompletionSource();

        var second = Task.Run(async () =>
        {
            using var handle = QueryBudgetContext.Begin();
            secondScopeStarted.SetResult();
            await recorded.Task;
            return QueryBudgetContext.Current!.Snapshot();
        });

        using var outer = QueryBudgetContext.Begin();
        var outerScope = QueryBudgetContext.Current!;
        await secondScopeStarted.Task;

        RecordFromDetachedFlow(ScopeAttributionMode.SingleActiveScopeFallback);
        recorded.SetResult();

        outerScope.Snapshot().Should().BeEmpty();
        (await second).Should().BeEmpty();
    }

    [Fact]
    public void Default_attribution_mode_is_AsyncLocalOnly()
    {
        new QueryBudgetLibraryOptions().AttributionMode
            .Should().Be(ScopeAttributionMode.AsyncLocalOnly);
    }

    /// <summary>
    /// Records a query from an execution flow that carries no scope, the way a hosted service or
    /// a parallel test would. The task is started inside the suppressed region and waited on
    /// outside it, because <see cref="AsyncFlowControl"/> must be undone on its own thread.
    /// </summary>
    private static void RecordFromDetachedFlow(ScopeAttributionMode mode)
    {
        Task work;
        using (ExecutionContext.SuppressFlow())
        {
            work = Task.Run(() => QueryBudgetContext.Record(
                new RecordedQuery
                {
                    CommandText = "SELECT 1",
                    Timestamp = DateTimeOffset.UtcNow
                },
                mode));
        }

        work.GetAwaiter().GetResult();
    }
}
