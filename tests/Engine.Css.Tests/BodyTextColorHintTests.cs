using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class BodyTextColorHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("red", 255, 0, 0)]
    [InlineData("#aBc", 170, 187, 204)]
    [InlineData("chucknorris", 192, 0, 0)]
    public void BodyTextUsesTheLegacyColorPresentationalHint(string value, byte red, byte green, byte blue)
    {
        var (document, body) = Create();
        var child = document.CreateElement("span");
        body.AppendChild(child);
        body.SetAttribute("text", value);

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue), result.Styles[body]["color"]);
        Assert.Equal(new CssColor(red, green, blue), result.Styles[child]["color"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("transparent")]
    public void InvalidBodyTextAddsNoHint(string value)
    {
        var (document, body) = Create();
        body.SetAttribute("text", value);

        var result = CssStyleEngine.Compute(document,
            [new CssStyleSource("html{color:#123456}")], includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(18, 52, 86), result.Styles[body]["color"]);
    }

    [Fact]
    public void BodyTextHintUsesCascadeInheritanceAndRecomputesAfterMutation()
    {
        var (document, body) = Create();
        var child = document.CreateElement("span");
        body.AppendChild(child);
        body.SetAttribute("text", "red");

        Assert.Equal(new CssColor(255, 0, 0), Color(document, body, child,
            new CssStyleSource("body{color:blue}", CssOrigin.User)));
        Assert.Equal(new CssColor(0, 128, 0), Color(document, body, child,
            new CssStyleSource("body{color:green}")));
        body.SetAttribute("style", "color:yellow");
        Assert.Equal(new CssColor(255, 255, 0), Color(document, body, child));

        body.RemoveAttribute("style");
        body.SetAttribute("text", "#abc");
        Assert.Equal(new CssColor(170, 187, 204), Color(document, body, child));
        body.RemoveAttribute("text");
        Assert.Equal(new CssColor(0, 0, 0), Color(document, body, child));
    }

    [Fact]
    public void BodyTextCountsAggregateRawAttributeCharactersAndAssignments()
    {
        var (document, body) = Create();
        body.SetAttribute("text", "red");
        var secondBody = document.CreateElement("body");
        secondBody.SetAttribute("text", "blue");
        body.AppendChild(secondBody);

        CssStyleResult Calculate(int characters, int assignments) =>
            CssStyleEngine.Compute(document, includeUserAgent: false,
                options: new()
                {
                    MaxBodyTextColorAttributeCharacters = characters,
                    MaxAssignments = assignments
                },
                cancellationToken: Cancellation);

        Assert.Equal(new CssColor(255, 0, 0), Calculate(7, 2).Styles[body]["color"]);
        Assert.Equal(new CssColor(0, 0, 255), Calculate(7, 2).Styles[secondBody]["color"]);
        Assert.Throws<CssLimitException>(() => Calculate(6, 2));
        Assert.Throws<CssLimitException>(() => Calculate(7, 1));

        secondBody.SetAttribute("text", "transparent");
        Assert.Throws<CssLimitException>(() => Calculate(13, 1));
        Assert.Equal(new CssColor(255, 0, 0), Calculate(14, 1).Styles[secondBody]["color"]);
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(0, 1));
    }

    [Fact]
    public void BodyTextDoesNotApplyToOtherElements()
    {
        var (document, body) = Create();
        var div = document.CreateElement("div");
        div.SetAttribute("text", "red");
        body.AppendChild(div);

        var result = CssStyleEngine.Compute(document, includeUserAgent: false,
            options: new() { MaxBodyTextColorAttributeCharacters = 1 }, cancellationToken: Cancellation);

        Assert.Equal(new CssColor(0, 0, 0), result.Styles[div]["color"]);
    }

    private static (DomDocument Document, DomElement Body) Create()
    {
        var document = HtmlParser.Parse("<!doctype html><body id=target></body>",
            cancellationToken: Cancellation).Document;
        return (document, document.GetElementById("target")!);
    }

    private static CssValue Color(DomDocument document, DomElement body, DomElement child,
        params CssStyleSource[] sources)
    {
        var result = CssStyleEngine.Compute(document, sources, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Equal(result.Styles[body]["color"], result.Styles[child]["color"]);
        return result.Styles[body]["color"];
    }
}
