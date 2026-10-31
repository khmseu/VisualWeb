using System.Globalization;

namespace VisualWeb.Engine.Css;

/// <summary>Typed static CSS values. Percentages remain unresolved until layout supplies a containing size.</summary>
/// <remarks>Spec: css-values; <see href="https://www.w3.org/TR/css-values-4/#lengths">lengths</see>,
/// including <see href="https://www.w3.org/TR/css-values-4/#absolute-lengths">absolute lengths</see>.
/// Absolute and em/rem lengths become px; line-height numbers remain multipliers.</remarks>
public abstract record CssValue;
public sealed record CssKeyword(string Value) : CssValue;
public sealed record CssLength(double Value, string Unit) : CssValue;
public sealed record CssNumber(double Value) : CssValue;
/// <summary>Clamped 8-bit sRGB channels; high-precision color management is deferred.</summary>
/// <remarks>Spec: css-color; <see href="https://www.w3.org/TR/css-color-4/#rgb-functions">RGB colors</see>.</remarks>
public sealed record CssColor(byte Red, byte Green, byte Blue, byte Alpha = 255) : CssValue;
/// <summary>Ordered font family names, before text shaping resolves available faces.</summary>
/// <remarks>Spec: css-fonts; <see href="https://www.w3.org/TR/css-fonts-4/#font-family-prop">font-family</see>.</remarks>
public sealed record CssFontFamilies(IReadOnlyList<string> Names) : CssValue;

internal sealed record CssProperty(CssValue Initial, bool Inherited);
internal sealed record CssAssignment(string Name, CssValue Value, bool Important, int Offset);

