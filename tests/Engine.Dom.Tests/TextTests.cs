using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class TextTests
{
    [Fact]
    public void SplitInsertsImmediatelyAfterOriginalPreservingDataAndLinks()
    {
        var document = new DomDocument();
        var parent = document.CreateElement("div");
        var text = document.CreateTextNode("a\U0001F600b");
        var after = document.CreateComment("stop");
        parent.AppendChild(text); parent.AppendChild(after);
        var created = text.SplitText(2);
        Assert.Equal("a\ud83d", text.Data);
        Assert.Equal("\ude00b", created.Data);
        Assert.Same(parent, created.ParentNode);
        Assert.Same(document, created.OwnerDocument);
        Assert.Same(created, text.NextSibling);
        Assert.Same(after, created.NextSibling);
        Assert.Equal("a\U0001F600b", created.WholeText);
        Assert.Equal(new[] { text, created }, created.GetContiguousTextNodes());
    }

    [Fact]
    public void DetachedAndBoundarySplitsProduceDistinctNodesAndInvalidOffsetsAreAtomic()
    {
        var text = new DomDocument().CreateTextNode("abc");
        var remainder = text.SplitText(0);
        Assert.Equal("", text.Data); Assert.Equal("abc", remainder.Data);
        Assert.Null(remainder.ParentNode);
        Assert.Equal("", text.WholeText);
        var empty = remainder.SplitText(3);
        Assert.NotSame(remainder, empty);
        Assert.Equal("", empty.Data); Assert.Equal("abc", remainder.Data);
        Assert.Equal(DomError.IndexSize, Assert.Throws<DomException>(() => remainder.SplitText(uint.MaxValue)).Error);
        Assert.Equal("abc", remainder.Data);
    }

    [Fact]
    public void WholeTextIncludesEmptyNodesAndStopsAtCommentsAndElements()
    {
        var document = new DomDocument();
        var fragment = document.CreateDocumentFragment();
        var a = document.CreateTextNode("a"); var empty = document.CreateTextNode("");
        var b = document.CreateTextNode("b"); var c = document.CreateTextNode("c");
        fragment.AppendChild(document.CreateElement("stop"));
        fragment.AppendChild(a); fragment.AppendChild(empty); fragment.AppendChild(b);
        fragment.AppendChild(document.CreateComment("stop")); fragment.AppendChild(c);
        Assert.Equal("ab", empty.WholeText);
        Assert.Equal("c", c.WholeText);
        fragment.RemoveChild(b);
        Assert.Equal("a", empty.WholeText);
        Assert.Equal("b", b.WholeText);
    }
}
