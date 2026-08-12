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

    /// <summary>
    /// How SQL is normalized before queries are grouped into patterns. Set this to
    /// <see cref="SqlNormalizationMode.MaskLiterals"/> when the SQL under test carries inline
    /// literals — raw SQL, <c>FromSqlRaw</c>, or constants the provider inlines — since otherwise
    /// every execution gets its own fingerprint and no pattern is ever detected.
    /// </summary>
    public SqlNormalizationMode SqlNormalization { get; init; }
        = SqlNormalizationMode.WhitespaceOnly;

    public string? ScopeLabel { get; init; }

    public QueryParameterDisplayMode ParameterDisplayMode { get; init; }
        = QueryParameterDisplayMode.Hidden;
}
