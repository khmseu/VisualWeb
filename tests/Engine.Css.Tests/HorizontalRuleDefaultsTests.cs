using VisualWeb.Engine.Css;
using VisualWeb.Engine.Html;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class HorizontalRuleDefaultsTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void HorizontalRuleUsesStandardDisableableUaDefaults()
    {
        var cancellation = Cancellation;
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

    [Fact]
    public void HorizontalRuleWidthAttributeMapsDimensionValueAndYieldsToAuthorCascade()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><hr id=rule width=' 35trailing'><p id=other width=20></p>",
            cancellationToken: Cancellation).Document;
        var rule = document.GetElementById("rule")!;
        var other = document.GetElementById("other")!;

        var hinted = CssStyleEngine.Compute(document, includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Equal(new CssLength(35, "px"), hinted.Styles[rule]["width"]);
        Assert.Equal(new CssKeyword("auto"), hinted.Styles[other]["width"]);

        var overridden = CssStyleEngine.Compute(document, [new("#rule{width:45px}")],
            includeUserAgent: false, cancellationToken: Cancellation);
        Assert.Equal(new CssLength(45, "px"), overridden.Styles[rule]["width"]);
    }

    [Fact]
    public void HorizontalRuleWidthAttributeHonorsInvalidValuesAssignmentAndAggregateLimits()
    {
        var oneRule = HtmlParser.Parse("<!doctype html><hr id=rule width=1>",
            cancellationToken: Cancellation).Document;
        var twoRules = HtmlParser.Parse("<!doctype html><hr width=1><hr width=2>",
            cancellationToken: Cancellation).Document;
        var invalidRule = HtmlParser.Parse("<!doctype html><hr width=invalid>",
            cancellationToken: Cancellation).Document;

        var exact = CssStyleEngine.Compute(oneRule, includeUserAgent: false,
            options: new() { MaxHorizontalRuleWidthAttributeCharacters = 1, MaxAssignments = 1 },
            cancellationToken: Cancellation);
        Assert.Equal(new CssLength(1, "px"), exact.Styles[oneRule.GetElementById("rule")!]["width"]);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoRules, includeUserAgent: false,
            options: new() { MaxHorizontalRuleWidthAttributeCharacters = 1 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoRules, includeUserAgent: false,
            options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(invalidRule, includeUserAgent: false,
            options: new() { MaxHorizontalRuleWidthAttributeCharacters = 6 },
            cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(oneRule, includeUserAgent: false,
            options: new() { MaxHorizontalRuleWidthAttributeCharacters = 0 },
            cancellationToken: Cancellation));
    }

    [Fact]
    public void HorizontalRuleAlignmentAttributeMapsSupportedValuesAndYieldsToAuthorCascade()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><hr id=left align=LEFT><hr id=right align=right>"
            + "<hr id=center align=center><hr id=invalid align=middle><hr id=unsupported align=justify>",
            cancellationToken: Cancellation).Document;
        var styles = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        Assert.Empty(styles.Diagnostics);

        var left = styles.Styles[document.GetElementById("left")!];
        Assert.Equal(new CssLength(0, "px"), left["margin-left"]);
        Assert.Equal(new CssKeyword("auto"), left["margin-right"]);

        var right = styles.Styles[document.GetElementById("right")!];
        Assert.Equal(new CssKeyword("auto"), right["margin-left"]);
        Assert.Equal(new CssLength(0, "px"), right["margin-right"]);

        foreach (var id in new[] { "center", "invalid", "unsupported" })
        {
            var style = styles.Styles[document.GetElementById(id)!];
            Assert.Equal(new CssKeyword("auto"), style["margin-left"]);
            Assert.Equal(new CssKeyword("auto"), style["margin-right"]);
        }

        var overridden = CssStyleEngine.Compute(document, [new("#left{margin-left:12px;margin-right:15px}")],
            cancellationToken: Cancellation);
        Assert.Equal(new CssLength(12, "px"), overridden.Styles[document.GetElementById("left")!]["margin-left"]);
        Assert.Equal(new CssLength(15, "px"), overridden.Styles[document.GetElementById("left")!]["margin-right"]);
    }

    [Fact]
    public void HorizontalRuleAlignmentAttributeCountsBothHintAssignments()
    {
        var document = HtmlParser.Parse("<!doctype html><hr align=left>",
            cancellationToken: Cancellation).Document;

        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(document,
            includeUserAgent: false, options: new() { MaxAssignments = 1 }, cancellationToken: Cancellation));
    }

    [Fact]
    public void HorizontalRuleColorAndNoShadeHintsSetSolidBordersAndLegacyColor()
    {
        var document = HtmlParser.Parse(
            "<!doctype html><hr id=color color=red><hr id=noshade noshade>"
            + "<hr id=empty color=''><hr id=transparent color=transparent><hr id=plain>",
            cancellationToken: Cancellation).Document;
        Assert.Equal(string.Empty, document.GetElementById("noshade")!.GetAttribute("noshade"));
        var styles = CssStyleEngine.Compute(document, cancellationToken: Cancellation);
        Assert.Empty(styles.Diagnostics);

        var color = styles.Styles[document.GetElementById("color")!];
        Assert.Equal(new CssColor(255, 0, 0), color["color"]);
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        { Assert.Equal(new CssKeyword("solid"), color[$"border-{side}-style"]); }

        var noShade = styles.Styles[document.GetElementById("noshade")!];
        Assert.Equal(new CssColor(128, 128, 128), noShade["color"]);
        Assert.Equal(new CssKeyword("solid"), noShade["border-top-style"]);

        foreach (var id in new[] { "empty", "transparent" })
        {
            var rule = styles.Styles[document.GetElementById(id)!];
            Assert.Equal(new CssColor(128, 128, 128), rule["color"]);
            Assert.Equal(new CssKeyword("solid"), rule["border-top-style"]);
        }

        var plain = styles.Styles[document.GetElementById("plain")!];
        Assert.Equal(new CssColor(128, 128, 128), plain["color"]);
        Assert.Equal(new CssKeyword("inset"), plain["border-top-style"]);

        var overridden = CssStyleEngine.Compute(document,
            [new("#color{color:blue;border-style:inset}")], cancellationToken: Cancellation);
        var overriddenRule = overridden.Styles[document.GetElementById("color")!];
        Assert.Equal(new CssColor(0, 0, 255), overriddenRule["color"]);
        Assert.Equal(new CssKeyword("inset"), overriddenRule["border-top-style"]);
    }

    [Fact]
    public void HorizontalRuleColorHintUsesAggregateAndAssignmentLimits()
    {
        var oneRule = HtmlParser.Parse("<!doctype html><hr id=rule color=red>",
            cancellationToken: Cancellation).Document;
        var twoRules = HtmlParser.Parse("<!doctype html><hr color=red><hr color=blue>",
            cancellationToken: Cancellation).Document;
        var transparentRule = HtmlParser.Parse("<!doctype html><hr color=transparent>",
            cancellationToken: Cancellation).Document;

        Assert.Equal(new CssColor(255, 0, 0),
            CssStyleEngine.Compute(oneRule, includeUserAgent: false,
                options: new() { MaxHorizontalRuleColorAttributeCharacters = 3, MaxAssignments = 5 },
                cancellationToken: Cancellation).Styles[oneRule.GetElementById("rule")!]["color"]);
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(twoRules, includeUserAgent: false,
            options: new() { MaxHorizontalRuleColorAttributeCharacters = 6 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(transparentRule, includeUserAgent: false,
            options: new() { MaxHorizontalRuleColorAttributeCharacters = 10 },
            cancellationToken: Cancellation));
        Assert.Throws<CssLimitException>(() => CssStyleEngine.Compute(oneRule, includeUserAgent: false,
            options: new() { MaxAssignments = 4 }, cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssStyleEngine.Compute(oneRule, includeUserAgent: false,
            options: new() { MaxHorizontalRuleColorAttributeCharacters = 0 },
            cancellationToken: Cancellation));
    }
}
