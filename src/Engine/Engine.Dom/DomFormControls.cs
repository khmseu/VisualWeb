namespace VisualWeb.Engine.Dom;

/// <summary>Static HTML form-control attribute reflection shared by layout and page rendering.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/input.html#attr-input-type">input type state</see>
/// and <see href="https://html.spec.whatwg.org/multipage/form-elements.html#attr-button-type">button type state</see>.
/// Only the enumerated-attribute mapping is provided; control behavior lives in the browser-owned subset.</remarks>
public static class DomFormControls
{
    private static readonly HashSet<string> InputTypes = new(StringComparer.Ordinal)
    {
        "hidden", "text", "search", "tel", "url", "email", "password", "date", "month", "week", "time",
        "datetime-local", "number", "range", "color", "checkbox", "radio", "file", "submit", "image", "reset", "button"
    };

    /// <summary>Returns the lowercase input type keyword; missing and invalid values map to "text".</summary>
    public static string InputType(DomElement input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var value = input.GetAttribute("type")?.ToLowerInvariant();
        return value is not null && InputTypes.Contains(value) && IsAscii(input.GetAttribute("type")!) ? value : "text";
    }

    /// <summary>Returns submit, reset or button; missing and invalid values map to submit.</summary>
    public static string ButtonType(DomElement button)
    {
        ArgumentNullException.ThrowIfNull(button);
        return button.GetAttribute("type") is { } value && IsAscii(value) && value.ToLowerInvariant() is "reset" or "button" or "submit"
            ? value.ToLowerInvariant() : "submit";
    }

    private static bool IsAscii(string value) => value.All(char.IsAscii);
}
