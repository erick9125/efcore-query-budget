using System.Text.RegularExpressions;

namespace EfCoreQueryBudget;

/// <summary>
/// SQL normalizer. Trims and collapses whitespace, and optionally masks inline literals so that
/// executions differing only in a literal share one structural fingerprint.
/// </summary>
public sealed partial class DefaultSqlNormalizer : ISqlNormalizer
{
    private readonly SqlNormalizationMode _mode;

    public DefaultSqlNormalizer(SqlNormalizationMode mode = SqlNormalizationMode.WhitespaceOnly)
    {
        _mode = mode;
    }

    public string Normalize(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        return _mode == SqlNormalizationMode.MaskLiterals
            ? SqlLiteralMasker.Mask(sql)
            : WhitespaceRegex().Replace(sql.Trim(), " ");
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
