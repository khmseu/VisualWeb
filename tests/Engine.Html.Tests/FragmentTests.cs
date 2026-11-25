using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Html.Tests;

public sealed class FragmentTests
{
    [Theory]
    [InlineData("caption")]
    [InlineData("col")]
    [InlineData("colgroup")]
    [InlineData("tbody")]
    [InlineData("thead")]
    [InlineData("tfoot")]
    [InlineData("tr")]
    [InlineData("td")]
    [InlineData("th")]
    public void BodyContextIgnoresTableStructureStartsAndKeepsFollowingContent(string tag)
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment($"<p>before<{tag} id=ignored>after<span>later</span></p>"
            + "<table><tr><td>cell</td></tr></table>",
            document.CreateElement("body"), cancellationToken: TestContext.Current.CancellationToken);

        var paragraph = Assert.IsType<DomElement>(result.Fragment.FirstChild);
        Assert.Equal("p", paragraph.LocalName);
        Assert.Equal("beforeafterlater", paragraph.TextContent);
        Assert.Equal("span", Assert.IsType<DomElement>(paragraph.LastChild).LocalName);
        var table = Assert.IsType<DomElement>(result.Fragment.LastChild);
        Assert.Equal("table", table.LocalName);
        var section = Assert.IsType<DomElement>(table.FirstChild);
        var row = Assert.IsType<DomElement>(section.FirstChild);
        var cell = Assert.IsType<DomElement>(row.FirstChild);
        Assert.Equal("tbody", section.LocalName);
        Assert.Equal("tr", row.LocalName);
        Assert.Equal("td", cell.LocalName);
        Assert.Equal("cell", cell.TextContent);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Single(result.Errors, error => error.Code == "unexpected-table-structure-start-tag-in-body");
        Assert.Single(result.Errors);
    }

    [Theory]
    [InlineData("caption")]
    [InlineData("col")]
    [InlineData("colgroup")]
    [InlineData("tbody")]
    [InlineData("thead")]
    [InlineData("tfoot")]
    [InlineData("tr")]
    [InlineData("td")]
    [InlineData("th")]
    public void BodyContextIgnoresUnmatchedTableStructureEndsAndKeepsFollowingContent(string tag)
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment($"<p>before</{tag}>after<span>later</span></p>"
            + "<table><tr><td>cell</td></tr></table>",
            document.CreateElement("body"), cancellationToken: TestContext.Current.CancellationToken);

        var paragraph = Assert.IsType<DomElement>(result.Fragment.FirstChild);
        Assert.Equal("p", paragraph.LocalName);
        Assert.Equal("beforeafterlater", paragraph.TextContent);
        Assert.Equal("span", Assert.IsType<DomElement>(paragraph.LastChild).LocalName);
        var table = Assert.IsType<DomElement>(result.Fragment.LastChild);
        Assert.Equal("table", table.LocalName);
        var section = Assert.IsType<DomElement>(table.FirstChild);
        var row = Assert.IsType<DomElement>(section.FirstChild);
        var cell = Assert.IsType<DomElement>(row.FirstChild);
        Assert.Equal("tbody", section.LocalName);
        Assert.Equal("tr", row.LocalName);
        Assert.Equal("td", cell.LocalName);
        Assert.Equal("cell", cell.TextContent);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Single(result.Errors, error => error.Code == "unexpected-end-tag");
        Assert.Single(result.Errors);
    }

    [Fact]
    public void SelectContextStillRejectsTableStructureEndTags()
    {
        var document = new DomDocument();
        Assert.Throws<UnsupportedHtmlException>(() => HtmlParser.ParseFragment("</td>",
            document.CreateElement("select"), cancellationToken: TestContext.Current.CancellationToken));
    }

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
    public void TableFragmentFostersTextIntoLastOpenTemplateAndHonorsLimits()
    {
        var document = new DomDocument();
        var context = document.CreateElement("table");
        const string input = "<template><tbody>A</tbody></template>";
        var result = HtmlParser.ParseFragment(input, context, new() { MaxNodes = 4, MaxDepth = 2 },
            TestContext.Current.CancellationToken);

        var template = Assert.IsType<DomTemplateElement>(Assert.Single(result.Fragment.ChildNodes));
        Assert.Collection(template.Content.ChildNodes,
            node => Assert.Equal("tbody", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("A", Assert.IsType<DomText>(node).Data));
        Assert.Same(template.Content.OwnerDocument, template.Content.LastChild!.OwnerDocument);
        Assert.Contains(result.Errors, error => error.Code == "non-whitespace-table-text");
        Assert.Throws<HtmlLimitException>(() => HtmlParser.ParseFragment(input, context, new() { MaxNodes = 3 },
            TestContext.Current.CancellationToken));
        Assert.Throws<HtmlLimitException>(() => HtmlParser.ParseFragment(input, context, new() { MaxDepth = 1 },
            TestContext.Current.CancellationToken));
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => HtmlParser.ParseFragment(input, context,
            cancellationToken: cancelled.Token));
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
    public void ParseFragment_selectTemplateContentUsesInBodyRulesAndResumesSelectContext()
    {
        var document = new DomDocument();

        var result = HtmlParser.ParseFragment(
            "<template><input><option>inside</option></template><option>after</option>",
            document.CreateElement("select"), cancellationToken: TestContext.Current.CancellationToken);

        var template = Assert.IsType<DomTemplateElement>(result.Fragment.FirstChild);
        Assert.Equal(new[] { "input", "option" },
            template.Content.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        Assert.Equal(new[] { "template", "option" },
            result.Fragment.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        Assert.Equal("after", result.Fragment.LastChild!.TextContent);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Empty(result.Errors);
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
    public void ParseFragmentMisnestedFormEndKeepsDescendantOpen()
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment("<form><div>one</form>two</div><form>three</form>",
            document.CreateElement("div"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "form", "form" },
            result.Fragment.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        Assert.Equal("onetwo", result.Fragment.FirstChild!.TextContent);
        Assert.Equal("three", result.Fragment.LastChild!.TextContent);
        Assert.Single(result.Errors, error => error.Code == "misnested-form-end-tag");
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
    }

    [Fact]
    public void ParseFragmentImplicitAncestorClosureKeepsFormPointerUntilFormEndTag()
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment(
            "<div><form id=first>one</div><form id=ignored>two</form><form id=second>three</form>",
            document.CreateElement("section"), cancellationToken: TestContext.Current.CancellationToken);
        var div = Assert.IsType<DomElement>(result.Fragment.FirstChild);
        var firstForm = Assert.IsType<DomElement>(div.FirstChild);
        var secondForm = Assert.IsType<DomElement>(result.Fragment.LastChild);

        Assert.Equal("first", firstForm.GetAttribute("id"));
        Assert.Equal("one", firstForm.TextContent);
        Assert.Equal("two", Assert.IsType<DomText>(result.Fragment.ChildNodes[1]).Data);
        Assert.Equal("second", secondForm.GetAttribute("id"));
        Assert.Equal("three", secondForm.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "implicitly-closed-descendants");
        Assert.Contains(result.Errors, error => error.Code == "nested-form");
        Assert.Contains(result.Errors, error => error.Code == "unexpected-form-end-tag");
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
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

    [Fact]
    public void ParseFragment_noscriptContextUsesDisabledScriptingDataState()
    {
        var document = new DomDocument();
        var context = document.CreateElement("noscript");

        var result = HtmlParser.ParseFragment("<p>fallback <b>markup</b></p>", context,
            cancellationToken: TestContext.Current.CancellationToken);

        var paragraph = Assert.IsType<DomElement>(Assert.Single(result.Fragment.ChildNodes));
        Assert.Equal("p", paragraph.LocalName);
        Assert.Equal("fallback markup", paragraph.TextContent);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("table")]
    [InlineData("tbody")]
    [InlineData("tr")]
    public void ParseFragment_noscriptInTableModesUsesFosteredInBodyRules(string contextName)
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment("<noscript><p>fallback</p></noscript>", document.CreateElement(contextName),
            cancellationToken: TestContext.Current.CancellationToken);

        var noscript = Assert.IsType<DomElement>(Assert.Single(result.Fragment.ChildNodes));
        var paragraph = Assert.IsType<DomElement>(Assert.Single(noscript.ChildNodes));
        Assert.Equal("noscript", noscript.LocalName);
        Assert.Equal("p", paragraph.LocalName);
        Assert.Equal("fallback", paragraph.TextContent);
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Contains(result.Errors, error => error.Code == "fostered-noscript-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-noscript-end-in-table");
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("math")]
    [InlineData("frameset")]
    public void ParseFragmentUnsupportedContextFailsExplicitly(string contextName)
    {
        var document = new DomDocument();

        Assert.Throws<UnsupportedHtmlException>(() =>
            HtmlParser.ParseFragment("content", document.CreateElement(contextName),
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("applet")]
    [InlineData("marquee")]
    [InlineData("object")]
    public void ParseFragmentEmbeddedContextUsesBoundedInBodyRules(string contextName)
    {
        var document = new DomDocument();
        var context = document.CreateElement(contextName);
        var result = HtmlParser.ParseFragment(
            $"<b>before<{contextName}>inside</{contextName}>after</b>", context,
            cancellationToken: TestContext.Current.CancellationToken);
        var bold = Assert.IsType<DomElement>(Assert.Single(result.Fragment.ChildNodes));

        Assert.Collection(bold.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var embedded = Assert.IsType<DomElement>(node);
                Assert.Equal(contextName, embedded.LocalName);
                Assert.Equal("inside", embedded.TextContent);
            },
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.All(result.Fragment.Descendants(), node => Assert.Same(document, node.OwnerDocument));
        Assert.Empty(context.ChildNodes);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("applet")]
    [InlineData("marquee")]
    [InlineData("object")]
    public void ParseFragmentEmbeddedContextDoesNotPutContextOnOpenElements(string contextName)
    {
        var document = new DomDocument();
        var context = document.CreateElement(contextName);
        var result = HtmlParser.ParseFragment($"</{contextName}>after", context,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("after", Assert.IsType<DomText>(Assert.Single(result.Fragment.ChildNodes)).Data);
        Assert.Single(result.Errors, error => error.Code == "unexpected-end-tag");
        Assert.Empty(context.ChildNodes);
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
