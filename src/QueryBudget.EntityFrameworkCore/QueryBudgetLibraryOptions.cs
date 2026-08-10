namespace ErickMorales.EntityFrameworkCore.QueryBudget;

/// <summary>
/// Library-level options registered through dependency injection.
/// </summary>
public sealed class QueryBudgetLibraryOptions
{
    public TimeSpan SlowQueryThreshold { get; set; }
        = TimeSpan.FromMilliseconds(100);

    public QueryParameterDisplayMode ParameterDisplayMode { get; set; }
        = QueryParameterDisplayMode.Hidden;

    /// <summary>
    /// When false, the interceptor returns immediately without capturing.
    /// Prefer leaving this true in tests and disabling explicitly in production hosts.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
