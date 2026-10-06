using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class RemovalTests
{
    [Fact]
    public void RemovalPreservesSubtreeAndOwnershipAndReconnectsAdjacentSiblings()
    {
        var document = new DomDocument(); var parent = document.CreateElement("div");
        var before = document.CreateTextNode("before"); var element = document.CreateElement("i");
        var text = document.CreateTextNode("kept"); element.AppendChild(text);
        var after = document.CreateComment("after");
        parent.AppendChild(before); parent.AppendChild(element); parent.AppendChild(after);
        element.Remove();
        Assert.Equal(new DomNode[] { before, after }, parent.ChildNodes);
        Assert.Same(after, before.NextSibling); Assert.Same(before, after.PreviousSibling);
        Assert.Null(element.ParentNode); Assert.Null(element.NextSibling); Assert.Null(element.PreviousSibling);
        Assert.Same(element, text.ParentNode); Assert.Same(document, text.OwnerDocument);
        Assert.Same(document, element.OwnerDocument); Assert.Equal("kept", element.TextContent);
        element.Remove(); Assert.Equal(2, parent.ChildNodes.Count);
        parent.AppendChild(element); Assert.Same(element, parent.LastChild);
        before.Remove(); Assert.Same(after, parent.FirstChild); Assert.Null(after.PreviousSibling);
        element.Remove(); Assert.Same(after, parent.LastChild); Assert.Null(after.NextSibling);
        after.Remove(); Assert.Empty(parent.ChildNodes);
    }

    [Fact]
    public void CharacterDataAndDoctypesCanRemoveThemselvesButDocumentAndFragmentReject()
    {
        var document = new DomDocument(); var fragment = document.CreateDocumentFragment();
        foreach (var node in new DomNode[] {
            document.CreateTextNode("text"), document.CreateComment("comment"), document.CreateProcessingInstruction("probe", "data") })
        {
            fragment.AppendChild(node); node.Remove(); node.Remove();
            Assert.Empty(fragment.ChildNodes); Assert.Same(document, node.OwnerDocument);
        }
        var doctype = document.CreateDocumentType("html"); document.AppendChild(doctype); doctype.Remove(); doctype.Remove();
        Assert.Empty(document.ChildNodes); Assert.Same(document, doctype.OwnerDocument);
        Assert.Equal(DomError.NotSupported, Assert.Throws<DomException>(() => document.Remove()).Error);
        Assert.Equal(DomError.NotSupported, Assert.Throws<DomException>(() => fragment.Remove()).Error);
    }
}
