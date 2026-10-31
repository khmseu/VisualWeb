using VisualWeb.Engine.Dom;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class PhrasingFontDefaultsTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("big", 36)]
    [InlineData("small", 25)]
    public void RelativeSizeDefaultsApplyOnlyWithTheUserAgentSheet(string tag, double expectedSize)
    {
        var document = Parse("<div style='font-size:30px'><" + tag + " id=target><span id=child>x</span></" + tag + "></div>");
        var enabled = Compute(document, true);
        var disabled = Compute(document, false);

        AssertSize(enabled, document, "target", expectedSize);
        AssertSize(enabled, document, "child", expectedSize);
        AssertSize(disabled, document, "target", 30);
        AssertSize(disabled, document, "child", 30);
    }

    [Theory]
    [InlineData("cite")]
    [InlineData("dfn")]
    [InlineData("em")]
    [InlineData("i")]
    [InlineData("var")]
    public void ItalicDefaultsAndTheirInheritanceDependOnTheUserAgentSheet(string tag)
    {
        var document = Parse("<" + tag + " id=target><span id=child>x</span></" + tag + ">");
        var enabled = Compute(document, true);
        var disabled = Compute(document, false);

        foreach (var id in new[] { "target", "child" })
        {
            Assert.Equal(new CssKeyword("italic"), Style(enabled, document, id)["font-style"]);
            Assert.Equal(new CssKeyword("normal"), Style(disabled, document, id)["font-style"]);
        }
    }

    [Theory]
    [InlineData("code")]
    [InlineData("kbd")]
    [InlineData("samp")]
    [InlineData("tt")]
    public void MonospaceDefaultsAndTheirInheritanceDependOnTheUserAgentSheet(string tag)
    {
        var document = Parse("<" + tag + " id=target><span id=child>x</span></" + tag + ">");
        var enabled = Compute(document, true);
        var disabled = Compute(document, false);

        foreach (var id in new[] { "target", "child" })
        {
            AssertFamily(enabled, document, id, "monospace");
            AssertFamily(disabled, document, id, "serif");
        }
    }

    [Fact]
    public void NestedRelativeSizesUseTheImmediateParentsComputedSize()
    {
        var document = Parse("""
            <div style="font-size:30px">
              <big id=big><big id=bigbig><small id=bigbigsmall><span id=child>x</span></small></big>
                <small id=bigsmall>x</small></big>
              <small id=small><small id=smallsmall>x</small><big id=smallbig>x</big></small>
            </div>
            """);
        var enabled = Compute(document, true);
        var disabled = Compute(document, false);

        foreach (var (id, expected) in new (string, double)[]
        {
            ("big", 36), ("bigbig", 43.2), ("bigbigsmall", 36), ("child", 36),
            ("bigsmall", 30), ("small", 25), ("smallsmall", 25 / 1.2), ("smallbig", 30)
        })
        {
            AssertSize(enabled, document, id, expected);
            AssertSize(disabled, document, id, 30);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InheritanceUserAuthorInlineImportantAndRevertRespectTheUserAgentOption(bool includeUserAgent)
    {
        var document = Parse("""
            <div id=parent>
              <big id=big><span id=bigChild>x</span></big>
              <small id=small><span id=smallChild>x</span></small>
              <dfn id=dfn><span id=dfnChild>x</span></dfn>
              <tt id=tt><span id=ttChild>x</span></tt>
            </div>
            """);
        CssStyleSource parent = new("#parent{font-size:30px;font-style:oblique;font-family:sans-serif}");
        CssStyleSource user = new("big{font-size:22px}small{font-size:24px}"
            + "dfn{font-style:normal}tt{font-family:serif}", CssOrigin.User);
        CssStyleSource author = new("big{font-size:26px}small{font-size:28px}"
            + "dfn{font-style:italic}tt{font-family:monospace}");
        CssStyleSource inherit = new("big,small{font-size:inherit}dfn{font-style:inherit}tt{font-family:inherit}");
        CssStyleSource revert = new("big,small{font-size:revert}dfn{font-style:revert}tt{font-family:revert}");
        CssStyleSource importantUser = new("big{font-size:10px!important}small{font-size:11px!important}"
            + "dfn{font-style:normal!important}tt{font-family:sans-serif!important}", CssOrigin.User);
        CssStyleSource importantAuthor = new("big{font-size:14px!important}small{font-size:15px!important}"
            + "dfn{font-style:italic!important}tt{font-family:monospace!important}");

        AssertValues(Compute(document, includeUserAgent, parent), includeUserAgent ? 36 : 30,
            includeUserAgent ? 25 : 30, includeUserAgent ? "italic" : "oblique",
            includeUserAgent ? "monospace" : "sans-serif");
        AssertValues(Compute(document, includeUserAgent, parent, user), 22, 24, "normal", "serif");
        AssertValues(Compute(document, includeUserAgent, parent, user, author), 26, 28, "italic", "monospace");
        AssertValues(Compute(document, includeUserAgent, parent, user, importantUser, importantAuthor),
            10, 11, "normal", "sans-serif");
        AssertValues(Compute(document, includeUserAgent, parent, inherit), 30, 30, "oblique", "sans-serif");
        AssertValues(Compute(document, includeUserAgent, parent, user, revert), 22, 24, "normal", "serif");
        AssertValues(Compute(document, includeUserAgent, parent, revert), includeUserAgent ? 36 : 30,
            includeUserAgent ? 25 : 30, includeUserAgent ? "italic" : "oblique",
            includeUserAgent ? "monospace" : "sans-serif");

        document.GetElementById("big")!.SetAttribute("style", "font-size:18px");
        document.GetElementById("small")!.SetAttribute("style", "font-size:19px");
        document.GetElementById("dfn")!.SetAttribute("style", "font-style:normal");
        document.GetElementById("tt")!.SetAttribute("style", "font-family:sans-serif");
        AssertValues(Compute(document, includeUserAgent, parent, user, author), 18, 19, "normal", "sans-serif");

        void AssertValues(CssStyleResult result, double big, double small, string style, string family)
        {
            foreach (var id in new[] { "big", "bigChild" }) { AssertSize(result, document, id, big); }
            foreach (var id in new[] { "small", "smallChild" }) { AssertSize(result, document, id, small); }
            foreach (var id in new[] { "dfn", "dfnChild" })
            { Assert.Equal(new CssKeyword(style), Style(result, document, id)["font-style"]); }
            foreach (var id in new[] { "tt", "ttChild" }) { AssertFamily(result, document, id, family); }
        }
    }

    [Fact]
    public void RelativeDefaultsPreserveFiniteValueLimitsAndCancellation()
    {
        var document = Parse("<div style='font-size:1.6e308px'><big>x</big></div>");
        Assert.Empty(Compute(document, false).Diagnostics);
        Assert.Throws<CssLimitException>(() => Compute(document, true));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document,
            options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssStyleEngine.Compute(document, cancellationToken: canceled.Token));
    }

    private static DomDocument Parse(string html) =>
        HtmlParser.Parse("<!doctype html>" + html, cancellationToken: Cancellation).Document;

    private static CssStyleResult Compute(DomDocument document, bool includeUserAgent, params CssStyleSource[] sources)
    {
        var result = CssStyleEngine.Compute(document, sources, includeUserAgent: includeUserAgent,
            cancellationToken: Cancellation);
        Assert.Empty(result.Diagnostics);
        return result;
    }

    private static CssComputedStyle Style(CssStyleResult result, DomDocument document, string id) =>
        result.Styles[document.GetElementById(id)!];

    private static void AssertSize(CssStyleResult result, DomDocument document, string id, double expected) =>
        Assert.Equal(expected, Assert.IsType<CssLength>(Style(result, document, id)["font-size"]).Value, 9);

    private static void AssertFamily(CssStyleResult result, DomDocument document, string id, string expected) =>
        Assert.Equal([expected], Assert.IsType<CssFontFamilies>(Style(result, document, id)["font-family"]).Names);
}
