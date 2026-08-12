namespace EfCoreQueryBudget;

/// <summary>
/// Configurable limits evaluated against captured query metrics.
/// All limits are optional; unset limits are not enforced.
/// </summary>
public sealed record QueryBudgetOptions
{
    public int? MaxQueries { get; init; }

    public int? MaxExactDuplicates { get; init; }

    public int? MaxRepeatedPatterns { get; init; }

    public int? MaxSlowQueries { get; init; }

    public TimeSpan? MaxTotalDuration { get; init; }

    public TimeSpan? MaxSingleQueryDuration { get; init; }

    public TimeSpan SlowQueryThreshold { get; init; }
        = TimeSpan.FromMilliseconds(100);

    public int RepeatedPatternThreshold { get; init; } = 5;

    public string? ScopeLabel { get; init; }

    public QueryParameterDisplayMode ParameterDisplayMode { get; init; }
        = QueryParameterDisplayMode.Hidden;
}
