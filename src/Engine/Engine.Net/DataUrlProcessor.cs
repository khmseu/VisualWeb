using System.Text;
using VisualWeb.Core.Mime;
using VisualWeb.Core.Url;

namespace VisualWeb.Engine.Net;

internal static class DataUrlProcessor
{
    // Spec: fetch; https://fetch.spec.whatwg.org/#data-url-processor
    public static ResourceResponse Load(BrowserUrl url, int limit, CancellationToken cancellationToken)
    {
        var input = ResourceLoader.WithoutFragment(url);
        var comma = input.IndexOf(',', 5);
        if (comma < 0)
        {
            throw Invalid("Data URL requires a comma separating metadata and body.");
        }

        var metadata = input[5..comma].Trim('\t', '\n', '\f', '\r', ' ');
        var base64 = metadata.EndsWith("base64", StringComparison.OrdinalIgnoreCase)
            && metadata[..^6].TrimEnd(' ').EndsWith(';');
        if (base64)
        {
            metadata = metadata[..^6].TrimEnd(' ')[..^1];
        }

        if (metadata.StartsWith(';'))
        {
            metadata = "text/plain" + metadata;
        }

        var parsed = MimeType.ParseResult(metadata);
        var diagnostics = new List<string>();
        if (!parsed.Success)
        {
            diagnostics.Add("Data URL MIME type defaulted to text/plain;charset=US-ASCII per Fetch.");
        }

        var mime = parsed.Value ?? MimeType.Parse("text/plain;charset=US-ASCII");
        byte[] body;
        if (base64)
        {
            var encoded = new StringBuilder();
            var maxEncoded = ((long)limit + 2) / 3 * 4;
            foreach (var b in PercentDecode(input, comma + 1, cancellationToken))
            {
                if (b is 0x09 or 0x0A or 0x0C or 0x0D or 0x20)
                {
                    continue;
                }

                if (b > 0x7F)
                {
                    throw Invalid("Data URL base64 contains a non-ASCII byte.");
                }

                if (encoded.Length >= maxEncoded)
                {
                    throw ResourceLoader.BodyLimit();
                }

                encoded.Append((char)b);
            }

            // Forgiving base64 accepts missing padding, but not extra or embedded '='.
            var text = encoded.ToString();
            if (text.Length % 4 == 0)
            {
                text = text.EndsWith("==", StringComparison.Ordinal) ? text[..^2]
                    : text.EndsWith('=') ? text[..^1] : text;
            }

            if (text.Length % 4 == 1 || text.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('+' or '/')))
            {
                throw Invalid("Data URL has invalid forgiving-base64 content.");
            }

            body = Convert.FromBase64String(text.PadRight((text.Length + 3) / 4 * 4, '='));
            if (body.Length > limit)
            {
                throw ResourceLoader.BodyLimit();
            }
        }
        else
        {
            using var buffer = new MemoryStream();
            foreach (var b in PercentDecode(input, comma + 1, cancellationToken))
            {
                if (buffer.Length == limit)
                {
                    throw ResourceLoader.BodyLimit();
                }

                buffer.WriteByte(b);
            }

            body = buffer.ToArray();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new(url, 200, false, new(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = Array.AsReadOnly(new[] { mime.Serialize() })
        }, mime, body, diagnostics);
    }

    private static IEnumerable<byte> PercentDecode(string input, int start, CancellationToken cancellationToken)
    {
        var processed = 0;
        for (var i = start; i < input.Length; i++)
        {
            if ((processed++ & 4095) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (input[i] == '%' && i + 2 < input.Length && Hex(input[i + 1]) is var high && high >= 0
                && Hex(input[i + 2]) is var low && low >= 0)
            {
                yield return (byte)(high * 16 + low);
                i += 2;
            }
            else if (input[i] < 0x80)
            {
                yield return (byte)input[i];
            }
            else
            {
                var rune = char.IsHighSurrogate(input[i]) && i + 1 < input.Length
                    && Rune.TryCreate(input[i], input[i + 1], out var pair) ? pair
                    : Rune.TryCreate(input[i], out var single) ? single : Rune.ReplacementChar;
                i += rune.Utf16SequenceLength - 1;
                foreach (var b in Encoding.UTF8.GetBytes(rune.ToString()))
                {
                    yield return b;
                }
            }
        }
    }

    private static int Hex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => -1
    };

    private static ResourceLoadException Invalid(string message) => new(ResourceError.InvalidDataUrl, message);
}
