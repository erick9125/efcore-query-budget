namespace EfCoreQueryBudget;

public sealed class ExactDuplicateDetector
{
    private readonly ISqlNormalizer _normalizer;
    private readonly IQueryFingerprinter _fingerprinter;

    /// <param name="normalizer">
    /// Normalizes SQL for exact grouping and for the reported query. Literals must survive here,
    /// so this is left whitespace-only regardless of <see cref="SqlNormalizationMode"/>.
    /// </param>
    /// <param name="fingerprinter">
    /// Computes the exact fingerprint queries are grouped by. Defaults to one built over
    /// <paramref name="normalizer"/>.
    /// </param>
    public ExactDuplicateDetector(
        ISqlNormalizer? normalizer = null,
        IQueryFingerprinter? fingerprinter = null)
    {
        _normalizer = normalizer ?? new DefaultSqlNormalizer();
        _fingerprinter = fingerprinter
            ?? new DefaultQueryFingerprinter(_normalizer, _normalizer);
    }

    public IReadOnlyList<QueryGroup> Detect(IReadOnlyList<RecordedQuery> queries)
    {
        return Group(queries, q => _fingerprinter.ExactFingerprint(q))
            .Where(g => g.Queries.Count > 1)
            .Select(g => new QueryGroup
            {
                Fingerprint = g.Fingerprint,
                NormalizedSql = _normalizer.Normalize(g.Queries[0].CommandText),
                ExecutionCount = g.Queries.Count,
                // Every query in the group shares a fingerprint, so they share an operation.
                Operation = SqlOperationClassifier.Classify(g.Queries[0].CommandText),
                DistinctVariantCount = 1,
                Queries = g.Queries
            })
            .OrderByDescending(g => g.ExecutionCount)
            .ToArray();
    }

    private static List<Grouping> Group(
        IReadOnlyList<RecordedQuery> queries,
        Func<RecordedQuery, string> fingerprintOf)
    {
        var map = new Dictionary<string, List<RecordedQuery>>(StringComparer.Ordinal);
        foreach (var query in queries)
        {
            var fingerprint = fingerprintOf(query);
            if (!map.TryGetValue(fingerprint, out var list))
            {
                list = [];
                map[fingerprint] = list;
            }

            list.Add(query);
        }

        return map.Select(pair => new Grouping(pair.Key, pair.Value)).ToList();
    }

    private sealed record Grouping(string Fingerprint, List<RecordedQuery> Queries);
}
