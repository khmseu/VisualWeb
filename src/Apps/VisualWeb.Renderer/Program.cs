using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;
using VisualWeb.PageRendering;
using VisualWeb.Platform.Linux.Sandbox;

if (args is not [var mode, "--font", var fontPath]
    || mode is not ("--development-unsandboxed" or "--linux-sandbox-bootstrap" or "--linux-sandbox-worker"))
{
    Console.Error.WriteLine("Renderer is an internal worker. Usage: (--development-unsandboxed | --linux-sandbox-bootstrap) --font TRUSTED_FONT");
    return 2;
}
try
{
    if (mode == "--linux-sandbox-bootstrap")
    {
        LinuxRendererSandbox.Enter(AppContext.BaseDirectory, fontPath);
        throw new InvalidOperationException("Sandbox bootstrap returned without confinement.");
    }
    var confined = mode == "--linux-sandbox-worker";
    if (confined) { LinuxRendererSandbox.VerifyWorker(); }
    using var input = Console.OpenStandardInput();
    using var output = Console.OpenStandardOutput();
    var channel = new RendererChannel(input, output);
    using var renderer = new StaticPageRenderer(fontPath, RendererProtocol.MaxPixels);
    channel.WriteAsync(new() { Kind = "hello", SandboxProfile = confined ? LinuxRendererSandbox.Profile : null }).GetAwaiter().GetResult();
    long lastId = 0;
    while (channel.ReadAsync().GetAwaiter().GetResult() is { } packet)
    {
        var request = packet.Message;
        if (request.Kind != "render" || request.Id <= lastId)
        {
            throw new IpcProtocolException("Expected a new monotonically identified render request.");
        }
        lastId = request.Id;
        try
        {
            var page = new LoadedPage(BrowserUrl.Parse(request.Url!), request.Html!, request.StatusCode, request.Diagnostics!);
            var rendered = renderer.Render(page, new(request.Width, request.Height, request.Scale), CancellationToken.None);
            var frame = rendered.Frame;
            channel.WriteAsync(new()
            {
                Kind = "frame",
                Id = request.Id,
                PixelWidth = frame.Size.Width,
                PixelHeight = frame.Size.Height,
                Stride = frame.Stride,
                Title = Bounded(rendered.Title),
                Status = Bounded(rendered.Status)
            }, frame.Pixels).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (StaticPageRenderer.IsRenderFailure(exception) || exception is UrlParseException)
        {
            channel.WriteAsync(new() { Kind = "error", Id = request.Id, Error = Bounded(exception.Message) }).GetAwaiter().GetResult();
        }
    }
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
