namespace EfCoreQueryBudget;

/// <summary>
/// How SQL is normalized before a structural fingerprint is computed.
/// </summary>
public enum SqlNormalizationMode
{
    /// <summary>
    /// Trim and collapse whitespace only. Two executions that differ in an inline literal
    /// get different structural fingerprints, so they are never grouped as one pattern.
    /// </summary>
    WhitespaceOnly = 0,

    /// <summary>
    /// Collapse whitespace, replace inline string and numeric literals with <c>?</c>, and
    /// collapse variable-length <c>IN</c> lists. Required to detect repeated patterns in raw
    /// SQL, in <c>FromSqlRaw</c>, or wherever the provider inlines constants instead of
    /// parameterizing them. Note that literals which carry meaning collapse too, so
    /// <c>LIMIT 10</c> and <c>LIMIT 20</c> become the same pattern.
    /// </summary>
    MaskLiterals = 1
}
