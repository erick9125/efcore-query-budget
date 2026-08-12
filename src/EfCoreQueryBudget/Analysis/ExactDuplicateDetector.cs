namespace EfCoreQueryBudget;

public sealed class ExactDuplicateDetector
{
    private readonly ISqlNormalizer _normalizer;
    private readonly IQueryFingerprinter _fingerprinter;

    /// <param name="normalizer">
    /// Renders the SQL a group reports. Literals must survive here, so this is the whitespace-only
    /// normalizer regardless of <see cref="SqlNormalizationMode"/>.
    /// </param>
    /// <param name="fingerprinter">Computes the exact fingerprint queries are grouped by.</param>
    public ExactDuplicateDetector(ISqlNormalizer normalizer, IQueryFingerprinter fingerprinter)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(fingerprinter);

        _normalizer = normalizer;
        _fingerprinter = fingerprinter;
    }

    public IReadOnlyList<QueryGroup> Detect(IReadOnlyList<RecordedQuery> queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        return QueryGrouper.By(queries, _fingerprinter.ExactFingerprint)
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
}
