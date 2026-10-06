using VisualWeb.Browser;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class SessionTests
{
    private static BrowserUrl Url(string path) => BrowserUrl.Parse("https://example.com/" + path);
    [Fact]
    public void HistoryCommitsBranchingAndRedirectedTraversalTransactionally()
    {
        var history = new NavigationHistory();
        Assert.False(history.CanGoBack);
        Assert.Null(history.Current);
        history.Commit(Url("a"));
        history.Commit(Url("b"));
        history.Commit(Url("c"));
        history.Commit(Url("redirected-b"), traversalIndex: 1);
        Assert.Equal(1, history.Index);
        Assert.True(history.CanGoBack);
        Assert.True(history.CanGoForward);
        Assert.Equal(Url("redirected-b").Href, history.Current!.Href);
        history.Commit(Url("d"));
        Assert.Equal(new[] { "a", "redirected-b", "d" }, history.Entries.Select(e => e.Pathname[1..]));
        Assert.False(history.CanGoForward);
        history.Commit(Url("reloaded"), replace: true);
        Assert.Equal(3, history.Entries.Count);
        Assert.Equal(Url("reloaded").Href, history.Current!.Href);
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Commit(Url("bad"), traversalIndex: 5));
        Assert.Equal(2, history.Index);
    }
    [Fact]
    public void HistoryLimitEvictsOldestEntryExactly()
    {
        var history = new NavigationHistory(2);
        history.Commit(Url("a")); history.Commit(Url("b"));
        Assert.Equal(2, history.Entries.Count);
        history.Commit(Url("c"));
        Assert.Equal(new[] { "/b", "/c" }, history.Entries.Select(e => e.Pathname));
        Assert.Equal(1, history.Index);
    }
    [Fact]
    public void TabMoveRetainsIdentityHistoryAndWindowSeparation()
    {
        var session = new BrowserSession();
        var first = session.CreateWindow(); var second = session.CreateWindow();
        var a = session.CreateTab(first.Id); var b = session.CreateTab(first.Id);
        var c = session.CreateTab(second.Id);
        a.History.Commit(Url("a"));
        session.MoveTab(a.Id, second.Id);
        Assert.Same(a, session.Tab(a.Id));
        Assert.Equal(a.Id, second.ActiveTabId);
        Assert.Equal(b.Id, first.ActiveTabId);
        Assert.Equal("/a", a.History.Current!.Pathname);
        session.CloseTab(a.Id);
        Assert.Equal(c.Id, second.ActiveTabId);
        session.CloseTab(b.Id);
        Assert.Single(session.Windows);
        Assert.Same(second, session.Windows[0]);
        Assert.Throws<ArgumentException>(() => session.Activate(second.Id, b.Id));
    }
    [Fact]
    public void MovingLastTabClosesOnlySourceWindow()
    {
        var session = new BrowserSession();
        var a = session.CreateWindow(); var b = session.CreateWindow();
        var tab = session.CreateTab(a.Id);
        session.MoveTab(tab.Id, b.Id);
        Assert.Single(session.Windows);
        Assert.Same(b, session.Windows[0]);
        session.CloseWindow(b.Id);
        Assert.Empty(session.Windows);
    }
    [Fact]
    public void WindowTabAndOptionLimitsAreExact()
    {
        var session = new BrowserSession(new() { MaxWindows = 1, MaxTabs = 1 });
        var window = session.CreateWindow();
        session.CreateTab(window.Id);
        Assert.Throws<BrowserLimitException>(() => session.CreateWindow());
        Assert.Throws<BrowserLimitException>(() => session.CreateTab(window.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BrowserSession(new() { MaxHistoryEntries = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationHistory(0));
    }
    [Fact]
    public void AddressEditingSelectionCaretAndUnicodeBoundariesAreAtomic()
    {
        var editor = new AddressEditor();
        editor.Reset("abc", selectAll: true);
        Assert.Equal("xy", editor.Insert("xy", 3));
        editor.Left();
        Assert.Equal("x!y", editor.Insert("!", 3));
        Assert.Throws<BrowserLimitException>(() => editor.Insert("long", 3));
        Assert.Equal("x!y", editor.Text);
        Assert.Throws<PageNavigationException>(() => editor.Insert("\n", 20));
        Assert.Equal("xy", editor.Backspace());
        Assert.Equal("x", editor.Delete());
        editor.Reset("a\U0001F600b"); editor.Left();
        Assert.Equal("ab", editor.Backspace());
        editor.Home(); Assert.Equal("b", editor.Delete());
        editor.End(); Assert.Equal("", editor.Backspace());
    }
    [Theory]
    [InlineData("--font", "x")]
    [InlineData("--development-single-process")]
    [InlineData("--development-single-process", "--allow-unsandboxed-development", "--font", "x", "--backend", "dummy")]
    [InlineData("--development-single-process", "--allow-unsandboxed-development", "--font", "x", "--skip-text-input")]
    [InlineData("--development-single-process", "--allow-unsandboxed-development", "--font", "x", "--smoke", "--url", "https://example.com")]
    [InlineData("--development-single-process", "--allow-unsandboxed-development", "--font", "x", "--unknown")]
    [InlineData("--development-single-process", "--allow-unsandboxed-development", "--font", "x", "--font", "y")]
    public void UnsafeOrInvalidLaunchOptionsAreRejected(params string[] args) =>
        Assert.Throws<ArgumentException>(() => BrowserLaunchOptions.Parse(args));
    [Fact]
    public void ExplicitDevelopmentAndSmokeLaunchOptionsAreAccepted()
    {
        var options = BrowserLaunchOptions.Parse(["--development-single-process", "--allow-unsandboxed-development", "--font", "font.ttf",
            "--smoke", "--backend", "dummy", "--skip-text-input"]);
        Assert.True(options.Smoke);
        Assert.True(options.SkipTextInput);
        Assert.True(Path.IsPathFullyQualified(options.FontPath));
    }
}
