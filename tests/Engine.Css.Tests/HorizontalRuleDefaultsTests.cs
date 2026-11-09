using VisualWeb.Engine.Css;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class HorizontalRuleDefaultsTests
{
    [Fact]
    public void HorizontalRuleUsesStandardDisableableUaDefaults()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var document = HtmlParser.Parse("<!doctype html><hr id=rule>", cancellationToken: cancellation).Document;
        var rule = document.GetElementById("rule")!;

        var defaults = CssStyleEngine.Compute(document, cancellationToken: cancellation);
        Assert.Empty(defaults.Diagnostics);
        var style = defaults.Styles[rule];
        Assert.Equal(new CssColor(128, 128, 128), style["color"]);
        Assert.Equal(new CssKeyword("inset"), style["border-top-style"]);
        Assert.Equal(new CssKeyword("inset"), style["border-bottom-style"]);
        Assert.Equal(new CssLength(1, "px"), style["border-top-width"]);
        Assert.Equal(new CssLength(8, "px"), style["margin-top"]);
        Assert.Equal(new CssLength(8, "px"), style["margin-bottom"]);
        Assert.Equal(new CssKeyword("auto"), style["margin-left"]);
        Assert.Equal(new CssKeyword("auto"), style["margin-right"]);

        var overridden = CssStyleEngine.Compute(document,
            [new("hr{border-style:solid;margin:0;color:red}")], cancellationToken: cancellation);
        Assert.Empty(overridden.Diagnostics);
        Assert.Equal(new CssKeyword("solid"), overridden.Styles[rule]["border-top-style"]);
        Assert.Equal(new CssLength(0, "px"), overridden.Styles[rule]["margin-top"]);
        Assert.Equal(new CssColor(255, 0, 0), overridden.Styles[rule]["color"]);

        var withoutUa = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: cancellation);
        Assert.Empty(withoutUa.Diagnostics);
        Assert.Equal(new CssKeyword("none"), withoutUa.Styles[rule]["border-top-style"]);
        Assert.Equal(new CssLength(0, "px"), withoutUa.Styles[rule]["margin-top"]);
    }
}
