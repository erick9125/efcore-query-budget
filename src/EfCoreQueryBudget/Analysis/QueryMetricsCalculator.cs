namespace EfCoreQueryBudget;

public sealed class QueryMetricsCalculator
{
    private readonly ExactDuplicateDetector _exactDuplicateDetector;
    private readonly RepeatedPatternDetector _repeatedPatternDetector;

    public QueryMetricsCalculator(
        ExactDuplicateDetector? exactDuplicateDetector = null,
        RepeatedPatternDetector? repeatedPatternDetector = null)
    {
        _exactDuplicateDetector = exactDuplicateDetector ?? new ExactDuplicateDetector();
        _repeatedPatternDetector = repeatedPatternDetector ?? new RepeatedPatternDetector();
    }

    public QueryMetrics Calculate(
        IReadOnlyList<RecordedQuery> queries,
        QueryBudgetOptions? options = null)
    {
        options ??= new QueryBudgetOptions();

        var exactDuplicateGroups = _exactDuplicateDetector.Detect(queries);
        var repeatedPatternGroups = _repeatedPatternDetector.Detect(
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
