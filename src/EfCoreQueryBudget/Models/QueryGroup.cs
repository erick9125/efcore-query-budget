namespace EfCoreQueryBudget;

/// <summary>
/// A group of recorded queries that share a fingerprint.
/// </summary>
public sealed record QueryGroup
{
    public required string Fingerprint { get; init; }

    public required string NormalizedSql { get; init; }

    public int ExecutionCount { get; init; }

    /// <summary>
    /// How many distinct queries the group holds once literals and parameter values are taken into
    /// account. A group of many executions with more than one variant is the N+1 shape; a group
    /// with a single variant is a repeated identical query.
    /// </summary>
    public int DistinctVariantCount { get; init; }

    public IReadOnlyList<RecordedQuery> Queries { get; init; }
        = Array.Empty<RecordedQuery>();
}
