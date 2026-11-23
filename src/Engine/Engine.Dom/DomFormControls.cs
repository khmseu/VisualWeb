namespace VisualWeb.Engine.Dom;

/// <summary>Bounded HTML form-control helpers shared by the DOM, layout and page rendering.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/input.html#attr-input-type">input type state</see>
/// and <see href="https://html.spec.whatwg.org/multipage/form-elements.html#attr-button-type">button type state</see>.
/// Type-state reflection is limited to enumerated mappings; control state and submission remain browser-owned.</remarks>
public static class DomFormControls
{
    private static readonly HashSet<string> InputTypes = new(StringComparer.Ordinal)
    {
        "hidden", "text", "search", "tel", "url", "email", "password", "date", "month", "week", "time",
        "datetime-local", "number", "range", "color", "checkbox", "radio", "file", "submit", "image", "reset", "button"
    };

    /// <summary>Returns the current form owner of a built-in listed form-associated element, or null.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-owner">form owner</see>
    /// and <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#reset-the-form-owner">reset the form owner</see>.
    /// The association is computed from the current tree and attributes; parser-inserted associations and custom elements are not modeled.</remarks>
    public static DomElement? FormOwner(DomElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.LocalName is not ("button" or "fieldset" or "input" or "object" or "output" or "select" or "textarea"))
        {
            throw new ArgumentException("Element is not a supported listed form-associated element.", nameof(element));
        }

        if (element.GetAttribute("form") is { } formId && element.IsConnected)
        {
            DomNode root = element;
            while (root.ParentNode is { } parent)
            {
                root = parent;
            }

            var firstElementWithId = root.Descendants().OfType<DomElement>()
                .FirstOrDefault(candidate => candidate.GetAttribute("id") == formId);
            return firstElementWithId?.LocalName == "form" ? firstElementWithId : null;
        }

        for (var ancestor = element.ParentNode; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            if (ancestor is DomElement { LocalName: "form" } form)
            {
                return form;
            }
        }

        return null;
    }

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
