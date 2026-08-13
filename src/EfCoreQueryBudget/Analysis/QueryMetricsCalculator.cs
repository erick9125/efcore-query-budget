namespace EfCoreQueryBudget;

public sealed class QueryMetricsCalculator
{
    private readonly IQueryAnalysisFactory _analysis;

    public QueryMetricsCalculator()
        : this(new DefaultQueryAnalysisFactory())
    {
    }

    public QueryMetricsCalculator(IQueryAnalysisFactory analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        _analysis = analysis;
    }

    /// <param name="queries">The queries the scope retained.</param>
    /// <param name="options">
    /// The budget being measured against. It shapes the metrics — through the slow-query threshold,
    /// the repeat threshold and the normalization mode — so it travels on the result as
    /// <see cref="QueryMetrics.Budget"/> and is the only budget they can be evaluated against.
    /// </param>
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
        ArgumentNullException.ThrowIfNull(queries);
        options ??= new QueryBudgetOptions();

        // Memoized for this analysis only: both detectors ask for the exact fingerprint, and a
        // longer-lived cache would pin every query it ever saw.
        var fingerprinter = new MemoizingQueryFingerprinter(
            _analysis.CreateFingerprinter(options.SqlNormalization));

        var patternNormalizer = _analysis.CreateNormalizer(options.SqlNormalization);
        var exactNormalizer = _analysis.CreateNormalizer(SqlNormalizationMode.WhitespaceOnly);

        var exactDuplicateGroups =
            new ExactDuplicateDetector(exactNormalizer, fingerprinter).Detect(queries);

        var repeatedPatternGroups =
            new RepeatedPatternDetector(patternNormalizer, fingerprinter)
                .Detect(queries, options.RepeatedPatternThreshold);

        // Only reads reach the budget. Repeating a read with the same parameters returns the same
        // rows, so the extra execution is provably wasted; repeating a write is not — two identical
        // inserts add two rows. Writes stay in the groups below so the report can still show them.
        var redundantExecutionCount = exactDuplicateGroups
            .Where(g => g.Operation == QueryOperation.Read)
            .Sum(g => g.ExecutionCount - 1);

        var readPatternGroups = repeatedPatternGroups
            .Where(g => g.Operation == QueryOperation.Read)
            .ToArray();

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
            Budget = options,
            QueryCount = totals?.ExecutionCount ?? queries.Count,
            RedundantExecutionCount = redundantExecutionCount,
            RepeatedPatternCount = readPatternGroups.Length,
            MaximumPatternExecutions = readPatternGroups.Length == 0
                ? 0
                : readPatternGroups.Max(g => g.ExecutionCount),
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
