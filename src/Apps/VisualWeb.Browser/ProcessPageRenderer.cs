using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VisualWeb.Engine.Paint;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;
using VisualWeb.Platform.Linux.Sandbox;
using VisualWeb.Platform.Windows.Sandbox;

namespace VisualWeb.Browser;

public sealed class RendererProcessException(string message) : IOException(message);

/// <summary>One tab's renderer process with explicit unsandboxed or required Linux confinement.</summary>
/// <remarks>References: dotnet-process/dotnet-process-start/dotnet-process-pipes;
/// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput">redirected pipes</see>.
/// Required confinement never falls back to unsandboxed launch.
/// Canceled/failed exchanges kill only this owned worker; the next request launches a replacement.</remarks>
public sealed class ProcessPageRenderer : IPageRenderer
{
    private sealed class Connection(Process process, LinuxRendererResources? resources, WindowsRendererWorker? windowsWorker) : IDisposable
    {
        internal LinuxRendererResources? Resources { get; } = resources;
        internal Process Process { get; } = process;
        internal RendererChannel Channel { get; } = new(windowsWorker?.Output ?? process.StandardOutput.BaseStream,
            windowsWorker?.Input ?? process.StandardInput.BaseStream);
        internal Task? Diagnostics { get; set; }
        private string tail = "";
        private readonly object sync = new();
        internal void Append(ReadOnlySpan<char> text)
        {
            lock (sync) { tail = (tail + text.ToString()); if (tail.Length > 8192) { tail = tail[^8192..]; } }
        }
        internal string Tail { get { lock (sync) { return tail; } } }
        internal void Abort() => windowsWorker?.Terminate();
        public void Dispose()
        {
            try { Resources?.Stop(); }
            finally
            {
                if (windowsWorker is not null)
                {
                    windowsWorker.Dispose();
                }
                else
                {
                    if (!Process.HasExited)
                    {
                        try { Process.Kill(entireProcessTree: true); }
                        catch (InvalidOperationException) when (Process.HasExited) { }
                        catch (Win32Exception) when (Process.HasExited) { }
                    }
                    Process.Dispose();
                }
            }
        }
    }
    private readonly string rendererPath;
    private readonly string fontPath;
    private readonly string dotnetPath;
    private readonly TimeSpan timeout;
    private readonly SemaphoreSlim exchange = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object sync = new();
    private Connection? connection;
    private bool disposed;
    private long nextId;
    private readonly bool requireSandbox;
    private readonly bool executeInlineScripts;
    private Guid committedDocumentId;
    private readonly string? requiredSandboxProfile;
    public int? ProcessId { get { lock (sync) { return connection?.Process.Id; } } }
    public string? ResourceUnit { get { lock (sync) { return connection?.Resources?.Unit; } } }

