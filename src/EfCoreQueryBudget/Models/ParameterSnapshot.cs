namespace EfCoreQueryBudget;

/// <summary>
/// An immutable stand-in for a parameter value that must not be retained by reference: a large
/// payload, a mutable one, or a value of an unknown type.
/// </summary>
/// <remarks>
/// Taken at the moment of capture. Holding the original would keep it alive for the whole scope and
/// let it change between the command running and the report being built, which would move a query
/// into a different fingerprint group after the fact.
/// </remarks>
public sealed record ParameterSnapshot
{
    /// <summary>
    /// The value's type as reports show it: <c>byte[1024]</c>, <c>String</c>, <c>Int32[]</c>.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Display text, already truncated. <see langword="null"/> for payloads that must never be
    /// printed, such as binary ones.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Hash of the full value, set whenever <see cref="Text"/> does not represent it completely.
    /// Without it, two long values sharing a prefix would fingerprint alike and be reported as the
    /// same query.
    /// </summary>
    public string? ContentHash { get; init; }
}
