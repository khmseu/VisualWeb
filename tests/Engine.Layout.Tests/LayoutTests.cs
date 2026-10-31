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
    public void MenuItemsUseTheSameDefaultOutsideGeometryAsUnorderedLists()
    {
        const string items = "<li id=a>a b</li><li id=b></li>";
        var menu = UnorderedList("<menu id=list>" + items + "</menu>", width: 50).Root!;
        var unordered = UnorderedList("<ul id=list>" + items + "</ul>", width: 50).Root!;
        Assert.Equal(Find(unordered, "list").Content, Find(menu, "list").Content);
        foreach (var id in new[] { "a", "b" })
        {
            var actual = Find(menu, id);
            var expected = Find(unordered, id);
            Assert.Equal(expected.Content, actual.Content);
            Assert.Equal(expected.DiscMarker, actual.DiscMarker);
            Assert.Equal(expected.Lines.Count, actual.Lines.Count);
        }
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(menu, "a").DiscMarker);
        Assert.Equal(2, Find(menu, "a").Lines.Count);
        Assert.Equal(10, Find(menu, "b").Content.Height);
    }

    [Fact]
    public void NestedMenuAndUnorderedListsUseExistingDepthAndMarginRules()
    {
        var root = UnorderedList(
            "<menu><li id=a><ul><li id=b><menu><li id=c></li></menu></li></ul></li></menu>",
            width: 180).Root!;
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(root, "a").DiscMarker);
        Assert.Equal(new LayoutRect(72, 3, 3, 3), Find(root, "b").CircleMarker);
        Assert.Equal(new LayoutRect(112, 3, 3, 3), Find(root, "c").SquareMarker);
        Assert.Equal(10, Find(root, "c").Content.Height);
        var ordered = UnorderedList("<ol start=9><li id=a><menu><li id=b></li></menu></li><li id=c></li></ol>",
            width: 180).Root!;
        Assert.Equal("9.", Find(ordered, "a").TextMarker!.Run.Text);
        Assert.NotNull(Find(ordered, "b").CircleMarker);
        Assert.Equal("10.", Find(ordered, "c").TextMarker!.Run.Text);
    }

    [Fact]
    public void MenuNumericMarkersRetainIndependentCountersAndItemOverrides()
    {
        var root = UnorderedList("""
            <menu start=9 reversed type=square>
            <li id=first><menu><li id=nested></li><li id=nested-next></li></menu></li>
            <li id=reset value=4></li><li id=roman type=I></li><li id=hidden></li>
            <li id=suppressed></li><li id=last></li></menu>
            """, "menu{list-style-type:decimal-leading-zero}#hidden{display:none}#suppressed{list-style:none}").Root!;
        Assert.Equal("01.", Find(root, "first").TextMarker!.Run.Text);
        Assert.Equal("01.", Find(root, "nested").TextMarker!.Run.Text);
        Assert.Equal("02.", Find(root, "nested-next").TextMarker!.Run.Text);
        Assert.Equal("04.", Find(root, "reset").TextMarker!.Run.Text);
        Assert.Equal("V.", Find(root, "roman").TextMarker!.Run.Text);
        Assert.Null(TryFind(root, "hidden"));
        Assert.Null(Find(root, "suppressed").TextMarker);
        Assert.Equal("07.", Find(root, "last").TextMarker!.Run.Text);
        var typeIsNotAHint = UnorderedList("<menu type=square><li id=x></li></menu>").Root!;
        Assert.NotNull(Find(typeIsNotAHint, "x").DiscMarker);
    }

    [Theory]
    [InlineData("<menu style='display:inline'></menu>")]
    [InlineData("<menu style='display:inline'><li>x</li></menu>")]
    [InlineData("<menu><li type=triangle>x</li></menu>")]
    [InlineData("<dir><li>x</li></dir>")]
    [InlineData("<div><li>x</li></div>")]
    public void MenuSupportKeepsUnsupportedListContextsExplicit(string html) =>
        Assert.Throws<UnsupportedLayoutException>(() => UnorderedList(html));

    [Fact]
    public void MenuListScansUseExistingExactLimitsAndCancellation()
    {
        const string html = "<menu><li></li></menu>";
        Assert.NotNull(UnorderedList(html, options: new() { MaxBoxes = 4, MaxNodes = 5, MaxShapingCalls = 1 }).Root);
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxBoxes = 3 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxNodes = 4 }));
        Assert.Throws<LayoutLimitException>(() =>
            UnorderedList("<menu><li></li><li></li></menu>", options: new() { MaxShapingCalls = 1 }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var document = HtmlParser.Parse(html, cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        Assert.Throws<OperationCanceledException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100, cancellationToken: canceled.Token));
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
    public void CircleMarkersKeepOutsideGeometryForWrappedNestedAndEmptyItems()
    {
        var root = UnorderedList("<ul id=parent><li id=a>a b</li><li id=b></li></ul>",
            "#parent{list-style:circle}", width: 50).Root!;
        var first = Find(root, "a");
        var second = Find(root, "b");
        Assert.Equal(new LayoutRect(40, 0, 10, 20), first.Content);
        Assert.Equal(new LayoutRect(32, 3, 3, 3), first.CircleMarker);
        Assert.Equal(new LayoutRect(32, 23, 3, 3), second.CircleMarker);
        Assert.Null(first.DiscMarker);
        Assert.Null(first.SquareMarker);
        Assert.Null(first.TextMarker);
        Assert.Equal(2, first.Lines.Count);
        Assert.Equal("a b", first.Lines[0].Fragments[0].Source.Data);
        Assert.Equal(10, second.Content.Height);

        const string nestedHtml = "<ul id=outer-list><li id=outer><ul id=inner-list><li id=inner></li></ul></li></ul>";
        var nestedDefault = UnorderedList(nestedHtml, "#outer-list{list-style:square}").Root!;
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(nestedDefault, "outer").SquareMarker);
        Assert.Null(Find(nestedDefault, "inner").DiscMarker);
        Assert.Equal(new LayoutRect(72, 3, 3, 3), Find(nestedDefault, "inner").CircleMarker);
        var nestedCircle = UnorderedList(nestedHtml, "#outer-list,#inner-list{list-style-type:circle}").Root!;
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(nestedCircle, "outer").CircleMarker);
        Assert.Equal(new LayoutRect(72, 3, 3, 3), Find(nestedCircle, "inner").CircleMarker);
        Assert.Equal(10, Find(nestedCircle, "inner").Content.Height);
    }

    [Fact]
    public void CircleSuppressionAndHiddenItemsPreserveOrderedListNumbering()
    {
        var root = UnorderedList(
            "<ol start=5><li id=a></li><li id=b></li><li id=hidden></li><li id=block></li><li id=c></li></ol>",
            "#a{list-style:circle}#b{list-style:none}#hidden{display:none}#block{display:block;list-style:circle}").Root!;
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(root, "a").CircleMarker);
        Assert.Null(Find(root, "a").TextMarker);
        Assert.Null(Find(root, "b").CircleMarker);
        Assert.Null(Find(root, "b").TextMarker);
        Assert.Null(TryFind(root, "hidden"));
        Assert.Null(Find(root, "block").CircleMarker);
        Assert.Null(Find(root, "block").TextMarker);
        Assert.Null(Find(root, "c").CircleMarker);
        Assert.Equal("7.", Find(root, "c").TextMarker!.Run.Text);
    }

    [Fact]
    public void CircleMarkersShareShapingBudgetsAndCancellation()
    {
        const string html = "<ul><li></li><li></li></ul>";
        const string css = "ul{list-style:circle}";
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
        Assert.Equal(new LayoutRect(72, 3, 3, 3), Find(nested, "inner").CircleMarker);
        Assert.Equal(10, Find(nested, "inner").Content.Height);
        var wrapped = Find(UnorderedList("<ul><li id=x>a b</li></ul>", width: 50).Root!, "x");
        Assert.Equal(2, wrapped.Lines.Count);
        Assert.Equal(new LayoutRect(32, 3, 3, 3), wrapped.DiscMarker);
    }

    [Fact]
    public void NestedDefaultMarkersUseDiscCircleThenSquareWithoutChangingGeometry()
    {
        var root = UnorderedList(
            "<ul><li id=a><ul><li id=b><ul><li id=c><ul><li id=d></li></ul></li></ul></li></ul></li></ul>",
            width: 200).Root!;
        Assert.Equal(new LayoutRect(32, 3, 3, 3), Find(root, "a").DiscMarker);
        Assert.Equal(new LayoutRect(72, 3, 3, 3), Find(root, "b").CircleMarker);
        Assert.Equal(new LayoutRect(112, 3, 3, 3), Find(root, "c").SquareMarker);
        Assert.Equal(new LayoutRect(152, 3, 3, 3), Find(root, "d").SquareMarker);
        Assert.Equal(10, root.Content.Height);
    }

    [Fact]
    public void NestedUaListMarginsDoNotInsertExtraVerticalGaps()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><ul id=outer><li id=first>a<ul id=nested><li id=inner>b</li></ul></li><li id=next>c</li></ul>",
            cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document,
            [new("html,body{margin:0}*{font-size:10px;line-height:10px}")], cancellationToken: Cancellation);
        Assert.Empty(styles.Diagnostics);
        var root = StaticLayout.Layout(document, styles, new MetricsShaper(), 200, 200,
            cancellationToken: Cancellation).Root!;
        var first = Find(root, "first");
        var nested = Find(root, "nested");
        var inner = Find(root, "inner");
        var next = Find(root, "next");
        Assert.Equal(0, nested.Margin.Top);
        Assert.Equal(0, nested.Margin.Bottom);
        Assert.Equal(first.Lines[0].Bounds.Y + 10, inner.Content.Y);
        Assert.Equal(inner.Content.Y + 10, next.Content.Y);
        Assert.Equal(30, Find(root, "outer").Content.Height);
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
    [InlineData("<ol type=x><li>x</li></ol>")]
    [InlineData("<ol type=circle><li>x</li></ol>")]
    [InlineData("<ul type=1><li>x</li></ul>")]
    [InlineData("<ul type=a><li>x</li></ul>")]
    [InlineData("<ul type=i><li>x</li></ul>")]
    [InlineData("<ul type='circle '><li>x</li></ul>")]
    [InlineData("<ul type='di\u017fc'><li>x</li></ul>")]
    [InlineData("<ul><li type=x>x</li></ul>")]
    [InlineData("<dir><li>x</li></dir>")]
    [InlineData("<li>x</li>")]
    public void UnimplementedListKindsRemainExplicitFailures(string html) =>
        Assert.Throws<UnsupportedLayoutException>(() => UnorderedList(html));

    [Theory]
    [InlineData("<ol type=circle><li>x</li></ol>")]
    [InlineData("<ul type=triangle><li>x</li></ul>")]
    [InlineData("<ul type=A><li>x</li></ul>")]
    [InlineData("<ul type=I><li>x</li></ul>")]
    [InlineData("<ul><li type=x>x</li></ul>")]
    public void CircleCssDoesNotEnableUnsupportedLegacyHtmlTypeAttributes(string html) =>
        Assert.Throws<UnsupportedLayoutException>(() => UnorderedList(html, "ul,ol,li{list-style:circle}"));

    [Theory]
    [InlineData("<ul type=DISC><li id=x></li></ul>", "disc")]
    [InlineData("<ul type=CiRcLe><li id=x></li></ul>", "circle")]
    [InlineData("<ul type=square><li id=x></li></ul>", "square")]
    [InlineData("<ul type=none><li id=x></li></ul>", "none")]
    [InlineData("<ol><li id=x type=circle></li></ol>", "circle")]
    [InlineData("<ol><li id=x type=square></li></ol>", "square")]
    [InlineData("<ul><li id=x type=1></li></ul>", "decimal")]
    public void ListTypeHintsProduceTheCorrespondingMarkers(string html, string marker)
    {
        var item = Find(UnorderedList(html).Root!, "x");
        Assert.Equal(marker == "disc", item.DiscMarker is not null);
        Assert.Equal(marker == "circle", item.CircleMarker is not null);
        Assert.Equal(marker == "square", item.SquareMarker is not null);
        Assert.Equal(marker == "decimal", item.TextMarker is not null);
        if (marker == "decimal") { Assert.Equal("1.", item.TextMarker!.Run.Text); }
    }

    [Fact]
    public void ListTypeHintsPreserveNumberingAndAuthorOverrides()
    {
        var root = UnorderedList("""
            <ol start=5><li id=a type=circle></li><li id=b type=none></li>
            <li id=c type=1></li><li id=d></li></ol>
            """).Root!;
        Assert.NotNull(Find(root, "a").CircleMarker);
        Assert.Null(Find(root, "b").TextMarker);
        Assert.Null(Find(root, "b").DiscMarker);
        Assert.Null(Find(root, "b").CircleMarker);
        Assert.Null(Find(root, "b").SquareMarker);
        Assert.Equal("7.", Find(root, "c").TextMarker!.Run.Text);
        Assert.Equal("8.", Find(root, "d").TextMarker!.Run.Text);
        var overridden = UnorderedList("<ul type=square><li id=x></li></ul>",
            "ul{list-style-type:circle}").Root!;
        Assert.NotNull(Find(overridden, "x").CircleMarker);
        Assert.Null(Find(overridden, "x").SquareMarker);
    }

    [Fact]
    public void CircleCssDoesNotEnableArbitraryListItemElements() =>
        Assert.Throws<UnsupportedLayoutException>(() =>
            UnorderedList("<ul><div>x</div></ul>", "div{display:list-item;list-style:circle}"));

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
            <ol start=5><li id=a><ul><li id=circle></li></ul>
            <ol reversed><li id=nested></li><li></li></ol></li><li id=b></li></ol>
            <ul id=decimal><li value=8 id=c></li><li id=d></li></ul>
            """, "#decimal{list-style-type:decimal}").Root!;
        Assert.Equal("5.", Find(root, "a").TextMarker!.Run.Text);
        Assert.Equal("6.", Find(root, "b").TextMarker!.Run.Text);
        Assert.Equal("2.", Find(root, "nested").TextMarker!.Run.Text);
        Assert.NotNull(Find(root, "circle").CircleMarker);
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

    [Theory]
    [InlineData(1, "a.", "A.")]
    [InlineData(26, "z.", "Z.")]
    [InlineData(27, "aa.", "AA.")]
    [InlineData(52, "az.", "AZ.")]
    [InlineData(53, "ba.", "BA.")]
    [InlineData(702, "zz.", "ZZ.")]
    [InlineData(703, "aaa.", "AAA.")]
    [InlineData(3999, "ewu.", "EWU.")]
    [InlineData(4000, "ewv.", "EWV.")]
    [InlineData(2147483647, "fxshrxw.", "FXSHRXW.")]
    public void AlphabeticMarkersUseBijectiveBase26ForBothCases(int value, string lower, string upper)
    {
        var html = $"<ol start={value}><li id=x></li></ol>";
        var lowerRoot = UnorderedList(html, "ol{list-style-type:lower-alpha}").Root!;
        var upperRoot = UnorderedList(html, "ol{list-style-type:upper-alpha}").Root!;
        Assert.Equal(lower, Find(lowerRoot, "x").TextMarker!.Run.Text);
        Assert.Equal(upper, Find(upperRoot, "x").TextMarker!.Run.Text);
    }

    [Theory]
    [InlineData("lower-alpha", "aa.")]
    [InlineData("lower-latin", "aa.")]
    [InlineData("upper-alpha", "AA.")]
    [InlineData("upper-latin", "AA.")]
    public void AlphabeticAliasesShareOutsideGeometryAndSupportUnorderedItems(string style, string expected)
    {
        var root = UnorderedList("<ul><li id=x value=27></li></ul>", "ul{list-style:" + style + "}").Root!;
        var item = Find(root, "x");
        Assert.Equal(expected, item.TextMarker!.Run.Text);
        Assert.Equal(20, item.TextMarker.X);
        Assert.Equal(8, item.TextMarker.Baseline);
        Assert.Null(item.DiscMarker);
        Assert.Empty(item.Lines);
        Assert.Equal(10, item.Content.Height);
    }

    [Theory]
    [InlineData("-2147483648", "-2147483648.")]
    [InlineData("-2", "-2.")]
    [InlineData("0", "0.")]
    public void AlphabeticMarkersFallBackToDecimalForNonpositiveOrdinals(string start, string expected)
    {
        var lower = UnorderedList($"<ol type=a start={start}><li id=x></li></ol>").Root!;
        var upper = UnorderedList($"<ol type=A start={start}><li id=x></li></ol>").Root!;
        Assert.Equal(expected, Find(lower, "x").TextMarker!.Run.Text);
        Assert.Equal(expected, Find(upper, "x").TextMarker!.Run.Text);
    }

    [Theory]
    [InlineData("lower-alpha")]
    [InlineData("lower-latin")]
    [InlineData("upper-alpha")]
    [InlineData("upper-latin")]
    public void AlphabeticCssAliasesAlsoUseDecimalFallbackOnUnorderedItems(string style)
    {
        var root = UnorderedList("<ul><li value=0 id=zero></li><li value=-1 id=negative></li></ul>",
            "ul{list-style:" + style + "}").Root!;
        Assert.Equal("0.", Find(root, "zero").TextMarker!.Run.Text);
        Assert.Equal("-1.", Find(root, "negative").TextMarker!.Run.Text);
    }

    [Fact]
    public void AlphabeticHintsPreserveReversedNumberingOverridesAndNestedCounters()
    {
        var root = UnorderedList("""
            <ol type=A reversed start=29>
            <li id=first><ol type=a start=26><li id=nested></li><li id=nested-next></li></ol></li>
            <li id=lower type=a value=27></li><li id=hidden></li><li id=none></li>
            <li id=next></li><li id=decimal type=1 value=0></li><li id=negative></li></ol>
            """, "#hidden{display:none}#none{list-style:none}").Root!;
        Assert.Equal("AC.", Find(root, "first").TextMarker!.Run.Text);
        Assert.Equal("z.", Find(root, "nested").TextMarker!.Run.Text);
        Assert.Equal("aa.", Find(root, "nested-next").TextMarker!.Run.Text);
        Assert.Equal("aa.", Find(root, "lower").TextMarker!.Run.Text);
        Assert.Null(TryFind(root, "hidden"));
        Assert.Null(Find(root, "none").TextMarker);
        Assert.Equal("Y.", Find(root, "next").TextMarker!.Run.Text);
        Assert.Equal("0.", Find(root, "decimal").TextMarker!.Run.Text);
        Assert.Equal("-1.", Find(root, "negative").TextMarker!.Run.Text);
        var beyondInt = UnorderedList("<ol type=A start=2147483647><li></li><li id=x></li></ol>").Root!;
        Assert.Equal("FXSHRXX.", Find(beyondInt, "x").TextMarker!.Run.Text);
        var overridden = UnorderedList("<ol type=a><li id=x></li></ol>", "ol{list-style:upper-latin}").Root!;
        Assert.Equal("A.", Find(overridden, "x").TextMarker!.Run.Text);
    }

    [Fact]
    public void AlphabeticMarkersChargeExactTextGlyphAndShapingBudgets()
    {
        const string html = "<ol type=a start=27><li></li></ol>";
        Assert.NotNull(UnorderedList(html, options: new()
        {
            MaxTextCharacters = 5,
            MaxShapedGlyphs = 3,
            MaxShapingCalls = 1
        }).Root);
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxTextCharacters = 4 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxShapedGlyphs = 2 }));
        Assert.Throws<LayoutLimitException>(() =>
            UnorderedList("<ol type=a><li></li><li></li></ol>", options: new() { MaxShapingCalls = 1 }));
        const string large = "<ol type=a start=2147483647><li></li></ol>";
        Assert.NotNull(UnorderedList(large, options: new() { MaxTextCharacters = 18 }).Root);
        Assert.Throws<LayoutLimitException>(() => UnorderedList(large, options: new() { MaxTextCharacters = 17 }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var document = HtmlParser.Parse(html, cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        Assert.Throws<OperationCanceledException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100, cancellationToken: canceled.Token));
    }

    [Theory]
    [InlineData("-2147483648", "-2147483648.")]
    [InlineData("-11", "-11.")]
    [InlineData("-10", "-10.")]
    [InlineData("-9", "-9.")]
    [InlineData("-1", "-1.")]
    [InlineData("0", "00.")]
    [InlineData("1", "01.")]
    [InlineData("9", "09.")]
    [InlineData("10", "10.")]
    [InlineData("99", "99.")]
    [InlineData("100", "100.")]
    [InlineData("2147483647", "2147483647.")]
    public void LeadingZeroMarkersPadTheEntireRepresentationToTwoCharacters(string start, string expected)
    {
        var root = UnorderedList($"<ol start={start}><li id=x></li></ol>",
            "ol{list-style-type:decimal-leading-zero}").Root!;
        Assert.Equal(expected, Find(root, "x").TextMarker!.Run.Text);
    }

    [Fact]
    public void LeadingZeroMarkersCoverEveryValueAroundThePaddingBoundary()
    {
        var html = "<ol id=list start=-10>" + string.Concat(Enumerable.Repeat("<li></li>", 21)) + "</ol>";
        var list = Find(UnorderedList(html, "ol{list-style:decimal-leading-zero}").Root!, "list");
        string[] expected =
        [
            "-10.", "-9.", "-8.", "-7.", "-6.", "-5.", "-4.", "-3.", "-2.", "-1.",
            "00.", "01.", "02.", "03.", "04.", "05.", "06.", "07.", "08.", "09.", "10."
        ];
        Assert.Equal(expected, list.Children.Select(item => item.TextMarker!.Run.Text));
    }

    [Fact]
    public void LeadingZeroMarkersPreserveReversedAndNestedNumbering()
    {
        var root = UnorderedList("""
            <ol reversed start=2>
            <li id=first><ol><li id=nested></li><li id=nested-next></li></ol></li>
            <li id=zero value=0></li><li id=hidden></li><li id=none></li>
            <li id=negative></li><li id=reset value=9></li><li id=last></li></ol>
            """, "ol{list-style:decimal-leading-zero}#hidden{display:none}#none{list-style:none}").Root!;
        Assert.Equal("02.", Find(root, "first").TextMarker!.Run.Text);
        Assert.Equal("01.", Find(root, "nested").TextMarker!.Run.Text);
        Assert.Equal("02.", Find(root, "nested-next").TextMarker!.Run.Text);
        Assert.Equal("00.", Find(root, "zero").TextMarker!.Run.Text);
        Assert.Null(TryFind(root, "hidden"));
        Assert.Null(Find(root, "none").TextMarker);
        Assert.Equal("-2.", Find(root, "negative").TextMarker!.Run.Text);
        Assert.Equal("09.", Find(root, "reset").TextMarker!.Run.Text);
        Assert.Equal("08.", Find(root, "last").TextMarker!.Run.Text);
        var beyondInt = UnorderedList("<ol start=2147483647><li></li><li id=x></li></ol>",
            "ol{list-style-type:decimal-leading-zero}").Root!;
        Assert.Equal("2147483648.", Find(beyondInt, "x").TextMarker!.Run.Text);
    }

    [Fact]
    public void LeadingZeroStyleWorksOnUnorderedItemsWithOutsideTextGeometry()
    {
        var root = UnorderedList("<ul><li id=x value=9></li></ul>",
            "ul{list-style:decimal-leading-zero}").Root!;
        var item = Find(root, "x");
        Assert.Equal("09.", item.TextMarker!.Run.Text);
        Assert.Equal(20, item.TextMarker.X);
        Assert.Equal(8, item.TextMarker.Baseline);
        Assert.Null(item.DiscMarker);
        Assert.Empty(item.Lines);
        Assert.Equal(10, item.Content.Height);
    }

    [Theory]
    [InlineData("<ol type=decimal-leading-zero><li>x</li></ol>")]
    [InlineData("<ol><li type=decimal-leading-zero>x</li></ol>")]
    public void LeadingZeroCssDoesNotEnableUnsupportedHtmlTypeAttributes(string html) =>
        Assert.Throws<UnsupportedLayoutException>(() => UnorderedList(html, "ol,li{list-style:decimal-leading-zero}"));

    [Fact]
    public void LeadingZeroMarkersChargeExactTextGlyphAndShapingBudgets()
    {
        const string html = "<ol><li></li></ol>";
        const string css = "ol{list-style:decimal-leading-zero}";
        Assert.NotNull(UnorderedList(html, css, options: new()
        {
            MaxTextCharacters = 3,
            MaxShapedGlyphs = 3,
            MaxShapingCalls = 1
        }).Root);
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, css, options: new() { MaxTextCharacters = 2 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, css, options: new() { MaxShapedGlyphs = 2 }));
        Assert.Throws<LayoutLimitException>(() =>
            UnorderedList("<ol><li></li><li></li></ol>", css, options: new() { MaxShapingCalls = 1 }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var document = HtmlParser.Parse(html, cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document, [new(css)], cancellationToken: Cancellation);
        Assert.Throws<OperationCanceledException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100, cancellationToken: canceled.Token));
    }

    [Theory]
    [InlineData(1, "i.", "I.")]
    [InlineData(3, "iii.", "III.")]
    [InlineData(4, "iv.", "IV.")]
    [InlineData(5, "v.", "V.")]
    [InlineData(8, "viii.", "VIII.")]
    [InlineData(9, "ix.", "IX.")]
    [InlineData(10, "x.", "X.")]
    [InlineData(14, "xiv.", "XIV.")]
    [InlineData(19, "xix.", "XIX.")]
    [InlineData(40, "xl.", "XL.")]
    [InlineData(49, "xlix.", "XLIX.")]
    [InlineData(90, "xc.", "XC.")]
    [InlineData(99, "xcix.", "XCIX.")]
    [InlineData(400, "cd.", "CD.")]
    [InlineData(944, "cmxliv.", "CMXLIV.")]
    [InlineData(1949, "mcmxlix.", "MCMXLIX.")]
    [InlineData(1999, "mcmxcix.", "MCMXCIX.")]
    [InlineData(3888, "mmmdccclxxxviii.", "MMMDCCCLXXXVIII.")]
    [InlineData(3999, "mmmcmxcix.", "MMMCMXCIX.")]
    public void RomanMarkersUseThePredefinedAdditiveStyles(int value, string lower, string upper)
    {
        var html = $"<ol start={value}><li id=x></li></ol>";
        var lowerRoot = UnorderedList(html, "ol{list-style-type:lower-roman}").Root!;
        var upperRoot = UnorderedList(html, "ol{list-style-type:upper-roman}").Root!;
        Assert.Equal(lower, Find(lowerRoot, "x").TextMarker!.Run.Text);
        Assert.Equal(upper, Find(upperRoot, "x").TextMarker!.Run.Text);
    }

    [Theory]
    [InlineData("lower-roman", false)]
    [InlineData("upper-roman", true)]
    public void RomanMarkersMatchAnIndependentPlaceValueTableAcrossTheirEntireRange(string style, bool upper)
    {
        string[] thousands = ["", "M", "MM", "MMM"];
        string[] hundreds = ["", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM"];
        string[] tens = ["", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC"];
        string[] ones = ["", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"];
        var html = "<ol id=list>" + string.Concat(Enumerable.Repeat("<li></li>", 3999)) + "</ol>";
        var list = Find(UnorderedList(html, "ol{list-style-type:" + style + "}").Root!, "list");
        Assert.Equal(3999, list.Children.Count);
        for (var value = 1; value <= 3999; value++)
        {
            var expected = thousands[value / 1000] + hundreds[value / 100 % 10]
                + tens[value / 10 % 10] + ones[value % 10] + ".";
            Assert.Equal(upper ? expected : expected.ToLowerInvariant(), list.Children[value - 1].TextMarker!.Run.Text);
        }
    }

    [Theory]
    [InlineData("-2147483648", "-2147483648.")]
    [InlineData("-1", "-1.")]
    [InlineData("0", "0.")]
    [InlineData("4000", "4000.")]
    [InlineData("2147483647", "2147483647.")]
    public void RomanMarkersUseDecimalFallbackOutsideThePredefinedRange(string start, string expected)
    {
        var lower = UnorderedList($"<ol type=i start={start}><li id=x></li></ol>").Root!;
        var upper = UnorderedList($"<ol type=I start={start}><li id=x></li></ol>").Root!;
        Assert.Equal(expected, Find(lower, "x").TextMarker!.Run.Text);
        Assert.Equal(expected, Find(upper, "x").TextMarker!.Run.Text);
    }

    [Theory]
    [InlineData("lower-roman")]
    [InlineData("upper-roman")]
    public void RomanCssStylesUseDecimalFallbackOnUnorderedItems(string style)
    {
        var root = UnorderedList("<ul><li value=0 id=zero></li><li value=4000 id=large></li></ul>",
            "ul{list-style:" + style + "}").Root!;
        Assert.Equal("0.", Find(root, "zero").TextMarker!.Run.Text);
        Assert.Equal("4000.", Find(root, "large").TextMarker!.Run.Text);
    }

    [Fact]
    public void RomanHintsPreserveReversedNumberingOverridesAndNestedCounters()
    {
        var root = UnorderedList("""
            <ol type=I reversed start=11>
            <li id=first><ol type=i start=3><li id=nested></li><li id=nested-next></li></ol></li>
            <li id=lower type=i value=4></li><li id=hidden></li><li id=none></li>
            <li id=next></li><li id=alpha type=a value=27></li><li id=last></li></ol>
            """, "#hidden{display:none}#none{list-style:none}").Root!;
        Assert.Equal("XI.", Find(root, "first").TextMarker!.Run.Text);
        Assert.Equal("iii.", Find(root, "nested").TextMarker!.Run.Text);
        Assert.Equal("iv.", Find(root, "nested-next").TextMarker!.Run.Text);
        Assert.Equal("iv.", Find(root, "lower").TextMarker!.Run.Text);
        Assert.Null(TryFind(root, "hidden"));
        Assert.Null(Find(root, "none").TextMarker);
        Assert.Equal("II.", Find(root, "next").TextMarker!.Run.Text);
        Assert.Equal("aa.", Find(root, "alpha").TextMarker!.Run.Text);
        Assert.Equal("XXVI.", Find(root, "last").TextMarker!.Run.Text);
        var boundary = UnorderedList("<ol type=I start=3999><li id=a></li><li id=b></li></ol>").Root!;
        Assert.Equal("MMMCMXCIX.", Find(boundary, "a").TextMarker!.Run.Text);
        Assert.Equal("4000.", Find(boundary, "b").TextMarker!.Run.Text);
        var beyondInt = UnorderedList("<ol type=i start=2147483647><li></li><li id=x></li></ol>").Root!;
        Assert.Equal("2147483648.", Find(beyondInt, "x").TextMarker!.Run.Text);
        var overridden = UnorderedList("<ol type=i><li id=x></li></ol>", "ol{list-style:upper-roman}").Root!;
        Assert.Equal("I.", Find(overridden, "x").TextMarker!.Run.Text);
    }

    [Fact]
    public void RomanStyleWorksOnUnorderedItemsWithOutsideTextGeometry()
    {
        var root = UnorderedList("<ul><li id=x value=49></li></ul>", "ul{list-style:upper-roman}").Root!;
        var item = Find(root, "x");
        Assert.Equal("XLIX.", item.TextMarker!.Run.Text);
        Assert.Equal(10, item.TextMarker.X);
        Assert.Equal(8, item.TextMarker.Baseline);
        Assert.Null(item.DiscMarker);
        Assert.Empty(item.Lines);
        Assert.Equal(10, item.Content.Height);
    }

    [Fact]
    public void RomanMarkersChargeExactTextGlyphAndShapingBudgets()
    {
        const string html = "<ol type=I start=3888><li></li></ol>";
        Assert.NotNull(UnorderedList(html, options: new()
        {
            MaxTextCharacters = 20,
            MaxShapedGlyphs = 16,
            MaxShapingCalls = 1
        }).Root);
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxTextCharacters = 19 }));
        Assert.Throws<LayoutLimitException>(() => UnorderedList(html, options: new() { MaxShapedGlyphs = 15 }));
        Assert.Throws<LayoutLimitException>(() =>
            UnorderedList("<ol type=i><li></li><li></li></ol>", options: new() { MaxShapingCalls = 1 }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var document = HtmlParser.Parse(html, cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        Assert.Throws<OperationCanceledException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 100, 100, cancellationToken: canceled.Token));
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
    [InlineData("normal", "a/b", 10, "a/b")]
    [InlineData("normal", "and/or", 15, "and/or")]
    [InlineData("normal", "aa/bb", 25, "aa/bb")]
    [InlineData("normal", "aa/bb", 24, "aa/bb")]
    [InlineData("normal", "A/z", 10, "A/z")]
    [InlineData("normal", "a/b/c", 10, "a/b/c")]
    [InlineData("normal", "go and/or", 30, "go|and/or")]
    [InlineData("normal", "a-b/c", 10, "a-|b/c")]
    [InlineData("normal", "a/b-c", 20, "a/b-|c")]
    [InlineData("normal", "a/b-d/e", 20, "a/b-|d/e")]
    [InlineData("normal", "a/b.", 10, "a/b.")]
    [InlineData("pre-line", "a/b\nc/d", 10, "a/b|c/d")]
    public void InternalAsciiSolidusStaysUnbrokenInWrappingText(
        string mode, string text, double width, string expected)
    {
        var box = Find(Layout("<p id=x>" + text + "</p>", "#x{white-space:" + mode + "}", width).Root!, "x");
        var actual = box.Lines.Select(line => string.Concat(line.Fragments.Select(fragment => fragment.Run.Text)));
        Assert.Equal(expected.Split('|'), actual);
        Assert.Equal(text.Replace("\n", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal),
            string.Concat(actual).Replace(" ", "", StringComparison.Ordinal));
    }

    [Fact]
    public void SolidusWordOverflowsAsOneShapedRunAndRetainsItsSource()
    {
        var (document, styles) = Page("<p id=x>a/b</p>");
        var source = Assert.IsType<DomText>(document.GetElementById("x")!.ChildNodes[0]);
        var box = Find(StaticLayout.Layout(document, styles, new MetricsShaper(), 10, 200,
            cancellationToken: Cancellation).Root!, "x");
        var fragment = Assert.Single(Assert.Single(box.Lines).Fragments);
        Assert.Same(source, fragment.Source);
        Assert.Equal("a/b", fragment.Run.Text);
        Assert.Equal(15, fragment.Run.Width);
        Assert.Equal(10, box.Content.Width);
    }

    [Fact]
    public void MixedHyphenAndSolidusFragmentsRetainTheirSource()
    {
        var (document, styles) = Page("<p id=x>aa-bb/cc</p>");
        var source = Assert.IsType<DomText>(document.GetElementById("x")!.ChildNodes[0]);
        var box = Find(StaticLayout.Layout(document, styles, new MetricsShaper(), 25, 200,
            cancellationToken: Cancellation).Root!, "x");
        var fragments = box.Lines.SelectMany(line => line.Fragments).ToArray();
        Assert.Equal(["aa-", "bb/cc"], fragments.Select(fragment => fragment.Run.Text));
        Assert.All(fragments, fragment => Assert.Same(source, fragment.Source));
    }

    [Fact]
    public void SolidusWordContributesItsFullWidthToTableIntrinsicSizing()
    {
        var root = UnorderedList("<table id=t><tr><td id=c>a/b</td></tr></table>", width: 10).Root!;
        Assert.Equal(21, Find(root, "t").Content.Width);
        var cell = Find(root, "c");
        Assert.Equal(15, cell.Content.Width);
        Assert.Equal("a/b", Assert.Single(Assert.Single(cell.Lines).Fragments).Run.Text);

        var mixed = UnorderedList("<table id=t><tr><td id=c>a/b-c</td></tr></table>", width: 10).Root!;
        Assert.Equal(26, Find(mixed, "t").Content.Width);
        Assert.Equal(20, Find(mixed, "c").Content.Width);
        Assert.Equal(["a/b-", "c"], Find(mixed, "c").Lines.Select(line =>
            string.Concat(line.Fragments.Select(fragment => fragment.Run.Text))));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/a")]
    [InlineData("a/")]
    [InlineData("a//b")]
    [InlineData("1/a")]
    [InlineData("a/1")]
    [InlineData("a/\u00e9")]
    [InlineData("\u00e9/a")]
    [InlineData("a / b")]
    [InlineData("a/<span>b</span>")]
    [InlineData("a<span>/b</span>")]
    [InlineData("a<span>/</span>b")]
    public void OtherSolidusAndCrossNodeCasesRemainExplicitlyUnsupported(string text)
    {
        foreach (var mode in new[] { "normal", "pre-line" })
        {
            var error = Assert.Throws<UnsupportedLayoutException>(() =>
                Layout("<p>" + text + "</p>", "p{white-space:" + mode + "}"));
            Assert.Contains("solidus", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SolidusWordsRespectExactTextAndShapingLimitsWithoutBreakSearch()
    {
        var exact = new LayoutOptions
        {
            MaxTextCharacters = 3,
            MaxShapedGlyphs = 3,
            MaxShapingCalls = 2,
            MaxLines = 1,
            MaxLineBreakOperations = 1
        };
        Assert.NotNull(Layout("<p>a/b</p>", width: 10, options: exact).Root);
        Assert.Throws<LayoutLimitException>(() => Layout("<p>a/b</p>", width: 10,
            options: exact with { MaxTextCharacters = 2 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>a/b</p>", width: 10,
            options: exact with { MaxShapedGlyphs = 2 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>a/b</p>", width: 10,
            options: exact with { MaxShapingCalls = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Layout("<p>a/b</p>", width: 10,
            options: exact with { MaxLineBreakOperations = 0 }));
    }

    [Fact]
    public void MixedHyphenAndSolidusBreakSearchSharesExactLimits()
    {
        var exact = new LayoutOptions
        {
            MaxTextCharacters = 8,
            MaxShapedGlyphs = 16,
            MaxShapingCalls = 5,
            MaxLines = 2,
            MaxLineBreakOperations = 29
        };
        Assert.NotNull(Layout("<p>aa-bb/cc</p>", width: 25, options: exact).Root);
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb/cc</p>", width: 25,
            options: exact with { MaxTextCharacters = 7 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb/cc</p>", width: 25,
            options: exact with { MaxShapedGlyphs = 15 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb/cc</p>", width: 25,
            options: exact with { MaxShapingCalls = 4 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb/cc</p>", width: 25,
            options: exact with { MaxLines = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb/cc</p>", width: 25,
            options: exact with { MaxLineBreakOperations = 28 }));
        Assert.NotNull(Layout("<p>aa-bb/cc</p><p>aa-bb/cc</p>", width: 25,
            options: new() { MaxLineBreakOperations = 58 }).Root);
        Assert.Throws<LayoutLimitException>(() => Layout("<p>aa-bb/cc</p><p>aa-bb/cc</p>", width: 25,
            options: new() { MaxLineBreakOperations = 57 }));
    }

    [Fact]
    public void SolidusLayoutObservesCancellationFromTheShaper()
    {
        var (document, styles) = Page("<p>a/b</p>");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        Assert.Throws<OperationCanceledException>(() => StaticLayout.Layout(document, styles,
            new CancelingShaper(cancellation), 10, 200, cancellationToken: cancellation.Token));
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
    [InlineData("a-b//c")]
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
            Layout("<p>a-b//c</p>", "p{white-space:" + mode + "}"));

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
