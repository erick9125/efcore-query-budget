namespace EfCoreQueryBudget;

/// <summary>
/// Collects recorded queries for a single measurement scope.
/// </summary>
public sealed class QueryScope
{
    private readonly List<RecordedQuery> _queries = [];
    private readonly HashSet<Guid> _capturedCommandIds = [];
    private readonly object _gate = new();
    private int _duplicateCaptureCount;

    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// How many command executions arrived more than once and were discarded. Anything above zero
    /// means the interceptor is attached to the <c>DbContext</c> more than once; every metric
    /// would otherwise be inflated by the number of attachments.
    /// </summary>
    public int DuplicateCaptureCount
    {
        get
        {
            lock (_gate)
            {
                return _duplicateCaptureCount;
            }
        }
    }

    public void Record(RecordedQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_gate)
        {
            // EF Core issues one CommandId per execution, so a repeat means the same execution was
            // intercepted twice rather than the same SQL running twice. Queries recorded by hand
            // carry no id and are always kept.
            if (query.CommandId != Guid.Empty && !_capturedCommandIds.Add(query.CommandId))
            {
                _duplicateCaptureCount++;
                return;
            }

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
