namespace EfCoreQueryBudget;

/// <summary>
/// Caches fingerprints for the duration of one analysis, so a query is hashed once however many
/// detectors ask for it.
/// </summary>
/// <remarks>
/// Keyed on <em>reference</em> identity, not value: <see cref="RecordedQuery"/> is a record, so
/// value equality would fold two separate executions of the same SQL into one entry — which is
/// exactly the distinction duplicate detection depends on — and would pay a deep comparison to do
/// it. Instances are scoped to a single analysis; a longer-lived one would pin every query it saw.
/// </remarks>
internal sealed class MemoizingQueryFingerprinter : IQueryFingerprinter
{
    private readonly IQueryFingerprinter _inner;

    private readonly Dictionary<RecordedQuery, string> _structural =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<RecordedQuery, string> _exact =
        new(ReferenceEqualityComparer.Instance);

    public MemoizingQueryFingerprinter(IQueryFingerprinter inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public string StructuralFingerprint(RecordedQuery query)
        => Memoize(_structural, query, _inner.StructuralFingerprint);

    public string ExactFingerprint(RecordedQuery query)
        => Memoize(_exact, query, _inner.ExactFingerprint);

    private static string Memoize(
        Dictionary<RecordedQuery, string> cache,
        RecordedQuery query,
        Func<RecordedQuery, string> compute)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (cache.TryGetValue(query, out var cached))
        {
            return cached;
        }

        var fingerprint = compute(query);
        cache[query] = fingerprint;
        return fingerprint;
    }
}