internal static class CssProperties
{
    private static readonly HashSet<string> Wide = new(["initial", "inherit", "unset", "revert"], StringComparer.Ordinal);
    private static readonly string[] Sides = ["top", "right", "bottom", "left"];
    internal static readonly IReadOnlyDictionary<string, CssProperty> Registry = CreateRegistry();
    private static readonly HashSet<string> BorderStyles = new(
        ["none", "hidden", "solid", "dotted", "dashed", "double", "groove", "ridge", "inset", "outset"], StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, CssProperty> CreateRegistry()
    {
        var result = new Dictionary<string, CssProperty>(StringComparer.Ordinal)
        {
            ["display"] = new(new CssKeyword("inline"), false),
            ["list-style-type"] = new(new CssKeyword("disc"), true),
            ["list-style-position"] = new(new CssKeyword("outside"), true),
            ["list-style-image"] = new(new CssKeyword("none"), true),
            ["table-layout"] = new(new CssKeyword("auto"), false),
            ["border-collapse"] = new(new CssKeyword("separate"), true),
            ["border-spacing"] = new(new CssLength(0, "px"), true),
            ["vertical-align"] = new(new CssKeyword("baseline"), false),
            ["overflow-x"] = new(new CssKeyword("visible"), false),
            ["overflow-y"] = new(new CssKeyword("visible"), false),
            ["color"] = new(new CssColor(0, 0, 0), true),
            ["background-color"] = new(new CssColor(0, 0, 0, 0), false),
            ["font-family"] = new(new CssFontFamilies(Array.AsReadOnly(new[] { "serif" })), true),
            ["font-size"] = new(new CssLength(16, "px"), true),
            ["font-weight"] = new(new CssNumber(400), true),
            ["font-style"] = new(new CssKeyword("normal"), true),
            ["line-height"] = new(new CssKeyword("normal"), true),
            ["text-align"] = new(new CssKeyword("start"), true),
            ["text-decoration-line"] = new(new CssKeyword("none"), false),
            ["white-space"] = new(new CssKeyword("normal"), true),
            ["box-sizing"] = new(new CssKeyword("content-box"), false)
        };
        foreach (var dimension in new[] { "width", "height" })
        {
            result[dimension] = new(new CssKeyword("auto"), false);
            result["min-" + dimension] = new(new CssKeyword("auto"), false);
            result["max-" + dimension] = new(new CssKeyword("none"), false);
        }
        foreach (var side in Sides)
        {
            result["margin-" + side] = new(new CssLength(0, "px"), false);
            result["padding-" + side] = new(new CssLength(0, "px"), false);
            result["border-" + side + "-width"] = new(new CssLength(3, "px"), false);
            result["border-" + side + "-style"] = new(new CssKeyword("none"), false);
            result["border-" + side + "-color"] = new(new CssKeyword("currentcolor"), false);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, CssProperty>(result);
    }

    internal static List<CssAssignment> Expand(IEnumerable<CssDeclaration> declarations, CssContext context)
    {
        var result = new List<CssAssignment>();
        foreach (var declaration in declarations)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            var name = declaration.Name;
            var value = declaration.Value.Where(t => t.Kind != CssTokenKind.Whitespace).ToList();
            var wide = value.Count == 1 && value[0].Kind == CssTokenKind.Ident && Wide.Contains(CssText.Lower(value[0].Value))
                ? new CssKeyword(CssText.Lower(value[0].Value)) : null;
            if (name.StartsWith("--", StringComparison.Ordinal) || value.Any(t => t.Kind == CssTokenKind.Function
                && CssText.Lower(t.Value) is "var" or "env" or "calc" or "min" or "max" or "clamp"))
            {
                context.Error("unsupported-value", declaration.Offset, "Custom properties and calculated/substituted values are deferred.");
                continue;
            }
            if (name == "all")
            {
                if (wide is null) { Invalid(); continue; }
                foreach (var property in Registry.Keys) { Add(property, wide); }
            }
            else if (name is "margin" or "padding" or "border-width" or "border-style" or "border-color")
            {
                var prefix = name.Split('-')[0];
                var suffix = name.StartsWith("border-", StringComparison.Ordinal) ? "-" + name.Split('-')[1] : "";
                var names = Sides.Select(s => prefix + "-" + s + suffix).ToArray();
                if (wide is not null) { foreach (var property in names) { Add(property, wide); } continue; }
                var components = Components(value);
                if (components.Count is < 1 or > 4) { Invalid(); continue; }
                var values = components.Select(t => Parse(names[0], t)).ToList();
                if (values.Any(v => v is null)) { Invalid(); continue; }
                var expanded = new[] { values[0], values.Count > 1 ? values[1] : values[0],
                    values.Count > 2 ? values[2] : values[0], values.Count > 3 ? values[3] : values.Count > 1 ? values[1] : values[0] };
                for (var i = 0; i < 4; i++) { Add(names[i], expanded[i]!); }
            }
            else if (name == "border" || Sides.Any(s => name == "border-" + s))
            {
                var sides = name == "border" ? Sides : [name["border-".Length..]];
                if (wide is not null)
                {
                    foreach (var side in sides)
                    {
                        foreach (var kind in new[] { "width", "style", "color" }) { Add("border-" + side + "-" + kind, wide); }
                    }
                    continue;
                }
                CssValue? width = null, style = null, color = null;
                var components = Components(value);
                var valid = components.Count is >= 1 and <= 3;
                foreach (var component in components)
                {
                    if (Parse("border-top-width", component) is { } w && width is null) { width = w; }
                    else if (Parse("border-top-style", component) is { } s && style is null) { style = s; }
                    else if (Parse("border-top-color", component) is { } c && color is null) { color = c; }
                    else { valid = false; }
                }
                if (!valid) { Invalid(); continue; }
                foreach (var side in sides)
                {
                    Add("border-" + side + "-width", width ?? Registry["border-top-width"].Initial);
                    Add("border-" + side + "-style", style ?? Registry["border-top-style"].Initial);
                    Add("border-" + side + "-color", color ?? Registry["border-top-color"].Initial);
                }
            }
            else if (name == "font")
            {
                var names = new[] { "font-style", "font-weight", "font-size", "line-height", "font-family" };
                if (wide is not null) { foreach (var property in names) { Add(property, wide); } continue; }
                CssValue? style = null, weight = null, size = null;
                var index = 0;
                var prefixCount = 0;
                var valid = true;
                while (index < value.Count)
                {
                    var component = new List<CssToken> { value[index++] };
                    if (Parse("font-size", component) is { } parsedSize) { size = parsedSize; break; }
                    if (++prefixCount > 4) { valid = false; break; }
                    if (component[0].Kind == CssTokenKind.Ident && CssText.Lower(component[0].Value) == "normal") { continue; }
                    if (style is null && Parse("font-style", component) is { } parsedStyle) { style = parsedStyle; }
                    else if (weight is null && Parse("font-weight", component) is { } parsedWeight) { weight = parsedWeight; }
                    else { valid = false; break; }
                }
                if (!valid || size is null) { Invalid(); continue; }
                var line = Registry["line-height"].Initial;
                if (index < value.Count && value[index].Kind == CssTokenKind.Delim && value[index].Value == "/")
                {
                    index++;
                    if (index == value.Count || Parse("line-height", [value[index++]]) is not { } parsedLine)
                    { Invalid(); continue; }
                    line = parsedLine;
                }
                if (Families(value.Skip(index).ToArray()) is not { } families) { Invalid(); continue; }
                Add(names[0], style ?? Registry[names[0]].Initial);
                Add(names[1], weight ?? Registry[names[1]].Initial);
                Add(names[2], size);
                Add(names[3], line);
                Add(names[4], families);
            }
            else if (name == "background")
            {
                CssValue? color = wide;
                if (wide is null)
                {
                    var components = Components(value);
                    var valid = components.Count is >= 1 and <= 2;
                    var none = false;
                    foreach (var component in components)
                    {
                        if (component.Count == 1 && component[0].Kind == CssTokenKind.Ident
                            && CssText.Lower(component[0].Value) == "none" && !none) { none = true; }
                        else if (color is null && Color(component) is { } parsed) { color = parsed; }
                        else { valid = false; }
                    }
                    if (!valid) { Invalid(); continue; }
                }
                Add("background-color", color ?? Registry["background-color"].Initial);
            }
            else if (name == "list-style")
            {
                var names = new[] { "list-style-type", "list-style-position", "list-style-image" };
                if (wide is not null) { foreach (var property in names) { Add(property, wide); } continue; }
                CssValue? type = null, position = null;
                var noneCount = 0;
                var valid = value.Count is >= 1 and <= 3;
                foreach (var token in value)
                {
                    var keyword = token.Kind == CssTokenKind.Ident ? CssText.Lower(token.Value) : "";
                    if (keyword == "none") { noneCount++; }
                    else if (keyword == "outside" && position is null) { position = new CssKeyword(keyword); }
                    else if (type is null && IsListStyleType(keyword)) { type = new CssKeyword(keyword); }
                    else { valid = false; }
                }
                if (!valid || noneCount > 2 || noneCount == 2 && type is not null) { Invalid(); continue; }
                Add(names[0], type ?? (noneCount > 0 ? new CssKeyword("none") : Registry[names[0]].Initial));
                Add(names[1], position ?? Registry[names[1]].Initial);
                Add(names[2], Registry[names[2]].Initial);
            }
            else if (name == "text-decoration")
            {
                var line = wide ?? Parse("text-decoration-line", declaration.Value);
                if (line is null) { Invalid(); }
                else { Add("text-decoration-line", line); }
            }
            else if (Registry.ContainsKey(name))
            {
                var parsed = wide ?? Parse(name, declaration.Value);
                if (parsed is null) { Invalid(); }
                else { Add(name, parsed); }
            }
            else
            {
                context.Error("unsupported-property", declaration.Offset, $"Property '{name}' is outside the supported static subset.");
            }

            void Add(string property, CssValue parsed)
            {
                context.Assignment();
                result.Add(new(property, parsed, declaration.Important, declaration.Offset));
            }
            void Invalid() => context.Error("invalid-property-value", declaration.Offset, $"Unsupported or invalid value for '{name}'.");
        }
        return result;
    }

    private static CssValue? Parse(string name, IReadOnlyList<CssToken> input)
    {
        var tokens = input.Where(t => t.Kind != CssTokenKind.Whitespace).ToList();
        if (tokens.Count == 0) { return null; }
        if (name is "color" or "background-color" || name.EndsWith("-color", StringComparison.Ordinal)) { return Color(tokens); }
        if (name == "font-family") { return Families(input); }
        if (tokens.Count != 1) { return null; }
        var token = tokens[0];
        var keyword = token.Kind == CssTokenKind.Ident ? CssText.Lower(token.Value) : "";
        if (name == "display")
        {
            return Keyword(keyword, "none", "block", "inline", "inline-block", "list-item",
                "table", "table-row-group", "table-header-group", "table-footer-group", "table-row", "table-cell");
        }
        if (name == "table-layout") { return Keyword(keyword, "auto"); }
        if (name == "border-collapse") { return Keyword(keyword, "separate"); }
        if (name == "border-spacing") { return Length(token, false) is { Value: >= 0 } spacing ? spacing : null; }
        if (name == "vertical-align") { return Keyword(keyword, "baseline", "top"); }
        if (name == "list-style-type") { return IsListStyleType(keyword) ? new CssKeyword(keyword) : null; }
        if (name == "list-style-position") { return Keyword(keyword, "outside"); }
        if (name == "list-style-image") { return Keyword(keyword, "none"); }
        if (name is "overflow-x" or "overflow-y") { return Keyword(keyword, "visible", "hidden", "auto"); }
        if (name == "box-sizing") { return Keyword(keyword, "content-box", "border-box"); }
        if (name == "font-style") { return Keyword(keyword, "normal", "italic", "oblique"); }
        if (name == "text-align") { return Keyword(keyword, "start", "end", "left", "right", "center", "justify"); }
        if (name == "text-decoration-line") { return Keyword(keyword, "none"); }
        if (name == "white-space") { return Keyword(keyword, "normal", "pre", "nowrap", "pre-wrap", "pre-line", "break-spaces"); }
        if (name == "font-weight")
        {
            if (keyword is "normal" or "bold") { return new CssNumber(keyword == "bold" ? 700 : 400); }
            if (keyword is "bolder" or "lighter") { return new CssKeyword(keyword); }
            return token.Kind == CssTokenKind.Number && Number(token) is >= 1 and <= 1000 and var weight ? new CssNumber(weight) : null;
        }
        if (name.EndsWith("-style", StringComparison.Ordinal)) { return BorderStyles.Contains(keyword) ? new CssKeyword(keyword) : null; }
        if (name == "font-size")
        {
            if (keyword is "xx-small" or "x-small" or "small" or "medium" or "large" or "x-large" or "xx-large" or "xxx-large")
            {
                return new CssLength(keyword switch
                {
                    "xx-small" => 9,
                    "x-small" => 10,
                    "small" => 13,
                    "medium" => 16,
                    "large" => 18,
                    "x-large" => 24,
                    "xx-large" => 32,
                    _ => 48
                }, "px");
            }
            if (keyword is "larger" or "smaller") { return new CssKeyword(keyword); }
        }
        if (name == "line-height")
        {
            if (keyword == "normal") { return new CssKeyword(keyword); }
            if (token.Kind == CssTokenKind.Number && Number(token) is >= 0 and var line) { return new CssNumber(line); }
        }
        var borderWidth = name.StartsWith("border-", StringComparison.Ordinal);
        if (borderWidth && keyword is "thin" or "medium" or "thick")
        {
            return new CssLength(keyword == "thin" ? 1 : keyword == "medium" ? 3 : 5, "px");
        }
        if (keyword == "auto" && (name.StartsWith("margin-", StringComparison.Ordinal)
            || name is "width" or "height" or "min-width" or "min-height")) { return new CssKeyword(keyword); }
        if (keyword == "none" && name is "max-width" or "max-height") { return new CssKeyword(keyword); }
        var length = Length(token, !borderWidth);
        return length is not null && (length.Value >= 0 || name.StartsWith("margin-", StringComparison.Ordinal)) ? length : null;
    }

    private static bool IsListStyleType(string keyword) =>
        keyword is "disc" or "circle" or "square" or "decimal" or "none"
            or "lower-alpha" or "upper-alpha" or "lower-latin" or "upper-latin"
            or "lower-roman" or "upper-roman";

    private static CssKeyword? Keyword(string keyword, params string[] allowed) => allowed.Contains(keyword, StringComparer.Ordinal)
        ? new(keyword) : null;
    private static double? Number(CssToken token) => double.TryParse(token.Value, NumberStyles.Float, CultureInfo.InvariantCulture,
        out var number) && double.IsFinite(number) ? number : null;
    private static CssLength? Length(CssToken token, bool percentage)
    {
        var number = Number(token);
        if (number is null) { return null; }
        if (token.Kind == CssTokenKind.Number && number == 0) { return new(0, "px"); }
        if (token.Kind == CssTokenKind.Percentage && percentage) { return new(number.Value, "%"); }
        if (token.Kind != CssTokenKind.Dimension) { return null; }
        var unit = CssText.Lower(token.Unit);
        return unit switch
        {
            "px" or "em" or "rem" => new(number.Value, unit),
            "in" => new(number.Value * 96, "px"),
            "cm" => new(number.Value * (96 / 2.54), "px"),
            "mm" => new(number.Value * (96 / 25.4), "px"),
            "q" => new(number.Value * (96 / 101.6), "px"),
            "pt" => new(number.Value * (96 / 72d), "px"),
            "pc" => new(number.Value * 16, "px"),
            _ => null
        };
    }

    private static CssFontFamilies? Families(IReadOnlyList<CssToken> tokens)
    {
        var names = new List<string>();
        var current = new List<CssToken>();
        foreach (var token in tokens)
        {
            if (token.Kind == CssTokenKind.Comma) { if (!Consume()) { return null; } }
            else if (token.Kind != CssTokenKind.Whitespace) { current.Add(token); }
        }
        if (!Consume()) { return null; }
        return new(names.AsReadOnly());

        bool Consume()
        {
            if (current.Count == 0) { return false; }
            if (current.Count == 1 && current[0].Kind == CssTokenKind.String) { names.Add(current[0].Value); }
            else if (current.All(t => t.Kind == CssTokenKind.Ident))
            {
                var name = string.Join(" ", current.Select(t => t.Value));
                if (current.Any(t => Wide.Contains(CssText.Lower(t.Value)))) { return false; }
                names.Add(name);
            }
            else { return false; }
            current.Clear();
            return true;
        }
    }

    private static CssValue? Color(List<CssToken> tokens)
    {
        if (tokens.Count == 1)
        {
            var token = tokens[0];
            if (token.Kind == CssTokenKind.Ident)
            {
                var name = CssText.Lower(token.Value);
                if (name == "currentcolor") { return new CssKeyword(name); }
                return Colors.GetValueOrDefault(name);
            }
            if (token.Kind == CssTokenKind.Hash && token.Value.All(char.IsAsciiHexDigit))
            {
                var hex = token.Value;
                if (hex.Length is 3 or 4) { hex = string.Concat(hex.Select(c => new string(c, 2))); }
                if (hex.Length is not (6 or 8)) { return null; }
                return new CssColor(Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16), hex.Length == 8 ? Convert.ToByte(hex[6..8], 16) : (byte)255);
            }
        }
        if (tokens[0].Kind != CssTokenKind.Function || tokens[^1].Kind != CssTokenKind.CloseParen) { return null; }
        var function = CssText.Lower(tokens[0].Value);
        if (function is not ("rgb" or "rgba" or "hsl" or "hsla")) { return null; }
        var args = tokens.Skip(1).Take(tokens.Count - 2).ToList();
        var legacy = args.Any(t => t.Kind == CssTokenKind.Comma);
        var values = ColorFunctionComponents(args, legacy);
        if (values is null) { return null; }
        if (function is "hsl" or "hsla") { return HslColor(values, legacy); }
        if (legacy)
        {
            if (values.Take(3).Any(t => t.Kind is not (CssTokenKind.Number or CssTokenKind.Percentage))
                || values.Take(3).Select(t => t.Kind).Distinct().Count() != 1
                || values.Count == 4 && values[3].Kind is not (CssTokenKind.Number or CssTokenKind.Percentage)) { return null; }
        }
        else if (values.Any(token => token.Kind is not (CssTokenKind.Number or CssTokenKind.Percentage) && !IsNone(token)))
        {
            return null;
        }
        var numbers = values.Select(token => token.Kind == CssTokenKind.Ident ? (double?)0 : Number(token)).ToList();
        if (numbers.Any(n => n is null)) { return null; }
        byte Channel(int index, bool alpha = false) => (byte)Math.Round(Math.Clamp(
            values[index].Kind == CssTokenKind.Percentage ? numbers[index]!.Value / 100 * 255
                : numbers[index]!.Value * (alpha ? 255 : 1), 0, 255),
            MidpointRounding.AwayFromZero);
        return new CssColor(Channel(0), Channel(1), Channel(2), values.Count == 4 ? Channel(3, true) : (byte)255);
    }

