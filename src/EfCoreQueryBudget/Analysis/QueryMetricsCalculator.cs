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

    public QueryMetrics Calculate(
        IReadOnlyList<RecordedQuery> queries,
        QueryBudgetOptions? options = null)
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
            QueryCount = queries.Count,
            ExactDuplicateCount = exactDuplicateCount,
            RepeatedPatternCount = repeatedPatternGroups.Count,
            SlowQueryCount = slowQueryCount,
            TotalDuration = totalDuration,
            MaximumDuration = maximumDuration,
            ExactDuplicateGroups = exactDuplicateGroups,
            RepeatedPatternGroups = repeatedPatternGroups,
            Queries = queries
        };
    }
}
