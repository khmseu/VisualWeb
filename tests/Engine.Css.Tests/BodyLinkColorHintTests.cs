using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class BodyLinkColorHintTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("red", 255, 0, 0)]
    [InlineData("#aBc", 170, 187, 204)]
    [InlineData("chucknorris", 192, 0, 0)]
    public void BodyLinkColorsStaticLinksAndTheirDescendants(string value, byte red, byte green, byte blue)
    {
        var document = Parse("""
            <body id=body link="VALUE" style="color:#123456">
              <a id=anchor href><span id=child>text</span></a>
              <area id=area href><a id=notlink>text</a>
              <div id=other>text</div>
            </body>
            """.Replace("VALUE", value, StringComparison.Ordinal));

        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);
        var expectedLinkColor = new CssColor(red, green, blue);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(expectedLinkColor, Style("anchor")["color"]);
        Assert.Equal(expectedLinkColor, Style("child")["color"]);
        Assert.Equal(expectedLinkColor, Style("area")["color"]);
        Assert.Equal(new CssColor(18, 52, 86), Style("body")["color"]);
        Assert.Equal(new CssColor(18, 52, 86), Style("notlink")["color"]);
        Assert.Equal(new CssColor(18, 52, 86), Style("other")["color"]);

        CssComputedStyle Style(string id) => result.Styles[document.GetElementById(id)!];
    }

    [Fact]
    public void BodyLinkHintUsesPresentationalHintCascadeAndAuthorOverride()
    {
        var document = Parse("<body link=red><a id=target href>text</a></body>");
        var link = document.GetElementById("target")!;
        var hintColor = new CssColor(255, 0, 0);

        Assert.Equal(hintColor, Compute().Styles[link]["color"]);
        Assert.Equal(hintColor, Compute(new CssStyleSource("a{color:blue}", CssOrigin.User)).Styles[link]["color"]);
        Assert.Equal(new CssColor(0, 128, 0),
            Compute(new CssStyleSource(":link{color:green}")).Styles[link]["color"]);
        link.SetAttribute("style", "color:yellow");
        Assert.Equal(new CssColor(255, 255, 0),
            Compute(new CssStyleSource(":link{color:green}")).Styles[link]["color"]);

        CssStyleResult Compute(params CssStyleSource[] sources) =>
            CssStyleEngine.Compute(document, sources, cancellationToken: Cancellation);
    }

    [Fact]
    public void BodyLinkMutationChangesOnlyNewStyleSnapshots()
    {
        var document = Parse("<body id=body><a id=target href>text</a></body>");
        var body = document.GetElementById("body")!;
        var link = document.GetElementById("target")!;
        body.SetAttribute("link", "red");
        var red = CssStyleEngine.Compute(document, cancellationToken: Cancellation);

        body.SetAttribute("link", "blue");
        var blue = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        body.RemoveAttribute("link");
        var normal = CssStyleEngine.Compute(document, cancellationToken: Cancellation);

        Assert.Equal(new CssColor(255, 0, 0), red.Styles[link]["color"]);
        Assert.Equal(new CssColor(0, 0, 255), blue.Styles[link]["color"]);
        Assert.Equal(new CssColor(0, 0, 238), normal.Styles[link]["color"]);
        Assert.Equal(new CssColor(255, 0, 0), red.Styles[link]["color"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("transparent")]
    public void EmptyAndTransparentBodyLinkValuesAddNoHint(string value)
    {
        var document = Parse("<body id=body><a id=target href>text</a></body>");
        document.GetElementById("body")!.SetAttribute("link", value);
        var result = CssStyleEngine.Compute(document, cancellationToken: Cancellation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(new CssColor(0, 0, 238), result.Styles[document.GetElementById("target")!]["color"]);
    }

    [Fact]
    public void BodyLinkAttributesHaveIndependentAggregateCharacterAndAssignmentLimits()
    {
        var document = Parse("<body id=body link=red><a id=first href>one</a><a id=second href>two</a></body>");
        document.DocumentElement!.AppendChild(CreateBody("transparent"));

        CssStyleResult Compute(int characters, int assignments) =>
            CssStyleEngine.Compute(document, includeUserAgent: false,
                options: new()
                {
                    MaxBodyLinkColorAttributeCharacters = characters,
                    MaxAssignments = assignments
                },
                cancellationToken: Cancellation);

        Assert.Throws<CssLimitException>(() => Compute(13, 2));
        Assert.Throws<CssLimitException>(() => Compute(14, 1));
        var exact = Compute(14, 2);
        Assert.Equal(new CssColor(255, 0, 0), exact.Styles[document.GetElementById("first")!]["color"]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CssStyleEngine.Compute(document, options: new() { MaxBodyLinkColorAttributeCharacters = 0 },
                cancellationToken: Cancellation));

        DomElement CreateBody(string value)
        {
            var body = document.CreateElement("body");
            body.SetAttribute("link", value);
            return body;
        }
    }

    [Fact]
    public void LinkAttributesOnOtherElementsAreIgnored()
    {
        var document = Parse("<body><div link=red><a id=target href>text</a></div></body>");
        var result = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);

        Assert.Equal(new CssColor(0, 0, 0), result.Styles[document.GetElementById("target")!]["color"]);
    }

    private static DomDocument Parse(string html) =>
        HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;
}
