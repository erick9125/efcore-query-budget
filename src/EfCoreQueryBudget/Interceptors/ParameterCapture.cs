using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EfCoreQueryBudget;

/// <summary>
/// Projects a live <c>DbParameter</c> value into something safe to hold for the life of a scope.
/// </summary>
/// <remarks>
/// Values arrive as references into the caller's state. Keeping them would pin large payloads for
/// the whole scope, leave personal data resident longer than the command that used it, and let a
/// mutable value change between the command running and the fingerprint being computed — moving an
/// already-executed query into a different group. Immutable scalars are cheap and stable, so they
/// are kept as they are; everything else is replaced by a <see cref="ParameterSnapshot"/>.
/// </remarks>
internal static class ParameterCapture
{
    private const int MaxTextLength = 256;

    // Fed between elements so that ["ab", "c"] and ["a", "bc"] do not hash alike.
    private static readonly byte[] ElementSeparator = [0x1F];

    public static object? Capture(object? value)
    {
        switch (value)
        {
            case null or DBNull:
                return null;

            // Immutable and small. Keeping the original type is what lets reports still say
            // "Int32" or "Guid" instead of collapsing everything to text.
            case bool or char
                or byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal
                or Guid or DateTime or DateTimeOffset or TimeSpan or DateOnly or TimeOnly
                or Enum:
                return value;

            case string text:
                return text.Length <= MaxTextLength
                    ? text
                    : new ParameterSnapshot
                    {
                        TypeName = "String",
                        Text = Quote(Truncate(text)),
                        ContentHash = Hash(text)
                    };

            case byte[] bytes:
                return new ParameterSnapshot
                {
                    TypeName = $"byte[{bytes.Length}]",
                    Text = null,
                    ContentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
                };

            case Array array:
                return FromArray(array);

            default:
                return FromOpaque(value);
        }
    }

    /// <summary>
    /// Array-valued parameters, which providers use for <c>= ANY(@p)</c> and similar. Rendering
    /// them through <c>ToString</c> would be useless — every <c>int[]</c> renders as
    /// <c>System.Int32[]</c> — so the elements are hashed one by one and two different arrays stay
    /// different queries. The hash is incremental, so a large array is never held as one string.
    /// </summary>
    private static ParameterSnapshot FromArray(Array array)
    {
        var text = new StringBuilder("[");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var index = 0;
        foreach (var item in array)
        {
            var rendered = Render(item);
            hash.AppendData(Encoding.UTF8.GetBytes(rendered));
            hash.AppendData(ElementSeparator);

            if (text.Length <= MaxTextLength)
            {
                text.Append(index > 0 ? ", " : string.Empty).Append(rendered);
            }

            index++;
        }

        text.Append(']');

        return new ParameterSnapshot
        {
            TypeName = $"{array.GetType().GetElementType()?.Name ?? "object"}[{array.Length}]",
            Text = Truncate(text.ToString()),
            ContentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()
        };
    }

    /// <summary>
    /// Anything else: spatial values, provider-specific structs, custom types. Rendered once,
    /// invariantly, and hashed so that truncation cannot make two different values look alike.
    /// A type that does not override <c>ToString</c> cannot be told apart from another instance of
    /// itself, which is the limit of what is knowable without reflecting over its state.
    /// </summary>
    private static ParameterSnapshot FromOpaque(object value)
    {
        var text = Render(value);

        return new ParameterSnapshot
        {
            TypeName = value.GetType().Name,
            Text = Truncate(text),
            ContentHash = Hash(text)
        };
    }

    private static string Render(object? value)
    {
        return value switch
        {
            null => "null",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Truncate(string value)
    {
        if (value.Length <= MaxTextLength)
        {
            return value;
        }

        // Never cut between a surrogate pair, which would leave an unpaired code unit in the report.
        var length = char.IsHighSurrogate(value[MaxTextLength - 1])
            ? MaxTextLength - 1
            : MaxTextLength;

        return $"{value[..length]}...";
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
