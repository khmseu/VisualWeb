using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Html.Tests;

public sealed class FragmentTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("nobr")]
    public void FragmentNestedFormattingStartsUseAdjustedAdoptionParent(string tag)
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment($"<{tag} id=old>before<div>inside<{tag} id=new>after</{tag}></div>",
            document.CreateElement("div"), cancellationToken: TestContext.Current.CancellationToken);
        var original = Assert.IsType<DomElement>(result.Fragment.FirstChild);
        var div = Assert.IsType<DomElement>(result.Fragment.LastChild);
        Assert.Equal("before", original.TextContent);
        Assert.Equal("old", Assert.IsType<DomElement>(div.FirstChild).GetAttribute("id"));
        Assert.Equal("new", Assert.IsType<DomElement>(div.LastChild).GetAttribute("id"));
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Contains(result.Errors, error => error.Code == $"nested-{tag}-start-tag");
    }

    [Fact]
    public void FragmentFormattingReconstructionPreservesOwnershipAndCountsNodes()
    {
        var document = new DomDocument();
        var context = document.CreateElement("div");
        const string input = "<b><i>one</b>two";
        var result = HtmlParser.ParseFragment(input, context, new() { MaxNodes = 5 },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Fragment.ChildNodes.Count);
        Assert.Equal("i", Assert.IsType<DomElement>(result.Fragment.ChildNodes[1]).LocalName);
        Assert.Equal("two", result.Fragment.ChildNodes[1].TextContent);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Throws<HtmlLimitException>(() => HtmlParser.ParseFragment(input, context, new() { MaxNodes = 4 },
            TestContext.Current.CancellationToken));
        Assert.Throws<HtmlLimitException>(() => HtmlParser.ParseFragment(input, context, new() { MaxDepth = 1 },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void FragmentAdoptionAgencyUsesFragmentAdjustedInsertionParent()
    {
        var document = new DomDocument();
        var context = document.CreateElement("div");
        const string input = "<b>before<p>inside</b>after</p>";
        var result = HtmlParser.ParseFragment(input, context, new() { MaxNodes = 6 },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Fragment.ChildNodes.Count);
        var originalBold = Assert.IsType<DomElement>(result.Fragment.FirstChild);
        var paragraph = Assert.IsType<DomElement>(result.Fragment.LastChild);
        Assert.Equal("before", originalBold.TextContent);
        Assert.Equal("p", paragraph.LocalName);
        Assert.Equal("b", Assert.IsType<DomElement>(paragraph.FirstChild).LocalName);
        Assert.Equal("after", Assert.IsType<DomText>(paragraph.LastChild).Data);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Throws<HtmlLimitException>(() => HtmlParser.ParseFragment(input, context, new() { MaxNodes = 5 },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ParseFragment_bodyContextReturnsOnlyFragmentNodesOwnedByContextDocument()
    {
        var document = new DomDocument();
        var context = document.CreateElement("div");
        context.AppendChild(document.CreateElement("aside"));

        var result = HtmlParser.ParseFragment("<p>one</p>tail", context,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Fragment.ChildNodes.Count);
        Assert.Equal("p", Assert.IsType<DomElement>(result.Fragment.FirstChild).LocalName);
        Assert.Equal("tail", Assert.IsType<DomText>(result.Fragment.LastChild).Data);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Single(context.ChildNodes);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseFragment_htmlContextUsesBeforeHeadInsertionMode()
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment("<!--before--><head></head><title>title</title><body><p>body</p></body>",
            document.CreateElement("html"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { DomNodeType.Comment, DomNodeType.Element, DomNodeType.Element },
            result.Fragment.ChildNodes.Select(node => node.NodeType));
        Assert.Equal("title", result.Fragment.ChildNodes[1].TextContent);
        Assert.Equal("body", result.Fragment.LastChild!.TextContent);
    }

    [Fact]
    public void ParseFragment_htmlContextIgnoresHtmlEndTagWithoutLeavingAfterBodyMode()
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment("<body>body</body></html>after",
            document.CreateElement("html"), cancellationToken: TestContext.Current.CancellationToken);

        var body = Assert.Single(result.Fragment.ChildNodes.OfType<DomElement>(), element => element.LocalName == "body");
        Assert.Equal("bodyafter", body.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "unexpected-html-end-tag");
    }

    [Fact]
    public void ParseFragment_tableContextBuildsTableBodyAndRows()
    {
        var document = new DomDocument();
        var context = document.CreateElement("table");

        var result = HtmlParser.ParseFragment("<tr><td>one</td></tr><tr><td>two</td></tr>", context,
            cancellationToken: TestContext.Current.CancellationToken);

        var body = Assert.IsType<DomElement>(Assert.Single(result.Fragment.ChildNodes));
        Assert.Equal("tbody", body.LocalName);
        Assert.Equal(new[] { "one", "two" }, body.ChildNodes.Select(row => row.TextContent));
        Assert.Same(document, body.OwnerDocument);
    }

    [Fact]
    public void ParseFragment_tableContextFosterParentsTextIntoFragment()
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment("before<tr><td>cell</td></tr>", document.CreateElement("table"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "before", "tbody" }, result.Fragment.ChildNodes.Select(node =>
            node is DomElement element ? element.LocalName : node.TextContent));
        Assert.Contains(result.Errors, error => error.Code == "non-whitespace-table-text");
    }

    [Fact]
    public void ParseFragmentFosteredTableTextReconstructsFormattingInTheFragment()
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment("<p><b>x</p><table>y<tr><td>cell</td></tr></table>",
            document.CreateElement("div"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "p", "b", "table" },
            result.Fragment.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        Assert.Equal("y", result.Fragment.ChildNodes[1].TextContent);
        Assert.Equal("cell", result.Fragment.LastChild!.TextContent);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
    }

    [Fact]
    public void ParseFragment_tableFosterTextMergesWithBufferedWhitespace()
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment(" \n</table>after", document.CreateElement("table"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(" \nafter", Assert.IsType<DomText>(Assert.Single(result.Fragment.ChildNodes)).Data);
    }

    [Theory]
    [InlineData("tbody", "<tr><td>cell</td></tr>", "tr")]
    [InlineData("thead", "<tr><td>cell</td></tr>", "tr")]
    [InlineData("tfoot", "<tr><td>cell</td></tr>", "tr")]
    [InlineData("tr", "<td>cell</td>", "td")]
    [InlineData("td", "<p>cell</p>", "p")]
    [InlineData("th", "<p>cell</p>", "p")]
    [InlineData("caption", "<p>caption</p>", "p")]
    [InlineData("colgroup", "<col>", "col")]
    public void ParseFragmentTableContextUsesMatchingInsertionMode(string contextName, string input, string childName)
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment(input, document.CreateElement(contextName),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(childName, Assert.IsType<DomElement>(Assert.Single(result.Fragment.ChildNodes)).LocalName);
    }

    [Fact]
    public void ParseFragmentCaptionContextRestoresModeAfterNestedTable()
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment("before<table><tr><td>inner</td></tr></table></caption><b>after</b>",
            document.CreateElement("caption"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("before", Assert.IsType<DomText>(result.Fragment.ChildNodes[0]).Data);
        Assert.Equal("inner", Assert.IsType<DomElement>(result.Fragment.ChildNodes[1]).TextContent);
        Assert.Equal("after", Assert.IsType<DomElement>(result.Fragment.ChildNodes[2]).TextContent);
        Assert.Contains(result.Errors, error => error.Code == "unexpected-caption-end-tag");
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
    }

    [Fact]
    public void ParseFragmentAdoptionFosterReparentingStaysInFragment()
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment("<table><b><p>x</b>y</p></table>", document.CreateElement("div"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "b", "p", "table" }, result.Fragment.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        Assert.Equal("xy", result.Fragment.ChildNodes[1].TextContent);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
    }

    [Fact]
    public void ParseFragment_selectContextProcessesOptionsAndIgnoresSelectOnlyTokens()
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment(
            "<option>one<option>two<input><select><optgroup><option>three</optgroup>", document.CreateElement("select"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "option", "option", "optgroup" },
            result.Fragment.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        Assert.Equal(new[] { "one", "two" }, result.Fragment.ChildNodes.Take(2).Select(node => node.TextContent));
        Assert.Equal("three", result.Fragment.LastChild!.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "input-start-tag-in-select");
        Assert.Contains(result.Errors, error => error.Code == "nested-select-start-tag");
    }

    [Fact]
    public void ParseFragment_templateContextKeepsNestedTemplateContentSeparate()
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment("<p>outer</p><template><b>inner</b></template>",
            document.CreateElement("template"), cancellationToken: TestContext.Current.CancellationToken);

        var template = Assert.IsType<DomTemplateElement>(result.Fragment.LastChild);
        Assert.Equal("outer", result.Fragment.FirstChild!.TextContent);
        Assert.Equal("inner", template.Content.TextContent);
        Assert.Empty(template.ChildNodes);
        Assert.Same(template.Content.OwnerDocument, template.Content.FirstChild!.OwnerDocument);
    }

    [Theory]
    [InlineData("title", "&amp;<b>text</b>", "&<b>text</b>")]
    [InlineData("textarea", "&amp;<b>text</b>", "&<b>text</b>")]
    [InlineData("style", "&amp;<b>text</b>", "&amp;<b>text</b>")]
    [InlineData("xmp", "&amp;<b>text</b>", "&amp;<b>text</b>")]
    [InlineData("iframe", "&amp;<b>text</b>", "&amp;<b>text</b>")]
    [InlineData("noembed", "&amp;<b>text</b>", "&amp;<b>text</b>")]
    [InlineData("noframes", "&amp;<b>text</b>", "&amp;<b>text</b>")]
    [InlineData("script", "&amp;<b>text</b>", "&amp;<b>text</b>")]
    [InlineData("plaintext", "&amp;<b>text</b>", "&amp;<b>text</b>")]
    public void ParseFragmentTextContextUsesContextTokenizerState(
        string contextName, string input, string expected)
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment(input, document.CreateElement(contextName),
            cancellationToken: TestContext.Current.CancellationToken);

        var text = Assert.IsType<DomText>(Assert.Single(result.Fragment.ChildNodes));
        Assert.Equal(expected, text.Data);
    }

    [Fact]
    public void ParseFragmentTextContextHasNoAppropriateEndTag()
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment("one</title>&amp;<b>two</b>", document.CreateElement("title"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("one</title>&<b>two</b>",
            Assert.IsType<DomText>(Assert.Single(result.Fragment.ChildNodes)).Data);
    }

    [Fact]
    public void ParseFragmentFormAncestorRetainsFormPointer()
    {
        var document = new DomDocument();
        var form = document.CreateElement("form");
        var context = document.CreateElement("div");
        form.AppendChild(context);

        var result = HtmlParser.ParseFragment("<form><input></form><p>after</p>",
            context, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "input", "p" },
            result.Fragment.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        Assert.Contains(result.Errors, error => error.Code == "nested-form");
    }

    [Fact]
    public void ParseFragmentUnmatchedFormEndClearsTheAncestorFormPointer()
    {
        var document = new DomDocument();
        var outer = document.CreateElement("form");
        var context = document.CreateElement("div");
        outer.AppendChild(context);

        var result = HtmlParser.ParseFragment("</form><form id=inner>inside</form>",
            context, cancellationToken: TestContext.Current.CancellationToken);

        var inner = Assert.IsType<DomElement>(Assert.Single(result.Fragment.ChildNodes));
        Assert.Equal("form", inner.LocalName);
        Assert.Equal("inner", inner.GetAttribute("id"));
        Assert.Equal("inside", inner.TextContent);
        Assert.Single(result.Errors, error => error.Code == "unexpected-form-end-tag");
        Assert.DoesNotContain(result.Errors, error => error.Code == "nested-form");
    }

    [Fact]
    public void ParseFragmentTemplateContextIgnoresAncestorFormPointer()
    {
        var document = new DomDocument();
        var outer = document.CreateElement("form");
        var context = document.CreateElement("template");
        outer.AppendChild(context);

        var result = HtmlParser.ParseFragment("<p>before<form id=inner><input></form><p>after</p>",
            context, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "p", "form", "p" },
            result.Fragment.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        var inner = Assert.IsType<DomElement>(result.Fragment.ChildNodes[1]);
        Assert.Equal("inner", inner.GetAttribute("id"));
        Assert.Equal("input", Assert.IsType<DomElement>(Assert.Single(inner.ChildNodes)).LocalName);
        Assert.DoesNotContain(result.Errors, error => error.Code is "nested-form" or "unexpected-form-end-tag");
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("math")]
    [InlineData("noscript")]
    [InlineData("frameset")]
    public void ParseFragmentUnsupportedContextFailsExplicitly(string contextName)
    {
        var document = new DomDocument();

        Assert.Throws<UnsupportedHtmlException>(() =>
            HtmlParser.ParseFragment("content", document.CreateElement(contextName),
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ParseFragmentHonorsNodeLimitsAndCancellation()
    {
        var document = new DomDocument();
        var context = document.CreateElement("div");

        Assert.Throws<HtmlLimitException>(() =>
            HtmlParser.ParseFragment("<p>x</p>", context, new() { MaxNodes = 1 },
                TestContext.Current.CancellationToken));
        Assert.Throws<HtmlLimitException>(() =>
            HtmlParser.ParseFragment("<p><b>x</b></p>", context, new() { MaxDepth = 1 },
                TestContext.Current.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HtmlParser.ParseFragment("text", context, cancellationToken: cancellation.Token));
    }
}
