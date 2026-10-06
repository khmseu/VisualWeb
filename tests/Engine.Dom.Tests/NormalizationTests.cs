using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class NormalizationTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    [Fact]
    public void NormalizationPreservesFirstNonemptyIdentityAndDetachesOtherTextWithoutChangingTheirData()
    {
        var document = new DomDocument(); var parent = document.CreateElement("div");
        var empty = document.CreateTextNode(""); var first = document.CreateTextNode("a\ud83d");
        var middle = document.CreateTextNode(""); var last = document.CreateTextNode("\ude00b");
        foreach (var node in new[] { empty, first, middle, last }) { parent.AppendChild(node); }
        parent.Normalize(Cancellation);
        Assert.Same(first, Assert.Single(parent.ChildNodes));
        Assert.Equal("a\U0001F600b", first.Data);
        foreach (var removed in new[] { empty, middle, last })
        {
            Assert.Null(removed.ParentNode); Assert.Same(document, removed.OwnerDocument);
            Assert.Null(removed.PreviousSibling); Assert.Null(removed.NextSibling);
        }
        Assert.Equal("\ude00b", last.Data);
        parent.Normalize(Cancellation);
        Assert.Same(first, Assert.Single(parent.ChildNodes));
        Assert.Equal("a\U0001F600b", first.Data);
    }

    [Fact]
    public void DocumentAndFragmentNormalizeNestedRunsWithoutCrossingCommentsInstructionsOrElements()
    {
        var document = new DomDocument(); var root = document.CreateElement("html"); document.AppendChild(root);
        var fragment = document.CreateDocumentFragment(); var nested = document.CreateElement("div");
        var comment = document.CreateComment("barrier"); var instruction = document.CreateProcessingInstruction("probe", "data");
        root.AppendChild(document.CreateTextNode("a")); root.AppendChild(comment);
        root.AppendChild(document.CreateTextNode("b")); root.AppendChild(instruction);
        root.AppendChild(nested); root.AppendChild(document.CreateTextNode("c"));
        nested.AppendChild(document.CreateTextNode("")); nested.AppendChild(document.CreateTextNode("d"));
        nested.AppendChild(document.CreateTextNode("e"));
        fragment.AppendChild(document.CreateTextNode("")); fragment.AppendChild(document.CreateTextNode(""));
        fragment.Normalize(Cancellation); Assert.Empty(fragment.ChildNodes);
        document.Normalize(Cancellation);
        Assert.Equal(6, root.ChildNodes.Count); Assert.Equal("de", Assert.Single(nested.ChildNodes).TextContent);
        Assert.Equal("abc", string.Concat(root.ChildNodes.OfType<DomText>().Select(t => t.Data)));
        Assert.Equal("barrier", comment.Data); Assert.Equal("data", instruction.Data);
    }

    [Fact]
    public void NormalizingTextDoesNotMergeOrRemoveTheReceiverOrItsSiblings()
    {
        var document = new DomDocument(); var fragment = document.CreateDocumentFragment();
        var empty = document.CreateTextNode(""); var text = document.CreateTextNode("a");
        fragment.AppendChild(empty); fragment.AppendChild(text);
        empty.Normalize(Cancellation); text.Normalize(Cancellation);
        Assert.Equal(2, fragment.ChildNodes.Count); Assert.Same(empty, fragment.FirstChild);
        document.CreateComment("comment").Normalize(Cancellation);
        document.CreateDocumentType("html").Normalize(Cancellation);
    }

    [Fact]
    public void CancellationRejectsBeforeMutationAndDeepTreesDoNotUseTheCallStack()
    {
        var document = new DomDocument(); var root = document.CreateDocumentFragment();
        root.AppendChild(document.CreateTextNode("")); root.AppendChild(document.CreateTextNode("kept"));
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => root.Normalize(canceled.Token));
        Assert.Equal(2, root.ChildNodes.Count);
        DomNode parent = root;
        for (var i = 0; i < 1000; i++)
        {
            var child = document.CreateElement("div"); parent.AppendChild(child); parent = child;
        }
        parent.AppendChild(document.CreateTextNode("a")); parent.AppendChild(document.CreateTextNode("b"));
        root.Normalize(TestContext.Current.CancellationToken);
        Assert.Equal("ab", Assert.Single(parent.ChildNodes).TextContent);
        Assert.Equal(2, root.ChildNodes.Count);
    }
}
