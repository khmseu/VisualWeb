using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Html.Tests;

public sealed class HtmlFragmentSerializerTests
{
    [Fact]
    public void SerializesOrderedAttributesTextRawTextVoidCommentsInstructionsAndTemplateContent()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        var attributes = document.CreateElement("span");
        attributes.SetAttribute("z", "first");
        attributes.SetAttribute("a", "&\"\u00a0");
        root.AppendChild(attributes);
        root.AppendChild(document.CreateTextNode("a<&>\u00a0"));
        var lineBreak = document.CreateElement("br");
        lineBreak.AppendChild(document.CreateTextNode("ignored"));
        root.AppendChild(lineBreak);
        var script = document.CreateElement("script");
        script.AppendChild(document.CreateTextNode("if(a<b){x&y}"));
        root.AppendChild(script);
        root.AppendChild(document.CreateComment("note"));
        root.AppendChild(document.CreateProcessingInstruction("target", "data"));
        var template = Assert.IsType<DomTemplateElement>(document.CreateElement("template"));
        template.AppendChild(document.CreateTextNode("not-content"));
        template.Content.AppendChild(document.CreateElement("i"));
        Assert.IsType<DomElement>(template.Content.FirstChild).AppendChild(document.CreateTextNode("inside"));
        root.AppendChild(template);

        Assert.Equal("<span z=\"first\" a=\"&amp;&quot;&nbsp;\"></span>a&lt;&amp;&gt;&nbsp;<br>"
            + "<script>if(a<b){x&y}</script><!--note--><?target data?><template><i>inside</i></template>",
            HtmlFragmentSerializer.SerializeChildren(root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ProcessingInstructionWithEmptyDataStillHasRequiredSpace()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        root.AppendChild(document.CreateProcessingInstruction("target", ""));

        Assert.Equal("<?target ?>", HtmlFragmentSerializer.SerializeChildren(root,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void OutputLimitRejectsRawTextAndCommentAppendsInsteadOfTruncating()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        var script = document.CreateElement("script");
        script.AppendChild(document.CreateTextNode("12345"));
        root.AppendChild(script);

        Assert.Throws<HtmlLimitException>(() => HtmlFragmentSerializer.SerializeChildren(root, maxOutputCharacters: 12,
            cancellationToken: TestContext.Current.CancellationToken));

        root = document.CreateElement("div");
        root.AppendChild(document.CreateComment("note"));
        Assert.Throws<HtmlLimitException>(() => HtmlFragmentSerializer.SerializeChildren(root, maxOutputCharacters: 10,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SerializationRejectsUnsupportedNodesAndEnforcesNodeOutputAndAttributeLimits()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        var child = document.CreateElement("i");
        child.SetAttribute("a", "1");
        child.SetAttribute("b", "2");
        child.AppendChild(document.CreateTextNode("12345"));
        root.AppendChild(child);
        var cancellation = TestContext.Current.CancellationToken;
        Assert.Throws<HtmlLimitException>(() => HtmlFragmentSerializer.SerializeChildren(root, maxNodes: 1,
            cancellationToken: cancellation));
        Assert.Throws<HtmlLimitException>(() => HtmlFragmentSerializer.SerializeChildren(root, maxOutputCharacters: 4,
            cancellationToken: cancellation));
        Assert.Throws<HtmlLimitException>(() => HtmlFragmentSerializer.SerializeChildren(root, maxAttributes: 1,
            cancellationToken: cancellation));
        Assert.ThrowsAny<OperationCanceledException>(() => HtmlFragmentSerializer.SerializeChildren(root,
            cancellationToken: new CancellationToken(canceled: true)));
        Assert.Throws<UnsupportedHtmlException>(() => HtmlFragmentSerializer.SerializeChildren(document.CreateElement("svg"),
            cancellationToken: cancellation));
    }
}
