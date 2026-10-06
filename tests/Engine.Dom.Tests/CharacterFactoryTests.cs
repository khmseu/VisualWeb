using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class CharacterFactoryTests
{
    [Fact]
    public void CommentFactoryPreservesRawUtf16IncludingMarkupAndDetachedOwnership()
    {
        var document = new DomDocument(); var comment = document.CreateComment("-->\0\ud800");
        Assert.Equal("-->\0\ud800", comment.Data); Assert.Equal(DomNodeType.Comment, comment.NodeType);
        Assert.Same(document, comment.OwnerDocument); Assert.Null(comment.ParentNode);
    }

    [Fact]
    public void InstructionFactoryPreservesTargetAndDataAndRejectsInvalidInitialValues()
    {
        var document = new DomDocument(); var pi = document.CreateProcessingInstruction("Probe:Name", "a\0\ud800");
        Assert.Equal("Probe:Name", pi.Target); Assert.Equal(pi.Target, pi.NodeName);
        Assert.Equal("a\0\ud800", pi.Data); Assert.Same(document, pi.OwnerDocument); Assert.Null(pi.ParentNode);
        foreach (var target in new[] { "", "1bad", "bad name", "\0", "\ud800" })
        {
            Assert.Equal(DomError.InvalidCharacter,
                Assert.Throws<DomException>(() => document.CreateProcessingInstruction(target, "data")).Error);
        }
        Assert.Equal(DomError.InvalidCharacter,
            Assert.Throws<DomException>(() => document.CreateProcessingInstruction("probe", "a?>b")).Error);
        pi.Data = "?>"; Assert.Equal("?>", pi.Data); Assert.Equal("Probe:Name", pi.Target);
    }
}
