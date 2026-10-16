using VisualWeb.Core.Url;
using VisualWeb.PageRendering;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ContentSecurityPolicyTests
{
    private static readonly BrowserUrl Document = BrowserUrl.Parse("https://example.test/page");

    [Fact]
    public void EmptyPolicyAllowsInlineAndExternalSources()
    {
        var policy = ContentSecurityPolicy.Parse([], []);

        Assert.True(policy.AllowsInlineScript());
        Assert.True(policy.AllowsInlineStyle());
        Assert.True(policy.AllowsScript(BrowserUrl.Parse("https://outside.test/app.js"), Document.Origin));
        Assert.True(policy.AllowsStyle(BrowserUrl.Parse("http://outside.test/site.css"), Document.Origin));
    }

    [Fact]
    public void SpecificDirectivesOverrideDefaultAndInlineRequiresNoApplicableDirective()
    {
        var diagnostics = new List<string>();
        var policy = ContentSecurityPolicy.Parse(
            ["default-src 'none'; script-src 'self' https://scripts.test; style-src https://styles.test"],
            diagnostics);

        Assert.False(policy.AllowsInlineScript());
        Assert.False(policy.AllowsInlineStyle());
        Assert.True(policy.AllowsScript(BrowserUrl.Parse("https://example.test/app.js"), Document.Origin));
        Assert.True(policy.AllowsScript(BrowserUrl.Parse("https://scripts.test/app.js"), Document.Origin));
        Assert.False(policy.AllowsScript(BrowserUrl.Parse("https://styles.test/app.js"), Document.Origin));
        Assert.True(policy.AllowsStyle(BrowserUrl.Parse("https://styles.test/site.css"), Document.Origin));
        Assert.False(policy.AllowsStyle(BrowserUrl.Parse("https://example.test/site.css"), Document.Origin));
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void NonceMustMatchExactlyInEveryApplicablePolicy()
    {
        var policy = ContentSecurityPolicy.Parse(
            ["script-src 'nonce-AbC+/=='", "default-src 'nonce-AbC+/=='"], []);

        Assert.True(policy.AllowsInlineScript("AbC+/=="));
        Assert.False(policy.AllowsInlineScript("abc+/=="));
        Assert.False(policy.AllowsInlineScript(null));
    }

    [Theory]
    [InlineData("sha256", "8Q56ftgtlXxkQr5RiA6Pw+WlboQEeqNCt3JbdRNi/+I=")]
    [InlineData("sha384", "sAr3Ml3K/YNdYoP1xtdZwYjiA62YQm+18yiR7XnBY95bi2OJlGYwhP+rtAeSYXIP")]
    [InlineData("sha512", "l5ALJocGkaIXY5pnKDowBSe2/3tDF8TeMWItw6b0vEO6hkP41bd2I/jQSyiDkAIMi+AweSqwtbnY9TWWTkEdaA==")]
    [InlineData("SHA512", "l5ALJocGkaIXY5pnKDowBSe2_3tDF8TeMWItw6b0vEO6hkP41bd2I_jQSyiDkAIMi-AweSqwtbnY9TWWTkEdaA==")]
    public void SupportedHashAlgorithmsMatchInlineText(string algorithm, string digest)
    {
        var policy = ContentSecurityPolicy.Parse([$"script-src '{algorithm}-{digest}'"], []);

        Assert.True(policy.AllowsInlineScript(null, "document.title='hashed'"));
    }

    [Fact]
    public void GrammaticalHashWithWrongDigestLengthDoesNotMatch()
    {
        var policy = ContentSecurityPolicy.Parse(["script-src 'sha256-YQ=='"], []);

        Assert.False(policy.AllowsInlineScript(null, "document.title='hashed'"));
    }

    [Fact]
    public void HashesMatchExactInlineContentAcrossIntersectingPolicies()
    {
        const string source = "document.title='hashed'";
        const string hash = "'sha256-8Q56ftgtlXxkQr5RiA6Pw+WlboQEeqNCt3JbdRNi/+I='";
        var policy = ContentSecurityPolicy.Parse([$"script-src {hash}", $"default-src {hash}"], []);

        Assert.True(policy.AllowsInlineScript(null, source));
        Assert.False(policy.AllowsInlineScript(null, source + " "));
    }

    [Fact]
    public void StyleSrcAttrOverridesStyleSrcAndUsesStyleSrcFallback()
    {
        var independent = ContentSecurityPolicy.Parse(
            ["style-src 'none'; style-src-attr 'unsafe-inline'"], []);
        var fallback = ContentSecurityPolicy.Parse(["style-src 'unsafe-inline'"], []);
        var restrictive = ContentSecurityPolicy.Parse(
            ["default-src 'unsafe-inline'; style-src 'unsafe-inline'; style-src-attr 'none'"], []);

        Assert.True(independent.AllowsInlineStyleAttribute("color:red"));
        Assert.False(independent.AllowsInlineStyle());
        Assert.True(fallback.AllowsInlineStyleAttribute("color:red"));
        Assert.False(restrictive.AllowsInlineStyleAttribute("color:red"));
    }

    [Fact]
    public void UnsafeInlineAllowsInlineElementsAndStyleAttributes()
    {
        var policy = ContentSecurityPolicy.Parse(["default-src 'unsafe-inline'"], []);

        Assert.True(policy.AllowsInlineScript());
        Assert.True(policy.AllowsInlineStyle());
        Assert.True(policy.AllowsInlineScript(null, "document.title='allowed'"));
        Assert.True(policy.AllowsInlineStyle(null, "p{color:red}"));
        Assert.True(policy.AllowsInlineStyleAttribute());
    }

    [Fact]
    public void UnsafeInlineIsOverriddenByNonceOrHashSourcesAndIntersectsPolicies()
    {
        const string source = "document.title='hashed'";
        const string hash = "'sha256-8Q56ftgtlXxkQr5RiA6Pw+WlboQEeqNCt3JbdRNi/+I='";
        var noncePolicy = ContentSecurityPolicy.Parse(
            ["script-src 'unsafe-inline' 'nonce-AbC+/=='", "style-src 'unsafe-inline' 'nonce-AbC+/=='"], []);
        var hashPolicy = ContentSecurityPolicy.Parse([$"script-src 'unsafe-inline' {hash}"], []);
        var intersectingPolicy = ContentSecurityPolicy.Parse(
            ["script-src 'unsafe-inline'", "script-src 'none'"], []);

        Assert.True(noncePolicy.AllowsInlineScript("AbC+/==", source));
        Assert.False(noncePolicy.AllowsInlineScript());
        Assert.False(noncePolicy.AllowsInlineStyle());
        Assert.False(noncePolicy.AllowsInlineScript(null, source));
        Assert.True(hashPolicy.AllowsInlineScript(null, source));
        Assert.False(hashPolicy.AllowsInlineScript(null, source + " "));
        Assert.False(intersectingPolicy.AllowsInlineScript(null, source));
        Assert.False(noncePolicy.AllowsInlineStyleAttribute());
    }

    [Theory]
    [InlineData("script-src 'nonce-'")]
    [InlineData("script-src 'nonce-a==='")]
    [InlineData("style-src 'nonce-a!'")]
    [InlineData("script-src 'sha1-YQ=='")]
    [InlineData("script-src 'sha256-a==='")]
    public void MalformedNonceOrHashSourcesFailVisibly(string field)
    {
        var error = Assert.Throws<PageNavigationException>(() => ContentSecurityPolicy.Parse([field], []));

        Assert.Contains("Content-Security-Policy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DataSchemeSourceAllowsDataStylesheetsButNotNetworkResources()
    {
        var policy = ContentSecurityPolicy.Parse(["default-src DATA:"], []);

        Assert.True(policy.AllowsStyle(BrowserUrl.Parse("data:text/css,p%7Bcolor%3Ared%7D"), Document.Origin));
        Assert.False(policy.AllowsStyle(BrowserUrl.Parse("https://example.test/site.css"), Document.Origin));
    }

    [Fact]
    public void HttpSchemeSourceAllowsOnlyHttpAndSecureUpgrade()
    {
        var httpPolicy = ContentSecurityPolicy.Parse(["style-src http:"], []);
        var httpsPolicy = ContentSecurityPolicy.Parse(["style-src https:"], []);

        Assert.True(httpPolicy.AllowsStyle(BrowserUrl.Parse("http://example.test/site.css"), Document.Origin));
        Assert.True(httpPolicy.AllowsStyle(BrowserUrl.Parse("https://example.test/site.css"), Document.Origin));
        Assert.False(httpPolicy.AllowsStyle(BrowserUrl.Parse("data:text/css,body{}"), Document.Origin));
        Assert.True(httpsPolicy.AllowsStyle(BrowserUrl.Parse("https://example.test/site.css"), Document.Origin));
        Assert.False(httpsPolicy.AllowsStyle(BrowserUrl.Parse("http://example.test/site.css"), Document.Origin));
    }

    [Fact]
    public void MultipleFieldsIntersect()
    {
        var policy = ContentSecurityPolicy.Parse(
            ["script-src 'self' https://scripts.test", "default-src https://scripts.test"], []);

        Assert.False(policy.AllowsScript(BrowserUrl.Parse("https://example.test/app.js"), Document.Origin));
        Assert.True(policy.AllowsScript(BrowserUrl.Parse("https://scripts.test/app.js"), Document.Origin));
    }

    [Fact]
    public void DuplicateSupportedDirectiveKeepsFirstAndUnsupportedDirectiveIsDiagnosed()
    {
        var diagnostics = new List<string>();
        var policy = ContentSecurityPolicy.Parse(
            ["SCRIPT-SRC 'self'; img-src 'none'; script-src https://outside.test"], diagnostics);

        Assert.True(policy.AllowsScript(BrowserUrl.Parse("https://example.test/app.js"), Document.Origin));
        Assert.False(policy.AllowsScript(BrowserUrl.Parse("https://outside.test/app.js"), Document.Origin));
        Assert.Contains(diagnostics, item => item.Contains("img-src", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Contains("Duplicate", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("script-src *")]
    [InlineData("script-src wss:")]
    [InlineData("script-src 'unsafe-eval'")]
    [InlineData("style-src https://example.test/path")]
    [InlineData("style-src ftp:")]
    [InlineData("style-src https://user@example.test")]
    [InlineData("style-src 'none' 'self'")]
    public void UnsupportedOrAmbiguousSourcesFailVisibly(string field)
    {
        var error = Assert.Throws<PageNavigationException>(() => ContentSecurityPolicy.Parse([field], []));
        Assert.Contains("Content-Security-Policy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParserLimitsFieldsCharactersDirectivesAndSources()
    {
        Assert.Throws<PageNavigationException>(() =>
            ContentSecurityPolicy.Parse(Enumerable.Repeat("default-src 'none'", 17).ToArray(), []));
        Assert.Throws<PageNavigationException>(() =>
            ContentSecurityPolicy.Parse(["default-src " + new string('x', 8192)], []));
        Assert.Throws<PageNavigationException>(() =>
            ContentSecurityPolicy.Parse([string.Join(';', Enumerable.Repeat("img-src 'none'", 65))], []));
        Assert.Throws<PageNavigationException>(() =>
            ContentSecurityPolicy.Parse(["script-src " + string.Join(' ', Enumerable.Repeat("'self'", 65))], []));
    }
}
