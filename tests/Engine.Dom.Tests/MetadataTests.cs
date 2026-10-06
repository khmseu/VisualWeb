using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class MetadataTests
{
    [Fact]
    public void HtmlElementNamesFoldAsciiOnlyAndColonsDoNotCreateNamespacePrefixes()
    {
        var document = new DomDocument(); var element = document.CreateElement("MiXeD:\u00c9\u0131");
        Assert.Equal("mixed:\u00c9\u0131", element.LocalName);
        Assert.Equal("MIXED:\u00c9\u0131", element.NodeName);
        Assert.Equal(DomElement.HtmlNamespace, element.NamespaceUri);
        Assert.Same(document, element.OwnerDocument);
    }

    [Fact]
    public void NamesDistinguishEverySupportedNodeInterfaceAndDocumentHasNoOwner()
    {
        var document = new DomDocument();
        Assert.Equal("#document", document.NodeName); Assert.Null(document.OwnerDocument);
        foreach (var (node, name) in new (DomNode, string)[] {
            (document.CreateTextNode("data"), "#text"), (document.CreateComment("data"), "#comment"),
            (document.CreateProcessingInstruction("Probe", "data"), "Probe"),
            (document.CreateDocumentType("HTML"), "HTML"), (document.CreateDocumentFragment(), "#document-fragment") })
        {
            Assert.Equal(name, node.NodeName); Assert.Same(document, node.OwnerDocument);
        }
    }

    [Fact]
    public void NativeOwnershipTracksAdoptionOfDetachedDescendantsWithoutChangingNames()
    {
        var original = new DomDocument(); var destination = new DomDocument();
        var parent = original.CreateElement("div"); var text = original.CreateTextNode("kept"); parent.AppendChild(text);
        destination.AdoptNode(parent);
        Assert.Same(destination, parent.OwnerDocument); Assert.Same(destination, text.OwnerDocument);
        Assert.Equal("DIV", parent.NodeName); Assert.Equal("#text", text.NodeName); Assert.Null(parent.ParentNode);
    }
}
