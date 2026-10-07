using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;

namespace VisualWeb.Browser;

/// <summary>UI-thread transactional navigation over local or asynchronous process renderers.</summary>
/// <remarks>Process results are published only by the UI pump; process separation is not OS confinement.
/// <see cref="BrowserTab.Origin"/> is taken from the browser-side <see cref="LoadedPage"/> and changes only when that
/// document is rendered, accepted by <see cref="IPageRenderer.CommitDocument"/> and published.
/// With <see cref="IsolatesOrigins"/>, top-level navigation keeps each tab renderer strictly per origin
/// (<see href="https://html.spec.whatwg.org/multipage/browsers.html#same-origin">same origin</see>, spec ID html):
/// the final <see cref="LoadedPage.Origin"/> is compared with the committed document origin; same-origin documents
/// reuse the committed renderer, while cross-origin and every new opaque document render in a fresh factory
/// candidate that replaces (and disposes) the committed renderer only after render, commit and history succeed.
/// This is navigation renderer rotation, not site isolation, frame isolation or same-origin policy enforcement.</remarks>
public sealed class BrowserController : IDisposable
{
    private sealed class Content(IPageSource source, IPageRenderer renderer)
    {
        internal IPageSource Source { get; } = source;
        internal IPageRenderer Renderer { get; set; } = renderer;
        /// <summary>Whether any document content has been sent to <see cref="Renderer"/>.</summary>
        internal bool Used { get; set; }
        internal LoadedPage? Document { get; set; }
        internal BrowserPage? Page { get; set; }
        internal PageViewport? Viewport { get; set; }
        internal double ScrollY { get; set; }
        internal bool ResizeFailed { get; set; }
    }
    private sealed class Operation(TabId tab, long generation, Task<LoadedPage> load, CancellationTokenSource cancellation,
        int? traversal, bool replace, bool resize = false, bool scroll = false)
    {
        internal TabId Tab { get; } = tab;
        internal long Generation { get; } = generation;
        internal Task<LoadedPage> Load { get; } = load;
        internal CancellationTokenSource Cancellation { get; } = cancellation;
        internal int? Traversal { get; } = traversal;
        internal bool Replace { get; } = replace;
        internal bool Resize { get; } = resize;
        internal bool Scroll { get; } = scroll;
        internal LoadedPage? Document { get; set; }
        internal Task<BrowserPage>? Render { get; set; }
        internal PageViewport? Viewport { get; set; }
        /// <summary>The renderer that actually received this operation's document.</summary>
        internal IPageRenderer? Renderer { get; set; }
        /// <summary>An unpromoted origin-isolation candidate owned (and disposed) by this operation.</summary>
        internal IPageRenderer? Candidate { get; set; }
        internal void ReleaseCandidate()
        {
            var candidate = Candidate;
            Candidate = null;
            candidate?.Dispose();
        }
    }
    private readonly Dictionary<TabId, Content> content = [];
    private readonly List<Operation> operations = [];
    private readonly Func<IPageSource> sourceFactory;
    private readonly Func<IPageRenderer> rendererFactory;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private bool disposed;
    public BrowserSession Session { get; }
    /// <summary>Whether top-level navigations rotate to a fresh renderer for each new cross-origin or opaque document.</summary>
    public bool IsolatesOrigins { get; }
    public event Action<TabId>? Changed;
    public event Action<TabId, string>? Failed;

