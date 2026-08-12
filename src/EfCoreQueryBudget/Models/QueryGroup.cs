namespace EfCoreQueryBudget;

/// <summary>
/// A group of recorded queries that share a fingerprint.
/// </summary>
public sealed record QueryGroup
{
    public required string Fingerprint { get; init; }

    public required string NormalizedSql { get; init; }

    public int ExecutionCount { get; init; }

    public int DistinctParameterSetCount { get; init; }

    public IReadOnlyList<RecordedQuery> Queries { get; init; }
        = Array.Empty<RecordedQuery>();
}
