using System.Text.Json;
using VisualWeb.Core.Mime;
using Xunit;

namespace VisualWeb.Core.Tests;

public sealed class MimeTests
{
    public static IEnumerable<object[]> Cases()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "mime-types.json")));
        foreach (var entry in json.RootElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
        {
            yield return [entry.Clone()];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OfficialMimeParsingFixture(JsonElement entry)
    {
        var parsed = MimeType.ParseResult(entry.GetProperty("input").GetString()!);
        var expected = entry.GetProperty("output");
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.False(parsed.Success);
            Assert.NotNull(parsed.Error);
        }
        else
        {
            Assert.True(parsed.Success, parsed.Error);
            Assert.Equal(expected.GetString(), parsed.Value!.Serialize());
        }
    }

    [Fact]
    public void ParameterNamesFoldButValuesAndFirstOccurrenceArePreserved()
    {
        var mime = MimeType.Parse("TEXT/HTML;Charset=UTF-8;charset=other;foo=\"a\\\"b\"");
        Assert.Equal("text/html", mime.Essence);
        Assert.Equal("UTF-8", mime.Parameters["charset"]);
        Assert.Equal("a\"b", mime.Parameters["foo"]);
    }
}
