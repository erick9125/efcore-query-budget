namespace ErickMorales.EntityFrameworkCore.QueryBudget;

/// <summary>
/// Collects recorded queries for a single measurement scope.
/// </summary>
public sealed class QueryScope
{
    private readonly List<RecordedQuery> _queries = [];
    private readonly object _gate = new();

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public void Record(RecordedQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_gate)
        {
            _queries.Add(query);
        }
    }

    public IReadOnlyList<RecordedQuery> Snapshot()
    {
        lock (_gate)
        {
            return _queries.ToArray();
        }
    }
}
