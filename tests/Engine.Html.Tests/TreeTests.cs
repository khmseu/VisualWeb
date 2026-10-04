using System.Text;
using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Html.Tests;

public sealed class TreeTests
{
    private static HtmlParseResult Parse(string input, HtmlParserOptions? options = null) =>
        HtmlParser.Parse(input, options, TestContext.Current.CancellationToken);

    public static IEnumerable<object[]> Cases()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Data", "tree"), "*.dat").Order())
        {
            var source = File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal);
            var cases = source.Split("#data\n", StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < cases.Length; index++)
            {
                var dataEnd = cases[index].IndexOf("\n#errors", StringComparison.Ordinal);
                var treeStart = cases[index].IndexOf("\n#document\n", StringComparison.Ordinal);
                if (dataEnd < 0 || treeStart < 0 || cases[index].Contains("#document-fragment", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unexpected tree fixture format.");
                }

                yield return [Path.GetFileName(file), index, cases[index][..dataEnd],
                    cases[index][(treeStart + "\n#document\n".Length)..].TrimEnd('\n')];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OfficialStaticTreeFixture(string file, int index, string input, string expected)
    {
        Assert.NotEmpty(file);
        Assert.True(index >= 0);
        var result = HtmlParser.Parse(input, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected, Dump(result.Document));
    }

    [Fact]
    public void ParsesStaticDocumentWithHeadListsAndRawText()
    {
        var result = Parse("<!doctype html><title>A &amp; B</title><style>p > a {color:red}</style>"
            + "<h1 id=heading>Hello</h1><p>one<div>block</div><ul><li>a<li>b</ul>"
            + "<textarea>\n&lt;hi&gt;</textarea><script>if (a < b) x = '&amp;';</script>");
        Assert.Equal(DomDocumentMode.NoQuirks, result.Document.Mode);
        Assert.Equal("A & B", result.Document.Head!.ChildNodes.OfType<DomElement>().First().TextContent);
        Assert.Equal("Hello", result.Document.GetElementById("heading")!.TextContent);
        var elements = result.Document.Body!.ChildNodes.OfType<DomElement>().ToArray();
        Assert.Equal(new[] { "h1", "p", "div", "ul", "textarea", "script" }, elements.Select(e => e.LocalName));
        Assert.Equal(new[] { "a", "b" }, elements[3].ChildNodes.Select(n => n.TextContent));
        Assert.Equal("<hi>", elements[4].TextContent);
        Assert.Equal("if (a < b) x = '&amp;';", elements[5].TextContent);
    }

    [Fact]
    public void BodyAndRootAttributesMergeWithoutOverwritingFirstValues()
    {
        var result = Parse("<html id=a><body id=b><html id=c lang=en><body id=d class=x>");
        Assert.Equal("a", result.Document.DocumentElement!.GetAttribute("id"));
        Assert.Equal("en", result.Document.DocumentElement.GetAttribute("lang"));
        Assert.Equal("b", result.Document.Body!.GetAttribute("id"));
        Assert.Equal("x", result.Document.Body.GetAttribute("class"));
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void MissingSkeletonIsInsertedAndRunsHaveMergedText()
    {
        var result = Parse("hello  world &amp; more");
        Assert.Equal(DomDocumentMode.Quirks, result.Document.Mode);
        Assert.NotNull(result.Document.Head);
        Assert.Equal("hello  world & more", Assert.IsType<DomText>(Assert.Single(result.Document.Body!.ChildNodes)).Data);
        Assert.Contains(result.Errors, e => e.Code == "missing-doctype");
        Assert.NotNull(Parse("").Document.Body);
    }

    [Theory]
    [InlineData("<table><tr><td>x")]
    [InlineData("<template>x")]
    [InlineData("<svg><path>")]
    [InlineData("<math>x")]
    [InlineData("<form>x")]
    [InlineData("<noscript>x")]
    [InlineData("<select><option>x")]
    [InlineData("<ruby>x")]
    [InlineData("<p><b>x</p>y")]
    [InlineData("<b><i>x</b>y</i>")]
    [InlineData("<a><a>x")]
    [InlineData("<nobr><nobr>x")]
    [InlineData("<!doctype html PUBLIC 'legacy'>")]
    public void UnsupportedTreeAlgorithmsFailExplicitly(string input) =>
        Assert.Throws<UnsupportedHtmlException>(() => Parse(input));

    [Fact]
    public void VoidElementsDoNotPushAndSelfClosingNonVoidRemainsOpen()
    {
        var result = Parse("<div/><br/>text<img src=x></div>");
        var div = Assert.IsType<DomElement>(Assert.Single(result.Document.Body!.ChildNodes));
        Assert.Equal(new[] { DomNodeType.Element, DomNodeType.Text, DomNodeType.Element }, div.ChildNodes.Select(n => n.NodeType));
        Assert.Contains(result.Errors, e => e.Code == "non-void-html-element-start-tag-with-trailing-solidus");
    }

    [Fact]
    public void ProcessingInstructionsRemainNodesNotText()
    {
        var result = Parse("<?build version?><p>x<?inside ok?></p>");
        Assert.IsType<DomProcessingInstruction>(result.Document.FirstChild);
        Assert.Equal("x", result.Document.Body!.TextContent);
        Assert.IsType<DomProcessingInstruction>(result.Document.Body.FirstChild!.LastChild);
    }

    [Theory]
    [InlineData(3, 100, "<p>x")]
    [InlineData(100, 2, "<p>x")]
    public void ParserLimitsFailRatherThanReturnPartialDocument(int nodes, int depth, string input) =>
        Assert.Throws<HtmlLimitException>(() => Parse(input, new() { MaxNodes = nodes, MaxDepth = depth }));

    [Fact]
    public void RawTextDoesNotTriggerUnsupportedMarkup()
    {
        Assert.Equal("<table>&amp;", Parse("<style><table>&amp;</style>").Document.Head!.FirstChild!.TextContent);
    }

    [Fact]
    public void TextCoalescingDoesNotDependOnIgnoredEndTags()
    {
        var result = Parse(string.Concat(Enumerable.Repeat("x</unknown>", 10_000)), new() { MaxErrors = 20_000 });
        Assert.Equal(new string('x', 10_000), Assert.IsType<DomText>(Assert.Single(result.Document.Body!.ChildNodes)).Data);
    }

    [Fact]
    public void RecoveredParserAttributeNamesDoNotUsePublicDomFactoryValidation()
    {
        var result = Parse("<p =foo=bar>");
        Assert.Equal("bar", result.Document.Body!.FirstChild is DomElement element ? element.GetAttribute("=foo") : null);
        Assert.Contains(result.Errors, e => e.Code == "unexpected-equals-sign-before-attribute-name");
    }

    [Fact]
    public void ExactSkeletonLimitsAllowFourNodesAndTwoOpenElements()
    {
        var result = Parse("", new() { MaxNodes = 4, MaxDepth = 2, MaxErrors = 1 });
        Assert.NotNull(result.Document.Body);
        Assert.Single(result.Errors);
        Assert.Throws<HtmlLimitException>(() => Parse("", new() { MaxNodes = 3 }));
        Assert.Throws<HtmlLimitException>(() => Parse("", new() { MaxDepth = 1 }));
        Assert.Throws<HtmlLimitException>(() => Parse("\0", new() { MaxErrors = 1 }));
    }

    [Fact]
    public void LeadingNewlineRulesApplyOnlyToFirstTextToken()
    {
        var document = Parse("<pre>\n\nx</pre><textarea>\n\n&amp;</textarea><pre><!--c-->\nx</pre>").Document;
        var elements = document.Body!.ChildNodes.OfType<DomElement>().ToArray();
        Assert.Equal("\nx", elements[0].TextContent);
        Assert.Equal("\n&", elements[1].TextContent);
        Assert.Equal("\nx", elements[2].TextContent);
    }

    [Fact]
    public void HeadContentAfterHeadUsesHeadThenRestoresBodyConstruction()
    {
        var document = Parse("<head></head><title>T</title><meta charset=utf-8><p>body").Document;
        Assert.Equal(new[] { "title", "meta" }, document.Head!.ChildNodes.OfType<DomElement>().Select(e => e.LocalName));
        Assert.Equal("body", document.Body!.TextContent);
        Assert.Same(document, document.Head.ParentNode!.ParentNode);
    }

    [Fact]
    public void ParserCancellationDoesNotReturnPartialResult()
    {
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HtmlParser.Parse("<p>hello", cancellationToken: cancelled.Token));
    }

    [Fact]
    public void DialogHasBlockStartRulesButIsNotInTheSpecialCategory()
    {
        var document = Parse("<x><dialog>inside</x>after").Document;
        Assert.Equal("inside", document.Body!.FirstChild!.TextContent);
        Assert.Equal("after", Assert.IsType<DomText>(document.Body.LastChild).Data);
    }

    public static string Dump(DomNode root)
    {
        var output = new StringBuilder();
        foreach (var child in root.ChildNodes) { Visit(child, 0); }
        return output.ToString().TrimEnd('\n');

        void Visit(DomNode node, int depth)
        {
            var prefix = "| " + new string(' ', depth * 2);
            output.Append(prefix);
            switch (node)
            {
                case DomElement element:
                    output.Append('<').Append(element.LocalName).Append(">\n");
                    foreach (var (name, value) in element.Attributes.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        output.Append(prefix).Append("  ").Append(name).Append("=\"").Append(value).Append("\"\n");
                    }

                    break;
                case DomText text: output.Append('"').Append(text.Data).Append("\"\n"); break;
                case DomComment comment: output.Append("<!-- ").Append(comment.Data).Append(" -->\n"); break;
                case DomDocumentType doctype:
                    output.Append("<!DOCTYPE ").Append(doctype.Name);
                    if (doctype.PublicId.Length > 0 || doctype.SystemId.Length > 0)
                    {
                        output.Append(" \"").Append(doctype.PublicId).Append("\" \"").Append(doctype.SystemId).Append('"');
                    }

                    output.Append(">\n");
                    break;
                case DomProcessingInstruction instruction:
                    output.Append("<?").Append(instruction.Target).Append(' ').Append(instruction.Data).Append(">\n");
                    break;
                default: throw new InvalidOperationException("Unexpected node type.");
            }

            foreach (var child in node.ChildNodes) { Visit(child, depth + 1); }
        }
    }
}
