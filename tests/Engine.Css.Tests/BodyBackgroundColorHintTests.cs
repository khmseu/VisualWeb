using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class BodyBackgroundColorHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("red", 255, 0, 0)]
    [InlineData("#aBc", 170, 187, 204)]
    [InlineData("chucknorris", 192, 0, 0)]
    public void BodyBgcolorUsesTheLegacyColorPresentationalHint(string value, byte red, byte green, byte blue)
    {
        var (document, body) = Create();
        body.SetAttribute("bgcolor", value);

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(red, green, blue), result.Styles[body]["background-color"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("transparent")]
    public void InvalidBodyBgcolorAddsNoHint(string value)
    {
        var (document, body) = Create();
        body.SetAttribute("bgcolor", value);

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 0, 0), result.Styles[body]["background-color"]);
    }

    [Fact]
    public void BodyBgcolorUsesPresentationalHintCascadeAndRecomputesAfterMutation()
    {
        var (document, body) = Create();
        body.SetAttribute("bgcolor", "red");

        Assert.Equal(new CssColor(255, 0, 0), Background(document, body,
            new CssStyleSource("body{background-color:blue}", CssOrigin.User)));
        Assert.Equal(new CssColor(0, 128, 0), Background(document, body,
            new CssStyleSource("body{background-color:green}")));
        body.SetAttribute("style", "background-color:yellow");
        Assert.Equal(new CssColor(255, 255, 0), Background(document, body));

        body.RemoveAttribute("style");
        body.SetAttribute("bgcolor", "#abc");
        Assert.Equal(new CssColor(170, 187, 204), Background(document, body));
        body.RemoveAttribute("bgcolor");
        Assert.Equal(new CssColor(0, 0, 0, 0), Background(document, body));
    }

    [Fact]
    public void BodyBgcolorCountsRawAttributeCharactersAndAssignments()
    {
        var (document, body) = Create();
        body.SetAttribute("bgcolor", "red");
        var font = document.CreateElement("font");
        font.SetAttribute("color", "red");
        body.AppendChild(font);

        CssStyleResult Calculate(int characters, int assignments) =>
            CssStyleEngine.Compute(document, includeUserAgent: false,
                options: new()
                {
                    MaxBodyBackgroundColorAttributeCharacters = characters,
                    MaxAssignments = assignments
                },
                cancellationToken: Cancellation);

        Assert.Equal(new CssColor(255, 0, 0), Calculate(3, 2).Styles[body]["background-color"]);
        Assert.Throws<CssLimitException>(() => Calculate(2, 2));
        Assert.Throws<CssLimitException>(() => Calculate(3, 1));

        body.SetAttribute("bgcolor", "transparent");
        Assert.Throws<CssLimitException>(() => Calculate(10, 2));
        Assert.Equal(new CssColor(0, 0, 0, 0), Calculate(11, 1).Styles[body]["background-color"]);
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(0, 1));
    }

    [Fact]
    public void BodyBgcolorDoesNotApplyToOtherElements()
    {
        var (document, body) = Create();
        var div = document.CreateElement("div");
        div.SetAttribute("bgcolor", "red");
        body.AppendChild(div);

        var result = CssStyleEngine.Compute(document, includeUserAgent: false,
            options: new() { MaxBodyBackgroundColorAttributeCharacters = 1 }, cancellationToken: Cancellation);

        Assert.Equal(new CssColor(0, 0, 0, 0), result.Styles[div]["background-color"]);
    }

    private static (DomDocument Document, DomElement Body) Create()
    {
        var document = HtmlParser.Parse("<!doctype html><body id=target></body>",
            cancellationToken: Cancellation).Document;
        return (document, document.GetElementById("target")!);
    }

    private static CssValue Background(DomDocument document, DomElement body, params CssStyleSource[] sources) =>
        CssStyleEngine.Compute(document, sources, includeUserAgent: false, cancellationToken: Cancellation)
            .Styles[body]["background-color"];
}
