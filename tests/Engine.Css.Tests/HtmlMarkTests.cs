using VisualWeb.Engine.Css;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class HtmlMarkTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void MarkHighlightUsesTheHtmlDefaultsAndAuthorCascade()
    {
        var document = HtmlParser.Parse("<!doctype html><mark id=target>highlight</mark>",
            cancellationToken: Cancellation).Document;
        var mark = document.GetElementById("target")!;
        var defaults = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        var overridden = CssStyleEngine.Compute(document,
            [new("mark{background-color:blue;color:red}")], cancellationToken: Cancellation);
        var withoutUserAgent = CssStyleEngine.Compute(document, includeUserAgent: false,
            cancellationToken: Cancellation);

        Assert.Empty(defaults.Diagnostics);
        Assert.Empty(overridden.Diagnostics);
        Assert.Equal(new CssColor(255, 255, 0), defaults.Styles[mark]["background-color"]);
        Assert.Equal(new CssColor(0, 0, 0), defaults.Styles[mark]["color"]);
        Assert.Equal(new CssColor(0, 0, 255), overridden.Styles[mark]["background-color"]);
        Assert.Equal(new CssColor(255, 0, 0), overridden.Styles[mark]["color"]);
        Assert.Equal(new CssColor(0, 0, 0, 0), withoutUserAgent.Styles[mark]["background-color"]);
    }
}
