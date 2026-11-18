using System.Text.Json;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Core.Tests;

public sealed class UrlSearchParamsTests
{
    [Fact]
    public void FormUrlEncodedParserMatchesPinnedWptVectors()
    {
        using var fixture = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "urlencoded-parser-wpt.json")));
        Assert.Equal(35, fixture.RootElement.GetArrayLength());

        foreach (var vector in fixture.RootElement.EnumerateArray())
        {
            var parameters = new UrlSearchParams(vector.GetProperty("input").GetString()!);
            var expected = vector.GetProperty("output").EnumerateArray()
                .Select(pair => new KeyValuePair<string, string>(
                    pair[0].GetString()!,
                    pair[1].GetString()!))
                .ToArray();

            Assert.Equal(expected, parameters.Entries().ToArray());
        }
    }

    [Fact]
    public void FormUrlEncodedSerializerMatchesPinnedWptVectors()
    {
        using var fixture = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "urlsearchparams-stringifier-wpt.json")));
        Assert.Equal(12, fixture.RootElement.GetArrayLength());

        foreach (var vector in fixture.RootElement.EnumerateArray())
        {
            var entries = vector.GetProperty("entries").EnumerateArray()
                .Select(pair => (IReadOnlyList<string>)[pair[0].GetString()!, pair[1].GetString()!])
                .ToArray();
            var parameters = new UrlSearchParams(entries);

            Assert.Equal(vector.GetProperty("output").GetString(), parameters.ToString());
        }
    }

    [Fact]
    public void SequenceConstructorMatchesPinnedWptVectors()
    {
        using var fixture = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "urlsearchparams-constructor-wpt.json")));
        Assert.Equal(1, fixture.RootElement.GetArrayLength());

        foreach (var vector in fixture.RootElement.EnumerateArray())
        {
            var input = vector.GetProperty("input").EnumerateArray()
                .Select(pair => (IReadOnlyList<string>)[pair[0].GetString()!, pair[1].GetString()!])
                .ToArray();
            var expected = vector.GetProperty("output").EnumerateArray()
                .Select(pair => new KeyValuePair<string, string>(
                    pair[0].GetString()!,
                    pair[1].GetString()!))
                .ToArray();
            var parameters = new UrlSearchParams(input);

            Assert.Equal(expected, parameters.Entries().ToArray());
        }
    }

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

    [Fact]
    public void HasMatchesDuplicateNamesAndNormalizesScalarValueStrings()
    {
        var unpairedSurrogate = new string('\uD800', 1);
        var parameters = new UrlSearchParams("a=first&a=second&%EF%BF%BD=value");

        Assert.True(parameters.Has("a"));
        Assert.True(parameters.Has(unpairedSurrogate));
        Assert.False(parameters.Has("missing"));
    }

    [Fact]
    public void HasWithValueMatchesOneTupleAndNormalizesBothStrings()
    {
        var unpairedSurrogate = new string('\uD800', 1);
        var parameters = new UrlSearchParams(
            "a=first&a=second&%EF%BF%BD=%EF%BF%BD&left=one&right=two");

        Assert.True(parameters.Has("a", "second"));
        Assert.True(parameters.Has(unpairedSurrogate, unpairedSurrogate));
        Assert.False(parameters.Has("left", "two"));
        Assert.False(parameters.Has("a", "missing"));
    }

    [Fact]
    public void DeleteRemovesEveryMatchingNameAndNormalizesScalarValueStrings()
    {
        var unpairedSurrogate = new string('\uD800', 1);
        var parameters = new UrlSearchParams(
            "a=first&b=middle&a=last&b=second&%EF%BF%BD=value&c=end");

        parameters.Delete("a");

        Assert.Equal(4, parameters.Size);
        Assert.Empty(parameters.GetAll("a"));
        Assert.Equal(new[] { "middle", "second" }, parameters.GetAll("b"));
        Assert.Equal("end", parameters.Get("c"));

        parameters.Delete(unpairedSurrogate);

        Assert.Equal(3, parameters.Size);
        Assert.Null(parameters.Get("\uFFFD"));
        parameters.Delete("missing");
        Assert.Equal(3, parameters.Size);
    }

    [Fact]
    public void DeleteWithValueRemovesOnlyMatchingTuplesAndNormalizesScalarValueStrings()
    {
        var unpairedSurrogate = new string('\uD800', 1);
        var parameters = new UrlSearchParams(
            "a=first&b=middle&a=last&a=first&%EF%BF%BD=%EF%BF%BD&%EF%BF%BD=other");

        parameters.Delete("a", "first");

        Assert.Equal(4, parameters.Size);
        Assert.Equal(new[] { "last" }, parameters.GetAll("a"));
        Assert.Equal("middle", parameters.Get("b"));

        parameters.Delete(unpairedSurrogate, unpairedSurrogate);

        Assert.Equal(3, parameters.Size);
        Assert.Equal(new[] { "other" }, parameters.GetAll("\uFFFD"));
        parameters.Delete("a", "missing");
        Assert.Equal(3, parameters.Size);
    }

    [Fact]
    public void SetReplacesFirstMatchingTupleAndRemovesLaterDuplicates()
    {
        var parameters = new UrlSearchParams("a=first&b=middle&a=last&a=third&c=end");

        parameters.Set("a", "replacement");

        Assert.Equal(3, parameters.Size);
        Assert.Equal(new[] { "replacement" }, parameters.GetAll("a"));
        Assert.Equal("middle", parameters.Get("b"));
        Assert.Equal("end", parameters.Get("c"));
    }

    [Fact]
    public void SetAppendsMissingNameAndNormalizesScalarValueStrings()
    {
        var unpairedSurrogate = new string('\uD800', 1);
        var parameters = new UrlSearchParams("a=1&b=2");

        parameters.Set(unpairedSurrogate, unpairedSurrogate);

        Assert.Equal(3, parameters.Size);
        Assert.Equal(new[] { "\uFFFD" }, parameters.GetAll("\uFFFD"));

        parameters.Set("new", "appended");

        Assert.Equal(4, parameters.Size);
        Assert.Equal("appended", parameters.Get("new"));
    }

    [Fact]
    public void SortOrdersNamesByUtf16CodeUnitsAndKeepsEqualNamesStable()
    {
        var parameters = new UrlSearchParams("z=last&b=first&a=one&b=second&%EE%80%80=bmp&%F0%90%80%80=supplementary");

        parameters.Sort();

        Assert.Equal("a=one&b=first&b=second&z=last&%F0%90%80%80=supplementary&%EE%80%80=bmp", parameters.ToString());
    }

    [Fact]
    public void SortIsSafeForAnEmptyListAndCanBeRepeated()
    {
        var parameters = new UrlSearchParams();

        parameters.Sort();
        Assert.Equal("", parameters.ToString());

        parameters.Append("b", "second");
        parameters.Append("a", "first");
        parameters.Sort();
        parameters.Sort();

        Assert.Equal("a=first&b=second", parameters.ToString());
    }

    [Fact]
    public void ToStringSerializesOrderedPairsWithFormUrlEncodedUtf8()
    {
        var parameters = new UrlSearchParams(
            "?a=hello+world&a=%2B&flag&empty=&name=%E2%9C%93&symbols=%21%27%28%29~%2A&q=x%26y%3Dz&=x");

        Assert.Equal(
            "a=hello+world&a=%2B&flag=&empty=&name=%E2%9C%93&symbols=%21%27%28%29%7E*&q=x%26y%3Dz&=x",
            parameters.ToString());
    }

    [Fact]
    public void ToStringSerializesEmptyAndScalarValueNormalizedEntries()
    {
        var unpairedSurrogate = new string('\uD800', 1);
        var parameters = new UrlSearchParams();

        Assert.Equal("", parameters.ToString());

        parameters.Append(unpairedSurrogate, "a b");

        Assert.Equal("%EF%BF%BD=a+b", parameters.ToString());
    }

    [Fact]
    public void SequenceInitializationPreservesOrderDuplicatesAndNormalizesScalarValues()
    {
        var parameters = new UrlSearchParams(new IReadOnlyList<string>[]
        {
            ["b", "1"], ["a", "x y"], ["b", "2"], ["\uD800", "\uDC00"],
        });

        Assert.Equal(4, parameters.Size);
        Assert.Equal(["1", "2"], parameters.GetAll("b"));
        Assert.Equal("b=1&a=x+y&b=2&%EF%BF%BD=%EF%BF%BD", parameters.ToString());
    }

    [Fact]
    public void SequenceInitializationAcceptsEmptyInputAndRejectsMalformedPairs()
    {
        Assert.Equal(0, new UrlSearchParams(Array.Empty<IReadOnlyList<string>>()).Size);
        Assert.Throws<ArgumentException>(() => new UrlSearchParams(new IReadOnlyList<string>[] { ["a"] }));
        Assert.Throws<ArgumentException>(() => new UrlSearchParams(new IReadOnlyList<string>[] { ["a", "b", "c"] }));
        Assert.Throws<ArgumentException>(() => new UrlSearchParams(new IReadOnlyList<string>[] { null! }));
        Assert.Throws<ArgumentNullException>(() => new UrlSearchParams((IEnumerable<IReadOnlyList<string>>)null!));
    }

    [Fact]
    public void RecordInitializationKeepsOrderAndCollapsesKeysEqualAfterNormalization()
    {
        var parameters = new UrlSearchParams(new KeyValuePair<string, string>[]
        {
            new("b", "1"), new("\uD800", "old"), new("a", "x y"), new("\uDFFF", "new"),
        });

        Assert.Equal(3, parameters.Size);
        Assert.Equal("b=1&%EF%BF%BD=new&a=x+y", parameters.ToString());
    }

    [Fact]
    public void RecordInitializationAcceptsDictionariesAndRejectsNullStrings()
    {
        Assert.Equal("k=v", new UrlSearchParams(new Dictionary<string, string> { ["k"] = "v" }).ToString());
        Assert.Equal(0, new UrlSearchParams(new Dictionary<string, string>()).Size);
        Assert.Throws<ArgumentException>(() => new UrlSearchParams(new KeyValuePair<string, string>[] { new("k", null!) }));
        Assert.Throws<ArgumentNullException>(() => new UrlSearchParams((IEnumerable<KeyValuePair<string, string>>)null!));
    }

    [Fact]
    public void EntriesEnumeratesPairsInOrderIncludingDuplicates()
    {
        var parameters = new UrlSearchParams("b=1&a=2&b=3");

        Assert.Equal(
            [new("b", "1"), new("a", "2"), new("b", "3")],
            parameters.Entries().ToArray());
        Assert.Empty(new UrlSearchParams().Entries());
    }

    [Fact]
    public void EntriesIteratorObservesMutationsMadeDuringEnumeration()
    {
        var parameters = new UrlSearchParams("a=1&b=2");
        var seen = new List<string>();

        foreach (var (name, _) in parameters.Entries())
        {
            seen.Add(name);
            if (name == "a")
            {
                parameters.Delete("a");
                parameters.Append("c", "3");
            }
        }

        // Removing the current entry shifts "b" into the visited index, which the live iterator skips.
        Assert.Equal(["a", "c"], seen);
    }

    [Fact]
    public void KeysEnumeratesNamesInOrderIncludingDuplicatesAndObservesAppends()
    {
        var parameters = new UrlSearchParams("b=1&a=2&b=3");

        Assert.Equal(["b", "a", "b"], parameters.Keys().ToArray());
        Assert.Empty(new UrlSearchParams().Keys());

        var seen = new List<string>();
        foreach (var name in parameters.Keys())
        {
            seen.Add(name);
            if (seen.Count == 1) { parameters.Append("z", "9"); }
        }

        Assert.Equal(["b", "a", "b", "z"], seen);
    }

    [Fact]
    public void ValuesEnumeratesValuesInOrderIncludingDuplicatesAndEmptyValues()
    {
        var parameters = new UrlSearchParams("b=1&a&b=1&c=%E2%9C%93");

        Assert.Equal(["1", "", "1", "\u2713"], parameters.Values().ToArray());
        Assert.Empty(new UrlSearchParams().Values());

        var seen = new List<string>();
        foreach (var value in parameters.Values())
        {
            seen.Add(value);
            if (seen.Count == 1) { parameters.Append("z", "9"); }
        }

        Assert.Equal(["1", "", "1", "\u2713", "9"], seen);
    }

    [Fact]
    public void ForEachPassesValueThenNameInOrderAndVisitsAppendedEntries()
    {
        var parameters = new UrlSearchParams("b=1&a=2");
        var calls = new List<string>();

        parameters.ForEach((value, name) =>
        {
            calls.Add($"{name}={value}");
            if (calls.Count == 1) { parameters.Append("c", "3"); }
        });

        Assert.Equal(["b=1", "a=2", "c=3"], calls);
        new UrlSearchParams().ForEach((_, _) => throw new InvalidOperationException());
        Assert.Throws<ArgumentNullException>(() => parameters.ForEach(null!));
    }
}
