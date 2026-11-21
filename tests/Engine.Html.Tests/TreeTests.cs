using System.Text;
using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Html.Tests;

public sealed class TreeTests
{
    private static HtmlParseResult Parse(string input, HtmlParserOptions? options = null) =>
        HtmlParser.Parse(input, options, TestContext.Current.CancellationToken);

    [Fact]
    public void LegacyPreElementsRetainTheirDistinctParsingModes()
    {
        var listing = Parse("<!doctype html><listing id=x>\n<a id=live href=/next>live</a></listing>").Document;
        var listElement = listing.GetElementById("x")!;
        Assert.Equal("live", listElement.TextContent);
        Assert.Equal("a", listing.GetElementById("live")!.LocalName);

        var xmp = Parse("<!doctype html><xmp id=x>\n<a id=fake href=/next>&amp;</a></xmp><a id=live>live</a>").Document;
        var raw = xmp.GetElementById("x")!;
        Assert.Equal("\n<a id=fake href=/next>&amp;</a>", Assert.IsType<DomText>(Assert.Single(raw.ChildNodes)).Data);
        Assert.Null(xmp.GetElementById("fake"));
        Assert.NotNull(xmp.GetElementById("live"));

        var plaintext = Parse("<!doctype html><plaintext id=x>\n<a id=fake>text</a></plaintext><a id=live>live</a>").Document;
        Assert.Equal("\n<a id=fake>text</a></plaintext><a id=live>live</a>",
            Assert.IsType<DomText>(Assert.Single(plaintext.GetElementById("x")!.ChildNodes)).Data);
        Assert.Null(plaintext.GetElementById("fake"));
        Assert.Null(plaintext.GetElementById("live"));
    }

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
    public void ScriptCompletionCallbackRunsAtTheParserPositionIncludingEof()
    {
        var document = new DomDocument();
        var observed = new List<string?>();
        var parsed = HtmlParser.Parse("<!doctype html><body><script>1</script><p id=later>text</p>"
            + "<script>2", options: null, cancellationToken: TestContext.Current.CancellationToken,
            document: document, scriptCompleted: script =>
                observed.Add(document.GetElementById("later")?.TextContent + ":" + script.TextContent));

        Assert.Equal(new[] { ":1", "text:2" }, observed);
        Assert.NotNull(parsed.Document.GetElementById("later"));
    }

    [Fact]
    public void TemplateContentUsesSeparateOwnerDocumentAndDoesNotEnterDocumentTree()
    {
        var result = Parse("<!doctype html><body><template><p id=inside>content</p></template><p id=outside>body</p>");
        var body = result.Document.Body!;
        var template = Assert.IsType<DomTemplateElement>(body.FirstChild);
        var contentParagraph = Assert.IsType<DomElement>(Assert.Single(template.Content.ChildNodes));
        var bodyParagraph = Assert.IsType<DomElement>(template.NextSibling);

        Assert.Equal("p", contentParagraph.LocalName);
        Assert.Equal("content", contentParagraph.TextContent);
        Assert.Equal("body", bodyParagraph.TextContent);
        Assert.Empty(template.ChildNodes);
        Assert.Same(template.Content, contentParagraph.ParentNode);
        Assert.NotSame(result.Document, contentParagraph.OwnerDocument);
        Assert.Same(template.Content.OwnerDocument, contentParagraph.OwnerDocument);
        Assert.Null(result.Document.GetElementById("inside"));
        Assert.Same(bodyParagraph, result.Document.GetElementById("outside"));
    }

