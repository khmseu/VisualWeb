using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ButtonClickEventTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly PageViewport Viewport = new(200, 200, 1);
    private static IPageRenderer Renderer(bool process, bool scripts) => process
        ? new ProcessPageRenderer(RendererPath, FontPath, executeInlineScripts: scripts)
        : new StaticPageRenderer(FontPath, 100000, scripts);
    private static LoadedPage Document(string html) => new(BrowserUrl.Parse("https://example.com/start"), html, 200, []);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void TypeButtonDispatchesOneTrustedClickAndRepaints(bool process, bool input, bool keyboard)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process, true));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/start");
        var button = input ? "<input id=b type=button name=ignored value=Push>" : "<button id=b type=button name=ignored>Push</button>";
        source.Requests[0].Completion.SetResult(Document($$"""
            <!doctype html><title>Start</title><style>*{margin:0}</style>
            <p id=p>{{button}}</p><form action="/save"><input name=q value=a><button name=go value=yes>Save</button></form>
            <script>
            let count=0; const b=document.getElementById('b');
            b.addEventListener('click',e=>{count++; e.preventDefault();
                document.title=count+':'+e.type+':'+e.isTrusted+':'+e.bubbles+':'+e.cancelable+':'+(e.target===b)+':'+(e.constructor===Event);
                queueMicrotask(()=>document.getElementById('p').setAttribute('data-clicked','yes'));
            });
            document.getElementById('p').addEventListener('click',()=>document.title+=':bubble');
            </script>
            """));
        PumpUntil(controller, () => !tab.IsLoading && controller.Page(tab.Id) is not null);
        var original = controller.Page(tab.Id)!;
        var control = original.FormControls[0];
        if (keyboard)
        {
            controller.FocusPage(tab.Id);
            Assert.True(controller.FocusControl(tab.Id, 0));
            Assert.True(controller.ActivateFocusedLink(tab.Id)); // Enter
        }
        else
        {
            Assert.True(controller.ActivateLink(tab.Id, control.Rect!.X + 1, control.Rect.Y + 1, Viewport, dispatchClickEvent: true));
        }
        PumpUntil(controller, () => tab.Title.StartsWith("1:click", StringComparison.Ordinal) || tab.Error is not null);
        Assert.Null(tab.Error);
        Assert.Equal("1:click:true:true:true:true:true:bubble", tab.Title);
        Assert.NotSame(original, controller.Page(tab.Id));
        Assert.Single(source.Requests);
        Assert.Single(tab.History.Entries);
        Assert.True(controller.ActivateFocusedButton(tab.Id)); // Space
        PumpUntil(controller, () => tab.Title.StartsWith("2:click", StringComparison.Ordinal) || tab.Error is not null);
        Assert.Null(tab.Error);
        Assert.Equal("2:click:true:true:true:true:true:bubble", tab.Title);
        Assert.Single(source.Requests);
        Assert.True(controller.FocusControl(tab.Id, 2));
        Assert.True(controller.ActivateFocusedLink(tab.Id));
        Assert.Equal("https://example.com/save?q=a&go=yes", source.Requests[^1].Url.Href);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RendererRejectsArbitraryAndStaleButtonTargets(bool process)
    {
        using var renderer = Renderer(process, true);
        var document = Document("""
            <!doctype html><button id=b type=button>Push</button><input id=x><a href="/next">link</a>
            <script>document.getElementById('b').addEventListener('focus',()=>document.getElementById('b').setAttribute('disabled',''))</script>
            """);
        var page = await renderer.RenderAsync(document, Viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        var button = page.FormControls[0].FocusTargetId;
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.DispatchButtonClickAsync(Guid.NewGuid(), button, Cancellation));
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.DispatchButtonClickAsync(document.DocumentId, page.FormControls[1].FocusTargetId, Cancellation));
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.DispatchButtonClickAsync(document.DocumentId, page.LinkTargets[0].FocusTargetId, Cancellation));
        await renderer.DispatchPageFocusEventAsync(document.DocumentId, button, PageFocusEventType.Focus, Cancellation);
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.DispatchButtonClickAsync(document.DocumentId, button, Cancellation));
    }

    [Theory]
    [InlineData(false, "disabled")]
    [InlineData(true, "disabled")]
    [InlineData(false, "type")]
    [InlineData(true, "type")]
    [InlineData(false, "remove")]
    [InlineData(true, "remove")]
    [InlineData(false, "fieldset")]
    [InlineData(true, "fieldset")]
    public async Task RendererRejectsButtonChangedAfterRender(bool process, string mutation)
    {
        var action = mutation switch
        {
            "disabled" => "b.setAttribute('disabled','')",
            "type" => "b.setAttribute('type','submit')",
            "remove" => "b.remove()",
            _ => "document.getElementById('f').setAttribute('disabled','')"
        };
        using var renderer = Renderer(process, true);
        var document = Document($$"""
            <!doctype html><fieldset id=f><button id=b type=button>Push</button></fieldset>
            <script>const b=document.getElementById('b'); b.addEventListener('focus',()=>{{action}});</script>
            """);
        var page = await renderer.RenderAsync(document, Viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        var target = Assert.Single(page.FormControls).FocusTargetId;
        await renderer.DispatchPageFocusEventAsync(document.DocumentId, target, PageFocusEventType.Focus, Cancellation);
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.DispatchButtonClickAsync(document.DocumentId, target, Cancellation));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FocusEventsPrecedeClickAndFailureKeepsCommittedFrame(bool process, bool fail)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process, true));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/start");
        source.Requests[0].Completion.SetResult(Document($$"""
            <!doctype html><title>Start</title><style>*{margin:0}</style><input id=a><button id=b type=button>Push</button>
            <script>
            let log=''; const a=document.getElementById('a'), b=document.getElementById('b');
            a.addEventListener('blur',()=>log+='blur,');
            b.addEventListener('focus',()=>log+='focus,');
            b.addEventListener('click',()=>{document.title=log+'click'; {{(fail ? "throw Error('boom')" : "")}}});
            </script>
            """));
        PumpUntil(controller, () => !tab.IsLoading && controller.Page(tab.Id) is not null);
        var initial = controller.Page(tab.Id)!;
        Assert.True(controller.FocusControl(tab.Id, 0));
        PumpUntil(controller, () => !ReferenceEquals(initial, controller.Page(tab.Id)) || tab.Error is not null);
        Assert.Null(tab.Error);
        var original = controller.Page(tab.Id)!;
        var rect = original.FormControls[1].Rect!;
        Assert.True(controller.ActivateLink(tab.Id, rect.X + 1, rect.Y + 1, Viewport, dispatchClickEvent: true));
        PumpUntil(controller, () => tab.Error is not null || tab.Title == "blur,focus,click");
        if (fail)
        {
            Assert.StartsWith("Page click event failed:", tab.Error);
            Assert.Same(original, controller.Page(tab.Id));
            Assert.Equal("Start", tab.Title);
        }
        else
        {
            Assert.Null(tab.Error);
            Assert.Equal("blur,focus,click", tab.Title);
        }
        Assert.Single(source.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScriptDisabledAndDisabledControlsDoNotDispatch(bool process)
    {
        using var renderer = Renderer(process, false);
        var document = Document("<!doctype html><button type=button>Push</button><button type=button disabled>No</button>");
        var page = await renderer.RenderAsync(document, Viewport, Cancellation);
        renderer.CommitDocument(document.DocumentId);
        Assert.False(renderer.DispatchesLinkClicks);
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.DispatchButtonClickAsync(document.DocumentId,
            page.FormControls[0].FocusTargetId, Cancellation));
        Assert.True(page.FormControls[1].Disabled);
        Assert.Equal(0, page.FormControls[1].FocusTargetId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ListenedButtonInsideFormNeverSubmitsOrSerializes(bool process, bool input)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process, true));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        controller.Navigate(tab.Id, "https://example.com/start");
        var button = input ? "<input id=b type=button name=ignored value=Push>" : "<button id=b type=button name=ignored>Push</button>";
        source.Requests[0].Completion.SetResult(Document($$"""
            <!doctype html><title>Start</title><style>*{margin:0}</style>
            <form action="/save"><input name=q value=a>{{button}}<button name=go value=yes>Save</button></form>
            <script>document.getElementById('b').addEventListener('click',()=>document.title='clicked');</script>
            """));
        PumpUntil(controller, () => !tab.IsLoading && controller.Page(tab.Id) is not null);
        var rect = controller.Page(tab.Id)!.FormControls[1].Rect!;
        Assert.True(controller.ActivateLink(tab.Id, rect.X + 1, rect.Y + 1, Viewport, dispatchClickEvent: true));
        PumpUntil(controller, () => tab.Title == "clicked" || tab.Error is not null);
        Assert.Null(tab.Error);
        Assert.Single(source.Requests);
        Assert.True(controller.FocusControl(tab.Id, 2));
        Assert.True(controller.ActivateFocusedLink(tab.Id));
        Assert.Equal("https://example.com/save?q=a&go=yes", source.Requests[^1].Url.Href);
    }

    private static void PumpUntil(BrowserController controller, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            controller.Pump(_ => Viewport);
            if (condition()) { return; }
            Thread.Sleep(5);
        } while (DateTime.UtcNow < deadline);
        Assert.True(condition());
    }
}
