namespace ErickMorales.EntityFrameworkCore.QueryBudget;

/// <summary>
/// Host-level options for the capture pipeline, registered through dependency injection.
/// </summary>
/// <remarks>
/// These options govern <em>capture</em> only. Everything that shapes a single measurement —
/// the limits, the slow-query threshold and the parameter display mode — belongs to
/// <see cref="QueryBudgetOptions"/> and is supplied per assertion, because two budgets in the
/// same test project routinely need different values.
/// </remarks>
public sealed class QueryBudgetLibraryOptions
{
    /// <summary>
    /// When false, the interceptor returns immediately without capturing.
    /// Prefer leaving this true in tests and disabling explicitly in production hosts.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How a captured command is matched to a measurement scope. Defaults to
    /// <see cref="ScopeAttributionMode.AsyncLocalOnly"/>.
    /// </summary>
    public ScopeAttributionMode AttributionMode { get; set; }
        = ScopeAttributionMode.AsyncLocalOnly;
}
