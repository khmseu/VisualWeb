using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class LegacyPreDefaultsTests
{
    [Theory]
    [InlineData("listing")]
    [InlineData("xmp")]
    [InlineData("plaintext")]
    public void DefaultsMatchPreAndCanBeDisabled(string tag)
    {
        var pre = Parse("<pre id=x>text</pre>");
        var legacy = Parse($"<{tag} id=x>text</{tag}>");
        CssStyleSource inheritedSize = new("body{font-size:23px}");
        foreach (var includeUserAgent in new[] { false, true })
        {
            var expected = Compute(pre, includeUserAgent, inheritedSize);
            var actual = Compute(legacy, includeUserAgent, inheritedSize);
            var style = actual.Styles[legacy.GetElementById("x")!];
            foreach (var property in new[] { "display", "margin-top", "margin-bottom", "white-space" })
            {
                Assert.Equal(expected.Styles[pre.GetElementById("x")!][property], style[property]);
            }
            Assert.Equal(
                Assert.IsType<CssFontFamilies>(expected.Styles[pre.GetElementById("x")!]["font-family"]).Names,
                Assert.IsType<CssFontFamilies>(style["font-family"]).Names);
            Assert.Equal(new CssLength(23, "px"), style["font-size"]);
            Assert.Equal(new CssKeyword(includeUserAgent ? "block" : "inline"), style["display"]);
            Assert.Equal(new CssKeyword(includeUserAgent ? "pre" : "normal"), style["white-space"]);
            Assert.Equal(new CssLength(includeUserAgent ? 23 : 0, "px"), style["margin-top"]);
            Assert.Equal(new CssLength(includeUserAgent ? 23 : 0, "px"), style["margin-bottom"]);
            Assert.Equal(new[] { includeUserAgent ? "monospace" : "serif" },
                Assert.IsType<CssFontFamilies>(style["font-family"]).Names);
        }
    }

    [Theory]
    [InlineData("listing", false)]
    [InlineData("listing", true)]
    [InlineData("xmp", false)]
    [InlineData("xmp", true)]
    [InlineData("plaintext", false)]
    [InlineData("plaintext", true)]
    public void DefaultsParticipateInCascadeAndHidden(string tag, bool includeUserAgent)
    {
        var document = Parse($"<{tag} id=x>text</{tag}>");
        var element = document.GetElementById("x")!;
        CssStyleSource user = new($"{tag}{{display:inline}}", CssOrigin.User);
        CssStyleSource author = new($"{tag}{{display:block}}");
        CssStyleSource userImportant = new($"{tag}{{display:inline-block!important}}", CssOrigin.User);
        CssStyleSource authorImportant = new($"{tag}{{display:block!important}}");
        var fallback = includeUserAgent ? "block" : "inline";
        AssertDisplay(fallback);
        AssertDisplay("inline", user);
        AssertDisplay("block", user, author);
        AssertDisplay("inline-block", userImportant, authorImportant);
        AssertDisplay(fallback, new CssStyleSource($"{tag}{{display:revert}}"));
        AssertDisplay("inline", user, new CssStyleSource($"{tag}{{display:revert}}"));
        element.SetAttribute("style", "display:inline");
        AssertDisplay("inline", author);
        AssertDisplay("block", authorImportant);
        element.SetAttribute("style", "display:revert");
        AssertDisplay("inline", user);
        element.RemoveAttribute("style");
        element.SetAttribute("hidden", "");
        AssertDisplay(includeUserAgent ? "none" : "inline");
        AssertDisplay("block", author);

        void AssertDisplay(string expected, params CssStyleSource[] sources) =>
            Assert.Equal(expected, Assert.IsType<CssKeyword>(Compute(document, includeUserAgent, sources).Styles[element]["display"]).Value);
    }

    private static DomDocument Parse(string html) =>
        HtmlParser.Parse("<!doctype html>" + html, cancellationToken: TestContext.Current.CancellationToken).Document;

    private static CssStyleResult Compute(DomDocument document, bool includeUserAgent, params CssStyleSource[] sources)
    {
        var result = CssStyleEngine.Compute(document, sources, includeUserAgent: includeUserAgent,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(result.Diagnostics);
        return result;
    }
}
