using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class TemplateTests
{
    [Fact]
    public void CreateElement_template_has_separate_stable_content()
    {
        var document = new DomDocument();
        var template = Assert.IsType<DomTemplateElement>(document.CreateElement("TEMPLATE"));

        Assert.Equal("template", template.LocalName);
        Assert.Same(template.Content, template.Content);
        Assert.Empty(template.ChildNodes);
        Assert.Empty(template.Content.ChildNodes);
        Assert.Same(document, template.OwnerDocument);
        Assert.NotSame(document, template.Content.OwnerDocument);
        Assert.Same(template.Content.OwnerDocument,
            Assert.IsType<DomTemplateElement>(document.CreateElement("template")).Content.OwnerDocument);
    }

    [Fact]
    public void Adopt_template_rehomes_content_and_nested_templates()
    {
        var source = new DomDocument();
        var target = new DomDocument();
        var template = Assert.IsType<DomTemplateElement>(source.CreateElement("template"));
        var contentDocument = template.Content.OwnerDocument!;
        var nested = Assert.IsType<DomTemplateElement>(contentDocument.CreateElement("template"));
        var nestedContentDocument = nested.Content.OwnerDocument;
        Assert.Same(contentDocument, nestedContentDocument);
        var text = contentDocument.CreateTextNode("inside");
        nested.Content.AppendChild(text);
        template.Content.AppendChild(nested);
        source.AppendChild(template);

        target.AdoptNode(template);

        Assert.Empty(source.ChildNodes);
        Assert.Same(target, template.OwnerDocument);
        Assert.Same(Assert.IsType<DomTemplateElement>(target.CreateElement("template")).Content.OwnerDocument,
            template.Content.OwnerDocument);
        Assert.NotSame(contentDocument, template.Content.OwnerDocument);
        Assert.Same(template.Content.OwnerDocument, nested.OwnerDocument);
        Assert.NotSame(nestedContentDocument, nested.Content.OwnerDocument);
        Assert.Same(template.Content.OwnerDocument, nested.Content.OwnerDocument);
        Assert.Same(nested.Content.OwnerDocument, text.OwnerDocument);
        Assert.False(text.IsConnected);
    }
}