    private static List<CssToken>? ColorFunctionComponents(IReadOnlyList<CssToken> args, bool legacy)
    {
        if (legacy)
        {
            var commaValues = args.Where(t => t.Kind != CssTokenKind.Comma).ToList();
            return commaValues.Count is 3 or 4 && args.Count == commaValues.Count * 2 - 1
                && args.Where((_, i) => i % 2 == 1).All(t => t.Kind == CssTokenKind.Comma) ? commaValues : null;
        }
        if (args.Count != 3 && (args.Count != 5 || !args[3].IsDelimiter("/"))) { return null; }
        var values = args.Take(3).ToList();
        if (args.Count == 5) { values.Add(args[4]); }
        return values;
    }

    private static CssColor? HslColor(IReadOnlyList<CssToken> values, bool legacy)
    {
        if (legacy && (values[1].Kind != CssTokenKind.Percentage || values[2].Kind != CssTokenKind.Percentage)) { return null; }
        if (Hue(values[0], !legacy) is not { } hue
            || ColorComponent(values[1], !legacy) is not { } saturation
            || ColorComponent(values[2], !legacy) is not { } lightness
            || saturation > 100 || lightness is < 0 or > 100) { return null; }
        var alpha = values.Count == 4 ? ColorComponent(values[3], !legacy) : 1;
        if (alpha is null) { return null; }
        var alphaValue = values.Count == 4 && values[3].Kind == CssTokenKind.Percentage ? alpha.Value / 100 : alpha.Value;

        saturation = Math.Max(0, saturation) / 100;
        lightness /= 100;
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var sector = hue / 60;
        var second = chroma * (1 - Math.Abs(sector % 2 - 1));
        var (red, green, blue) = sector switch
        {
            < 1 => (chroma, second, 0d),
            < 2 => (second, chroma, 0d),
            < 3 => (0d, chroma, second),
            < 4 => (0d, second, chroma),
            < 5 => (second, 0d, chroma),
            _ => (chroma, 0d, second)
        };
        var offset = lightness - chroma / 2;
        static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value * 255, 0, 255), MidpointRounding.AwayFromZero);
        return new CssColor(Channel(red + offset), Channel(green + offset), Channel(blue + offset), Channel(alphaValue));
    }

    private static double? Hue(CssToken token, bool allowNone)
    {
        if (allowNone && IsNone(token)) { return 0; }
        if (token.Kind is not (CssTokenKind.Number or CssTokenKind.Dimension) || Number(token) is not { } number) { return null; }
        var unit = token.Kind == CssTokenKind.Number ? "deg" : CssText.Lower(token.Unit);
        var (period, degrees) = unit switch
        {
            "deg" => (360d, 1d),
            "grad" => (400d, 0.9d),
            "rad" => (2 * Math.PI, 180 / Math.PI),
            "turn" => (1d, 360d),
            _ => (0d, 0d)
        };
        if (period == 0) { return null; }
        var hue = number % period * degrees;
        if (hue < 0) { hue += 360; }
        return hue >= 360 ? 0 : hue;
    }

    private static double? ColorComponent(CssToken token, bool allowNone) => allowNone && IsNone(token)
        ? 0 : token.Kind is CssTokenKind.Number or CssTokenKind.Percentage ? Number(token) : null;

    private static bool IsNone(CssToken token) => token.Kind == CssTokenKind.Ident && CssText.Lower(token.Value) == "none";

    private static List<List<CssToken>> Components(List<CssToken> tokens)
    {
        var result = new List<List<CssToken>>();
        var current = new List<CssToken>();
        var stack = new Stack<CssTokenKind>();
        foreach (var token in tokens)
        {
            current.Add(token);
            if (CssText.Opens(token.Kind)) { stack.Push(CssText.Closing(token.Kind)); }
            else if (stack.TryPeek(out var close) && token.Kind == close) { stack.Pop(); }
            if (stack.Count == 0) { result.Add(current); current = []; }
        }
        if (current.Count > 0) { result.Add(current); }
        return result;
    }

    private static readonly Dictionary<string, CssColor> Colors = new(StringComparer.Ordinal)
    {
        ["aliceblue"] = new(240, 248, 255),
        ["antiquewhite"] = new(250, 235, 215),
        ["aquamarine"] = new(127, 255, 212),
        ["azure"] = new(240, 255, 255),
        ["beige"] = new(245, 245, 220),
        ["bisque"] = new(255, 228, 196),
        ["blanchedalmond"] = new(255, 235, 205),
        ["blueviolet"] = new(138, 43, 226),
        ["brown"] = new(165, 42, 42),
        ["burlywood"] = new(222, 184, 135),
        ["cadetblue"] = new(95, 158, 160),
        ["chartreuse"] = new(127, 255, 0),
        ["chocolate"] = new(210, 105, 30),
        ["coral"] = new(255, 127, 80),
        ["cornflowerblue"] = new(100, 149, 237),
        ["cornsilk"] = new(255, 248, 220),
        ["crimson"] = new(220, 20, 60),
        ["cyan"] = new(0, 255, 255),
        ["darkblue"] = new(0, 0, 139),
        ["darkcyan"] = new(0, 139, 139),
        ["darkgoldenrod"] = new(184, 134, 11),
        ["darkgray"] = new(169, 169, 169),
        ["darkgreen"] = new(0, 100, 0),
        ["darkgrey"] = new(169, 169, 169),
        ["darkkhaki"] = new(189, 183, 107),
        ["darkmagenta"] = new(139, 0, 139),
        ["darkolivegreen"] = new(85, 107, 47),
        ["darkorange"] = new(255, 140, 0),
        ["darkorchid"] = new(153, 50, 204),
        ["darkred"] = new(139, 0, 0),
        ["darksalmon"] = new(233, 150, 122),
        ["darkseagreen"] = new(143, 188, 143),
        ["darkslateblue"] = new(72, 61, 139),
        ["darkslategray"] = new(47, 79, 79),
        ["darkslategrey"] = new(47, 79, 79),
        ["darkturquoise"] = new(0, 206, 209),
        ["darkviolet"] = new(148, 0, 211),
        ["deeppink"] = new(255, 20, 147),
        ["deepskyblue"] = new(0, 191, 255),
        ["dimgray"] = new(105, 105, 105),
        ["dimgrey"] = new(105, 105, 105),
        ["dodgerblue"] = new(30, 144, 255),
        ["firebrick"] = new(178, 34, 34),
        ["floralwhite"] = new(255, 250, 240),
        ["forestgreen"] = new(34, 139, 34),
        ["gainsboro"] = new(220, 220, 220),
        ["ghostwhite"] = new(248, 248, 255),
        ["gold"] = new(255, 215, 0),
        ["goldenrod"] = new(218, 165, 32),
        ["greenyellow"] = new(173, 255, 47),
        ["grey"] = new(128, 128, 128),
        ["honeydew"] = new(240, 255, 240),
        ["hotpink"] = new(255, 105, 180),
        ["indianred"] = new(205, 92, 92),
        ["indigo"] = new(75, 0, 130),
        ["ivory"] = new(255, 255, 240),
        ["khaki"] = new(240, 230, 140),
        ["lavender"] = new(230, 230, 250),
        ["lavenderblush"] = new(255, 240, 245),
        ["lawngreen"] = new(124, 252, 0),
        ["lemonchiffon"] = new(255, 250, 205),
        ["lightblue"] = new(173, 216, 230),
        ["lightcoral"] = new(240, 128, 128),
        ["lightcyan"] = new(224, 255, 255),
        ["lightgoldenrodyellow"] = new(250, 250, 210),
        ["lightgray"] = new(211, 211, 211),
        ["lightgreen"] = new(144, 238, 144),
        ["lightgrey"] = new(211, 211, 211),
        ["lightpink"] = new(255, 182, 193),
        ["lightsalmon"] = new(255, 160, 122),
        ["lightseagreen"] = new(32, 178, 170),
        ["lightskyblue"] = new(135, 206, 250),
        ["lightslategray"] = new(119, 136, 153),
        ["lightslategrey"] = new(119, 136, 153),
        ["lightsteelblue"] = new(176, 196, 222),
        ["lightyellow"] = new(255, 255, 224),
        ["limegreen"] = new(50, 205, 50),
        ["linen"] = new(250, 240, 230),
        ["magenta"] = new(255, 0, 255),
        ["mediumaquamarine"] = new(102, 205, 170),
        ["mediumblue"] = new(0, 0, 205),
        ["mediumorchid"] = new(186, 85, 211),
        ["mediumpurple"] = new(147, 112, 219),
        ["mediumseagreen"] = new(60, 179, 113),
        ["mediumslateblue"] = new(123, 104, 238),
        ["mediumspringgreen"] = new(0, 250, 154),
        ["mediumturquoise"] = new(72, 209, 204),
        ["mediumvioletred"] = new(199, 21, 133),
        ["midnightblue"] = new(25, 25, 112),
        ["mintcream"] = new(245, 255, 250),
        ["mistyrose"] = new(255, 228, 225),
        ["moccasin"] = new(255, 228, 181),
        ["navajowhite"] = new(255, 222, 173),
        ["oldlace"] = new(253, 245, 230),
        ["olivedrab"] = new(107, 142, 35),
        ["orangered"] = new(255, 69, 0),
        ["orchid"] = new(218, 112, 214),
        ["palegoldenrod"] = new(238, 232, 170),
        ["palegreen"] = new(152, 251, 152),
        ["paleturquoise"] = new(175, 238, 238),
        ["palevioletred"] = new(219, 112, 147),
        ["papayawhip"] = new(255, 239, 213),
        ["peachpuff"] = new(255, 218, 185),
        ["peru"] = new(205, 133, 63),
        ["pink"] = new(255, 192, 203),
        ["plum"] = new(221, 160, 221),
        ["powderblue"] = new(176, 224, 230),
        ["rosybrown"] = new(188, 143, 143),
        ["royalblue"] = new(65, 105, 225),
        ["saddlebrown"] = new(139, 69, 19),
        ["salmon"] = new(250, 128, 114),
        ["sandybrown"] = new(244, 164, 96),
        ["seagreen"] = new(46, 139, 87),
        ["seashell"] = new(255, 245, 238),
        ["sienna"] = new(160, 82, 45),
        ["skyblue"] = new(135, 206, 235),
        ["slateblue"] = new(106, 90, 205),
        ["slategray"] = new(112, 128, 144),
        ["slategrey"] = new(112, 128, 144),
        ["snow"] = new(255, 250, 250),
        ["springgreen"] = new(0, 255, 127),
        ["steelblue"] = new(70, 130, 180),
        ["tan"] = new(210, 180, 140),
        ["thistle"] = new(216, 191, 216),
        ["tomato"] = new(255, 99, 71),
        ["turquoise"] = new(64, 224, 208),
        ["violet"] = new(238, 130, 238),
        ["wheat"] = new(245, 222, 179),
        ["whitesmoke"] = new(245, 245, 245),
        ["yellowgreen"] = new(154, 205, 50),
        ["black"] = new(0, 0, 0),
        ["silver"] = new(192, 192, 192),
        ["gray"] = new(128, 128, 128),
        ["white"] = new(255, 255, 255),
        ["maroon"] = new(128, 0, 0),
        ["red"] = new(255, 0, 0),
        ["purple"] = new(128, 0, 128),
        ["fuchsia"] = new(255, 0, 255),
        ["green"] = new(0, 128, 0),
        ["lime"] = new(0, 255, 0),
        ["olive"] = new(128, 128, 0),
        ["yellow"] = new(255, 255, 0),
        ["navy"] = new(0, 0, 128),
        ["blue"] = new(0, 0, 255),
        ["teal"] = new(0, 128, 128),
        ["aqua"] = new(0, 255, 255),
        ["orange"] = new(255, 165, 0),
        ["rebeccapurple"] = new(102, 51, 153),
        ["transparent"] = new(0, 0, 0, 0)
    };
}
