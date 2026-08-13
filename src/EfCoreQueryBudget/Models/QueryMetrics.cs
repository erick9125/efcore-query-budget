namespace EfCoreQueryBudget;

/// <summary>
/// Aggregated metrics computed from recorded queries.
/// </summary>
public sealed record QueryMetrics
{
    /// <summary>
    /// The budget these metrics were computed against. It is not a convenience copy: the slow-query
    /// threshold, the repeat threshold and the normalization mode all shape the numbers below, so
    /// metrics only mean anything next to the budget that produced them. Evaluation reads it from
    /// here, which is what makes it impossible to score metrics against a different budget.
    /// </summary>
    public required QueryBudgetOptions Budget { get; init; }

    public int QueryCount { get; init; }

    /// <summary>
    /// Executions of a read that were not needed: the same SQL with the same parameter values,
    /// returning rows the caller already had. It counts executions past the first in each group —
    /// six identical reads are five redundant ones — not how many groups repeated. Writes are
    /// excluded, since repeating one is not redundant by itself.
    /// </summary>
    public int RedundantExecutionCount { get; init; }

    /// <summary>
    /// How many read patterns repeated. Answers "how many places", not "how big" — see
    /// <see cref="MaximumPatternExecutions"/> for that.
    /// </summary>
    public int RepeatedPatternCount { get; init; }

    /// <summary>
    /// Executions in the largest repeated read pattern, or zero when there is none. A single group
    /// of 5000 executions and one of 5 both count as one pattern, so this is what tells them apart.
    /// </summary>
    public int MaximumPatternExecutions { get; init; }

    public int SlowQueryCount { get; init; }

    public TimeSpan TotalDuration { get; init; }

    public TimeSpan MaximumDuration { get; init; }

    /// <summary>
    /// Command executions that were captured more than once and discarded. Above zero means the
    /// interceptor is attached to the <c>DbContext</c> more than once.
    /// </summary>
    public int DuplicateCaptureCount { get; init; }

    /// <summary>
    /// Commands that ran but were not retained, because the scope had reached
    /// <see cref="QueryBudgetOptions.MaxRecordedQueries"/>. They are counted and timed in
    /// <see cref="QueryCount"/>, <see cref="TotalDuration"/>, <see cref="MaximumDuration"/> and
    /// <see cref="SlowQueryCount"/>; the duplicate and pattern groups below cover only what was
    /// retained.
    /// </summary>
    public int DiscardedQueryCount { get; init; }

    public IReadOnlyList<QueryGroup> ExactDuplicateGroups { get; init; }
        = Array.Empty<QueryGroup>();

    public IReadOnlyList<QueryGroup> RepeatedPatternGroups { get; init; }
        = Array.Empty<QueryGroup>();

    public IReadOnlyList<RecordedQuery> Queries { get; init; }
        = Array.Empty<RecordedQuery>();
}
