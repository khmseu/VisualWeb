using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Scripting;

namespace VisualWeb.PageRendering;

/// <summary>Finite post-parse inline classic-script policy, not HTML parser-blocking scheduling.</summary>
/// <remarks>Specs: html, mime-sniffing;
/// <see href="https://html.spec.whatwg.org/multipage/scripting.html#prepare-the-script-element">script preparation</see>,
/// <see href="https://mimesniff.spec.whatwg.org/#javascript-mime-type-essence-match">script type matching</see>.</remarks>
public static class InlinePageScripts
{
    private static readonly HashSet<string> ClassicTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/ecmascript", "application/javascript", "application/x-ecmascript", "application/x-javascript",
        "text/ecmascript", "text/javascript", "text/javascript1.0", "text/javascript1.1", "text/javascript1.2",
        "text/javascript1.3", "text/javascript1.4", "text/javascript1.5", "text/jscript", "text/livescript",
        "text/x-ecmascript", "text/x-javascript"
    };

    public static IReadOnlyList<string> Collect(DomDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sources = new List<string>();
        var characters = 0;
        foreach (var element in document.Descendants().OfType<DomElement>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName != "script") { continue; }
            var type = element.GetAttribute("type");
            var language = element.GetAttribute("language");
            var kind = type is not null ? type.Length == 0 ? "text/javascript" : type.Trim(' ', '\t', '\n', '\r', '\f')
                : string.IsNullOrEmpty(language) ? "text/javascript" : "text/" + language;
            if (kind.Any(character => character > 0x7f)) { continue; }
            if (kind.Equals("module", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("importmap", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("speculationrules", StringComparison.OrdinalIgnoreCase))
            {
                throw new PageNavigationException("Modules, import maps and speculation rules are deferred.");
            }
            if (!ClassicTypes.Contains(kind) || element.GetAttribute("nomodule") is not null) { continue; }
            if (element.GetAttribute("src") is not null)
            {
                throw new PageNavigationException("External scripts are deferred; no script was fetched.");
            }
            if (element.GetAttribute("async") is not null || element.GetAttribute("defer") is not null
                || element.GetAttribute("for") is not null || element.GetAttribute("event") is not null)
            {
                throw new PageNavigationException("Script scheduling attributes are outside the post-parse inline subset.");
            }
            var nodes = element.ChildNodes.OfType<DomText>().ToArray();
            var length = nodes.Sum(node => (long)node.Data.Length);
            if (length == 0) { continue; }
            if (sources.Count >= V8ScriptHost.MaxBatchScripts || length > V8ScriptHost.MaxSourceCharacters
                || characters + length > V8ScriptHost.MaxBatchSourceCharacters)
            {
                throw new ScriptLimitException("Inline script source count/character limit exceeded.");
            }
            characters += (int)length;
            sources.Add(string.Concat(nodes.Select(node => node.Data)));
        }
        return sources.AsReadOnly();
    }

    internal static int Execute(DomDocument document, CancellationToken cancellationToken)
    {
        var sources = Collect(document, cancellationToken);
        using var host = new V8ScriptHost(document: document, enableMicrotasks: true, enableEvents: true, enableDocumentLifecycle: true);
        host.ExecuteInitialDocumentBatch(sources, cancellationToken);
        return sources.Count;
    }
}
