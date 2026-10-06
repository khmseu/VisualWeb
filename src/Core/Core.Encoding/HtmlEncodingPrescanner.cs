using System.Text;

namespace VisualWeb.Core.Encoding;

/// <summary>Which declaration produced a prescan result.</summary>
public enum HtmlPrescanSource
{
    /// <summary>The prescan and the XML-declaration fallback both failed.</summary>
    None,

    /// <summary>A UTF-16LE/BE <c>&lt;?x</c> prefix at the start of the bytes.</summary>
    Utf16XmlPrefix,

    /// <summary>A <c>meta</c> element's <c>charset</c> attribute.</summary>
    MetaCharset,

    /// <summary>A <c>meta</c> element's <c>content</c> attribute with <c>http-equiv="content-type"</c>.</summary>
    MetaPragma,

    /// <summary>The <c>encoding</c> pseudo-attribute of a leading <c>&lt;?xml</c> declaration.</summary>
    XmlDeclaration,
}

/// <summary>Result of <see cref="HtmlEncodingPrescanner.Prescan"/>.</summary>
/// <param name="Encoding">The returned encoding, or null for failure.</param>
/// <param name="Source">The declaration kind that produced <paramref name="Encoding"/>.</param>
/// <param name="DeclarationOffset">Byte offset of the declaring <c>&lt;</c>, or null for failure.</param>
/// <param name="ScannedByteCount">Bytes made available to the prescan (at most <see cref="HtmlEncodingPrescanner.ByteLimit"/>).</param>
/// <param name="IgnoredLabels">Labels found in meta declarations that did not resolve to an encoding, in input order.</param>
public sealed record HtmlPrescanResult(WebEncoding? Encoding, HtmlPrescanSource Source, int? DeclarationOffset,
    int ScannedByteCount, IReadOnlyList<string> IgnoredLabels);

/// <summary>Byte-level HTML character encoding declaration prescan.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#prescan-a-byte-stream-to-determine-its-encoding">prescan a byte stream to determine its encoding</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#concept-get-attributes-when-sniffing">get an attribute</see> and
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#concept-get-xml-encoding-when-sniffing">get an XML encoding</see>.
/// The end condition is the spec-encouraged first <see cref="ByteLimit"/> bytes: a declaration that is incomplete at that
/// boundary aborts the prescan exactly as if the bytes had run out, and later bytes are never read. The input is only read;
/// work is bounded by the byte limit, so no cancellation token is taken.</remarks>
public static class HtmlEncodingPrescanner
{
    /// <summary>The prescan end condition, in bytes (not characters).</summary>
    public const int ByteLimit = 1024;

    private enum AttributeStatus { Attribute, None, OutOfBytes }

    public static HtmlPrescanResult Prescan(ReadOnlySpan<byte> bytes)
    {
        var input = bytes[..Math.Min(bytes.Length, ByteLimit)];
        var ignored = new List<string>();
        var (encoding, source, offset) = ScanDeclarations(input, ignored);
        if (encoding is null)
        {
            encoding = GetXmlEncoding(input);
            (source, offset) = encoding is null ? (HtmlPrescanSource.None, (int?)null) : (HtmlPrescanSource.XmlDeclaration, 0);
        }

        return new(encoding, source, offset, input.Length, ignored.AsReadOnly());
    }

    /// <summary>Extracts an encoding from a <c>meta</c> <c>content</c> value, or returns null for "nothing".</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/urls-and-fetching.html#algorithm-for-extracting-a-character-encoding-from-a-meta-element">algorithm for extracting a character encoding from a meta element</see>.
    /// An unrecognized label (get an encoding failure) is also returned as null.</remarks>
    public static WebEncoding? ExtractFromMetaContent(string content) =>
        ExtractLabelFromMetaContent(content) is { } label && WebEncoding.TryForLabel(label, out var encoding) ? encoding : null;

