using System.Collections.ObjectModel;
using System.Text;

namespace VisualWeb.Core.Mime;

/// <summary>A MIME record with ordered, first-occurrence parameters.</summary>
/// <remarks>Spec: mime-sniffing; <see href="https://mimesniff.spec.whatwg.org/#parsing-a-mime-type">MIME parsing</see>
/// and <see href="https://mimesniff.spec.whatwg.org/#serializing-a-mime-type">serialization</see>.</remarks>
public sealed class MimeType
{
    public string Type { get; }
    public string Subtype { get; }
    public string Essence => Type + "/" + Subtype;
    public IReadOnlyDictionary<string, string> Parameters { get; }

    private MimeType(string type, string subtype, Dictionary<string, string> parameters)
    {
        Type = type;
        Subtype = subtype;
        Parameters = new ReadOnlyDictionary<string, string>(parameters);
    }

    public static MimeParseResult ParseResult(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var text = input.AsSpan().Trim("\t\n\r ");
        var slash = text.IndexOf('/');
        if (slash <= 0 || !IsToken(text[..slash]))
        {
            return new(null, "MIME type must begin with an HTTP token followed by '/'.");
        }

        var type = text[..slash].ToString().ToLowerInvariant();
        text = text[(slash + 1)..];
        var semicolon = text.IndexOf(';');
        var subtype = (semicolon < 0 ? text : text[..semicolon]).TrimEnd("\t\n\r ");
        if (subtype.IsEmpty || !IsToken(subtype))
        {
            return new(null, "MIME subtype must be a nonempty HTTP token.");
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var remaining = semicolon < 0 ? ReadOnlySpan<char>.Empty : text[semicolon..];
        while (!remaining.IsEmpty)
        {
            remaining = remaining[1..].TrimStart("\t\n\r ");
            var nameEnd = 0;
            while (nameEnd < remaining.Length && remaining[nameEnd] is not (';' or '='))
            {
                nameEnd++;
            }

            var name = remaining[..nameEnd].ToString().ToLowerInvariant();
            remaining = remaining[nameEnd..];
            if (remaining.IsEmpty)
            {
                break;
            }

            if (remaining[0] == ';')
            {
                continue;
            }

            remaining = remaining[1..];
            string value;
            if (!remaining.IsEmpty && remaining[0] == '"')
            {
                remaining = remaining[1..];
                var buffer = new StringBuilder();
                var position = 0;
                while (position < remaining.Length)
                {
                    var c = remaining[position++];
                    if (c == '"')
                    {
                        break;
                    }

                    if (c == '\\' && position < remaining.Length)
                    {
                        c = remaining[position++];
                    }

                    buffer.Append(c);
                }

                value = buffer.ToString();
                remaining = remaining[position..];
                var next = remaining.IndexOf(';');
                remaining = next < 0 ? [] : remaining[next..];
            }
            else
            {
                var next = remaining.IndexOf(';');
                value = (next < 0 ? remaining : remaining[..next]).TrimEnd("\t\n\r ").ToString();
                remaining = next < 0 ? [] : remaining[next..];
                if (value.Length == 0)
                {
                    continue;
                }
            }

            if (name.Length > 0 && IsToken(name) && value.All(IsQuotedValueCharacter))
            {
                parameters.TryAdd(name, value);
            }
        }

        return new(new(type, subtype.ToString().ToLowerInvariant(), parameters), null);
    }

    public static MimeType Parse(string input)
    {
        var result = ParseResult(input);
        return result.Value ?? throw new FormatException(result.Error);
    }

    public string Serialize()
    {
        var output = new StringBuilder(Essence);
        foreach (var (name, value) in Parameters)
        {
            output.Append(';').Append(name).Append('=');
            if (value.Length > 0 && IsToken(value))
            {
                output.Append(value);
            }
            else
            {
                output.Append('"');
                foreach (var c in value)
                {
                    if (c is '"' or '\\')
                    {
                        output.Append('\\');
                    }

                    output.Append(c);
                }

                output.Append('"');
            }
        }

        return output.ToString();
    }

    private static bool IsToken(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('!' or '#' or '$' or '%' or '&' or '\'' or '*'
                or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsQuotedValueCharacter(char c) => c is '\t' or >= ' ' and <= '~' or >= '\u0080' and <= '\u00FF';
    public override string ToString() => Serialize();
}

public sealed record MimeParseResult(MimeType? Value, string? Error)
{
    public bool Success => Value is not null;
}
