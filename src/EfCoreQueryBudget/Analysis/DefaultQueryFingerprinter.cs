using System.Globalization;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EfCoreQueryBudget;

public sealed class DefaultQueryFingerprinter : IQueryFingerprinter
{
    private readonly ISqlNormalizer _structuralNormalizer;
    private readonly ISqlNormalizer _exactNormalizer;

    /// <param name="structuralNormalizer">
    /// Normalizes SQL for pattern grouping. This is where literal masking belongs.
    /// </param>
    /// <param name="exactNormalizer">
    /// Normalizes SQL for exact-duplicate grouping. Masking literals here would report
    /// <c>WHERE id = 1</c> and <c>WHERE id = 2</c> as the same query, so callers pass a
    /// whitespace-only normalizer.
    /// </param>
    public DefaultQueryFingerprinter(
        ISqlNormalizer structuralNormalizer,
        ISqlNormalizer exactNormalizer)
    {
        ArgumentNullException.ThrowIfNull(structuralNormalizer);
        ArgumentNullException.ThrowIfNull(exactNormalizer);

        _structuralNormalizer = structuralNormalizer;
        _exactNormalizer = exactNormalizer;
    }

    public string StructuralFingerprint(RecordedQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Hash(_structuralNormalizer.Normalize(query.CommandText));
    }

    public string ExactFingerprint(RecordedQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var payload = JsonSerializer.Serialize(new
        {
            sql = _exactNormalizer.Normalize(query.CommandText),
            parameters = ParameterNormalizer.Normalize(query.Parameters)
        });
        return Hash(payload);
    }

    /// <summary>
    /// Groups queries; it is not a security boundary, so it uses a fast non-cryptographic hash
    /// rather than SHA-256 and a 64-character hex string per query. The content hash in
    /// <c>ParameterCapture</c> stays on SHA-256, where it stands in for a value that is never shown.
    /// </summary>
    private static string Hash(string value)
    {
        var bytes = XxHash128.Hash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

internal static class ParameterNormalizer
{
    public static IReadOnlyDictionary<string, object?> Normalize(
        IReadOnlyDictionary<string, object?> parameters)
    {
        var normalized = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in parameters)
        {
            normalized[pair.Key] = NormalizeValue(pair.Value);
        }

        return normalized;
    }

    public static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            bool b => b,
            byte or sbyte or short or ushort or int or uint or long or ulong => value,
            float f => f.ToString("G9", CultureInfo.InvariantCulture),
            double d => d.ToString("G17", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            Guid g => g.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            DateTime dt => DateTime.SpecifyKind(
                    dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime(),
                    DateTimeKind.Utc)
                .ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            // Values captured from a live command arrive already projected. The hash is what keeps
            // two long values sharing a prefix from fingerprinting alike.
            ParameterSnapshot snapshot => new Dictionary<string, object?>
            {
                ["__type"] = snapshot.TypeName,
                ["text"] = snapshot.Text,
                ["hash"] = snapshot.ContentHash
            },
            byte[] bytes => new Dictionary<string, object>
            {
                ["__type"] = "byte[]",
                ["length"] = bytes.Length,
                ["hash"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
            },
            Enum e => new Dictionary<string, object>
            {
                ["__type"] = "enum",
                ["type"] = e.GetType().FullName ?? e.GetType().Name,
                ["name"] = e.ToString(),
                ["value"] = Convert.ToInt64(e, CultureInfo.InvariantCulture)
            },
            // Invariant where the type allows it: a culture-sensitive ToString would give the same
            // value a different fingerprint on a machine with a different locale.
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
