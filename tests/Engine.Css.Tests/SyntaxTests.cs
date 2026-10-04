using VisualWeb.Engine.Css;
using Xunit;

namespace VisualWeb.Engine.Css.Tests;

public sealed class SyntaxTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("foo", CssTokenKind.Ident, "foo")]
    [InlineData("--x", CssTokenKind.Ident, "--x")]
    [InlineData("-5", CssTokenKind.Number, "-5")]
    [InlineData("+.5e-2px", CssTokenKind.Dimension, "+.5e-2")]
    [InlineData("3%", CssTokenKind.Percentage, "3")]
    [InlineData("#123", CssTokenKind.Hash, "123")]
    [InlineData("@media", CssTokenKind.AtKeyword, "media")]
    [InlineData("url(a\\ b)", CssTokenKind.Url, "a b")]
    [InlineData("\"a\\\nb\"", CssTokenKind.String, "ab")]
    [InlineData("f\\6f o", CssTokenKind.Ident, "foo")]
    [InlineData("\\1f600", CssTokenKind.Ident, "\U0001F600")]
    [InlineData("\\0", CssTokenKind.Ident, "\uFFFD")]
    [InlineData("\\110000", CssTokenKind.Ident, "\uFFFD")]
    [InlineData("\0x", CssTokenKind.Ident, "\uFFFDx")]
    [InlineData("<!--", CssTokenKind.Cdo, "<!--")]
    [InlineData("-->", CssTokenKind.Cdc, "-->")]
    public void TokenKindsAndDecodedValues(string input, CssTokenKind kind, string value)
    {
        var token = new CssTokenizer(input, cancellationToken: Cancellation).Read();
        Assert.Equal(kind, token.Kind);
        Assert.Equal(value, token.Value);
    }

    [Fact]
    public void LoneSurrogatesArePreprocessedWithoutTestTransportEncoding()
    {
        var input = new string(['\uD800', 'x', '\uDC00']);
        Assert.Equal("\uFFFDx\uFFFD", new CssTokenizer(input, cancellationToken: Cancellation).Read().Value);
    }

    [Theory]
    [InlineData("foo\\", CssTokenKind.Ident, "foo\uFFFD")]
    [InlineData("1foo\\", CssTokenKind.Dimension, "1")]
    [InlineData("url(foo\\", CssTokenKind.Url, "foo\uFFFD")]
    [InlineData("\"foo\\", CssTokenKind.String, "foo")]
    public void OfficialEscapedEofSemantics(string input, CssTokenKind kind, string value)
    {
        var token = new CssTokenizer(input, cancellationToken: Cancellation).Read();
        Assert.Equal(kind, token.Kind);
        Assert.Equal(value, token.Value);
        if (kind == CssTokenKind.Dimension) { Assert.Equal("foo\uFFFD", token.Unit); }
    }

    [Fact]
    public void TokensRetainFlagsUnitsAndNumberRepresentation()
    {
        var tokenizer = new CssTokenizer("#foo #123 1e3PX 2n 1e999", cancellationToken: Cancellation);
        var tokens = Read(tokenizer);
        Assert.True(tokens[0].IsId);
        Assert.False(tokens[2].IsId);
        Assert.Equal("PX", tokens[4].Unit);
        Assert.False(tokens[4].IsInteger);
        Assert.True(tokens[6].IsInteger);
        Assert.Equal("1e999", tokens[8].Value);
    }

    [Fact]
    public void CommentsAreRemovedWithoutInventingWhitespaceOrMergingTokens()
    {
        var tokens = Read(new CssTokenizer("a/**/b", cancellationToken: Cancellation));
        Assert.Equal(new[] { "a", "b" }, tokens.Select(t => t.Value));
        var selector = Assert.Throws<FormatException>(() => CssSelectorList.Parse("a/**/b", cancellationToken: Cancellation));
        Assert.Contains("Invalid selector", selector.Message);
    }

    [Fact]
    public void QuotedUrlIsAFunctionAndUnquotedUrlIsSingleToken()
    {
        var tokens = Read(new CssTokenizer("url( 'a') url(b)", cancellationToken: Cancellation));
        Assert.Equal(CssTokenKind.Function, tokens[0].Kind);
        Assert.Equal(CssTokenKind.String, tokens[2].Kind);
        Assert.Equal(CssTokenKind.Url, tokens[^1].Kind);
    }

    [Theory]
    [InlineData("/*", "eof-in-comment")]
    [InlineData("\"x", "eof-in-string")]
    [InlineData("\"x\ny", "newline-in-string")]
    [InlineData("url(x y)", "bad-url")]
    [InlineData("url(x", "eof-in-url")]
    [InlineData("\\\n", "invalid-escape")]
    public void TokenizerErrorsAreObservable(string input, string code)
    {
        var tokenizer = new CssTokenizer(input, cancellationToken: Cancellation);
        Read(tokenizer);
        Assert.Contains(tokenizer.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void StylesheetKeepsAtRulesAndBalancedSelectorFunctions()
    {
        var sheet = CssSyntax.ParseStyleSheet("<!-- @media print { p { color:red } } p:is(.x,.y) { color:blue } -->",
            cancellationToken: Cancellation);
        Assert.Empty(sheet.Diagnostics);
        Assert.Equal(2, sheet.Rules.Count);
        Assert.Equal("media", sheet.Rules[0].AtName);
        Assert.Null(sheet.Rules[1].AtName);
        Assert.Equal(CssTokenKind.CloseParen, sheet.Rules[1].Prelude[^1].Kind);
    }

    [Fact]
    public void DeclarationParsingRespectsNestedSemicolonsAndImportant()
    {
        var result = CssSyntax.ParseDeclarations("COLOR : rgb(1,2,3); font-family:'a;b'; margin:1px ! IMPORTANT; broken; color:blue",
            cancellationToken: Cancellation);
        Assert.Equal(4, result.Declarations.Count);
        Assert.Equal("color", result.Declarations[0].Name);
        Assert.True(result.Declarations[2].Important);
        Assert.Equal("a;b", result.Declarations[1].Value[0].Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "missing-declaration-colon");
    }

    [Fact]
    public void EofBlockRecoveryRetainsInnerClosingTokens()
    {
        var result = CssSyntax.ParseStyleSheet("p { color:rgb(1,2,3", cancellationToken: Cancellation);
        Assert.Single(result.Rules);
        Assert.Contains(result.Diagnostics, d => d.Code == "eof-in-block");
        Assert.Equal(CssTokenKind.CloseParen, result.Rules[0].Block![^1].Kind);
        Assert.Single(CssSyntax.ParseDeclarations("color:rgb(1,2,3", cancellationToken: Cancellation).Declarations);
    }

    [Fact]
    public void UnsupportedNestedRulesDoNotSwallowFollowingDeclarations()
    {
        var result = CssSyntax.ParseDeclarations("@media screen {color:red} color:blue; & p {color:green} margin:1px",
            cancellationToken: Cancellation);
        Assert.Equal(new[] { "color", "margin" }, result.Declarations.Select(d => d.Name));
        Assert.Contains(result.Diagnostics, d => d.Code == "unsupported-at-rule");
        Assert.Contains(result.Diagnostics, d => d.Code == "unsupported-nested-rule");
        var custom = CssSyntax.ParseDeclarations("--x:{a;b}; unknown:f(!); color:red", cancellationToken: Cancellation);
        Assert.Empty(custom.Diagnostics);
        Assert.Equal(3, custom.Declarations.Count);
    }

    [Fact]
    public void InputTokenDepthAndDiagnosticLimitsHaveExactBoundaries()
    {
        Assert.Equal("a", new CssTokenizer("a", new() { MaxInputCharacters = 1, MaxTokens = 1 }, Cancellation).Read().Value);
        Assert.Throws<CssLimitException>(() => new CssTokenizer("ab", new() { MaxInputCharacters = 1 }, Cancellation));
        var tokenizer = new CssTokenizer("a b", new() { MaxTokens = 1 }, Cancellation);
        tokenizer.Read();
        Assert.Throws<CssLimitException>(() => tokenizer.Read());
        Assert.Single(CssSyntax.ParseStyleSheet("p{a:b}", new() { MaxDepth = 1 }, Cancellation).Rules);
        Assert.Throws<CssLimitException>(() => CssSyntax.ParseStyleSheet("p{a:f(b)}", new() { MaxDepth = 1 }, Cancellation));
        Assert.Single(CssSyntax.ParseDeclarations("a:f(b)", new() { MaxDepth = 1 }, Cancellation).Declarations);
        Assert.Throws<CssLimitException>(() => CssSyntax.ParseDeclarations("a:f(g(b))", new() { MaxDepth = 1 }, Cancellation));
        Assert.Throws<CssLimitException>(() => CssSyntax.ParseDeclarations("bad;worse", new() { MaxDiagnostics = 1 }, Cancellation));
    }

    [Fact]
    public void CancellationAndInvalidOptionsNeverReturnSuccess()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => CssSyntax.ParseStyleSheet("p{}", cancellationToken: source.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CssTokenizer("", new() { MaxDepth = 0 }, Cancellation));
    }

    private static List<CssToken> Read(CssTokenizer tokenizer)
    {
        var result = new List<CssToken>();
        for (var token = tokenizer.Read(); token.Kind != CssTokenKind.Eof; token = tokenizer.Read()) { result.Add(token); }
        return result;
    }
}
