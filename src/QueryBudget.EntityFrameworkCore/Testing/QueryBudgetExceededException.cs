namespace ErickMorales.EntityFrameworkCore.QueryBudget;

public sealed class QueryBudgetExceededException : Exception
{
    public QueryBudgetResult Result { get; }

    public QueryBudgetExceededException(QueryBudgetResult result, string message)
        : base(message)
    {
        Result = result;
    }
}
