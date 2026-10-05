using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VisualWeb.Engine.Paint;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;
using VisualWeb.Platform.Linux.Sandbox;

namespace VisualWeb.Browser;

public sealed class RendererProcessException(string message) : IOException(message);

/// <summary>One tab's renderer process with explicit unsandboxed or required Linux confinement.</summary>
/// <remarks>References: dotnet-process/dotnet-process-start/dotnet-process-pipes;
/// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput">redirected pipes</see>.
/// Required confinement never falls back to unsandboxed launch.
/// Canceled/failed exchanges kill only this owned worker; the next request launches a replacement.</remarks>
public sealed class ProcessPageRenderer : IPageRenderer
{
    private sealed class Connection(Process process, LinuxRendererResources? resources) : IDisposable
    {
        internal LinuxRendererResources? Resources { get; } = resources;
        internal Process Process { get; } = process;
        internal RendererChannel Channel { get; } = new(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
        internal Task? Diagnostics { get; set; }
        private string tail = "";
        private readonly object sync = new();
        internal void Append(ReadOnlySpan<char> text)
        {
            lock (sync) { tail = (tail + text.ToString()); if (tail.Length > 8192) { tail = tail[^8192..]; } }
        }
        internal string Tail { get { lock (sync) { return tail; } } }
        public void Dispose()
        {
            try { Resources?.Stop(); }
            finally
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
    public int? ProcessId { get { lock (sync) { return connection?.Process.Id; } } }
    public string? ResourceUnit { get { lock (sync) { return connection?.Resources?.Unit; } } }

    public ProcessPageRenderer(string rendererPath, string fontPath, TimeSpan? timeout = null,
        bool requireSandbox = false, string? dotnetPath = null)
    {
        if (requireSandbox) { LinuxRendererResources.RequireSupport(); }
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
    public async Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        lock (sync) { ObjectDisposedException.ThrowIf(disposed, this); }
        var request = new RendererMessage
        {
            Kind = "render",
            Id = 1,
            Url = page.Url.Href,
            Html = page.Html,
            StatusCode = page.StatusCode,
            Diagnostics = page.Diagnostics.ToArray(),
            Width = viewport.Width,
            Height = viewport.Height,
            Scale = viewport.Scale
        };
        RendererProtocol.Validate(request, 0);
        var expected = RendererProtocol.Dimensions(viewport.Width, viewport.Height, viewport.Scale);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        deadline.CancelAfter(timeout);
        try { await exchange.WaitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
        {
            throw new RendererProcessException("Renderer exchange queue deadline exceeded.");
        }
        request = request with { Id = checked(++nextId) };
        Connection? current = null;
        try
        {
            bool started;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                started = connection is null;
                current = connection ??= Start();
            }
            if (started)
            {
                var hello = await Receive(current, deadline.Token).ConfigureAwait(false);
                if (hello.Message.Kind != "hello") { throw new IpcProtocolException("Renderer handshake is missing."); }
                if (requireSandbox && hello.Message.SandboxProfile != LinuxRendererSandbox.Profile)
                {
                    throw new IpcProtocolException("Renderer did not confirm the required Linux confinement profile.");
                }
            }
            await current.Channel.WriteAsync(request, cancellationToken: deadline.Token).ConfigureAwait(false);
            var packet = await Receive(current, deadline.Token).ConfigureAwait(false);
            if (packet.Message.Id != request.Id) { throw new IpcProtocolException("Renderer reply identity does not match its request."); }
            if (packet.Message.Kind == "error") { throw new PageNavigationException(packet.Message.Error!); }
            if (packet.Message.Kind != "frame" || packet.Message.PixelWidth != expected.Width || packet.Message.PixelHeight != expected.Height)
            {
                throw new IpcProtocolException("Renderer frame does not match the requested physical viewport.");
            }
            var frame = RasterFrame.CopyFrom(packet.Pixels,
                new(packet.Message.PixelWidth, packet.Message.PixelHeight), packet.Message.Stride);
            return new(frame, packet.Message.Title!, packet.Message.Status!);
        }
        catch (OperationCanceledException)
        {
            Stop(current);
            if (cancellationToken.IsCancellationRequested || lifetime.IsCancellationRequested) { throw; }
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
        var owner = new Connection(process, resources);
        owner.Diagnostics = DrainDiagnostics(owner);
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
    private static async Task DrainDiagnostics(Connection owner)
    {
        var reader = owner.Process.StandardError;
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
            connection.Dispose();
            connection = null;
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
            connection?.Dispose();
            connection = null;
        }
    }
}
