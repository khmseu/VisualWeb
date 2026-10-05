using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;

namespace VisualWeb.Browser;

/// <summary>UI-thread transactional navigation over local or asynchronous process renderers.</summary>
/// <remarks>Process results are published only by the UI pump; process separation is not OS confinement.</remarks>
public sealed class BrowserController : IDisposable
{
    private sealed class Content(IPageSource source, IPageRenderer renderer)
    {
        internal IPageSource Source { get; } = source;
        internal IPageRenderer Renderer { get; } = renderer;
        internal LoadedPage? Document { get; set; }
        internal BrowserPage? Page { get; set; }
        internal PageViewport? Viewport { get; set; }
        internal bool ResizeFailed { get; set; }
    }
    private sealed class Operation(TabId tab, long generation, Task<LoadedPage> load, CancellationTokenSource cancellation,
        int? traversal, bool replace, bool resize = false)
    {
        internal TabId Tab { get; } = tab;
        internal long Generation { get; } = generation;
        internal Task<LoadedPage> Load { get; } = load;
        internal CancellationTokenSource Cancellation { get; } = cancellation;
        internal int? Traversal { get; } = traversal;
        internal bool Replace { get; } = replace;
        internal bool Resize { get; } = resize;
        internal LoadedPage? Document { get; set; }
        internal Task<BrowserPage>? Render { get; set; }
        internal PageViewport? Viewport { get; set; }
    }
    private readonly Dictionary<TabId, Content> content = [];
    private readonly List<Operation> operations = [];
    private readonly Func<IPageSource> sourceFactory;
    private readonly Func<IPageRenderer> rendererFactory;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private bool disposed;
    public BrowserSession Session { get; }
    public event Action<TabId>? Changed;
    public event Action<TabId, string>? Failed;

    public BrowserController(Func<IPageSource> sourceFactory, Func<IPageRenderer> rendererFactory, BrowserOptions? options = null)
    {
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
                operation.Viewport = size;
                operation.Render = content[operation.Tab].Renderer.RenderAsync(document, size.Value, operation.Cancellation.Token);
                if (!operation.Render.IsCompleted) { finished = false; return; }
            }
            var rendered = operation.Render.GetAwaiter().GetResult();
            var owner = content[operation.Tab];
            var tab = Session.Tab(operation.Tab);
            owner.Document = document; owner.Page = rendered; owner.Viewport = operation.Viewport;
            if (operation.Resize)
            {
                if (owner.ResizeFailed) { tab.Error = null; }
                if (tab.Error is null) { tab.Status = rendered.Status; }
            }
            else
            {
                tab.History.Commit(document.Url, operation.Traversal, operation.Replace);
                tab.AddressText = document.Url.Href; tab.Title = rendered.Title; tab.Status = rendered.Status;
                tab.Error = null; tab.IsLoading = false;
            }
            owner.ResizeFailed = false;
            Changed?.Invoke(operation.Tab);
        }
        catch (OperationCanceledException)
        {
            if (current) { Report(operation.Tab, "Navigation/rendering canceled."); }
        }
        catch (Exception exception) when (IsPageFailure(exception))
        {
            if (current)
            {
                if (operation.Resize)
                {
                    var owner = content[operation.Tab];
                    owner.Page = null; owner.Viewport = operation.Viewport; owner.ResizeFailed = true;
                }
                Report(operation.Tab, (operation.Resize ? "Resize rendering failed: " : "") + exception.Message);
            }
        }
        finally
        {
            if (finished) { operations.Remove(operation); operation.Cancellation.Dispose(); }
        }
    }
    public void Resize(TabId id, PageViewport viewport)
    {
        Check();
        var owner = content[id];
        var tab = Session.Tab(id);
        if (tab.IsLoading || owner.Document is null) { return; }
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
