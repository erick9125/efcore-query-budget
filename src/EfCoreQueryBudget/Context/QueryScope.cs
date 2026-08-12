namespace EfCoreQueryBudget;

/// <summary>
/// Collects recorded queries for a single measurement scope.
/// </summary>
/// <remarks>
/// Retention is bounded, but counting is not: a scope past its cap stops holding queries and keeps
/// aggregating them, so the budget is still evaluated against everything that ran.
/// </remarks>
public sealed class QueryScope
{
    private readonly List<RecordedQuery> _queries = [];
    private readonly HashSet<Guid> _capturedCommandIds = [];
    private readonly object _gate = new();
    private readonly int? _maxRecordedQueries;
    private readonly TimeSpan _slowQueryThreshold;

    private int _duplicateCaptureCount;
    private int _executionCount;
    private int _discardedQueryCount;
    private int _slowQueryCount;
    private TimeSpan _totalDuration;
    private TimeSpan _maximumDuration;

    public QueryScope()
        : this(new QueryBudgetOptions())
    {
    }

    /// <param name="options">
    /// Supplies the retention cap and the slow-query threshold. Only those two are read; the limits
    /// themselves are evaluated later, against <see cref="Totals"/>.
    /// </param>
    public QueryScope(QueryBudgetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _maxRecordedQueries = options.MaxRecordedQueries;
        _slowQueryThreshold = options.SlowQueryThreshold;
    }

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

    /// <summary>
    /// Aggregates over every command the scope accepted, including those the cap kept it from
    /// retaining.
    /// </summary>
    public QueryCaptureTotals Totals
    {
        get
        {
            lock (_gate)
            {
                return new QueryCaptureTotals
                {
                    ExecutionCount = _executionCount,
                    DiscardedQueryCount = _discardedQueryCount,
                    TotalDuration = _totalDuration,
                    MaximumDuration = _maximumDuration,
                    SlowQueryCount = _slowQueryCount
                };
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

            _executionCount++;
            _totalDuration += query.Duration;
            if (query.Duration > _maximumDuration)
            {
                _maximumDuration = query.Duration;
            }

            if (query.Duration >= _slowQueryThreshold)
            {
                _slowQueryCount++;
            }

            // The first queries are kept rather than the last: a pattern shows itself from where
            // the scope started, and a sliding window would keep rewriting the evidence.
            if (_maxRecordedQueries is { } maximum && _queries.Count >= maximum)
            {
                _discardedQueryCount++;
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
