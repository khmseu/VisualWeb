using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class DomTests
{
    [Fact]
    public void DocumentInvariantsAreCheckedBeforeMutation()
    {
        var document = new DomDocument();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(document.CreateElement("a"));
        fragment.AppendChild(document.CreateElement("b"));
        Assert.Equal(DomError.HierarchyRequest, Assert.Throws<DomException>(() => document.AppendChild(fragment)).Error);
        Assert.Equal(2, fragment.ChildNodes.Count);
        Assert.Same(root, document.DocumentElement);
        Assert.Throws<DomException>(() => document.AppendChild(document.CreateTextNode("")));
        Assert.Throws<DomException>(() => document.AppendChild(document.CreateDocumentType("html")));
        Assert.Throws<DomException>(() => root.AppendChild(document.CreateDocumentType("html")));
        document.InsertBefore(document.CreateDocumentType("html"), root);
        Assert.NotNull(document.Doctype);
    }

    [Fact]
    public void InsertMovesNodesAndUpdatesSiblingLinks()
    {
        var document = new DomDocument();
        var parent = document.CreateElement("div");
        var first = document.CreateElement("a");
        var second = document.CreateElement("b");
        var third = document.CreateElement("c");
        parent.AppendChild(first);
        parent.AppendChild(second);
        parent.AppendChild(third);
        parent.InsertBefore(third, first);
        Assert.Equal(new[] { third, first, second }, parent.ChildNodes);
        parent.InsertBefore(first, first);
        Assert.Same(third, first.PreviousSibling);
        Assert.Same(second, first.NextSibling);
        parent.AppendChild(third);
        Assert.Equal(new[] { first, second, third }, parent.ChildNodes);
        Assert.Same(parent, first.ParentNode);
    }

    [Fact]
    public void CyclesAndWrongReferencesFailWithoutDetachingNodes()
    {
        var document = new DomDocument();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        parent.AppendChild(child);
        Assert.Throws<DomException>(() => child.AppendChild(parent));
        Assert.Throws<DomException>(() => parent.InsertBefore(child, document.CreateElement("other")));
        Assert.Throws<DomException>(() => child.AppendChild(document));
        Assert.Same(parent, child.ParentNode);
        Assert.Null(parent.ParentNode);
    }

    [Fact]
    public void FragmentInsertionAdoptsDescendantsAndEmptiesFragment()
    {
        var source = new DomDocument();
        var target = new DomDocument();
        var fragment = source.CreateDocumentFragment();
        var element = source.CreateElement("div");
        var text = source.CreateTextNode("hi");
        element.AppendChild(text);
        fragment.AppendChild(element);
        target.AppendChild(fragment);
        Assert.Empty(fragment.ChildNodes);
        Assert.Same(target, fragment.OwnerDocument);
        Assert.Same(target, text.OwnerDocument);
        Assert.True(text.IsConnected);
        Assert.Same(element, target.DocumentElement);
    }

    [Fact]
    public void ReplaceAndRemovePreserveOwnershipAndValidateFirst()
    {
        var document = new DomDocument();
        var first = document.CreateElement("a");
        var second = document.CreateElement("b");
        document.AppendChild(first);
        Assert.Throws<DomException>(() => document.ReplaceChild(document.CreateTextNode("bad"), first));
        Assert.Same(first, document.DocumentElement);
        Assert.Same(first, document.ReplaceChild(second, first));
        Assert.Null(first.ParentNode);
        Assert.Same(document, first.OwnerDocument);
        Assert.Same(second, document.ReplaceChild(second, second));
        document.RemoveChild(second);
        Assert.False(second.IsConnected);
        Assert.Throws<DomException>(() => document.RemoveChild(second));
    }

    [Fact]
    public void AttributesFoldOnlyAsciiAndTextContentExcludesComments()
    {
        var document = new DomDocument();
        var element = document.CreateElement("DIV");
        document.AppendChild(element);
        element.SetAttribute("ID", "x");
        element.SetAttribute("\u00C4", "unicode");
        Assert.Equal("div", element.LocalName);
        Assert.Equal("\u00E4TEST", document.CreateElement("\u00E4test").NodeName);
        Assert.Equal("x", element.GetAttribute("id"));
        Assert.Null(element.GetAttribute("\u00E4"));
        Assert.Same(element, document.GetElementById("x"));
        element.AppendChild(document.CreateTextNode("a"));
        element.AppendChild(document.CreateComment("not text"));
        var nested = document.CreateElement("span");
        nested.AppendChild(document.CreateTextNode("b"));
        element.AppendChild(nested);
        Assert.Equal("ab", element.TextContent);
        element.TextContent = "replacement";
        Assert.Single(element.ChildNodes);
        Assert.Null(nested.ParentNode);
        Assert.Equal("replacement", element.TextContent);
        document.TextContent = "ignored";
        Assert.Null(document.TextContent);
        element.RemoveAttribute("ID");
        Assert.Null(document.GetElementById("x"));
    }

    [Fact]
    public void AdoptDetachesWholeTreeWithoutConnectingIt()
    {
        var old = new DomDocument();
        var next = new DomDocument();
        var root = old.CreateElement("html");
        old.AppendChild(root);
        var child = old.CreateElement("body");
        root.AppendChild(child);
        next.AdoptNode(root);
        Assert.Empty(old.ChildNodes);
        Assert.Same(next, child.OwnerDocument);
        Assert.False(child.IsConnected);
        Assert.Throws<DomException>(() => next.AdoptNode(old));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad name")]
    [InlineData("a/b")]
    [InlineData("a>")]
    [InlineData("a\0")]
    [InlineData("1name")]
    [InlineData("_!")]
    public void InvalidNamesFailExplicitly(string name)
    {
        var document = new DomDocument();
        Assert.Equal(DomError.InvalidCharacter, Assert.Throws<DomException>(() => document.CreateElement(name)).Error);
    }

    [Fact]
    public void AttributeDoctypeAndProcessingInstructionNamesHaveDistinctRules()
    {
        var document = new DomDocument();
        var element = document.CreateElement("a=valid");
        Assert.Throws<DomException>(() => element.SetAttribute("a=invalid", "x"));
        element.SetAttribute("1", "valid");
        Assert.Equal("valid", element.GetAttribute("1"));
        Assert.Equal("", document.CreateDocumentType("").Name);
        Assert.Throws<DomException>(() => document.CreateDocumentType("bad name"));
        Assert.Equal("a:b", document.CreateProcessingInstruction("a:b", "data").Target);
        Assert.Throws<DomException>(() => document.CreateProcessingInstruction("1bad", ""));
        Assert.Throws<DomException>(() => document.CreateProcessingInstruction("ok", "?>"));
    }

    [Fact]
    public void ReplacingWithFollowingSiblingAndDocumentFragmentUsesCorrectReference()
    {
        var document = new DomDocument();
        var parent = document.CreateElement("div");
        var first = document.CreateElement("a");
        var second = document.CreateElement("b");
        var third = document.CreateElement("c");
        parent.AppendChild(first);
        parent.AppendChild(second);
        parent.AppendChild(third);
        parent.ReplaceChild(second, first);
        Assert.Equal(new[] { second, third }, parent.ChildNodes);
        var fragment = document.CreateDocumentFragment();
        var replacement = document.CreateElement("r");
        fragment.AppendChild(replacement);
        parent.ReplaceChild(fragment, third);
        Assert.Equal(new[] { second, replacement }, parent.ChildNodes);
        Assert.Empty(fragment.ChildNodes);
        Assert.Null(third.ParentNode);
    }

    [Fact]
    public void InvalidDoctypeReorderingPreservesExistingTree()
    {
        var document = new DomDocument();
        var doctype = document.CreateDocumentType("html");
        var root = document.CreateElement("html");
        document.AppendChild(doctype);
        document.AppendChild(root);
        Assert.Throws<DomException>(() => document.AppendChild(doctype));
        Assert.Throws<DomException>(() => document.InsertBefore(root, doctype));
        Assert.Equal(new DomNode[] { doctype, root }, document.ChildNodes);
    }
}
