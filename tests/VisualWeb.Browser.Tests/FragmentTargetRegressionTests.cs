using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Text;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class FragmentTargetRegressionTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static LoadedPage Document(string html) => new(BrowserUrl.Parse("https://example.test/page"), html, 200, []);
    private static IPageRenderer Renderer(bool process) => process
        ? new ProcessPageRenderer(RendererPath, FontPath)
        : new StaticPageRenderer(FontPath, 100000);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineWidgetIdsAndAncestorsUseWidgetGeometryAndOverrideLegacyNames(bool process)
    {
        var page = Document("""
            <!doctype html><style>
            *{margin:0}body{font-size:12px;line-height:1}
            input,button,textarea,select{width:50px;height:20px}
            </style>
            <div><a name=widget-input>Legacy</a></div><div style="height:25px"></div>
            <div><span id=ancestor-input><input id=widget-input name=input></span></div>
            <div><span id=ancestor-button><button id=widget-button name=button>Button</button></span></div>
            <div><span id=ancestor-textarea><textarea id=widget-textarea name=textarea>Text</textarea></span></div>
            <div><a name=legacy-widget><span id=ancestor-select><select id=widget-select name=select><option>Option</option></select></span></a></div>
            <div><span id=short-ancestor><input id=short name=short style="height:5px"></span><span style="font-size:30px">Tall</span></div>
            """);
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(page, new(200, 160, 1), Cancellation);
        foreach (var kind in new[] { "input", "button", "textarea", "select" })
        {
            var control = Assert.Single(rendered.FormControls, item => item.Name == kind);
            Assert.NotNull(control.Rect);
            var widget = Assert.Single(rendered.FragmentTargets, item => item.Id == "widget-" + kind);
            var ancestor = Assert.Single(rendered.FragmentTargets, item => item.Id == "ancestor-" + kind);
            Assert.Equal(control.Rect.Y, widget.Y);
            Assert.Equal(control.Rect.Y, ancestor.Y);
        }
        var legacyText = Assert.Single(rendered.TextTargets, item => item.Text == "L");
        Assert.True(rendered.FragmentTargets.Single(item => item.Id == "widget-input").Y > legacyText.Rect.Y);
        var select = Assert.Single(rendered.FormControls, item => item.Name == "select");
        Assert.Equal(select.Rect!.Y, Assert.Single(rendered.FragmentTargets, item => item.Id == "legacy-widget").Y);
        var shortWidget = Assert.Single(rendered.FormControls, item => item.Name == "short");
        Assert.NotNull(shortWidget.Rect);
        var tallText = Assert.Single(rendered.TextTargets, item => item.Text == "T");
        Assert.True(shortWidget.Rect.Y > tallText.Rect.Y);
        Assert.Equal(shortWidget.Rect.Y, Assert.Single(rendered.FragmentTargets, item => item.Id == "short").Y);
        Assert.Equal(shortWidget.Rect.Y, Assert.Single(rendered.FragmentTargets, item => item.Id == "short-ancestor").Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HiddenFirstWinnersBlockWidgetIdsAndNamesButVisibleIdsBeatHiddenNames(bool process)
    {
        var page = Document("""
            <!doctype html><style>*{margin:0}input{width:50px;height:20px}</style>
            <div hidden id=shadow></div>
            <div><span id=visible-ancestor><input id=shadow name=shown></span></div>
            <div><a name=shadow>Fallback</a></div>
            <input hidden id=hidden-widget name=hidden-control>
            <div><a name=hidden-widget>Fallback</a></div>
            <a hidden name=hidden-name></a><div><a name=hidden-name>Fallback</a></div>
            <a hidden name=widget-wins></a>
            <div><span id=winning-ancestor><input id=widget-wins name=winner></span></div>
            """);
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(page, new(200, 160, 1), Cancellation);
        Assert.DoesNotContain(rendered.FragmentTargets, target => target.Id is "shadow" or "hidden-widget" or "hidden-name");
        var shown = Assert.Single(rendered.FormControls, item => item.Name == "shown");
        Assert.NotNull(shown.Rect);
        Assert.Equal(shown.Rect.Y, Assert.Single(rendered.FragmentTargets, item => item.Id == "visible-ancestor").Y);
        var winner = Assert.Single(rendered.FormControls, item => item.Name == "winner");
        Assert.NotNull(winner.Rect);
        Assert.Equal(winner.Rect.Y, Assert.Single(rendered.FragmentTargets, item => item.Id == "widget-wins").Y);
        Assert.Equal(winner.Rect.Y, Assert.Single(rendered.FragmentTargets, item => item.Id == "winning-ancestor").Y);
        Assert.Null(Assert.Single(rendered.FormControls, item => item.Name == "hidden-control").Rect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameDocumentFragmentNavigationScrollsToInlineInput(bool process)
    {
        var source = new ControllerTests.Source();
        using var controller = new BrowserController(() => source, () => Renderer(process));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(160, 50, 1);
        controller.Navigate(tab.Id, "https://example.test/page");
        source.Requests[0].Completion.SetResult(Document("""
            <!doctype html><style>*{margin:0}input{width:50px;height:20px}</style>
            <a href="#field">Jump</a><div style="height:100px"></div>
            <span><input id=field name=field></span><div style="height:100px"></div>
            """));
        PumpUntil(controller, tab, viewport, () => controller.Page(tab.Id) is not null);
        var rendered = controller.Page(tab.Id)!;
        var target = Assert.Single(rendered.FragmentTargets, item => item.Id == "field");
        Assert.True(target.Y > 0);
        controller.FocusLink(tab.Id, 0);
        Assert.True(controller.ActivateFocusedLink(tab.Id, viewport));
        Assert.Equal(Math.Min(target.Y, rendered.ScrollHeight - viewport.Height), controller.ScrollY(tab.Id));
        Assert.Single(source.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverlappingIdsAndNamesUseExactEmittedCountLimit(bool process)
    {
        using var renderer = Renderer(process);
        var ordinary = await renderer.RenderAsync(Document(OverlappingAnchors(2049)), new(100, 40, 1), Cancellation);
        Assert.Equal(2049, ordinary.FragmentTargets.Count);
        var exact = await renderer.RenderAsync(Document(OverlappingAnchors(RendererProtocol.MaxFragmentTargets)),
            new(100, 40, 1), Cancellation);
        Assert.Equal(RendererProtocol.MaxFragmentTargets, exact.FragmentTargets.Count);
        Assert.Equal(RendererProtocol.MaxFragmentTargets, exact.FragmentTargets.Select(target => target.Id).Distinct().Count());
        var exception = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Document(OverlappingAnchors(RendererProtocol.MaxFragmentTargets + 1)), new(100, 40, 1), Cancellation));
        Assert.Contains("fragment target", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShadowedIdAndNameCandidatesDoNotConsumeEmittedCountBudget(bool process)
    {
        var markup = new StringBuilder("<!doctype html><style>*{margin:0}a:not([hidden]){display:block;height:1px}</style>");
        for (var index = 0; index < 2048; index++)
        {
            markup.Append("<div hidden id=i").Append(index).Append("></div><a id=i").Append(index)
                .Append(" name=i").Append(index).Append("></a>");
        }
        for (var index = 0; index < 2048; index++)
        {
            markup.Append("<a hidden name=n").Append(index).Append("></a><a name=n").Append(index).Append("></a>");
        }
        markup.Append("<a id=survivor name=survivor></a>");
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(Document(markup.ToString()), new(100, 40, 1), Cancellation);
        Assert.Equal("survivor", Assert.Single(rendered.FragmentTargets).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverlappingIdsAndNamesUseExactSerializedByteLimit(bool process)
    {
        var exactTargets = ExactByteTargets();
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes, JsonSerializer.SerializeToUtf8Bytes(exactTargets).Length);
        using var renderer = Renderer(process);
        var exact = await renderer.RenderAsync(Document(AnchorMarkup(exactTargets)), new(100, 40, 1), Cancellation);
        Assert.Equal(exactTargets, exact.FragmentTargets);
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes,
            JsonSerializer.SerializeToUtf8Bytes(exact.FragmentTargets).Length);

        var overTargets = exactTargets.ToArray();
        overTargets[^1] = overTargets[^1] with { Id = overTargets[^1].Id + "x" };
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes + 1, JsonSerializer.SerializeToUtf8Bytes(overTargets).Length);
        var exception = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Document(AnchorMarkup(overTargets)), new(100, 40, 1), Cancellation));
        Assert.Contains("fragment target", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShadowedCandidatesDoNotConsumeUtf8ByteBudget(bool process)
    {
        var markup = new StringBuilder("<!doctype html><style>*{margin:0}a:not([hidden]){display:block;height:1px}</style>");
        const int candidates = 128;
        for (var index = 0; index < candidates; index++)
        {
            var prefix = index.ToString("D4", CultureInfo.InvariantCulture);
            var id = prefix + new string('x', RendererProtocol.MaxTextCharacters - prefix.Length);
            markup.Append("<div hidden id='").Append(id).Append("'></div><a name='").Append(id).Append("'></a>");
        }
        markup.Append("<div hidden id=q></div><a name=q></a>");
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes + 1,
            candidates * RendererProtocol.MaxTextCharacters + 1);
        markup.Append("<a id=survivor name=survivor></a>");
        Assert.True(markup.Length < RendererProtocol.MaxHtmlCharacters);
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(Document(markup.ToString()), new(100, 40, 1), Cancellation);
        Assert.Equal("survivor", Assert.Single(rendered.FragmentTargets).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShadowedOverlongIdsAndNamesDoNotConsumeTextLengthBudget(bool process)
    {
        var markup = new StringBuilder("<!doctype html><style>*{margin:0}a:not([hidden]){display:block;height:1px}</style>");
        var oversizedId = new string('y', RendererProtocol.MaxTextCharacters + 1);
        var oversizedName = new string('z', RendererProtocol.MaxTextCharacters + 1);
        markup.Append("<div hidden id='").Append(oversizedId).Append("'></div><a id='").Append(oversizedId)
            .Append("' name='").Append(oversizedId).Append("'></a>");
        markup.Append("<a hidden name='").Append(oversizedName).Append("'></a><a name='").Append(oversizedName).Append("'></a>");
        markup.Append("<a id=survivor name=survivor></a>");
        Assert.True(markup.Length < RendererProtocol.MaxHtmlCharacters);
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(Document(markup.ToString()), new(100, 40, 1), Cancellation);
        Assert.Equal("survivor", Assert.Single(rendered.FragmentTargets).Id);
        var exception = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Document("<!doctype html><div id='" + oversizedId + "'>Visible</div>"), new(100, 40, 1), Cancellation));
        Assert.Contains("fragment target", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmittedUnicodeIdsRespectSerializedJsonByteLimit(bool process)
    {
        var targets = Enumerable.Range(0, 22).Select(index =>
        {
            var prefix = index.ToString("D4", CultureInfo.InvariantCulture);
            return new PageFragmentTarget(prefix + new string('\u00e9', RendererProtocol.MaxTextCharacters - prefix.Length), index);
        }).ToArray();
        Assert.True(targets.Sum(target => Encoding.UTF8.GetByteCount(target.Id)) < RendererProtocol.MaxFragmentMetadataBytes);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(targets).Length > RendererProtocol.MaxFragmentMetadataBytes);
        using var renderer = Renderer(process);
        var exception = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Document(AnchorMarkup(targets)), new(100, 40, 1), Cancellation));
        Assert.Contains("Serialized JSON bytes.", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmittedUtf8BytesUseTheirOwnLimitDiagnostic(bool process)
    {
        var targets = Enumerable.Range(0, 128).Select(index =>
        {
            var prefix = index.ToString("D4", CultureInfo.InvariantCulture);
            return new PageFragmentTarget(prefix + new string('x', RendererProtocol.MaxTextCharacters - prefix.Length), index);
        }).Append(new PageFragmentTarget("q", 128)).ToArray();
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes + 1,
            targets.Sum(target => Encoding.UTF8.GetByteCount(target.Id)));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(targets).Length > RendererProtocol.MaxFragmentMetadataBytes);
        using var renderer = Renderer(process);
        var exception = await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(
            Document(AnchorMarkup(targets)), new(100, 40, 1), Cancellation));
        Assert.Contains("UTF-8 bytes.", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeepSharedTextAncestorsAndOffscreenWidgetsRetainTargets(bool process)
    {
        var markup = new StringBuilder("""
            <!doctype html><style>*{margin:0}body{font-size:12px;line-height:1}input{width:20px;height:10px}</style>
            <div hidden id=shadow></div><div style="width:80px"><span id=outer>
            """);
        const int depth = 60;
        for (var index = 0; index < depth; index++) { markup.Append("<span>"); }
        markup.Append("<span id=deep>");
        for (var index = 0; index < 80; index++) { markup.Append("word "); }
        markup.Append("<input id=late name=late> <input id=shadow name=shadow></span>");
        for (var index = 0; index < depth; index++) { markup.Append("</span>"); }
        markup.Append("</span></div>");
        using var renderer = Renderer(process);
        var rendered = await renderer.RenderAsync(Document(markup.ToString()), new(120, 70, 1), Cancellation);
        var outer = Assert.Single(rendered.FragmentTargets, target => target.Id == "outer");
        Assert.Equal(outer.Y, Assert.Single(rendered.FragmentTargets, target => target.Id == "deep").Y);
        Assert.True(Assert.Single(rendered.FragmentTargets, target => target.Id == "late").Y > outer.Y);
        Assert.DoesNotContain(rendered.FragmentTargets, target => target.Id == "shadow");
        Assert.Null(Assert.Single(rendered.FormControls, control => control.Name == "late").Rect);
    }

    [Fact]
    public void IdentityWorkHasIndependentExactNodeElementTextAndCancellationLimits()
    {
        var document = new DomDocument();
        var root = document.CreateElement("html");
        root.SetAttribute("id", "ab");
        document.AppendChild(root);
        var child = document.CreateElement("div");
        child.SetAttribute("id", "xy");
        root.AppendChild(child);
        var rectangle = new LayoutRect(0, 0, 1, 1);
        var childBox = new LayoutBox(child, rectangle, rectangle, rectangle, new(), [], []);
        var rootBox = new LayoutBox(root, rectangle, rectangle, rectangle, new(), [childBox], [])
        { Flow = [new LayoutBlockItem(childBox)] };
        var layout = new LayoutResult(rootBox, 100, 100);
        var method = typeof(StaticPageRenderer).GetMethod("CollectFragmentTargets", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        IReadOnlyList<PageFragmentTarget> Collect(long nodes, int elements, long characters, CancellationToken cancellation) =>
            Assert.IsAssignableFrom<IReadOnlyList<PageFragmentTarget>>(method.Invoke(
                null, [document, layout, 100d, nodes, elements, characters, 1000L, cancellation]));

        Assert.Equal(["ab", "xy"], Collect(2, 2, 12, Cancellation).Select(target => target.Id));
        var nodeFailure = Assert.Throws<TargetInvocationException>(() => Collect(1, 2, 12, Cancellation));
        Assert.Contains("identity node", Assert.IsType<PageNavigationException>(nodeFailure.InnerException).Message);
        var elementFailure = Assert.Throws<TargetInvocationException>(() => Collect(2, 1, 12, Cancellation));
        Assert.Contains("identity element", Assert.IsType<PageNavigationException>(elementFailure.InnerException).Message);
        var textFailure = Assert.Throws<TargetInvocationException>(() => Collect(2, 2, 11, Cancellation));
        Assert.Contains("identity text", Assert.IsType<PageNavigationException>(textFailure.InnerException).Message);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var cancellationFailure = Assert.Throws<TargetInvocationException>(() => Collect(2, 2, 12, canceled.Token));
        Assert.IsAssignableFrom<OperationCanceledException>(cancellationFailure.InnerException);
    }

    [Fact]
    public void SharedFragmentAncestorsHaveLinearExactWorkAndPreserveFirstGeometry()
    {
        const int depth = 96;
        const int fragmentCount = 300;
        const int widgetCount = 300;
        var document = new DomDocument();
        var root = document.CreateElement("html");
        root.SetAttribute("id", "root");
        document.AppendChild(root);
        var hidden = document.CreateElement("div");
        hidden.SetAttribute("hidden", "");
        hidden.SetAttribute("id", "shadow");
        root.AppendChild(hidden);
        var parent = root;
        for (var index = 1; index < depth; index++)
        {
            var ancestor = document.CreateElement(index == 1 ? "a" : "span");
            if (index == 1) { ancestor.SetAttribute("name", "widget-target"); }
            if (index == 2) { ancestor.SetAttribute("id", "outer"); }
            parent.AppendChild(ancestor);
            parent = ancestor;
        }
        parent.SetAttribute("id", "boxed");
        var text = document.CreateTextNode("x");
        parent.AppendChild(text);
        var fragment = new LayoutTextFragment(text, new ShapedRun("x", "test", 1, [], 1, new(1, 0, 0)),
            0, 30, null!);
        var fragments = Enumerable.Repeat(fragment, fragmentCount).ToArray();
        var widgets = new LayoutInlineWidget[widgetCount];
        for (var index = 0; index < widgets.Length; index++)
        {
            var widget = document.CreateElement("input");
            if (index == 0) { widget.SetAttribute("id", "widget-target"); }
            if (index == 1) { widget.SetAttribute("id", "shadow"); }
            parent.AppendChild(widget);
            widgets[index] = new(widget, new(0, 50, 1, 1));
        }
        var line = new LayoutLine(new(0, 30, 100, 1), 30, fragments) { Widgets = widgets };
        var rectangle = new LayoutRect(0, 5, 1, 1);
        var box = new LayoutBox(parent, rectangle, rectangle, rectangle, new(), [], [line])
        { Flow = [new LayoutLineItem(line)] };
        var layout = new LayoutResult(box, 100, 100);
        var method = typeof(StaticPageRenderer).GetMethod("CollectFragmentTargets", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var nodes = depth + widgetCount + 2L;
        var elements = depth + widgetCount;
        // Per pass: depth ancestors and the document, then one visit per additional
        // fragment and two visits per distinct widget; both passes share the cap.
        var exactVisits = 2L * (depth + fragmentCount + 2 * widgetCount);

        IReadOnlyList<PageFragmentTarget> Collect(long ancestorVisits, CancellationToken cancellation)
        {
            object?[] arguments = [document, layout, 100d, nodes, elements, 100000L, ancestorVisits, cancellation];
            return Assert.IsAssignableFrom<IReadOnlyList<PageFragmentTarget>>(method.Invoke(null, arguments));
        }

        var targets = Collect(exactVisits, Cancellation);
        Assert.Equal(30, Assert.Single(targets, target => target.Id == "root").Y);
        Assert.Equal(30, Assert.Single(targets, target => target.Id == "outer").Y);
        Assert.Equal(5, Assert.Single(targets, target => target.Id == "boxed").Y);
        Assert.Equal(50, Assert.Single(targets, target => target.Id == "widget-target").Y);
        Assert.DoesNotContain(targets, target => target.Id == "shadow");
        var failure = Assert.Throws<TargetInvocationException>(() => Collect(exactVisits - 1, Cancellation));
        Assert.Contains("ancestor", Assert.IsType<PageNavigationException>(failure.InnerException).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedAncestorTraversalObservesCancellationInEachLayoutPass(bool secondPass)
    {
        var document = new DomDocument();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var span = document.CreateElement("span");
        span.SetAttribute("id", "target");
        root.AppendChild(span);
        var text = document.CreateTextNode("x");
        span.AppendChild(text);
        var fragment = new LayoutTextFragment(text, new ShapedRun("x", "test", 1, [], 1, new(1, 0, 0)),
            0, 0, null!);
        using var canceled = new CancellationTokenSource();
        var fragments = new CancelingList<LayoutTextFragment>([fragment, fragment], canceled, secondPass);
        var rectangle = new LayoutRect(0, 0, 1, 1);
        var line = new LayoutLine(rectangle, 0, fragments);
        var box = new LayoutBox(root, rectangle, rectangle, rectangle, new(), [], [line])
        { Flow = [new LayoutLineItem(line)] };
        var layout = new LayoutResult(box, 100, 100);
        var method = typeof(StaticPageRenderer).GetMethod("CollectFragmentTargets", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        object?[] arguments = [document, layout, 100d, 3L, 2, 1000L, 1000L, canceled.Token];
        var failure = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, arguments));
        Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
    }

    private sealed class CancelingList<T>(IReadOnlyList<T> items, CancellationTokenSource canceled, bool secondPass)
        : IReadOnlyList<T>
    {
        private int enumerations;
        public int Count => items.Count;
        public T this[int index] => items[index];
        public IEnumerator<T> GetEnumerator()
        {
            var pass = ++enumerations;
            for (var index = 0; index < items.Count; index++)
            {
                if (pass == (secondPass ? 2 : 1) && index == (secondPass ? 0 : 1)) { canceled.Cancel(); }
                yield return items[index];
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static string OverlappingAnchors(int count)
    {
        var markup = new StringBuilder("<!doctype html><style>*{margin:0}a{display:block;height:1px}</style>");
        for (var index = 0; index < count; index++)
        {
            markup.Append("<a id=x").Append(index).Append(" name=x").Append(index).Append("></a>");
        }
        return markup.ToString();
    }

    private static PageFragmentTarget[] ExactByteTargets()
    {
        var targets = Enumerable.Range(0, 128).Select(index => new PageFragmentTarget("", index)).ToArray();
        var remaining = RendererProtocol.MaxFragmentMetadataBytes - JsonSerializer.SerializeToUtf8Bytes(targets).Length;
        for (var index = 0; index < targets.Length; index++)
        {
            var length = Math.Min(RendererProtocol.MaxTextCharacters, remaining);
            var prefix = index.ToString("D4", CultureInfo.InvariantCulture);
            Assert.True(length > prefix.Length);
            targets[index] = targets[index] with { Id = prefix + new string('x', length - prefix.Length) };
            remaining -= length;
        }
        Assert.Equal(0, remaining);
        return targets;
    }

    private static string AnchorMarkup(IEnumerable<PageFragmentTarget> targets)
    {
        var markup = new StringBuilder("<!doctype html><style>*{margin:0}a{display:block;height:1px}</style>");
        foreach (var target in targets)
        {
            markup.Append("<a id='").Append(target.Id).Append("' name='").Append(target.Id).Append("'></a>");
        }
        return markup.ToString();
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
        Assert.Fail("Browser operation did not complete before the test deadline.");
    }
}
