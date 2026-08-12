namespace EfCoreQueryBudget;

public sealed class RepeatedPatternDetector
{
    private readonly ISqlNormalizer _normalizer;
    private readonly IQueryFingerprinter _fingerprinter;

    /// <param name="normalizer">
    /// Renders the pattern a group reports. Pass a
    /// <see cref="SqlNormalizationMode.MaskLiterals"/> normalizer to group executions that differ
    /// only in an inline literal.
    /// </param>
    /// <param name="fingerprinter">
    /// Computes both the structural fingerprint queries are grouped by and the exact fingerprint
    /// variants are counted with.
    /// </param>
    public RepeatedPatternDetector(ISqlNormalizer normalizer, IQueryFingerprinter fingerprinter)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(fingerprinter);

        _normalizer = normalizer;
        _fingerprinter = fingerprinter;
    }

    public IReadOnlyList<QueryGroup> Detect(
        IReadOnlyList<RecordedQuery> queries,
        int threshold = 5)
    {
        ArgumentNullException.ThrowIfNull(queries);

        return QueryGrouper.By(queries, _fingerprinter.StructuralFingerprint)
            .Select(group =>
            {
                // Variants, not parameter sets: a query with inline literals carries no parameters
                // at all, so counting those would collapse every raw-SQL group to one variant and
                // discard it below.
                var variants = group.Queries
                    .Select(_fingerprinter.ExactFingerprint)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                return new QueryGroup
                {
                    Fingerprint = group.Fingerprint,
                    NormalizedSql = _normalizer.Normalize(group.Queries[0].CommandText),
                    ExecutionCount = group.Queries.Count,
                    // Every query in the group shares a fingerprint, so they share an operation.
                    Operation = SqlOperationClassifier.Classify(group.Queries[0].CommandText),
                    DistinctVariantCount = variants,
                    Queries = group.Queries
                };
            })
            .Where(g => g.ExecutionCount >= threshold && g.DistinctVariantCount > 1)
            .OrderByDescending(g => g.ExecutionCount)
            .ToArray();
    }
}
