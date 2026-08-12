namespace EfCoreQueryBudget;

public interface IQueryReportFormatter
{
    string Format(QueryBudgetResult result);
}
