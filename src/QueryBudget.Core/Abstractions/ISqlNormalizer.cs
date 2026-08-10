namespace ErickMorales.EntityFrameworkCore.QueryBudget;

public interface ISqlNormalizer
{
    string Normalize(string sql);
}
