using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace VisualWeb.Engine.Html.Tests;

public sealed class TokenizerTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Data", "tokenizer"), "*.test").Order())
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var entry in json.RootElement.GetProperty("tests").EnumerateArray())
            {
                var states = entry.TryGetProperty("initialStates", out var initial)
                    ? initial.EnumerateArray().Select(e => e.GetString()!).ToArray() : ["Data state"];
                foreach (var state in states)
                {
                    yield return [Path.GetFileName(file), state, entry.Clone()];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OfficialTokenizerTokens(string file, string state, JsonElement entry)
    {
        Assert.NotEmpty(file);
        var mode = state switch
        {
            "Data state" => HtmlTextMode.Data,
            "RCDATA state" => HtmlTextMode.Rcdata,
            "RAWTEXT state" => HtmlTextMode.Rawtext,
            "Script data state" => HtmlTextMode.ScriptData,
            "PLAINTEXT state" => HtmlTextMode.Plaintext,
            "CDATA section state" => HtmlTextMode.Cdata,
            _ => throw new InvalidOperationException("Unknown fixture initial state: " + state)
        };
        var escaped = entry.TryGetProperty("doubleEscaped", out var doubleEscaped) && doubleEscaped.GetBoolean();
        string ReadString(JsonElement value)
        {
            var text = value.GetString()!;
            return escaped ? Regex.Replace(text, @"\\u([0-9a-fA-F]{4})", match =>
                ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString()) : text;
        }

        var tokenizer = new HtmlTokenizer(ReadString(entry.GetProperty("input")), mode,
            entry.TryGetProperty("lastStartTag", out var tag) ? tag.GetString() : null,
            cancellationToken: TestContext.Current.CancellationToken);
        var actual = new List<object[]>();
        while (tokenizer.Read() is var token && token is not HtmlEndOfFile)
        {
            switch (token)
            {
                case HtmlCharacters chars:
                    if (actual.LastOrDefault() is ["Character", string previous])
                    {
                        actual[^1] = ["Character", previous + chars.Data];
                    }
                    else
                    {
                        actual.Add(["Character", chars.Data]);
                    }

                    break;
                case HtmlTag { IsEndTag: true } end:
                    actual.Add(["EndTag", end.Name]);
                    break;
                case HtmlTag start:
                    actual.Add(start.SelfClosing ? ["StartTag", start.Name, start.Attributes, true]
                        : ["StartTag", start.Name, start.Attributes]);
                    break;
                case HtmlComment comment:
                    actual.Add(["Comment", comment.Data]);
                    break;
                case HtmlProcessingInstruction instruction:
                    actual.Add(["ProcessingInstruction", instruction.Target, instruction.Data]);
                    break;
                case HtmlDoctype doctype:
                    actual.Add(["DOCTYPE", doctype.Name!, doctype.PublicId!, doctype.SystemId!, !doctype.ForceQuirks]);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected token.");
            }
        }

        var expected = entry.GetProperty("output").EnumerateArray().Select(e => ConvertValue(e)).ToArray();
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        var expectedErrors = entry.TryGetProperty("errors", out var parseErrors)
            ? parseErrors.EnumerateArray().Select(e => e.GetProperty("code").GetString() is
                "unexpected-question-mark-instead-of-tag-name" ? "invalid-first-character-of-processing-instruction-target"
                : e.GetProperty("code").GetString()!).Order().ToArray() : [];
        Assert.Equal(expectedErrors, tokenizer.Errors.Select(e => e.Code).Order().ToArray());

        object? ConvertValue(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertValue).ToArray(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(p => escaped
                ? Regex.Replace(p.Name, @"\\u([0-9a-fA-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString()) : p.Name,
                p => ReadString(p.Value)),
            JsonValueKind.String => ReadString(value),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => throw new InvalidOperationException("Unexpected fixture value.")
        };
    }

    [Fact]
    public void DuplicateAttributesAndNullsHaveDiagnostics()
    {
        var tokenizer = new HtmlTokenizer("<p id=a ID=b>\0");
        var tag = Assert.IsType<HtmlTag>(tokenizer.Read());
        Assert.Equal("a", tag.Attributes["id"]);
        Assert.IsType<HtmlCharacters>(tokenizer.Read());
        Assert.Contains(tokenizer.Errors, e => e.Code == "duplicate-attribute");
        Assert.Contains(tokenizer.Errors, e => e.Code == "unexpected-null-character");
    }

    [Fact]
    public void LimitsAndCancellationFailExplicitly()
    {
        Assert.Throws<HtmlLimitException>(() => new HtmlTokenizer("12345", options: new() { MaxInputCharacters = 4 }));
        var tokenizer = new HtmlTokenizer("\0\0", options: new() { MaxErrors = 1 });
        Assert.Throws<HtmlLimitException>(() => tokenizer.Read());
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            new HtmlTokenizer("abc", cancellationToken: cancelled.Token).Read());
    }

    [Fact]
    public void EmbeddedNamedReferencesMatchPinnedOfficialDigest()
    {
        using var resource = typeof(HtmlTokenizer).Assembly.GetManifestResourceStream("VisualWeb.Engine.Html.Data.entities.json");
        Assert.NotNull(resource);
        Assert.Equal("D741D877AC77C4194C4AD526B5B4A19AEF8DFE411AB840A466891CDBB9F362E6",
            Convert.ToHexString(SHA256.HashData(resource)));
    }

    [Theory]
    [InlineData("x1", "</x1>")]
    [InlineData("x\u00C4", "</x\u00E4>")]
    public void ContextualEndTagNamesUseAsciiLettersOnly(string lastTag, string input)
    {
        var tokenizer = new HtmlTokenizer(input, HtmlTextMode.Rawtext, lastTag,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(input, Assert.IsType<HtmlCharacters>(tokenizer.Read()).Data);
        Assert.IsType<HtmlEndOfFile>(tokenizer.Read());
    }
}
