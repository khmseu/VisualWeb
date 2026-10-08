using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class LayoutTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static (DomDocument Document, CssStyleResult Styles) Page(string html, string css = "")
    {
        var document = HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("html,body,div,p {display:block} head{display:none} *{margin:0; font-size:10px;line-height:10px} " + css)],
            includeUserAgent: false, cancellationToken: Cancellation);
        return (document, styles);
    }
    private static LayoutResult Layout(string html, string css = "", double width = 100, LayoutOptions? options = null)
    {
        var (doc, styles) = Page(html, css);
        return StaticLayout.Layout(doc, styles, new MetricsShaper(), width, 200, options, Cancellation);
    }
    private static LayoutBox Find(LayoutBox root, string id)
    {
        if (root.Element.GetAttribute("id") == id) { return root; }
        foreach (var child in root.Children)
        {
            var found = TryFind(child, id);
            if (found is not null) { return found; }
        }
        throw new InvalidOperationException("Missing box " + id);
    }
    private static LayoutBox? TryFind(LayoutBox root, string id) => root.Element.GetAttribute("id") == id ? root
        : root.Children.Select(child => TryFind(child, id)).FirstOrDefault(found => found is not null);

    [Fact]
    public void WidthPaddingBorderAndAutoMarginsFollowContainingBlockEquation()
    {
        var root = Layout("<div id=x>hi</div>", "#x{width:40px; padding:5px;border:2px solid; margin-left:auto;margin-right:auto}").Root!;
        var box = Find(root, "x");
        Assert.Equal(new LayoutRect(30, 7, 40, 10), box.Content);
        Assert.Equal(new LayoutRect(23, 0, 54, 24), box.BorderBox);
        Assert.Equal(23, box.Margin.Left);
        Assert.Equal(23, box.Margin.Right);
        Assert.Equal(100, box.Margin.Left + box.BorderBox.Width + box.Margin.Right);
    }

    [Fact]
    public void BorderBoxSizingMinMaxAndPercentPaddingUseCssPixels()
    {
        var root = Layout("<div id=x></div>", "#x{width:80px;max-width:60px;box-sizing:border-box;padding:10%;border:5px solid;height:40px}").Root!;
        var box = Find(root, "x");
        Assert.Equal(60, box.BorderBox.Width);
        Assert.Equal(30, box.Content.Width);
        Assert.Equal(40, box.BorderBox.Height);
        Assert.Equal(10, box.Content.Height);
        var constrained = Find(Layout("<div id=x></div>", "#x{width:20px;max-width:10px;min-width:30px}").Root!, "x");
        Assert.Equal(30, constrained.Content.Width);
    }

    [Fact]
    public void AutoWidthCanBeConstrainedAndCenteredAndOverconstraintAdjustsRightMargin()
    {
        var box = Find(Layout("<div id=x></div>", "#x{max-width:40px;margin-left:auto;margin-right:auto}").Root!, "x");
        Assert.Equal(30, box.Margin.Left);
        Assert.Equal(30, box.Margin.Right);
        var overflow = Find(Layout("<div id=x></div>", "#x{width:120px;margin-left:5px;margin-right:5px}").Root!, "x");
        Assert.Equal(-25, overflow.Margin.Right);
    }

    [Fact]
    public void ChildrenUseDefiniteClampedHeightForPercentages()
    {
        var root = Layout("<div id=x><div id=y></div></div>", "#x{height:100px;max-height:60px} #y{height:50%}").Root!;
        Assert.Equal(60, Find(root, "x").Content.Height);
        Assert.Equal(30, Find(root, "y").Content.Height);
        var auto = Layout("<div id=x><div id=y>hi</div></div>", "#y{height:50%}").Root!;
        Assert.Equal(10, Find(auto, "y").Content.Height);
    }

    [Fact]
    public void BlockChildrenStackAndHiddenSubtreesDoNotGenerateGeometry()
    {
        var root = Layout("<div id=a>A</div><div id=hidden>long</div><div id=b>B</div>", "#hidden{display:none}").Root!;
        Assert.Equal(0, Find(root, "a").BorderBox.Y);
        Assert.Equal(10, Find(root, "b").BorderBox.Y);
        Assert.Equal(20, root.Content.Height);
        Assert.Throws<InvalidOperationException>(() => Find(root, "hidden"));
    }

    [Fact]
    public void AdjacentBlockVerticalMarginsCollapseToLargestPositiveMargin()
    {
        var root = Layout("<div><p id=a>A</p><p id=b>B</p></div>",
            "#a{margin-top:3px;margin-bottom:8px} #b{margin-top:12px;margin-bottom:4px}").Root!;
        var first = Find(root, "a");
        var second = Find(root, "b");

        Assert.Equal(3, first.BorderBox.Y);
        Assert.Equal(first.BorderBox.Y + first.BorderBox.Height + 12, second.BorderBox.Y);
    }

    [Fact]
    public void ParentAndFirstBlockChildTopMarginsCollapseAtUnborderedUnpaddedEdge()
    {
        var root = Layout("<div id=parent><p id=child>A</p></div>",
            "body{border-top:1px solid} #parent{margin-top:6px} #child{margin-top:10px}").Root!;
        var parent = Find(root, "parent");
        var child = Find(root, "child");

        Assert.Equal(10, parent.Margin.Top);
        Assert.Equal(parent.BorderBox.Y, child.BorderBox.Y);
        Assert.Equal(10, parent.Content.Height);
    }

    [Fact]
    public void PercentageMarginsInParentEdgeChainsUseEachContainingWidth()
    {
        var root = Layout("<div id=parent><p id=child>A</p></div>",
            "body{border-top:1px solid} #parent{width:50%} #child{margin-top:10%}").Root!;
        var parent = Find(root, "parent");
        var child = Find(root, "child");

        Assert.Equal(50, parent.Content.Width);
        Assert.Equal(5, parent.Margin.Top);
        Assert.Equal(parent.BorderBox.Y, child.BorderBox.Y);
    }

    [Fact]
    public void ParentAndLastBlockChildBottomMarginsCollapseIntoNextSiblingGap()
    {
        var root = Layout("<div id=parent><p id=child>A</p></div><div id=next>B</div>",
            "#parent{margin-bottom:3px} #child{margin-bottom:8px} #next{margin-top:12px}").Root!;
        var parent = Find(root, "parent");
        var child = Find(root, "child");
        var next = Find(root, "next");

        Assert.Equal(8, parent.Margin.Bottom);
        Assert.Equal(parent.BorderBox.Y + parent.BorderBox.Height, child.BorderBox.Y + child.BorderBox.Height);
        Assert.Equal(parent.BorderBox.Y + parent.BorderBox.Height + 12, next.BorderBox.Y);
    }

    [Fact]
    public void ParentBottomMarginDoesNotCollapseThroughBorderOrDefiniteHeight()
    {
        var bordered = Find(Layout("<div id=parent><p id=child>A</p></div>",
            "#parent{border-bottom:1px solid} #child{margin-bottom:8px}").Root!, "parent");
        var definite = Find(Layout("<div id=parent><p id=child>A</p></div>",
            "#parent{height:30px} #child{margin-bottom:8px}").Root!, "parent");

        Assert.Equal(0, bordered.Margin.Bottom);
        Assert.Equal(0, definite.Margin.Bottom);
        Assert.Equal(30, definite.Content.Height);
    }

    [Fact]
    public void InlineContentPreventsBottomParentEdgeCollapse()
    {
        var root = Layout("<div id=parent><p id=child>A</p>tail</div>",
            "#child{margin-bottom:8px}").Root!;
        var parent = Find(root, "parent");
        var child = Find(root, "child");

        Assert.Equal(0, parent.Margin.Bottom);
        Assert.True(parent.Content.Height > child.BorderBox.Height);
    }

    [Fact]
    public void AdjacentNegativeBlockMarginsCollapseToMostNegativeMargin()
    {
        var root = Layout("<div><p id=a>A</p><p id=b>B</p></div>",
            "#a{margin-bottom:-4px} #b{margin-top:-7px}").Root!;
        var first = Find(root, "a");
        var second = Find(root, "b");

        Assert.Equal(first.BorderBox.Y + first.BorderBox.Height - 7, second.BorderBox.Y);
    }

    [Fact]
    public void CollapsedWhitespaceWrapsAtSpacesAndDropsLineEdges()
    {
        var box = Find(Layout("<p id=x>  aa \n bb\tcc  </p>", width: 24).Root!, "x");
        Assert.Equal(3, box.Lines.Count);
        Assert.Equal(new[] { "aa", "bb", "cc" }, box.Lines.Select(line => string.Concat(line.Fragments.Select(f => f.Run.Text))));
        Assert.Equal(new[] { 0.0, 10, 20 }, box.Lines.Select(line => line.Bounds.Y));
        Assert.Equal(30, box.Content.Height);
    }

    [Theory]
    [InlineData("nowrap", "aa bb", 1)]
    [InlineData("pre", "aa bb", 1)]
    [InlineData("pre", "aa\nbb", 2)]
    [InlineData("pre-line", "aa\n  bb", 2)]
    public void WhitespaceModesPreserveBreaksOrPreventWrapping(string mode, string text, int count)
    {
        var box = Find(Layout("<p id=x>" + text + "</p>", "#x{white-space:" + mode + "}", width: 15).Root!, "x");
        Assert.Equal(count, box.Lines.Count);
    }

    [Fact]
    public void BrBreaksAndPreservedSpacesHaveExplicitLineGeometry()
    {
        var box = Find(Layout("<p id=x>a<br>b<br></p>").Root!, "x");
        Assert.Equal(2, box.Lines.Count);
        var pre = Find(Layout("<p id=x> a  </p>", "#x{white-space:pre}").Root!, "x");
        Assert.Equal(" a  ", string.Concat(pre.Lines[0].Fragments.Select(f => f.Run.Text)));
        Assert.Equal(20, pre.Lines[0].Fragments.Sum(f => f.Run.Width));
    }

    [Theory]
    [InlineData("left", 0)]
    [InlineData("right", 90)]
    [InlineData("center", 45)]
    [InlineData("end", 90)]
    public void TextAlignUsesActualRunWidth(string align, double expected)
    {
        var box = Find(Layout("<p id=x>ab</p>", "#x{text-align:" + align + "}").Root!, "x");
        Assert.Equal(expected, box.Lines[0].Fragments[0].X);
    }

    [Fact]
    public void MixedFontRunsAlignBaselinesAndRespectLeading()
    {
        var box = Find(Layout("<p id=x>a <span id=big>b</span></p>", "#big{font-size:20px;line-height:20px}").Root!, "x");
        var line = Assert.Single(box.Lines);
        Assert.Equal(20, line.Bounds.Height);
        Assert.Equal(16, line.Baseline);
        Assert.All(line.Fragments, f => Assert.Equal(16, f.Baseline));
    }

    [Fact]
    public void InlineGroupsBeforeAndAfterBlocksCreateAnonymousLineGroups()
    {
        var box = Find(Layout("<div id=x>before<p>inside</p>after</div>").Root!, "x");
        Assert.Equal(2, box.Lines.Count);
        Assert.Equal(0, box.Lines[0].Bounds.Y);
        Assert.Equal(10, Assert.Single(box.Children).BorderBox.Y);
        Assert.Equal(20, box.Lines[1].Bounds.Y);
        Assert.Equal(30, box.Content.Height);
    }

    [Fact]
    public void InlineFormControlsAreAtomicAndWrapWithTextFlow()
    {
        var root = Layout("<body id=container><input id=a><input id=b></body>",
            "input{display:inline-block;width:25px;height:12px}", width: 40).Root!;
        var lines = Find(root, "container").Lines;
        Assert.Equal(2, lines.Count);
        Assert.Equal(0, lines[0].Widgets.Single().Bounds.X);
        Assert.Equal(25, lines[0].Widgets.Single().Bounds.Width);
        Assert.Equal(12, lines[0].Widgets.Single().Bounds.Height);
        Assert.Equal(lines[0].Bounds.Height, lines[1].Widgets.Single().Bounds.Y);
    }

    [Theory]
    [InlineData("<span><div>x</div></span>", "")]
    [InlineData("<span>x</span>", "span{display:inline-block}")]
    [InlineData("<span>x</span>", "span{padding:1px}")]
    [InlineData("<p dir=rtl>x</p>", "")]
    [InlineData("<p>\u05D0</p>", "")]
    [InlineData("<p>ab<span>cd</span></p>", "")]
    [InlineData("<p>x</p>", "p{text-align:justify}")]
    [InlineData("<p>x</p>", "p{white-space:pre-wrap}")]
    [InlineData("<p>\t</p>", "p{white-space:pre}")]
    [InlineData("<p>a-b</p>", "")]
    [InlineData("<p lang=de>x</p>", "")]
    [InlineData("<span>x</span>", "span{background-color:red}")]
    [InlineData("<p>a <span>bb cc</span></p>", "span{white-space:nowrap}")]
    [InlineData("<img>", "")]
    [InlineData("<input>", "")]
    [InlineData("<button>x</button>", "")]
    [InlineData("<input type=password>", "input{display:block}")]
    [InlineData("<input type=hidden>", "input{display:block}")]
    [InlineData("<ul><li>x</li></ul>", "")]
    public void DeferredAlgorithmsNeverReturnApproximateGeometry(string html, string css)
    {
        var exception = Record.Exception(() => Layout(html, css));
        Assert.True(exception is UnsupportedLayoutException or UnsupportedTextException, exception?.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" type=search")]
    [InlineData(" type=SUBMIT")]
    [InlineData(" type=reset")]
    [InlineData(" type=bogus")]
    public void BlockTextAndSubmitInputsUseOneLineHeightWithoutChildren(string type)
    {
        var root = Layout($"<form id=f><input id=x{type} value='not painted'><button id=b>go</button></form>",
            "form,input,button{display:block} input{border:1px solid;padding:2px}").Root!;
        var input = Find(root, "x");
        Assert.Equal(new LayoutRect(3, 3, 94, 10), input.Content);
        Assert.Equal(16, input.BorderBox.Height);
        Assert.Empty(input.Lines);
        Assert.Empty(input.Children);
        var button = Find(root, "b");
        Assert.Equal(16, button.BorderBox.Y);
        Assert.Single(button.Lines);
    }

    [Fact]
    public void EmptyInlineStrutsParticipateButDoNotCreateStandaloneLines()
    {
        var box = Find(Layout("<p id=x>a<span></span></p>", "span{font-size:20px;line-height:20px}").Root!, "x");
        Assert.Equal(20, Assert.Single(box.Lines).Bounds.Height);
        Assert.Empty(Find(Layout("<p id=x><span></span></p>").Root!, "x").Lines);
        var wrapped = Find(Layout("<p id=x><span><em>aa bb</em></span></p>",
            "span{font-size:20px;line-height:20px} em{font-size:10px;line-height:10px}", width: 24).Root!, "x");
        Assert.Equal(2, wrapped.Lines.Count);
        Assert.All(wrapped.Lines, line => Assert.Equal(20, line.Bounds.Height));
    }

    [Fact]
    public void ExactFitUnbreakableOverflowAndNegativeLeadingHaveMeasuredOutputs()
    {
        var fit = Find(Layout("<p id=x>aa bb</p>", width: 25).Root!, "x");
        Assert.Single(fit.Lines);
        Assert.Equal(25, fit.Lines[0].Fragments.Sum(f => f.Run.Width));
        var overflow = Find(Layout("<p id=x>unbroken</p>", width: 10).Root!, "x");
        Assert.Single(overflow.Lines);
        Assert.Equal(40, overflow.Lines[0].Fragments[0].Run.Width);
        var leading = Find(Layout("<p id=x>a</p>", "#x{line-height:0}").Root!, "x");
        Assert.Equal(0, leading.Content.Height);
        Assert.Equal(3, leading.Lines[0].Baseline);
    }

    [Fact]
    public void CssDiagnosticsAndNodeBudgetsAreNotSilentlyBypassed()
    {
        var (doc, styles) = Page("<p>x</p>", "p{float:left}");
        Assert.Throws<UnsupportedLayoutException>(() => StaticLayout.Layout(doc, styles, new MetricsShaper(), 100, 200, cancellationToken: Cancellation));
        Assert.NotNull(Layout("<p>x</p>", options: new() { MaxNodes = 4 }).Root);
        Assert.Throws<LayoutLimitException>(() => Layout("<p>x</p>", options: new() { MaxNodes = 3 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Layout("<p>x</p>", width: double.NaN));
        var empty = new DomDocument();
        Assert.Null(StaticLayout.Layout(empty, CssStyleEngine.Compute(empty, cancellationToken: Cancellation),
            new MetricsShaper(), 100, 100, cancellationToken: Cancellation).Root);
    }

    [Fact]
    public void LimitsCancellationAndMissingStylesFailExplicitly()
    {
        var (doc, styles) = Page("<p>x</p>");
        var result = StaticLayout.Layout(doc, styles, new MetricsShaper(), 100, 200,
            new() { MaxBoxes = 3, MaxLines = 1, MaxTextCharacters = 1, MaxShapedGlyphs = 1, MaxShapingCalls = 2 }, Cancellation);
        Assert.NotNull(result.Root);
        Assert.Throws<LayoutLimitException>(() => Layout("<p>x</p>", options: new() { MaxBoxes = 2 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>x<br>y</p>", options: new() { MaxLines = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>xy</p>", options: new() { MaxTextCharacters = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>xy</p>", options: new() { MaxShapedGlyphs = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>x</p>", options: new() { MaxShapingCalls = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>x</p>", options: new() { MaxDepth = 2 }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => StaticLayout.Layout(doc, styles, new MetricsShaper(), 100, 200, cancellationToken: canceled.Token));
        doc.Body!.AppendChild(doc.CreateElement("div"));
        Assert.Throws<InvalidOperationException>(() => StaticLayout.Layout(doc, styles, new MetricsShaper(), 100, 200, cancellationToken: Cancellation));
    }

    [Fact]
    public void NativeShapingFeedsLayoutWithoutDisplayOrRasterization()
    {
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var fonts = new FontSet();
        fonts.Register("serif", font);
        var (doc, styles) = Page("<p id=x>office sample text</p>");
        var result = StaticLayout.Layout(doc, styles, fonts, 60, 200, cancellationToken: Cancellation);
        var box = Find(result.Root!, "x");
        Assert.True(box.Lines.Count >= 2);
        var office = box.Lines[0].Fragments[0].Run;
        Assert.Equal("office", office.Text);
        Assert.True(office.Glyphs.Count < office.Text.Length);
        Assert.Equal(font.Shape("office", 10, Cancellation).Width, office.Width);
    }

    [Fact]
    public void NormalLineHeightUsesNativeFontExtentsAndGap()
    {
        using var font = new TextFont(Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf"));
        var fonts = new FontSet();
        fonts.Register("serif", font);
        var (doc, styles) = Page("<p id=x>a</p>", "*{font-size:16px;line-height:normal}");
        var result = StaticLayout.Layout(doc, styles, fonts, 100, 200, cancellationToken: Cancellation);
        var line = Assert.Single(Find(result.Root!, "x").Lines);
        Assert.Equal(21.796875, line.Bounds.Height);
        Assert.Equal(17.109375, line.Baseline);
    }

    private sealed class MetricsShaper : ITextShaper
    {
        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var glyphs = text.Select((c, i) => new ShapedGlyph(c, i, request.Size / 2, 0, 0)).ToList().AsReadOnly();
            return new(text, "deterministic", request.Size, glyphs, text.Length * request.Size / 2,
                new(request.Size * .8, request.Size * .2, 0));
        }
    }
}
