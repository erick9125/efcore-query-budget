namespace EfCoreQueryBudget;

public enum QueryBudgetViolationType
{
    QueryCountExceeded,
    ExactDuplicatesExceeded,
    RepeatedPatternsExceeded,
    PatternExecutionsExceeded,
    SlowQueriesExceeded,
    TotalDurationExceeded,
    SingleQueryDurationExceeded
}
