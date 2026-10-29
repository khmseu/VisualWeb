using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;
using VisualWeb.PageRendering;
using VisualWeb.Platform.Linux.Sandbox;
using VisualWeb.Platform.Windows.Sandbox;

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
    for (var index = 3; index < args.Length; index += 2)
    {
        var option = args[index];
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
    }
    if (mode == "--linux-sandbox-bootstrap")
    {
        LinuxRendererSandbox.Enter(AppContext.BaseDirectory, fontPath,
            boldFontPath: bold, italicFontPath: italic, boldItalicFontPath: boldItalic);
        throw new InvalidOperationException("Sandbox bootstrap returned without confinement.");
    }
    var confined = mode == "--linux-sandbox-worker";
    var windowsConfined = mode == "--windows-sandbox-worker";
    if (confined) { LinuxRendererSandbox.VerifyWorker(); }
    if (windowsConfined) { WindowsRendererSandbox.VerifyWorker(); }
    using var input = Console.OpenStandardInput();
    using var output = Console.OpenStandardOutput();
    var channel = new RendererChannel(input, output);
    using var renderer = new StaticPageRenderer(fontPath, RendererProtocol.MaxPixels,
        boldFontPath: bold, italicFontPath: italic, boldItalicFontPath: boldItalic);
    StaticPageRenderer? scriptedRenderer = null;
    bool? scriptPolicy = null;
    channel.WriteAsync(new()
    {
        Kind = "hello",
        SandboxProfile = confined ? LinuxRendererSandbox.Profile
        : windowsConfined ? WindowsRendererSandbox.Profile : null
    }).GetAwaiter().GetResult();
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
                    BaseUrl = request.BaseUrl is null ? null : BrowserUrl.Parse(request.BaseUrl)
                };
                var rendered = active.Render(page,
                    new(request.Width, request.Height, request.Scale)
                    {
                        ScrollY = request.ScrollY,
                        ScrollX = request.ScrollX,
                        ScrollOffsets = request.ScrollOffsets ?? Array.Empty<PageScrollOffset>()
                    },
                    CancellationToken.None, request.ReuseDocument);
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
