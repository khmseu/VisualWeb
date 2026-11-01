namespace VisualWeb.Engine.Css;

/// <summary>HTML's legacy color parsing, not CSS color syntax.</summary>
/// <remarks>Spec: html; https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-a-legacy-colour-value</remarks>
internal static class HtmlLegacyColor
{
    internal static CssColor? Parse(string input, CancellationToken cancellation)
    {
        if (input.Length == 0) { return null; }
        var start = 0;
        var end = input.Length;
        while (start < end && IsWhitespace(input[start]))
        {
            if ((start & 1023) == 0) { cancellation.ThrowIfCancellationRequested(); }
            start++;
        }
        while (end > start && IsWhitespace(input[end - 1]))
        {
            if ((end & 1023) == 0) { cancellation.ThrowIfCancellationRequested(); }
            end--;
        }
        var length = end - start;
        if (length <= 32)
        {
            var name = CssText.Lower(input.Substring(start, length));
            if (name == "transparent") { return null; }
            if (CssProperties.NamedColor(name) is { } named) { return named; }
        }
        if (length == 4 && input[start] == '#' && char.IsAsciiHexDigit(input[start + 1])
            && char.IsAsciiHexDigit(input[start + 2]) && char.IsAsciiHexDigit(input[start + 3]))
        {
            return new((byte)(Hex(input[start + 1]) * 17), (byte)(Hex(input[start + 2]) * 17),
                (byte)(Hex(input[start + 3]) * 17));
        }

        Span<char> buffer = stackalloc char[130];
        var count = 0;
        for (var i = start; i < end && count < 128; i++)
        {
            var c = input[i];
            if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(input[i + 1]))
            {
                buffer[count++] = '0';
                if (count < 128) { buffer[count++] = '0'; }
                i++;
            }
            else { buffer[count++] = c; }
        }
        var first = count > 0 && buffer[0] == '#' ? 1 : 0;
        var size = count - first;
        for (var i = first; i < count; i++)
        {
            if (!char.IsAsciiHexDigit(buffer[i])) { buffer[i] = '0'; }
        }
        while (size == 0 || size % 3 != 0) { buffer[count++] = '0'; size++; }
        var componentLength = size / 3;
        var offset = Math.Max(0, componentLength - 8);
        var remaining = componentLength - offset;
        while (remaining > 2 && buffer[first + offset] == '0'
            && buffer[first + componentLength + offset] == '0'
            && buffer[first + 2 * componentLength + offset] == '0')
        {
            offset++;
            remaining--;
        }
        return new(Channel(buffer, first + offset, remaining),
            Channel(buffer, first + componentLength + offset, remaining),
            Channel(buffer, first + 2 * componentLength + offset, remaining));
    }

    private static byte Channel(ReadOnlySpan<char> buffer, int position, int length) =>
        (byte)(Hex(buffer[position]) * (length > 1 ? 16 : 1)
            + (length > 1 ? Hex(buffer[position + 1]) : 0));

    private static bool IsWhitespace(char c) => c is '\t' or '\n' or '\f' or '\r' or ' ';
    private static int Hex(char c) => c <= '9' ? c - '0' : (c | 32) - 'a' + 10;
}
