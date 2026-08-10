namespace ErickMorales.EntityFrameworkCore.QueryBudget;

public interface IQueryReportFormatter
{
    string Format(QueryBudgetResult result);
}
