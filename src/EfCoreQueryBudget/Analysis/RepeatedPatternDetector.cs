namespace EfCoreQueryBudget;

public sealed class RepeatedPatternDetector
{
    private readonly ISqlNormalizer _normalizer;
    private readonly IQueryFingerprinter _fingerprinter;

    public RepeatedPatternDetector(
        ISqlNormalizer? normalizer = null,
        IQueryFingerprinter? fingerprinter = null)
    {
        _normalizer = normalizer ?? new DefaultSqlNormalizer();
        _fingerprinter = fingerprinter ?? new DefaultQueryFingerprinter(_normalizer);
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
                var parameterSets = pair.Value
                    .Select(q => DefaultQueryFingerprinter.ParameterSetKey(q.Parameters))
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                return new QueryGroup
                {
                    Fingerprint = pair.Key,
                    NormalizedSql = _normalizer.Normalize(pair.Value[0].CommandText),
                    ExecutionCount = pair.Value.Count,
                    DistinctParameterSetCount = parameterSets,
                    Queries = pair.Value
                };
            })
            .Where(g => g.ExecutionCount >= threshold && g.DistinctParameterSetCount > 1)
            .OrderByDescending(g => g.ExecutionCount)
            .ToArray();
    }
}
