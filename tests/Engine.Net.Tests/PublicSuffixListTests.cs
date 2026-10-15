using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class PublicSuffixListTests
{
    [Fact]
    public void ExactWildcardExceptionAndDefaultRulesSelectPublicSuffixes()
    {
        var list = PublicSuffixList.Parse("""
            // test rules
            com
            co.uk
            *.ck
            !www.ck
            公司.cn
            """);

        Assert.True(list.IsPublicSuffix("com"));
        Assert.True(list.IsPublicSuffix("co.uk"));
        Assert.False(list.IsPublicSuffix("example.com"));
        Assert.True(list.IsPublicSuffix("shop.ck"));
        Assert.False(list.IsPublicSuffix("www.ck"));
        Assert.False(list.IsPublicSuffix("a.www.ck"));
        Assert.True(list.IsPublicSuffix("xn--55qx5d.cn"));
        Assert.True(list.IsPublicSuffix("unknown"));
        Assert.False(list.IsPublicSuffix("example.unknown"));
    }

    [Fact]
    public void ParserNormalizesCaseRootDotsAndIdna()
    {
        var list = PublicSuffixList.Parse("COM.\nBÜCHER.example\n");

        Assert.True(list.IsPublicSuffix("com"));
        Assert.True(list.IsPublicSuffix("xn--bcher-kva.example."));
        Assert.False(list.IsPublicSuffix("shop.xn--bcher-kva.example"));
    }

    [Fact]
    public void ParserRejectsInvalidDuplicateAndOversizedInput()
    {
        Assert.Throws<InvalidDataException>(() => PublicSuffixList.Parse(""));
        Assert.Throws<InvalidDataException>(() => PublicSuffixList.Parse("com\nCOM\n"));
        Assert.Throws<InvalidDataException>(() => PublicSuffixList.Parse("bad/domain\n"));
        Assert.Throws<InvalidDataException>(() => PublicSuffixList.Parse(new string('a', PublicSuffixList.MaxLineCharacters + 1)));
        Assert.Throws<InvalidDataException>(() => PublicSuffixList.Parse(new string('\n', PublicSuffixList.MaxCharacters + 1)));
    }

    [Fact]
    public void PinnedListIncludesIcannAndPrivateRules()
    {
        var list = PublicSuffixList.Default;

        Assert.Equal(10336, list.RuleCount);
        Assert.True(list.IsPublicSuffix("com"));
        Assert.True(list.IsPublicSuffix("co.uk"));
        Assert.True(list.IsPublicSuffix("github.io"));
        Assert.True(list.IsPublicSuffix("s3.amazonaws.com"));
        Assert.False(list.IsPublicSuffix("example.com"));
        Assert.False(list.IsPublicSuffix("project.github.io"));
    }
}
