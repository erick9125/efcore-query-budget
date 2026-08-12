namespace EfCoreQueryBudget;

/// <summary>
/// Composes the default analysis pipeline. This is the one place that states the rule the two
/// fingerprints follow: the structural one normalizes according to the mode, the exact one is
/// always whitespace-only.
/// </summary>
public sealed class DefaultQueryAnalysisFactory : IQueryAnalysisFactory
{
    public ISqlNormalizer CreateNormalizer(SqlNormalizationMode mode)
        => new DefaultSqlNormalizer(mode);

    public IQueryFingerprinter CreateFingerprinter(SqlNormalizationMode mode)
    {
        return new DefaultQueryFingerprinter(
            CreateNormalizer(mode),
            // Masking here would report `WHERE id = 1` and `WHERE id = 2` as the same query.
            new DefaultSqlNormalizer(SqlNormalizationMode.WhitespaceOnly));
    }
}
