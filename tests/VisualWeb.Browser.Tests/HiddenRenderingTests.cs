using VisualWeb.Core.Url;
using VisualWeb.Engine.Layout;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class HiddenRenderingTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static LoadedPage Document(string html) => new(BrowserUrl.Parse("https://example.test/page"), html, 200, []);
    private static IPageRenderer Renderer(bool process, bool scripts = false) => process
        ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: scripts)
        : new StaticPageRenderer(FontPath, 100000, executeInlineScripts: scripts);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenElementsHaveNoPaintLinkSelectionFocusOrFragmentGeometryAcrossRetainedReflow(bool process)
    {
        var actual = Document(Markup("hidden", "hidden"));
        var reference = Document(Markup("style='display:none'", "style='display:none'"));
        var narrow = new PageViewport(160, 140, 1);
        var wide = new PageViewport(220, 140, 1);
        BrowserPage expectedNarrow, expectedWide;
        using (var renderer = new StaticPageRenderer(FontPath, 100000))
        {
            expectedNarrow = renderer.Render(reference, narrow, Cancellation);
            renderer.CommitDocument(reference.DocumentId);
            expectedWide = renderer.Render(reference, wide, Cancellation, reuseDocument: true);
        }

        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.test/page");
        source.Requests[0].Completion.SetResult(actual);
        PumpUntil(controller, tab, narrow, () => controller.Page(tab.Id) is not null);
        var first = controller.Page(tab.Id)!;
        AssertEquivalent(expectedNarrow, first);
        Assert.Equal(["https://example.test/page#missing", "https://example.test/shown"],
            first.LinkTargets.Select(link => link.Url));
        Assert.Equal(2, first.FormControls.Count);
        Assert.All(first.FormControls, control =>
        {
            Assert.Null(control.Rect);
            Assert.Equal(0, control.FocusTargetId);
        });
        Assert.DoesNotContain(first.FragmentTargets, target => target.Id is "hidden-parent" or "hidden-child" or "legacy");
        Assert.Contains(first.FragmentTargets, target => target.Id == "override");
        Assert.False(controller.FocusControl(tab.Id, 0));
        Assert.False(controller.FocusControl(tab.Id, 1));
        Assert.Equal(2, controller.PageFocusCount(tab.Id));
        controller.FocusPage(tab.Id);
        Assert.Equal(0, controller.MoveLinkFocus(tab.Id));
        Assert.Equal(1, controller.MoveLinkFocus(tab.Id));
        Assert.Equal(0, controller.MoveLinkFocus(tab.Id));
        Assert.True(controller.SelectAllVisibleText(tab.Id));
        Assert.Contains("Jump", controller.SelectedText(tab.Id), StringComparison.Ordinal);
        Assert.Contains("Shown", controller.SelectedText(tab.Id), StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", controller.SelectedText(tab.Id), StringComparison.Ordinal);

        controller.Resize(tab.Id, wide);
        PumpUntil(controller, tab, wide, () => !ReferenceEquals(first, controller.Page(tab.Id)));
        AssertEquivalent(expectedWide, controller.Page(tab.Id)!);
        Assert.Single(source.Requests);

        static string Markup(string parent, string input) => """
            <!doctype html><style hidden>*{margin:0}body{font-size:20px;line-height:1}
            input{width:60px;height:20px}#gap{height:20px}</style>
            <a href="#missing">Jump</a>
            """ + "<div id=hidden-parent " + parent + "><a id=hidden-child name=legacy href=/secret>Secret</a>"
            + "<input name=nested value=inside></div><input name=direct value=one " + input + "> "
            + "<a id=override hidden style='display:inline' href=/shown>Shown</a>"
            + "<div id=gap></div><div id=end>End</div>";

        static void AssertEquivalent(BrowserPage expected, BrowserPage actual)
        {
            Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
            Assert.Equal(expected.TextTargets.ToArray(), actual.TextTargets.ToArray());
            Assert.Equal(expected.LinkTargets.SelectMany(link => link.Rects).ToArray(),
                actual.LinkTargets.SelectMany(link => link.Rects).ToArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenControlsAndSelectedOptionsRemainSuccessfulOnSubmission(bool process)
    {
        var page = Document("""
            <!doctype html><style hidden>*{margin:0}input,button,select{width:80px;height:20px}</style>
            <form action=/send><input hidden name=a value=x>
            <div hidden><input name=b value=y></div>
            <select hidden name=c><option hidden selected value=z>Secret</option><option value=q>Other</option></select>
            <input name=shown value=ok><button name=send value=yes>Send</button></form>
            """);
        var viewport = new PageViewport(300, 100, 1);
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.test/page");
        source.Requests[0].Completion.SetResult(page);
        PumpUntil(controller, tab, viewport, () => controller.Page(tab.Id) is not null);
        var rendered = controller.Page(tab.Id)!;
        Assert.Equal(5, rendered.FormControls.Count);
        Assert.All(rendered.FormControls.Take(3), control => Assert.Null(control.Rect));
        Assert.Equal("z", rendered.FormControls[2].Value);
        Assert.True(rendered.FormControls[2].Options[0].Selected);
        Assert.False(rendered.FormControls[2].Options[0].Disabled);
        Assert.False(controller.FocusControl(tab.Id, 0));
        Assert.False(controller.FocusControl(tab.Id, 1));
        Assert.False(controller.FocusControl(tab.Id, 2));
        Assert.Equal(2, controller.PageFocusCount(tab.Id));
        Assert.True(controller.FocusControl(tab.Id, 4));
        Assert.True(controller.ActivateFocusedLink(tab.Id, viewport));
        Assert.Equal("https://example.test/send?a=x&b=y&c=z&shown=ok&send=yes", source.Requests[^1].Url.Href);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtomicButtonLabelsFilterHiddenDescendantsEvenOffscreenAndHonorOverrides(bool process)
    {
        var page = Document("""
            <!doctype html><style>*{margin:0}button{width:120px;height:20px}#gap{height:100px}</style>
            <button>Shown<span hidden>Secret</span><span hidden style="display:inline"> Override</span><span style="display:none">Other</span><span hidden=until-found style="display:none">Later</span></button>
            <div id=gap></div><button>Later<span hidden>Secret</span></button>
            """);
        using var renderer = Renderer(process);
        var initial = await renderer.RenderAsync(page, new(200, 40, 1), Cancellation);
        Assert.Equal(2, initial.FormControls.Count);
        Assert.Equal("Shown Override", initial.FormControls[0].Label);
        Assert.Equal("Later", initial.FormControls[1].Label);
        Assert.Null(initial.FormControls[1].Rect);
        var plain = Document("""
            <!doctype html><style>*{margin:0}button{width:120px;height:20px}#gap{height:100px}</style>
            <button>Shown Override</button><div id=gap></div><button>Later</button>
            """);
        BrowserPage reference;
        using (var local = new StaticPageRenderer(FontPath, 100000))
        { reference = local.Render(plain, new(200, 40, 1), Cancellation); }
        var session = new BrowserSession();
        var window = session.CreateWindow();
        session.CreateTab(window.Id);
        using (var chrome = new ShellChrome(FontPath, 100000))
        {
            var size = new PixelSize(200, 160);
            var state = new ShellFormState(["", ""], -1, -1);
            var actualPixels = chrome.Render(window, initial, size, 1, forms: state).Pixels;
            var referencePixels = chrome.Render(window, reference, size, 1, forms: state).Pixels;
            var leaked = initial with
            {
                FormControls = [initial.FormControls[0] with { Label = "ShownSecret OverrideOtherLater" }, initial.FormControls[1]]
            };
            Assert.Equal(referencePixels, actualPixels);
            Assert.NotEqual(chrome.Render(window, leaked, size, 1, forms: state).Pixels, actualPixels);
        }
        renderer.CommitDocument(page.DocumentId);
        var scrolled = await renderer.RenderRetainedAsync(page, new PageViewport(200, 40, 1) { ScrollY = 100 }, Cancellation);
        Assert.Equal(initial.FormControls.Select(control => control.Label), scrolled.FormControls.Select(control => control.Label));
        Assert.NotNull(scrolled.FormControls[1].Rect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverriddenOptionsKeepNormalSelectednessAndValues(bool process)
    {
        var page = Document("""
            <!doctype html><style>*{margin:0}</style><form>
            <select name=q><option hidden style="display:inline" value=one>One</option>
            <optgroup hidden style="display:block" label=group><option value=two selected>Two</option></optgroup></select>
            </form>
            """);
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(page, new(200, 100, 1), Cancellation);
        var control = Assert.Single(rendered.FormControls);
        Assert.Equal("two", control.Value);
        Assert.Equal(["one", "two"], control.Options.Select(option => option.Value));
        Assert.Equal(["One", "group: Two"], control.Options.Select(option => option.Label));
        Assert.Equal([false, true], control.Options.Select(option => option.Selected));
        Assert.All(control.Options, option => Assert.False(option.Disabled));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VisibleAndOffscreenUnsupportedWidgetContentFailsClosed(bool process)
    {
        using var renderer = Renderer(process);
        foreach (var (html, reason) in new[]
        {
            ("<button>Shown<span hidden=until-found>Secret</span></button>", "until-found"),
            ("<div style='height:100px'></div><button><span hidden=until-found>Secret</span></button>", "until-found"),
            ("<select><option hidden selected value=secret>Secret</option></select>", "select"),
            ("<div style='height:100px'></div><select><optgroup hidden label=group><option>Secret</option></optgroup></select>", "select"),
            ("<button>Shown<embed hidden></button>", "<embed>"),
        })
        {
            var page = Document("<!doctype html><style>*{margin:0}</style>" + html);
            var exception = process
                ? (Exception)await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(page, new(200, 40, 1), Cancellation))
                : await Assert.ThrowsAsync<UnsupportedLayoutException>(() => renderer.RenderAsync(page, new(200, 40, 1), Cancellation));
            Assert.Contains(reason, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HiddenStylesAndScriptsStillProcessMutationsOnceAcrossRetainedReflow(bool process)
    {
        const string style = "*{margin:0}body{font-size:20px;line-height:1;color:#008000}";
        var actual = Document("""
            <!doctype html><title>before</title><style hidden>*{margin:0}body{font-size:20px;line-height:1;color:#008000}</style>
            <a id=first hidden href=/first>First</a> <a id=second href=/second>Second</a>
            <script hidden>
            let a=document.getElementById('first'),b=document.getElementById('second');
            if(a.hasAttribute('hidden')){a.removeAttribute('hidden');b.setAttribute('hidden','');document.title='changed'}
            else{a.setAttribute('hidden','');b.removeAttribute('hidden');document.title='reran'}
            </script>
            <script hidden=until-found>if(document.title!=='changed')throw Error('script was skipped')</script>
            """);
        var reference = Document("<!doctype html><title>changed</title><style>" + style
            + "</style><a id=first href=/first>First</a> <a id=second hidden href=/second>Second</a>");
        var narrow = new PageViewport(160, 60, 1);
        var wide = new PageViewport(200, 60, 1);
        BrowserPage expected, expectedRetained;
        using (var renderer = new StaticPageRenderer(FontPath, 100000))
        {
            expected = renderer.Render(reference, narrow, Cancellation);
            renderer.CommitDocument(reference.DocumentId);
            expectedRetained = renderer.Render(reference, wide, Cancellation, reuseDocument: true);
        }
        using var actualRenderer = Renderer(process, scripts: true);
        var rendered = await actualRenderer.RenderAsync(actual, narrow, Cancellation);
        AssertMutation(expected, rendered);
        actualRenderer.CommitDocument(actual.DocumentId);
        var retained = await actualRenderer.RenderRetainedAsync(actual, wide, Cancellation);
        AssertMutation(expectedRetained, retained);

        static void AssertMutation(BrowserPage expected, BrowserPage actual)
        {
            Assert.Equal("changed", actual.Title);
            Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
            Assert.Equal(expected.TextTargets.ToArray(), actual.TextTargets.ToArray());
            Assert.Equal("https://example.test/first", Assert.Single(actual.LinkTargets).Url);
            Assert.DoesNotContain(actual.FragmentTargets, target => target.Id == "second");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScriptedButtonDescendantMutationRecomputesLabelAndSurvivesResize(bool process)
    {
        var page = Document("""
            <!doctype html><title>before</title><style>*{margin:0}button{width:120px;height:20px}</style>
            <button name=send value=yes>Shown<span id=first hidden> reveal</span><span id=second> secret</span></button>
            <script hidden>
            let first=document.getElementById('first'), second=document.getElementById('second');
            if(first.hasAttribute('hidden')) {
                first.removeAttribute('hidden'); second.setAttribute('hidden',''); document.title='once';
            } else {
                first.setAttribute('hidden',''); second.removeAttribute('hidden'); document.title='reran';
            }
            </script>
            """);
        using var renderer = Renderer(process, scripts: true);
        var rendered = await renderer.RenderAsync(page, new(160, 50, 1), Cancellation);
        var button = Assert.Single(rendered.FormControls);
        Assert.Equal("once", rendered.Title);
        Assert.Equal("Shown reveal", button.Label);
        Assert.Equal("yes", button.Value);
        renderer.CommitDocument(page.DocumentId);
        var retained = await renderer.RenderRetainedAsync(page, new(200, 50, 1), Cancellation);
        Assert.Equal("once", retained.Title);
        Assert.Equal(button.Label, Assert.Single(retained.FormControls).Label);
        Assert.Equal(button.Value, Assert.Single(retained.FormControls).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HiddenLinkedStylesheetIsStillDiscoveredAndApplied(bool process)
    {
        const string css = "body{margin:0;background-color:blue}";
        var page = Document("<!doctype html><link hidden rel=stylesheet href=/hidden.css><div>Text</div>") with
        {
            Stylesheets = [new PageStylesheet("https://example.test/hidden.css", css)]
        };
        Assert.Equal(["https://example.test/hidden.css"],
            StaticPageRenderer.DiscoverStylesheets(page.Html, page.Url, Cancellation));
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(page, new(100, 40, 1), Cancellation);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rendered.Frame.Pixels.Span[..4].ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenFirstIdAndNameReserveFragmentIdentityWithoutScrollingToDuplicates(bool process)
    {
        var page = Document("""
            <!doctype html><style>*{margin:0}#space{height:180px}.box{height:30px}</style>
            <a href="#x">Jump</a> <a href="#y">Next</a> <a href="#z">Last</a>
            <div hidden id=x>Hidden</div><div class=box id=x>Duplicate</div>
            <a name=y>Visible name</a><div hidden id=y>Hidden ID</div>
            <a hidden name=z>Hidden name</a> <a name=z>Duplicate name</a>
            <div style="display:none" id=styled>Hidden</div><div class=box id=styled>Duplicate</div>
            <div class=box id=first>First</div><div hidden id=first>Later</div>
            <div id=space></div><div id=end>End</div>
            """);
        var viewport = new PageViewport(200, 60, 1);
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.test/page");
        source.Requests[0].Completion.SetResult(page);
        PumpUntil(controller, tab, viewport, () => controller.Page(tab.Id) is not null);
        var rendered = controller.Page(tab.Id)!;
        Assert.DoesNotContain(rendered.FragmentTargets, target => target.Id is "x" or "y" or "z" or "styled");
        Assert.Contains(rendered.FragmentTargets, target => target.Id == "first");
        Assert.Contains(rendered.FragmentTargets, target => target.Id == "end");

        controller.Scroll(tab.Id, 25);
        PumpUntil(controller, tab, viewport, () => !ReferenceEquals(rendered, controller.Page(tab.Id)));
        var before = controller.ScrollY(tab.Id);
        Assert.True(before > 0);
        var fragments = new[] { "x", "y", "z" };
        for (var index = 0; index < fragments.Length; index++)
        {
            controller.FocusLink(tab.Id, index);
            Assert.True(controller.ActivateFocusedLink(tab.Id, viewport));
            Assert.Equal(before, controller.ScrollY(tab.Id));
            Assert.Equal("https://example.test/page#" + fragments[index], tab.History.Current!.Href);
        }
        Assert.Single(source.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FragmentIdentityUsesDomOrderEvenWhenTableLayoutReordersRows(bool process)
    {
        var page = Document("""
            <!doctype html><style>*{margin:0}table{border-spacing:0}td,th{padding:0}</style>
            <table><tbody><tr><td id=duplicate>Body</td></tr></tbody>
            <thead><tr><th id=duplicate>Head</th></tr></thead></table>
            """);
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(page, new(200, 100, 1), Cancellation);
        var target = Assert.Single(rendered.FragmentTargets, item => item.Id == "duplicate");
        var body = Assert.Single(rendered.TextTargets, item => item.Text == "B");
        var head = Assert.Single(rendered.TextTargets, item => item.Text == "H");
        Assert.True(body.Rect.Y > head.Rect.Y);
        Assert.Equal(body.Rect.Y, target.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FragmentReservationPreservesExactTargetLengthLimitAndIgnoresUnrenderedOnlyIds(bool process)
    {
        var exact = new string('x', RendererProtocol.MaxTextCharacters);
        using var renderer = Renderer(process);
        var blocked = Document("<!doctype html><style>*{margin:0}</style><div hidden id='" + exact
            + "'></div><div id='" + exact + "'>Visible</div><div hidden id='" + exact + "x'></div>");
        var rendered = await renderer.RenderAsync(blocked, new(200, 100, 1), Cancellation);
        Assert.DoesNotContain(rendered.FragmentTargets, target => target.Id == exact);
        var tooLong = Document("<!doctype html><div id='" + exact + "x'>Visible</div>");
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(tooLong, new(200, 100, 1), Cancellation));
    }

    private static void PumpUntil(BrowserController controller, BrowserTab tab, PageViewport viewport, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            Cancellation.ThrowIfCancellationRequested();
            controller.Pump(_ => viewport);
            if (condition()) { return; }
            if (!tab.IsLoading && tab.Error is not null) { Assert.Fail(tab.Error); }
            Thread.Sleep(5);
        } while (DateTime.UtcNow < deadline);
        Assert.Null(tab.Error);
        Assert.True(condition());
    }
}
