using System.Globalization;

namespace VisualWeb.Engine.Css;

/// <summary>Typed static CSS values. Percentages remain unresolved until layout supplies a containing size.</summary>
/// <remarks>Spec: css-values; <see href="https://www.w3.org/TR/css-values-4/#lengths">lengths</see>.
/// em/rem lengths become px during computation; line-height numbers remain multipliers.</remarks>
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
            ["color"] = new(new CssColor(0, 0, 0), true),
            ["background-color"] = new(new CssColor(0, 0, 0, 0), false),
            ["font-family"] = new(new CssFontFamilies(Array.AsReadOnly(new[] { "serif" })), true),
            ["font-size"] = new(new CssLength(16, "px"), true),
            ["font-weight"] = new(new CssNumber(400), true),
            ["font-style"] = new(new CssKeyword("normal"), true),
            ["line-height"] = new(new CssKeyword("normal"), true),
            ["text-align"] = new(new CssKeyword("start"), true),
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
        if (name == "display") { return Keyword(keyword, "none", "block", "inline", "inline-block"); }
        if (name == "box-sizing") { return Keyword(keyword, "content-box", "border-box"); }
        if (name == "font-style") { return Keyword(keyword, "normal", "italic", "oblique"); }
        if (name == "text-align") { return Keyword(keyword, "start", "end", "left", "right", "center", "justify"); }
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
        var unit = CssText.Lower(token.Unit);
        return token.Kind == CssTokenKind.Dimension && unit is "px" or "em" or "rem" ? new(number.Value, unit) : null;
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
        if (function is not ("rgb" or "rgba")) { return null; }
        var args = tokens.Skip(1).Take(tokens.Count - 2).ToList();
        var values = args.Where(t => t.Kind != CssTokenKind.Comma).ToList();
        if (values.Count is not (3 or 4) || args.Count != values.Count * 2 - 1
            || args.Where((_, i) => i % 2 == 1).Any(t => t.Kind != CssTokenKind.Comma)
            || values.Take(3).Any(t => t.Kind is not (CssTokenKind.Number or CssTokenKind.Percentage))
            || values.Take(3).Select(t => t.Kind).Distinct().Count() != 1
            || values.Count == 4 && values[3].Kind is not (CssTokenKind.Number or CssTokenKind.Percentage)) { return null; }
        var numbers = values.Select(Number).ToList();
        if (numbers.Any(n => n is null)) { return null; }
        byte Channel(int index, bool alpha = false) => (byte)Math.Round(Math.Clamp(
            values[index].Kind == CssTokenKind.Percentage ? numbers[index]!.Value / 100 * 255
                : numbers[index]!.Value * (alpha ? 255 : 1), 0, 255),
            MidpointRounding.AwayFromZero);
        return new CssColor(Channel(0), Channel(1), Channel(2), values.Count == 4 ? Channel(3, true) : (byte)255);
    }

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