    public ProcessPageRenderer(string rendererPath, string fontPath, TimeSpan? timeout = null,
        bool requireSandbox = false, string? dotnetPath = null, bool executeInlineScripts = false)
    {
        this.executeInlineScripts = executeInlineScripts;
        if (requireSandbox)
        {
            if (OperatingSystem.IsLinux())
            {
                LinuxRendererResources.RequireSupport();
                requiredSandboxProfile = LinuxRendererSandbox.Profile;
            }
            else if (OperatingSystem.IsWindows())
            {
                WindowsRendererSandbox.RequireSupport();
                requiredSandboxProfile = WindowsRendererSandbox.Profile;
            }
            else { throw new PlatformNotSupportedException("Renderer confinement is unavailable on this platform."); }
        }
        this.requireSandbox = requireSandbox;
        ArgumentException.ThrowIfNullOrWhiteSpace(rendererPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath);
        this.rendererPath = Path.GetFullPath(rendererPath);
        this.fontPath = Path.GetFullPath(fontPath);
        this.dotnetPath = dotnetPath ?? (this.rendererPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? RuntimeHost() : "");
        if (dotnetPath is not null) { ArgumentException.ThrowIfNullOrWhiteSpace(dotnetPath); }
        this.timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (this.timeout <= TimeSpan.Zero || this.timeout.TotalMilliseconds > uint.MaxValue - 1) { throw new ArgumentOutOfRangeException(nameof(timeout)); }
        if (!File.Exists(this.rendererPath)) { throw new FileNotFoundException("Build the renderer project and provide its executable or DLL.", this.rendererPath); }
    }
    public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken) =>
        RenderAsync(page, viewport, cancellationToken, reuseDocument: false);
    public Task<BrowserPage> RenderRetainedAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken) =>
        RenderAsync(page, viewport, cancellationToken, reuseDocument: true);
    public void CommitDocument(Guid documentId)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            committedDocumentId = documentId;
        }
    }
    private async Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken,
        bool reuseDocument)
    {
        ArgumentNullException.ThrowIfNull(page);
        lock (sync) { ObjectDisposedException.ThrowIf(disposed, this); }
        var request = new RendererMessage
        {
            Kind = "render",
            DocumentId = page.DocumentId,
            ExecuteInlineScripts = executeInlineScripts,
            ReuseDocument = reuseDocument,
            Id = 1,
            Url = page.Url.Href,
            Html = page.Html,
            StatusCode = page.StatusCode,
            Diagnostics = page.Diagnostics.ToArray(),
            Stylesheets = page.Stylesheets.ToArray(),
            Width = viewport.Width,
            Height = viewport.Height,
            Scale = viewport.Scale,
            ScrollY = viewport.ScrollY
        };
        RendererProtocol.Validate(request, 0);
        var expected = RendererProtocol.Dimensions(viewport.Width, viewport.Height, viewport.Scale);
        using var deadline = reuseDocument
            ? CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        deadline.CancelAfter(timeout);
        using var queued = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try { await exchange.WaitAsync(queued.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
        {
            throw new RendererProcessException("Renderer exchange queue deadline exceeded.");
        }
        request = request with { Id = checked(++nextId) };
        Connection? current = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool started;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                started = connection is null;
                current = connection ??= Start();
            }
            using var cancellationAbort = requireSandbox && OperatingSystem.IsWindows()
                ? deadline.Token.Register(() => current?.Abort())
                : default;
            lock (sync) { request = request with { CommittedDocumentId = committedDocumentId }; }
            if (started)
            {
                var hello = await Receive(current, deadline.Token).ConfigureAwait(false);
                if (hello.Message.Kind != "hello") { throw new IpcProtocolException("Renderer handshake is missing."); }
                if (requireSandbox && hello.Message.SandboxProfile != requiredSandboxProfile)
                {
                    throw new IpcProtocolException("Renderer did not confirm the required platform confinement profile.");
                }
            }
            await current.Channel.WriteAsync(request, cancellationToken: deadline.Token).ConfigureAwait(false);
            var packet = await Receive(current, deadline.Token).ConfigureAwait(false);
            if (packet.Message.Id != request.Id) { throw new IpcProtocolException("Renderer reply identity does not match its request."); }
            if (packet.Message.Kind == "error") { throw new PageNavigationException(packet.Message.Error!); }
            if (packet.Message.Kind != "frame" || packet.Message.PixelWidth != expected.Width || packet.Message.PixelHeight != expected.Height
                || packet.Message.Width != viewport.Width || packet.Message.Height != viewport.Height || packet.Message.Scale != viewport.Scale)
            {
                throw new IpcProtocolException("Renderer frame does not match the requested physical viewport.");
            }
            if (packet.Message.ScrollHeight < viewport.Height)
            { throw new IpcProtocolException("Renderer scroll height is smaller than its CSS viewport."); }
            var frame = RasterFrame.CopyFrom(packet.Pixels,
                new(packet.Message.PixelWidth, packet.Message.PixelHeight), packet.Message.Stride);
            // Drain superseded exchanges under the deadline so retained DOM survives and replies stay aligned.
            cancellationToken.ThrowIfCancellationRequested();
            return new(frame, packet.Message.Title!, packet.Message.Status!)
            {
                ScrollHeight = packet.Message.ScrollHeight,
                LinkTargets = Array.AsReadOnly(packet.Message.LinkTargets!),
                Forms = Array.AsReadOnly(packet.Message.Forms!),
                FormControls = Array.AsReadOnly(packet.Message.FormControls!),
                TextTargets = Array.AsReadOnly(packet.Message.TextTargets!)
            };
        }
        catch (OperationCanceledException)
        {
            if (deadline.IsCancellationRequested) { Stop(current); }
            if (cancellationToken.IsCancellationRequested || lifetime.IsCancellationRequested) { throw; }
            throw new RendererProcessException("Renderer startup/render deadline exceeded; worker terminated.");
        }
        catch (Exception) when (deadline.IsCancellationRequested)
        {
            Stop(current);
            if (cancellationToken.IsCancellationRequested || lifetime.IsCancellationRequested)
            {
                throw new OperationCanceledException(deadline.Token);
            }
            throw new RendererProcessException("Renderer startup/render deadline exceeded; worker terminated.");
        }
        catch (Exception exception) when (exception is IOException or Win32Exception or InvalidOperationException or ObjectDisposedException)
        {
            var diagnostics = current?.Tail ?? "";
            Stop(current);
            throw new RendererProcessException("Renderer communication failed: " + exception.Message
                + (diagnostics.Length == 0 ? "" : " " + diagnostics));
        }
        finally { exchange.Release(); }
    }
    private static async Task<RendererPacket> Receive(Connection owner, CancellationToken cancellationToken)
    {
        var packet = await owner.Channel.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (packet is not null) { return packet; }
        // EOF can precede the worker's final stderr diagnostic; retain it within the same deadline.
        await owner.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (owner.Diagnostics is { } diagnostics) { await diagnostics.WaitAsync(cancellationToken).ConfigureAwait(false); }
        throw new RendererProcessException($"Renderer exited ({owner.Process.ExitCode}) before completing its reply.");
    }
    private Connection Start()
    {
        if (requireSandbox && OperatingSystem.IsWindows())
        {
            if (!rendererPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException("Windows confinement requires the framework-dependent renderer DLL.");
            }
            var worker = WindowsRendererSandbox.Start(Path.GetDirectoryName(rendererPath)!, fontPath,
                RuntimeEnvironment.GetRuntimeDirectory(), Path.GetFileName(rendererPath));
            var connection = new Connection(worker.Process, null, worker);
            connection.Diagnostics = DrainDiagnostics(connection, worker.Error);
            return connection;
        }
        var dll = rendererPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var info = new ProcessStartInfo(dll ? dotnetPath : rendererPath)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (dll) { info.ArgumentList.Add(rendererPath); }
        if (requireSandbox)
        {
            info.Environment.Clear();
            info.Environment["DOTNET_EnableDiagnostics"] = "0";
            info.Environment["DOTNET_ROOT"] = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent?.Parent?.Parent?.FullName;
        }
        info.ArgumentList.Add(requireSandbox ? "--linux-sandbox-bootstrap" : "--development-unsandboxed");
        info.ArgumentList.Add("--font"); info.ArgumentList.Add(fontPath);
        var resources = requireSandbox ? new LinuxRendererResources() : null;
        var process = Process.Start(resources?.Wrap(info) ?? info) ?? throw new RendererProcessException("Renderer process did not start.");
        var owner = new Connection(process, resources, null);
        owner.Diagnostics = DrainDiagnostics(owner, process.StandardError.BaseStream);
        return owner;
    }
    private static string RuntimeHost()
    {
        var root = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent?.Parent?.Parent;
        var host = root is null ? null : Path.Combine(root.FullName, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (host is null || !File.Exists(host))
        {
            throw new RendererProcessException("Cannot locate the host for the active .NET runtime; configure an explicit dotnetPath.");
        }
        return host;
    }
    private static async Task DrainDiagnostics(Connection owner, Stream error)
    {
        using var reader = new StreamReader(error);
        var buffer = new char[1024];
        try
        {
            while (await reader.ReadAsync(buffer).ConfigureAwait(false) is > 0 and var count) { owner.Append(buffer.AsSpan(0, count)); }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            owner.Append((" [stderr stream closed: " + exception.Message + "]").AsSpan());
        }
    }
    public string? TakeFailure()
    {
        lock (sync)
        {
            if (disposed || connection is null || !connection.Process.HasExited || exchange.CurrentCount == 0) { return null; }
            var failure = $"Renderer process {connection.Process.Id} exited ({connection.Process.ExitCode}). {connection.Tail}";
            var exited = connection;
            connection = null;
            exited.Dispose();
            return failure;
        }
    }
    private void Stop(Connection? owner)
    {
        if (owner is null) { return; }
        lock (sync)
        {
            if (!ReferenceEquals(connection, owner)) { return; }
            connection = null;
            owner.Dispose();
        }
    }
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) { return; }
            disposed = true;
            lifetime.Cancel();
            var owned = connection;
            connection = null;
            owned?.Dispose();
        }
    }
}
