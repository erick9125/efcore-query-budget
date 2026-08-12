using System.Text.RegularExpressions;

namespace EfCoreQueryBudget;

/// <summary>
/// Conservative SQL normalizer: trim and collapse whitespace only.
/// </summary>
public sealed partial class DefaultSqlNormalizer : ISqlNormalizer
{
    public string Normalize(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return WhitespaceRegex().Replace(sql.Trim(), " ");
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