    [Fact]
    public void TemplateFormsIgnoreAndPreserveTheOuterFormPointer()
    {
        var result = Parse("<!doctype html><body><form id=outer><template><p>before"
            + "<form id=inner><input></form><p>after</p></template><input id=outer-input></form>"
            + "<form id=next>next</form>");
        var outer = Assert.IsType<DomElement>(result.Document.Body!.FirstChild);
        var template = Assert.IsType<DomTemplateElement>(outer.FirstChild);
        Assert.Collection(template.Content.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomElement>(node).TextContent),
            node =>
            {
                var inner = Assert.IsType<DomElement>(node);
                Assert.Equal("form", inner.LocalName);
                Assert.Equal("inner", inner.GetAttribute("id"));
                Assert.Equal("input", Assert.IsType<DomElement>(Assert.Single(inner.ChildNodes)).LocalName);
            },
            node => Assert.Equal("after", Assert.IsType<DomElement>(node).TextContent));
        Assert.Equal("outer-input", Assert.IsType<DomElement>(outer.LastChild).GetAttribute("id"));
        Assert.Equal("next", Assert.IsType<DomElement>(outer.NextSibling).GetAttribute("id"));
        Assert.DoesNotContain(result.Errors, error => error.Code is "nested-form" or "unexpected-form-end-tag");
    }

    [Fact]
    public void TableFormsInsideTemplateDoNotReplaceTheOuterFormPointer()
    {
        var result = Parse("<!doctype html><body><form id=outer><template><table>"
            + "<form id=first><form id=second><tr><td>cell</td></tr></table>"
            + "<form id=inner>inside</form></template><input id=outside></form><form id=next>next</form>");
        var outer = Assert.IsType<DomElement>(result.Document.Body!.FirstChild);
        var template = Assert.IsType<DomTemplateElement>(outer.FirstChild);
        var table = Assert.IsType<DomElement>(template.Content.FirstChild);
        Assert.Equal(new[] { "first", "second" },
            table.ChildNodes.OfType<DomElement>().Where(element => element.LocalName == "form")
                .Select(element => element.GetAttribute("id")));
        var inner = Assert.IsType<DomElement>(template.Content.LastChild);
        Assert.Equal("inner", inner.GetAttribute("id"));
        Assert.Equal("inside", inner.TextContent);
        Assert.Equal("outside", Assert.IsType<DomElement>(outer.LastChild).GetAttribute("id"));
        Assert.Equal("next", Assert.IsType<DomElement>(outer.NextSibling).GetAttribute("id"));
        Assert.Equal(2, result.Errors.Count(error => error.Code == "form-start-in-table"));
    }

    [Fact]
    public void NestedTemplateFormsCloseInScopeWithoutSettingAFormPointer()
    {
        var result = Parse("<!doctype html><body><template><form id=first><form id=second>"
            + "<p>inner</form>outer</form><form id=last>last</form></template><form id=after>after</form>");
        var template = Assert.IsType<DomTemplateElement>(result.Document.Body!.FirstChild);
        var first = Assert.IsType<DomElement>(template.Content.FirstChild);
        var second = Assert.IsType<DomElement>(first.FirstChild);
        Assert.Equal("first", first.GetAttribute("id"));
        Assert.Equal("second", second.GetAttribute("id"));
        Assert.Equal("inner", second.TextContent);
        Assert.Equal("outer", Assert.IsType<DomText>(first.LastChild).Data);
        Assert.Equal("last", Assert.IsType<DomElement>(template.Content.LastChild).GetAttribute("id"));
        Assert.Equal("after", Assert.IsType<DomElement>(template.NextSibling).GetAttribute("id"));
        Assert.DoesNotContain(result.Errors, error => error.Code is "nested-form" or "unexpected-form-end-tag");
    }

    [Fact]
    public void UnmatchedTemplateFormEndDoesNotCloseOuterForm()
    {
        var result = Parse("<!doctype html><body><form id=outer><template><p>content</p></form></template>"
            + "<input id=inside></form><form id=after>after</form>");
        var outer = Assert.IsType<DomElement>(result.Document.Body!.FirstChild);
        var template = Assert.IsType<DomTemplateElement>(outer.FirstChild);
        Assert.Equal("content", template.Content.TextContent);
        Assert.Equal("inside", Assert.IsType<DomElement>(outer.LastChild).GetAttribute("id"));
        Assert.Equal("after", Assert.IsType<DomElement>(outer.NextSibling).GetAttribute("id"));
        Assert.Single(result.Errors, error => error.Code == "unexpected-form-end-tag");
    }

    [Fact]
    public void TemplateContentElementHonorsExactParserDepthLimit()
    {
        const string input = "<body><template><p id=inside>content</p></template>";
        Assert.Throws<HtmlLimitException>(() => Parse(input, new() { MaxDepth = 3 }));

        var document = Parse(input, new() { MaxDepth = 4 }).Document;
        var template = Assert.IsType<DomTemplateElement>(Assert.Single(document.Body!.ChildNodes));
        var paragraph = Assert.IsType<DomElement>(Assert.Single(template.Content.ChildNodes));
        Assert.Equal("p", paragraph.LocalName);
        Assert.Equal("content", paragraph.TextContent);
        Assert.Same(template.Content, paragraph.ParentNode);
    }

    [Theory]
    [InlineData("<!--hidden-->", typeof(DomComment))]
    [InlineData("<?build hidden?>", typeof(DomProcessingInstruction))]
    public void TemplateMiscellaneousNodesUseInertContentFragment(string markup, Type nodeType)
    {
        var document = Parse($"<!doctype html><body><template>{markup}</template>").Document;
        var template = Assert.IsType<DomTemplateElement>(Assert.Single(document.Body!.ChildNodes));
        var node = Assert.IsAssignableFrom<DomCharacterData>(Assert.Single(template.Content.ChildNodes));

        Assert.IsType(nodeType, node);
        Assert.Equal("hidden", node.Data);
        if (node is DomProcessingInstruction instruction) { Assert.Equal("build", instruction.Target); }
        Assert.Empty(template.ChildNodes);
        Assert.Same(template.Content, node.ParentNode);
        Assert.Same(template.Content.OwnerDocument, node.OwnerDocument);
        Assert.NotSame(document, node.OwnerDocument);
    }

    [Fact]
    public void TemplateCommentAndProcessingInstructionAfterVoidChildUseContentFragment()
    {
        var document = Parse("<template><meta><!--hidden--><?build hidden?></template>").Document;
        var template = Assert.IsType<DomTemplateElement>(Assert.Single(document.Head!.ChildNodes));
        var nodes = template.Content.ChildNodes.ToArray();

        Assert.Collection(nodes,
            meta => Assert.Equal("meta", Assert.IsType<DomElement>(meta).LocalName),
            comment => Assert.IsType<DomComment>(comment),
            instruction => Assert.IsType<DomProcessingInstruction>(instruction));
        Assert.Empty(template.ChildNodes);
        Assert.All(nodes.Skip(1), node =>
        {
            Assert.Same(template.Content, node.ParentNode);
            Assert.Same(template.Content.OwnerDocument, node.OwnerDocument);
        });
    }

    [Fact]
    public void NestedTemplateContentInsideOrdinaryContentRemainsSeparate()
    {
        var document = Parse("<body><template><div><template><p>nested</p></template><span>outer</span></div></template></body>")
            .Document;
        var outer = Assert.IsType<DomTemplateElement>(Assert.Single(document.Body!.ChildNodes));
        var container = Assert.IsType<DomElement>(Assert.Single(outer.Content.ChildNodes));
        var nested = Assert.IsType<DomTemplateElement>(container.FirstChild);
        var nestedParagraph = Assert.IsType<DomElement>(Assert.Single(nested.Content.ChildNodes));
        var outerSpan = Assert.IsType<DomElement>(nested.NextSibling);

        Assert.Equal("nested", nestedParagraph.TextContent);
        Assert.Equal("outer", outerSpan.TextContent);
        Assert.Empty(nested.ChildNodes);
        Assert.Same(outer.Content.OwnerDocument, container.OwnerDocument);
        Assert.Same(outer.Content.OwnerDocument, nested.OwnerDocument);
        Assert.Same(nested.Content.OwnerDocument, nestedParagraph.OwnerDocument);
        Assert.Same(nested.Content.OwnerDocument, outer.Content.OwnerDocument);
    }

    [Fact]
    public void ValidOrdinaryAndNestedTemplateStartsDoNotConsumeParseErrorBudget()
    {
        var result = Parse("<!doctype html><body><template><div><template><p>nested</p></template></div></template></body>",
            new() { MaxErrors = 1 });

        Assert.DoesNotContain(result.Errors, error => error.Code == "template-start-tag");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TemplateInHeadRestoresHeadModeBeforeOrdinaryBodyConstruction()
    {
        var document = Parse("<head><template><div>hidden</div></template><title>later</title></head><body><p>body</p></body>")
            .Document;
        var template = Assert.IsType<DomTemplateElement>(document.Head!.FirstChild);
        var body = Assert.IsType<DomElement>(document.Body);

        Assert.Equal("later", document.Title);
        Assert.Equal("body", body.TextContent);
        Assert.Equal("hidden", template.Content.TextContent);
        Assert.Empty(template.ChildNodes);
    }

    [Fact]
    public void TemplateAfterHeadRestoresBodyModeAfterTemplateClose()
    {
        var document = Parse("<head></head><template><div>hidden</div></template><p>body</p>").Document;
        var template = Assert.IsType<DomTemplateElement>(Assert.Single(document.Head!.ChildNodes));

        Assert.Equal("hidden", template.Content.TextContent);
        Assert.Equal("body", document.Body!.TextContent);
        Assert.Equal("p", Assert.IsType<DomElement>(document.Body.FirstChild).LocalName);
    }

    [Fact]
    public void EofClosesTemplateStackAndReportsTemplateSpecificParseError()
    {
        var result = Parse("<body><template><div>content");
        var template = Assert.IsType<DomTemplateElement>(Assert.Single(result.Document.Body!.ChildNodes));

        Assert.Equal("content", template.Content.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "eof-in-template");
    }

    [Fact]
    public void TemplateContentUsesBoundedSelectParsingAndInertOwnerDocument()
    {
        var result = Parse("<template><select id=inside>"
            + "<option value=one>One<option value=two>Two</select>"
            + "<p id=after-select>after</p></template><p id=outside>outside</p>");
        var template = Assert.IsType<DomTemplateElement>(
            Assert.Single(Descendants(result.Document), element => element.LocalName == "template"));
        var select = Assert.IsType<DomElement>(template.Content.FirstChild);
        var options = select.ChildNodes.OfType<DomElement>().ToArray();
        var afterSelect = Assert.IsType<DomElement>(select.NextSibling);

        Assert.Equal("select", select.LocalName);
        Assert.Collection(options,
            option =>
            {
                Assert.Equal("option", option.LocalName);
                Assert.Equal("one", option.GetAttribute("value"));
                Assert.Equal("One", option.TextContent);
            },
            option =>
            {
                Assert.Equal("option", option.LocalName);
                Assert.Equal("two", option.GetAttribute("value"));
                Assert.Equal("Two", option.TextContent);
            });
        Assert.Same(template.Content, select.ParentNode);
        Assert.Same(template.Content.OwnerDocument, select.OwnerDocument);
        Assert.NotSame(result.Document, select.OwnerDocument);
        Assert.Equal("p", afterSelect.LocalName);
        Assert.Equal("after", afterSelect.TextContent);
        Assert.Same(template.Content.OwnerDocument, afterSelect.OwnerDocument);
        Assert.DoesNotContain(result.Errors, error => error.Code == "eof-in-template");
        Assert.Null(result.Document.GetElementById("inside"));
        Assert.Null(result.Document.GetElementById("after-select"));
        Assert.Equal("outside", result.Document.GetElementById("outside")!.TextContent);
    }

    [Theory]
    [InlineData("<template><svg><path></path></svg></template>")]
    public void UnsupportedTemplateInsertionContextsFailExplicitly(string input) =>
        Assert.Throws<UnsupportedHtmlException>(() => Parse(input));

    [Theory]
    [InlineData("<template><table><tbody><tr><td>cell</td></tr></tbody></table></template>", "table", "cell")]
    [InlineData("<template><caption>caption</caption></template>", "caption", "caption")]
    [InlineData("<template><colgroup><col></colgroup></template>", "colgroup", "")]
    [InlineData("<template><tbody><tr><td>section</td></tr></tbody></template>", "tbody", "section")]
    [InlineData("<template><thead><tr><th>head</th></tr></thead></template>", "thead", "head")]
    [InlineData("<template><tfoot><tr><td>foot</td></tr></tfoot></template>", "tfoot", "foot")]
    [InlineData("<template><tr><td>row</td></tr></template>", "tr", "row")]
    [InlineData("<template><td>cell</td></template>", "td", "cell")]
    [InlineData("<template><col></template>", "col", "")]
    public void TemplateContentUsesBoundedTableInsertionModes(
        string input,
        string expectedChild,
        string expectedText)
    {
        var result = Parse(input);
        var template = Assert.IsType<DomTemplateElement>(
            Assert.Single(Descendants(result.Document), element => element.LocalName == "template"));
        var content = Assert.IsType<DomElement>(Assert.Single(template.Content.ChildNodes));

        Assert.Equal(expectedChild, content.LocalName);
        Assert.Equal(expectedText, content.TextContent);
        Assert.DoesNotContain(result.Errors, error => error.Code == "eof-in-template");
    }

    [Theory]
    [InlineData("<template><table>")]
    [InlineData("<template><table>  ")]
    [InlineData("<template><caption>")]
    [InlineData("<template><colgroup>")]
    [InlineData("<template><tbody>")]
    [InlineData("<template><tr>")]
    [InlineData("<template><td>")]
    public void EofInTemplateTableModesClosesTemplateAndReportsError(string input)
    {
        var result = Parse(input);

        Assert.Contains(result.Errors, error => error.Code == "eof-in-template");
    }

    [Fact]
    public void EofInNestedTemplateTableModesClosesEachTemplate()
    {
        var result = Parse("<template><table><template><colgroup>");

        Assert.Equal(2, result.Errors.Count(error => error.Code == "eof-in-template"));
    }

    [Fact]
    public void TemplateInColumnGroupRestoresColumnGroupMode()
    {
        var result = Parse("<template><colgroup><template><p>hidden</p></template><col></colgroup></template>");
        var outer = Assert.IsType<DomTemplateElement>(
            Assert.Single(Descendants(result.Document), element => element.LocalName == "template"));
        var group = Assert.IsType<DomElement>(Assert.Single(outer.Content.ChildNodes));
        var nested = Assert.IsType<DomTemplateElement>(group.FirstChild);

        Assert.Equal("hidden", nested.Content.TextContent);
        Assert.Equal("col", Assert.IsType<DomElement>(nested.NextSibling).LocalName);
        Assert.DoesNotContain(result.Errors, error => error.Code == "unexpected-template-end-tag");
    }

    [Fact]
    public void TemplateStartAndEndInTableModesUseTemplateContentAndRestoreTableMode()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>", "table"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>", "tbody"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>", "tr")
        };

        foreach (var (tableStart, tableEnd, expectedParent) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>" + tableStart
                + "<template id=template><p>hidden</p></template>" + tableEnd + "</div>");
            var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
            var template = Assert.IsType<DomTemplateElement>(
                Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "template"));

            Assert.Equal("hidden", template.Content.TextContent);
            Assert.Empty(template.ChildNodes);
            Assert.Equal(expectedParent, Assert.IsType<DomElement>(template.ParentNode).LocalName);
            Assert.Equal("cell", table.TextContent);
            Assert.DoesNotContain(result.Errors, error => error.Code == "unexpected-template-end-tag");
        }
    }

    [Fact]
    public void TemplateInTableFragmentRestoresTheFragmentInsertionMode()
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment(
            "<template><p>hidden</p></template><tr><td>cell</td></tr>",
            document.CreateElement("table"),
            cancellationToken: TestContext.Current.CancellationToken);
        var template = Assert.IsType<DomTemplateElement>(result.Fragment.FirstChild);
        var body = Assert.IsType<DomElement>(result.Fragment.LastChild);

        Assert.Equal("template", template.LocalName);
        Assert.Same(result.Fragment, template.ParentNode);
        Assert.Equal("hidden", template.Content.TextContent);
        Assert.Equal("tbody", body.LocalName);
        Assert.Equal("cell", body.TextContent);
    }

    [Fact]
    public void TemplateTableModesWorkInTemplateFragmentContext()
    {
        var document = new DomDocument();
        var result = HtmlParser.ParseFragment(
            "<tbody><tr><td>cell</td></tr></tbody>",
            document.CreateElement("template"),
            cancellationToken: TestContext.Current.CancellationToken);
        var section = Assert.IsType<DomElement>(Assert.Single(result.Fragment.ChildNodes));
        var row = Assert.IsType<DomElement>(Assert.Single(section.ChildNodes));
        var cell = Assert.IsType<DomElement>(Assert.Single(row.ChildNodes));

        Assert.Equal("tbody", section.LocalName);
        Assert.Equal("tr", row.LocalName);
        Assert.Equal("cell", cell.TextContent);
    }

    [Theory]
    [InlineData("<select><template></template></select>", "Template insertion inside select content is not implemented.")]
    public void TemplateStartsInUnsupportedContextsFailExplicitly(string input, string message)
    {
        var exception = Assert.Throws<UnsupportedHtmlException>(() => Parse(input));
        Assert.StartsWith(message, exception.Message);
    }

    [Fact]
    public void TemplateContentFragmentCountsTowardParserNodeBudget()
    {
        Assert.Throws<HtmlLimitException>(() => Parse("<body><template></template>", new() { MaxNodes = 5 }));
        var result = Parse("<body><template></template>", new() { MaxNodes = 6 });
        Assert.IsType<DomTemplateElement>(Assert.Single(result.Document.Body!.ChildNodes));
    }

    [Fact]
    public void TemplateContentScriptsDoNotInvokeCompletionCallback()
    {
        var observed = new List<string?>();
        HtmlParser.Parse("<template><script>inert</script></template><script>ordinary</script>",
            options: null, cancellationToken: TestContext.Current.CancellationToken, document: new(),
            scriptCompleted: script => observed.Add(script.TextContent));

        Assert.Equal(new[] { "ordinary" }, observed);
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
    [InlineData("<!doctype html>", DomDocumentMode.NoQuirks, false)]
    [InlineData("<!doctype html SYSTEM 'about:legacy-compat'>", DomDocumentMode.NoQuirks, false)]
    [InlineData("<!doctype html SYSTEM 'ABOUT:LEGACY-COMPAT'>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype html SYSTEM ''>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype html PUBLIC ''>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype html PUBLIC 'legacy'>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype html PUBLIC 'HTML'>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype html PUBLIC 'HTMLsuffix'>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype html PUBLIC '-/W3C/DTD HTML 4.0 Transitional/EN'>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3O//DTD W3 HTML Strict 3.0//EN//'>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD HTML 4.0 Transitional//EN' 'anything'>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Transitional//EN'>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Transitional//EN' ''>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Transitional//EN' 'anything'>", DomDocumentMode.LimitedQuirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Frameset//EN' 'anything'>", DomDocumentMode.LimitedQuirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01//EN' 'anything'>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN'>", DomDocumentMode.LimitedQuirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD XHTML 1.0 Frameset//EN' ''>", DomDocumentMode.LimitedQuirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD XHTML 1.0 Strict//EN'>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype html PUBLIC '-//w3c//dtd xhtml 1.0 transitional//en'>", DomDocumentMode.LimitedQuirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD XHTML 1.0 Tran\u017fitional//EN'>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype html SYSTEM 'HTTP://WWW.IBM.COM/DATA/DTD/V11/IBMXHTML1-TRANSITIONAL.DTD'>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype html SYSTEM 'http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtdx'>", DomDocumentMode.NoQuirks, true)]
    [InlineData("<!doctype svg>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype>", DomDocumentMode.Quirks, true)]
    [InlineData("<!doctype html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' x>", DomDocumentMode.Quirks, true)]
    public void DoctypeClassificationPreservesRecoveryAndDiagnosticPolicy(string declaration, DomDocumentMode mode, bool invalid)
    {
        var result = Parse(declaration + "<p id=body>Readable</p>");
        Assert.Equal(mode, result.Document.Mode);
        Assert.Equal("Readable", result.Document.GetElementById("body")!.TextContent);
        Assert.Equal(invalid, result.Errors.Any(error => error.Code == "invalid-doctype"));
        Assert.NotNull(result.Document.Doctype);
    }

    [Theory]
    [InlineData("+//Silmaril//dtd html Pro v0r11 19970101//")]
    [InlineData("-//AS//DTD HTML 3.0 asWedit + extensions//")]
    [InlineData("-//IETF//DTD HTML 2.0 Strict Level 2//")]
    [InlineData("-//Microsoft//DTD Internet Explorer 3.0 Tables//")]
    [InlineData("-//O'Reilly and Associates//DTD HTML Extended Relaxed 1.0//")]
    [InlineData("-//SoftQuad Software//DTD HoTMetaL PRO 6.0::19990601::extensions to HTML 4.0//")]
    [InlineData("-//W3C//DTD HTML Experimental 970421//")]
    [InlineData("-//WebTechs//DTD Mozilla HTML//")]
    public void LegacyQuirksPrefixesMatchAsciiCaseAndAllowTrailingIdentifiers(string prefix)
    {
        var original = prefix + "EN";
        var result = Parse("<!doctype html PUBLIC \"" + original + "\" 'https://invalid.example/never-fetch.dtd'>");
        Assert.Equal(DomDocumentMode.Quirks, result.Document.Mode);
        Assert.Equal(original, result.Document.Doctype!.PublicId);
        Assert.Equal("https://invalid.example/never-fetch.dtd", result.Document.Doctype.SystemId);
        var mixed = Parse("<!doctype html PUBLIC \"" + original.ToUpperInvariant() + "\">");
        Assert.Equal(DomDocumentMode.Quirks, mixed.Document.Mode);
    }

    [Fact]
    public void LaterDoctypesCannotReplaceTheDocumentTypeOrMode()
    {
        var result = Parse("<!doctype html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN'>"
            + "<!doctype html PUBLIC 'HTML'><p>Body</p><!doctype html>");
        Assert.Equal(DomDocumentMode.LimitedQuirks, result.Document.Mode);
        Assert.Equal("-//W3C//DTD XHTML 1.0 Transitional//EN", result.Document.Doctype!.PublicId);
        Assert.Single(result.Document.ChildNodes.OfType<DomDocumentType>());
        Assert.Contains(result.Errors, error => error.Code == "unexpected-doctype");
    }

    [Fact]
    public void SimpleSelectOptionsBuildAsDirectChildren()
    {
        var document = Parse("<!doctype html><form><select name=mode><option value=a>Alpha</option><option selected>Beta</option></select></form>").Document;
        var select = Assert.IsType<DomElement>(Assert.Single(document.Descendants().OfType<DomElement>(), element => element.LocalName == "select"));
        Assert.Equal(["option", "option"], select.ChildNodes.OfType<DomElement>().Select(option => option.LocalName));
        Assert.Equal("Alpha", select.FirstChild!.FirstChild!.TextContent);
        Assert.Equal("Beta", select.LastChild!.TextContent);
    }

    [Fact]
    public void SelectOptgroupsRemainNestedInTheStaticDom()
    {
        var result = Parse("<!doctype html><select><optgroup label=Modes><option>a</option></optgroup>"
            + "<optgroup label=Other><option>b</option></optgroup></select>");
        var select = Assert.IsType<DomElement>(Assert.Single(result.Document.Descendants()
            .OfType<DomElement>(), element => element.LocalName == "select"));
        var groups = select.ChildNodes.OfType<DomElement>().ToArray();
        Assert.Equal(["optgroup", "optgroup"], groups.Select(group => group.LocalName));
        Assert.Equal(["a", "b"], groups.Select(group => Assert.IsType<DomElement>(group.FirstChild).TextContent));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void OptionAndOptgroupStartsOutsideSelectUseBodyInsertionRules()
    {
        var result = Parse("<!doctype html><datalist><option value=a>Alpha<optgroup label=More>"
            + "<option>Beta</option></optgroup><option value=c>Gamma<option value=d>Delta"
            + "</option></datalist>");
        var document = result.Document;
        var datalist = Assert.IsType<DomElement>(Assert.Single(document.Descendants()
            .OfType<DomElement>(), element => element.LocalName == "datalist"));
        var children = datalist.ChildNodes.OfType<DomElement>().ToArray();

        Assert.Equal(["option", "optgroup", "option", "option"], children.Select(element => element.LocalName));
        Assert.Equal("Alpha", children[0].TextContent);
        Assert.Equal("Gamma", children[2].TextContent);
        Assert.Equal("Delta", children[3].TextContent);
        Assert.Equal("Beta", Assert.IsType<DomElement>(children[1].FirstChild).TextContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SelectHrClosesOpenOptionGroupsAndRemainsInsideSelect()
    {
        var result = Parse("<!doctype html><select><optgroup><option>A<hr></select>");
        var select = Assert.IsType<DomElement>(Assert.Single(result.Document.Descendants()
            .OfType<DomElement>(), element => element.LocalName == "select"));

        Assert.Equal(["optgroup", "hr"], select.ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Equal("A", select.FirstChild!.TextContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void InputStartInSelectClosesSelectAndReprocessesInput()
    {
        var result = Parse("<!doctype html><select><option>before<input name=after><p>tail</p>");
        var body = result.Document.Body!;

        Assert.Equal(["select", "input", "p"], body.ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Equal("before", body.FirstChild!.TextContent);
        Assert.Equal("after", body.ChildNodes.OfType<DomElement>().ElementAt(1).GetAttribute("name"));
        Assert.Contains(result.Errors, error => error.Code == "input-start-tag-in-select");
    }

    [Fact]
    public void TextareaInSelectUsesBodyModeRcdataAndKeepsTheSelectOpen()
    {
        var result = Parse("<!doctype html><select><option>before<textarea>\n&lt;inside&gt;</textarea>after"
            + "</option><option>next</option></select>");
        var select = Assert.IsType<DomElement>(Assert.Single(result.Document.Body!.ChildNodes));
        var options = select.ChildNodes.OfType<DomElement>().ToArray();

        Assert.Equal(["option", "option"], options.Select(element => element.LocalName));
        Assert.Equal(["textarea"], options[0].ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Equal("<inside>", Assert.IsType<DomElement>(options[0].ChildNodes.OfType<DomElement>().Single()).TextContent);
        Assert.Equal("before<inside>after", options[0].TextContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void OtherSupportedBodyTokensUseInBodyRulesInsideSelect()
    {
        var result = Parse("<!doctype html><select><option>before<div>inside</div>after</option>"
            + "<optgroup><div>group</div><option>next</option></optgroup></select>");
        var select = Assert.IsType<DomElement>(Assert.Single(result.Document.Body!.ChildNodes));
        var elements = select.ChildNodes.OfType<DomElement>().ToArray();

        Assert.Equal(["option", "optgroup"], elements.Select(element => element.LocalName));
        Assert.Equal(["div"], elements[0].ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Equal("beforeinsideafter", elements[0].TextContent);
        Assert.Equal(["div", "option"], elements[1].ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Equal("group", elements[1].FirstChild!.TextContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void RubyAnnotationStartTagsApplyImpliedEndTagRules()
    {
        var result = Parse("<!doctype html><ruby><rb>base<rtc><rt>reading<rt>alternate</rt></rtc>"
            + "<rp>(</rp><rt>fallback</rt><rp>)</rp></ruby>");
        var ruby = Assert.IsType<DomElement>(Assert.Single(result.Document.Body!.ChildNodes));
        var children = ruby.ChildNodes.OfType<DomElement>().ToArray();
        var rb = Assert.IsType<DomElement>(children[0]);
        var rtc = Assert.IsType<DomElement>(children[1]);

        Assert.Equal(["rb", "rtc", "rp", "rt", "rp"], children.Select(element => element.LocalName));
        Assert.Equal("base", rb.TextContent);
        Assert.Equal(["rt", "rt"], rtc.ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Equal("reading", rtc.FirstChild!.TextContent);
        Assert.Equal("alternate", rtc.LastChild!.TextContent);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("<rb>outside</rb>", "ruby-base-start-tag-without-current-ruby")]
    [InlineData("<rtc>outside</rtc>", "ruby-base-start-tag-without-current-ruby")]
    [InlineData("<rp>outside</rp>", "ruby-annotation-start-tag-without-current-ruby-or-rtc")]
    [InlineData("<rt>outside</rt>", "ruby-annotation-start-tag-without-current-ruby-or-rtc")]
    public void RubyAnnotationStartOutsideRubyReportsParseError(string input, string expectedError)
    {
        var result = Parse(input);

        Assert.Contains(result.Errors, error => error.Code == expectedError);
    }

    [Fact]
    public void NestedSelectStartClosesCurrentSelectAndIgnoresNestedStart()
    {
        var result = Parse("<!doctype html><select><option>before<select><p>after</p>");
        var body = result.Document.Body!;

        Assert.Equal(["select", "p"], body.ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Single(Descendants(result.Document), element => element.LocalName == "select");
        Assert.Equal("after", body.LastChild!.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "nested-select-start-tag");
    }

    [Fact]
    public void SimpleTablesBuildSectionsRowsAndCells()
    {
        var result = Parse("<!doctype html><table id=t>\n<!--inside--><tr id=first><td>A<td>B</tr></tbody>\n"
            + "<tbody id=explicit><tr><th>C</th><td><p>D</p></td></tr></tbody></table>");
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        Assert.Equal("t", table.GetAttribute("id"));
        Assert.Contains(table.ChildNodes, node => node is DomComment comment && comment.Data == "inside");
        Assert.Contains(table.ChildNodes, node => node is DomText text && text.Data == "\n");
        var sections = table.ChildNodes.OfType<DomElement>().ToArray();
        Assert.Equal(["tbody", "tbody"], sections.Select(section => section.LocalName));
        Assert.Null(sections[0].GetAttribute("id"));
        Assert.Equal("explicit", sections[1].GetAttribute("id"));

        var firstRow = Assert.IsType<DomElement>(sections[0].FirstChild);
        Assert.Equal("first", firstRow.GetAttribute("id"));
        Assert.Equal(["td", "td"], firstRow.ChildNodes.OfType<DomElement>().Select(cell => cell.LocalName));
        Assert.Equal(["A", "B"], firstRow.ChildNodes.OfType<DomElement>().Select(cell => cell.TextContent));
        var secondRow = Assert.IsType<DomElement>(sections[1].FirstChild);
        Assert.Equal(["th", "td"], secondRow.ChildNodes.OfType<DomElement>().Select(cell => cell.LocalName));
        Assert.Equal("D", secondRow.LastChild!.TextContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TableCellsImplyRowsAndTableEndClosesOpenContainers()
    {
        var result = Parse("<!doctype html><table><tbody><td>x</table>");
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        var section = Assert.IsType<DomElement>(table.FirstChild);
        var row = Assert.IsType<DomElement>(section.FirstChild);
        var cell = Assert.IsType<DomElement>(row.FirstChild);
        Assert.Equal("tbody", section.LocalName);
        Assert.Equal("tr", row.LocalName);
        Assert.Equal("td", cell.LocalName);
        Assert.Equal("x", cell.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "cell-start-without-row");
        Assert.DoesNotContain(result.Errors, error => error.Code == "unclosed-elements-at-eof");
    }

    [Fact]
    public void TableColumnGroupsAndColumnsBuildWithImpliedGroups()
    {
        var result = Parse("<!doctype html><table><colgroup id=explicit>\n<col span=2>\n</colgroup>"
            + "<col id=single><tbody><tr><td>x</td></tr></tbody></table>");
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        var groups = table.ChildNodes.OfType<DomElement>().Where(element => element.LocalName == "colgroup").ToArray();
        Assert.Equal(2, groups.Length);
        Assert.Equal("explicit", groups[0].GetAttribute("id"));
        Assert.Equal("2", Assert.IsType<DomElement>(groups[0].ChildNodes.OfType<DomElement>().Single()).GetAttribute("span"));
        Assert.Null(groups[1].GetAttribute("id"));
        Assert.Equal("single", Assert.IsType<DomElement>(groups[1].ChildNodes.OfType<DomElement>().Single()).GetAttribute("id"));
        Assert.Contains(groups[0].ChildNodes, node => node is DomText text && text.Data == "\n");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TableCaptionsBuildWithInBodyContentAndReturnToTableMode()
    {
        var result = Parse("<!doctype html><table><caption id=caption>Title <strong>text</strong><p>detail</p></caption>"
            + "<tbody><tr><td>cell</td></tr></tbody></table>");
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        var children = table.ChildNodes.OfType<DomElement>().ToArray();
        Assert.Equal(["caption", "tbody"], children.Select(element => element.LocalName));
        Assert.Equal("caption", children[0].GetAttribute("id"));
        Assert.Equal("Title textdetail", children[0].TextContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TableScriptAndStyleUseInertRawTextHandling()
    {
        var result = Parse("<!doctype html><table><style>td > span { color: red; }</style>"
            + "<script>if (a < b) value = '<td>';</script><tbody><tr>"
            + "<script>const row = '<tr>';</script><td>cell</td></tr>"
            + "<style>td { color: blue; }</style></tbody></table>");
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        var tableStyle = Assert.Single(table.ChildNodes.OfType<DomElement>(), element => element.LocalName == "style");
        var tableScript = Assert.Single(table.ChildNodes.OfType<DomElement>(), element => element.LocalName == "script");
        Assert.Equal("td > span { color: red; }", tableStyle.TextContent);
        Assert.Equal("if (a < b) value = '<td>';", tableScript.TextContent);

        var section = Assert.IsType<DomElement>(table.ChildNodes.OfType<DomElement>()
            .Single(element => element.LocalName == "tbody"));
        var row = Assert.IsType<DomElement>(section.ChildNodes.OfType<DomElement>()
            .Single(element => element.LocalName == "tr"));
        var rowScript = Assert.Single(row.ChildNodes.OfType<DomElement>(), element => element.LocalName == "script");
        var sectionStyle = Assert.Single(section.ChildNodes.OfType<DomElement>(), element => element.LocalName == "style");
        Assert.Equal("const row = '<tr>';", rowScript.TextContent);
        Assert.Equal("td { color: blue; }", sectionStyle.TextContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void HiddenInputsAreInsertedAsVoidTableChildren()
    {
        var result = Parse("<!doctype html><table><input type=HIDDEN id=table-input>"
            + "<tbody><tr><input type=hidden id=row-input><td>cell</td></tr></tbody></table>");
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        var tableInput = Assert.IsType<DomElement>(table.FirstChild);
        Assert.Equal("input", tableInput.LocalName);
        Assert.Equal("HIDDEN", tableInput.GetAttribute("type"));
        Assert.Equal("table-input", tableInput.GetAttribute("id"));
        Assert.Empty(tableInput.ChildNodes);

        var section = Assert.IsType<DomElement>(table.LastChild);
        var row = Assert.IsType<DomElement>(section.FirstChild);
        var rowInput = Assert.IsType<DomElement>(row.FirstChild);
        Assert.Equal("input", rowInput.LocalName);
        Assert.Equal("row-input", rowInput.GetAttribute("id"));
        Assert.Empty(rowInput.ChildNodes);
        Assert.Equal(2, result.Errors.Count(error => error.Code == "hidden-input-in-table"));
    }

    [Theory]
    [InlineData("", "table")]
    [InlineData("<tbody>", "tbody")]
    [InlineData("<tbody><tr>", "tr")]
    public void FormStartsInTableModesUseTheFormPointerException(string context, string expectedParent)
    {
        var result = Parse($"<!doctype html><table>{context}<form id=first>"
            + "<form id=ignored></form><td>cell</td></table>");
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        var form = Assert.Single(Descendants(table), element => element.LocalName == "form");
        Assert.Equal("first", form.GetAttribute("id"));
        Assert.Equal(expectedParent, Assert.IsType<DomElement>(form.ParentNode).LocalName);
        Assert.Equal(2, result.Errors.Count(error => error.Code == "form-start-in-table"));
        Assert.Single(result.Errors, error => error.Code == "unexpected-form-end-tag");
    }

    [Theory]
    [InlineData("")]
    [InlineData("<tbody>")]
    [InlineData("<tbody><tr>")]
    public void HtmlStartInTableModesMergesRootAttributes(string context)
    {
        var result = Parse($"<!doctype html><html lang=original><table>{context}"
            + "<html lang=ignored data-added=merged><td>cell</td></table>");
        var root = result.Document.DocumentElement!;
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        Assert.Equal("original", root.GetAttribute("lang"));
        Assert.Equal("merged", root.GetAttribute("data-added"));
        Assert.Equal("tbody", Assert.IsType<DomElement>(table.FirstChild).LocalName);
        Assert.Contains(result.Errors, error => error.Code == "html-start-in-body");
    }

    [Fact]
    public void NestedTableStartClosesAndReprocessesTheCurrentTable()
    {
        var result = Parse("<!doctype html><div id=host><table id=first><table id=second>"
            + "<tbody><tr><td>cell</td></tr></tbody></table></div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        var tables = host.ChildNodes.OfType<DomElement>().ToArray();
        Assert.Equal(["first", "second"], tables.Select(table => table.GetAttribute("id")));
        Assert.Empty(tables[0].ChildNodes);
        Assert.Equal("cell", tables[1].TextContent);
        Assert.Contains(result.Errors, error => error.Code == "nested-table-start-tag");
    }

    [Fact]
    public void NestedTableInCellRestoresCellInsertionModeWhenClosed()
    {
        var result = Parse("<!doctype html><table id=outer><tbody><tr><td id=outer-cell>before"
            + "<table id=inner><tbody><tr><td>inside</td></tr></tbody></table>"
            + "after</td></tr></tbody></table>");
        var tables = Descendants(result.Document).Where(element => element.LocalName == "table").ToArray();
        var outerCell = Assert.Single(Descendants(tables[0]), element => element.GetAttribute("id") == "outer-cell");
        var innerTable = Assert.Single(outerCell.ChildNodes.OfType<DomElement>(), element => element.LocalName == "table");
        Assert.Equal("inner", innerTable.GetAttribute("id"));
        Assert.Equal("beforeinsideafter", outerCell.TextContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TableTokensCloseCaptionAndAreReprocessed()
    {
        var result = Parse("<!doctype html><table><caption>Title<tr><td>cell</td></tr></table><p>after</p>");
        var table = Assert.Single(Descendants(result.Document), element => element.LocalName == "table");
        Assert.Equal(["caption", "tbody"], table.ChildNodes.OfType<DomElement>().Select(element => element.LocalName));
        Assert.Equal("Title", table.FirstChild!.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "caption-closed-by-table-token");
        Assert.Equal("after", result.Document.Body!.LastChild!.TextContent);
    }

    [Fact]
    public void NonWhitespaceTableTextIsFosterParentedWithAdjacentTextMerging()
    {
        var result = Parse("<!doctype html><div id=host>before<table> \ntext \n<tbody><tr><td>cell</td></tr>"
            + "</tbody></table></div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before \ntext \n", Assert.IsType<DomText>(node).Data),
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName));
        Assert.Contains(result.Errors, error => error.Code == "non-whitespace-table-text");
    }

    [Fact]
    public void FosteredTableTextRunsSeparatedByTagsMergeBeforeTable()
    {
        var result = Parse("<!doctype html><div id=host>A<table>B<tr>C</tr>D</table></div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        Assert.Equal("ABCD", Assert.IsType<DomText>(host.FirstChild).Data);
        Assert.Equal("table", Assert.IsType<DomElement>(host.LastChild).LocalName);
        Assert.Equal(3, result.Errors.Count(error => error.Code == "non-whitespace-table-text"));
    }

    [Fact]
    public void NonWhitespaceColumnGroupTextFallsBackToTableTextFosterParenting()
    {
        var result = Parse("<!doctype html><div id=host><table><colgroup>text</colgroup></table></div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        Assert.Equal("text", Assert.IsType<DomText>(host.FirstChild).Data);
        Assert.Equal("table", Assert.IsType<DomElement>(host.LastChild).LocalName);
        Assert.Contains(result.Errors, error => error.Code == "non-whitespace-column-group-content");
        Assert.Contains(result.Errors, error => error.Code == "non-whitespace-table-text");
    }

    public static IEnumerable<object[]> OrdinaryElementsInTableModes()
    {
        string[] names = ["widget", "custom-widget", "meter"];
        (string Start, string End)[] tables =
        [
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>"),
            ("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")
        ];
        foreach (var name in names)
        {
            foreach (var table in tables)
            {
                yield return [name, table.Start, table.End];
            }
        }
    }

    [Theory]
    [MemberData(nameof(OrdinaryElementsInTableModes))]
    public void OtherOrdinaryElementsInTableModesUseFosterParenting(string name, string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + $"<{name} data-value=x>content</{name}>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal(name, element.LocalName);
                Assert.Equal("x", element.GetAttribute("data-value"));
                Assert.Equal("content", element.TextContent);
            },
            node =>
            {
                var table = Assert.IsType<DomElement>(node);
                Assert.Equal("table", table.LocalName);
                Assert.Equal("cell", table.TextContent);
            },
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{name}-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{name}-end-in-table");
    }

    [Fact]
    public void UnmatchedCustomElementEndInTableUsesInBodyRecovery()
    {
        var result = Parse("<!doctype html><table></custom-widget><tr><td>cell</td></tr></table>");
        Assert.Equal("cell", Assert.Single(Descendants(result.Document), element => element.LocalName == "td").TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-custom-widget-end-in-table");
        Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
    }

    [Fact]
    public void NestedCustomElementsInTableRemainNestedAtFosterParent()
    {
        var result = Parse("<!doctype html><div id=host><table><outer-widget><inner-widget>x</inner-widget></outer-widget>"
            + "<tr><td>cell</td></tr></table></div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        Assert.Collection(host.ChildNodes,
            node =>
            {
                var outer = Assert.IsType<DomElement>(node);
                Assert.Equal("outer-widget", outer.LocalName);
                var inner = Assert.IsType<DomElement>(Assert.Single(outer.ChildNodes));
                Assert.Equal("inner-widget", inner.LocalName);
                Assert.Equal("x", inner.TextContent);
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName));
    }

    public static IEnumerable<object[]> PhrasingElementsInTableModes()
    {
        string[] names = ["abbr", "cite", "data", "dfn", "kbd", "mark", "q", "samp", "sub", "sup", "time", "var"];
        (string Start, string End)[] tables =
        [
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>"),
            ("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")
        ];
        foreach (var name in names)
        {
            foreach (var table in tables)
            {
                yield return [name, table.Start, table.End];
            }
        }
    }

    [Theory]
    [MemberData(nameof(PhrasingElementsInTableModes))]
    public void OrdinaryPhrasingElementsInTableModesUseFosterParenting(string name, string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + $"<{name} id=item data-value=x>content</{name}>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal(name, element.LocalName);
                Assert.Equal("x", element.GetAttribute("data-value"));
                Assert.Equal("content", element.TextContent);
            },
            node => Assert.Equal("cell", Assert.IsType<DomElement>(node).TextContent),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{name}-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{name}-end-in-table");
    }

    [Fact]
    public void UnmatchedPhrasingEndInTableUsesInBodyRecovery()
    {
        var result = Parse("<!doctype html><table></mark><tr><td>cell</td></tr></table>");
        Assert.Equal("cell", Assert.Single(Descendants(result.Document), element => element.LocalName == "td").TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-mark-end-in-table");
        Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
    }

    [Theory]
    [InlineData("<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    public void SpanStartAndEndInTableModesUseFosterParenting(string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "<span id=fostered>content</span>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var span = Assert.IsType<DomElement>(node);
                Assert.Equal("span", span.LocalName);
                Assert.Equal("fostered", span.GetAttribute("id"));
                Assert.Equal("content", span.TextContent);
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Contains(result.Errors, error => error.Code == "fostered-span-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-span-end-in-table");
    }

    [Theory]
    [InlineData("<table>", "<tr><td>cell</td></tr></table>", false)]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>", false)]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>", false)]
    [InlineData("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>", true)]
    public void LabelStartAndEndInTableModesUseFosterParenting(string tableStart, string tableEnd, bool hasColumnGroup)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "<label id=label for=control data-kind=example>content</label>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        DomElement? table = null;
        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var label = Assert.IsType<DomElement>(node);
                Assert.Equal("label", label.LocalName);
                Assert.Equal("label", label.GetAttribute("id"));
                Assert.Equal("control", label.GetAttribute("for"));
                Assert.Equal("example", label.GetAttribute("data-kind"));
                Assert.Equal("content", Assert.IsType<DomText>(Assert.Single(label.ChildNodes)).Data);
            },
            node => table = Assert.IsType<DomElement>(node),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Equal("table", table!.LocalName);
        Assert.Equal(hasColumnGroup ? new[] { "colgroup", "tbody" } : new[] { "tbody" },
            table.ChildNodes.Cast<DomElement>().Select(element => element.LocalName));
        var body = Assert.IsType<DomElement>(table.LastChild);
        var row = Assert.IsType<DomElement>(Assert.Single(body.ChildNodes));
        Assert.Equal("tr", row.LocalName);
        var cell = Assert.IsType<DomElement>(Assert.Single(row.ChildNodes));
        Assert.Equal("td", cell.LocalName);
        Assert.Equal("cell", cell.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-label-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-label-end-in-table");
    }

    [Theory]
    [InlineData("<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    public void UnmatchedLabelEndInTableModesUsesFosteredInBodyRecovery(string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "</label>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Equal("cell", Assert.Single(Descendants(result.Document), element => element.LocalName == "td").TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-label-end-in-table");
        Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
    }

    [Theory]
    [InlineData("<table>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    public void AnchorStartAndEndInTableModesUseFosterParenting(string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "<a id=link href=/next>linked</a>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        var anchor = Assert.IsType<DomElement>(host.ChildNodes[1]);
        var table = Assert.IsType<DomElement>(host.ChildNodes[2]);

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal("a", element.LocalName);
                Assert.Equal("link", element.GetAttribute("id"));
                Assert.Equal("/next", element.GetAttribute("href"));
                Assert.Equal("linked", element.TextContent);
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Same(host, anchor.ParentNode);
        Assert.Equal("cell", table.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-a-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-a-end-in-table");
    }

    [Fact]
    public void AnchorAdoptionRequiringTableFosterParentingRemainsUnsupported()
    {
        Assert.Throws<UnsupportedHtmlException>(() =>
            Parse("<!doctype html><div><table><a><div>content</a></div></table></div>"));
    }

    [Fact]
    public void NestedAnchorInsideTableUsesBoundedFosteredInBodyRecovery()
    {
        var result = Parse("<!doctype html><div id=host><a id=outer><table><a id=inner>text");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        var outer = Assert.IsType<DomElement>(Assert.Single(host.ChildNodes));
        var inner = Assert.IsType<DomElement>(outer.FirstChild);
        var table = Assert.IsType<DomElement>(outer.LastChild);

        Assert.Equal("a", outer.LocalName);
        Assert.Equal("outer", outer.GetAttribute("id"));
        Assert.Equal("a", inner.LocalName);
        Assert.Equal("inner", inner.GetAttribute("id"));
        Assert.Equal("text", inner.TextContent);
        Assert.Equal("table", table.LocalName);
        Assert.Contains(result.Errors, error => error.Code == "fostered-a-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "nested-a-start-tag");
    }

    [Theory]
    [InlineData("<table>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    public void FormattingStartAndEndInTableModesUseFosterParenting(string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "<b id=bold><i id=italic>content</i></b>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        var bold = Assert.IsType<DomElement>(host.ChildNodes[1]);
        var italic = Assert.IsType<DomElement>(Assert.Single(bold.ChildNodes));
        var table = Assert.IsType<DomElement>(host.ChildNodes[2]);

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal("b", element.LocalName);
                Assert.Equal("bold", element.GetAttribute("id"));
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Equal("i", italic.LocalName);
        Assert.Equal("italic", italic.GetAttribute("id"));
        Assert.Equal("content", italic.TextContent);
        Assert.Same(bold, italic.ParentNode);
        Assert.Equal("cell", table.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-b-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-i-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-i-end-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-b-end-in-table");
    }

    [Theory]
    [InlineData("<table>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    public void RubyAnnotationElementsInTableModesUseFosterParenting(string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "<ruby><rb>base<rtc><rt>reading<rt>alternate</rt></rtc><rp>(</rp></rb></ruby>"
            + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        var ruby = Assert.IsType<DomElement>(host.ChildNodes[1]);
        var table = Assert.IsType<DomElement>(host.ChildNodes[2]);

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node => Assert.Equal("ruby", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Equal("basereadingalternate(", ruby.TextContent);
        Assert.Equal("cell", table.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-ruby-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-ruby-end-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-rb-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-rtc-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-rt-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-rp-start-in-table");
    }

    [Theory]
    [InlineData("<table>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    public void SelectStartInTableModesUsesFosterParenting(string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "<select id=choice><option>selected</option></select>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        var select = Assert.IsType<DomElement>(host.ChildNodes[1]);
        var table = Assert.IsType<DomElement>(host.ChildNodes[2]);
        var option = Assert.IsType<DomElement>(Assert.Single(select.ChildNodes));

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal("select", element.LocalName);
                Assert.Equal("choice", element.GetAttribute("id"));
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Equal("option", option.LocalName);
        Assert.Equal("selected", option.TextContent);
        Assert.Equal("cell", table.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-select-start-in-table");
    }

    [Fact]
    public void UnmatchedSelectEndInTableModeUsesFosteredInBodyRecovery()
    {
        var result = Parse("<!doctype html><table></select><tbody><tr><td>cell</td></tr></tbody></table>");

        Assert.Equal("cell", Assert.Single(Descendants(result.Document), element => element.LocalName == "td").TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-select-end-in-table");
        Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
    }

    [Fact]
    public void NestedNobrInsideTableUsesBoundedInBodyRecovery()
    {
        var result = Parse("<!doctype html><div id=host><nobr id=outer><table>"
            + "<nobr id=inner>text</nobr><tbody><tr><td>cell</td></tr></tbody></table></nobr></div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        var outer = Assert.IsType<DomElement>(Assert.Single(host.ChildNodes));
        var inner = Assert.IsType<DomElement>(outer.FirstChild);
        var table = Assert.IsType<DomElement>(outer.LastChild);

        Assert.Equal("nobr", outer.LocalName);
        Assert.Equal("outer", outer.GetAttribute("id"));
        Assert.Equal("nobr", inner.LocalName);
        Assert.Equal("inner", inner.GetAttribute("id"));
        Assert.Equal("text", inner.TextContent);
        Assert.Equal("table", table.LocalName);
        Assert.Equal("cell", table.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-nobr-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-nobr-end-in-table");
    }

    [Theory]
    [InlineData("<table>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("<table><colgroup>", "<tbody><tr><td>cell</td></tr></tbody></table>")]
    public void OptionAndOptgroupStartAndEndInTableModesUseFosterParenting(string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "<optgroup id=group label=group><option id=option value=item>item</option></optgroup>"
            + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");
        var group = Assert.IsType<DomElement>(host.ChildNodes[1]);
        var option = Assert.IsType<DomElement>(Assert.Single(group.ChildNodes));
        var table = Assert.IsType<DomElement>(host.ChildNodes[2]);

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal("optgroup", element.LocalName);
                Assert.Equal("group", element.GetAttribute("id"));
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Equal("group", group.GetAttribute("label"));
        Assert.Equal("option", option.LocalName);
        Assert.Equal("option", option.GetAttribute("id"));
        Assert.Equal("item", option.GetAttribute("value"));
        Assert.Equal("item", option.TextContent);
        Assert.Same(group, option.ParentNode);
        Assert.Equal("cell", table.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-optgroup-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-option-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-option-end-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-optgroup-end-in-table");
    }

    [Fact]
    public void SpanInTemplateTableModeIsFosteredWithinTemplateContent()
    {
        var result = Parse("<template><table><span>content</span><tbody><tr><td>cell</td></tr></tbody></table></template>");
        var template = Assert.IsType<DomTemplateElement>(
            Assert.Single(Descendants(result.Document), element => element.LocalName == "template"));
        var span = Assert.IsType<DomElement>(template.Content.FirstChild);
        var table = Assert.IsType<DomElement>(template.Content.LastChild);

        Assert.Equal("span", span.LocalName);
        Assert.Same(template.Content, span.ParentNode);
        Assert.Equal("content", span.TextContent);
        Assert.Equal("table", table.LocalName);
        Assert.Equal("cell", table.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-span-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == "fostered-span-end-in-table");
    }

    [Theory]
    [InlineData("<table>")]
    [InlineData("<table><tbody>")]
    [InlineData("<table><tbody><tr>")]
    [InlineData("<table><colgroup>")]
    public void PlaintextStartInTableModesIsFosteredAndConsumesTheRemainder(string tableStart)
    {
        const string remainder = "literal <b>markup</b></plaintext></table>after</div>";
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + "<plaintext id=fostered>" + remainder);
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var plaintext = Assert.IsType<DomElement>(node);
                Assert.Equal("plaintext", plaintext.LocalName);
                Assert.Equal("fostered", plaintext.GetAttribute("id"));
                Assert.Equal(remainder, plaintext.TextContent);
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName));
        Assert.Contains(result.Errors, error => error.Code == "fostered-plaintext-start-in-table");
        Assert.DoesNotContain(Descendants(result.Document), element => element.LocalName == "b");
    }

    [Fact]
    public void PlaintextStartInTemplateTableModeIsFosteredWithinTemplateContent()
    {
        const string remainder = "literal <b>markup</b></plaintext></table>";
        var result = Parse("<template><table><plaintext id=fostered>" + remainder);
        var template = Assert.IsType<DomTemplateElement>(
            Assert.Single(Descendants(result.Document), element => element.LocalName == "template"));

        Assert.Collection(template.Content.ChildNodes,
            node =>
            {
                var plaintext = Assert.IsType<DomElement>(node);
                Assert.Equal("plaintext", plaintext.LocalName);
                Assert.Same(template.Content, plaintext.ParentNode);
                Assert.Equal("fostered", plaintext.GetAttribute("id"));
                Assert.Equal(remainder, plaintext.TextContent);
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName));
        Assert.Contains(result.Errors, error => error.Code == "fostered-plaintext-start-in-table");
    }

    [Fact]
    public void UnmatchedPlaintextEndTagInTableModeUsesFosteredInBodyRecovery()
    {
        var result = Parse("<table></plaintext><tr><td>cell</td></tr></table>");

        Assert.Equal("cell", Assert.Single(Descendants(result.Document), element => element.LocalName == "td").TextContent);
        Assert.Contains(result.Errors, error => error.Code == "fostered-plaintext-end-in-table");
        Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
    }

    [Theory]
    [InlineData("<input id=plain>", "plain")]
    [InlineData("<input type=text id=text>", "text")]
    [InlineData("<input type=h\u0131dden id=unicode>", "unicode")]
    public void NonHiddenInputsInTableAreFosterParentedBeforeTheTable(string input, string expectedId)
    {
        var result = Parse("<!doctype html><div id=host>before<table>" + input
            + "<tr><td>cell</td></tr></table>after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal("input", element.LocalName);
                Assert.Empty(element.ChildNodes);
                Assert.Equal(expectedId, element.GetAttribute("id"));
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Contains(result.Errors, error => error.Code == "non-hidden-input-in-table");
    }

    [Theory]
    [InlineData("<tbody><tr><input id=row-input><td>cell</td></tr></tbody>", "row-input")]
    [InlineData("<tbody><input id=section-input><tr><td>cell</td></tr></tbody>", "section-input")]
    public void NonHiddenInputsInTableSectionsAndRowsAreFosterParentedBeforeTheTable(string content, string expectedId)
    {
        var result = Parse("<!doctype html><div id=host><table>" + content + "</table></div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

        Assert.Equal(expectedId, Assert.IsType<DomElement>(host.FirstChild).GetAttribute("id"));
        Assert.Equal("table", Assert.IsType<DomElement>(host.LastChild).LocalName);
        Assert.Contains(result.Errors, error => error.Code == "non-hidden-input-in-table");
    }

    [Fact]
    public void NonHiddenInputsInsideCellsRemainInTheCell()
    {
        var result = Parse("<!doctype html><table><tr><td><input id=cell-input></td></tr></table>");
        var cell = Assert.Single(Descendants(result.Document), element => element.LocalName == "td");

        Assert.Equal("cell-input", Assert.IsType<DomElement>(cell.FirstChild).GetAttribute("id"));
        Assert.DoesNotContain(result.Errors, error => error.Code == "non-hidden-input-in-table");
    }

    [Theory]
    [InlineData("<table>", "<tr><td>cell</td></tr></table>", "<br id=break>", "br", "break")]
    [InlineData("<table>", "<tr><td>cell</td></tr></table>", "<hr id=rule>", "hr", "rule")]
    [InlineData("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>", "<br id=break>", "br", "break")]
    [InlineData("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>", "<hr id=rule>", "hr", "rule")]
    public void BrAndHrInTableModesAreFosterParentedBeforeTheTable(
        string tableStart, string tableEnd, string token, string expectedName, string expectedId)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart + token + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal(expectedName, element.LocalName);
                Assert.Equal(expectedId, element.GetAttribute("id"));
                Assert.Empty(element.ChildNodes);
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{expectedName}-in-table");
    }

    [Theory]
    [InlineData("div", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("div", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("div", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("p", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("p", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("p", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("h1", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("h1", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("h1", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("h2", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("h2", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("h2", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("h3", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("h3", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("h3", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("h4", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("h4", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("h4", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("h5", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("h5", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("h5", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("h6", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("h6", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("h6", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    public void BlockAndHeadingStartAndEndInTableModesUseFosterParenting(
        string tagName, string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + $"<{tagName} id=fostered>content</{tagName}>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal(tagName, element.LocalName);
                Assert.Equal("fostered", element.GetAttribute("id"));
                Assert.Equal("content", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-end-in-table");
    }

    [Theory]
    [InlineData("pre", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("pre", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("pre", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    [InlineData("listing", "<table>", "<tr><td>cell</td></tr></table>")]
    [InlineData("listing", "<table><tbody>", "<tr><td>cell</td></tr></tbody></table>")]
    [InlineData("listing", "<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")]
    public void PreAndListingStartAndEndInTableModesUseFosterParenting(
        string tagName, string tableStart, string tableEnd)
    {
        var result = Parse("<!doctype html><div id=host>before" + tableStart
            + $"<{tagName} id=fostered>\ncontent</{tagName}>" + tableEnd + "after</div>");
        var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

        Assert.Collection(host.ChildNodes,
            node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
            node =>
            {
                var element = Assert.IsType<DomElement>(node);
                Assert.Equal(tagName, element.LocalName);
                Assert.Equal("fostered", element.GetAttribute("id"));
                Assert.Equal("content", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
            },
            node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
            node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-start-in-table");
        Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-end-in-table");
    }

    [Theory]
    [InlineData("address")]
    [InlineData("article")]
    [InlineData("aside")]
    [InlineData("blockquote")]
    [InlineData("center")]
    [InlineData("details")]
    [InlineData("dialog")]
    [InlineData("dir")]
    [InlineData("dl")]
    [InlineData("fieldset")]
    [InlineData("figcaption")]
    [InlineData("figure")]
    [InlineData("footer")]
    [InlineData("header")]
    [InlineData("hgroup")]
    [InlineData("main")]
    [InlineData("menu")]
    [InlineData("nav")]
    [InlineData("ol")]
    [InlineData("search")]
    [InlineData("section")]
    [InlineData("summary")]
    [InlineData("ul")]
    public void RemainingBlockStartAndEndTagsInTableModesUseFosterParenting(string tagName)
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + $"<{tagName} id=fostered>content</{tagName}>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal(tagName, element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Equal("content", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-start-in-table");
            Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-end-in-table");
        }
    }

    [Theory]
    [InlineData("li")]
    [InlineData("dd")]
    [InlineData("dt")]
    public void ListItemStartAndEndTagsInTableModesUseFosterParenting(string tagName)
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + $"<{tagName} id=fostered>content</{tagName}>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal(tagName, element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Equal("content", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-start-in-table");
            Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-end-in-table");
        }
    }

    [Fact]
    public void ButtonStartAndEndInTableModesUseFosterParenting()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "<button id=fostered>content</button>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal("button", element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Equal("content", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-button-start-in-table");
            Assert.Contains(result.Errors, error => error.Code == "fostered-button-end-in-table");
        }
    }

    [Fact]
    public void TextAfterFosteredButtonStartIsInsertedIntoTheButton()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "<button id=fostered>content</button>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal("button", element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Equal("content", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "unexpected-character-in-table");
        }
    }

    [Fact]
    public void TextareaInTableModesUsesFosterParentingAndRestoresTableMode()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "<textarea id=fostered>\nalpha<b>beta</b></textarea>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal("textarea", element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Equal("alpha<b>beta</b>", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-textarea-start-in-table");
        }
    }

    [Fact]
    public void UnmatchedTextareaEndTagInTableModesUsesFosteredInBodyRecovery()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "</textarea>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-textarea-end-in-table");
            Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
        }
    }

    [Fact]
    public void TitleInTableModesUsesFosteredRcdataAndRestoresTableMode()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "<title id=fostered>\nA&amp;<b>B</b></title>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal("title", element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Equal("\nA&<b>B</b>", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-title-start-in-table");
        }
    }

    [Fact]
    public void UnmatchedTitleEndTagInTableModesUsesFosteredInBodyRecovery()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "</title>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-title-end-in-table");
            Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
        }
    }

    [Theory]
    [InlineData("iframe")]
    [InlineData("noembed")]
    [InlineData("noframes")]
    public void RawTextElementsInTableModesUseFosteredInsertion(string tagName)
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + $"<{tagName} id=fostered>\nA&amp;<b>B</b></{tagName}>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal(tagName, element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Equal("\nA&amp;<b>B</b>", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-start-in-table");
        }
    }

    [Theory]
    [InlineData("iframe")]
    [InlineData("noembed")]
    [InlineData("noframes")]
    public void UnmatchedRawTextEndTagsInTableModesUseFosteredInBodyRecovery(string tagName)
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + $"</{tagName}>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-end-in-table");
            Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
        }
    }

    [Fact]
    public void XmpInTableModesUsesFosteredRawTextAndRestoresTableMode()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "<xmp id=fostered>\nA&amp;<b>B</b></xmp>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal("xmp", element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Equal("\nA&amp;<b>B</b>", Assert.IsType<DomText>(Assert.Single(element.ChildNodes)).Data);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-xmp-start-in-table");
        }
    }

    [Fact]
    public void UnmatchedXmpEndTagInTableModesUsesFosteredInBodyRecovery()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "</xmp>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-xmp-end-in-table");
            Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
        }
    }

    [Fact]
    public void ImageStartInTableModesUsesFosterParenting()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "<img id=fostered></img>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal("img", element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Empty(element.ChildNodes);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-img-start-in-table");
            Assert.Contains(result.Errors, error => error.Code == "fostered-img-end-in-table");
        }
    }

    [Fact]
    public void ImageAliasInTableModesUsesFosterParenting()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "<image id=fostered></image>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal("img", element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Empty(element.ChildNodes);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-image-start-in-table");
            Assert.Contains(result.Errors, error => error.Code == "image-start-tag");
            Assert.Contains(result.Errors, error => error.Code == "fostered-image-end-in-table");
            Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
        }
    }

    [Fact]
    public void WbrStartInTableModesUsesFosterParenting()
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + "<wbr id=fostered></wbr>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal("wbr", element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Empty(element.ChildNodes);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == "fostered-wbr-start-in-table");
            Assert.Contains(result.Errors, error => error.Code == "fostered-wbr-end-in-table");
        }
    }

    [Theory]
    [InlineData("area")]
    [InlineData("base")]
    [InlineData("basefont")]
    [InlineData("bgsound")]
    [InlineData("embed")]
    [InlineData("keygen")]
    [InlineData("link")]
    [InlineData("meta")]
    [InlineData("param")]
    [InlineData("source")]
    [InlineData("track")]
    public void RemainingVoidStartAndEndTagsInTableModesUseFosterParenting(string tagName)
    {
        var tableModes = new[]
        {
            ("<table>", "<tr><td>cell</td></tr></table>"),
            ("<table><tbody>", "<tr><td>cell</td></tr></tbody></table>"),
            ("<table><tbody><tr>", "<td>cell</td></tr></tbody></table>")
        };

        foreach (var (tableStart, tableEnd) in tableModes)
        {
            var result = Parse("<!doctype html><div id=host>before" + tableStart
                + $"<{tagName} id=fostered></{tagName}>" + tableEnd + "after</div>");
            var host = Assert.Single(Descendants(result.Document), element => element.GetAttribute("id") == "host");

            Assert.Collection(host.ChildNodes,
                node => Assert.Equal("before", Assert.IsType<DomText>(node).Data),
                node =>
                {
                    var element = Assert.IsType<DomElement>(node);
                    Assert.Equal(tagName, element.LocalName);
                    Assert.Equal("fostered", element.GetAttribute("id"));
                    Assert.Empty(element.ChildNodes);
                },
                node => Assert.Equal("table", Assert.IsType<DomElement>(node).LocalName),
                node => Assert.Equal("after", Assert.IsType<DomText>(node).Data));
            Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-start-in-table");
            Assert.Contains(result.Errors, error => error.Code == $"fostered-{tagName}-end-in-table");
        }
    }

    [Theory]
    [InlineData("<table><caption>x<table></table></caption></table>")]
    [InlineData("<table><b><p>x</b></table>")]
    [InlineData("<table><svg></svg></table>")]
    public void UnsupportedTableRecoveryFailsExplicitly(string input) =>
        Assert.Throws<UnsupportedHtmlException>(() => Parse(input));

    [Fact]
    public void UnclosedSelectReportsASelectSpecificParseError()
    {
        var result = Parse("<!doctype html><select><option>x");
        Assert.Contains(result.Errors, error => error.Code == "unclosed-select");
    }

    [Theory]
    [InlineData("<svg><path>")]
    [InlineData("<math>x")]
    [InlineData("<form><div></form>x")]
    [InlineData("<div><form></div><input>")]
    [InlineData("<dl><dd><form></dl>x")]
    [InlineData("<noscript>x")]
    [InlineData("<p><b>x</p><table>y</table>")]
    public void UnsupportedTreeAlgorithmsFailExplicitly(string input) =>
        Assert.Throws<UnsupportedHtmlException>(() => Parse(input));

    [Theory]
    [InlineData("a")]
    [InlineData("nobr")]
    public void NestedFormattingStartsAdoptAndReconstructDescendants(string tag)
    {
        var result = Parse($"<!doctype html><{tag} id=old><i class=kept>one<{tag} id=new>two</{tag}>three");
        var body = result.Document.Body!;
        var original = Assert.IsType<DomElement>(body.ChildNodes[0]);
        var originalItalic = Assert.IsType<DomElement>(original.FirstChild);
        var rebuiltItalic = Assert.IsType<DomElement>(body.ChildNodes[1]);
        var replacement = Assert.IsType<DomElement>(rebuiltItalic.FirstChild);
        Assert.Equal("old", original.GetAttribute("id"));
        Assert.Equal("one", originalItalic.TextContent);
        Assert.Equal("i", rebuiltItalic.LocalName);
        Assert.Equal("kept", rebuiltItalic.GetAttribute("class"));
        Assert.NotSame(originalItalic, rebuiltItalic);
        Assert.Equal(tag, replacement.LocalName);
        Assert.Equal("new", replacement.GetAttribute("id"));
        Assert.Equal("two", replacement.TextContent);
        Assert.Equal("three", Assert.IsType<DomText>(rebuiltItalic.LastChild).Data);
        Assert.Contains(result.Errors, error => error.Code == $"nested-{tag}-start-tag");
        Assert.Contains(result.Errors, error => error.Code == "misnested-formatting-end-tag");
    }

    [Fact]
    public void NestedAnchorRemovesStaleOriginalBeforeReconstructing()
    {
        var result = Parse("<!doctype html><p><a id=old><i class=kept>x</p><a id=new>y</a>z");
        var body = result.Document.Body!;
        var rebuiltItalic = Assert.IsType<DomElement>(body.ChildNodes[1]);
        Assert.Equal("i", rebuiltItalic.LocalName);
        Assert.Equal("kept", rebuiltItalic.GetAttribute("class"));
        var anchor = Assert.IsType<DomElement>(rebuiltItalic.FirstChild);
        Assert.Equal("new", anchor.GetAttribute("id"));
        Assert.Equal("y", anchor.TextContent);
        Assert.Equal("z", Assert.IsType<DomText>(rebuiltItalic.LastChild).Data);
        Assert.Contains(result.Errors, error => error.Code == "nested-a-start-tag");
        Assert.Contains(result.Errors, error => error.Code == "formatting-element-not-open");
    }

    [Fact]
    public void NestedAnchorRemovesAdoptionCloneBeforeInsertingNewAnchor()
    {
        var result = Parse("<!doctype html><a id=old>before<div>inside<a id=new>after</a>more</div>");
        var body = result.Document.Body!;
        var original = Assert.IsType<DomElement>(body.FirstChild);
        var div = Assert.IsType<DomElement>(body.LastChild);
        var adopted = Assert.IsType<DomElement>(div.ChildNodes[0]);
        var replacement = Assert.IsType<DomElement>(div.ChildNodes[1]);

        Assert.Equal("old", original.GetAttribute("id"));
        Assert.Equal("before", original.TextContent);
        Assert.Equal("old", adopted.GetAttribute("id"));
        Assert.Equal("inside", adopted.TextContent);
        Assert.Equal("new", replacement.GetAttribute("id"));
        Assert.Equal("after", replacement.TextContent);
        Assert.Equal("more", Assert.IsType<DomText>(div.LastChild).Data);
        Assert.Contains(result.Errors, error => error.Code == "nested-a-start-tag");
    }

    [Fact]
    public void NestedNobrReconstructsBeforeCheckingScopeAndAgainAfterAdoption()
    {
        var result = Parse("<!doctype html><p><nobr id=old><i class=kept>x</p><nobr id=new>y</nobr>z");
        var rebuiltOld = Assert.IsType<DomElement>(result.Document.Body!.ChildNodes[1]);
        var rebuiltOldItalic = Assert.IsType<DomElement>(rebuiltOld.FirstChild);
        var rebuiltItalic = Assert.IsType<DomElement>(result.Document.Body.ChildNodes[2]);
        Assert.Equal("old", rebuiltOld.GetAttribute("id"));
        Assert.Empty(rebuiltOldItalic.ChildNodes);
        Assert.Equal("kept", rebuiltItalic.GetAttribute("class"));
        Assert.NotSame(rebuiltOldItalic, rebuiltItalic);
        Assert.Equal("new", Assert.IsType<DomElement>(rebuiltItalic.FirstChild).GetAttribute("id"));
        Assert.Equal("yz", rebuiltItalic.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "nested-nobr-start-tag");
        Assert.Contains(result.Errors, error => error.Code == "misnested-formatting-end-tag");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("nobr")]
    public void NestedFormattingStartsUseFurthestBlockAdoption(string tag)
    {
        var result = Parse($"<!doctype html><{tag} id=old>before<div>inside<{tag} id=new>after</{tag}></div>");
        var body = result.Document.Body!;
        var original = Assert.IsType<DomElement>(body.FirstChild);
        var div = Assert.IsType<DomElement>(body.LastChild);
        var recreated = Assert.IsType<DomElement>(div.FirstChild);
        var replacement = Assert.IsType<DomElement>(div.LastChild);
        Assert.Equal("before", original.TextContent);
        Assert.Equal("old", recreated.GetAttribute("id"));
        Assert.Equal("inside", recreated.TextContent);
        Assert.Equal("new", replacement.GetAttribute("id"));
        Assert.Equal("after", replacement.TextContent);
        Assert.Contains(result.Errors, error => error.Code == $"nested-{tag}-start-tag");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("nobr")]
    public void NestedFormattingRecoveryHonorsLimitsAndCancellation(string tag)
    {
        var input = $"<!doctype html><{tag}><i>x<{tag}>y";
        Assert.Throws<HtmlLimitException>(() => Parse(input, new() { MaxErrors = 1 }));
        Assert.Throws<HtmlLimitException>(() => Parse(input, new() { MaxNodes = 10 }));
        Assert.Throws<HtmlLimitException>(() => Parse(input, new() { MaxDepth = 3 }));
        Assert.Equal("xy", Parse(input, new() { MaxNodes = 11, MaxDepth = 4 }).Document.Body!.TextContent);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => HtmlParser.Parse(input, cancellationToken: cancelled.Token));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("nobr")]
    public void NestedFormattingFurthestBlockCloneCountsTowardNodeLimit(string tag)
    {
        var input = $"<!doctype html><{tag} id=old><div>x<{tag} id=new>";
        Assert.Throws<HtmlLimitException>(() => Parse(input, new() { MaxNodes = 8 }));
        Assert.Throws<HtmlLimitException>(() => Parse(input, new() { MaxNodes = 9 }));
        var body = Parse(input, new() { MaxNodes = 10 }).Document.Body!;
        var div = Assert.IsType<DomElement>(body.LastChild);
        Assert.Equal("old", Assert.IsType<DomElement>(div.FirstChild).GetAttribute("id"));
        Assert.Equal("new", Assert.IsType<DomElement>(div.LastChild).GetAttribute("id"));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("nobr")]
    public void FormattingMarkersKeepOuterEntriesSeparateFromNestedStarts(string tag)
    {
        var result = Parse($"<!doctype html><{tag} id=outer><template><{tag} id=inner>x<{tag} id=second>z</{tag}></template>y</{tag}>");
        var outer = Assert.IsType<DomElement>(result.Document.Body!.FirstChild);
        var template = Assert.IsType<DomTemplateElement>(outer.FirstChild);
        Assert.Equal("inner", Assert.IsType<DomElement>(template.Content.FirstChild).GetAttribute("id"));
        Assert.Equal("second", Assert.IsType<DomElement>(template.Content.LastChild).GetAttribute("id"));
        Assert.Equal("y", Assert.IsType<DomText>(outer.LastChild).Data);
        Assert.Single(result.Errors, error => error.Code == $"nested-{tag}-start-tag");
    }

    [Fact]
    public void NestedAnchorInTableCaptionHonorsFormattingMarker()
    {
        var result = Parse("<!doctype html><a id=outer><table><caption><a id=inner>x");
        var outer = Assert.IsType<DomElement>(result.Document.Body!.FirstChild);
        var table = Assert.IsType<DomElement>(outer.FirstChild);
        var caption = Assert.IsType<DomElement>(table.FirstChild);
        var inner = Assert.IsType<DomElement>(caption.FirstChild);

        Assert.Equal("outer", outer.GetAttribute("id"));
        Assert.Equal("table", table.LocalName);
        Assert.Equal("caption", caption.LocalName);
        Assert.Equal("inner", inner.GetAttribute("id"));
        Assert.Equal("x", inner.TextContent);
        Assert.DoesNotContain(result.Errors, error => error.Code == "nested-a-start-tag");
    }

    [Fact]
    public void NestedNobrInTableCaptionDoesNotReportNesting()
    {
        var result = Parse("<!doctype html><nobr id=outer><table><caption><nobr id=inner>x");
        var outer = Assert.IsType<DomElement>(result.Document.Body!.FirstChild);
        var table = Assert.IsType<DomElement>(outer.FirstChild);
        var caption = Assert.IsType<DomElement>(table.FirstChild);
        var inner = Assert.IsType<DomElement>(caption.FirstChild);

        Assert.Equal("outer", outer.GetAttribute("id"));
        Assert.Equal("table", table.LocalName);
        Assert.Equal("caption", caption.LocalName);
        Assert.Equal("inner", inner.GetAttribute("id"));
        Assert.DoesNotContain(result.Errors, error => error.Code == "nested-nobr-start-tag");
    }

    [Fact]
    public void FormattingEndTagReconstructsRemainingDescendantWithTokenAttributes()
    {
        var body = Parse("<!doctype html><b><i id=x class=y>one</b>two</i>three").Document.Body!;
        Assert.Equal(3, body.ChildNodes.Count);
        var bold = Assert.IsType<DomElement>(body.ChildNodes[0]);
        var original = Assert.IsType<DomElement>(Assert.Single(bold.ChildNodes));
        var rebuilt = Assert.IsType<DomElement>(body.ChildNodes[1]);
        Assert.Equal("b", bold.LocalName);
        Assert.Equal("i", original.LocalName);
        Assert.Equal("one", original.TextContent);
        Assert.Equal("i", rebuilt.LocalName);
        Assert.Equal("two", rebuilt.TextContent);
        Assert.Equal("x", rebuilt.GetAttribute("id"));
        Assert.Equal("y", rebuilt.GetAttribute("class"));
        Assert.NotSame(original, rebuilt);
        Assert.Equal("three", Assert.IsType<DomText>(body.ChildNodes[2]).Data);
    }

    [Fact]
    public void FormattingEndTagReparentsFurthestBlockAndRecreatesFormattingElement()
    {
        var result = Parse("<!doctype html><b>one<p>two</b>three</p>");
        var body = result.Document.Body!;
        Assert.Equal(2, body.ChildNodes.Count);

        var originalBold = Assert.IsType<DomElement>(body.ChildNodes[0]);
        var paragraph = Assert.IsType<DomElement>(body.ChildNodes[1]);
        Assert.Equal("b", originalBold.LocalName);
        Assert.Equal("one", originalBold.TextContent);
        var recreatedBold = Assert.IsType<DomElement>(paragraph.ChildNodes[0]);
        Assert.Equal("b", recreatedBold.LocalName);
        Assert.Single(recreatedBold.ChildNodes);
        Assert.Equal("two", recreatedBold.TextContent);
        Assert.Equal("three", Assert.IsType<DomText>(paragraph.ChildNodes[1]).Data);
        Assert.Contains(result.Errors, error => error.Code == "misnested-formatting-end-tag");
    }

    [Fact]
    public void FormattingEndTagReparentsDivFurthestBlock()
    {
        var body = Parse("<b>before<div>inside</b>after</div>").Document.Body!;
        var originalBold = Assert.IsType<DomElement>(body.ChildNodes[0]);
        var div = Assert.IsType<DomElement>(body.ChildNodes[1]);
        var recreatedBold = Assert.IsType<DomElement>(div.FirstChild);
        Assert.Equal("before", originalBold.TextContent);
        Assert.Equal("div", div.LocalName);
        Assert.Equal("b", recreatedBold.LocalName);
        Assert.Equal("inside", recreatedBold.TextContent);
        Assert.Equal("after", Assert.IsType<DomText>(div.LastChild).Data);
    }

    [Fact]
    public void FormattingEndTagRemovesNonFormattingNodesFromStackDuringReparenting()
    {
        var body = Parse("<b><span><p>inside</b>after</p>").Document.Body!;
        var originalBold = Assert.IsType<DomElement>(body.ChildNodes[0]);
        var originalSpan = Assert.IsType<DomElement>(originalBold.FirstChild);
        var paragraph = Assert.IsType<DomElement>(body.ChildNodes[1]);
        var recreatedBold = Assert.IsType<DomElement>(paragraph.FirstChild);

        Assert.Equal("span", originalSpan.LocalName);
        Assert.Empty(originalSpan.ChildNodes);
        Assert.Equal("p", paragraph.LocalName);
        Assert.Equal("b", recreatedBold.LocalName);
        Assert.Equal("inside", recreatedBold.TextContent);
        Assert.Equal("after", Assert.IsType<DomText>(paragraph.LastChild).Data);
    }

    [Fact]
    public void FormattingEndTagBoundsInnerCloningAndMaintainsStackOrder()
    {
        var body = Parse("<b><i><u><em><strong><p>x</b></p>y").Document.Body!;
        var paragraph = Assert.IsType<DomElement>(Descendants(body.ChildNodes[1])
            .Single(element => element.LocalName == "p"));
        var recreatedBold = Assert.IsType<DomElement>(paragraph.FirstChild);
        Assert.Equal("b", recreatedBold.LocalName);
        Assert.Equal("x", recreatedBold.TextContent);

        var clonedUnderline = Assert.IsType<DomElement>(body.ChildNodes[1]);
        var clonedEmphasis = Assert.IsType<DomElement>(clonedUnderline.FirstChild);
        var clonedStrong = Assert.IsType<DomElement>(clonedEmphasis.FirstChild);
        Assert.Equal("u", clonedUnderline.LocalName);
        Assert.Equal("em", clonedEmphasis.LocalName);
        Assert.Equal("strong", clonedStrong.LocalName);
        Assert.Same(paragraph, clonedStrong.FirstChild);
        Assert.Equal("y", Assert.IsType<DomText>(clonedStrong.LastChild).Data);
        Assert.Equal(2, Descendants(body).Append(body).Count(element => element.LocalName == "b"));
    }

    [Fact]
    public void FormattingEndTagCountsRecreatedElementsAgainstNodeLimit()
    {
        const string input = "<b><p>x</b>";
        Assert.Throws<HtmlLimitException>(() => HtmlParser.Parse(input, new() { MaxNodes = 7 },
            TestContext.Current.CancellationToken));
        var result = HtmlParser.Parse(input, new() { MaxNodes = 8 }, TestContext.Current.CancellationToken);
        Assert.Equal("x", result.Document.Body!.TextContent);
        Assert.Throws<HtmlLimitException>(() => HtmlParser.Parse(input, new() { MaxDepth = 3 },
            TestContext.Current.CancellationToken));
        Assert.Equal("x", HtmlParser.Parse(input, new() { MaxDepth = 4 },
            TestContext.Current.CancellationToken).Document.Body!.TextContent);
    }

    [Fact]
    public void FormattingEndTagUsesAdjustedTemplateInsertionParent()
    {
        var document = Parse("<template><b><p>x</b>y</p></template>").Document;
        var template = Assert.IsType<DomTemplateElement>(Assert.Single(Descendants(document),
            element => element is DomTemplateElement));
        Assert.Empty(template.ChildNodes);
        var originalBold = Assert.IsType<DomElement>(template.Content.FirstChild);
        var paragraph = Assert.IsType<DomElement>(template.Content.LastChild);
        Assert.Equal("b", originalBold.LocalName);
        Assert.Equal("p", paragraph.LocalName);
        Assert.Equal("b", Assert.IsType<DomElement>(paragraph.FirstChild).LocalName);
        Assert.Equal("y", Assert.IsType<DomText>(paragraph.LastChild).Data);
    }

    [Fact]
    public void ReconstructionUsesOriginalTokenRatherThanMutatedElementAttributes()
    {
        var result = HtmlParser.Parse("<!doctype html><b><i id=original><script></script>one</b>two", null,
            TestContext.Current.CancellationToken, new DomDocument(),
            script => Assert.IsType<DomElement>(script.ParentNode).SetAttribute("id", "changed"));
        var body = result.Document.Body!;
        var original = Assert.IsType<DomElement>(Assert.IsType<DomElement>(body.ChildNodes[0]).FirstChild);
        var rebuilt = Assert.IsType<DomElement>(body.ChildNodes[1]);
        Assert.Equal("changed", original.GetAttribute("id"));
        Assert.Equal("original", rebuilt.GetAttribute("id"));
    }

    [Fact]
    public void NoahsArkComparesAttributeSetsRegardlessOfOrder()
    {
        var body = Parse("<!doctype html><p><b a=1 b=2><b b=2 a=1><b a=1 b=2><b b=2 a=1>x</p>y").Document.Body!;
        var node = body.ChildNodes[1];
        for (var index = 0; index < 3; index++)
        {
            var bold = Assert.IsType<DomElement>(node);
            Assert.Equal("b", bold.LocalName);
            Assert.Equal("1", bold.GetAttribute("a"));
            Assert.Equal("2", bold.GetAttribute("b"));
            node = Assert.Single(bold.ChildNodes);
        }

        Assert.Equal("y", Assert.IsType<DomText>(node).Data);
    }

    [Fact]
    public void CellAndCaptionMarkersPreventFormattingLeakage()
    {
        var body = Parse("<!doctype html><table><caption><i>caption</caption><tr><td><b>one</td><td>two</td></tr></table>after").Document.Body!;
        var table = Assert.IsType<DomElement>(body.ChildNodes[0]);
        var caption = Assert.IsType<DomElement>(table.ChildNodes[0]);
        Assert.Equal("i", Assert.IsType<DomElement>(caption.FirstChild).LocalName);
        var section = Assert.IsType<DomElement>(table.ChildNodes[1]);
        var row = Assert.IsType<DomElement>(section.FirstChild);
        var first = Assert.IsType<DomElement>(row.ChildNodes[0]);
        var second = Assert.IsType<DomElement>(row.ChildNodes[1]);
        Assert.Equal("b", Assert.IsType<DomElement>(first.FirstChild).LocalName);
        Assert.Equal("two", Assert.IsType<DomText>(Assert.Single(second.ChildNodes)).Data);
        Assert.Equal("after", Assert.IsType<DomText>(body.ChildNodes[1]).Data);
    }

    [Fact]
    public void TemplateMarkerSeparatesFormattingAndRestoresOuterEntries()
    {
        var body = Parse("<!doctype html><p><b>outer</p><template><i>inner</template>after").Document.Body!;
        var template = Assert.IsType<DomTemplateElement>(body.ChildNodes[1]);
        var inner = Assert.IsType<DomElement>(Assert.Single(template.Content.ChildNodes));
        Assert.Equal("i", inner.LocalName);
        Assert.Equal("inner", inner.TextContent);
        var rebuilt = Assert.IsType<DomElement>(body.ChildNodes[2]);
        Assert.Equal("b", rebuilt.LocalName);
        Assert.Equal("after", rebuilt.TextContent);
    }

    [Fact]
    public void FormattingReconstructsOnInlineStart()
    {
        var body = Parse("<!doctype html><p><b>x</p><span>y</span>").Document.Body!;
        var rebuilt = Assert.IsType<DomElement>(body.ChildNodes[1]);
        Assert.Equal("b", rebuilt.LocalName);
        Assert.Equal("span", Assert.IsType<DomElement>(rebuilt.FirstChild).LocalName);
        Assert.Equal("y", rebuilt.TextContent);
    }

    [Fact]
    public void FormattingReconstructsForWhitespaceAfterBody()
    {
        var body = Parse("<!doctype html><p><b>x</p></body> ").Document.Body!;
        var rebuilt = Assert.IsType<DomElement>(body.ChildNodes[1]);
        Assert.Equal("b", rebuilt.LocalName);
        Assert.Equal(" ", rebuilt.TextContent);
    }

    [Fact]
    public void FormattingEndTagRemovesStaleEntryBeforeReconstruction()
    {
        var result = Parse("<!doctype html><p><b>x</p></b>plain");
        Assert.Equal("plain", Assert.IsType<DomText>(result.Document.Body!.ChildNodes[1]).Data);
        Assert.Contains(result.Errors, error => error.Code == "formatting-element-not-open");
    }

    [Fact]
    public void FormattingRecoveryStillEnforcesErrorLimit()
    {
        Assert.Throws<HtmlLimitException>(() =>
            Parse("<!doctype html><b><i>x</b></i>", new() { MaxErrors = 1 }));
    }

    [Fact]
    public void FormattingEndTagRespectsTableScope()
    {
        var result = Parse("<!doctype html><b><table><tr><td></b>x</td></tr></table>y");
        var bold = Assert.IsType<DomElement>(Assert.Single(result.Document.Body!.ChildNodes));
        Assert.Equal("xy", bold.TextContent);
        Assert.Contains(result.Errors, error => error.Code == "unexpected-end-tag");
    }

    [Fact]
    public void SimpleFormsUseFormPointerAndKeepControlsVoid()
    {
        var result = Parse("<!doctype html><p>a<form action=/s><p>b<input name=q>text<button>go</button></form>after");
        var body = result.Document.Body!;
        Assert.Equal(new[] { "p", "form" }, body.ChildNodes.OfType<DomElement>().Select(e => e.LocalName));
        var form = body.ChildNodes.OfType<DomElement>().Single(e => e.LocalName == "form");
        var paragraph = Assert.IsType<DomElement>(form.FirstChild);
        Assert.Equal("p", paragraph.LocalName);
        var input = paragraph.ChildNodes.OfType<DomElement>().Single(e => e.LocalName == "input");
        Assert.Empty(input.ChildNodes);
        Assert.IsType<DomText>(input.NextSibling);
        Assert.Equal("after", Assert.IsType<DomText>(body.LastChild).Data);
        Assert.DoesNotContain(result.Errors, e => e.Code == "implicitly-closed-descendants");
    }

    [Theory]
    [InlineData("<!doctype html><form id=a><form id=b><input></form>x", "nested-form")]
    [InlineData("<!doctype html><p></form>x", "unexpected-form-end-tag")]
    [InlineData("<!doctype html><form></form></form>x", "unexpected-form-end-tag")]
    public void FormParseErrorsAreReportedAndIgnoredTagsDoNotNest(string input, string code)
    {
        var result = Parse(input);
        Assert.Contains(result.Errors, e => e.Code == code);
        var forms = Descendants(result.Document).Where(e => e.LocalName == "form").ToList();
        Assert.True(forms.Count <= 1);
        Assert.All(forms, form => Assert.DoesNotContain(Descendants(form), e => e.LocalName == "form"));
        Assert.EndsWith("x", result.Document.Body!.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void FormPointerClosesAtEndTagAndAllowsLaterForm()
    {
        var result = Parse("<!doctype html><form id=a></form><form id=b><input></form>");
        Assert.Equal(new[] { "a", "b" }, Descendants(result.Document).Where(e => e.LocalName == "form").Select(e => e.GetAttribute("id")));
        Assert.Empty(result.Errors);
    }

    private static IEnumerable<DomElement> Descendants(DomNode node)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is DomElement element)
            {
                yield return element;
                foreach (var nested in Descendants(element)) { yield return nested; }
            }
        }
    }

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
            HtmlParser.Parse("<b><i><u><em><strong><p>x</b>", cancellationToken: cancelled.Token));
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
