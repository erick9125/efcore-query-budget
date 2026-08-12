namespace EfCoreQueryBudget;

/// <summary>
/// Builds the analysis pipeline for a normalization mode.
/// </summary>
/// <remarks>
/// The mode arrives with the budget, in the call, while the normalizer and the fingerprinter are
/// needed to build the detectors. This factory is the seam between the two, and the way to put a
/// custom <see cref="ISqlNormalizer"/> or <see cref="IQueryFingerprinter"/> into the pipeline:
/// supply your own and hand it to <see cref="QueryBudgetRunner"/>.
/// </remarks>
public interface IQueryAnalysisFactory
{
    /// <summary>
    /// The normalizer used to group queries and to render the SQL a group reports.
    /// </summary>
    ISqlNormalizer CreateNormalizer(SqlNormalizationMode mode);

    /// <summary>
    /// The fingerprinter used to group queries. Implementations must keep literals out of the
    /// structural fingerprint only: the exact one has to keep them, or two queries differing in a
    /// value would be reported as the same query.
    /// </summary>
    IQueryFingerprinter CreateFingerprinter(SqlNormalizationMode mode);
}
