namespace ErickMorales.EntityFrameworkCore.QueryBudget;

public enum QueryBudgetViolationType
{
    QueryCountExceeded,
    ExactDuplicatesExceeded,
    RepeatedPatternsExceeded,
    SlowQueriesExceeded,
    TotalDurationExceeded,
    SingleQueryDurationExceeded
}
