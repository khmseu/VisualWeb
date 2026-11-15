using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Core.Tests;

public sealed class UrlSearchParamsTests
{
    [Fact]
    public void StringInitializerPreservesOrderedDuplicatePairs()
    {
        var parameters = new UrlSearchParams("?a=first&a=second&flag&empty=&=x&&b=x=y");

        Assert.Equal(6, parameters.Size);
        Assert.Equal("first", parameters.Get("a"));
        var all = parameters.GetAll("a");
        Assert.Equal(new[] { "first", "second" }, all);
        all[0] = "changed";
        Assert.Equal("first", parameters.Get("a"));
        Assert.Equal("", parameters.Get("flag"));
        Assert.Equal("", parameters.Get("empty"));
        Assert.Equal("x", parameters.Get(""));
        Assert.Equal("x=y", parameters.Get("b"));
        Assert.Null(parameters.Get("missing"));
        Assert.Empty(parameters.GetAll("missing"));
    }

    [Fact]
    public void StringInitializerDecodesPlusPercentEscapesAndMalformedUtf8()
    {
        var parameters = new UrlSearchParams("q=a+b%26%2B%25&emoji=%F0%9F%98%80&bad=%G1&partial=%E2%28%A1");

        Assert.Equal("a b&+%", parameters.Get("q"));
        Assert.Equal("\U0001F600", parameters.Get("emoji"));
        Assert.Equal("%G1", parameters.Get("bad"));
        Assert.Equal("\uFFFD(\uFFFD", parameters.Get("partial"));
    }

    [Fact]
    public void StringInitializerRemovesOnlyOneLeadingQuestionMark()
    {
        Assert.Equal(0, new UrlSearchParams("?").Size);
        Assert.Equal("y", new UrlSearchParams("??x=y").Get("?x"));
    }

    [Fact]
    public void StringInitializerAndLookupUseScalarValueStrings()
    {
        var unpairedSurrogate = new string('\uD800', 1);
        var parameters = new UrlSearchParams(unpairedSurrogate + "=value");

        Assert.Equal("value", parameters.Get(unpairedSurrogate));
        Assert.Equal("value", parameters.Get("\uFFFD"));
    }

    [Fact]
    public void AppendPreservesOrderAndNormalizesScalarValueStrings()
    {
        var unpairedSurrogate = new string('\uD800', 1);
        var parameters = new UrlSearchParams("a=first&a=second");

        parameters.Append("a", "third");
        parameters.Append(unpairedSurrogate, unpairedSurrogate);

        Assert.Equal(4, parameters.Size);
        Assert.Equal(new[] { "first", "second", "third" }, parameters.GetAll("a"));
        Assert.Equal("\uFFFD", parameters.Get("\uFFFD"));
    }
}
