using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Paint;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class OriginIsolationTests
{
    private static readonly PageViewport Viewport = new(20, 20, 1);
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");

    private sealed class Harness : IDisposable
    {
        internal List<ControllerTests.Source> Sources { get; } = [];
        internal List<FakeRenderer> Renderers { get; } = [];
        internal Func<FakeRenderer>? NextRenderer { get; set; }
        internal Exception? FactoryFailure { get; set; }
        internal BrowserController Controller { get; }
        internal Harness(bool isolate = true, BrowserOptions? options = null) =>
            Controller = new(() => { var source = new ControllerTests.Source(); Sources.Add(source); return source; }, () =>
            {
                if (FactoryFailure is { } failure) { throw failure; }
                var renderer = NextRenderer?.Invoke() ?? new FakeRenderer();
                renderer.Index = Renderers.Count;
                Renderers.Add(renderer);
                return renderer;
            }, options, isolate);
        internal BrowserTab Tab(BrowserWindowId? window = null) => Controller.CreateTab(window ?? Controller.Session.CreateWindow().Id);
        internal LoadedPage Complete(BrowserTab tab, string requested, string final, string html = "<!doctype html>")
        {
            Controller.Navigate(tab.Id, requested);
            return Finish(tab, final, html);
        }
        internal LoadedPage Finish(BrowserTab tab, string final, string html = "<!doctype html>")
        {
            var document = new LoadedPage(BrowserUrl.Parse(final), html, 200, []);
            SourceOf(tab).Requests[^1].Completion.SetResult(document);
            Controller.Pump(_ => Viewport);
            return document;
        }
        internal ControllerTests.Source SourceOf(BrowserTab tab) => Sources[(int)tab.Id.Value - 1];
        public void Dispose() => Controller.Dispose();
    }

    internal sealed class FakeRenderer : IPageRenderer
    {
        internal int Index { get; set; }
        internal bool Async { get; set; }
        internal bool Fail { get; set; }
        internal bool RejectCommit { get; set; }
        internal string? Failure { get; set; }
        internal int Disposals { get; private set; }
        internal List<LoadedPage> Pages { get; } = [];
        internal List<LoadedPage> RetainedPages { get; } = [];
        internal List<Guid> Committed { get; } = [];
        internal List<TaskCompletionSource<BrowserPage>> Pending { get; } = [];
        internal List<CancellationToken> Tokens { get; } = [];
        public Task<BrowserPage> RenderAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposals > 0, this);
            Pages.Add(page);
            return Render(viewport, cancellationToken);
        }
        public Task<BrowserPage> RenderRetainedAsync(LoadedPage page, PageViewport viewport, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposals > 0, this);
            RetainedPages.Add(page);
            return Render(viewport, cancellationToken);
        }
        private Task<BrowserPage> Render(PageViewport viewport, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            if (Async) { var pending = new TaskCompletionSource<BrowserPage>(); Pending.Add(pending); return pending.Task; }
            if (Fail) { return Task.FromException<BrowserPage>(new UnsupportedHtmlException("Unsupported candidate page.")); }
            return Task.FromResult(Blank(viewport, "Renderer " + Index));
        }
        public void CommitDocument(Guid documentId)
        {
            ObjectDisposedException.ThrowIf(Disposals > 0, this);
            if (RejectCommit) { throw new PageNavigationException("Candidate commit rejected."); }
            Committed.Add(documentId);
        }
        public string? TakeFailure() { var failure = Failure; Failure = null; return failure; }
        public void Dispose() => Disposals++;
    }
    internal static BrowserPage Blank(PageViewport viewport, string title)
    {
        using var fonts = new PaintFontRegistry();
        return new(CpuRasterizer.Render(new(viewport.Width, viewport.Height, []), fonts, viewport.Scale), title, "Ready");
    }

    [Fact]
    public void LowLevelControllersKeepOneTabRendererUnlessIsolationIsRequested()
    {
        using var harness = new Harness(isolate: false);
        Assert.False(harness.Controller.IsolatesOrigins);
        var tab = harness.Tab();
        harness.Complete(tab, "https://first.example/", "https://first.example/");
        harness.Complete(tab, "https://second.example/", "https://second.example/");
        harness.Complete(tab, "data:text/html,a", "data:text/html,a");
        harness.Complete(tab, "data:text/html,a", "data:text/html,a");
        var renderer = Assert.Single(harness.Renderers);
        Assert.Equal(4, renderer.Committed.Count);
        Assert.Equal(0, renderer.Disposals);
        using var isolated = new Harness();
        Assert.True(isolated.Controller.IsolatesOrigins);
    }

    [Fact]
    public void FinalRedirectedOriginDecidesReuseWithSchemeHostAndPortNormalization()
    {
        using var harness = new Harness();
        var tab = harness.Tab();
        var first = harness.Complete(tab, "https://first.example/a", "https://first.example:443/redirected");
        var pristine = Assert.Single(harness.Renderers);
        Assert.Same(first, Assert.Single(pristine.Pages));
        Assert.Same(first.Origin, tab.Origin);

        // The requested URL is cross-origin, but the authoritative final response is same-origin.
        var same = harness.Complete(tab, "https://other.example/b", "https://FIRST.example/c");
        Assert.Single(harness.Renderers);
        Assert.Same(same, pristine.Pages[^1]);
        Assert.Same(same.Origin, tab.Origin);
        Assert.Equal(0, pristine.Disposals);

        // The requested URL is same-origin, but the redirect changes the port.
        var port = harness.Complete(tab, "https://first.example/d", "https://first.example:8443/e");
        Assert.Equal(2, harness.Renderers.Count);
        Assert.Equal(1, pristine.Disposals);
        Assert.Equal(2, pristine.Pages.Count);
        Assert.Same(port, Assert.Single(harness.Renderers[1].Pages));
        Assert.Equal(new[] { port.DocumentId }, harness.Renderers[1].Committed);
        Assert.Same(port.Origin, tab.Origin);
        Assert.Equal("Renderer 1", tab.Title);

        var scheme = harness.Complete(tab, "https://first.example:8443/f", "http://first.example:8443/f");
        Assert.Equal(3, harness.Renderers.Count);
        Assert.Equal(1, harness.Renderers[1].Disposals);
        var defaultPort = harness.Complete(tab, "http://first.example:8443/g", "http://first.example:80/g");
        Assert.Equal(4, harness.Renderers.Count);
        var normalized = harness.Complete(tab, "http://FIRST.EXAMPLE/h", "http://first.example/h");
        Assert.Equal(4, harness.Renderers.Count);
        Assert.Same(normalized, harness.Renderers[3].Pages[^1]);
        Assert.True(defaultPort.Origin.IsSameOrigin(normalized.Origin));
        var host = harness.Complete(tab, "http://first.example/i", "http://www.first.example/i");
        Assert.Equal(5, harness.Renderers.Count);
        Assert.All(harness.Renderers.SkipLast(1), renderer => Assert.Equal(1, renderer.Disposals));
        Assert.Equal(0, harness.Renderers[^1].Disposals);
        Assert.Same(host.Origin, tab.Origin);
        Assert.False(scheme.Origin.IsSameOrigin(port.Origin));
        Assert.Equal(7, tab.History.Entries.Count);
    }

    [Fact]
    public void OpaqueDocumentsRotateForEveryNewDocumentButRetainedResizeStays()
    {
        using var harness = new Harness();
        var tab = harness.Tab();
        var first = harness.Complete(tab, "data:text/html,a", "data:text/html,a");
        var pristine = Assert.Single(harness.Renderers);
        Assert.Same(first, Assert.Single(pristine.Pages));
        harness.Controller.Resize(tab.Id, new(30, 20, 1));
        Assert.Same(first, Assert.Single(pristine.RetainedPages));
        Assert.Single(harness.Renderers);
        Assert.Single(harness.SourceOf(tab).Requests);

        harness.Controller.Reload(tab.Id);
        var reloaded = harness.Finish(tab, "data:text/html,a");
        Assert.Equal(2, harness.Renderers.Count);
        Assert.Equal(1, pristine.Disposals);
        Assert.Same(reloaded, Assert.Single(harness.Renderers[1].Pages));
        Assert.False(first.Origin.IsSameOrigin(reloaded.Origin));
        Assert.Same(reloaded.Origin, tab.Origin);
        harness.Controller.Resize(tab.Id, new(40, 20, 1));
        Assert.Same(reloaded, Assert.Single(harness.Renderers[1].RetainedPages));
        Assert.Empty(pristine.RetainedPages.Skip(1));

        harness.Complete(tab, "https://first.example/", "https://first.example/");
        Assert.Equal(3, harness.Renderers.Count);
        harness.Complete(tab, "about:blank", "about:blank");
        Assert.Equal(4, harness.Renderers.Count);
        harness.Complete(tab, "about:blank", "about:blank");
        Assert.Equal(5, harness.Renderers.Count);
        Assert.All(harness.Renderers.SkipLast(1), renderer => Assert.Equal(1, renderer.Disposals));
        Assert.All(harness.Renderers, renderer => Assert.Single(renderer.Pages));
    }

    [Fact]
    public void FailedCandidatesAreDisposedWithoutChangingCommittedRendererDocumentOrHistory()
    {
        using var harness = new Harness();
        var tab = harness.Tab();
        var committed = harness.Complete(tab, "https://first.example/", "https://first.example/");
        var owner = harness.Renderers[0];
        var page = harness.Controller.Page(tab.Id);
        void AssertRetained(int renderers)
        {
            Assert.Equal(renderers, harness.Renderers.Count);
            Assert.Equal(0, owner.Disposals);
            Assert.Same(committed, Assert.Single(owner.Pages));
            Assert.Same(page, harness.Controller.Page(tab.Id));
            Assert.Same(committed.Origin, tab.Origin);
            Assert.Equal("Renderer 0", tab.Title);
            Assert.Equal("https://first.example/", Assert.Single(tab.History.Entries).Href);
            Assert.False(tab.IsLoading);
            Assert.NotNull(tab.Error);
        }

        harness.NextRenderer = () => new FakeRenderer { Fail = true };
        harness.Complete(tab, "https://second.example/", "https://second.example/");
        AssertRetained(2);
        Assert.Contains("Unsupported candidate", tab.Error);
        Assert.Equal(1, harness.Renderers[1].Disposals);
        Assert.Empty(harness.Renderers[1].Committed);

        harness.NextRenderer = () => new FakeRenderer { RejectCommit = true };
        harness.Complete(tab, "https://second.example/", "https://second.example/");
        AssertRetained(3);
        Assert.Contains("commit rejected", tab.Error);
        Assert.Equal(1, harness.Renderers[2].Disposals);

        harness.NextRenderer = null;
        harness.FactoryFailure = new PlatformNotSupportedException("factory unavailable");
        harness.Complete(tab, "https://second.example/", "https://second.example/");
        AssertRetained(3);
        Assert.Contains("factory unavailable", tab.Error);
        harness.FactoryFailure = null;

        harness.NextRenderer = () => new FakeRenderer { Async = true };
        harness.Controller.Navigate(tab.Id, "https://second.example/");
        harness.Finish(tab, "https://second.example/");
        harness.Renderers[3].Pending[0].SetException(new IOException("candidate worker crashed"));
        harness.Controller.Pump(_ => Viewport);
        AssertRetained(4);
        Assert.Equal(1, harness.Renderers[3].Disposals);

        // Same-origin navigation still reuses the retained committed renderer after candidate failures.
        harness.NextRenderer = null;
        var same = harness.Complete(tab, "https://first.example/next", "https://first.example/next");
        Assert.Equal(4, harness.Renderers.Count);
        Assert.Same(same, owner.Pages[^1]);
        Assert.Null(tab.Error);
    }

    [Fact]
    public void UnexpectedFactoryFailurePropagatesWithoutChangingRendererOwnership()
    {
        using var harness = new Harness();
        var tab = harness.Tab();
        var committed = harness.Complete(tab, "https://first.example/", "https://first.example/");
        var owner = Assert.Single(harness.Renderers);
        var page = harness.Controller.Page(tab.Id);
        var failure = new FormatException("Unexpected factory bug.");
        harness.FactoryFailure = failure;

        var thrown = Assert.Throws<FormatException>(() =>
            harness.Complete(tab, "https://second.example/", "https://second.example/"));

        Assert.Same(failure, thrown);
        Assert.Same(owner, Assert.Single(harness.Renderers));
        Assert.Equal(0, owner.Disposals);
        Assert.Same(committed, Assert.Single(owner.Pages));
        Assert.Equal(committed.DocumentId, Assert.Single(owner.Committed));
        Assert.Same(page, harness.Controller.Page(tab.Id));
        Assert.Same(committed.Origin, tab.Origin);
        Assert.Equal("https://first.example/", Assert.Single(tab.History.Entries).Href);
        Assert.Null(tab.Error);

        harness.FactoryFailure = null;
        var same = harness.Complete(tab, "https://first.example/next", "https://first.example/next");
        Assert.Same(owner, Assert.Single(harness.Renderers));
        Assert.Same(same, owner.Pages[^1]);
        Assert.Equal(0, owner.Disposals);
    }

    [Fact]
    public void HistoryCommitFailureDisposesCandidateBeforePromotion()
    {
        using var harness = new Harness();
        var tab = harness.Tab();
        harness.Complete(tab, "https://a.example/", "https://a.example/");
        harness.Complete(tab, "https://a.example/b", "https://a.example/b");
        var current = harness.Complete(tab, "https://a.example/c", "https://a.example/c");
        harness.Controller.Back(tab.Id);
        var back = harness.Finish(tab, "https://a.example/b");
        var owner = Assert.Single(harness.Renderers);
        var page = harness.Controller.Page(tab.Id);
        harness.Controller.Forward(tab.Id);
        // Shrink history outside the controller so the traversal commit throws after the candidate commit.
        tab.History.Commit(current.Url, 0);
        tab.History.Commit(BrowserUrl.Parse("https://a.example/d"));
        var request = harness.SourceOf(tab).Requests[^1];
        request.Completion.SetResult(new(BrowserUrl.Parse("https://redirected.example/c"), "", 200, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Controller.Pump(_ => Viewport));
        var candidate = harness.Renderers[1];
        Assert.Single(candidate.Committed);
        Assert.Equal(1, candidate.Disposals);
        Assert.Equal(0, owner.Disposals);
        Assert.Same(page, harness.Controller.Page(tab.Id));
        Assert.Same(back.Origin, tab.Origin);
        Assert.Equal(4, owner.Pages.Count);
        Assert.Empty(candidate.RetainedPages);
    }

    [Fact]
    public void CanceledAndSupersededCandidatesNeverPromoteOrContaminateTheReusedRenderer()
    {
        using var harness = new Harness();
        var tab = harness.Tab();
        var committed = harness.Complete(tab, "https://first.example/", "https://first.example/");
        var owner = harness.Renderers[0];
        harness.NextRenderer = () => new FakeRenderer { Async = true };

        harness.Controller.Navigate(tab.Id, "https://second.example/");
        harness.Finish(tab, "https://second.example/");
        var stale = harness.Renderers[1];
        Assert.Single(stale.Pending);
        Assert.Equal(0, stale.Disposals);
        // A superseding same-origin navigation reuses the committed renderer, not the canceled candidate.
        harness.NextRenderer = null;
        harness.Controller.Navigate(tab.Id, "https://first.example/again");
        Assert.True(stale.Tokens[0].IsCancellationRequested);
        var same = harness.Finish(tab, "https://first.example/again");
        Assert.Same(same, owner.Pages[^1]);
        Assert.Equal(2, owner.Pages.Count);
        stale.Pending[0].SetResult(Blank(Viewport, "late"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(1, stale.Disposals);
        Assert.Empty(stale.Committed);
        Assert.Same(same.Origin, tab.Origin);
        Assert.Equal("Renderer 0", tab.Title);

        // Overlapping cross-origin candidates: only the current one may promote.
        harness.NextRenderer = () => new FakeRenderer { Async = true };
        harness.Controller.Navigate(tab.Id, "https://third.example/");
        var third = harness.Finish(tab, "https://third.example/");
        harness.Controller.Navigate(tab.Id, "https://fourth.example/");
        var fourth = harness.Finish(tab, "https://fourth.example/");
        var thirdRenderer = harness.Renderers[2]; var fourthRenderer = harness.Renderers[3];
        Assert.Same(third, Assert.Single(thirdRenderer.Pages));
        Assert.Same(fourth, Assert.Single(fourthRenderer.Pages));
        fourthRenderer.Pending[0].SetResult(Blank(Viewport, "fourth"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Same(fourth.Origin, tab.Origin);
        Assert.Equal(1, owner.Disposals);
        Assert.Equal(0, thirdRenderer.Disposals);
        thirdRenderer.Pending[0].SetException(new PageNavigationException("late stale failure"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(1, thirdRenderer.Disposals);
        Assert.Empty(thirdRenderer.Committed);
        Assert.Null(tab.Error);
        Assert.Same(fourth.Origin, tab.Origin);
        Assert.Equal("fourth", tab.Title);
        Assert.Equal(0, fourthRenderer.Disposals);
        Assert.Equal(new[] { "https://first.example/", "https://first.example/again", "https://fourth.example/" },
            tab.History.Entries.Select(url => url.Href));
        Assert.Same(committed, owner.Pages[0]);
    }

    [Fact]
    public void CanceledFirstRenderTaintsThePristineRendererForLaterDocuments()
    {
        using var harness = new Harness();
        harness.NextRenderer = () => new FakeRenderer { Async = true };
        var tab = harness.Tab();
        harness.Controller.Navigate(tab.Id, "https://first.example/");
        harness.Finish(tab, "https://first.example/");
        var pristine = harness.Renderers[0];
        Assert.Single(pristine.Pending);
        harness.Controller.Navigate(tab.Id, "https://first.example/again");
        var document = harness.Finish(tab, "https://first.example/again");
        var candidate = harness.Renderers[1];
        Assert.Same(document, Assert.Single(candidate.Pages));
        Assert.Single(pristine.Pages);
        candidate.Pending[0].SetResult(Blank(Viewport, "candidate"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(1, pristine.Disposals);
        Assert.Same(document.Origin, tab.Origin);
        pristine.Pending[0].SetResult(Blank(Viewport, "late"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal("candidate", tab.Title);
        Assert.Empty(pristine.Committed);
    }

    [Fact]
    public void ResizeUsesRetainedCommittedRendererWithoutRotationOrRerun()
    {
        using var harness = new Harness();
        var tab = harness.Tab();
        harness.Complete(tab, "https://first.example/", "https://first.example/");
        var rotated = harness.Complete(tab, "https://second.example/", "https://second.example/");
        var owner = harness.Renderers[1];
        harness.Controller.Resize(tab.Id, new(30, 20, 1));
        harness.Controller.Resize(tab.Id, new(35, 20, 1));
        Assert.Equal(2, harness.Renderers.Count);
        Assert.Equal(new[] { rotated, rotated }, owner.RetainedPages);
        Assert.Single(owner.Pages);
        Assert.Empty(harness.Renderers[0].RetainedPages);
        Assert.Equal(2, harness.SourceOf(tab).Requests.Count);

        harness.NextRenderer = () => new FakeRenderer { Async = true };
        harness.Controller.Navigate(tab.Id, "https://third.example/");
        harness.Finish(tab, "https://third.example/");
        harness.Controller.Resize(tab.Id, new(50, 20, 1));
        Assert.Equal(2, owner.RetainedPages.Count);
        Assert.Empty(harness.Renderers[2].RetainedPages);
        harness.Renderers[2].Pending[0].SetException(new PageNavigationException("candidate failed"));
        harness.Controller.Pump(_ => Viewport);
        harness.Controller.Resize(tab.Id, new(60, 20, 1));
        Assert.Equal(3, owner.RetainedPages.Count);
        Assert.Same(rotated, owner.RetainedPages[^1]);
        Assert.Equal(3, harness.Renderers.Count);
        Assert.Empty(harness.Renderers[2].RetainedPages);
    }

    [Fact]
    public void CandidateFailureNotificationsDoNotEraseCommittedOwnershipAndCrashRecoveryReusesOwner()
    {
        using var harness = new Harness();
        var tab = harness.Tab();
        harness.Complete(tab, "https://first.example/", "https://first.example/");
        var old = harness.Renderers[0];
        harness.NextRenderer = () => new FakeRenderer { Async = true, Failure = "candidate crashed early" };
        harness.Controller.Navigate(tab.Id, "https://second.example/");
        harness.Finish(tab, "https://second.example/");
        var candidate = harness.Renderers[1];
        harness.Controller.Pump(_ => Viewport);
        Assert.Null(tab.Error);
        candidate.Failure = null;
        candidate.Pending[0].SetResult(Blank(Viewport, "second"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(1, old.Disposals);
        old.Failure = "disposed renderer exited";
        harness.Controller.Pump(_ => Viewport);
        Assert.Null(tab.Error);
        candidate.Failure = "promoted renderer exited";
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal("promoted renderer exited", tab.Error);
        var page = harness.Controller.Page(tab.Id);
        Assert.NotNull(page);
        harness.Controller.Reload(tab.Id);
        harness.Finish(tab, "https://second.example/");
        candidate.Pending[^1].SetResult(Blank(Viewport, "reloaded"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(2, harness.Renderers.Count);
        Assert.Equal(2, candidate.Pages.Count);
        Assert.Null(tab.Error);
        Assert.Equal("reloaded", tab.Title);
    }

    [Fact]
    public void ClosingTabsWindowsAndControllerDisposesPendingCandidatesOnce()
    {
        var harness = new Harness();
        var window = harness.Controller.Session.CreateWindow();
        var first = harness.Tab(window.Id);
        var second = harness.Tab(window.Id);
        harness.Complete(first, "https://a.example/", "https://a.example/");
        harness.Complete(second, "https://a.example/", "https://a.example/");
        harness.NextRenderer = () => new FakeRenderer { Async = true };
        harness.Controller.Navigate(first.Id, "https://b.example/");
        harness.Finish(first, "https://b.example/");
        var firstCandidate = harness.Renderers[2];
        harness.Controller.CloseTab(first.Id);
        Assert.Equal(1, firstCandidate.Disposals);
        Assert.Equal(1, harness.Renderers[0].Disposals);
        Assert.Equal(0, harness.Renderers[1].Disposals);
        firstCandidate.Pending[0].SetResult(Blank(Viewport, "late"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(1, firstCandidate.Disposals);

        harness.Controller.Navigate(second.Id, "https://c.example/");
        harness.Finish(second, "https://c.example/");
        var secondCandidate = harness.Renderers[3];
        harness.Controller.CloseWindow(window.Id);
        Assert.Equal(1, secondCandidate.Disposals);
        Assert.Equal(1, harness.Renderers[1].Disposals);
        secondCandidate.Pending[0].SetException(new PageNavigationException("late"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(1, secondCandidate.Disposals);

        var third = harness.Tab();
        harness.Complete(third, "https://a.example/", "https://a.example/");
        harness.Controller.Navigate(third.Id, "https://d.example/");
        harness.Finish(third, "https://d.example/");
        var thirdCandidate = harness.Renderers[5];
        harness.Dispose();
        Assert.Equal(1, thirdCandidate.Disposals);
        Assert.Equal(1, harness.Renderers[4].Disposals);
        thirdCandidate.Pending[0].SetResult(Blank(Viewport, "late"));
        Assert.All(harness.Renderers, renderer => Assert.Equal(1, renderer.Disposals));
        Assert.All(new[] { firstCandidate, secondCandidate, thirdCandidate }, renderer => Assert.Empty(renderer.Committed));
    }

    [Fact]
    public void ConcurrentTabsAndMovedTabsRotateOnlyTheirOwnRenderers()
    {
        using var harness = new Harness(options: new() { MaxPendingLoads = 2 });
        var left = harness.Controller.Session.CreateWindow();
        var right = harness.Controller.Session.CreateWindow();
        var first = harness.Tab(left.Id);
        var second = harness.Tab(left.Id);
        harness.Complete(first, "https://a.example/", "https://a.example/");
        harness.Complete(second, "https://a.example/", "https://a.example/");
        harness.NextRenderer = () => new FakeRenderer { Async = true };
        harness.Controller.Navigate(first.Id, "https://b.example/");
        harness.Controller.Navigate(second.Id, "https://c.example/");
        Assert.Throws<BrowserLimitException>(() => harness.Controller.Navigate(first.Id, "https://e.example/"));
        harness.SourceOf(first).Requests[^1].Completion.SetResult(new(BrowserUrl.Parse("https://b.example/"), "", 200, []));
        harness.SourceOf(second).Requests[^1].Completion.SetResult(new(BrowserUrl.Parse("https://c.example/"), "", 200, []));
        harness.Controller.Pump(_ => Viewport);
        harness.Controller.Session.MoveTab(first.Id, right.Id);
        var firstCandidate = harness.Renderers[2];
        var secondCandidate = harness.Renderers[3];
        secondCandidate.Pending[0].SetResult(Blank(Viewport, "c"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(1, harness.Renderers[1].Disposals);
        Assert.Equal(0, harness.Renderers[0].Disposals);
        Assert.Equal("https://a.example", first.Origin!.Serialize());
        Assert.Equal("https://c.example", second.Origin!.Serialize());
        firstCandidate.Pending[0].SetResult(Blank(Viewport, "b"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Equal(1, harness.Renderers[0].Disposals);
        Assert.Equal(0, firstCandidate.Disposals);
        Assert.Equal(0, secondCandidate.Disposals);
        Assert.Equal("b", first.Title);
        Assert.Equal("c", second.Title);
        Assert.Contains(right.Tabs, tab => tab.Id == first.Id);
        harness.NextRenderer = null;
        harness.Controller.Resize(first.Id, new(30, 20, 1));
        firstCandidate.Pending[^1].SetResult(Blank(new(30, 20, 1), "b"));
        harness.Controller.Pump(_ => Viewport);
        Assert.Single(firstCandidate.RetainedPages);
        Assert.Empty(secondCandidate.RetainedPages);
        Assert.Equal(4, harness.Renderers.Count);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        [SuppressMessage("Sonar", "S1144:Unused private types or members should be removed", Justification = "HttpClient dispatches this deterministic response fixture through the HttpMessageHandler override.")]
        [SuppressMessage("Sonar", "S1172:Unused method parameters should be removed", Justification = "HttpMessageHandler requires a cancellation token; this deterministic fixture completes synchronously.")]
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
    private static HttpResponseMessage Html(HttpRequestMessage request, string html) => new(HttpStatusCode.OK)
    {
        RequestMessage = request,
        Content = new StringContent(html, Encoding.UTF8, "text/html")
    };

    [Fact]
    public void ActualProcessRenderersSwapForCrossOriginAndStayForSameOrigin()
    {
        const string blue = "<!doctype html><title>Blue</title><style>body{margin:0;background-color:blue}</style>";
        const string red = "<!doctype html><title>Red</title><style>body{margin:0;background-color:red}</style>";
        var renderers = new List<ProcessPageRenderer>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
            request.RequestUri!.Host switch
            {
                "first.test" when request.RequestUri.AbsolutePath == "/redirect" => new(HttpStatusCode.Found)
                {
                    RequestMessage = request,
                    Headers = { Location = new Uri("http://second.test/landing") }
                },
                "first.test" => Html(request, blue),
                "second.test" => Html(request, red),
                _ => Html(request, "<!doctype html><table></table>")
            })), () =>
            {
                var renderer = new ProcessPageRenderer(RendererPath, FontPath);
                renderers.Add(renderer);
                return renderer;
            }, isolateOrigins: true);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        Run("http://first.test/a");
        Assert.Equal("Blue", tab.Title);
        var firstPid = renderers[0].ProcessId!.Value;
        Assert.NotEqual(Environment.ProcessId, firstPid);
        AssertRendererHost(firstPid);
        Run("http://first.test/b");
        Assert.Single(renderers);
        Assert.Equal(firstPid, renderers[0].ProcessId);

        Run("http://first.test/redirect");
        Assert.Equal("http://second.test/landing", tab.History.Current!.Href);
        Assert.Equal("http://second.test", tab.Origin!.Serialize());
        Assert.Equal(2, renderers.Count);
        var secondPid = renderers[1].ProcessId!.Value;
        Assert.NotEqual(firstPid, secondPid);
        AssertRendererHost(secondPid);
        Assert.Null(renderers[0].ProcessId);
        AssertExited(firstPid);
        Assert.Equal("Red", tab.Title);
        var redPage = controller.Page(tab.Id)!;
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, redPage.Frame.Pixels.Span[..4].ToArray());

        Run("http://third.test/unsupported");
        Assert.NotNull(tab.Error);
        Assert.Equal(3, renderers.Count);
        Assert.Null(renderers[2].ProcessId);
        Assert.Same(redPage, controller.Page(tab.Id));
        Assert.Equal(secondPid, renderers[1].ProcessId);
        Assert.Equal("http://second.test", tab.Origin!.Serialize());
        Assert.Equal("Red", tab.Title);

        controller.Resize(tab.Id, new(30, 10, 1));
        PumpUntil(() => controller.Page(tab.Id)!.Frame.Size.Width == 30);
        Assert.Equal(secondPid, renderers[1].ProcessId);
        Assert.Equal(3, renderers.Count);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, controller.Page(tab.Id)!.Frame.Pixels.Span[..4].ToArray());

        void Run(string address)
        {
            controller.Navigate(tab.Id, address);
            PumpUntil(() => !tab.IsLoading);
        }
        void PumpUntil(Func<bool> complete)
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Cancellation.ThrowIfCancellationRequested();
                controller.Pump(_ => new(20, 10, 1));
                if (complete()) { return; }
                Thread.Sleep(5);
            } while (timer.Elapsed < TimeSpan.FromSeconds(30));
            Assert.Fail("Process navigation timed out.");
        }
    }
    private static void AssertRendererHost(int pid)
    {
        if (!OperatingSystem.IsLinux()) { return; }
        var arguments = File.ReadAllText($"/proc/{pid}/cmdline").Split('\0');
        Assert.Contains(RendererPath, arguments);
        Assert.Contains("--development-unsandboxed", arguments);
    }
    private static void AssertExited(int pid)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited) { return; }
            }
            catch (ArgumentException) { return; }
            Thread.Sleep(10);
        }
        Assert.Fail($"Previous renderer process {pid} is still running.");
    }
}