    private static string? ExtractLabelFromMetaContent(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var position = 0;
        while (true)
        {
            var match = s.IndexOf("charset", position, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
            {
                return null;
            }

            position = match + 7;
            while (position < s.Length && IsAsciiWhitespace(s[position]))
            {
                position++;
            }

            if (position < s.Length && s[position] == '=')
            {
                position++;
                break;
            }
        }

        while (position < s.Length && IsAsciiWhitespace(s[position]))
        {
            position++;
        }

        if (position >= s.Length)
        {
            return null;
        }

        var next = s[position];
        if (next is '"' or '\'')
        {
            var end = s.IndexOf(next, position + 1);
            return end < 0 ? null : s[(position + 1)..end];
        }

        var stop = position;
        while (stop < s.Length && !IsAsciiWhitespace(s[stop]) && s[stop] != ';')
        {
            stop++;
        }

        return s[position..stop];
    }

    // Returns a null encoding when the prescan is aborted (bytes ran out or the end condition was reached).
    private static (WebEncoding?, HtmlPrescanSource, int?) ScanDeclarations(ReadOnlySpan<byte> input, List<string> ignored)
    {
        if (input.StartsWith((ReadOnlySpan<byte>)[0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00]))
        {
            return (WebEncoding.ForLabel("UTF-16LE"), HtmlPrescanSource.Utf16XmlPrefix, 0);
        }

        if (input.StartsWith((ReadOnlySpan<byte>)[0x00, 0x3C, 0x00, 0x3F, 0x00, 0x78]))
        {
            return (WebEncoding.ForLabel("UTF-16BE"), HtmlPrescanSource.Utf16XmlPrefix, 0);
        }

        var position = 0;
        while (position < input.Length)
        {
            var rest = input[position..];
            if (rest.StartsWith("<!--"u8))
            {
                var end = position + 4;
                while (end < input.Length && !(input[end] == '>' && input[end - 1] == '-' && input[end - 2] == '-'))
                {
                    end++;
                }

                if (end >= input.Length)
                {
                    return default;
                }

                position = end;
            }
            else if (rest.Length >= 6 && IsMetaStart(rest))
            {
                var tagStart = position;
                position += 5;
                var result = ProcessMeta(input, ref position, ignored);
                if (result.Aborted)
                {
                    return default;
                }

                if (result.Encoding is { } encoding)
                {
                    return (encoding, result.Source, tagStart);
                }
            }
            else if (rest.Length >= 2 && rest[0] == '<'
                && (IsAsciiAlpha(rest[1]) || (rest[1] == '/' && rest.Length >= 3 && IsAsciiAlpha(rest[2]))))
            {
                while (position < input.Length && !IsWhitespace(input[position]) && input[position] != '>')
                {
                    position++;
                }

                AttributeStatus status;
                while ((status = GetAttribute(input, ref position, out _, out _)) == AttributeStatus.Attribute)
                {
                }

                if (status == AttributeStatus.OutOfBytes)
                {
                    return default;
                }
            }
            else if (rest.Length >= 2 && rest[0] == '<' && rest[1] is (byte)'!' or (byte)'/' or (byte)'?')
            {
                var end = rest[1..].IndexOf((byte)'>');
                if (end < 0)
                {
                    return default;
                }

                position += end + 1;
            }

            position++;
        }

        return default;
    }

    private static bool IsMetaStart(ReadOnlySpan<byte> rest) =>
        rest[0] == '<' && (rest[1] | 0x20) == 'm' && (rest[2] | 0x20) == 'e' && (rest[3] | 0x20) == 't'
        && (rest[4] | 0x20) == 'a' && (IsWhitespace(rest[5]) || rest[5] == '/');

    private readonly record struct MetaResult(bool Aborted, WebEncoding? Encoding, HtmlPrescanSource Source);

    private static MetaResult ProcessMeta(ReadOnlySpan<byte> input, ref int position, List<string> ignored)
    {
        var attributes = new List<string>();
        var gotPragma = false;
        bool? needPragma = null;
        var charsetSet = false;
        WebEncoding? charset = null;
        while (true)
        {
            var status = GetAttribute(input, ref position, out var name, out var value);
            if (status == AttributeStatus.OutOfBytes)
            {
                return new(true, null, HtmlPrescanSource.None);
            }

            if (status == AttributeStatus.None)
            {
                break;
            }

            if (attributes.Contains(name))
            {
                continue;
            }

            attributes.Add(name);
            switch (name)
            {
                case "http-equiv":
                    gotPragma |= value == "content-type";
                    break;
                case "content":
                    if (ExtractLabelFromMetaContent(value) is { } label)
                    {
                        if (!WebEncoding.TryForLabel(label, out var extracted))
                        {
                            ignored.Add(label);
                        }
                        else if (!charsetSet)
                        {
                            (charsetSet, charset, needPragma) = (true, extracted, true);
                        }
                    }

                    break;
                case "charset":
                    charsetSet = true;
                    needPragma = false;
                    if (!WebEncoding.TryForLabel(value, out charset))
                    {
                        ignored.Add(value);
                    }

                    break;
            }
        }

        if (needPragma is null || (needPragma == true && !gotPragma) || charset is null)
        {
            return default;
        }

        if (charset.Name is "UTF-16LE" or "UTF-16BE")
        {
            charset = WebEncoding.ForLabel("UTF-8");
        }
        else if (charset.Name == "x-user-defined")
        {
            charset = WebEncoding.ForLabel("windows-1252");
        }

        return new(false, charset, needPragma == true ? HtmlPrescanSource.MetaPragma : HtmlPrescanSource.MetaCharset);
    }

    private static AttributeStatus GetAttribute(ReadOnlySpan<byte> input, ref int position, out string name, out string value)
    {
        name = value = "";
        while (position < input.Length && (IsWhitespace(input[position]) || input[position] == '/'))
        {
            position++;
        }

        if (position >= input.Length)
        {
            return AttributeStatus.OutOfBytes;
        }

        if (input[position] == '>')
        {
            return AttributeStatus.None;
        }

        var nameBuilder = new StringBuilder();
        while (true)
        {
            if (position >= input.Length)
            {
                return AttributeStatus.OutOfBytes;
            }

            var b = input[position];
            if (b == '=' && nameBuilder.Length > 0)
            {
                position++;
                break;
            }

            if (IsWhitespace(b))
            {
                while (position < input.Length && IsWhitespace(input[position]))
                {
                    position++;
                }

                if (position >= input.Length)
                {
                    return AttributeStatus.OutOfBytes;
                }

                name = nameBuilder.ToString();
                if (input[position] != '=')
                {
                    return AttributeStatus.Attribute;
                }

                position++;
                break;
            }

            if (b is (byte)'/' or (byte)'>')
            {
                name = nameBuilder.ToString();
                return AttributeStatus.Attribute;
            }

            nameBuilder.Append(Lower(b));
            position++;
        }

        name = nameBuilder.ToString();
        while (position < input.Length && IsWhitespace(input[position]))
        {
            position++;
        }

        if (position >= input.Length)
        {
            return AttributeStatus.OutOfBytes;
        }

        var valueBuilder = new StringBuilder();
        var first = input[position];
        if (first is (byte)'"' or (byte)'\'')
        {
            while (true)
            {
                position++;
                if (position >= input.Length)
                {
                    return AttributeStatus.OutOfBytes;
                }

                if (input[position] == first)
                {
                    position++;
                    value = valueBuilder.ToString();
                    return AttributeStatus.Attribute;
                }

                valueBuilder.Append(Lower(input[position]));
            }
        }

        if (first == '>')
        {
            return AttributeStatus.Attribute;
        }

        valueBuilder.Append(Lower(first));
        position++;
        while (true)
        {
            if (position >= input.Length)
            {
                return AttributeStatus.OutOfBytes;
            }

            var b = input[position];
            if (IsWhitespace(b) || b == '>')
            {
                value = valueBuilder.ToString();
                return AttributeStatus.Attribute;
            }

            valueBuilder.Append(Lower(b));
            position++;
        }
    }

    private static WebEncoding? GetXmlEncoding(ReadOnlySpan<byte> input)
    {
        if (!input.StartsWith("<?xml"u8))
        {
            return null;
        }

        var declarationEnd = input.IndexOf((byte)'>');
        if (declarationEnd < 0)
        {
            return null;
        }

        var match = input[..declarationEnd].IndexOf("encoding"u8);
        if (match < 0)
        {
            return null;
        }

        var position = match + 8;
        while (position < input.Length && input[position] <= 0x20)
        {
            position++;
        }

        if (position >= input.Length || input[position] != '=')
        {
            return null;
        }

        position++;
        while (position < input.Length && input[position] <= 0x20)
        {
            position++;
        }

        if (position >= input.Length || input[position] is not ((byte)'"' or (byte)'\''))
        {
            return null;
        }

        var quote = input[position++];
        var length = input[position..].IndexOf(quote);
        if (length < 0)
        {
            return null;
        }

        var potential = input.Slice(position, length);
        if (potential.IndexOfAnyInRange((byte)0, (byte)0x20) >= 0
            || !WebEncoding.TryForLabel(System.Text.Encoding.Latin1.GetString(potential), out var encoding))
        {
            return null;
        }

        return encoding.Name is "UTF-16LE" or "UTF-16BE" ? WebEncoding.ForLabel("UTF-8") : encoding;
    }

    private static char Lower(byte b) => (char)(b is >= 0x41 and <= 0x5A ? b + 0x20 : b);
    private static bool IsAsciiAlpha(byte b) => (uint)((b | 0x20) - 'a') <= 'z' - 'a';
    private static bool IsWhitespace(byte b) => b is 0x09 or 0x0A or 0x0C or 0x0D or 0x20;
    private static bool IsAsciiWhitespace(char c) => c is '\t' or '\n' or '\f' or '\r' or ' ';
}
