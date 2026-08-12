namespace EfCoreQueryBudget;

/// <summary>
/// Groups queries by a fingerprint, preserving the order they were captured in so that the first
/// query of a group is the earliest one.
/// </summary>
internal static class QueryGrouper
{
    public static List<QueryGrouping> By(
        IReadOnlyList<RecordedQuery> queries,
        Func<RecordedQuery, string> fingerprintOf)
    {
        var groups = new List<QueryGrouping>();
        var index = new Dictionary<string, QueryGrouping>(StringComparer.Ordinal);

        foreach (var query in queries)
        {
            var fingerprint = fingerprintOf(query);
            if (!index.TryGetValue(fingerprint, out var group))
            {
                group = new QueryGrouping(fingerprint);
                index[fingerprint] = group;
                groups.Add(group);
            }

            group.Queries.Add(query);
        }

        return groups;
    }
}

internal sealed class QueryGrouping
{
    public QueryGrouping(string fingerprint)
    {
        Fingerprint = fingerprint;
    }

    public string Fingerprint { get; }

    public List<RecordedQuery> Queries { get; } = [];
}
