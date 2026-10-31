using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class AtomicWidgetWrappingTests
{
    private const string WidgetCss = "input,button,textarea,select{display:inline-block;width:10px;height:10px}";
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static (DomDocument Document, CssStyleResult Styles) Page(string content, string css = "")
    {
        var document = HtmlParser.Parse("<!doctype html><p id=p>" + content + "</p>",
            cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("html,body,p{display:block}head{display:none}*{margin:0;font-size:10px;line-height:10px}"
                + WidgetCss + css)], includeUserAgent: false, cancellationToken: Cancellation);
        return (document, styles);
    }

    private static LayoutBox Layout(DomDocument document, CssStyleResult styles, double width,
        LayoutOptions? options = null)
    {
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), width, 200,
            options, Cancellation).Root!;
        return Find(root);
    }

    private static LayoutBox Find(LayoutBox root)
    {
        if (root.Element.GetAttribute("id") == "p") { return root; }
        foreach (var child in root.Children)
        {
            var found = TryFind(child);
            if (found is not null) { return found; }
        }
        throw new InvalidOperationException("Missing paragraph.");
    }

    private static LayoutBox? TryFind(LayoutBox root) => root.Element.GetAttribute("id") == "p" ? root
        : root.Children.Select(TryFind).FirstOrDefault(found => found is not null);

    private static string[] DescribeLines(LayoutBox box) => box.Lines.Select(line => string.Concat(
        line.Fragments.Select(fragment => (fragment.X, Text: fragment.Run.Text))
            .Concat(line.Widgets.Select(widget => (widget.Bounds.X, Text: "[" + widget.Element.GetAttribute("id") + "]")))
            .OrderBy(piece => piece.X).Select(piece => piece.Text))).ToArray();

    private static void AssertLineIdentityAndContiguousGeometry(LayoutBox box, double width)
    {
        Assert.Equal(box.Lines.Count, box.Flow.Count);
        var y = 0.0;
        for (var index = 0; index < box.Lines.Count; index++)
        {
            var line = box.Lines[index];
            Assert.Same(line, Assert.IsType<LayoutLineItem>(box.Flow[index]).Line);
            var height = line.Widgets.Count == 0 ? 10 : 12;
            Assert.Equal(new LayoutRect(0, y, width, height), line.Bounds);
            Assert.Equal(y + (line.Widgets.Count == 0 ? 8 : 10), line.Baseline);
            var pieces = line.Fragments.Select(fragment => (fragment.X, Width: fragment.Run.Width))
                .Concat(line.Widgets.Select(widget => (widget.Bounds.X, widget.Bounds.Width)))
                .OrderBy(piece => piece.X);
            var x = 0.0;
            foreach (var piece in pieces)
            {
                Assert.Equal(x, piece.X);
                x += piece.Width;
            }
            Assert.All(line.Fragments, fragment => Assert.Equal(line.Baseline, fragment.Baseline));
            Assert.All(line.Widgets, widget => Assert.Equal(new LayoutRect(widget.Bounds.X, y, 10, 10), widget.Bounds));
            y += height;
        }
        Assert.Equal(y, box.Content.Height);
    }

    public static TheoryData<string, bool, string, bool> BoundaryCases()
    {
        var data = new TheoryData<string, bool, string, bool>();
        foreach (var text in new[] { "-1", "1/2", "2026-10-31", "ordinary" })
        {
            foreach (var widgetFirst in new[] { true, false })
            {
                foreach (var mode in new[] { "normal", "pre-line", "nowrap", "pre" })
                {
                    data.Add(text, widgetFirst, mode, true);
                    data.Add(text, widgetFirst, mode, false);
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void AtomicWidgetBoundariesKeepUnbreakableTextAndGeometry(
        string text, bool widgetFirst, string mode, bool exactFit)
    {
        const string widget = "<input id=w>";
        var (document, styles) = Page(widgetFirst ? widget + text : text + widget, "#p{white-space:" + mode + "}");
        var paragraph = document.GetElementById("p")!;
        var source = Assert.Single(paragraph.ChildNodes.OfType<DomText>());
        var element = document.GetElementById("w")!;
        var width = 10 + text.Length * 5 - (exactFit ? 0 : 1);
        var box = Layout(document, styles, width);
        var wraps = !exactFit && mode is "normal" or "pre-line";

        Assert.Equal(wraps ? 2 : 1, box.Lines.Count);
        var textLine = wraps && widgetFirst ? 1 : 0;
        var widgetLine = wraps && !widgetFirst ? 1 : 0;
        var fragment = Assert.Single(box.Lines[textLine].Fragments);
        var placed = Assert.Single(box.Lines[widgetLine].Widgets);
        Assert.Same(source, fragment.Source);
        Assert.Same(element, placed.Element);
        Assert.Equal(text, fragment.Run.Text);
        Assert.Equal(text.Length * 5, fragment.Run.Width);
        Assert.Equal(!wraps && widgetFirst ? 10 : 0, fragment.X);
        Assert.Equal(!wraps && !widgetFirst ? text.Length * 5 : 0, placed.Bounds.X);
        AssertLineIdentityAndContiguousGeometry(box, width);
    }

    [Theory]
    [InlineData(true, "1/2")]
    [InlineData(false, "1/2")]
    [InlineData(true, "2026-10-31")]
    [InlineData(false, "2026-10-31")]
    public void AtomicWidgetBoundariesMoveEvenOverlongWordsToTheirOwnLine(bool widgetFirst, string text)
    {
        foreach (var mode in new[] { "normal", "pre-line" })
        {
            const string widget = "<input id=w>";
            var (document, styles) = Page(widgetFirst ? widget + text : text + widget, "#p{white-space:" + mode + "}");
            var box = Layout(document, styles, 5);
            Assert.Equal(widgetFirst ? new[] { "[w]", text } : new[] { text, "[w]" }, DescribeLines(box));
            Assert.Same(Assert.Single(document.GetElementById("p")!.ChildNodes.OfType<DomText>()),
                Assert.Single(box.Lines.SelectMany(line => line.Fragments)).Source);
            AssertLineIdentityAndContiguousGeometry(box, 5);
        }
    }

    [Theory]
    [InlineData("1-a", true, 25, "[w]1-a")]
    [InlineData("1-a", true, 24, "[w]1-|a")]
    [InlineData("1-a", true, 19, "[w]|1-a")]
    [InlineData("1-a", true, 10, "[w]|1-|a")]
    [InlineData("1-a", false, 25, "1-a[w]")]
    [InlineData("1-a", false, 24, "1-a|[w]")]
    [InlineData("1-a", false, 10, "1-|a|[w]")]
    [InlineData("a-1-b", true, 35, "[w]a-1-b")]
    [InlineData("a-1-b", true, 30, "[w]a-1-|b")]
    [InlineData("a-1-b", true, 29, "[w]|a-1-b")]
    [InlineData("a-1-b", false, 35, "a-1-b[w]")]
    [InlineData("a-1-b", false, 29, "a-1-b|[w]")]
    [InlineData("1/2-3-a/b", true, 39, "[w]|1/2-3-|a/b")]
    [InlineData("1/2-3-a/b", false, 50, "1/2-3-a/b|[w]")]
    public void AtomicWidgetBoundariesPreserveGreedyNumericHyphenBreaks(
        string text, bool widgetFirst, double width, string expected)
    {
        foreach (var mode in new[] { "normal", "pre-line" })
        {
            const string widget = "<input id=w>";
            var (document, styles) = Page(widgetFirst ? widget + text : text + widget, "#p{white-space:" + mode + "}");
            var box = Layout(document, styles, width);
            var source = Assert.Single(document.GetElementById("p")!.ChildNodes.OfType<DomText>());
            Assert.Equal(expected.Split('|'), DescribeLines(box));
            Assert.Equal(text, string.Concat(box.Lines.SelectMany(line => line.Fragments).Select(fragment => fragment.Run.Text)));
            Assert.All(box.Lines.SelectMany(line => line.Fragments), fragment => Assert.Same(source, fragment.Source));
            AssertLineIdentityAndContiguousGeometry(box, width);
        }
    }

    [Theory]
    [InlineData("nowrap", true)]
    [InlineData("nowrap", false)]
    [InlineData("pre", true)]
    [InlineData("pre", false)]
    public void AtomicWidgetBoundariesDoNotBreakMixedNumericTextInNonWrappingModes(string mode, bool widgetFirst)
    {
        var content = widgetFirst ? "<input id=w>a-1-b" : "a-1-b<input id=w>";
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}#w{white-space:normal}");
        var box = Layout(document, styles, 10);
        Assert.Equal(widgetFirst ? new[] { "[w]a-1-b" } : new[] { "a-1-b[w]" }, DescribeLines(box));
        Assert.Same(Assert.Single(document.GetElementById("p")!.ChildNodes.OfType<DomText>()),
            Assert.Single(Assert.Single(box.Lines).Fragments).Source);
        AssertLineIdentityAndContiguousGeometry(box, 10);
    }

    [Theory]
    [InlineData("normal", "nowrap", true, "[w]|1/2")]
    [InlineData("normal", "pre", false, "1/2|[w]")]
    [InlineData("pre-line", "pre-wrap", true, "[w]|1/2")]
    [InlineData("pre-line", "nowrap", false, "1/2|[w]")]
    [InlineData("nowrap", "normal", true, "[w]1/2")]
    [InlineData("nowrap", "pre-line", false, "1/2[w]")]
    [InlineData("pre", "normal", true, "[w]1/2")]
    [InlineData("pre", "pre-wrap", false, "1/2[w]")]
    public void AtomicWidgetBoundariesUseContainingWhitespaceNotWidgetWhitespace(
        string parentMode, string widgetMode, bool widgetFirst, string expected)
    {
        const string widget = "<span><input id=w></span>";
        var (document, styles) = Page(widgetFirst ? widget + "1/2" : "1/2" + widget,
            "#p{white-space:" + parentMode + "}#w{white-space:" + widgetMode + "}");
        var box = Layout(document, styles, 20);
        Assert.Equal(expected.Split('|'), DescribeLines(box));
        Assert.Same(Assert.Single(document.GetElementById("p")!.ChildNodes.OfType<DomText>()),
            Assert.Single(box.Lines.SelectMany(line => line.Fragments)).Source);
        Assert.Same(document.GetElementById("w"), Assert.Single(box.Lines.SelectMany(line => line.Widgets)).Element);
        AssertLineIdentityAndContiguousGeometry(box, 20);
    }

    [Theory]
    [InlineData("normal", 20, false)]
    [InlineData("normal", 19, true)]
    [InlineData("pre-line", 20, false)]
    [InlineData("pre-line", 19, true)]
    [InlineData("nowrap", 20, false)]
    [InlineData("nowrap", 19, false)]
    [InlineData("pre", 20, false)]
    [InlineData("pre", 19, false)]
    public void AtomicWidgetBoundariesBetweenAdjacentControlsHonorWhitespace(
        string mode, double width, bool wraps)
    {
        var (document, styles) = Page("<input id=a><input id=b>",
            "#p{white-space:" + mode + "}#a{white-space:nowrap}#b{white-space:normal}");
        var box = Layout(document, styles, width);
        Assert.Equal(wraps ? new[] { "[a]", "[b]" } : new[] { "[a][b]" }, DescribeLines(box));
        var widgets = box.Lines.SelectMany(line => line.Widgets).ToArray();
        Assert.Equal(2, widgets.Length);
        Assert.Same(document.GetElementById("a"), widgets[0].Element);
        Assert.Same(document.GetElementById("b"), widgets[1].Element);
        AssertLineIdentityAndContiguousGeometry(box, width);
    }

    [Theory]
    [InlineData("normal", 25, "[a] [b]")]
    [InlineData("normal", 24, "[a]|[b]")]
    [InlineData("pre-line", 24, "[a]|[b]")]
    [InlineData("nowrap", 24, "[a] [b]")]
    [InlineData("pre", 34, "[a]   [b]")]
    public void AtomicWidgetBoundariesBetweenControlsRetainWhitespace(
        string mode, double width, string expected)
    {
        var (document, styles) = Page("<input id=a>   <input id=b>", "#p{white-space:" + mode + "}");
        var box = Layout(document, styles, width);
        var source = Assert.Single(document.GetElementById("p")!.ChildNodes.OfType<DomText>());
        Assert.Equal(expected.Split('|'), DescribeLines(box));
        Assert.All(box.Lines.SelectMany(line => line.Fragments), fragment => Assert.Same(source, fragment.Source));
        AssertLineIdentityAndContiguousGeometry(box, width);
    }

    [Theory]
    [InlineData("<input id=w>")]
    [InlineData("<button id=w></button>")]
    [InlineData("<textarea id=w></textarea>")]
    [InlineData("<select id=w></select>")]
    public void AtomicWidgetBoundariesApplyToAllSupportedInlineFormControls(string widget)
    {
        foreach (var widgetFirst in new[] { true, false })
        {
            var (document, styles) = Page(widgetFirst ? widget + "-1" : "-1" + widget);
            var box = Layout(document, styles, 19);
            Assert.Equal(widgetFirst ? new[] { "[w]", "-1" } : new[] { "-1", "[w]" }, DescribeLines(box));
            Assert.Same(document.GetElementById("w"), Assert.Single(box.Lines.SelectMany(line => line.Widgets)).Element);
            AssertLineIdentityAndContiguousGeometry(box, 19);
        }
    }

    [Theory]
    [InlineData("<input id=w>")]
    [InlineData("<input id=a><input id=b>")]
    [InlineData("<span><input id=w></span>")]
    public void AtomicWidgetBoundariesRejectUnsupportedContainingWhitespaceWithoutText(string content)
    {
        var (document, styles) = Page(content, "#p{white-space:pre-wrap}input{white-space:normal}");
        Assert.Throws<UnsupportedLayoutException>(() => Layout(document, styles, 20));
    }

    [Theory]
    [InlineData("normal", true, 30, "[w] 1/2")]
    [InlineData("normal", true, 29, "[w]|1/2")]
    [InlineData("pre-line", false, 30, "1/2 [w]")]
    [InlineData("pre-line", false, 29, "1/2|[w]")]
    [InlineData("nowrap", true, 29, "[w] 1/2")]
    [InlineData("nowrap", false, 29, "1/2 [w]")]
    [InlineData("pre", true, 39, "[w]   1/2")]
    [InlineData("pre", false, 39, "1/2   [w]")]
    public void AtomicWidgetBoundariesRetainCollapsedAndPreservedSpaces(
        string mode, bool widgetFirst, double width, string expected)
    {
        var content = widgetFirst ? "<input id=w>   1/2" : "1/2   <input id=w>";
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}");
        var box = Layout(document, styles, width);
        var source = Assert.Single(document.GetElementById("p")!.ChildNodes.OfType<DomText>());
        Assert.Equal(expected.Split('|'), DescribeLines(box));
        Assert.All(box.Lines.SelectMany(line => line.Fragments), fragment => Assert.Same(source, fragment.Source));
        AssertLineIdentityAndContiguousGeometry(box, width);
    }

    [Theory]
    [InlineData("normal", "<input id=w>   <br>  1/2", "[w]|1/2")]
    [InlineData("nowrap", "1/2<br><input id=w>", "1/2|[w]")]
    [InlineData("pre-line", "<input id=w>   \n  1/2", "[w]|1/2")]
    [InlineData("pre-line", "1/2\n<input id=w>", "1/2|[w]")]
    [InlineData("pre", "<input id=w>\n1/2", "[w]|1/2")]
    [InlineData("pre", "1/2<br><input id=w>", "1/2|[w]")]
    public void AtomicWidgetBoundariesKeepForcedBreaksAndDiscardCollapsedLineEdges(
        string mode, string content, string expected)
    {
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}");
        var box = Layout(document, styles, 20);
        var source = Assert.Single(document.GetElementById("p")!.ChildNodes.OfType<DomText>(),
            text => text.Data.Contains("1/2", StringComparison.Ordinal));
        Assert.Equal(expected.Split('|'), DescribeLines(box));
        Assert.Same(source, Assert.Single(box.Lines.SelectMany(line => line.Fragments)).Source);
        Assert.Same(document.GetElementById("w"), Assert.Single(box.Lines.SelectMany(line => line.Widgets)).Element);
        AssertLineIdentityAndContiguousGeometry(box, 20);
    }

    [Theory]
    [InlineData("normal", true, "\u00a01/2")]
    [InlineData("normal", false, "1/2\u00a0")]
    [InlineData("pre-line", true, "\u00a01/2")]
    [InlineData("pre-line", false, "1/2\u00a0")]
    public void AtomicWidgetBoundariesRetainTheNoBreakSpaceCompatibilityException(
        string mode, bool widgetFirst, string text)
    {
        var content = widgetFirst ? "<input id=w>" + text : text + "<input id=w>";
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}");
        var box = Layout(document, styles, 29);
        Assert.Equal(widgetFirst ? new[] { "[w]", text } : new[] { text, "[w]" }, DescribeLines(box));
        Assert.Same(Assert.Single(document.GetElementById("p")!.ChildNodes.OfType<DomText>()),
            Assert.Single(box.Lines.SelectMany(line => line.Fragments)).Source);
        AssertLineIdentityAndContiguousGeometry(box, 29);
    }

    [Theory]
    [InlineData("normal", true, "\u035c1/2")]
    [InlineData("normal", false, "1/2\u035c")]
    [InlineData("pre-line", true, "\u03621/2")]
    [InlineData("pre-line", false, "1/2\u0362\u0301")]
    [InlineData("normal", true, "\u035c1-a")]
    [InlineData("pre-line", false, "1-a\u035c")]
    public void AtomicWidgetBoundariesRejectWrappingBesideSupportedGlue(string mode, bool widgetFirst, string text)
    {
        var content = widgetFirst ? "<input id=w>" + text : text + "<input id=w>";
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}");
        AssertGlueBoundaryRejected(document, styles, 19);
    }

    [Theory]
    [InlineData("normal", "<input id=a><input id=b>\u035c1")]
    [InlineData("pre-line", "<input id=a><input id=b>\u035c1")]
    [InlineData("normal", "a-b\u035c<input id=w>")]
    [InlineData("pre-line", "a-b\u035c<input id=w>")]
    public void AtomicWidgetBoundariesRejectGlueBacktrackingReviewExamples(string mode, string content)
    {
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}input{white-space:nowrap}");
        AssertGlueBoundaryRejected(document, styles, 20);
    }

    public static TheoryData<char, string, bool> SupportedGlueBoundaryCases()
    {
        var data = new TheoryData<char, string, bool>();
        for (var code = 0x035c; code <= 0x0362; code++)
        {
            foreach (var mode in new[] { "normal", "pre-line" })
            {
                data.Add((char)code, mode, true);
                data.Add((char)code, mode, false);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SupportedGlueBoundaryCases))]
    public void AtomicWidgetBoundariesRejectEveryAdjacentGlueRegardlessOfFit(char glue, string mode, bool widgetFirst)
    {
        var content = widgetFirst ? "<input id=w>" + glue + "1" : "1" + glue + "<input id=w>";
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}#w{white-space:nowrap}");
        AssertGlueBoundaryRejected(document, styles, 20);
        AssertGlueBoundaryRejected(document, styles, 200);
    }

    [Theory]
    [InlineData("<input id=w>")]
    [InlineData("<button id=w></button>")]
    [InlineData("<textarea id=w></textarea>")]
    [InlineData("<select id=w></select>")]
    public void AtomicWidgetBoundariesRejectAdjacentGlueForEveryControl(string widget)
    {
        foreach (var mode in new[] { "normal", "pre-line" })
        {
            foreach (var widgetFirst in new[] { true, false })
            {
                var content = widgetFirst ? widget + "\u035c1" : "1\u035c" + widget;
                var (document, styles) = Page(content, "#p{white-space:" + mode + "}#w{white-space:nowrap}");
                AssertGlueBoundaryRejected(document, styles, 20);
            }
        }
    }

    [Theory]
    [InlineData("<input id=w><span><span>\u035c\u0301\u03001</span></span>")]
    [InlineData("<span><span>1\u0362\u0301\u0300</span></span><input id=w>")]
    [InlineData("<input id=w><span><span></span></span>\u035c\u03011")]
    [InlineData("1\u035c\u0301<span><span></span></span><input id=w>")]
    [InlineData("<span><span><input id=w></span></span>\u03621")]
    [InlineData("1\u0362<span><span><input id=w></span></span>")]
    public void AtomicWidgetBoundariesRejectGlueThroughNestedStrutsAndFollowingMarks(string content)
    {
        foreach (var mode in new[] { "normal", "pre-line" })
        {
            var (document, styles) = Page(content, "#p{white-space:" + mode + "}#w{white-space:nowrap}");
            AssertGlueBoundaryRejected(document, styles, 20);
        }
    }

    [Theory]
    [InlineData("nowrap", "<input id=w>\u035c\u03011", "[w]\u035c\u03011")]
    [InlineData("pre", "<input id=w>\u035c\u03011", "[w]\u035c\u03011")]
    [InlineData("nowrap", "1\u0362\u0301<input id=w>", "1\u0362\u0301[w]")]
    [InlineData("pre", "1\u0362\u0301<input id=w>", "1\u0362\u0301[w]")]
    [InlineData("nowrap", "<span><input id=w></span><span>\u035c1</span>", "[w]\u035c1")]
    [InlineData("pre", "<span>1\u035c\u0301</span><span><input id=w></span>", "1\u035c\u0301[w]")]
    [InlineData("nowrap", "<input id=a><input id=b>\u035c1", "[a][b]\u035c1")]
    [InlineData("pre", "a-b\u035c<input id=w>", "a-b\u035c[w]")]
    [InlineData("pre", "<input id=w>\u035c1\n2", "[w]\u035c1|2")]
    [InlineData("nowrap", "<input id=w>\u035c1\n2", "[w]\u035c1 2")]
    public void AtomicWidgetBoundariesKeepGlueInNonWrappingParentModes(string mode, string content, string expected)
    {
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}input{white-space:normal}");
        var box = Layout(document, styles, 20);
        Assert.Equal(expected.Split('|'), DescribeLines(box));
        AssertLineIdentityAndContiguousGeometry(box, 20);
    }

    [Theory]
    [InlineData("normal", "<input id=w> \u035c1/2", "[w]|\u035c1/2")]
    [InlineData("pre-line", "<input id=w> \u035c1/2", "[w]|\u035c1/2")]
    [InlineData("normal", "1/2\u035c <input id=w>", "1/2\u035c|[w]")]
    [InlineData("pre-line", "1/2\u035c <input id=w>", "1/2\u035c|[w]")]
    [InlineData("pre-line", "<span><input id=w></span><span> \u0362\u0301a</span>", "[w]|\u0362\u0301a")]
    [InlineData("normal", "<span>a\u0362\u0301 </span><span><input id=w></span>", "a\u0362\u0301|[w]")]
    [InlineData("normal", "<input id=w><br>\u035c1/2", "[w]|\u035c1/2")]
    [InlineData("pre-line", "1/2\u035c\n<input id=w>", "1/2\u035c|[w]")]
    [InlineData("normal", "<input id=w>\u034f1/2", "[w]|\u034f1/2")]
    [InlineData("pre-line", "1/2\u034f<input id=w>", "1/2\u034f|[w]")]
    [InlineData("pre-line", "<input id=w>\u03011/2", "[w]|\u03011/2")]
    [InlineData("normal", "1/2\u0301<input id=w>", "1/2\u0301|[w]")]
    [InlineData("normal", "<input id=w>\u035b1/2", "[w]|\u035b1/2")]
    [InlineData("pre-line", "1/2\u0363<input id=w>", "1/2\u0363|[w]")]
    [InlineData("normal", "<input id=w>\u00a0\u03011/2", "[w]|\u00a0\u03011/2")]
    [InlineData("pre-line", "<input id=w>\u00a0\u03011/2", "[w]|\u00a0\u03011/2")]
    [InlineData("normal", "1/2\u00a0\u0301<input id=w>", "1/2\u00a0\u0301|[w]")]
    [InlineData("pre-line", "1/2\u00a0\u0301<input id=w>", "1/2\u00a0\u0301|[w]")]
    [InlineData("normal", "<input id=w>\u00a0\u0301\u035c1", "[w]|\u00a0\u0301\u035c1")]
    [InlineData("pre-line", "1\u035c\u00a0\u0301<input id=w>", "1\u035c\u00a0\u0301|[w]")]
    [InlineData("normal", "<input id=w>a\u035c1", "[w]|a\u035c1")]
    [InlineData("pre-line", "1\u035c\u0301a<input id=w>", "1\u035c\u0301a|[w]")]
    public void AtomicWidgetBoundariesStillBreakAtSpacesOrNonGlueCombiningMarks(
        string mode, string content, string expected)
    {
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}");
        var box = Layout(document, styles, 20);
        Assert.Equal(expected.Split('|'), DescribeLines(box));
        AssertLineIdentityAndContiguousGeometry(box, 20);
    }

    private static void AssertGlueBoundaryRejected(DomDocument document, CssStyleResult styles, double width)
    {
        var error = Assert.Throws<UnsupportedLayoutException>(() => Layout(document, styles, width));
        Assert.Contains("GL", error.Message, StringComparison.Ordinal);
        Assert.Contains("widget", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("backtracking", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AtomicWidgetBoundariesJustifyOnlyTheSoftWrappedLine()
    {
        var (document, styles) = Page("a b<input id=w>1/2", "#p{text-align:justify}");
        var paragraph = document.GetElementById("p")!;
        var sources = paragraph.ChildNodes.OfType<DomText>().ToArray();
        var box = Layout(document, styles, 26);
        Assert.Equal(new[] { "a b[w]", "1/2" }, DescribeLines(box));
        var first = box.Lines[0];
        Assert.Equal(new[] { 0.0, 5, 11 }, first.Fragments.Select(fragment => fragment.X));
        Assert.Equal(6, first.Fragments[1].Run.Width);
        Assert.Equal(6, Assert.Single(first.Fragments[1].Run.Glyphs).Advance);
        Assert.Equal(new LayoutRect(16, 0, 10, 10), Assert.Single(first.Widgets).Bounds);
        Assert.All(first.Fragments, fragment => Assert.Same(sources[0], fragment.Source));
        Assert.Same(sources[1], Assert.Single(box.Lines[1].Fragments).Source);
        AssertLineIdentityAndContiguousGeometry(box, 26);

        var (forcedDocument, forcedStyles) = Page("a b<input id=w><br>1/2", "#p{text-align:justify}");
        var forced = Layout(forcedDocument, forcedStyles, 26);
        Assert.Equal(new[] { "a b[w]", "1/2" }, DescribeLines(forced));
        Assert.Equal(5, forced.Lines[0].Fragments[1].Run.Width);
        Assert.Equal(new LayoutRect(15, 0, 10, 10), Assert.Single(forced.Lines[0].Widgets).Bounds);
        AssertLineIdentityAndContiguousGeometry(forced, 26);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AtomicWidgetBoundariesRetainExactSharedTextAndLineBudgets(bool widgetFirst)
    {
        var content = widgetFirst ? "<input id=w>1/2" : "1/2<input id=w>";
        var (document, styles) = Page(content);
        var exact = new LayoutOptions
        {
            MaxTextCharacters = 3,
            MaxShapedGlyphs = 3,
            MaxShapingCalls = 4,
            MaxLines = 2,
            MaxLineBreakOperations = 1
        };
        Assert.Equal(2, Layout(document, styles, 20, exact).Lines.Count);
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 20, exact with { MaxTextCharacters = 2 }));
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 20, exact with { MaxShapedGlyphs = 2 }));
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 20, exact with { MaxShapingCalls = 3 }));
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 20, exact with { MaxLines = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Layout(document, styles, 20, exact with { MaxLineBreakOperations = 0 }));
    }

    [Fact]
    public void AtomicWidgetBoundariesRetainExactHyphenSearchBudgets()
    {
        var (document, styles) = Page("<input id=w>1-a");
        var exact = new LayoutOptions
        {
            MaxTextCharacters = 3,
            MaxShapedGlyphs = 8,
            MaxShapingCalls = 6,
            MaxLines = 2,
            MaxLineBreakOperations = 11
        };
        Assert.Equal(new[] { "[w]", "1-a" }, DescribeLines(Layout(document, styles, 19, exact)));
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 19, exact with { MaxTextCharacters = 2 }));
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 19, exact with { MaxShapedGlyphs = 7 }));
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 19, exact with { MaxShapingCalls = 5 }));
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 19, exact with { MaxLines = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout(document, styles, 19, exact with { MaxLineBreakOperations = 10 }));
    }

    [Theory]
    [InlineData(true, "1/2", "1/2")]
    [InlineData(false, "1/2", "1/2")]
    [InlineData(true, "1-a", "1-")]
    public void AtomicWidgetBoundariesObserveShapingCancellation(bool widgetFirst, string text, string canceledText)
    {
        var content = widgetFirst ? "<input id=w>" + text : text + "<input id=w>";
        var (document, styles) = Page(content);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var shaper = new CancelOnTextShaper(cancellation, canceledText);
        Assert.Throws<OperationCanceledException>(() => StaticLayout.Layout(document, styles, shaper, 19, 200,
            cancellationToken: cancellation.Token));
        Assert.True(shaper.Canceled);
    }

    [Theory]
    [InlineData("normal", true)]
    [InlineData("normal", false)]
    [InlineData("pre-line", true)]
    [InlineData("pre-line", false)]
    [InlineData("nowrap", true)]
    [InlineData("nowrap", false)]
    [InlineData("pre", true)]
    [InlineData("pre", false)]
    public void AtomicWidgetBoundariesDoNotAcceptUnsupportedSoftHyphens(string mode, bool widgetFirst)
    {
        var content = widgetFirst ? "<input id=w>a\u00adb" : "a\u00adb<input id=w>";
        var (document, styles) = Page(content, "#p{white-space:" + mode + "}");
        Assert.Throws<UnsupportedTextException>(() => Layout(document, styles, 20));
    }

    [Theory]
    [InlineData("<input id=w>1/<span>2</span>", "")]
    [InlineData("1/<span>2</span><input id=w>", "")]
    [InlineData("<input id=w>1<span>2</span>", "")]
    [InlineData("1<span>2</span><input id=w>", "")]
    [InlineData("<input id=w><span>1/2</span>", "span{white-space:nowrap}")]
    public void AtomicWidgetBoundariesDoNotAcceptUnsupportedTextOrInlineModeBoundaries(string content, string css)
    {
        var (document, styles) = Page(content, css);
        Assert.Throws<UnsupportedLayoutException>(() => Layout(document, styles, 20));
    }

    private sealed class CancelOnTextShaper(CancellationTokenSource cancellation, string expected) : ITextShaper
    {
        public bool Canceled { get; private set; }

        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
        {
            var run = new MetricsShaper().Shape(text, request, cancellationToken);
            if (text == expected)
            {
                Canceled = true;
                cancellation.Cancel();
            }
            return run;
        }
    }

    private sealed class MetricsShaper : ITextShaper
    {
        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var glyphs = text.Select((character, index) => new ShapedGlyph(character, index, request.Size / 2, 0, 0))
                .ToList().AsReadOnly();
            return new(text, "deterministic", request.Size, glyphs, text.Length * request.Size / 2,
                new(request.Size * .8, request.Size * .2, 0));
        }
    }
}
