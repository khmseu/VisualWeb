using System.Diagnostics;
using System.Globalization;
using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;
using VisualWeb.PageRendering;
using VisualWeb.Platform.Linux.Sandbox;
using VisualWeb.Platform.Windows.Sandbox;

var startupTimingRequested = Array.IndexOf(args, "--smoke-startup-timings") >= 0;
var managedEntry = startupTimingRequested ? Stopwatch.GetTimestamp() : 0L;
if (args.Length < 3 || args[1] != "--font"
    || args[0] is not ("--development-unsandboxed" or "--linux-sandbox-bootstrap" or "--linux-sandbox-worker" or "--windows-sandbox-worker"))
{
    Console.Error.WriteLine("Renderer is an internal worker. Usage: (--development-unsandboxed | --linux-sandbox-bootstrap | --windows-sandbox-worker) --font TRUSTED_FONT [--font-bold PATH] [--font-italic PATH] [--font-bold-italic PATH]");
    return 2;
}
try
{
    var mode = args[0];
    var fontPath = args[2];
    ArgumentException.ThrowIfNullOrWhiteSpace(fontPath);
    string? bold = null, italic = null, boldItalic = null;
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var startupTimingEnabled = false;
    for (var index = 3; index < args.Length;)
    {
        var option = args[index];
        if (option == "--smoke-startup-timings")
        {
            if (startupTimingEnabled) { throw new ArgumentException("Duplicate renderer startup timing option."); }
            startupTimingEnabled = true;
            index++;
            continue;
        }
        if (index + 1 >= args.Length || !seen.Add(option)) { throw new ArgumentException("Missing or duplicate renderer font option: " + option); }
        var path = args[index + 1];
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.StartsWith("--", StringComparison.Ordinal)) { throw new ArgumentException("Missing renderer font path for " + option); }
        switch (option)
        {
            case "--font-bold": bold = path; break;
            case "--font-italic": italic = path; break;
            case "--font-bold-italic": boldItalic = path; break;
            default: throw new ArgumentException("Unknown renderer font option: " + option);
        }
        index += 2;
    }
    if (startupTimingEnabled && mode == "--linux-sandbox-bootstrap")
    { throw new ArgumentException("Startup timing is not supported by the Linux sandbox bootstrap."); }
    var managedPreflightMs = startupTimingEnabled
        ? Stopwatch.GetElapsedTime(managedEntry).TotalMilliseconds : 0;
    if (mode == "--linux-sandbox-bootstrap")
    {
        LinuxRendererSandbox.Enter(AppContext.BaseDirectory, fontPath,
            boldFontPath: bold, italicFontPath: italic, boldItalicFontPath: boldItalic);
        throw new InvalidOperationException("Sandbox bootstrap returned without confinement.");
    }
    var confined = mode == "--linux-sandbox-worker";
    var windowsConfined = mode == "--windows-sandbox-worker";
    var confinementStart = startupTimingEnabled ? Stopwatch.GetTimestamp() : 0L;
    if (confined) { LinuxRendererSandbox.VerifyWorker(); }
    if (windowsConfined) { WindowsRendererSandbox.VerifyWorker(); }
    var confinementMs = startupTimingEnabled && (confined || windowsConfined)
        ? Stopwatch.GetElapsedTime(confinementStart).TotalMilliseconds : 0;
    using var input = Console.OpenStandardInput();
    using var output = Console.OpenStandardOutput();
    var channel = new RendererChannel(input, output);
    var rendererConstructionStart = startupTimingEnabled ? Stopwatch.GetTimestamp() : 0L;
    using var renderer = new StaticPageRenderer(fontPath, RendererProtocol.MaxPixels,
        boldFontPath: bold, italicFontPath: italic, boldItalicFontPath: boldItalic);
    var rendererConstructionMs = startupTimingEnabled
        ? Stopwatch.GetElapsedTime(rendererConstructionStart).TotalMilliseconds : 0;
    StaticPageRenderer? scriptedRenderer = null;
    bool? scriptPolicy = null;
    var helloWriteStart = startupTimingEnabled ? Stopwatch.GetTimestamp() : 0L;
    channel.WriteAsync(new()
    {
        Kind = "hello",
        SandboxProfile = confined ? LinuxRendererSandbox.Profile
        : windowsConfined ? WindowsRendererSandbox.Profile : null
    }).GetAwaiter().GetResult();
    var helloWriteMs = startupTimingEnabled ? Stopwatch.GetElapsedTime(helloWriteStart).TotalMilliseconds : 0;
    if (startupTimingEnabled)
    {
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"smoke-worker-startup v=1 managed-ms={managedPreflightMs:F3} confinement-ms={confinementMs:F3} " +
            $"renderer-ms={rendererConstructionMs:F3} hello-ms={helloWriteMs:F3}"));
        Console.Error.WriteLine("smoke-worker-startup-end v=1");
    }
    long lastId = 0;
    try
    {
        while (channel.ReadAsync().GetAwaiter().GetResult() is { } packet)
        {
            var request = packet.Message;
            if (request.Kind is not ("render" or "click" or "focus") || request.Id <= lastId)
            {
                throw new IpcProtocolException("Expected a new monotonically identified render or click request.");
            }
            if (scriptPolicy is { } policy && policy != request.ExecuteInlineScripts)
            { throw new IpcProtocolException("Script opt-in policy cannot change within a tab channel."); }
            scriptPolicy = request.ExecuteInlineScripts;
            lastId = request.Id;
            if (request.Kind == "click")
            {
                // Click dispatch runs on this owning thread in the retained scripted document; only a flag returns.
                try
                {
                    if (scriptedRenderer is null || !scriptedRenderer.HasDocument(request.DocumentId))
                    { throw new PageNavigationException("Retained document was lost; reload explicitly rather than rerunning scripts."); }
                    scriptedRenderer.CommitDocument(request.DocumentId);
                    var eventType = request.LinkEventType switch
                    {
                        "click" => PageLinkEventType.Click,
                        "auxclick" => PageLinkEventType.AuxClick,
                        _ => throw new IpcProtocolException("Invalid renderer link event type.")
                    };
                    var allowed = request.FocusTargetId > 0
                        ? scriptedRenderer.DispatchLinkTargetEvent(request.DocumentId, request.FocusTargetId, eventType,
                            CancellationToken.None)
                        : scriptedRenderer.DispatchLinkEvent(request.DocumentId, request.LinkIndex, request.Url!,
                            eventType, CancellationToken.None);
                    channel.WriteAsync(new() { Kind = "click-result", Id = request.Id, DefaultPrevented = !allowed }).GetAwaiter().GetResult();
                }
                catch (Exception exception) when (StaticPageRenderer.IsRenderFailure(exception))
                {
                    channel.WriteAsync(new() { Kind = "error", Id = request.Id, Error = Bounded(exception.Message) }).GetAwaiter().GetResult();
                }
                continue;
            }
            if (request.Kind == "focus")
            {
                try
                {
                    if (scriptedRenderer is null || !scriptedRenderer.HasDocument(request.DocumentId))
                    { throw new PageNavigationException("Retained document was lost; reload explicitly rather than rerunning scripts."); }
                    scriptedRenderer.CommitDocument(request.DocumentId);
                    var eventType = request.FocusEventType switch
                    {
                        "focus" => PageFocusEventType.Focus,
                        "blur" => PageFocusEventType.Blur,
                        _ => throw new IpcProtocolException("Invalid renderer focus event type.")
                    };
                    scriptedRenderer.DispatchPageFocusEvent(request.DocumentId, request.FocusTargetId, eventType,
                        CancellationToken.None);
                    channel.WriteAsync(new() { Kind = "focus-result", Id = request.Id }).GetAwaiter().GetResult();
                }
                catch (Exception exception) when (StaticPageRenderer.IsRenderFailure(exception))
                {
                    channel.WriteAsync(new() { Kind = "error", Id = request.Id, Error = Bounded(exception.Message) }).GetAwaiter().GetResult();
                }
                continue;
            }
            try
            {
                var active = request.ExecuteInlineScripts
                    ? scriptedRenderer ??= new StaticPageRenderer(fontPath, RendererProtocol.MaxPixels, executeInlineScripts: true,
                        boldFontPath: bold, italicFontPath: italic, boldItalicFontPath: boldItalic)
                    : renderer;
                if (active.HasDocument(request.CommittedDocumentId)) { active.CommitDocument(request.CommittedDocumentId); }
                var page = new LoadedPage(BrowserUrl.Parse(request.Url!), request.Html!, request.StatusCode, request.Diagnostics!)
                {
                    DocumentId = request.DocumentId,
                    Stylesheets = request.Stylesheets!,
                    BaseUrl = request.BaseUrl is null ? null : BrowserUrl.Parse(request.BaseUrl),
                    AboutBaseUrl = request.AboutBaseUrl is null ? null : BrowserUrl.Parse(request.AboutBaseUrl)
                };
                var scriptRequestId = 0;
                string FetchScript(BrowserUrl url, CancellationToken token)
                {
                    token.ThrowIfCancellationRequested();
                    if (++scriptRequestId > RendererProtocol.MaxScriptRequests)
                    { throw new PageNavigationException("External script request count limit exceeded."); }
                    channel.WriteAsync(new()
                    {
                        Kind = "script-request",
                        Id = request.Id,
                        DocumentId = request.DocumentId,
                        ScriptRequestId = scriptRequestId,
                        ScriptUrl = url.Href
                    }, cancellationToken: token).GetAwaiter().GetResult();
                    var response = channel.ReadAsync(token).GetAwaiter().GetResult()
                        ?? throw new IOException("Browser closed the renderer channel during an external script request.");
                    if (response.Message.Kind != "script-response" || response.Message.Id != request.Id
                        || response.Message.DocumentId != request.DocumentId
                        || response.Message.ScriptRequestId != scriptRequestId
                        || response.Message.ScriptUrl != url.Href)
                    { throw new IpcProtocolException("Browser script response does not match the pending parser request."); }
                    if (response.Message.ScriptError is { } error) { throw new PageNavigationException(error); }
                    return response.Message.ScriptSource
                        ?? throw new IpcProtocolException("Browser script response omitted its source.");
                }
                PageScriptResourceLoader? scriptLoader = request.ExecuteInlineScripts
                    ? (url, token) => Task.FromResult(FetchScript(url, token))
                    : null;
                var rendered = active.Render(page,
                    new(request.Width, request.Height, request.Scale)
                    {
                        ScrollY = request.ScrollY,
                        ScrollX = request.ScrollX,
                        ScrollOffsets = request.ScrollOffsets ?? Array.Empty<PageScrollOffset>()
                    },
                    CancellationToken.None, request.ReuseDocument, scriptLoader);
                var frame = rendered.Frame;
                channel.WriteAsync(new()
                {
                    Kind = "frame",
                    Id = request.Id,
                    Width = request.Width,
                    Height = request.Height,
                    Scale = request.Scale,
                    PixelWidth = frame.Size.Width,
                    PixelHeight = frame.Size.Height,
                    Stride = frame.Stride,
                    Title = Bounded(rendered.Title),
                    Status = Bounded(rendered.Status),
                    ScrollHeight = rendered.ScrollHeight,
                    ScrollWidth = rendered.ScrollWidth,
                    DocumentBaseUrl = rendered.DocumentBaseUrl?.Href,
                    LinkTargets = rendered.LinkTargets.ToArray(),
                    FragmentTargets = rendered.FragmentTargets.ToArray(),
                    Forms = rendered.Forms.ToArray(),
                    FormControls = rendered.FormControls.ToArray(),
                    TextTargets = rendered.TextTargets.ToArray(),
                    ScrollContainers = rendered.ScrollContainers.ToArray()
                }, frame.Pixels).GetAwaiter().GetResult();
            }
            catch (Exception exception) when (StaticPageRenderer.IsRenderFailure(exception) || exception is UrlParseException)
            {
                channel.WriteAsync(new() { Kind = "error", Id = request.Id, Error = Bounded(exception.Message) }).GetAwaiter().GetResult();
            }
        }
    }
    finally { scriptedRenderer?.Dispose(); }
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
    or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException
    or System.ComponentModel.Win32Exception or PlatformNotSupportedException
    || StaticPageRenderer.IsRenderFailure(exception))
{
    Console.Error.WriteLine("Renderer terminated: " + exception.Message);
    return 1;
}

static string Bounded(string text) => text.Length <= RendererProtocol.MaxTextCharacters
    ? text : text[..(RendererProtocol.MaxTextCharacters - 3)] + "...";
