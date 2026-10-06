using Xunit;

namespace VisualWeb.Engine.Dom.Tests;

public sealed class ToggleAttributeTests
{
    [Fact]
    public void ToggleNormalizesHtmlNamesAndPreservesForcedExistingValuesAndOrder()
    {
        var element = new DomDocument().CreateElement("div");
        Assert.False(element.ToggleAttribute("DATA-X", false));
        Assert.Empty(element.Attributes);
        Assert.True(element.ToggleAttribute("DATA-X"));
        Assert.Equal("", element.GetAttribute("data-x"));
        element.SetAttribute("data-x", "retained");
        element.SetAttribute("other", "second");
        Assert.True(element.ToggleAttribute("Data-X", true));
        Assert.Equal("retained", element.GetAttribute("data-x"));
        Assert.Equal(new[] { "data-x", "other" }, element.Attributes.Keys);
        Assert.False(element.ToggleAttribute("DATA-X"));
        Assert.True(element.ToggleAttribute("DATA-X", true));
        Assert.Equal(new[] { "other", "data-x" }, element.Attributes.Keys);
        Assert.Equal("", element.GetAttribute("data-x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad name")]
    [InlineData("bad\tname")]
    [InlineData("bad\nname")]
    [InlineData("bad\rname")]
    [InlineData("bad\fname")]
    [InlineData("bad\0name")]
    [InlineData("bad/name")]
    [InlineData("bad=name")]
    [InlineData("bad>name")]
    public void InvalidNamesFailEvenForForcedAbsenceWithoutMutation(string name)
    {
        var element = new DomDocument().CreateElement("div");
        element.SetAttribute("kept", "value");
        Assert.Equal(DomError.InvalidCharacter, Assert.Throws<DomException>(() => element.ToggleAttribute(name, false)).Error);
        Assert.Single(element.Attributes);
        Assert.Equal("value", element.GetAttribute("kept"));
    }

    [Fact]
    public void OrdinaryRemovalAndReadditionAlsoAppendWithoutReorderingReplacement()
    {
        var element = new DomDocument().CreateElement("div");
        element.SetAttribute("first", "1"); element.SetAttribute("second", "2");
        element.SetAttribute("FIRST", "updated");
        Assert.Equal(new[] { "first", "second" }, element.Attributes.Keys);
        element.RemoveAttribute("first"); element.SetAttribute("FIRST", "new");
        Assert.Equal(new[] { "second", "first" }, element.Attributes.Keys);
    }
}
