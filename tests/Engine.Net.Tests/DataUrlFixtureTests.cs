using System.Text.Json;
using VisualWeb.Core.Url;
using Xunit;

namespace VisualWeb.Engine.Net.Tests;

public sealed class DataUrlFixtureTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var file in new[] { "data-urls.json", "base64.json" })
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", file)));
            foreach (var entry in json.RootElement.EnumerateArray())
            {
                yield return [file == "base64.json", entry.Clone()];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OfficialDataUrlFixture(bool base64, JsonElement entry)
    {
        var input = (base64 ? "data:;base64," : "") + entry[0].GetString();
        using var loader = new ResourceLoader();
        if (entry[1].ValueKind == JsonValueKind.Null)
        {
            var parsed = BrowserUrl.ParseResult(input);
            if (parsed.Success)
            {
                await Assert.ThrowsAsync<ResourceLoadException>(() =>
                    loader.LoadAsync(parsed.Url!, cancellationToken: TestContext.Current.CancellationToken));
            }
        }
        else
        {
            var response = await loader.LoadAsync(BrowserUrl.Parse(input), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(entry[base64 ? 1 : 2].EnumerateArray().Select(e => e.GetByte()).ToArray(), response.Body.ToArray());
            Assert.Equal(base64 ? "text/plain;charset=US-ASCII" : entry[1].GetString(), response.ContentType!.Serialize());
        }
    }
}
