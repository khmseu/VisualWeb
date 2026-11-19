using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Scripting;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;
using VisualWeb.PageRendering;
using VisualWeb.Platform.Abstractions;
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
    private sealed class Connection(Process process, LinuxRendererResources? resources, WindowsRendererWorker? windowsWorker,
        SmokeTimings? timings, long rendererId, bool collectWorkerStartup) : IDisposable
    {
        internal long RendererId { get; } = rendererId;
        internal LinuxRendererResources? Resources { get; } = resources;
        internal Process Process { get; } = process;
        internal RendererChannel Channel { get; } = new(windowsWorker?.Output ?? process.StandardOutput.BaseStream,
            windowsWorker?.Input ?? process.StandardInput.BaseStream);
        internal Task? Diagnostics { get; set; }
        private readonly CancellationTokenSource diagnosticsCancellation = new();
        private readonly StartupDiagnosticParser? startup = collectWorkerStartup
            ? new(timings!, rendererId) : null;
        private string tail = "";
        private readonly object sync = new();

        internal void Append(ReadOnlySpan<char> text)
        {
            lock (sync)
            {
                tail = (tail + text.ToString());
                if (tail.Length > 8192) { tail = tail[^8192..]; }
            }
            startup?.Append(text);
        }

        internal string Tail { get { lock (sync) { return tail; } } }
        internal Task? StartupDiagnostics => startup?.Completion;
        internal CancellationToken DiagnosticsCancellation => diagnosticsCancellation.Token;
        internal void RequireStartupDiagnostics() => startup?.Require();
        internal void ReportMissingStartupDiagnostics() => startup?.ReportMissing();
        internal void CompleteDiagnostics() => startup?.Complete();
        internal void Abort() => windowsWorker?.Terminate();

        public void Dispose()
        {
            startup?.Cancel();
            // Cancellation is best effort (notably for synchronous Windows pipes). Native cleanup
            // remains synchronous; only the subsequent optional diagnostics join has its own bound.
            diagnosticsCancellation.Cancel();
            using var timing = timings?.Measure("renderer.dispose", RendererId);
            try { Resources?.Stop(); }
            finally
            {
                try
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
                        try { Process.StandardError.Close(); }
                        finally { Process.Dispose(); }
                    }
                }
                finally
                {
                    try
                    {
                        if (Diagnostics is { } diagnostics) { timings?.JoinDiagnosticDrain(diagnostics, RendererId); }
                    }
                    finally { diagnosticsCancellation.Dispose(); }
                }
            }
        }
    }

    internal sealed class StartupDiagnosticParser(SmokeTimings timings, long renderer)
    {
        private const int MaxLineLength = 256;
        private readonly object sync = new();
        private readonly char[] line = new char[MaxLineLength];
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int lineLength;
        private bool overlong;
        private bool required;
        private bool batchSeen;
        private bool batchEnded;
        private bool errorRecorded;
        private bool canceled;
        private bool eof;
        private double managedMs;
        private double confinementMs;
        private double rendererMs;
        private double helloMs;
        internal Task Completion => completion.Task;

        internal void Require()
        {
            lock (sync)
            {
                required = true;
                if (eof && !batchEnded && !errorRecorded && !canceled)
                { RecordError(HostTimingDiagnosticErrorCode.WorkerBatchMissing); }
                if (batchEnded || errorRecorded || eof || canceled) { completion.TrySetResult(); }
            }
        }

        internal void Append(ReadOnlySpan<char> text)
        {
            lock (sync)
            {
                if (canceled) { return; }
                var complete = false;
                foreach (var character in text)
                {
                    if (character == '\n')
                    {
                        complete |= ProcessLine();
                        lineLength = 0;
                        overlong = false;
                    }
                    else if (lineLength < line.Length) { line[lineLength++] = character; }
                    else { overlong = true; }
                }
                if (complete) { completion.TrySetResult(); }
            }
        }

        internal void Complete()
        {
            lock (sync)
            {
                if (!canceled && lineLength > 0) { _ = ProcessLine(partial: true); }
                if (required && !batchEnded && !errorRecorded && !canceled)
                { RecordError(HostTimingDiagnosticErrorCode.WorkerBatchMissing); }
                eof = true;
                completion.TrySetResult();
            }
        }

        internal void ReportMissing()
        {
            lock (sync)
            {
                if (required && !batchEnded && !errorRecorded && !canceled)
                {
                    if (lineLength > 0) { _ = ProcessLine(partial: true); }
                    if (!errorRecorded) { RecordError(HostTimingDiagnosticErrorCode.WorkerBatchMissing); }
                }
                completion.TrySetResult();
            }
        }

        internal void Cancel()
        {
            lock (sync)
            {
                canceled = true;
                completion.TrySetResult();
            }
        }

        private bool ProcessLine(bool partial = false)
        {
            var value = line.AsSpan(0, lineLength);
            if (!partial && value.Length > 0 && value[^1] == '\r') { value = value[..^1]; }
            if (!value.StartsWith("smoke-worker", StringComparison.Ordinal)) { return false; }
            if (overlong)
            {
                RecordError(HostTimingDiagnosticErrorCode.WorkerBatchOverlong);
                return true;
            }
            if (errorRecorded) { return true; }
            if (partial)
            {
                RecordError(HostTimingDiagnosticErrorCode.WorkerBatchPartial);
                return true;
            }
            if (value.StartsWith("smoke-worker-startup ", StringComparison.Ordinal))
            {
                if (batchSeen || batchEnded)
                {
                    RecordError(HostTimingDiagnosticErrorCode.WorkerBatchDuplicate);
                    return true;
                }
                if (!TryParseBatch(value))
                {
                    RecordError(HostTimingDiagnosticErrorCode.WorkerBatchMalformed);
                    return true;
                }
                batchSeen = true;
                return false;
            }
            if (value.SequenceEqual("smoke-worker-startup-end v=1"))
            {
                if (!batchSeen || batchEnded)
                {
                    RecordError(batchEnded ? HostTimingDiagnosticErrorCode.WorkerBatchDuplicate
                        : HostTimingDiagnosticErrorCode.WorkerBatchMalformed);
                    return true;
                }
                batchEnded = true;
                timings.Record("worker.managed-preflight", renderer, 0, TimeSpan.FromMilliseconds(managedMs));
                timings.Record("worker.confinement-verification", renderer, 0, TimeSpan.FromMilliseconds(confinementMs));
                timings.Record("worker.renderer-construction", renderer, 0, TimeSpan.FromMilliseconds(rendererMs));
                timings.Record("worker.hello-write", renderer, 0, TimeSpan.FromMilliseconds(helloMs));
                return true;
            }
            RecordError(batchEnded ? HostTimingDiagnosticErrorCode.WorkerBatchDuplicate
                : HostTimingDiagnosticErrorCode.WorkerBatchMalformed);
            return true;
        }

        private bool TryParseBatch(ReadOnlySpan<char> value)
        {
            var fields = value.ToString().Split(' ', StringSplitOptions.None);
            return fields.Length == 6
                && fields[0] == "smoke-worker-startup"
                && fields[1] == "v=1"
                && TryDuration(fields[2], "managed-ms=", out managedMs)
                && TryDuration(fields[3], "confinement-ms=", out confinementMs)
                && TryDuration(fields[4], "renderer-ms=", out rendererMs)
                && TryDuration(fields[5], "hello-ms=", out helloMs);
        }

        private static bool TryDuration(string field, string prefix, out double milliseconds)
        {
            milliseconds = 0;
            if (!field.StartsWith(prefix, StringComparison.Ordinal)) { return false; }
            var value = field.AsSpan(prefix.Length);
            var dot = false;
            foreach (var character in value)
            {
                if (character == '.' && !dot) { dot = true; }
                else if (character is < '0' or > '9') { return false; }
            }
            return value.Length > 0 && double.TryParse(value, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out milliseconds)
                && double.IsFinite(milliseconds) && milliseconds is >= 0 and <= 86_400_000;
        }

        private void RecordError(HostTimingDiagnosticErrorCode code)
        {
            if (errorRecorded) { return; }
            errorRecorded = true;
            timings.Buffer.RecordDiagnosticError(new(renderer, code));
        }
    }
    private readonly string rendererPath;
    private readonly string fontPath;
    private readonly string? boldFontPath;
    private readonly string? italicFontPath;
    private readonly string? boldItalicFontPath;
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
    private readonly SmokeTimings? timings;
    private Guid committedDocumentId;
    private readonly string? requiredSandboxProfile;
    public int? ProcessId { get { lock (sync) { return connection?.Process.Id; } } }
    public string? ResourceUnit { get { lock (sync) { return connection?.Resources?.Unit; } } }

    public ProcessPageRenderer(string rendererPath, string fontPath, TimeSpan? timeout = null,
        bool requireSandbox = false, string? dotnetPath = null, bool executeInlineScripts = false,
        string? boldFontPath = null, string? italicFontPath = null, string? boldItalicFontPath = null,
        SmokeTimings? timings = null)
    {
        this.timings = timings;
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
        this.boldFontPath = boldFontPath is null ? null : Path.GetFullPath(boldFontPath);
        this.italicFontPath = italicFontPath is null ? null : Path.GetFullPath(italicFontPath);
        this.boldItalicFontPath = boldItalicFontPath is null ? null : Path.GetFullPath(boldItalicFontPath);
        this.dotnetPath = dotnetPath ?? (this.rendererPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? RuntimeHost() : "");
        if (dotnetPath is not null) { ArgumentException.ThrowIfNullOrWhiteSpace(dotnetPath); }
        this.timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (this.timeout <= TimeSpan.Zero || this.timeout.TotalMilliseconds > uint.MaxValue - 1) { throw new ArgumentOutOfRangeException(nameof(timeout)); }
        if (!File.Exists(this.rendererPath)) { throw new FileNotFoundException("Build the renderer project and provide its executable or DLL.", this.rendererPath); }
    }
    public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken) =>
        RenderAsync(page, viewport, cancellationToken, reuseDocument: false);
    public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport,
        PageScriptResourceLoader? scriptLoader, CancellationToken cancellationToken) =>
        RenderAsync(page, viewport, cancellationToken, reuseDocument: false, scriptLoader);
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
        bool reuseDocument, PageScriptResourceLoader? scriptLoader = null)
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
            BaseUrl = page.BaseUrl?.Href,
            AboutBaseUrl = page.AboutBaseUrl?.Href,
            Html = page.Html,
            StatusCode = page.StatusCode,
            Diagnostics = page.Diagnostics.ToArray(),
            Stylesheets = page.Stylesheets.ToArray(),
            Width = viewport.Width,
            Height = viewport.Height,
            Scale = viewport.Scale,
            ScrollY = viewport.ScrollY,
            ScrollX = viewport.ScrollX,
            ScrollOffsets = viewport.ScrollOffsets.ToArray()
        };
        RendererProtocol.Validate(request, 0);
        var expected = RendererProtocol.Dimensions(viewport.Width, viewport.Height, viewport.Scale);
        return await ExchangeAsync(request, drain: reuseDocument, cancellationToken, packet =>
        {
            if (packet.Message.Kind != "frame" || packet.Message.PixelWidth != expected.Width || packet.Message.PixelHeight != expected.Height
                || packet.Message.Width != viewport.Width || packet.Message.Height != viewport.Height || packet.Message.Scale != viewport.Scale)
            {
                throw new IpcProtocolException("Renderer frame does not match the requested physical viewport.");
            }
            if (packet.Message.ScrollHeight < viewport.Height)
            { throw new IpcProtocolException("Renderer scroll height is smaller than its CSS viewport."); }
            if (packet.Message.ScrollWidth < viewport.Width)
            { throw new IpcProtocolException("Renderer scroll width is smaller than its CSS viewport."); }
            var frame = RasterFrame.CopyFrom(packet.Pixels,
                new(packet.Message.PixelWidth, packet.Message.PixelHeight), packet.Message.Stride);
            return new BrowserPage(frame, packet.Message.Title!, packet.Message.Status!)
            {
                DocumentBaseUrl = BrowserUrl.Parse(packet.Message.DocumentBaseUrl!),
                ScrollHeight = packet.Message.ScrollHeight,
                ScrollWidth = packet.Message.ScrollWidth,
                LinkTargets = Array.AsReadOnly(packet.Message.LinkTargets!),
                FragmentTargets = Array.AsReadOnly(packet.Message.FragmentTargets!),
                Forms = Array.AsReadOnly(packet.Message.Forms!),
                FormControls = Array.AsReadOnly(packet.Message.FormControls!),
                TextTargets = Array.AsReadOnly(packet.Message.TextTargets!),
                ScrollContainers = Array.AsReadOnly(packet.Message.ScrollContainers ?? Array.Empty<PageScrollContainer>())
            };
        }, scriptLoader, page.DocumentId).ConfigureAwait(false);
    }
    public bool DispatchesLinkClicks => executeInlineScripts;
    public bool DispatchesPageFocusEvents => executeInlineScripts;
    /// <summary>Send a data-only click request for the committed document; the worker dispatches on its owning thread.</summary>
    public Task<bool> DispatchLinkClickAsync(Guid documentId, int linkIndex, string url, CancellationToken cancellationToken) =>
        DispatchLinkEventAsync(documentId, linkIndex, url, PageLinkEventType.Click, cancellationToken);
    /// <summary>Send a data-only typed link event request; the worker dispatches on its owning thread.</summary>
    /// <remarks>Like retained repaints, the request drains under the exchange deadline so worker DOM/listener state
    /// stays aligned; a deadline or communication failure terminates the worker and with it the page host.</remarks>
    public async Task<bool> DispatchLinkEventAsync(Guid documentId, int linkIndex, string url, PageLinkEventType eventType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        var linkEventType = eventType switch
        {
            PageLinkEventType.Click => "click",
            PageLinkEventType.AuxClick => "auxclick",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!executeInlineScripts) { throw new PageNavigationException("Page link events require the inline-script opt-in."); }
            if (documentId == Guid.Empty || documentId != committedDocumentId)
            { throw new PageNavigationException("Clicked document is not the committed retained document; reload explicitly."); }
        }
        var request = new RendererMessage
        {
            Kind = "click",
            Id = 1,
            DocumentId = documentId,
            ExecuteInlineScripts = true,
            LinkIndex = linkIndex,
            LinkEventType = linkEventType,
            Url = url
        };
        try { RendererProtocol.Validate(request, 0); }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        return await ExchangeAsync(request, drain: true, cancellationToken, packet => packet.Message.Kind == "click-result"
            ? packet.Message.DefaultPrevented is false
            : throw new IpcProtocolException("Renderer click reply has the wrong kind.")).ConfigureAwait(false);
    }
    public async Task<bool> DispatchLinkTargetEventAsync(Guid documentId, int focusTargetId,
        PageLinkEventType eventType, CancellationToken cancellationToken)
    {
        var linkEventType = eventType switch
        {
            PageLinkEventType.Click => "click",
            PageLinkEventType.AuxClick => "auxclick",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!executeInlineScripts) { throw new PageNavigationException("Page link events require the inline-script opt-in."); }
            if (documentId == Guid.Empty || documentId != committedDocumentId)
            { throw new PageNavigationException("Clicked document is not the committed retained document; reload explicitly."); }
        }
        var request = new RendererMessage
        {
            Kind = "click",
            Id = 1,
            DocumentId = documentId,
            ExecuteInlineScripts = true,
            FocusTargetId = focusTargetId,
            LinkEventType = linkEventType
        };
        try { RendererProtocol.Validate(request, 0); }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        return await ExchangeAsync(request, drain: true, cancellationToken, packet => packet.Message.Kind == "click-result"
            ? packet.Message.DefaultPrevented is false
            : throw new IpcProtocolException("Renderer click reply has the wrong kind.")).ConfigureAwait(false);
    }
    /// <summary>Send a data-only trusted focus/blur request to the worker owning the committed document.</summary>
    public async Task DispatchPageFocusEventAsync(Guid documentId, int focusTargetId, PageFocusEventType eventType,
        CancellationToken cancellationToken)
    {
        var focusEventType = eventType switch
        {
            PageFocusEventType.Focus => "focus",
            PageFocusEventType.Blur => "blur",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!executeInlineScripts) { throw new PageNavigationException("Page focus events require the inline-script opt-in."); }
            if (documentId == Guid.Empty || documentId != committedDocumentId)
            { throw new PageNavigationException("Focused document is not the committed retained document; reload explicitly."); }
        }
        var request = new RendererMessage
        {
            Kind = "focus",
            Id = 1,
            DocumentId = documentId,
            ExecuteInlineScripts = true,
            FocusTargetId = focusTargetId,
            FocusEventType = focusEventType
        };
        try { RendererProtocol.Validate(request, 0); }
        catch (IpcProtocolException exception) { throw new PageNavigationException(exception.Message); }
        await ExchangeAsync(request, drain: true, cancellationToken, packet => packet.Message.Kind == "focus-result"
            ? true
            : throw new IpcProtocolException("Renderer focus reply has the wrong kind.")).ConfigureAwait(false);
    }
    private async Task<T> ExchangeAsync<T>(RendererMessage request, bool drain, CancellationToken cancellationToken,
        Func<RendererPacket, T> accept, PageScriptResourceLoader? scriptLoader = null, Guid scriptDocumentId = default)
    {
        using var deadline = drain
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
        var rendererId = 0L;
        var exchangeStart = timings is null ? 0 : Stopwatch.GetTimestamp();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool started;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                started = connection is null;
                rendererId = connection?.RendererId ?? timings?.NextRenderer() ?? 0;
                current = connection ??= Start(rendererId);
            }
            using var cancellationAbort = requireSandbox && OperatingSystem.IsWindows()
                ? deadline.Token.Register(() => current?.Abort())
                : default;
            if (request.Kind == "render") { lock (sync) { request = request with { CommittedDocumentId = committedDocumentId }; } }
            if (started)
            {
                using var helloTiming = timings?.Measure("renderer.hello", rendererId, request.Id);
                RendererPacket hello;
                using (timings?.Measure("renderer.hello-receive", rendererId, request.Id))
                {
                    hello = await Receive(current, deadline.Token).ConfigureAwait(false);
                }
                using (timings?.Measure("renderer.hello-validation", rendererId, request.Id))
                {
                    if (hello.Message.Kind != "hello") { throw new IpcProtocolException("Renderer handshake is missing."); }
                    if (requireSandbox && hello.Message.SandboxProfile != requiredSandboxProfile)
                    {
                        throw new IpcProtocolException("Renderer did not confirm the required platform confinement profile.");
                    }
                }
                current.RequireStartupDiagnostics();
                if (current.StartupDiagnostics is { } startupDiagnostics)
                { await startupDiagnostics.WaitAsync(deadline.Token).ConfigureAwait(false); }
            }
            // Only render requests: excludes startup/Hello, includes write, broker round trips,
            // identity/viewport validation, frame copy and the final cancellation check.
            using var renderTiming = request.Kind == "render"
                ? timings?.Measure("renderer.render-request", rendererId, request.Id)
                : null;
            await current.Channel.WriteAsync(request, cancellationToken: deadline.Token).ConfigureAwait(false);
            var scriptRequestId = 0;
            var scriptCharacters = 0;
            RendererPacket packet;
            while (true)
            {
                packet = await Receive(current, deadline.Token).ConfigureAwait(false);
                if (packet.Message.Id != request.Id) { throw new IpcProtocolException("Renderer reply identity does not match its request."); }
                if (packet.Message.Kind != "script-request") { break; }

                var scriptRequest = packet.Message;
                if (request.Kind != "render" || scriptRequest.DocumentId != scriptDocumentId
                    || scriptRequest.ScriptRequestId != ++scriptRequestId)
                { throw new IpcProtocolException("Parser-pause script request identity does not match its render exchange."); }

                string? source = null;
                string? scriptError = null;
                if (scriptLoader is null)
                {
                    scriptError = "External scripts require the browser-owned script resource broker.";
                }
                else
                {
                    try
                    {
                        source = await scriptLoader(BrowserUrl.Parse(scriptRequest.ScriptUrl!), deadline.Token).ConfigureAwait(false);
                        var characters = checked(scriptCharacters + source.Length);
                        if (source.Length > RendererProtocol.MaxScriptSourceCharacters
                            || characters > RendererProtocol.MaxScriptBatchCharacters)
                        {
                            source = null;
                            scriptError = "External script batch character limit exceeded.";
                        }
                        else { scriptCharacters = characters; }
                    }
                    catch (PageNavigationException exception)
                    {
                        scriptError = BoundText(exception.Message);
                    }
                }

                await current.Channel.WriteAsync(new RendererMessage
                {
                    Kind = "script-response",
                    Id = request.Id,
                    DocumentId = scriptDocumentId,
                    ScriptRequestId = scriptRequestId,
                    ScriptUrl = scriptRequest.ScriptUrl,
                    ScriptSource = source,
                    ScriptError = scriptError
                }, cancellationToken: deadline.Token).ConfigureAwait(false);
            }
            if (packet.Message.Kind == "error") { throw new PageNavigationException(packet.Message.Error!); }
            var result = accept(packet);
            // Drain superseded exchanges under the deadline so retained DOM survives and replies stay aligned.
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
            { current?.ReportMissingStartupDiagnostics(); }
            if (deadline.IsCancellationRequested) { Stop(current); }
            if (cancellationToken.IsCancellationRequested || lifetime.IsCancellationRequested) { throw; }
            throw new RendererProcessException("Renderer startup/render deadline exceeded; worker terminated.");
        }
        catch (Exception) when (deadline.IsCancellationRequested)
        {
            if (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
            { current?.ReportMissingStartupDiagnostics(); }
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
        finally
        {
            try
            {
                if (timings is not null) { timings.Record("renderer.exchange", rendererId, request.Id, Stopwatch.GetElapsedTime(exchangeStart)); }
            }
            finally { exchange.Release(); }
        }
    }
    private static string BoundText(string text) => text.Length <= RendererProtocol.MaxTextCharacters
        ? text : text[..(RendererProtocol.MaxTextCharacters - 3)] + "...";
    private static async Task<RendererPacket> Receive(Connection owner, CancellationToken cancellationToken)
    {
        var packet = await owner.Channel.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (packet is not null) { return packet; }
        // EOF can precede the worker's final stderr diagnostic; retain it within the same deadline.
        await owner.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (owner.Diagnostics is { } diagnostics) { await diagnostics.WaitAsync(cancellationToken).ConfigureAwait(false); }
        throw new RendererProcessException($"Renderer exited ({owner.Process.ExitCode}) before completing its reply.");
    }
    private Connection Start(long rendererId)
    {
        using var timing = timings?.Measure("renderer.start", rendererId);
        if (requireSandbox && OperatingSystem.IsWindows())
        {
            if (!rendererPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException("Windows confinement requires the framework-dependent renderer DLL.");
            }
            var worker = WindowsRendererSandbox.Start(Path.GetDirectoryName(rendererPath)!, fontPath,
                RuntimeEnvironment.GetRuntimeDirectory(), Path.GetFileName(rendererPath),
                workerArguments: WorkerStartupTimingEnabled ? ["--smoke-startup-timings"] : null,
                boldFontPath: boldFontPath, italicFontPath: italicFontPath, boldItalicFontPath: boldItalicFontPath,
                timing: timings?.Buffer, timingRenderer: rendererId);
            var connection = new Connection(worker.Process, null, worker, timings, rendererId, WorkerStartupTimingEnabled);
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
        if (boldFontPath is not null) { info.ArgumentList.Add("--font-bold"); info.ArgumentList.Add(boldFontPath); }
        if (italicFontPath is not null) { info.ArgumentList.Add("--font-italic"); info.ArgumentList.Add(italicFontPath); }
        if (boldItalicFontPath is not null) { info.ArgumentList.Add("--font-bold-italic"); info.ArgumentList.Add(boldItalicFontPath); }
        if (WorkerStartupTimingEnabled && !requireSandbox)
        { info.ArgumentList.Add("--smoke-startup-timings"); }
        var resources = requireSandbox ? new LinuxRendererResources() : null;
        var process = Process.Start(resources?.Wrap(info) ?? info) ?? throw new RendererProcessException("Renderer process did not start.");
        var owner = new Connection(process, resources, null, timings, rendererId, WorkerStartupTimingEnabled && !requireSandbox);
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

    private bool WorkerStartupTimingEnabled => timings is not null
        && !(requireSandbox && OperatingSystem.IsLinux());

    private static async Task DrainDiagnostics(Connection owner, Stream error)
    {
        // The connection/native worker owns this stream and closes it synchronously. A late read
        // completion after the optional join times out must not attempt a second stream disposal.
        using var reader = new StreamReader(error, leaveOpen: true);
        var buffer = new char[1024];
        var cancellation = owner.DiagnosticsCancellation;
        try
        {
            while (await reader.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false) is > 0 and var count)
            { owner.Append(buffer.AsSpan(0, count)); }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            owner.Append((" [stderr stream closed: " + exception.Message + "]").AsSpan());
        }
        finally { owner.CompleteDiagnostics(); }
    }
    public string? TakeFailure()
    {
        Connection? exited;
        string failure;
        lock (sync)
        {
            if (disposed || connection is null || !connection.Process.HasExited || exchange.CurrentCount == 0) { return null; }
            failure = $"Renderer process {connection.Process.Id} exited ({connection.Process.ExitCode}). {connection.Tail}";
            exited = connection;
            connection = null;
        }
        exited.Dispose();
        return failure;
    }
    private void Stop(Connection? owner)
    {
        if (owner is null) { return; }
        var detached = false;
        lock (sync)
        {
            if (!ReferenceEquals(connection, owner)) { return; }
            connection = null;
            detached = true;
        }
        if (detached) { owner.Dispose(); }
    }
    public void Dispose()
    {
        Connection? owned;
        lock (sync)
        {
            if (disposed) { return; }
            disposed = true;
            lifetime.Cancel();
            owned = connection;
            connection = null;
        }
        owned?.Dispose();
    }
}
