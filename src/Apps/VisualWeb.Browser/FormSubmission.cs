using System.Text;
using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.Browser;

/// <summary>Caret edits for the focused browser-owned text field.</summary>
public enum FormEdit { Backspace, Delete, Left, Right, Up, Down, Home, End }

/// <summary>Bounded same-tab GET form submission helpers: entry list, urlencoded serializer and action URL mutation.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#constructing-the-form-data-set">constructing
/// the entry list</see>, <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#submit-mutate-action">mutate
/// action URL</see>; url; <see href="https://url.spec.whatwg.org/#concept-urlencoded-serializer">application/x-www-form-urlencoded
/// serializer</see>. Only UTF-8 output is implemented. file: actions are mutated like http(s), as the HTML scheme table leaves
/// file undefined. No POST, multipart, text/plain, dirname or script-visible events exist.</remarks>
public static class FormSubmission
{
    private static readonly UTF8Encoding Utf8 = new(false, false);

    /// <summary>Serializes name/value tuples with the urlencoded byte serializer; lone surrogates become U+FFFD.</summary>
    public static string Serialize(IEnumerable<(string Name, string Value)> entries, int maxCharacters = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var output = new StringBuilder();
        foreach (var (name, value) in entries)
        {
            if (output.Length > 0) { output.Append('&'); }
            Bytes(output, name);
            output.Append('=');
            Bytes(output, value);
            if (output.Length > maxCharacters) { throw new BrowserLimitException("Form submission URL length limit exceeded."); }
        }
        return output.ToString();
    }

    private static void Bytes(StringBuilder output, string text)
    {
        foreach (var b in Utf8.GetBytes(text))
        {
            if (b == 0x20) { output.Append('+'); }
            else if (b is (>= (byte)'0' and <= (byte)'9') or (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z')
                or (byte)'*' or (byte)'-' or (byte)'.' or (byte)'_')
            { output.Append((char)b); }
            else { output.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)); }
        }
    }

    /// <summary>Replaces the action URL's query with <paramref name="query"/>, keeping any fragment.</summary>
    public static BrowserUrl ApplyQuery(BrowserUrl action, string query, int maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(query);
        // Serialized URLs percent-encode '?' and '#' outside query/fragment, so the first '#' and the first '?' before it delimit them.
        var href = action.Href;
        var hash = href.IndexOf('#', StringComparison.Ordinal);
        var head = hash < 0 ? href : href[..hash];
        var fragment = hash < 0 ? "" : href[hash..];
        var mark = head.IndexOf('?', StringComparison.Ordinal);
        if (mark >= 0) { head = head[..mark]; }
        if ((long)head.Length + 1 + query.Length + fragment.Length > maxCharacters)
        { throw new BrowserLimitException("Form submission URL length limit exceeded."); }
        var result = head + "?" + query + fragment;
        var parsed = BrowserUrl.ParseResult(result).Url;
        if (parsed?.Href != result) { throw new PageNavigationException("Form action URL cannot carry the submitted query."); }
        return parsed;
    }

    /// <summary>Builds the tree-ordered entry list for <paramref name="form"/>; <paramref name="submitter"/> is -1 for the form itself.</summary>
    internal static List<(string Name, string Value)> Entries(IReadOnlyList<PageFormControl> controls, int form, int submitter,
        Func<int, string> value, Func<int, bool> isChecked)
    {
        var entries = new List<(string, string)>();
        for (var index = 0; index < controls.Count; index++)
        {
            var control = controls[index];
            if (control.Form != form || control.Disabled || control.Kind is "submit" or "button" && index != submitter
                || control.Kind == "checkbox" && !isChecked(index) || control.Name.Length == 0) { continue; }
            var data = control.Kind == "hidden" && control.Name.Equals("_charset_", StringComparison.OrdinalIgnoreCase)
                ? "UTF-8" : value(index);
            entries.Add((Newlines(control.Name), Newlines(data)));
        }
        return entries;
    }

    // Spec: html; https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#converting-an-entry-list-to-a-list-of-name-value-pairs
    private static string Newlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", "\r\n", StringComparison.Ordinal);
}
