namespace EfCoreQueryBudget;

public sealed class RepeatedPatternDetector
{
    private readonly ISqlNormalizer _normalizer;
    private readonly IQueryFingerprinter _fingerprinter;

    /// <param name="normalizer">
    /// Normalizes SQL for grouping and for the reported pattern. Pass a
    /// <see cref="SqlNormalizationMode.MaskLiterals"/> normalizer to group executions that differ
    /// only in an inline literal.
    /// </param>
    /// <param name="fingerprinter">
    /// Computes both the structural fingerprint queries are grouped by and the exact fingerprint
    /// variants are counted with. Defaults to one that masks only on the structural side.
    /// </param>
    public RepeatedPatternDetector(
        ISqlNormalizer? normalizer = null,
        IQueryFingerprinter? fingerprinter = null)
    {
        _normalizer = normalizer ?? new DefaultSqlNormalizer();

        // The exact fingerprint counts distinct variants within a group, so it must keep the
        // literals the structural one may have masked away.
        _fingerprinter = fingerprinter
            ?? new DefaultQueryFingerprinter(_normalizer, new DefaultSqlNormalizer());
    }

    public IReadOnlyList<QueryGroup> Detect(
        IReadOnlyList<RecordedQuery> queries,
        int threshold = 5)
    {
        var groups = new Dictionary<string, List<RecordedQuery>>(StringComparer.Ordinal);

        foreach (var query in queries)
        {
            var fingerprint = _fingerprinter.StructuralFingerprint(query);
            if (!groups.TryGetValue(fingerprint, out var list))
            {
                list = [];
                groups[fingerprint] = list;
            }

            list.Add(query);
        }

        return groups
            .Select(pair =>
            {
                // Variants, not parameter sets: a query with inline literals carries no parameters
                // at all, so counting those would collapse every raw-SQL group to one variant and
                // discard it below.
                var variants = pair.Value
                    .Select(_fingerprinter.ExactFingerprint)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                return new QueryGroup
                {
                    Fingerprint = pair.Key,
                    NormalizedSql = _normalizer.Normalize(pair.Value[0].CommandText),
                    ExecutionCount = pair.Value.Count,
                    DistinctVariantCount = variants,
                    Queries = pair.Value
                };
            })
            .Where(g => g.ExecutionCount >= threshold && g.DistinctVariantCount > 1)
            .OrderByDescending(g => g.ExecutionCount)
            .ToArray();
    }
}
