namespace VisualWeb.PageRendering;

internal static class PageTabIndex
{
    private static readonly char[] AsciiWhitespace = ['\t', '\n', '\f', '\r', ' '];

    public static int? Parse(string? attribute)
    {
        if (attribute is null) { return null; }
        var position = 0;
        while (position < attribute.Length && AsciiWhitespace.Contains(attribute[position])) { position++; }
        if (position == attribute.Length) { return null; }

        var negative = false;
        if (attribute[position] is '-' or '+')
        {
            negative = attribute[position] == '-';
            position++;
        }
        var firstDigit = position;
        long value = 0;
        var limit = negative ? (long)int.MaxValue + 1 : int.MaxValue;
        while (position < attribute.Length && attribute[position] is >= '0' and <= '9')
        {
            var digit = attribute[position] - '0';
            if (value > (limit - digit) / 10)
            { throw new PageNavigationException("tabindex exceeds the supported signed 32-bit range."); }
            value = value * 10 + digit;
            position++;
        }
        if (position == firstDigit) { return null; }
        return negative ? (int)-value : (int)value;
    }
}
