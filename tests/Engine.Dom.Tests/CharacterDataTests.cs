using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class CharacterDataTests
{
    [Fact]
    public void SubstringAndReplacementUseUtf16AndClampWithoutUnsignedOverflow()
    {
        var text = new DomDocument().CreateTextNode("a\U0001F600b");
        Assert.Equal(4, text.Length);
        Assert.Equal("\ud83d", text.SubstringData(1, 1));
        Assert.Equal("\ude00b", text.SubstringData(2, uint.MaxValue));
        Assert.Equal("", text.SubstringData(4, uint.MaxValue));
        text.ReplaceData(2, uint.MaxValue, "X");
        Assert.Equal("a\ud83dX", text.Data);
        text.ReplaceData(3, 0, "end");
        Assert.Equal("a\ud83dXend", text.Data);
    }

    [Fact]
    public void InvalidOffsetsAndNullReplacementDoNotMutate()
    {
        var comment = new DomDocument().CreateComment("kept");
        Assert.Equal(DomError.IndexSize, Assert.Throws<DomException>(() => comment.ReplaceData(uint.MaxValue, 1, "bad")).Error);
        Assert.Equal(DomError.IndexSize, Assert.Throws<DomException>(() => comment.SubstringData(5, 0)).Error);
        Assert.Throws<ArgumentNullException>(() => comment.ReplaceData(0, 0, null!));
        Assert.Equal("kept", comment.Data);
    }

    [Fact]
    public void ProcessingInstructionDataAndTextContentStayLive()
    {
        var pi = new DomDocument().CreateProcessingInstruction("target", "abc");
        pi.ReplaceData(1, 1, "XYZ");
        Assert.Equal("aXYZc", pi.TextContent);
        Assert.Equal("target", pi.Target);
        pi.TextContent = null;
        Assert.Equal(0, pi.Length);
    }
}
