using VisualWeb.Core.Url;
using VisualWeb.Engine.Dom;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.PageRendering;

/// <summary>Collects the bounded data-only form subset from a rendered document in tree order.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/forms.html#the-form-element">form</see>,
/// <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-attributes">form
/// submission attributes</see> and <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-owner">form
/// owner</see>. Controls use the first matching ID for an explicit <c>form</c> attribute, otherwise the nearest
/// ancestor form; unsupported form-associated elements remain visible errors. Anything outside same-tab GET with
/// urlencoded data becomes a visible per-form error instead of a
/// partial submission. Limits throw <see cref="PageNavigationException"/>.</remarks>
internal static class PageForms
{
    private static readonly char[] AsciiWhitespace = [' ', '\t', '\n', '\r', '\f'];
    private static readonly string[] Utf8Labels =
        ["unicode-1-1-utf-8", "unicode11utf8", "unicode20utf8", "utf-8", "utf8", "x-unicode20utf8"];
    private static readonly string[] SubmitterOverrides = ["formaction", "formenctype", "formmethod", "formtarget"];
    private static readonly string[] TextOnlyAttributes = ["dirname", "list"];

    public static (IReadOnlyList<PageForm> Forms, IReadOnlyList<PageFormControl> Controls) Collect(DomDocument document,
        BrowserUrl url, IReadOnlyDictionary<DomElement, (PageLinkRect Rect, int BeforeLink)> geometry, int linkCount,
        double width, double height, CancellationToken cancellationToken)
    {
        var formIndex = new Dictionary<DomElement, int>();
        var errors = new List<string?>();
        var actions = new List<string>();
        var controls = new List<PageFormControl>();
        var elements = document.Descendants().OfType<DomElement>().ToList();
        var baseTarget = BaseTarget(elements, cancellationToken);
        var baseUrl = BaseUrl(elements, url, cancellationToken);
        var firstElementById = new Dictionary<string, DomElement>(StringComparer.Ordinal);
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.GetAttribute("id") is { Length: > 0 } id) { firstElementById.TryAdd(id, element); }
        }
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName != "form") { continue; }
            if (formIndex.Count >= RendererProtocol.MaxForms)
            { throw new PageNavigationException($"Renderer form count limit ({RendererProtocol.MaxForms}) exceeded."); }
            var (action, error) = Form(element, url, baseUrl, baseTarget);
            formIndex.Add(element, formIndex.Count);
            actions.Add(action);
            errors.Add(error);
        }
        var lastLink = 0;
        var patternCount = 0;
        var optionCount = 0;
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName == "form") { continue; }
            var owner = Owner(element, firstElementById);
            var index = owner is null ? -1 : formIndex[owner];
            if (element.LocalName == "fieldset" && element.GetAttribute("form") is not null)
            { Reject(index, "fieldset form owner"); }
            if (element.LocalName is not ("input" or "button" or "textarea" or "select" or "output" or "object")) { continue; }
            var kind = Kind(element);
            if (kind is null)
            {
                if (element.LocalName == "button" && DomFormControls.ButtonType(element) == "button") { continue; }
                Reject(index, element.LocalName == "input" ? $"input type={DomFormControls.InputType(element)}"
                    : element.LocalName == "button" ? "button type=reset" : $"<{element.LocalName}>");
                continue;
            }
            if (kind is "text" or "search" or "email" or "tel" or "url" or "password" or "number")
            {
                foreach (var name in TextOnlyAttributes)
                {
                    if (element.GetAttribute(name) is not null) { Reject(index, $"text field attribute {name}"); }
                }
            }
            if (kind == "select")
            {
                if (element.GetAttribute("multiple") is not null) { Reject(index, "select multiple"); }
                if (SelectSize(element.GetAttribute("size")) != 1) { Reject(index, "select display size other than 1"); }
            }
            if (kind is "date" or "time" or "month" or "week")
            {
                foreach (var attribute in new[] { "min", "max", "step", "pattern", "minlength", "maxlength", "list", "multiple", "dirname" })
                { if (element.GetAttribute(attribute) is not null) { Reject(index, $"{kind} {attribute}"); } }
            }
            if (kind is "number" or "range")
            {
                if (element.GetAttribute("multiple") is not null) { Reject(index, $"{kind} multiple"); }
                foreach (var attribute in new[] { "pattern", "minlength", "maxlength", "list" })
                { if (element.GetAttribute(attribute) is not null) { Reject(index, $"{kind} {attribute}"); } }
            }
            string? pattern = null;
            if ((kind is "text" or "search" or "email" or "tel" or "url" or "password") && element.GetAttribute("pattern") is { } sourcePattern)
            {
                if (sourcePattern.Length > FormPattern.MaxCharacters || ++patternCount > RendererProtocol.MaxFormPatterns
                    || !FormPattern.IsValid(sourcePattern))
                { Reject(index, "unsupported or invalid pattern expression"); }
                else { pattern = sourcePattern; }
            }
            else if (kind == "textarea" && element.GetAttribute("pattern") is not null)
            { Reject(index, "textarea pattern"); }
            if (kind == "textarea")
            {
                if (element.GetAttribute("dirname") is not null) { Reject(index, "textarea dirname"); }
                if (element.GetAttribute("wrap")?.Equals("hard", StringComparison.OrdinalIgnoreCase) == true)
                { Reject(index, "textarea wrap=hard"); }
            }
            if (kind is "submit" or "button")
            {
                foreach (var name in SubmitterOverrides)
                {
                    if (element.GetAttribute(name) is not null) { Reject(index, $"submit button override {name}"); }
                }
            }
            if (Ancestors(element).Any(a => a.LocalName == "datalist"
                || a.LocalName == "fieldset" && a.GetAttribute("disabled") is not null))
            {
                Reject(index, "controls inside disabled fieldsets or datalists");
            }
            if (controls.Count >= RendererProtocol.MaxFormControls)
            { throw new PageNavigationException($"Renderer form control count limit ({RendererProtocol.MaxFormControls}) exceeded."); }
            var text = kind is "text" or "search" or "email" or "tel" or "url" or "password" or "number" or "textarea";
            var range = kind == "range";
            var minLength = text && kind != "number" ? MinLength(element.GetAttribute("minlength")) : -1;
            if (minLength > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException($"Form minlength exceeds the supported {RendererProtocol.MaxTextCharacters} code-unit limit."); }
            var options = kind == "select" ? Options(element, ref optionCount) : Array.Empty<PageFormOption>();
            var selectedOption = Array.FindLastIndex(options, option => option.Selected);
            if (kind == "select" && selectedOption < 0)
            { selectedOption = Array.FindIndex(options, option => !option.Disabled); }
            if (kind == "select" && selectedOption >= 0)
            { options = options.Select((option, optionIndex) => option with { Selected = optionIndex == selectedOption }).ToArray(); }
            var placeholder = kind is "text" or "search" or "email" or "tel" or "url" or "password" or "number" or "textarea"
                ? element.GetAttribute("placeholder") ?? "" : "";
            placeholder = kind == "textarea" ? NormalizeTextArea(placeholder)
                : placeholder.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
            if (placeholder.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Form placeholder text limit exceeded."); }
            var value = kind == "textarea" ? NormalizeTextArea(element.TextContent ?? "")
                : kind == "select" ? selectedOption >= 0 ? options[selectedOption].Value : ""
                : kind is "checkbox" or "radio" ? element.GetAttribute("value") ?? "on" : element.GetAttribute("value");
            var minimum = kind == "number" ? NumberBound(element.GetAttribute("min"))
                : range ? NumberBound(element.GetAttribute("min")) ?? 0 : null;
            var maximum = kind == "number" ? NumberBound(element.GetAttribute("max"))
                : range ? NumberBound(element.GetAttribute("max")) ?? 100 : null;
            var stepAny = (kind is "number" or "range") && element.GetAttribute("step")?.Equals("any", StringComparison.OrdinalIgnoreCase) == true;
            double? step = kind is not ("number" or "range") || stepAny ? null
                : NumberBound(element.GetAttribute("step")) is { } parsedStep && parsedStep > 0 ? parsedStep : 1;
            if (minimum is { } min && maximum is { } max && min > max)
            {
                Reject(index, $"{kind} minimum exceeds maximum");
                if (range) { maximum = minimum; }
            }
            if (range)
            {
                var lower = minimum!.Value;
                var upper = maximum!.Value;
                var initial = FormNumber.TryParse(value ?? "", out var parsed) ? parsed : lower / 2 + upper / 2;
                initial = Math.Clamp(initial, lower, upper);
                if (!stepAny && step is { } rangeStep)
                {
                    var quotient = (initial - lower) / rangeStep;
                    if (double.IsFinite(quotient) && Math.Abs(quotient) <= 1_000_000_000_000d)
                    {
                        var snapped = lower + Math.Floor(quotient + 0.5) * rangeStep;
                        if (snapped > upper) { snapped = lower + Math.Floor(quotient) * rangeStep; }
                        initial = Math.Clamp(snapped, lower, upper);
                    }
                    else { Reject(index, "range step precision limit"); initial = lower; }
                }
                value = initial.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            value = kind switch
            {
                "text" or "search" or "tel" or "url" or "password" => (value ?? "").Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal),
                "email" => FormEmail.Sanitize(value ?? "", element.GetAttribute("multiple") is not null),
                "date" => FormDate.IsValid(value ?? "") ? value ?? "" : "",
                "time" => FormTime.IsValid(value ?? "") ? value ?? "" : "",
                "month" => FormMonth.IsValid(value ?? "") ? value ?? "" : "",
                "week" => FormWeek.IsValid(value ?? "") ? value ?? "" : "",
                _ => value ?? "",
            };
            var label = kind switch
            {
                "submit" => element.GetAttribute("value") ?? "Submit",
                "reset" when element.LocalName == "input" => element.GetAttribute("value") ?? "Reset",
                "reset" => element.TextContent ?? "",
                "button" => element.TextContent ?? "",
                _ => "",
            };
            var controlName = element.GetAttribute("name") ?? "";
            if (controlName.Length > RendererProtocol.MaxTextCharacters || value.Length > RendererProtocol.MaxTextCharacters
                || label.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Renderer form control text limit exceeded."); }
            PageLinkRect? rect = null;
            if (kind != "hidden" && geometry.TryGetValue(element, out var placed))
            {
                rect = placed.Rect;
                lastLink = placed.BeforeLink;
            }
            controls.Add(new(index, kind, controlName, value, label, pattern, element.GetAttribute("disabled") is not null,
                (text || kind is "date" or "time" or "month" or "week") && element.GetAttribute("readonly") is not null,
                (text || kind is "checkbox" or "radio" or "select" or "date" or "time" or "month" or "week") && element.GetAttribute("required") is not null,
                minLength,
                text && kind != "number" ? MaxLength(element.GetAttribute("maxlength")) : -1, lastLink, rect,
                kind is "checkbox" or "radio" && element.GetAttribute("checked") is not null, minimum, maximum, step, stepAny)
            {
                Options = options,
                Multiple = kind == "email" && element.GetAttribute("multiple") is not null,
                Placeholder = placeholder,
                FormNoValidate = (kind is "submit" or "button") && element.GetAttribute("formnovalidate") is not null
            });
            if (kind == "radio" && controlName.Length > 0 && controls[^1].Checked)
            {
                for (var previous = controls.Count - 2; previous >= 0; previous--)
                {
                    if (controls[previous] is { Kind: "radio", Checked: true, Name: var name, Form: var form }
                        && form == index && name == controlName)
                    { controls[previous] = controls[previous] with { Checked = false }; }
                }
            }
        }
        var forms = actions.Select((action, i) => errors[i] is { } error ? new PageForm("", error) : new PageForm(action, null)).ToArray();
        var result = controls.ToArray();
        try { RendererProtocol.ValidateForms(forms, result, linkCount, width, height); }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        return (Array.AsReadOnly(forms), Array.AsReadOnly(result));

        void Reject(int index, string what)
        {
            if (index >= 0) { errors[index] ??= $"Unsupported form semantics: {what}."; }
        }
    }

    private static (string Action, string? Error) Form(DomElement form, BrowserUrl url, BrowserUrl baseUrl,
        string? baseTarget)
    {
        var method = form.GetAttribute("method")?.ToLowerInvariant();
        if (method is "post" or "dialog")
        { return ("", $"Unsupported form method: {method} (only GET submission is implemented)."); }
        if (form.GetAttribute("enctype") is { } enctype && !enctype.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            && (enctype.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) || enctype.Equals("text/plain", StringComparison.OrdinalIgnoreCase)))
        { return ("", $"Unsupported form enctype: {enctype}."); }
        var target = (form.GetAttribute("target") ?? baseTarget)?.Trim(AsciiWhitespace);
        if (!string.IsNullOrEmpty(target) && !IsCurrentTarget(target))
        { return ("", "Unsupported form target: only current-tab targets are implemented."); }
        if (form.GetAttribute("novalidate") is not null)
        { return ("", "Unsupported form novalidate: constraint validation cannot be bypassed in this subset."); }
        if (form.GetAttribute("accept-charset") is { } charset
            && !charset.Split([' ', '\t', '\n', '\f', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .All(label => Utf8Labels.Contains(label.ToLowerInvariant())))
        { return ("", "Unsupported form accept-charset: only UTF-8 labels are implemented."); }
        var raw = form.GetAttribute("action");
        if (raw is { Length: > RendererProtocol.MaxTextCharacters }) { return ("", "Form action URL limit exceeded."); }
        var parsed = string.IsNullOrEmpty(raw) ? url : BrowserUrl.ParseResult(raw, baseUrl).Url;
        if (parsed is null) { return ("", "Invalid form action URL."); }
        if (parsed.Protocol is not ("http:" or "https:" or "file:" or "data:"))
        { return ("", $"Unsupported form action URL scheme: {parsed.Protocol}"); }
        if (parsed.Href.Length > RendererProtocol.MaxTextCharacters) { return ("", "Form action URL limit exceeded."); }
        return (parsed.Href, null);
    }

    private static string? BaseTarget(IReadOnlyList<DomElement> elements, CancellationToken cancellationToken)
    {
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName == "base" && element.GetAttribute("target") is { } target) { return target; }
        }
        return null;
    }

    private static BrowserUrl BaseUrl(IReadOnlyList<DomElement> elements, BrowserUrl fallback,
        CancellationToken cancellationToken)
    {
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName != "base" || element.GetAttribute("href") is not { } href) { continue; }
            if (href.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Renderer base URL limit exceeded."); }
            var parsed = BrowserUrl.ParseResult(href, fallback).Url;
            if (parsed is null || parsed.Protocol is "data:" or "javascript:") { return fallback; }
            if (parsed.Href.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Renderer base URL limit exceeded."); }
            return parsed;
        }
        return fallback;
    }

    private static bool IsCurrentTarget(string target) => target.All(character => character <= 0x7f)
        && (target.Equals("_self", StringComparison.OrdinalIgnoreCase)
            || target.Equals("_parent", StringComparison.OrdinalIgnoreCase)
            || target.Equals("_top", StringComparison.OrdinalIgnoreCase));

    private static string? Kind(DomElement element) => element.LocalName switch
    {
        "input" => DomFormControls.InputType(element) is var type && type is "text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number" or "range" or "checkbox" or "radio" or "hidden" or "submit" or "reset" ? type : null,
        "button" => DomFormControls.ButtonType(element) switch
        {
            "submit" => "button",
            "reset" => "reset",
            _ => null,
        },
        "textarea" => "textarea",
        "select" => "select",
        _ => null,
    };

    private static int SelectSize(string? value)
    {
        if (value is null) { return 1; }
        var text = value.TrimStart(' ', '\t', '\n', '\f', '\r');
        var negative = text.StartsWith('-');
        var position = text.StartsWith('+') || negative ? 1 : 0;
        var end = position;
        while (end < text.Length && char.IsAsciiDigit(text[end])) { end++; }
        if (end == position) { return 1; }
        var digits = text.AsSpan(position, end - position);
        var firstNonZero = 0;
        while (firstNonZero < digits.Length && digits[firstNonZero] == '0') { firstNonZero++; }
        if (negative && firstNonZero < digits.Length) { return 1; }
        if (firstNonZero == digits.Length) { return 0; }
        return digits.Length - firstNonZero == 1 && digits[firstNonZero] == '1' ? 1 : 2;
    }

    private static PageFormOption[] Options(DomElement select, ref int total)
    {
        var elements = select.ChildNodes.OfType<DomElement>().ToArray();
        if ((long)total + elements.Length > RendererProtocol.MaxSelectOptions)
        { throw new PageNavigationException("Select option count limit exceeded."); }
        total += elements.Length;
        var result = new PageFormOption[elements.Length];
        for (var index = 0; index < elements.Length; index++)
        {
            var option = elements[index];
            if (option.LocalName != "option")
            { throw new PageNavigationException("Only direct option children are supported in select controls."); }
            var text = OptionText(option.TextContent ?? "");
            var value = option.GetAttribute("value") ?? text;
            var label = option.GetAttribute("label") ?? text;
            if (value.Length > RendererProtocol.MaxTextCharacters || label.Length > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException("Select option text limit exceeded."); }
            result[index] = new(value, label, option.GetAttribute("disabled") is not null,
                option.GetAttribute("selected") is not null);
        }
        return result;
    }

    private static string OptionText(string value) =>
        string.Join(' ', value.Split(AsciiWhitespace, StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeTextArea(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static DomElement? Owner(DomElement element, IReadOnlyDictionary<string, DomElement> firstElementById)
    {
        if (element.GetAttribute("form") is { } formId)
        {
            return formId.Length > 0 && firstElementById.TryGetValue(formId, out var target) && target.LocalName == "form"
                ? target : null;
        }
        return Ancestors(element).FirstOrDefault(ancestor => ancestor.LocalName == "form");
    }

    private static IEnumerable<DomElement> Ancestors(DomElement element)
    {
        for (var node = element.ParentNode; node is not null; node = node.ParentNode)
        {
            if (node is DomElement parent) { yield return parent; }
        }
    }

    private static double? NumberBound(string? value) =>
        value is not null && FormNumber.TryParse(value, out var number) ? number : null;

    // Spec: html; https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#valid-floating-point-number
    private static int MinLength(string? value)
    {
        if (value is null) { return -1; }
        var text = value.TrimStart(' ', '\t', '\n', '\f', '\r');
        if (text.StartsWith('+')) { text = text[1..]; }
        var digits = text.TakeWhile(char.IsAsciiDigit).Count();
        if (digits == 0) { return -1; }
        if (!int.TryParse(text.AsSpan(0, digits), out var parsed)) { return RendererProtocol.MaxTextCharacters + 1; }
        return parsed;
    }

    // Spec: html; https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-non-negative-integers
    private static int MaxLength(string? value)
    {
        if (value is null) { return -1; }
        var text = value.TrimStart(' ', '\t', '\n', '\f', '\r');
        var negative = text.StartsWith('-');
        if (negative || text.StartsWith('+')) { text = text[1..]; }
        var digits = text.TakeWhile(char.IsAsciiDigit).Count();
        if (digits == 0) { return -1; }
        return !int.TryParse(text.AsSpan(0, digits), out var parsed) || negative && parsed != 0 ? -1 : parsed;
    }
}
