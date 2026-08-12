namespace EfCoreQueryBudget;

public sealed class QueryMetricsCalculator
{
    private readonly ExactDuplicateDetector? _injectedExactDuplicateDetector;
    private readonly RepeatedPatternDetector? _injectedRepeatedPatternDetector;

    public QueryMetricsCalculator(
        ExactDuplicateDetector? exactDuplicateDetector = null,
        RepeatedPatternDetector? repeatedPatternDetector = null)
    {
        _injectedExactDuplicateDetector = exactDuplicateDetector;
        _injectedRepeatedPatternDetector = repeatedPatternDetector;
    }

    /// <param name="queries">The queries the scope retained.</param>
    /// <param name="options">The budget being measured against.</param>
    /// <param name="totals">
    /// What the scope counted across every command, including any it could not retain. When given,
    /// the aggregate metrics come from here instead of from <paramref name="queries"/>, so a scope
    /// that ran past its retention cap is still evaluated against everything that ran.
    /// </param>
    public QueryMetrics Calculate(
        IReadOnlyList<RecordedQuery> queries,
        QueryBudgetOptions? options = null,
        QueryCaptureTotals? totals = null)
    {
        options ??= new QueryBudgetOptions();

        // The pattern detector depends on the normalization mode, which only arrives with the
        // options, so it is composed per call. An injected detector wins over the option.
        var repeatedPatternDetector = _injectedRepeatedPatternDetector
            ?? new RepeatedPatternDetector(new DefaultSqlNormalizer(options.SqlNormalization));

        // Exact duplicates never mask: two queries differing in a literal are not the same query.
        var exactDuplicateDetector = _injectedExactDuplicateDetector ?? new ExactDuplicateDetector();

        var exactDuplicateGroups = exactDuplicateDetector.Detect(queries);
        var repeatedPatternGroups = repeatedPatternDetector.Detect(
            queries,
            options.RepeatedPatternThreshold);

        var exactDuplicateCount = exactDuplicateGroups.Sum(g => g.ExecutionCount - 1);
        var totalDuration = TimeSpan.Zero;
        var maximumDuration = TimeSpan.Zero;
        var slowQueryCount = 0;

        foreach (var query in queries)
        {
            totalDuration += query.Duration;
            if (query.Duration > maximumDuration)
            {
                maximumDuration = query.Duration;
            }

            if (query.Duration >= options.SlowQueryThreshold)
            {
                slowQueryCount++;
            }
        }

        return new QueryMetrics
        {
            QueryCount = totals?.ExecutionCount ?? queries.Count,
            ExactDuplicateCount = exactDuplicateCount,
            RepeatedPatternCount = repeatedPatternGroups.Count,
            SlowQueryCount = totals?.SlowQueryCount ?? slowQueryCount,
            TotalDuration = totals?.TotalDuration ?? totalDuration,
            MaximumDuration = totals?.MaximumDuration ?? maximumDuration,
            DiscardedQueryCount = totals?.DiscardedQueryCount ?? 0,
            ExactDuplicateGroups = exactDuplicateGroups,
            RepeatedPatternGroups = repeatedPatternGroups,
            Queries = queries
        };
    }
}
