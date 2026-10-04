using System.Text;
using System.Text.Json;

namespace VisualWeb.Core.Encoding;

/// <summary>Whole-buffer decoders and label resolution from the Encoding Standard.</summary>
/// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#concept-encoding-get">get an encoding</see>
/// and <see href="https://encoding.spec.whatwg.org/#decode">decode</see>.
/// Raw Decode does not sniff or strip a BOM; DecodeWithBom implements BOM precedence.</remarks>
public sealed class WebEncoding
{
    private static readonly IReadOnlyDictionary<string, WebEncoding> Labels = LoadLabels();
    internal static readonly IReadOnlyDictionary<string, int?[]> Indexes = LoadIndexes();
    public string Name { get; }
    private WebEncoding(string name) => Name = name;

    public static WebEncoding ForLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        var normalized = label.AsSpan().Trim("\t\n\f\r ").ToString().ToLowerInvariant();
        return Labels.TryGetValue(normalized, out var encoding) ? encoding
            : throw new ArgumentException($"Unknown web encoding label: {label}", nameof(label));
    }

    public string Decode(ReadOnlySpan<byte> bytes, bool fatal = false)
    {
        if (Name == "UTF-8")
        {
            try
            {
                return new UTF8Encoding(false, fatal).GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new WebDecodingException(Name, exception.Message);
            }
        }

        if (Name is "UTF-16LE" or "UTF-16BE")
        {
            return DecodeUtf16(bytes, Name == "UTF-16BE", fatal);
        }

        if (Name == "replacement")
        {
            return bytes.IsEmpty ? "" : Error(fatal);
        }

        var output = new StringBuilder();
        if (Name == "x-user-defined")
        {
            foreach (var b in bytes)
            {
                output.Append((char)(b < 0x80 ? b : 0xF780 + b - 0x80));
            }

            return output.ToString();
        }

        var indexName = Name == "ISO-8859-8-I" ? "iso-8859-8" : Name.ToLowerInvariant();
        if (Indexes.TryGetValue(indexName, out var index) && index.Length == 128)
        {
            foreach (var b in bytes)
            {
                output.Append(b < 0x80 ? ((char)b).ToString()
                    : index[b - 0x80] is { } point ? char.ConvertFromUtf32(point) : Error(fatal));
            }

            return output.ToString();
        }

        return LegacyDecoder.Decode(bytes, Name, fatal);
    }

    public static DecodedText DecodeWithBom(ReadOnlySpan<byte> bytes, WebEncoding fallback, bool fatal = false)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        var (encoding, skip) = bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? (ForLabel("UTF-8"), 3)
            : bytes.StartsWith(new byte[] { 0xFF, 0xFE }) ? (ForLabel("UTF-16LE"), 2)
            : bytes.StartsWith(new byte[] { 0xFE, 0xFF }) ? (ForLabel("UTF-16BE"), 2) : (fallback, 0);
        return new(encoding, encoding.Decode(bytes[skip..], fatal));
    }

    internal static string Error(bool fatal)
    {
        if (fatal)
        {
            throw new WebDecodingException("Decoder", "Invalid byte sequence.");
        }

        return "\uFFFD";
    }

    private static string DecodeUtf16(ReadOnlySpan<byte> bytes, bool bigEndian, bool fatal)
    {
        var output = new StringBuilder();
        var position = 0;
        while (position + 1 < bytes.Length)
        {
            var unit = ReadUnit(bytes, position, bigEndian);
            position += 2;
            if (char.IsHighSurrogate(unit))
            {
                if (position + 1 < bytes.Length && char.IsLowSurrogate(ReadUnit(bytes, position, bigEndian)))
                {
                    output.Append(unit).Append(ReadUnit(bytes, position, bigEndian));
                    position += 2;
                }
                else
                {
                    output.Append(Error(fatal));
                    if (position + 1 >= bytes.Length)
                    {
                        position = bytes.Length;
                    }
                }
            }
            else
            {
                output.Append(char.IsLowSurrogate(unit) ? Error(fatal) : unit.ToString());
            }
        }

        if (position < bytes.Length)
        {
            output.Append(Error(fatal));
        }

        return output.ToString();
    }

    private static char ReadUnit(ReadOnlySpan<byte> bytes, int position, bool bigEndian) =>
        (char)(bigEndian ? bytes[position] << 8 | bytes[position + 1] : bytes[position] | bytes[position + 1] << 8);

    private static IReadOnlyDictionary<string, WebEncoding> LoadLabels()
    {
        using var stream = OpenData("encodings.json");
        using var document = JsonDocument.Parse(stream);
        var labels = new Dictionary<string, WebEncoding>(StringComparer.Ordinal);
        foreach (var group in document.RootElement.EnumerateArray())
        {
            foreach (var entry in group.GetProperty("encodings").EnumerateArray())
            {
                var encoding = new WebEncoding(entry.GetProperty("name").GetString()!);
                foreach (var label in entry.GetProperty("labels").EnumerateArray())
                {
                    labels.Add(label.GetString()!, encoding);
                }
            }
        }

        return labels;
    }

    private static IReadOnlyDictionary<string, int?[]> LoadIndexes()
    {
        using var stream = OpenData("indexes.json");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject()
            .Where(entry => entry.Name != "gb18030-ranges")
            .ToDictionary(entry => entry.Name, entry => entry.Value.EnumerateArray()
                .Select(value => value.ValueKind == JsonValueKind.Null ? (int?)null : value.GetInt32()).ToArray());
    }

    internal static Stream OpenData(string name) =>
        typeof(WebEncoding).Assembly.GetManifestResourceStream("VisualWeb.Core.Encoding.Data." + name)
        ?? throw new InvalidOperationException($"Embedded encoding data is missing: {name}");
}

public sealed record DecodedText(WebEncoding Encoding, string Text);
public sealed class WebDecodingException(string encoding, string message) : FormatException($"{encoding}: {message}");
