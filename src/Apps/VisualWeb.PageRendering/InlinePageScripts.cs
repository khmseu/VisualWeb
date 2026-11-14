using VisualWeb.Core.Url;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Scripting;

namespace VisualWeb.PageRendering;

/// <summary>Finite parser-blocking classic-script policy for opted-in page rendering.</summary>
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

    public static bool IsJavaScriptMimeType(string value) =>
        value is not null && ClassicTypes.Contains(value.Trim(' ', '\t', '\n', '\r', '\f'));

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

    /// <summary>Run the initial batch and return the caller-owned document host that retains its listeners.</summary>
    /// <remarks>The host is creating-thread-affine; it is disposed here on failure and by the renderer otherwise.</remarks>
    internal static (V8ScriptHost Host, int Scripts) Execute(DomDocument document, CancellationToken cancellationToken)
    {
        var sources = Collect(document, cancellationToken);
        var host = new V8ScriptHost(document: document, enableMicrotasks: true, enableEvents: true, enableDocumentLifecycle: true);
        try
        {
            host.ExecuteInitialDocumentBatch(sources, cancellationToken);
            return (host, sources.Count);
        }
        catch { host.Dispose(); throw; }
    }

    internal static (HtmlParseResult Parsed, V8ScriptHost Host, int Scripts) ParseAndExecute(string html,
        BrowserUrl pageUrl, BrowserUrl? baseUrl, HtmlParserOptions options, PageScriptResourceLoader? scriptLoader,
        CancellationToken cancellationToken)
    {
        var document = new DomDocument();
        var host = new V8ScriptHost(document: document, enableMicrotasks: true, enableEvents: true, enableDocumentLifecycle: true);
        HtmlParseResult? parsed = null;
        var scripts = 0;
        try
        {
            host.ExecuteInitialDocumentParserBatch((execute, taskToken) =>
            {
                parsed = HtmlParser.Parse(html, options, taskToken, document, element =>
                {
                    if (!IsClassicScript(element)) { return; }
                    var effectiveBase = ParserBaseUrl(document, pageUrl, baseUrl, taskToken);
                    var source = ReadClassicSource(element, pageUrl, effectiveBase, scriptLoader, taskToken);
                    if (source.Length == 0) { return; }
                    execute(source);
                    scripts++;
                });
            }, cancellationToken);
            return (parsed ?? throw new InvalidOperationException("HTML parsing did not return a document."),
                host, scripts);
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private static bool IsClassicScript(DomElement element)
    {
        var type = element.GetAttribute("type");
        var language = element.GetAttribute("language");
        var kind = type is not null ? type.Length == 0 ? "text/javascript" : type.Trim(' ', '\t', '\n', '\r', '\f')
            : string.IsNullOrEmpty(language) ? "text/javascript" : "text/" + language;
        if (kind.Any(character => character > 0x7f)) { return false; }
        if (kind.Equals("module", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("importmap", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("speculationrules", StringComparison.OrdinalIgnoreCase))
        {
            throw new PageNavigationException("Modules, import maps and speculation rules are deferred.");
        }
        return ClassicTypes.Contains(kind) && element.GetAttribute("nomodule") is null;
    }

    private static string ReadClassicSource(DomElement element, BrowserUrl pageUrl, BrowserUrl? baseUrl,
        PageScriptResourceLoader? scriptLoader, CancellationToken cancellationToken)
    {
        if (element.GetAttribute("async") is not null || element.GetAttribute("defer") is not null
            || element.GetAttribute("for") is not null || element.GetAttribute("event") is not null)
        {
            throw new PageNavigationException("Script scheduling attributes are outside the parser-blocking classic subset.");
        }
        if (element.GetAttribute("src") is { } sourceUrl)
        {
            if (element.GetAttribute("integrity") is not null || element.GetAttribute("crossorigin") is not null
                || element.GetAttribute("referrerpolicy") is not null || element.GetAttribute("fetchpriority") is not null
                || element.GetAttribute("nonce") is not null)
            {
                throw new PageNavigationException(
                    "External script integrity, CORS mode, referrer policy, fetch priority and nonce authorization are unsupported.");
            }
            if (scriptLoader is null)
            {
                throw new PageNavigationException("External scripts require the browser-owned script resource broker.");
            }
            var url = BrowserUrl.Parse(sourceUrl, baseUrl ?? pageUrl);
            return scriptLoader(url, cancellationToken).GetAwaiter().GetResult();
        }
        return string.Concat(element.ChildNodes.OfType<DomText>().Select(node => node.Data));
    }

    private static BrowserUrl ParserBaseUrl(DomDocument document, BrowserUrl pageUrl, BrowserUrl? approvedBaseUrl,
        CancellationToken cancellationToken)
    {
        var parsedBaseUrl = LinkedStylesheets.BaseHrefUrl(document, pageUrl, cancellationToken);
        if (approvedBaseUrl is null) { return parsedBaseUrl ?? pageUrl; }
        return parsedBaseUrl?.Href == approvedBaseUrl.Href ? approvedBaseUrl : pageUrl;
    }
}