    public BrowserController(Func<IPageSource> sourceFactory, Func<IPageRenderer> rendererFactory, BrowserOptions? options = null,
        bool isolateOrigins = false)
    {
        IsolatesOrigins = isolateOrigins;
        ArgumentNullException.ThrowIfNull(sourceFactory);
        ArgumentNullException.ThrowIfNull(rendererFactory);
        this.sourceFactory = sourceFactory; this.rendererFactory = rendererFactory;
        Session = new(options);
    }
    public BrowserTab CreateTab(BrowserWindowId window)
    {
        Check();
        var tab = Session.CreateTab(window);
        IPageSource? source = null;
        try
        {
            source = sourceFactory();
            content.Add(tab.Id, new(source, rendererFactory()));
            return tab;
        }
        catch { source?.Dispose(); Session.CloseTab(tab.Id); throw; }
    }
    public BrowserPage? Page(TabId tab) { Check(); return content[tab].Page; }
    public double ScrollY(TabId tab) { Check(); return content[tab].ScrollY; }
    public bool ActivateLink(TabId id, double x, double y, PageViewport? displayedViewport = null)
    {
        Check();
        var owner = content[id];
        if (!double.IsFinite(x) || !double.IsFinite(y))
        { throw new PageNavigationException("Link coordinates must be finite."); }
        if (owner.Page is null || owner.Viewport is not { } viewport
            || x < 0 || y < 0 || x >= viewport.Width || y >= viewport.Height) { return false; }
        if (displayedViewport is { } visible
            && (viewport.Width != visible.Width || viewport.Height != visible.Height || viewport.Scale != visible.Scale))
        { return false; }
        for (var index = owner.Page.LinkTargets.Count - 1; index >= 0; index--)
        {
            var link = owner.Page.LinkTargets[index];
            if (!link.Contains(x, y)) { continue; }
            var url = BrowserUrl.Parse(link.Url);
            if (url.Protocol is not ("http:" or "https:" or "file:" or "data:"))
            { throw new PageNavigationException("Unsupported link URL scheme: " + url.Protocol); }
            Navigate(id, url.Href);
            return true;
        }
        return false;
    }
    public void SetAddress(TabId id, string value)
    {
        Check();
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > Session.Options.MaxAddressCharacters) { throw new BrowserLimitException("Address length limit exceeded."); }
        Session.Tab(id).AddressText = value;
        Changed?.Invoke(id);
    }
    public void Navigate(TabId id, string address)
    {
        Check();
        ArgumentNullException.ThrowIfNull(address);
        var tab = Session.Tab(id);
        if (address.Length > Session.Options.MaxAddressCharacters) { throw new BrowserLimitException("Address length limit exceeded."); }
        Start(tab, BrowserUrl.Parse(address), null, false);
    }
    public void Back(TabId id)
    {
        Check();
        var tab = Session.Tab(id);
        if (!tab.History.CanGoBack) { throw new InvalidOperationException("No back history entry."); }
        var index = tab.History.Index - 1;
        Start(tab, tab.History.Entries[index], index, false);
    }
    public void Forward(TabId id)
    {
        Check();
        var tab = Session.Tab(id);
        if (!tab.History.CanGoForward) { throw new InvalidOperationException("No forward history entry."); }
        var index = tab.History.Index + 1;
        Start(tab, tab.History.Entries[index], index, false);
    }
    public void Reload(TabId id)
    {
        Check();
        var tab = Session.Tab(id);
        Start(tab, tab.History.Current ?? throw new InvalidOperationException("No committed page to reload."), null, true);
    }
    private void Start(BrowserTab tab, BrowserUrl url, int? traversal, bool replace)
    {
        Limit();
        Cancel(tab.Id);
        var owner = content[tab.Id];
        owner.ScrollY = owner.Viewport?.ScrollY ?? 0;
        var cancellation = new CancellationTokenSource();
        Task<LoadedPage> task;
        try { task = content[tab.Id].Source.LoadAsync(url, cancellation.Token); }
        catch { cancellation.Dispose(); throw; }
        operations.Add(new(tab.Id, checked(++tab.Generation), task, cancellation, traversal, replace));
        tab.AddressText = url.Href; tab.Error = null; tab.IsLoading = true; tab.Status = "Loading " + url.Href;
        Changed?.Invoke(tab.Id);
    }
    /// <summary>Observe completed loading/rendering without awaiting on the UI thread.</summary>
    public void Pump(Func<TabId, PageViewport?> viewport)
    {
        Check();
        ArgumentNullException.ThrowIfNull(viewport);
        foreach (var (id, owner) in content)
        {
            if (owner.Renderer.TakeFailure() is { } failure) { Report(id, failure, finishNavigation: false); }
        }
        foreach (var operation in operations.ToArray())
        {
            Advance(operation, () => viewport(operation.Tab));
        }
    }
    private void Advance(Operation operation, Func<PageViewport?> viewport)
    {
        var current = Session.Contains(operation.Tab) && Session.Tab(operation.Tab).Generation == operation.Generation;
        if (!operation.Load.IsCompleted || operation.Render is { IsCompleted: false }) { return; }
        var finished = true;
        try
        {
            var document = operation.Load.GetAwaiter().GetResult();
            if (!current)
            {
                if (operation.Render is { } stale) { _ = stale.GetAwaiter().GetResult(); }
                return;
            }
            if (operation.Render is null)
            {
                var size = operation.Viewport ?? viewport();
                if (size is null) { finished = false; return; }
                operation.Document = document;
                operation.Viewport = operation.Resize ? size : size.Value with { ScrollY = 0 };
                var renderer = operation.Renderer = Select(content[operation.Tab], operation, document);
                operation.Render = operation.Resize
                    ? renderer.RenderRetainedAsync(document, operation.Viewport.Value, operation.Cancellation.Token)
                    : renderer.RenderAsync(document, operation.Viewport.Value, operation.Cancellation.Token);
                if (!operation.Render.IsCompleted) { finished = false; return; }
            }
            var rendered = operation.Render.GetAwaiter().GetResult();
            if (!operation.Resize)
            {
                var latest = viewport();
                if (latest is null) { finished = false; return; }
                var geometry = latest.Value with { ScrollY = 0 };
                if (geometry != operation.Viewport)
                {
                    operation.Viewport = geometry;
                    operation.Render = operation.Renderer!.RenderRetainedAsync(document, geometry, operation.Cancellation.Token);
                    finished = false;
                    return;
                }
            }
            var owner = content[operation.Tab];
            var tab = Session.Tab(operation.Tab);
            // Throwing steps precede publication so a rejected commit leaves renderer, document, origin and history intact.
            operation.Renderer!.CommitDocument(document.DocumentId);
            if (!operation.Resize) { tab.History.Commit(document.Url, operation.Traversal, operation.Replace); }
            var previous = owner.Renderer;
            if (operation.Candidate is { } candidate)
            {
                operation.Candidate = null;
                owner.Renderer = candidate; owner.Used = true;
            }
            owner.ScrollY = Math.Min(operation.Viewport!.Value.ScrollY,
                Math.Max(0, rendered.ScrollHeight - operation.Viewport.Value.Height));
            owner.Document = document; owner.Page = rendered;
            owner.Viewport = operation.Viewport.Value with { ScrollY = owner.ScrollY };
            tab.Origin = document.Origin;
            if (operation.Resize)
            {
                if (owner.ResizeFailed) { tab.Error = null; }
                if (tab.Error is null) { tab.Status = rendered.Status; }
            }
            else
            {
                tab.AddressText = document.Url.Href; tab.Title = rendered.Title; tab.Status = rendered.Status;
                tab.Error = null; tab.IsLoading = false;
            }
            owner.ResizeFailed = false;
            if (!ReferenceEquals(previous, owner.Renderer)) { previous.Dispose(); }
            Changed?.Invoke(operation.Tab);
        }
        catch (OperationCanceledException)
        {
            if (current)
            {
                if (operation.Resize) { content[operation.Tab].ScrollY = content[operation.Tab].Viewport?.ScrollY ?? 0; }
                Report(operation.Tab, "Navigation/rendering canceled.");
            }
        }
        catch (Exception exception) when (IsPageFailure(exception))
        {
            if (current)
            {
                if (operation.Resize)
                {
                    var owner = content[operation.Tab];
                    owner.ScrollY = owner.Viewport?.ScrollY ?? 0;
                    if (!operation.Scroll)
                    { owner.Page = null; owner.Viewport = operation.Viewport; owner.ResizeFailed = true; }
                }
                Report(operation.Tab, (operation.Scroll ? "Scroll rendering failed: " : operation.Resize ? "Resize rendering failed: " : "") + exception.Message);
            }
        }
        finally
        {
            if (finished)
            {
                operations.Remove(operation); operation.Cancellation.Dispose();
                operation.ReleaseCandidate();
            }
        }
    }
    /// <summary>Choose the renderer for a loaded document: the committed one or a fresh origin-isolation candidate.</summary>
    private IPageRenderer Select(Content owner, Operation operation, LoadedPage document)
    {
        var reuse = !IsolatesOrigins || operation.Resize || (owner.Document is { } committed
            ? !document.Origin.IsOpaque && committed.Origin.IsSameOrigin(document.Origin)
            : !owner.Used);
        if (reuse)
        {
            owner.Used = true;
            return owner.Renderer;
        }
        IPageRenderer candidate;
        try { candidate = rendererFactory() ?? throw new InvalidOperationException("Renderer factory returned null."); }
        catch (Exception exception) when (IsPageFailure(exception)
            || exception is ArgumentException or PlatformNotSupportedException or InvalidOperationException)
        {
            throw new RendererProcessException("Origin-isolated renderer creation failed: " + exception.Message);
        }
        operation.Candidate = candidate;
        return candidate;
    }
    public void Resize(TabId id, PageViewport viewport)
    {
        Check();
        var owner = content[id];
        var tab = Session.Tab(id);
        if (tab.IsLoading || owner.Document is null) { return; }
        viewport = viewport with { ScrollY = owner.ScrollY };
        var pending = operations.FirstOrDefault(op => op.Tab == id && op.Generation == tab.Generation);
        if (pending?.Viewport == viewport) { return; }
        if (owner.Viewport == viewport)
        {
            if (pending is not null) { Cancel(id); tab.Generation = checked(tab.Generation + 1); }
            return;
        }
        Limit();
        Cancel(id);
        var operation = new Operation(id, checked(++tab.Generation), Task.FromResult(owner.Document),
            new(), null, false, resize: true)
        { Viewport = viewport };
        operations.Add(operation);
        Advance(operation, () => viewport);
    }
    /// <summary>Scroll the active retained document in CSS pixels; finite deltas saturate at its extent.</summary>
    public void Scroll(TabId id, double delta)
    {
        Check();
        if (!double.IsFinite(delta)) { throw new ArgumentOutOfRangeException(nameof(delta), "Scroll delta must be finite."); }
        var owner = content[id];
        var tab = Session.Tab(id);
        if (tab.IsLoading || owner.Document is null || owner.Page is null || owner.Viewport is null || delta == 0) { return; }
        var pending = operations.FirstOrDefault(op => op.Tab == id && op.Generation == tab.Generation);
        var viewport = pending?.Viewport ?? owner.Viewport.Value;
        var maximum = Math.Max(0, owner.Page.ScrollHeight - viewport.Height);
        var offset = Math.Clamp(owner.ScrollY + delta, 0, maximum);
        if (offset == owner.ScrollY) { return; }
        Limit();
        Cancel(id);
        owner.ScrollY = offset;
        viewport = viewport with { ScrollY = offset };
        var operation = new Operation(id, checked(++tab.Generation), Task.FromResult(owner.Document),
            new(), null, false, resize: true, scroll: true)
        { Viewport = viewport };
        operations.Add(operation);
        Advance(operation, () => viewport);
    }
    public void Report(TabId id, string error) => Report(id, error, finishNavigation: true);
    private void Report(TabId id, string error, bool finishNavigation)
    {
        Check();
        var tab = Session.Tab(id);
        if (finishNavigation) { tab.IsLoading = false; }
        tab.Error = error; tab.Status = error;
        Failed?.Invoke(id, error); Changed?.Invoke(id);
    }
    public void CloseTab(TabId id)
    {
        Check();
        Cancel(id);
        foreach (var operation in operations.Where(op => op.Tab == id)) { operation.ReleaseCandidate(); }
        var owner = content[id];
        content.Remove(id);
        owner.Source.Dispose(); owner.Renderer.Dispose(); Session.CloseTab(id);
    }
    public void CloseWindow(BrowserWindowId id)
    {
        Check();
        foreach (var tab in Session.Window(id).Tabs.ToArray()) { CloseTab(tab.Id); }
        if (Session.Windows.Any(w => w.Id == id)) { Session.CloseWindow(id); }
    }
    private void Limit()
    {
        if (operations.Count >= Session.Options.MaxPendingLoads) { throw new BrowserLimitException("Pending load/render limit exceeded; wait for canceled operations to finish."); }
    }
    private void Cancel(TabId id)
    {
        foreach (var operation in operations.Where(op => op.Tab == id)) { operation.Cancellation.Cancel(); }
    }
    internal static bool IsPageFailure(Exception exception) => StaticPageRenderer.IsRenderFailure(exception)
        || exception is IOException or UnauthorizedAccessException or UrlParseException or WebDecodingException or BrowserLimitException;
    private void Check()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (thread != Environment.CurrentManagedThreadId) { throw new InvalidOperationException("Browser controller must stay on its creating thread."); }
    }
    public void Dispose()
    {
        if (disposed) { return; }
        Check();
        foreach (var tab in content.Keys.ToArray()) { CloseTab(tab); }
        foreach (var operation in operations)
        {
            Task task = operation.Render is { } render ? render : operation.Load;
            _ = task.ContinueWith(done =>
            {
                if (done.IsFaulted) { _ = done.Exception; }
                operation.Cancellation.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        operations.Clear(); disposed = true;
    }
}
