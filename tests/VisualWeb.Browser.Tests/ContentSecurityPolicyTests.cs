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
    public void ScriptSrcElemOverridesScriptSrcAndUsesScriptSrcFallback()
    {
        const string source = "document.title='allowed'";
        var specific = ContentSecurityPolicy.Parse(
            ["script-src 'none'; script-src-elem 'unsafe-inline' https://scripts.test"], []);
        var fallback = ContentSecurityPolicy.Parse(["script-src https://scripts.test"], []);
        var defaultFallback = ContentSecurityPolicy.Parse(["default-src https://scripts.test"], []);
        var restricted = ContentSecurityPolicy.Parse(
            ["default-src 'unsafe-inline'; script-src 'unsafe-inline'; script-src-elem 'none'"], []);

        Assert.True(specific.AllowsInlineScript(null, source));
        Assert.True(specific.AllowsScript(BrowserUrl.Parse("https://scripts.test/app.js"), Document.Origin));
        Assert.False(specific.AllowsScript(BrowserUrl.Parse("https://example.test/app.js"), Document.Origin));
        Assert.True(fallback.AllowsScript(BrowserUrl.Parse("https://scripts.test/app.js"), Document.Origin));
        Assert.True(defaultFallback.AllowsScript(BrowserUrl.Parse("https://scripts.test/app.js"), Document.Origin));
        Assert.False(restricted.AllowsInlineScript(null, source));
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
    public void StyleSrcElemOverridesStyleSrcAndUsesFallbacks()
    {
        var specific = ContentSecurityPolicy.Parse(
            ["default-src 'none'; style-src 'none'; style-src-elem 'unsafe-inline' 'self' https://styles.test"], []);
        var styleFallback = ContentSecurityPolicy.Parse(["style-src https://styles.test"], []);
        var defaultFallback = ContentSecurityPolicy.Parse(["default-src https://styles.test"], []);

        Assert.True(specific.AllowsInlineStyle(null, "p{color:red}"));
        Assert.True(specific.AllowsStyle(Document, Document.Origin));
        Assert.True(specific.AllowsStyle(BrowserUrl.Parse("https://styles.test/site.css"), Document.Origin));
        Assert.False(specific.AllowsStyle(BrowserUrl.Parse("https://outside.test/site.css"), Document.Origin));
        Assert.False(specific.AllowsInlineStyleAttribute("color:red"));
        Assert.False(styleFallback.AllowsInlineStyle(null, "p{color:red}"));
        Assert.True(styleFallback.AllowsStyle(BrowserUrl.Parse("https://styles.test/site.css"), Document.Origin));
        Assert.False(defaultFallback.AllowsInlineStyle(null, "p{color:red}"));
        Assert.True(defaultFallback.AllowsStyle(BrowserUrl.Parse("https://styles.test/site.css"), Document.Origin));
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
    public void UpgradeInsecureRequestsRewritesOnlyHttpStylesheetUrlsAndPreservesComponents()
    {
        var policy = ContentSecurityPolicy.Parse(
            ["upgrade-insecure-requests; style-src 'self'"], []);
        var insecure = BrowserUrl.Parse("http://example.test:8080/site.css?theme=dark#section");
        var secure = BrowserUrl.Parse("https://example.test:8080/site.css?theme=dark#section");
        var data = BrowserUrl.Parse("data:text/css,p%7Bcolor%3Ared%7D");

        Assert.True(policy.UpgradesInsecureRequests);
        Assert.Equal(secure.Href, policy.UpgradeStyleUrl(insecure).Href);
        Assert.Same(secure, policy.UpgradeStyleUrl(secure));
        Assert.Same(data, policy.UpgradeStyleUrl(data));
        Assert.True(policy.AllowsStyle(secure, BrowserUrl.Parse("http://example.test:8080/").Origin));
        Assert.False(ContentSecurityPolicy.Parse(["style-src 'self'"], []).UpgradesInsecureRequests);
    }

    [Fact]
    public void DuplicateUpgradeInsecureRequestsDirectiveIsDiagnosed()
    {
        var diagnostics = new List<string>();
        var policy = ContentSecurityPolicy.Parse(["upgrade-insecure-requests; UPGRADE-INSECURE-REQUESTS"], diagnostics);

        Assert.True(policy.UpgradesInsecureRequests);
        Assert.Contains(diagnostics, item => item.Contains("Duplicate", StringComparison.Ordinal));
    }

    [Fact]
    public void FormActionUsesOnlyItsOwnSourceListAndIntersectsPolicies()
    {
        var document = BrowserUrl.Parse("https://example.test/");
        var sameOrigin = BrowserUrl.Parse("https://example.test/search");
        var allowedHost = BrowserUrl.Parse("https://forms.test/search");
        var otherHost = BrowserUrl.Parse("https://outside.test/search");

        Assert.True(ContentSecurityPolicy.Parse(["form-action 'self'"], []).AllowsFormAction(sameOrigin, document.Origin));
        Assert.False(ContentSecurityPolicy.Parse(["form-action 'self'"], []).AllowsFormAction(allowedHost, document.Origin));
        Assert.True(ContentSecurityPolicy.Parse(["form-action https://forms.test"], []).AllowsFormAction(allowedHost, document.Origin));
        Assert.False(ContentSecurityPolicy.Parse(["form-action https://forms.test"], []).AllowsFormAction(otherHost, document.Origin));
        Assert.True(ContentSecurityPolicy.Parse(["default-src 'none'"], []).AllowsFormAction(otherHost, document.Origin));
        Assert.False(ContentSecurityPolicy.Parse(["form-action https:", "form-action 'self'"], [])
            .AllowsFormAction(allowedHost, document.Origin));
    }

    [Fact]
    public void BaseUriUsesOnlyItsOwnSourceListAndIntersectsPolicies()
    {
        var document = BrowserUrl.Parse("https://example.test/");
        var sameOrigin = BrowserUrl.Parse("https://example.test/assets/");
        var allowedHost = BrowserUrl.Parse("https://base.test/assets/");
        var otherHost = BrowserUrl.Parse("https://outside.test/assets/");

        Assert.True(ContentSecurityPolicy.Parse(["base-uri 'self'"], []).HasBaseUri);
        Assert.True(ContentSecurityPolicy.Parse(["base-uri 'self'"], []).AllowsBaseUri(sameOrigin, document.Origin));
        Assert.False(ContentSecurityPolicy.Parse(["base-uri 'self'"], []).AllowsBaseUri(allowedHost, document.Origin));
        Assert.True(ContentSecurityPolicy.Parse(["base-uri https://base.test"], []).AllowsBaseUri(allowedHost, document.Origin));
        Assert.False(ContentSecurityPolicy.Parse(["base-uri https://base.test"], []).AllowsBaseUri(otherHost, document.Origin));
        Assert.True(ContentSecurityPolicy.Parse(["default-src 'none'"], []).AllowsBaseUri(otherHost, document.Origin));
        var intersected = ContentSecurityPolicy.Parse(
            ["base-uri 'self' https://base.test", "base-uri https://base.test"], []);
        Assert.True(intersected.AllowsBaseUri(allowedHost, document.Origin));
        Assert.False(intersected.AllowsBaseUri(sameOrigin, document.Origin));
        Assert.False(ContentSecurityPolicy.Parse(["base-uri"], []).AllowsBaseUri(sameOrigin, document.Origin));
    }

    [Fact]
    public void WildcardHostSourcesMatchSubdomainsButNotApexOrLookalikes()
    {
        var policy = ContentSecurityPolicy.Parse(["style-src https://*.example.test"], []);

        Assert.True(policy.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/site.css"), Document.Origin));
        Assert.True(policy.AllowsStyle(BrowserUrl.Parse("https://deep.cdn.example.test/site.css"), Document.Origin));
        Assert.False(policy.AllowsStyle(BrowserUrl.Parse("https://example.test/site.css"), Document.Origin));
        Assert.False(policy.AllowsStyle(BrowserUrl.Parse("https://notexample.test/site.css"), Document.Origin));
        Assert.False(policy.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test:8443/site.css"), Document.Origin));
    }

    [Fact]
    public void WildcardHostSourceMatchesExplicitAndWildcardPorts()
    {
        var explicitPort = ContentSecurityPolicy.Parse(["style-src https://*.example.test:8443"], []);
        var anyPort = ContentSecurityPolicy.Parse(["style-src https://*.example.test:*"], []);
        var explicitDefaultPort = ContentSecurityPolicy.Parse(["style-src https://*.example.test:443"], []);

        Assert.True(explicitPort.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test:8443/site.css"), Document.Origin));
        Assert.False(explicitPort.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test:9443/site.css"), Document.Origin));
        Assert.False(explicitPort.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/site.css"), Document.Origin));
        Assert.True(anyPort.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test:8443/site.css"), Document.Origin));
        Assert.True(anyPort.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/site.css"), Document.Origin));
        Assert.True(explicitDefaultPort.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/site.css"), Document.Origin));
        Assert.False(explicitDefaultPort.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test:8443/site.css"), Document.Origin));
    }

    [Fact]
    public void WildcardHostSourcePathsMatchExactFilesAndDirectoryPrefixes()
    {
        var directory = ContentSecurityPolicy.Parse(
            ["style-src https://*.example.test/assets/"], []);
        var rootPath = ContentSecurityPolicy.Parse(
            ["style-src https://*.example.test/"], []);
        var directoryWithPort = ContentSecurityPolicy.Parse(
            ["style-src https://*.example.test:8443/assets/"], []);
        var exactFile = ContentSecurityPolicy.Parse(
            ["style-src https://*.example.test/assets/site.css"], []);

        Assert.True(directory.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/assets/site.css"), Document.Origin));
        Assert.True(directory.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/assets/nested/site.css"), Document.Origin));
        Assert.False(directory.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/assets-v2/site.css"), Document.Origin));
        Assert.False(directory.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/Assets/site.css"), Document.Origin));
        Assert.True(rootPath.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/other/site.css"), Document.Origin));
        Assert.True(directoryWithPort.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test:8443/assets/site.css"), Document.Origin));
        Assert.False(directoryWithPort.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test:8443/other/site.css"), Document.Origin));
        Assert.True(exactFile.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/assets/site.css?version=1"), Document.Origin));
        Assert.False(exactFile.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/assets/site.css/child"), Document.Origin));
        Assert.False(exactFile.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/assets/other.css"), Document.Origin));
    }

    [Fact]
    public void WildcardHostSourcePathsComparePercentDecodedSegments()
    {
        var encodedSlash = ContentSecurityPolicy.Parse(
            ["style-src https://*.example.test/assets%2Fprivate/"], []);

        Assert.True(encodedSlash.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test/assets%2fprivate/site.css"), Document.Origin));
        Assert.False(encodedSlash.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test/assets/private/site.css"), Document.Origin));
    }

    [Fact]
    public void ExactDnsHostSourcePathsMatchFilesAndDirectoryPrefixes()
    {
        var directory = ContentSecurityPolicy.Parse(
            ["style-src https://cdn.example.test/assets/"], []);
        var exactFile = ContentSecurityPolicy.Parse(
            ["style-src https://cdn.example.test/assets/site.css"], []);
        var schemeless = ContentSecurityPolicy.Parse(
            ["style-src cdn.example.test/assets/"], []);

        Assert.True(directory.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test/assets/nested/site.css"), Document.Origin));
        Assert.False(directory.AllowsStyle(
            BrowserUrl.Parse("https://deep.cdn.example.test/assets/site.css"), Document.Origin));
        Assert.False(directory.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test/assets-v2/site.css"), Document.Origin));
        Assert.True(exactFile.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test/assets/site.css?version=1"), Document.Origin));
        Assert.False(exactFile.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test/assets/site.css/child"), Document.Origin));
        Assert.True(schemeless.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test/assets/site.css"), Document.Origin));
        Assert.False(schemeless.AllowsStyle(
            BrowserUrl.Parse("http://cdn.example.test/assets/site.css"), Document.Origin));
    }

    [Fact]
    public void ExactDnsHostSourcePathsHonorPortsAndRejectIpLiterals()
    {
        var explicitPort = ContentSecurityPolicy.Parse(
            ["style-src https://cdn.example.test:8443/assets/"], []);
        var anyPort = ContentSecurityPolicy.Parse(
            ["style-src https://cdn.example.test:*/assets/"], []);

        Assert.True(explicitPort.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test:8443/assets/site.css"), Document.Origin));
        Assert.False(explicitPort.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test/assets/site.css"), Document.Origin));
        Assert.True(anyPort.AllowsStyle(
            BrowserUrl.Parse("https://cdn.example.test:9443/assets/site.css"), Document.Origin));
        Assert.Throws<PageNavigationException>(() =>
            ContentSecurityPolicy.Parse(["style-src https://127.0.0.1/assets/"], []));
    }

    [Fact]
    public void WildcardHostSourceSchemeMatchingUsesSecureUpgradeAndDocumentScheme()
    {
        var explicitHttp = ContentSecurityPolicy.Parse(["style-src HTTP://*.example.test"], []);
        var schemeless = ContentSecurityPolicy.Parse(["style-src *.example.test"], []);

        Assert.True(explicitHttp.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/site.css"), Document.Origin));
        Assert.True(schemeless.AllowsStyle(BrowserUrl.Parse("https://cdn.example.test/site.css"), Document.Origin));
        Assert.False(schemeless.AllowsStyle(BrowserUrl.Parse("http://cdn.example.test/site.css"), Document.Origin));
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
    [InlineData("style-src https://*.example.test:65536")]
    [InlineData("style-src https://*.example.test:999999999999999999999")]
    [InlineData("style-src https://*.example.test/assets?query")]
    [InlineData("style-src https://*.example.test/assets#fragment")]
    [InlineData("style-src https://*.example.test/assets%")]
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
