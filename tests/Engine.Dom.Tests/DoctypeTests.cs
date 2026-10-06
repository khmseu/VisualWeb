using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class DoctypeTests
{
    [Fact]
    public void LookupSkipsCommentsAndMetadataSurvivesRemovalAndAdoptionWithoutChangingMode()
    {
        var document = new DomDocument { Mode = DomDocumentMode.Quirks };
        var doctype = document.CreateDocumentType("MiXeD:\u00c9", "PUBLIC\0\ud800", "SYSTEM\0\udfff");
        document.AppendChild(document.CreateComment("before")); document.AppendChild(doctype);
        Assert.Same(doctype, document.Doctype); Assert.Equal("MiXeD:\u00c9", doctype.Name);
        Assert.Equal(doctype.Name, doctype.NodeName); Assert.Equal("PUBLIC\0\ud800", doctype.PublicId);
        Assert.Equal("SYSTEM\0\udfff", doctype.SystemId); Assert.Equal(DomNodeType.DocumentType, doctype.NodeType);
        doctype.Remove(); Assert.Null(document.Doctype); Assert.Same(document, doctype.OwnerDocument);
        Assert.Equal(DomDocumentMode.Quirks, document.Mode);
        var destination = new DomDocument(); destination.AdoptNode(doctype); destination.AppendChild(doctype);
        Assert.Same(doctype, destination.Doctype); Assert.Same(destination, doctype.OwnerDocument);
        Assert.Equal("PUBLIC\0\ud800", doctype.PublicId); Assert.Equal("SYSTEM\0\udfff", doctype.SystemId);
        Assert.Equal(DomDocumentMode.NoQuirks, destination.Mode);
    }

    [Fact]
    public void MissingDoctypeIsNullAndMissingIdentifiersAreEmptyStrings()
    {
        var document = new DomDocument(); Assert.Null(document.Doctype);
        var doctype = document.CreateDocumentType(""); document.AppendChild(doctype);
        Assert.Same(doctype, document.Doctype); Assert.Equal("", doctype.Name);
        Assert.Equal("", doctype.PublicId); Assert.Equal("", doctype.SystemId);
    }
}
