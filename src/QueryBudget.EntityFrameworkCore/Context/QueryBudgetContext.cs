namespace ErickMorales.EntityFrameworkCore.QueryBudget;

/// <summary>
/// Isolates query capture per async execution flow using <see cref="AsyncLocal{T}"/>.
/// When AsyncLocal is empty but exactly one scope is active (typical WebApplicationFactory
/// HTTP callbacks), queries are attributed to that sole scope.
/// </summary>
public static class QueryBudgetContext
{
    private static readonly AsyncLocal<QueryScope?> CurrentScope = new();
    private static readonly object Gate = new();
    private static readonly HashSet<QueryScope> ActiveScopes = [];

    public static QueryScope? Current => CurrentScope.Value;

    public static bool HasActiveScope
    {
        get
        {
            if (CurrentScope.Value is not null)
            {
                return true;
            }

            lock (Gate)
            {
                return ActiveScopes.Count > 0;
            }
        }
    }

    public static IDisposable Begin()
    {
        if (CurrentScope.Value is not null)
        {
            throw new InvalidOperationException(
                "Nested query budget scopes are not supported.");
        }

        var scope = new QueryScope();
        CurrentScope.Value = scope;

        lock (Gate)
        {
            ActiveScopes.Add(scope);
        }

        return new ScopeHandle(scope);
    }

    public static bool TryGetScope(out QueryScope scope)
    {
        var local = CurrentScope.Value;
        if (local is not null)
        {
            scope = local;
            return true;
        }

        lock (Gate)
        {
            if (ActiveScopes.Count == 1)
            {
                scope = ActiveScopes.First();
                return true;
            }
        }

        scope = null!;
        return false;
    }

    public static void Record(RecordedQuery query)
    {
        if (!TryGetScope(out var scope))
        {
            return;
        }

        scope.Record(query);
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
