using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class EqualityTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void NullIdentityOwnershipParentAndAttributeInsertionOrderHaveDistinctSemantics()
    {
        var a = new DomDocument().CreateElement("div"); var b = new DomDocument().CreateElement("DIV");
        a.SetAttribute("id", "same"); a.SetAttribute("class", "raw");
        b.SetAttribute("class", "raw"); b.SetAttribute("id", "same");
        a.OwnerDocument!.CreateElement("parent").AppendChild(a);
        Assert.True(a.IsEqualNode(b, Cancellation)); Assert.True(a.IsEqualNode(a, Cancellation));
        Assert.False(a.IsEqualNode(null, Cancellation)); Assert.NotSame(a, b);
        b.SetAttribute("class", "Raw"); Assert.False(a.IsEqualNode(b, Cancellation));
        b.SetAttribute("class", "raw"); b.RemoveAttribute("id"); Assert.False(a.IsEqualNode(b, Cancellation));
        Assert.False(a.IsEqualNode(b.OwnerDocument!.CreateElement("span"), Cancellation));
    }

    [Fact]
    public void CharacterInterfacesDataTargetsAndDoctypeFieldsAreComparedExactly()
    {
        var document = new DomDocument();
        var text = document.CreateTextNode("a\0\ud800");
        Assert.True(text.IsEqualNode(document.CreateTextNode("a\0\ud800"), Cancellation));
        Assert.False(text.IsEqualNode(document.CreateTextNode("a\0\ud801"), Cancellation));
        Assert.False(text.IsEqualNode(document.CreateComment("a\0\ud800"), Cancellation));
        Assert.True(document.CreateComment("data").IsEqualNode(document.CreateComment("data"), Cancellation));
        var instruction = document.CreateProcessingInstruction("probe", "data");
        Assert.True(instruction.IsEqualNode(document.CreateProcessingInstruction("probe", "data"), Cancellation));
        Assert.False(instruction.IsEqualNode(document.CreateProcessingInstruction("other", "data"), Cancellation));
        Assert.False(instruction.IsEqualNode(document.CreateProcessingInstruction("probe", "other"), Cancellation));
        var doctype = document.CreateDocumentType("html", "public", "system");
        Assert.True(doctype.IsEqualNode(document.CreateDocumentType("html", "public", "system"), Cancellation));
        foreach (var different in new[] {
            document.CreateDocumentType("other", "public", "system"),
            document.CreateDocumentType("html", "other", "system"),
            document.CreateDocumentType("html", "public", "other") })
        { Assert.False(doctype.IsEqualNode(different, Cancellation)); }
    }

    [Fact]
    public void ChildrenAreOrderedAndTextSegmentationMattersEvenWhenTextContentMatches()
    {
        var document = new DomDocument(); var a = document.CreateDocumentFragment(); var b = document.CreateDocumentFragment();
        a.AppendChild(document.CreateTextNode("ab")); b.AppendChild(document.CreateTextNode("a"));
        b.AppendChild(document.CreateTextNode("b"));
        Assert.Equal(a.TextContent, b.TextContent); Assert.False(a.IsEqualNode(b, Cancellation));
        b.Normalize(Cancellation); Assert.True(a.IsEqualNode(b, Cancellation));
        a.AppendChild(document.CreateComment("barrier")); b.InsertBefore(document.CreateComment("barrier"), b.FirstChild);
        Assert.False(a.IsEqualNode(b, Cancellation));
        Assert.False(a.IsEqualNode(document.CreateElement("div"), Cancellation));
    }

    [Fact]
    public void DocumentModeDoesNotAffectStructuralEqualityAndNestedDifferencesAreDetected()
    {
        var a = new DomDocument(); var b = new DomDocument { Mode = DomDocumentMode.Quirks };
        Assert.True(a.IsEqualNode(b, Cancellation));
        foreach (var document in new[] { a, b })
        {
            document.AppendChild(document.CreateDocumentType("html"));
            var root = document.CreateElement("html"); document.AppendChild(root);
            root.AppendChild(document.CreateElement("body")); root.FirstChild!.AppendChild(document.CreateTextNode("same"));
        }
        Assert.True(a.IsEqualNode(b, Cancellation));
        ((DomText)b.DocumentElement!.FirstChild!.FirstChild!).Data = "changed";
        Assert.False(a.IsEqualNode(b, Cancellation));
    }

    [Fact]
    public void DeepComparisonIsIterativeAndCancellationDoesNotChangeEitherTree()
    {
        var document = new DomDocument(); var a = document.CreateDocumentFragment(); var b = document.CreateDocumentFragment();
        foreach (var root in new[] { a, b })
        {
            DomNode parent = root;
            for (var i = 0; i < 1000; i++)
            { var next = document.CreateElement("i"); parent.AppendChild(next); parent = next; }
            parent.AppendChild(document.CreateTextNode("kept"));
        }
        Assert.True(a.IsEqualNode(b, Cancellation));
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => a.IsEqualNode(b, canceled.Token));
        Assert.Equal(1001, a.Descendants().Count()); Assert.Equal(1001, b.Descendants().Count());
    }
}
