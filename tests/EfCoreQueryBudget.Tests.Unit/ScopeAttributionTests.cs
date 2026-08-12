using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class ScopeAttributionTests
{
    [Fact]
    public async Task AsyncLocalOnly_ignores_commands_from_a_flow_without_a_scope()
    {
        var foreign = await ForeignScope.StartAsync();

        Record(ScopeAttributionMode.AsyncLocalOnly);

        (await foreign.EndAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SingleActiveScopeFallback_claims_commands_from_a_flow_without_a_scope()
    {
        var foreign = await ForeignScope.StartAsync();

        Record(ScopeAttributionMode.SingleActiveScopeFallback);

        (await foreign.EndAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task SingleActiveScopeFallback_stands_down_when_several_scopes_are_active()
    {
        var first = await ForeignScope.StartAsync();
        var second = await ForeignScope.StartAsync();

        Record(ScopeAttributionMode.SingleActiveScopeFallback);

        (await first.EndAsync()).Should().BeEmpty();
        (await second.EndAsync()).Should().BeEmpty();
    }

    [Fact]
    public void Default_attribution_mode_is_AsyncLocalOnly()
    {
        new QueryBudgetLibraryOptions().AttributionMode
            .Should().Be(ScopeAttributionMode.AsyncLocalOnly);
    }

    private static void Record(ScopeAttributionMode mode)
    {
        QueryBudgetContext.Record(
            new RecordedQuery
            {
                CommandText = "SELECT 1",
                Timestamp = DateTimeOffset.UtcNow
            },
            mode);
    }

    /// <summary>
    /// A scope living on its own execution flow. Because <see cref="AsyncLocal{T}"/> changes do not
    /// propagate back to the parent, the test method's flow provably carries no scope — which is
    /// the condition under test. Suppressing the flow instead would be non-deterministic: the
    /// pooled thread keeps whatever execution context it already had.
    /// </summary>
    private sealed class ForeignScope
    {
        private readonly TaskCompletionSource _release;
        private readonly Task<IReadOnlyList<RecordedQuery>> _owner;

        private ForeignScope(TaskCompletionSource release, Task<IReadOnlyList<RecordedQuery>> owner)
        {
            _release = release;
            _owner = owner;
        }

        public static async Task<ForeignScope> StartAsync()
        {
            var started = new TaskCompletionSource();
            var release = new TaskCompletionSource();

            var owner = Task.Run(async () =>
            {
                using var handle = QueryBudgetContext.Begin();
                var scope = QueryBudgetContext.Current!;
                started.SetResult();
                await release.Task;
                return scope.Snapshot();
            });

            await started.Task;
            return new ForeignScope(release, owner);
        }

        public Task<IReadOnlyList<RecordedQuery>> EndAsync()
        {
            _release.SetResult();
            return _owner;
        }
    }
}
