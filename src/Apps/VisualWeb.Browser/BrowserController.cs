using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;

namespace VisualWeb.Browser;

/// <summary>Main-thread transactional navigation with per-tab loaders/renderers.</summary>
/// <remarks>This is development-only, same-process state separation, not a security boundary.</remarks>
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
    private sealed record Load(TabId Tab, long Generation, Task<LoadedPage> Task, CancellationTokenSource Cancellation,
        int? Traversal, bool Replace);
    private readonly Dictionary<TabId, Content> content = [];
    private readonly List<Load> loads = [];
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
        this.sourceFactory = sourceFactory;
        this.rendererFactory = rendererFactory;
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
            var renderer = rendererFactory();
            content.Add(tab.Id, new(source, renderer));
            return tab;
        }
        catch
        {
            source?.Dispose();
            Session.CloseTab(tab.Id);
            throw;
        }
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
        var tab = Session.Tab(id);
        if (address.Length > Session.Options.MaxAddressCharacters) { throw new BrowserLimitException("Address length limit exceeded."); }
        var url = BrowserUrl.Parse(address);
        Start(tab, url, null, false);
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
        if (loads.Count >= Session.Options.MaxPendingLoads) { throw new BrowserLimitException("Pending navigation limit exceeded; wait for canceled requests to finish."); }
        Cancel(tab.Id);
        var cancellation = new CancellationTokenSource();
        Task<LoadedPage> task;
        try { task = content[tab.Id].Source.LoadAsync(url, cancellation.Token); }
        catch { cancellation.Dispose(); throw; }
        loads.Add(new(tab.Id, checked(++tab.Generation), task, cancellation, traversal, replace));
        tab.AddressText = url.Href;
        tab.Error = null;
        tab.IsLoading = true;
        tab.Status = "Loading " + url.Href;
        Changed?.Invoke(tab.Id);
    }
    /// <summary>Observe completed loads and render only on the owning thread.</summary>
    public void Pump(Func<TabId, PageViewport?> viewport)
    {
        Check();
        ArgumentNullException.ThrowIfNull(viewport);
        foreach (var load in loads.Where(load => load.Task.IsCompleted).ToArray())
        {
            // Minimized windows keep completed loads pending until a valid viewport is available.
            var current = Session.Contains(load.Tab) && Session.Tab(load.Tab).Generation == load.Generation;
            var size = current && !load.Task.IsFaulted && !load.Task.IsCanceled ? viewport(load.Tab) : null;
            if (current && size is null && !load.Task.IsFaulted && !load.Task.IsCanceled) { continue; }
            loads.Remove(load);
            try
            {
                var document = load.Task.GetAwaiter().GetResult();
                if (!current) { continue; }
                var owner = content[load.Tab];
                var rendered = owner.Renderer.Render(document, size!.Value, load.Cancellation.Token);
                var tab = Session.Tab(load.Tab);
                owner.Document = document;
                owner.Page = rendered;
                owner.Viewport = size;
                owner.ResizeFailed = false;
                tab.History.Commit(document.Url, load.Traversal, load.Replace);
                tab.AddressText = document.Url.Href;
                tab.Title = rendered.Title;
                tab.Status = rendered.Status;
                tab.Error = null;
                tab.IsLoading = false;
                Changed?.Invoke(load.Tab);
            }
            catch (OperationCanceledException)
            {
                if (current) { Report(load.Tab, "Navigation canceled."); }
            }
            catch (Exception exception) when (IsPageFailure(exception))
            {
                if (current) { Report(load.Tab, exception.Message); }
            }
            finally { load.Cancellation.Dispose(); }
        }
    }
    public void Resize(TabId id, PageViewport viewport)
    {
        Check();
        var owner = content[id];
        if (owner.Document is null || owner.Viewport == viewport) { return; }
        try
        {
            var rendered = owner.Renderer.Render(owner.Document, viewport, CancellationToken.None);
            owner.Page = rendered;
            owner.Viewport = viewport;
            if (owner.ResizeFailed) { Session.Tab(id).Error = null; owner.ResizeFailed = false; }
            if (Session.Tab(id).Error is null) { Session.Tab(id).Status = rendered.Status; }
            Changed?.Invoke(id);
        }
        catch (Exception exception) when (IsPageFailure(exception))
        {
            owner.Page = null;
            owner.Viewport = viewport;
            owner.ResizeFailed = true;
            Report(id, "Resize rendering failed: " + exception.Message);
        }
    }
    public void Report(TabId id, string error)
    {
        Check();
        var tab = Session.Tab(id);
        tab.IsLoading = false;
        tab.Error = error;
        tab.Status = error;
        Failed?.Invoke(id, error);
        Changed?.Invoke(id);
    }
    public void CloseTab(TabId id)
    {
        Check();
        Cancel(id);
        var owner = content[id];
        content.Remove(id);
        owner.Source.Dispose();
        owner.Renderer.Dispose();
        Session.CloseTab(id);
    }
    public void CloseWindow(BrowserWindowId id)
    {
        Check();
        var window = Session.Window(id);
        foreach (var tab in window.Tabs.ToArray()) { CloseTab(tab.Id); }
        if (Session.Windows.Any(w => w.Id == id)) { Session.CloseWindow(id); }
    }
    private void Cancel(TabId id)
    {
        foreach (var load in loads.Where(load => load.Tab == id)) { load.Cancellation.Cancel(); }
    }
    internal static bool IsPageFailure(Exception exception) => exception is PageNavigationException or IOException or UnauthorizedAccessException
        or UrlParseException or WebDecodingException or HtmlLimitException or UnsupportedHtmlException
        or CssLimitException or UnsupportedCssException or LayoutLimitException or UnsupportedLayoutException
        or TextLimitException or UnsupportedTextException or FontLoadException or PaintLimitException or UnsupportedPaintException
        or BrowserLimitException;
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
        foreach (var load in loads)
        {
            // Observe abandoned tasks, including sources that finish after cancellation.
            _ = load.Task.ContinueWith(task =>
            {
                if (task.IsFaulted) { _ = task.Exception; }
                load.Cancellation.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        loads.Clear();
        disposed = true;
    }
}
