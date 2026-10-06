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
}
