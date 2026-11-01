namespace VisualWeb.Engine.Css;

/// <summary>HTML legacy font size parsing.</summary>
/// <remarks>Spec: html; https://html.spec.whatwg.org/multipage/rendering.html#rules-for-parsing-a-legacy-font-size</remarks>
internal static class HtmlLegacyFontSize
{
    internal static CssLength? Parse(string input, CancellationToken cancellation)
    {
        var position = 0;
        while (position < input.Length && input[position] is '\t' or '\n' or '\f' or '\r' or ' ')
        {
            if ((position & 1023) == 0) { cancellation.ThrowIfCancellationRequested(); }
            position++;
        }
        var mode = 0;
        if (position < input.Length && input[position] is '+' or '-')
        {
            mode = input[position++] == '+' ? 1 : -1;
        }
        var start = position;
        var value = 0;
        while (position < input.Length && char.IsAsciiDigit(input[position]))
        {
            if ((position & 1023) == 0) { cancellation.ThrowIfCancellationRequested(); }
            value = Math.Min(7, value * 10 + input[position++] - '0');
        }
        if (position == start) { return null; }
        value = Math.Clamp(mode == 0 ? value : 3 + mode * value, 1, 7);
        var keyword = value switch
        {
            1 => "x-small",
            2 => "small",
            3 => "medium",
            4 => "large",
            5 => "x-large",
            6 => "xx-large",
            7 => "xxx-large",
            _ => throw new InvalidOperationException("Clamped font size is out of range.")
        };
        return CssProperties.AbsoluteFontSize(keyword)
            ?? throw new InvalidOperationException("Unknown absolute font size keyword.");
    }
}
