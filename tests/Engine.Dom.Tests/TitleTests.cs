using VisualWeb.Engine.Dom;
using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class TitleTests
{
    [Fact]
    public void TitleCreatesInHeadPreservesTextAndCollapsesOnlyAsciiWhitespace()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html"); document.AppendChild(html);
        var head = document.CreateElement("head"); html.AppendChild(head);
        Assert.Equal("", document.Title);
        document.Title = " \tHello\n \rworld\f\u00A0 ";
        Assert.Equal("Hello world \u00A0", document.Title);
        Assert.Equal(" \tHello\n \rworld\f\u00A0 ", head.FirstChild!.TextContent);
        document.Title = "";
        Assert.Single(head.ChildNodes);
        Assert.Empty(head.FirstChild!.ChildNodes);
    }
    [Fact]
    public void FirstTitleInTreeOrderWinsEvenOutsideHeadAndMissingHeadIsNoOp()
    {
        var document = new DomDocument();
        document.Title = "ignored";
        Assert.Empty(document.ChildNodes);
        var html = document.CreateElement("html"); document.AppendChild(html);
        var body = document.CreateElement("body"); html.AppendChild(body);
        document.Title = "ignored";
        Assert.Empty(body.ChildNodes);
        var first = document.CreateElement("title"); body.AppendChild(first);
        var second = document.CreateElement("title"); body.AppendChild(second);
        first.TextContent = "first"; second.TextContent = "second";
        document.Title = "updated";
        Assert.Equal("updated", first.TextContent);
        Assert.Equal("second", second.TextContent);
        Assert.Equal("updated", document.Title);
        Assert.Throws<ArgumentNullException>(() => document.Title = null!);
    }
}
