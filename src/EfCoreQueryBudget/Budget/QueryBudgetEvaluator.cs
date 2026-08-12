namespace EfCoreQueryBudget;

/// <summary>
/// Pure budget evaluation with no EF Core dependency.
/// </summary>
public sealed class QueryBudgetEvaluator
{
    public QueryBudgetResult Evaluate(QueryBudgetOptions budget, QueryMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(metrics);

        var violations = new List<QueryBudgetViolation>();

        if (budget.MaxQueries is int maxQueries && metrics.QueryCount > maxQueries)
        {
            violations.Add(new QueryBudgetViolation(
                QueryBudgetViolationType.QueryCountExceeded,
                maxQueries,
                metrics.QueryCount,
                "Query count"));
        }

        if (budget.MaxExactDuplicates is int maxExact
            && metrics.ExactDuplicateCount > maxExact)
        {
            violations.Add(new QueryBudgetViolation(
                QueryBudgetViolationType.ExactDuplicatesExceeded,
                maxExact,
                metrics.ExactDuplicateCount,
                "Exact duplicates"));
        }

        if (budget.MaxRepeatedPatterns is int maxPatterns
            && metrics.RepeatedPatternCount > maxPatterns)
        {
            violations.Add(new QueryBudgetViolation(
                QueryBudgetViolationType.RepeatedPatternsExceeded,
                maxPatterns,
                metrics.RepeatedPatternCount,
                "Repeated query patterns"));
        }

        if (budget.MaxExecutionsPerPattern is int maxPerPattern
            && metrics.MaximumPatternExecutions > maxPerPattern)
        {
            violations.Add(new QueryBudgetViolation(
                QueryBudgetViolationType.PatternExecutionsExceeded,
                maxPerPattern,
                metrics.MaximumPatternExecutions,
                "Executions in a single pattern"));
        }

        if (budget.MaxSlowQueries is int maxSlow && metrics.SlowQueryCount > maxSlow)
        {
            violations.Add(new QueryBudgetViolation(
                QueryBudgetViolationType.SlowQueriesExceeded,
                maxSlow,
                metrics.SlowQueryCount,
                "Slow queries"));
        }

        if (budget.MaxTotalDuration is TimeSpan maxTotal
            && metrics.TotalDuration > maxTotal)
        {
            violations.Add(new QueryBudgetViolation(
                QueryBudgetViolationType.TotalDurationExceeded,
                maxTotal,
                metrics.TotalDuration,
                "Total database time"));
        }

        if (budget.MaxSingleQueryDuration is TimeSpan maxSingle
            && metrics.MaximumDuration > maxSingle)
        {
            violations.Add(new QueryBudgetViolation(
                QueryBudgetViolationType.SingleQueryDurationExceeded,
                maxSingle,
                metrics.MaximumDuration,
                "Single query duration"));
        }

        return new QueryBudgetResult
        {
            Passed = violations.Count == 0,
            Budget = budget,
            Metrics = metrics,
            Violations = violations
        };
    }
}
