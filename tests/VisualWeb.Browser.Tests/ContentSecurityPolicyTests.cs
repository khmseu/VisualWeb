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
    [InlineData("script-src https:")]
    [InlineData("script-src 'unsafe-inline'")]
    [InlineData("style-src https://example.test/path")]
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
