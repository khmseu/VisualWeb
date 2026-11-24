using System.Reflection;
using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class ReplaceAllTests
{
    [Fact]
    public void ReplaceAllRemainsAnInternalDomAlgorithm()
    {
        var method = typeof(DomNode).GetMethod(nameof(DomNode.ReplaceAll),
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.True(method.IsAssembly);
        Assert.Null(typeof(DomNode).GetMethod(nameof(DomNode.ReplaceAll),
            BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void ReplaceAllPreflightsBeforeRemovingExistingChildrenAndPreservesRemovedIdentity()
    {
        var document = new DomDocument();
        var originalRoot = document.CreateElement("html");
        document.AppendChild(originalRoot);
        var originalChild = document.CreateElement("body");
        originalRoot.AppendChild(originalChild);

        var invalid = document.CreateDocumentFragment();
        invalid.AppendChild(document.CreateTextNode("invalid document text"));
        Assert.Throws<DomException>(() => document.ReplaceAll(invalid));
        Assert.Same(originalRoot, document.FirstChild);
        Assert.Same(originalChild, originalRoot.FirstChild);
        Assert.Same(document, originalRoot.OwnerDocument);
        Assert.Single(invalid.ChildNodes);

        var sourceDocument = new DomDocument();
        var target = document.CreateElement("section");
        target.AppendChild(originalChild);
        var source = sourceDocument.CreateDocumentFragment();
        var inserted = sourceDocument.CreateElement("p");
        source.AppendChild(inserted);
        target.ReplaceAll(source);
        Assert.Same(inserted, target.FirstChild);
        Assert.Null(originalChild.ParentNode);
        Assert.Same(document, originalChild.OwnerDocument);
        Assert.Same(document, inserted.OwnerDocument);
        Assert.Empty(source.ChildNodes);
    }

    [Fact]
    public void ReplaceAllAdoptsTemplateContentsIntoTheDestinationInertOwnerDocument()
    {
        var destination = new DomDocument();
        var source = new DomDocument();
        var template = Assert.IsType<DomTemplateElement>(destination.CreateElement("template"));
        var fragment = source.CreateDocumentFragment();
        var nested = Assert.IsType<DomTemplateElement>(source.CreateElement("template"));
        nested.Content.AppendChild(source.CreateTextNode("nested"));
        fragment.AppendChild(nested);

        template.Content.ReplaceAll(fragment);

        Assert.Same(template.Content.OwnerDocument, nested.OwnerDocument);
        Assert.Same(template.Content.OwnerDocument, nested.Content.OwnerDocument);
        Assert.Same(nested.Content.OwnerDocument, nested.Content.FirstChild!.OwnerDocument);
        Assert.NotSame(destination, nested.OwnerDocument);
    }
}
