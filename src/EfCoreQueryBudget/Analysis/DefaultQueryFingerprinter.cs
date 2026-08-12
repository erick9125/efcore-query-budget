using System.Globalization;
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
        ISqlNormalizer? structuralNormalizer = null,
        ISqlNormalizer? exactNormalizer = null)
    {
        _structuralNormalizer = structuralNormalizer ?? new DefaultSqlNormalizer();
        _exactNormalizer = exactNormalizer ?? new DefaultSqlNormalizer();
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

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
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
            _ => value.ToString()
        };
    }
}
