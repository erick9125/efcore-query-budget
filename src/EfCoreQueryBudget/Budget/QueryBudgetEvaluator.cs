namespace EfCoreQueryBudget;

/// <summary>
/// Pure budget evaluation with no EF Core dependency.
/// </summary>
public sealed class QueryBudgetEvaluator
{
    /// <param name="metrics">
    /// The metrics to score, which carry the budget they were computed against. Taking the budget
    /// from here rather than as a second argument is what stops metrics from being scored against a
    /// budget that did not produce them.
    /// </param>
    public QueryBudgetResult Evaluate(QueryMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var budget = metrics.Budget;
        var violations = new List<QueryBudgetViolation>();

        void Count(int? limit, int actual, QueryBudgetViolationType type)
        {
            if (limit is int maximum && actual > maximum)
            {
                violations.Add(new CountBudgetViolation(type, maximum, actual));
            }
        }

        void Duration(TimeSpan? limit, TimeSpan actual, QueryBudgetViolationType type)
        {
            if (limit is TimeSpan maximum && actual > maximum)
            {
                violations.Add(new DurationBudgetViolation(type, maximum, actual));
            }
        }

        Count(
            budget.MaxQueries,
            metrics.QueryCount,
            QueryBudgetViolationType.QueryCountExceeded);

        Count(
            budget.MaxExactDuplicates,
            metrics.ExactDuplicateCount,
            QueryBudgetViolationType.ExactDuplicatesExceeded);

        Count(
            budget.MaxRepeatedPatterns,
            metrics.RepeatedPatternCount,
            QueryBudgetViolationType.RepeatedPatternsExceeded);

        Count(
            budget.MaxExecutionsPerPattern,
            metrics.MaximumPatternExecutions,
            QueryBudgetViolationType.PatternExecutionsExceeded);

        Count(
            budget.MaxSlowQueries,
            metrics.SlowQueryCount,
            QueryBudgetViolationType.SlowQueriesExceeded);

        Duration(
            budget.MaxTotalDuration,
            metrics.TotalDuration,
            QueryBudgetViolationType.TotalDurationExceeded);

        Duration(
            budget.MaxSingleQueryDuration,
            metrics.MaximumDuration,
            QueryBudgetViolationType.SingleQueryDurationExceeded);

        return new QueryBudgetResult
        {
            Passed = violations.Count == 0,
            Metrics = metrics,
            Violations = violations
        };
    }
}
