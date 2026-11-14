using System.Collections.Concurrent;
using VisualWeb.PageRendering;

namespace VisualWeb.Browser;

/// <summary>Runs a local renderer on one stable owner thread while browser resource brokers remain asynchronous.</summary>
internal sealed class LocalPageRenderer : IPageRenderer
{
    private readonly BlockingCollection<Action> work = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object sync = new();
    private readonly Thread thread;
    private readonly bool executeInlineScripts;
    private StaticPageRenderer? renderer;
    private bool disposed;

    internal LocalPageRenderer(Func<StaticPageRenderer> createRenderer, bool executeInlineScripts)
    {
        ArgumentNullException.ThrowIfNull(createRenderer);
        this.executeInlineScripts = executeInlineScripts;
        thread = new Thread(() => Run(createRenderer))
        {
            IsBackground = true,
            Name = "VisualWeb local renderer"
        };
        thread.Start();
        started.Task.GetAwaiter().GetResult();
    }

    public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken) =>
        RenderAsync(page, viewport, scriptLoader: null, cancellationToken);

    public async Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport,
        PageScriptResourceLoader? scriptLoader, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        return await Invoke(renderer => renderer.Render(page, viewport, linked.Token, scriptLoader: scriptLoader))
            .ConfigureAwait(false);
    }

    public async Task<BrowserPage> RenderRetainedAsync(LoadedPage page, PageViewport viewport,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        return await Invoke(renderer => renderer.Render(page, viewport, linked.Token, reuseDocument: true))
            .ConfigureAwait(false);
    }

    public void CommitDocument(Guid documentId) =>
        Invoke(renderer => { renderer.CommitDocument(documentId); return true; }).GetAwaiter().GetResult();

    public bool DispatchesLinkClicks => executeInlineScripts;
    public bool DispatchesPageFocusEvents => executeInlineScripts;

    public Task<bool> DispatchLinkEventAsync(Guid documentId, int linkIndex, string url,
        PageLinkEventType eventType, CancellationToken cancellationToken) =>
        Invoke(renderer => renderer.DispatchLinkEvent(documentId, linkIndex, url, eventType, cancellationToken));

    public Task<bool> DispatchLinkTargetEventAsync(Guid documentId, int focusTargetId,
        PageLinkEventType eventType, CancellationToken cancellationToken) =>
        Invoke(renderer => renderer.DispatchLinkTargetEvent(documentId, focusTargetId, eventType, cancellationToken));

    public Task DispatchPageFocusEventAsync(Guid documentId, int focusTargetId,
        PageFocusEventType eventType, CancellationToken cancellationToken) =>
        Invoke(renderer =>
        {
            renderer.DispatchPageFocusEvent(documentId, focusTargetId, eventType, cancellationToken);
            return true;
        });

    private Task<T> Invoke<T>(Func<StaticPageRenderer, T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            work.Add(() =>
            {
                try { completion.TrySetResult(action(renderer!)); }
                catch (Exception exception) { completion.TrySetException(exception); }
            });
        }
        return completion.Task;
    }

    private void Run(Func<StaticPageRenderer> createRenderer)
    {
        try
        {
            renderer = createRenderer();
            started.TrySetResult();
            foreach (var action in work.GetConsumingEnumerable()) { action(); }
        }
        catch (Exception exception)
        {
            started.TrySetException(exception);
        }
        finally
        {
            renderer?.Dispose();
            work.Dispose();
        }
    }

    public string? TakeFailure() => null;

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) { return; }
            disposed = true;
            lifetime.Cancel();
            work.CompleteAdding();
        }
        thread.Join();
        lifetime.Dispose();
    }
}
