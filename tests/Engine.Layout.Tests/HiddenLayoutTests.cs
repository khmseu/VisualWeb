using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Layout.Tests;

public sealed class HiddenLayoutTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void HiddenSubtreeIsAbsentEvenWhenDescendantsRequestDisplayAndUnsupportedLayout()
    {
        const string html = """
            <div id=gone hidden><span style="display:inline">Secret</span><embed hidden>
            <bdi>unsupported</bdi><p hidden=until-found>unrevealed</p><input name=x></div><p id=shown>AB</p>
            """;
        var root = Layout(html).Root!;
        Assert.Null(Find(root, "gone"));
        var shown = Assert.IsType<LayoutBox>(Find(root, "shown"));
        Assert.Equal(new LayoutRect(0, 0, 120, 10), shown.Content);
        Assert.Equal("AB", Assert.Single(Assert.Single(shown.Lines).Fragments).Run.Text);
    }

    [Fact]
    public void HiddenRootProducesNoBoxAndAuthorOverrideRestoresLayout()
    {
        Assert.Null(Layout("<html hidden><body><p>Secret</p></body></html>").Root);
        var visible = Layout("<html hidden style='display:block'><body><p id=shown>Shown</p></body></html>").Root;
        Assert.NotNull(Find(Assert.IsType<LayoutBox>(visible), "shown"));
    }

    [Fact]
    public void RecomputedStylesAddAndRemoveHiddenBoxesWithoutMutatingPriorSnapshots()
    {
        var (document, before) = Page("<div id=target><span>Visible</span></div>");
        var target = document.GetElementById("target")!;
        var initial = StaticLayout.Layout(document, before, new MetricsShaper(), 120, 200, cancellationToken: Cancellation);
        target.SetAttribute("hidden", "");
        var hidden = CssStyleEngine.Compute(document,
            [new("*{margin:0;font-size:10px;line-height:10px}")], cancellationToken: Cancellation);
        var suppressed = StaticLayout.Layout(document, hidden, new MetricsShaper(), 120, 200, cancellationToken: Cancellation);
        target.RemoveAttribute("hidden");
        var restored = CssStyleEngine.Compute(document,
            [new("*{margin:0;font-size:10px;line-height:10px}")], cancellationToken: Cancellation);
        var visible = StaticLayout.Layout(document, restored, new MetricsShaper(), 120, 200, cancellationToken: Cancellation);

        Assert.NotNull(Find(initial.Root!, "target"));
        Assert.Null(Find(suppressed.Root!, "target"));
        Assert.NotNull(Find(visible.Root!, "target"));
        Assert.Equal(Find(initial.Root!, "target")!.Content, Find(visible.Root!, "target")!.Content);
    }

    [Fact]
    public void HiddenListItemsAndTableRowsAndCellsMatchExplicitDisplayNone()
    {
        const string content = """
            <ol><li id=first>A</li><li @hidden value=99>Secret</li><li id=last>B</li></ol>
            <table><tbody><tr @hidden><td>Secret</td></tr><tr><td @hidden>Secret</td><td id=cell>C</td></tr></tbody></table>
            """;
        var actual = Layout(content.Replace("@hidden", "hidden", StringComparison.Ordinal)).Root!;
        var reference = Layout(content.Replace("@hidden", "style='display:none'", StringComparison.Ordinal)).Root!;
        foreach (var id in new[] { "first", "last", "cell" })
        {
            var actualBox = Assert.IsType<LayoutBox>(Find(actual, id));
            var expectedBox = Assert.IsType<LayoutBox>(Find(reference, id));
            Assert.Equal(expectedBox.Content, actualBox.Content);
            Assert.Equal(expectedBox.BorderBox, actualBox.BorderBox);
        }
        Assert.Equal("1.", Find(actual, "first")!.TextMarker!.Run.Text);
        Assert.Equal("2.", Find(actual, "last")!.TextMarker!.Run.Text);
        Assert.Equal(reference.Content, actual.Content);
    }

    [Theory]
    [InlineData("<div hidden=until-found>Text</div>")]
    [InlineData("<span hidden=UnTiL-FoUnD>Text</span>")]
    [InlineData("<input hidden=until-found>")]
    [InlineData("<table><tbody hidden=until-found><tr><td>Text</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr><td hidden=until-found>Text</td></tr></tbody></table>")]
    [InlineData("<html hidden=until-found><body>Text</body></html>")]
    [InlineData("<button>Shown<span hidden=until-found>Secret</span></button>")]
    [InlineData("<select><option hidden=until-found>Secret</option></select>")]
    [InlineData("<select><optgroup hidden=UnTiL-FoUnD label=group><option>Secret</option></optgroup></select>")]
    public void VisibleUntilFoundIsRejectedAtEveryRenderedBoundary(string html)
    {
        var exception = Assert.Throws<UnsupportedLayoutException>(() => Layout(html));
        Assert.Contains("until-found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UntilFoundRejectionUsesAsciiOnlyEvenWithoutUaStyles()
    {
        Assert.Throws<UnsupportedLayoutException>(() =>
            Layout("<div hidden=UNTIL-FOUND>Text</div>", includeUserAgent: false));
        var root = Layout("<div hidden='unt\u0131l-found' id=shown>Text</div>",
            includeUserAgent: false).Root!;
        Assert.NotNull(Find(root, "shown"));
        Assert.Null(Find(Layout("<div hidden='unt\u0131l-found' id=gone>Text</div>").Root!, "gone"));
    }

    [Theory]
    [InlineData("<embed hidden>")]
    [InlineData("<embed hidden=until-found>")]
    [InlineData("<button>Shown<embed hidden></button>")]
    public void VisibleHiddenEmbedKeepsItsSpecializedLayoutFailure(string html)
    {
        var exception = Assert.Throws<UnsupportedLayoutException>(() => Layout(html));
        Assert.Contains("<embed>", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<div hidden=until-found style='display:none'><embed hidden></div>")]
    [InlineData("<div hidden><p hidden=until-found>Text</p><embed hidden></div>")]
    [InlineData("<embed hidden style='display:none'>")]
    [InlineData("<button>Shown<span style='display:none' hidden=until-found>Secret</span></button>")]
    [InlineData("<select hidden><option hidden selected value=secret>Secret</option></select>")]
    [InlineData("<div style='display:none'><select><option hidden=until-found>Secret</option></select></div>")]
    public void ExistingDisplayNoneSubtreesRemainExempt(string html) =>
        Assert.NotNull(Layout(html).Root);

    [Theory]
    [InlineData("<select><option hidden selected value=secret>Secret</option><option>Visible</option></select>")]
    [InlineData("<select><optgroup hidden label=group><option selected>Secret</option></optgroup></select>")]
    [InlineData("<select><option style='display:none' value=secret>Secret</option></select>")]
    public void HiddenOptionsInVisibleAtomicSelectAreRejectedRatherThanChangingSelection(string html)
    {
        var exception = Assert.Throws<UnsupportedLayoutException>(() => Layout(html));
        Assert.Contains("select", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthorOverrideMakesHiddenWidgetDescendantsVisible()
    {
        Assert.NotNull(Layout("<button>Shown<span hidden style='display:inline'>Secret</span></button>").Root);
        Assert.NotNull(Layout("<select><option hidden style='display:inline' selected>Visible</option></select>").Root);
    }

    [Fact]
    public void HiddenSubtreesShareExactLayoutLimitsAndCancellation()
    {
        const string html = "<div hidden><span>ignored</span><embed hidden></div><p id=shown>AB</p>";
        var exact = new LayoutOptions
        {
            MaxBoxes = 3,
            MaxNodes = 4,
            MaxLines = 1,
            MaxTextCharacters = 2,
            MaxShapedGlyphs = 2,
            MaxShapingCalls = 2
        };
        Assert.NotNull(Layout(html, options: exact).Root);
        Assert.Throws<LayoutLimitException>(() => Layout(html, options: exact with { MaxBoxes = 2 }));
        Assert.Throws<LayoutLimitException>(() => Layout(html, options: exact with { MaxNodes = 3 }));
        Assert.Throws<LayoutLimitException>(() => Layout(html, options: exact with { MaxTextCharacters = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout(html, options: exact with { MaxShapedGlyphs = 1 }));
        Assert.Throws<LayoutLimitException>(() => Layout(html, options: exact with { MaxShapingCalls = 1 }));

        var (document, styles) = Page(html);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 120, 200, cancellationToken: canceled.Token));
    }

    [Fact]
    public void AtomicWidgetDescendantValidationSharesNodeDepthAndCancellationLimits()
    {
        const string html = "<button><span hidden>Secret</span><span>Shown</span></button>";
        var exact = new LayoutOptions { MaxNodes = 6, MaxDepth = 5 };
        Assert.NotNull(Layout(html, options: exact).Root);
        Assert.Throws<LayoutLimitException>(() => Layout(html, options: exact with { MaxNodes = 5 }));
        Assert.Throws<LayoutLimitException>(() => Layout(html, options: exact with { MaxDepth = 4 }));
        var (document, styles) = Page(html);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            StaticLayout.Layout(document, styles, new MetricsShaper(), 120, 200, cancellationToken: canceled.Token));
    }

    private static (DomDocument Document, CssStyleResult Styles) Page(string html, bool includeUserAgent = true)
    {
        var document = HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;
        var css = includeUserAgent ? "*{margin:0;font-size:10px;line-height:10px}"
            : "html,body,div{display:block}head{display:none}*{margin:0;font-size:10px;line-height:10px}";
        var styles = CssStyleEngine.Compute(document, [new(css)], includeUserAgent, cancellationToken: Cancellation);
        Assert.Empty(styles.Diagnostics);
        return (document, styles);
    }

    private static LayoutResult Layout(string html, bool includeUserAgent = true, LayoutOptions? options = null)
    {
        var (document, styles) = Page(html, includeUserAgent);
        return StaticLayout.Layout(document, styles, new MetricsShaper(), 120, 200, options, Cancellation);
    }

    private static LayoutBox? Find(LayoutBox root, string id) => root.Element.GetAttribute("id") == id ? root
        : root.Children.Select(child => Find(child, id)).FirstOrDefault(found => found is not null);

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
