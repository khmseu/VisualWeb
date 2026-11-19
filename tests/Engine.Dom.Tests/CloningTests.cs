using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class CloningTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void ShallowAndDeepElementClonesCopyOrderedAttributesAndDescendantsWithoutReparenting()
    {
        var document = new DomDocument(); var source = document.CreateElement("MiXeD");
        source.SetAttribute("first", "one"); source.SetAttribute("second", "two");
        var child = document.CreateTextNode("text"); source.AppendChild(child);
        var shallow = Assert.IsType<DomElement>(source.CloneNode(cancellationToken: Cancellation));
        Assert.Equal("mixed", shallow.LocalName); Assert.Empty(shallow.ChildNodes);
        Assert.Equal(new[] { "first", "second" }, shallow.Attributes.Keys);
        Assert.Equal("one", shallow.GetAttribute("first")); Assert.Same(document, shallow.OwnerDocument);
        var clone = Assert.IsType<DomElement>(source.CloneNode(deep: true, cancellationToken: Cancellation));
        Assert.NotSame(source, clone); Assert.NotSame(child, clone.FirstChild);
        Assert.Equal("text", clone.TextContent); Assert.Same(document, clone.FirstChild!.OwnerDocument);
        Assert.Same(source, child.ParentNode); Assert.Null(clone.ParentNode);
        Assert.Equal(source.Attributes, clone.Attributes);
    }

    [Fact]
    public void EverySupportedInterfaceAndDocumentClonePreservePayloadButNotIdentityOrParent()
    {
        var document = new DomDocument { Mode = DomDocumentMode.Quirks };
        var source = document.CreateElement("html"); document.AppendChild(source);
        source.AppendChild(document.CreateComment("raw\0\ud800"));
        source.AppendChild(document.CreateProcessingInstruction("Probe", "data"));
        source.AppendChild(document.CreateDocumentFragment()); source.AppendChild(document.CreateTextNode("tail"));
        var fragment = document.CreateDocumentFragment(); fragment.AppendChild(document.CreateComment("child"));
        var fragmentClone = Assert.IsType<DomDocumentFragment>(fragment.CloneNode(deep: true, cancellationToken: Cancellation));
        Assert.NotSame(fragment.FirstChild, fragmentClone.FirstChild);
        Assert.Equal("child", Assert.IsType<DomComment>(fragmentClone.FirstChild).Data);
        Assert.Equal("Probe", Assert.IsType<DomProcessingInstruction>(source.ChildNodes[1]).CloneNode(cancellationToken: Cancellation).NodeName);
        Assert.Equal("raw\0\ud800", Assert.IsType<DomComment>(source.FirstChild).CloneNode(cancellationToken: Cancellation).TextContent);
        var documentClone = Assert.IsType<DomDocument>(document.CloneNode(deep: true, cancellationToken: Cancellation));
        Assert.Equal(DomDocumentMode.Quirks, documentClone.Mode);
        Assert.Equal(document.ChildNodes.Count, documentClone.ChildNodes.Count);
        Assert.NotSame(document.DocumentElement, documentClone.DocumentElement);
        Assert.Same(documentClone, documentClone.DocumentElement!.OwnerDocument);
    }

    [Fact]
    public void DoctypeFieldsAreCopiedAndCancellationIsObservedBeforeReturning()
    {
        var document = new DomDocument(); var doctype = document.CreateDocumentType("html", "public", "system");
        var clone = Assert.IsType<DomDocumentType>(doctype.CloneNode(cancellationToken: Cancellation));
        Assert.Equal("html", clone.Name); Assert.Equal("public", clone.PublicId); Assert.Equal("system", clone.SystemId);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => doctype.CloneNode(cancellationToken: canceled.Token));
    }

    [Fact]
    public void ShallowClone_template_has_empty_distinct_content()
    {
        var document = new DomDocument();
        var source = Assert.IsType<DomTemplateElement>(document.CreateElement("template"));
        source.Content.AppendChild(document.CreateTextNode("source"));

        var clone = Assert.IsType<DomTemplateElement>(source.CloneNode(cancellationToken: Cancellation));

        Assert.NotSame(source, clone);
        Assert.NotSame(source.Content, clone.Content);
        Assert.Empty(clone.ChildNodes);
        Assert.Empty(clone.Content.ChildNodes);
        Assert.Same(source.OwnerDocument, clone.OwnerDocument);
        Assert.Same(source.Content.OwnerDocument, clone.Content.OwnerDocument);
    }

    [Fact]
    public void DeepClone_template_copies_content_without_element_children()
    {
        var document = new DomDocument();
        var source = Assert.IsType<DomTemplateElement>(document.CreateElement("template"));
        var child = document.CreateElement("div");
        child.AppendChild(document.CreateTextNode("source"));
        source.Content.AppendChild(child);
        var nested = Assert.IsType<DomTemplateElement>(source.Content.OwnerDocument!.CreateElement("template"));
        Assert.Same(source.Content.OwnerDocument, nested.Content.OwnerDocument);
        nested.Content.AppendChild(nested.Content.OwnerDocument!.CreateTextNode("nested"));
        source.Content.AppendChild(nested);

        var clone = Assert.IsType<DomTemplateElement>(source.CloneNode(deep: true, cancellationToken: Cancellation));
        var clonedChild = Assert.IsType<DomElement>(clone.Content.ChildNodes[0]);
        var nestedClone = Assert.IsType<DomTemplateElement>(clone.Content.ChildNodes[1]);

        Assert.Empty(source.ChildNodes);
        Assert.Empty(clone.ChildNodes);
        Assert.DoesNotContain(child, source.Descendants());
        Assert.NotSame(source.Content, clone.Content);
        Assert.NotSame(child, clonedChild);
        Assert.Equal("source", clonedChild.TextContent);
        Assert.Same(clone.Content.OwnerDocument, clonedChild.OwnerDocument);
        Assert.NotSame(nested, nestedClone);
        Assert.NotSame(nested.Content, nestedClone.Content);
        Assert.Equal("nested", nestedClone.Content.TextContent);
        Assert.Same(clone.Content.OwnerDocument, nestedClone.OwnerDocument);
        Assert.Same(clone.Content.OwnerDocument, nestedClone.Content.OwnerDocument);
        Assert.Same(nestedClone.Content.OwnerDocument, nestedClone.Content.FirstChild!.OwnerDocument);
        Assert.Same(source.Content.FirstChild, child);
    }

    [Fact]
    public void DeepClone_template_preflights_content_node_limits()
    {
        var document = new DomDocument();
        var source = Assert.IsType<DomTemplateElement>(document.CreateElement("template"));
        source.Content.AppendChild(document.CreateTextNode("x"));
        var checkedTexts = new List<string>();

        Assert.Throws<InvalidOperationException>(() => source.CloneNode(
            deep: true, maxNodes: 2, maxTextCharacters: 10, maxAttributes: 0,
            text => { checkedTexts.Add(text); }, Cancellation));

        Assert.Equal(new[] { "template" }, checkedTexts);
        Assert.Single(source.Content.ChildNodes);
    }

    [Fact]
    public void DeepClone_template_cancels_before_preflighting_pending_content()
    {
        var document = new DomDocument();
        var parent = document.CreateElement("div");
        var source = Assert.IsType<DomTemplateElement>(document.CreateElement("template"));
        parent.AppendChild(source);
        var content = source.Content;
        var child = document.CreateTextNode("source");
        content.AppendChild(child);
        var checkedTexts = new List<string>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);

        Assert.ThrowsAny<OperationCanceledException>(() => source.CloneNode(
            deep: true, maxNodes: 3, maxTextCharacters: 10, maxAttributes: 0,
            text => { checkedTexts.Add(text); cancellation.Cancel(); }, cancellation.Token));

        Assert.Equal(new[] { "template" }, checkedTexts);
        Assert.Same(source, Assert.Single(parent.ChildNodes));
        Assert.Same(parent, source.ParentNode);
        Assert.Empty(source.ChildNodes);
        Assert.Same(content, source.Content);
        Assert.Same(child, Assert.Single(content.ChildNodes));
        Assert.Same(content, child.ParentNode);
        Assert.Equal("source", child.Data);
    }
}
