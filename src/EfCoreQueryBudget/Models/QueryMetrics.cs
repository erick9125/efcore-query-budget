namespace EfCoreQueryBudget;

/// <summary>
/// Aggregated metrics computed from recorded queries.
/// </summary>
public sealed record QueryMetrics
{
    public int QueryCount { get; init; }

    public int ExactDuplicateCount { get; init; }

    public int RepeatedPatternCount { get; init; }

    public int SlowQueryCount { get; init; }

    public TimeSpan TotalDuration { get; init; }

    public TimeSpan MaximumDuration { get; init; }

    /// <summary>
    /// Command executions that were captured more than once and discarded. Above zero means the
    /// interceptor is attached to the <c>DbContext</c> more than once.
    /// </summary>
    public int DuplicateCaptureCount { get; init; }

    public IReadOnlyList<QueryGroup> ExactDuplicateGroups { get; init; }
        = Array.Empty<QueryGroup>();

    public IReadOnlyList<QueryGroup> RepeatedPatternGroups { get; init; }
        = Array.Empty<QueryGroup>();

    public IReadOnlyList<RecordedQuery> Queries { get; init; }
        = Array.Empty<RecordedQuery>();
}
