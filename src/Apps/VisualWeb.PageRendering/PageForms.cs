using VisualWeb.Core.Url;
using VisualWeb.Engine.Dom;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.PageRendering;

/// <summary>Collects the bounded data-only form subset from a rendered document in tree order.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/forms.html#the-form-element">form</see>,
/// <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-attributes">form
/// submission attributes</see> and <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-owner">form
/// owner</see>. The parser keeps form ownership equal to the nearest ancestor form, so form-attribute owners are
/// rejected. Anything outside same-tab GET with urlencoded data becomes a visible per-form error instead of a
/// partial submission. Limits throw <see cref="PageNavigationException"/>.</remarks>
internal static class PageForms
{
    private static readonly string[] Utf8Labels =
        ["unicode-1-1-utf-8", "unicode11utf8", "unicode20utf8", "utf-8", "utf8", "x-unicode20utf8"];
    private static readonly string[] SubmitterOverrides = ["formaction", "formenctype", "formmethod", "formnovalidate", "formtarget"];
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
        var baseTarget = elements.Any(e => e.LocalName == "base" && e.GetAttribute("target") is not null);
        var baseHref = elements.Any(e => e.LocalName == "base" && e.GetAttribute("href") is not null);
        var formAttribute = elements.Any(e => e.LocalName is "button" or "fieldset" or "input" or "object" or "output"
            or "select" or "textarea" && e.GetAttribute("form") is not null);
        var lastLink = 0;
        var patternCount = 0;
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName == "form")
            {
                if (formIndex.Count >= RendererProtocol.MaxForms)
                { throw new PageNavigationException($"Renderer form count limit ({RendererProtocol.MaxForms}) exceeded."); }
                var (action, error) = Form(element, url, baseTarget, baseHref, formAttribute);
                formIndex.Add(element, formIndex.Count);
                actions.Add(action);
                errors.Add(error);
                continue;
            }
            if (element.LocalName is not ("input" or "button" or "textarea" or "select" or "output" or "object")) { continue; }
            var owner = Owner(element);
            var index = owner is null ? -1 : formIndex[owner];
            var kind = Kind(element);
            if (kind is null)
            {
                if (element.LocalName == "button" && DomFormControls.ButtonType(element) == "button") { continue; }
                Reject(index, element.LocalName == "input" ? $"input type={DomFormControls.InputType(element)}"
                    : element.LocalName == "button" ? "button type=reset" : $"<{element.LocalName}>");
                continue;
            }
            if (kind is "text" or "search" or "email" or "tel" or "url")
            {
                foreach (var name in TextOnlyAttributes)
                {
                    if (element.GetAttribute(name) is not null) { Reject(index, $"text field attribute {name}"); }
                }
            }
            if (kind == "email" && element.GetAttribute("multiple") is not null)
            { Reject(index, "email multiple addresses"); }
            string? pattern = null;
            if ((kind is "text" or "search" or "email" or "tel" or "url") && element.GetAttribute("pattern") is { } sourcePattern)
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
            var text = kind is "text" or "search" or "email" or "tel" or "url" or "textarea";
            var minLength = text ? MinLength(element.GetAttribute("minlength")) : -1;
            if (minLength > RendererProtocol.MaxTextCharacters)
            { throw new PageNavigationException($"Form minlength exceeds the supported {RendererProtocol.MaxTextCharacters} code-unit limit."); }
            var value = kind == "textarea" ? NormalizeTextArea(element.TextContent ?? "")
                : kind is "checkbox" or "radio" ? element.GetAttribute("value") ?? "on" : element.GetAttribute("value");
            value = kind switch
            {
                "text" or "search" or "email" or "tel" or "url" => (value ?? "").Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal),
                _ => value ?? "",
            };
            var label = kind switch
            {
                "submit" => element.GetAttribute("value") ?? "Submit",
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
                text && element.GetAttribute("readonly") is not null,
                (text || kind is "checkbox" or "radio") && element.GetAttribute("required") is not null,
                minLength,
                text ? MaxLength(element.GetAttribute("maxlength")) : -1, lastLink, rect,
                kind is "checkbox" or "radio" && element.GetAttribute("checked") is not null));
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

    private static (string Action, string? Error) Form(DomElement form, BrowserUrl url, bool baseTarget, bool baseHref,
        bool formAttribute)
    {
        var method = form.GetAttribute("method")?.ToLowerInvariant();
        if (method is "post" or "dialog")
        { return ("", $"Unsupported form method: {method} (only GET submission is implemented)."); }
        if (form.GetAttribute("enctype") is { } enctype && !enctype.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            && (enctype.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) || enctype.Equals("text/plain", StringComparison.OrdinalIgnoreCase)))
        { return ("", $"Unsupported form enctype: {enctype}."); }
        if (form.GetAttribute("target") is { } target && target.Length != 0 && !target.Equals("_self", StringComparison.OrdinalIgnoreCase)
            || baseTarget && form.GetAttribute("target") is null)
        { return ("", "Unsupported form target: only same-tab (_self) submission is implemented."); }
        if (form.GetAttribute("novalidate") is not null)
        { return ("", "Unsupported form novalidate: constraint validation cannot be bypassed in this subset."); }
        if (form.GetAttribute("accept-charset") is { } charset
            && !charset.Split([' ', '\t', '\n', '\f', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .All(label => Utf8Labels.Contains(label.ToLowerInvariant())))
        { return ("", "Unsupported form accept-charset: only UTF-8 labels are implemented."); }
        if (formAttribute) { return ("", "Unsupported form attribute owners: controls must be descendants of their form."); }
        if (baseHref) { return ("", "Unsupported form action resolution with <base href>."); }
        var raw = form.GetAttribute("action") ?? "";
        if (raw.Length > RendererProtocol.MaxTextCharacters) { return ("", "Form action URL limit exceeded."); }
        var parsed = raw.Trim(' ', '\t', '\n', '\f', '\r').Length == 0 ? url : BrowserUrl.ParseResult(raw, url).Url;
        if (parsed is null) { return ("", "Invalid form action URL."); }
        if (parsed.Protocol is not ("http:" or "https:" or "file:" or "data:"))
        { return ("", $"Unsupported form action URL scheme: {parsed.Protocol}"); }
        if (parsed.Href.Length > RendererProtocol.MaxTextCharacters) { return ("", "Form action URL limit exceeded."); }
        return (parsed.Href, null);
    }

    private static string? Kind(DomElement element) => element.LocalName switch
    {
        "input" => DomFormControls.InputType(element) is var type && type is "text" or "search" or "email" or "tel" or "url" or "checkbox" or "radio" or "hidden" or "submit" ? type : null,
        "button" => DomFormControls.ButtonType(element) == "submit" ? "button" : null,
        "textarea" => "textarea",
        _ => null,
    };

    private static string NormalizeTextArea(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static DomElement? Owner(DomElement element) => Ancestors(element).FirstOrDefault(a => a.LocalName == "form");

    private static IEnumerable<DomElement> Ancestors(DomElement element)
    {
        for (var node = element.ParentNode; node is not null; node = node.ParentNode)
        {
            if (node is DomElement parent) { yield return parent; }
        }
    }

    // Spec: html; https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-non-negative-integers
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
