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

    private static LayoutResult UnorderedList(string html, string css = "", double width = 100, LayoutOptions? options = null)
    {
        var doc = HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(doc, [new("*{margin:0;font-size:10px;line-height:10px}" + css)],
            cancellationToken: Cancellation);
        return StaticLayout.Layout(doc, styles, new MetricsShaper(), width, 200, options, Cancellation);
    }

    [Fact]
    public void DefaultUnorderedListsHaveOutsideDiscsWithoutChangingContentGeometry()
    {
        var root = UnorderedList("<ul><li id=a>a</li><li id=b>b</li></ul>").Root!;
        var first = Find(root, "a");
        var second = Find(root, "b");
        Assert.Equal(new LayoutRect(40, 0, 60, 10), first.Content);
        Assert.Equal(new LayoutRect(32, 3, 3, 3), first.DiscMarker);
        Assert.Equal(new LayoutRect(32, 13, 3, 3), second.DiscMarker);
        Assert.Equal(20, root.Content.Height);
        Assert.Single(first.Lines);
        Assert.Equal("a", Assert.Single(first.Lines[0].Fragments).Source.Data);
    }

    [Fact]
    public void SquareMarkersKeepOutsideGeometryForWrappedNestedAndEmptyItems()
    {
        var root = UnorderedList("<ul id=parent><li id=a>a b</li><li id=b></li></ul>",
            "#parent{list-style:square}", width: 50).Root!;
        var first = Find(root, "a");
        var second = Find(root, "b");
        Assert.Equal(new LayoutRect(40, 0, 10, 20), first.Content);
        Assert.Equal(new LayoutRect(32, 3, 3, 3), first.SquareMarker);
        Assert.Equal(new LayoutRect(32, 23, 3, 3), second.SquareMarker);
        Assert.Null(first.DiscMarker);
        Assert.Null(first.TextMarker);
        Assert.Equal(2, first.Lines.Count);
        Assert.Equal("a b", first.Lines[0].Fragments[0].Source.Data);

        var nested = UnorderedList("<ul><li id=outer><ul><li id=inner></li></ul></li></ul>",
            "ul{list-style-type:square}").Root!;
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(nested, "outer").SquareMarker);
        Assert.Equal(new LayoutRect(72, 3, 3, 3), Find(nested, "inner").SquareMarker);
        Assert.Equal(10, Find(nested, "inner").Content.Height);
    }

    [Fact]
    public void SquareAndSuppressedMarkersDoNotAlterOrderedListNumbering()
    {
        var root = UnorderedList("<ol start=5><li id=a></li><li id=b></li><li id=c></li></ol>",
            "#a{list-style:square}#b{list-style:none}").Root!;
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(root, "a").SquareMarker);
        Assert.Null(Find(root, "a").TextMarker);
        Assert.Null(Find(root, "b").SquareMarker);
        Assert.Null(Find(root, "b").TextMarker);
        Assert.Null(Find(root, "c").SquareMarker);
        Assert.Equal("7.", Find(root, "c").TextMarker!.Run.Text);
    }

    [Fact]
    public void SquareMarkersShareShapingBudgetsAndCancellation()
    {
        const string html = "<ul><li></li><li></li></ul>";
        const string css = "ul{list-style:square}";
        Assert.NotNull(UnorderedList(html, css, options: new() { MaxShapingCalls = 2 }).Root);
        Assert.Throws<LayoutLimitException>(() =>
            UnorderedList(html, css, options: new() { MaxShapingCalls = 1 }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var doc = HtmlParser.Parse(html, cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(doc, [new(css)], cancellationToken: Cancellation);
        Assert.Throws<OperationCanceledException>(() =>
            StaticLayout.Layout(doc, styles, new MetricsShaper(), 100, 100, cancellationToken: canceled.Token));
    }

    [Fact]
    public void NestedEmptyAndWrappedUnorderedItemsRetainOneMarkerEach()
    {
        var nested = UnorderedList("<ul><li id=outer><ul><li id=inner></li></ul></li></ul>").Root!;
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(nested, "outer").DiscMarker);
        Assert.Equal(new LayoutRect(72, 3, 3, 3), Find(nested, "inner").DiscMarker);
        Assert.Equal(10, Find(nested, "inner").Content.Height);
        var wrapped = Find(UnorderedList("<ul><li id=x>a b</li></ul>", width: 50).Root!, "x");
        Assert.Equal(2, wrapped.Lines.Count);
        Assert.Equal(new LayoutRect(32, 3, 3, 3), wrapped.DiscMarker);
    }

    [Fact]
    public void HiddenAndSuppressedMarkersDoNotAppear()
    {
        var root = UnorderedList("<ul><li id=a>a</li><li id=b>b</li><li id=c>c</li></ul>",
            "#a{list-style-type:none} #b{display:none} #c{display:block}").Root!;
        Assert.Null(Find(root, "a").DiscMarker);
        Assert.Null(Find(root, "c").DiscMarker);
        Assert.Null(TryFind(root, "b"));
    }

    [Theory]
    [InlineData("<ol type=a><li>x</li></ol>")]
    [InlineData("<ul type=square><li>x</li></ul>")]
    [InlineData("<ul><li type=circle>x</li></ul>")]
    [InlineData("<li>x</li>")]
    public void UnimplementedListKindsRemainExplicitFailures(string html) =>
        Assert.Throws<UnsupportedLayoutException>(() => UnorderedList(html));

    [Fact]
    public void EmptyUnorderedListMarkersChargeShapingBudgetsAndHonorCancellation()
    {
        Assert.NotNull(UnorderedList("<ul><li></li></ul>", options: new() { MaxShapingCalls = 1 }).Root);
        Assert.Throws<LayoutLimitException>(() =>
            UnorderedList("<ul><li></li><li></li></ul>", options: new() { MaxShapingCalls = 1 }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var doc = HtmlParser.Parse("<ul><li>x</li></ul>", cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(doc, cancellationToken: Cancellation);
        Assert.Throws<OperationCanceledException>(() =>
            StaticLayout.Layout(doc, styles, new MetricsShaper(), 100, 100, cancellationToken: canceled.Token));
    }

    [Fact]
    public void OrderedListMarkersUseShapedDecimalsOutsideTheFirstBaseline()
    {
        var root = UnorderedList("<ol type=1 start=9><li id=a>a b</li><li id=b></li></ol>", width: 50).Root!;
        var first = Find(root, "a");
        var second = Find(root, "b");
        var marker = Assert.IsType<LayoutTextMarker>(first.TextMarker);
        Assert.Equal("9.", marker.Run.Text);
        Assert.Equal(25, marker.X);
        Assert.Equal(8, marker.Baseline);
        Assert.Equal(2, first.Lines.Count);
        Assert.Equal("10.", second.TextMarker!.Run.Text);
        Assert.Equal(20, second.TextMarker.X);
        Assert.Equal(28, second.TextMarker.Baseline);
        Assert.Equal(10, second.Content.Height);
        Assert.Null(first.DiscMarker);
        Assert.Equal("a", first.Lines[0].Fragments[0].Source.Data[..1]);
    }

    [Theory]
    [InlineData("", "1.", "2.", "3.")]
    [InlineData("start=' \t+004tail'", "4.", "5.", "6.")]
    [InlineData("start='-2'", "-2.", "-1.", "0.")]
    [InlineData("start='x'", "1.", "2.", "3.")]
    [InlineData("reversed", "3.", "2.", "1.")]
    [InlineData("reversed=false start=7", "7.", "6.", "5.")]
    [InlineData("reversed start='-'", "3.", "2.", "1.")]
    public void OrderedListStartingValuesFollowHtmlIntegerParsing(string attributes, string a, string b, string c)
    {
        var root = UnorderedList($"<ol {attributes}><li id=a></li><li id=b></li><li id=c></li></ol>").Root!;
        Assert.Equal(new[] { a, b, c }, new[] { "a", "b", "c" }.Select(id => Find(root, id).TextMarker!.Run.Text));
    }

    [Fact]
    public void ReversedNumberingCountsOnlyRenderedListItemsAndValueResetsTheSequence()
    {
        var root = UnorderedList("""
            <ol reversed><li id=a></li><li id=hidden value=100></li><li id=block value=200></li>
            <li id=b value='-3suffix'></li><li id=c></li><li id=d></li></ol>
            """, "#hidden{display:none} #block{display:block} #a{list-style-type:none}").Root!;
        Assert.Null(Find(root, "a").TextMarker);
        Assert.Null(Find(root, "block").TextMarker);
        Assert.Null(TryFind(root, "hidden"));
        Assert.Equal("-3.", Find(root, "b").TextMarker!.Run.Text);
        Assert.Equal("-4.", Find(root, "c").TextMarker!.Run.Text);
        Assert.Equal("-5.", Find(root, "d").TextMarker!.Run.Text);
        var counted = UnorderedList("<ol reversed><li id=a></li><li id=b></li><li id=c></li></ol>",
            "#b{display:none}").Root!;
        Assert.Equal("2.", Find(counted, "a").TextMarker!.Run.Text);
        Assert.Equal("1.", Find(counted, "c").TextMarker!.Run.Text);
    }

    [Fact]
    public void NestedListCountersAreIndependentAndDecimalOverridesWorkOnUnorderedLists()
    {
        var root = UnorderedList("""
            <ol start=5><li id=a><ul><li id=disc></li></ul>
            <ol reversed><li id=nested></li><li></li></ol></li><li id=b></li></ol>
            <ul id=decimal><li value=8 id=c></li><li id=d></li></ul>
            """, "#decimal{list-style-type:decimal}").Root!;
        Assert.Equal("5.", Find(root, "a").TextMarker!.Run.Text);
        Assert.Equal("6.", Find(root, "b").TextMarker!.Run.Text);
        Assert.Equal("2.", Find(root, "nested").TextMarker!.Run.Text);
        Assert.NotNull(Find(root, "disc").DiscMarker);
        Assert.Equal("8.", Find(root, "c").TextMarker!.Run.Text);
        Assert.Equal("9.", Find(root, "d").TextMarker!.Run.Text);
    }

    [Theory]
    [InlineData("start=2147483648", "")]
    [InlineData("start=-2147483649", "")]
    [InlineData("", "value=999999999999999999999")]
    public void OutOfRangeNumberingFailsVisibly(string listAttributes, string itemAttributes) =>
        Assert.Throws<LayoutLimitException>(() => UnorderedList($"<ol {listAttributes}><li {itemAttributes}></li></ol>"));

    [Fact]
    public void DecimalMarkersChargeExactTextGlyphAndShapingLimits()
    {
        const string html = "<ol><li></li></ol>";
        Assert.NotNull(UnorderedList(html, options: new() { MaxTextCharacters = 2, MaxShapedGlyphs = 2, MaxShapingCalls = 1 }).Root);
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxTextCharacters = 1 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxShapedGlyphs = 1 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList("<ol><li></li><li></li></ol>",
            options: new() { MaxShapingCalls = 1 }));
        Assert.Equal("2147483648.", Find(UnorderedList("<ol start=2147483647><li></li><li id=x></li></ol>").Root!, "x").TextMarker!.Run.Text);
    }

    [Fact]
    public void AutomaticTablesShrinkToIntrinsicColumnsAndKeepExactSeparatedGeometry()
    {
        var root = UnorderedList("<table id=t><tr><td id=a>aa bb</td><td id=b>x</td></tr>"
            + "<tr><td id=c>cccc</td><td>yy</td></tr></table>").Root!;
        Assert.Equal(new LayoutRect(0, 0, 45, 30), Find(root, "t").Content);
        Assert.Equal(new LayoutRect(2, 2, 27, 12), Find(root, "a").BorderBox);
        Assert.Equal(new LayoutRect(3, 3, 25, 10), Find(root, "a").Content);
        Assert.Equal(new LayoutRect(31, 2, 12, 12), Find(root, "b").BorderBox);
        Assert.Equal(16, Find(root, "c").BorderBox.Y);
        Assert.Single(Find(root, "a").Lines);
    }

    [Theory]
    [InlineData(30, 40, 22, 40)]
    [InlineData(40, 40, 22, 40)]
    [InlineData(42.5, 42.5, 24.5, 40)]
    [InlineData(45, 45, 27, 30)]
    public void AutomaticTablesInterpolateColumnsWrapAndOverflowAtTheirIntrinsicMinimum(
        double viewport, double tableWidth, double columnWidth, double height)
    {
        var root = UnorderedList("<table id=t><tr><td id=a>aa bb</td><td>x</td></tr>"
            + "<tr><td>cccc</td><td>yy</td></tr></table>", width: viewport).Root!;
        Assert.Equal(tableWidth, Find(root, "t").Content.Width);
        Assert.Equal(columnWidth, Find(root, "a").BorderBox.Width);
        Assert.Equal(height, Find(root, "t").Content.Height);
    }

    [Fact]
    public void ExplicitTableWidthDistributesSurplusAndHonorsWrapperAutoMargins()
    {
        var root = UnorderedList("<table id=t><tr><td id=a>aa bb</td><td id=b>x</td></tr>"
            + "<tr><td>cccc</td><td>yy</td></tr></table>", "table{width:65px;margin-left:auto;margin-right:auto}").Root!;
        Assert.Equal(17.5, Find(root, "t").Content.X);
        Assert.Equal(37, Find(root, "a").BorderBox.Width);
        Assert.Equal(22, Find(root, "b").BorderBox.Width);
    }

    [Fact]
    public void EmptyTablesAndMissingCellsPreserveGridSpacingWithoutInventingBoxes()
    {
        var empty = Find(UnorderedList("<table id=t></table>").Root!, "t");
        Assert.Equal(0, empty.Content.Width);
        Assert.Equal(0, empty.Content.Height);
        Assert.Empty(empty.Children);
        var root = UnorderedList("<table id=t><tr><td id=a>a</td><td>b</td></tr>"
            + "<tr id=row><td id=c>c</td></tr></table>").Root!;
        Assert.Equal(20, Find(root, "t").Content.Width);
        Assert.Equal(30, Find(root, "t").Content.Height);
        Assert.Single(Find(root, "row").Children);
        Assert.Equal(Find(root, "a").BorderBox.Width, Find(root, "c").BorderBox.Width);
        Assert.Equal(16, Find(root, "c").BorderBox.Y);
    }

    [Fact]
    public void TablesSupportOrdinaryBlockCellsHiddenRowsAndHeaderFooterOrdering()
    {
        var root = UnorderedList("""
            <table id=t><tfoot><tr><td id=foot>end</td></tr></tfoot>
            <tbody><tr id=hidden><td>verylonghiddenword</td></tr><tr><td id=body><div>aa bb</div><p>ccc</p></td></tr></tbody>
            <thead><tr><th id=head>top</th></tr></thead></table>
            """, "#hidden{display:none}").Root!;
        Assert.Equal(31, Find(root, "t").Content.Width);
        Assert.True(Find(root, "head").BorderBox.Y < Find(root, "body").BorderBox.Y);
        Assert.True(Find(root, "body").BorderBox.Y < Find(root, "foot").BorderBox.Y);
        Assert.Equal(2, Find(root, "body").Children.Count);
        Assert.Null(TryFind(root, "hidden"));
    }

    [Fact]
    public void TableCellsDoNotCollapseMarginsThroughTheirEdgesAndStretchToRowHeight()
    {
        var root = UnorderedList("<table><tr><td id=a><p>a</p></td><td id=b>b<br>b</td></tr></table>",
            "td{padding:0;height:1px}p{margin-top:5px;margin-bottom:5px}").Root!;
        Assert.Equal(20, Find(root, "a").BorderBox.Height);
        Assert.Equal(20, Find(root, "b").BorderBox.Height);
        Assert.Equal(7, Find(root, "a").Children[0].Content.Y);
        Assert.Equal(2, Find(root, "b").Content.Y);
    }

    [Fact]
    public void TableMeasurementAndFinalLayoutShareExactWorkBudgets()
    {
        const string html = "<table><tr><td>a</td></tr></table>";
        Assert.NotNull(UnorderedList(html, options: new()
        {
            MaxBoxes = 6,
            MaxNodes = 12,
            MaxDepth = 7,
            MaxLines = 3,
            MaxTextCharacters = 2,
            MaxShapedGlyphs = 3,
            MaxShapingCalls = 6
        }).Root);
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxBoxes = 5 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxNodes = 11 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxDepth = 6 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxLines = 2 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxTextCharacters = 1 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxShapedGlyphs = 2 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxShapingCalls = 5 }));
    }

    [Fact]
    public void TableIntrinsicMeasurementObservesCancellationBeforePublishingGeometry()
    {
        var doc = HtmlParser.Parse("<table><tr><td>one two</td></tr></table>", cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(doc, cancellationToken: Cancellation);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        Assert.Throws<OperationCanceledException>(() => StaticLayout.Layout(doc, styles,
            new CancelingShaper(cancellation), 100, 100, cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData("<table><tr><td colspan=2>x</td></tr></table>", "")]
    [InlineData("<table><tr><td rowspan=0>x</td></tr></table>", "")]
    [InlineData("<table><caption>Title</caption><tr><td>x</td></tr></table>", "")]
    [InlineData("<table><colgroup><col></colgroup><tr><td>x</td></tr></table>", "")]
    [InlineData("<table><tr><td><table></table></td></tr></table>", "")]
    [InlineData("<table><tr><td><input></td></tr></table>", "")]
    [InlineData("<table width=100><tr><td>x</td></tr></table>", "")]
    [InlineData("<table><tr><td>x</td></tr></table>", "td{width:50%}")]
    [InlineData("<table><tr><td>x</td></tr></table>", "td{vertical-align:baseline}")]
    [InlineData("<table><tr><td>x</td></tr></table>", "table{height:100px}")]
    [InlineData("<table><tr><td>x</td></tr></table>", "tr{background-color:red}")]
    public void UnsupportedTableFeaturesFailInsteadOfReturningApproximateGeometry(string html, string css) =>
        Assert.Throws<UnsupportedLayoutException>(() => UnorderedList(html, css));

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

    [Theory]
    [InlineData("nowrap")]
    [InlineData("pre")]
    public void NonWrappingTextPreservesUrlsAndInternalPunctuationWithoutSoftBreaks(string mode)
    {
        const string text = "well-known https://example.test/a?x=1";
        var box = Find(Layout("<p id=x>" + text + "</p>", "#x{white-space:" + mode + "}", width: 10).Root!, "x");
        var line = Assert.Single(box.Lines);
        Assert.Equal(text, string.Concat(line.Fragments.Select(fragment => fragment.Run.Text)));
        Assert.Equal(text.Length * 5, line.Fragments.Sum(fragment => fragment.Run.Width));
        Assert.Equal(10, box.Content.Height);
    }

    [Theory]
    [InlineData("nowrap", 1, "a-b c/d")]
    [InlineData("pre", 2, "  a-b  c/d  ")]
    public void NonWrappingPunctuationKeepsExistingWhitespaceAndForcedBreakSemantics(
        string mode, int lines, string expected)
    {
        var box = Find(Layout("<p id=x>  a-b \n c/d  </p>", "#x{white-space:" + mode + "}", width: 10).Root!, "x");
        Assert.Equal(lines, box.Lines.Count);
        var actual = string.Concat(box.Lines.SelectMany(line => line.Fragments).Select(fragment => fragment.Run.Text));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("normal", "aa-bb", 25, "aa-bb")]
    [InlineData("normal", "aa-bb", 24, "aa-|bb")]
    [InlineData("normal", "aa-bb", 15, "aa-|bb")]
    [InlineData("normal", "aa-bb", 10, "aa-|bb")]
    [InlineData("normal", "a-b-c", 20, "a-b-|c")]
    [InlineData("normal", "a-b-c", 10, "a-|b-|c")]
    [InlineData("normal", "x aa-bb", 25, "x aa-|bb")]
    [InlineData("normal", "x aa-bb", 20, "x|aa-|bb")]
    [InlineData("normal", "aaa b-c", 10, "aaa|b-|c")]
    [InlineData("normal", "aa-bb cc-dd", 25, "aa-bb|cc-dd")]
    [InlineData("normal", "aa-bb.", 15, "aa-|bb.")]
    [InlineData("pre-line", "a-b\nc-d", 10, "a-|b|c-|d")]
    public void InternalAsciiHyphensProvideSoftWrapOpportunities(
        string mode, string text, double width, string expected)
    {
        var box = Find(Layout("<p id=x>" + text + "</p>", "#x{white-space:" + mode + "}", width).Root!, "x");
        var actual = box.Lines.Select(line => string.Concat(line.Fragments.Select(fragment => fragment.Run.Text)));
        Assert.Equal(expected.Split('|'), actual);
        Assert.Equal(text.Replace("\n", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal),
            string.Concat(actual).Replace(" ", "", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnwrappedHyphenatedWordRemainsOneShapedRun()
    {
        var box = Find(Layout("<p id=x>aa-bb</p>", width: 25).Root!, "x");
        var fragment = Assert.Single(Assert.Single(box.Lines).Fragments);
        Assert.Equal("aa-bb", fragment.Run.Text);
        Assert.Equal(25, fragment.Run.Width);
    }

    [Fact]
    public void BrokenHyphenatedFragmentsRetainTheirSourceTextNode()
    {
        var (document, styles) = Page("<p id=x>aa-bb</p>");
        var source = Assert.IsType<DomText>(document.GetElementById("x")!.ChildNodes[0]);
        var box = Find(StaticLayout.Layout(document, styles, new MetricsShaper(), 15, 200,
            cancellationToken: Cancellation).Root!, "x");
        Assert.All(box.Lines.SelectMany(line => line.Fragments), fragment => Assert.Same(source, fragment.Source));
    }

    [Fact]
    public void HyphenatedTextContributesItsWidestUnbreakablePartToTableIntrinsicWidth()
    {
        var root = UnorderedList("<table id=t><tr><td id=c>aa-bb</td></tr></table>", width: 10).Root!;
        Assert.Equal(21, Find(root, "t").Content.Width);
        var cell = Find(root, "c");
        Assert.Equal(15, cell.Content.Width);
        Assert.Equal(["aa-", "bb"], cell.Lines.Select(line =>
            string.Concat(line.Fragments.Select(fragment => fragment.Run.Text))));
    }

    [Theory]
    [InlineData("-ab")]
    [InlineData("ab-")]
    [InlineData("a--b")]
    [InlineData("1-a")]
    [InlineData("a-1")]
    [InlineData("a-b/c")]
    [InlineData("a-<span>b</span>")]
    public void OtherHyphenAndCrossNodeCasesRemainExplicitlyUnsupported(string text) =>
        Assert.Throws<UnsupportedLayoutException>(() => Layout("<p>" + text + "</p>"));

    [Fact]
    public void HyphenBreakWorkAndExistingTextLimitsHaveExactSharedBoundaries()
    {
        var exact = new LayoutOptions
        {
            MaxTextCharacters = 5,
            MaxShapedGlyphs = 10,
            MaxShapingCalls = 5,
            MaxLines = 2,
            MaxLineBreakOperations = 17
        };
        Assert.NotNull(Layout("<p>aa-bb</p>", width: 15, options: exact).Root);
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb</p>", width: 15,
            options: exact with { MaxTextCharacters = 4 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb</p>", width: 15,
            options: exact with { MaxShapedGlyphs = 9 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb</p>", width: 15,
            options: exact with { MaxShapingCalls = 4 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb</p>", width: 15,
            options: exact with { MaxLines = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb</p>", width: 15,
            options: exact with { MaxLineBreakOperations = 16 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Layout("<p>aa-bb</p>", width: 15,
            options: exact with { MaxLineBreakOperations = 0 }));
        var shared = new LayoutOptions { MaxLineBreakOperations = 34 };
        Assert.NotNull(Layout("<p>aa-bb</p><p>aa-bb</p>", width: 15, options: shared).Root);
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb</p><p>aa-bb</p>", width: 15,
            options: shared with { MaxLineBreakOperations = 33 }));
    }

    [Fact]
    public void HyphenBreakSearchObservesCancellationFromTheShaper()
    {
        var (document, styles) = Page("<p>aa-bb</p>");
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => StaticLayout.Layout(document, styles,
            new CancelingShaper(cancellation), 15, 200, cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("pre-line")]
    public void WrappingPunctuationStillRequiresSupportedLineBreakAnalysis(string mode) =>
        Assert.Throws<UnsupportedLayoutException>(() =>
            Layout("<p>a-b/c</p>", "p{white-space:" + mode + "}"));

    [Theory]
    [InlineData("nowrap", "\u00AD")]
    [InlineData("pre", "\u00AD")]
    [InlineData("nowrap", "\u202E")]
    [InlineData("pre", "\u202E")]
    [InlineData("nowrap", "\u4E00")]
    [InlineData("pre", "\u4E00")]
    public void NonWrappingTextRetainsUnsupportedScriptControlAndSoftHyphenFailures(string mode, string text) =>
        Assert.Throws<UnsupportedTextException>(() =>
            Layout("<p>" + text + "</p>", "p{white-space:" + mode + "}"));

    [Fact]
    public void NonWrappingPunctuationHasIdenticalIntrinsicMinimumAndMaximumWidths()
    {
        var root = UnorderedList("<table id=t><tr><td id=c>a-b/c</td></tr></table>",
            "td{white-space:nowrap}", width: 10).Root!;
        Assert.Equal(31, Find(root, "t").Content.Width);
        Assert.Equal(25, Find(root, "c").Content.Width);
        Assert.Single(Find(root, "c").Lines);
    }

    [Theory]
    [InlineData("nowrap")]
    [InlineData("pre")]
    public void NonWrappingPunctuationStillChargesExactTextAndShapingLimits(string mode)
    {
        var css = "p{white-space:" + mode + "}";
        Assert.NotNull(Layout("<p>a-b/c</p>", css, options: new()
        {
            MaxTextCharacters = 5,
            MaxShapedGlyphs = 5,
            MaxShapingCalls = 2,
            MaxLines = 1
        }).Root);
        Assert.Throws<LayoutLimitException>(() => Layout("<p>a-b/c</p>", css,
            options: new() { MaxTextCharacters = 4 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>a-b/c</p>", css,
            options: new() { MaxShapedGlyphs = 4 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>a-b/c</p>", css,
            options: new() { MaxShapingCalls = 1 }));
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
    [InlineData("<p>-ab</p>", "")]
    [InlineData("<p lang=de>x</p>", "")]
    [InlineData("<span>x</span>", "span{background-color:red}")]
    [InlineData("<p>a <span>bb cc</span></p>", "span{white-space:nowrap}")]
    [InlineData("<img>", "")]
    [InlineData("<input>", "")]
    [InlineData("<button>x</button>", "")]
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
    [InlineData(" type=password")]
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

    private sealed class CancelingShaper(CancellationTokenSource cancellation) : ITextShaper
    {
        public ShapedRun Shape(string text, TextFontRequest request, CancellationToken cancellationToken = default)
        {
            var run = new MetricsShaper().Shape(text, request, cancellationToken);
            cancellation.Cancel();
            return run;
        }
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
