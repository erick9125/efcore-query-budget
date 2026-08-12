namespace EfCoreQueryBudget;

/// <summary>
/// What a scope counted across <em>every</em> command it accepted, whether or not the command was
/// retained for analysis.
/// </summary>
/// <remarks>
/// Retention is capped by <see cref="QueryBudgetOptions.MaxRecordedQueries"/>, but the limits a
/// budget is evaluated against must not shrink with it: a scope that ran past the cap has to fail
/// <c>MaxQueries</c> and <c>MaxTotalDuration</c> on what actually ran, not on the retained sample.
/// </remarks>
public sealed record QueryCaptureTotals
{
    /// <summary>Commands accepted by the scope, retained or not.</summary>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// Commands that ran but were not retained because the scope was already at its cap. Duplicate
    /// and pattern analysis covers the retained ones only.
    /// </summary>
    public int DiscardedQueryCount { get; init; }

    public TimeSpan TotalDuration { get; init; }

    public TimeSpan MaximumDuration { get; init; }

    public int SlowQueryCount { get; init; }
}
