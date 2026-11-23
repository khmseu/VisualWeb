using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class FormControlTests
{
    [Theory]
    [InlineData("button")]
    [InlineData("fieldset")]
    [InlineData("input")]
    [InlineData("object")]
    [InlineData("output")]
    [InlineData("select")]
    [InlineData("textarea")]
    public void FormOwnerUsesNearestAncestorWhenFormAttributeIsAbsent(string localName)
    {
        var (document, root) = CreateDocument();
        var outer = document.CreateElement("form");
        var inner = document.CreateElement("form");
        var control = document.CreateElement(localName);
        root.AppendChild(outer);
        outer.AppendChild(inner);
        inner.AppendChild(control);

        Assert.Same(inner, DomFormControls.FormOwner(control));
        inner.RemoveChild(control);
        outer.AppendChild(control);
        Assert.Same(outer, DomFormControls.FormOwner(control));
    }

    [Fact]
    public void ExplicitFormAttributeUsesFirstMatchingIdAndDoesNotFallBackToAncestor()
    {
        var (document, root) = CreateDocument();
        var ancestorForm = document.CreateElement("form");
        var firstMatch = document.CreateElement("div");
        var targetForm = document.CreateElement("form");
        var input = document.CreateElement("input");
        firstMatch.SetAttribute("id", "target");
        targetForm.SetAttribute("id", "target");
        input.SetAttribute("form", "target");
        root.AppendChild(ancestorForm);
        root.AppendChild(firstMatch);
        root.AppendChild(targetForm);
        ancestorForm.AppendChild(input);

        Assert.Null(DomFormControls.FormOwner(input));

        firstMatch.SetAttribute("id", "other");
        Assert.Same(targetForm, DomFormControls.FormOwner(input));
        targetForm.Remove();
        Assert.Null(DomFormControls.FormOwner(input));
    }

    [Fact]
    public void FormOwnerReflectsAttributeAndTreeChanges()
    {
        var (document, root) = CreateDocument();
        var ancestorForm = document.CreateElement("form");
        var externalForm = document.CreateElement("form");
        var input = document.CreateElement("input");
        externalForm.SetAttribute("id", "external");
        input.SetAttribute("form", "external");
        ancestorForm.AppendChild(input);

        Assert.Same(ancestorForm, DomFormControls.FormOwner(input));

        root.AppendChild(ancestorForm);
        root.AppendChild(externalForm);
        Assert.Same(externalForm, DomFormControls.FormOwner(input));

        input.RemoveAttribute("form");
        Assert.Same(ancestorForm, DomFormControls.FormOwner(input));
    }

    [Fact]
    public void EmptyFormIdCanMatchAnEmptyElementId()
    {
        var (document, root) = CreateDocument();
        var form = document.CreateElement("form");
        var input = document.CreateElement("input");
        form.SetAttribute("id", "");
        input.SetAttribute("form", "");
        root.AppendChild(form);
        root.AppendChild(input);

        Assert.Same(form, DomFormControls.FormOwner(input));
    }

    [Fact]
    public void FormOwnerRejectsElementsOutsideTheListedBuiltInSet()
    {
        var document = new DomDocument();

        Assert.Throws<ArgumentException>(() => DomFormControls.FormOwner(document.CreateElement("div")));
    }

    private static (DomDocument Document, DomElement Root) CreateDocument()
    {
        var document = new DomDocument();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        return (document, root);
    }
}
