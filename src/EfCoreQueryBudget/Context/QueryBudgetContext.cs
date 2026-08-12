namespace EfCoreQueryBudget;

/// <summary>
/// Isolates query capture per async execution flow using <see cref="AsyncLocal{T}"/>.
/// </summary>
/// <remarks>
/// Attribution follows the execution flow by default. The process-wide
/// <see cref="ScopeAttributionMode.SingleActiveScopeFallback"/> is opt-in because it cannot tell a
/// command issued by the code under measurement from one issued by anything else in the process.
/// </remarks>
public static class QueryBudgetContext
{
    private static readonly AsyncLocal<QueryScope?> CurrentScope = new();
    private static readonly object Gate = new();
    private static readonly HashSet<QueryScope> ActiveScopes = [];

    /// <summary>
    /// The scope on the current execution flow, or <see langword="null"/> when there is none.
    /// </summary>
    public static QueryScope? Current => CurrentScope.Value;

    /// <summary>
    /// Whether the current execution flow carries a scope.
    /// </summary>
    public static bool HasActiveScope => CurrentScope.Value is not null;

    /// <summary>
    /// Starts a scope on the current execution flow. Dispose the result to end it.
    /// </summary>
    /// <exception cref="InvalidOperationException">A scope is already active on this flow.</exception>
    public static IDisposable Begin()
        => Begin(new QueryBudgetOptions());

    /// <summary>
    /// Starts a scope on the current execution flow, taking its retention cap and slow-query
    /// threshold from <paramref name="options"/>. Dispose the result to end it.
    /// </summary>
    /// <exception cref="InvalidOperationException">A scope is already active on this flow.</exception>
    public static IDisposable Begin(QueryBudgetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (CurrentScope.Value is not null)
        {
            throw new InvalidOperationException(
                "Nested query budget scopes are not supported.");
        }

        var scope = new QueryScope(options);
        CurrentScope.Value = scope;

        lock (Gate)
        {
            ActiveScopes.Add(scope);
        }

        return new ScopeHandle(scope);
    }

    /// <summary>
    /// Resolves the scope a command should be attributed to, following the execution flow only.
    /// </summary>
    public static bool TryGetScope(out QueryScope scope)
        => TryGetScope(ScopeAttributionMode.AsyncLocalOnly, out scope);

    /// <summary>
    /// Resolves the scope a command should be attributed to under the given
    /// <paramref name="mode"/>.
    /// </summary>
    public static bool TryGetScope(ScopeAttributionMode mode, out QueryScope scope)
    {
        var local = CurrentScope.Value;
        if (local is not null)
        {
            scope = local;
            return true;
        }

        if (mode == ScopeAttributionMode.SingleActiveScopeFallback)
        {
            lock (Gate)
            {
                if (ActiveScopes.Count == 1)
                {
                    scope = ActiveScopes.First();
                    return true;
                }
            }
        }

        scope = null!;
        return false;
    }

    /// <summary>
    /// Records a query against the scope on the current execution flow, if any.
    /// </summary>
    /// <remarks>
    /// Internal on purpose: capture is the interceptor's job, and a public entry point would be
    /// mutable global state offered as API.
    /// </remarks>
    internal static void Record(RecordedQuery query)
        => Record(query, ScopeAttributionMode.AsyncLocalOnly);

    /// <summary>
    /// Records a query against the scope resolved under the given <paramref name="mode"/>, if any.
    /// </summary>
    internal static void Record(RecordedQuery query, ScopeAttributionMode mode)
    {
        if (TryGetScope(mode, out var scope))
        {
            scope.Record(query);
        }
    }

    private sealed class ScopeHandle : IDisposable
    {
        private QueryScope? _scope;

        public ScopeHandle(QueryScope scope)
        {
            _scope = scope;
        }

        public void Dispose()
        {
            var scope = Interlocked.Exchange(ref _scope, null);
            if (scope is null)
            {
                return;
            }

            if (ReferenceEquals(CurrentScope.Value, scope))
            {
                CurrentScope.Value = null;
            }

            lock (Gate)
            {
                ActiveScopes.Remove(scope);
            }
        }
    }
}
