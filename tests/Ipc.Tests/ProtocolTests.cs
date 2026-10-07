using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Ipc.Transport;
using Xunit;

namespace VisualWeb.Ipc.Tests;

public sealed class ProtocolTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e9 + 1)]
    public void InvalidScrollRequestOffsetsFail(double offset) =>
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { ScrollY = offset }, 0));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(10_000_001)]
    public void InvalidFrameScrollExtentsFail(double height) =>
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new()
        {
            Kind = "frame",
            Id = 1,
            Width = 1,
            Height = 1,
            Scale = 1,
            LinkTargets = [],
            Forms = [],
            FormControls = [],
            TextTargets = [],
            PixelWidth = 1,
            PixelHeight = 1,
            Stride = 4,
            Title = "",
            Status = "",
            ScrollHeight = height
        }, 4));

    [Fact]
    public void ScrollFieldsRoundTripAndRemainScopedToTheirMessageKinds()
    {
        Assert.Equal(11, RendererProtocol.Version);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Version = 10 }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", Version = 10 }, 0));
        RendererProtocol.Validate(Request with { ScrollY = 1e9 }, 0);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { ScrollHeight = 1 }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", ScrollY = 1 }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", ScrollHeight = 1 }, 0));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static RendererMessage LinkFrame => new()
    {
        Kind = "frame",
        Id = 1,
        Width = 2,
        Height = 2,
        Scale = 1,
        PixelWidth = 2,
        PixelHeight = 2,
        Stride = 8,
        Title = "",
        Status = "",
        ScrollHeight = 2,
        LinkTargets = [new(0, 0, 2, 2, "https://example.com/path#part")],
        Forms = [],
        FormControls = [],
        TextTargets = []
    };
    private static RendererMessage FormFrame => LinkFrame with
    {
        Forms = [new("https://example.com/search?old#frag", null), new("", "Unsupported form method: post.")],
        FormControls =
        [
            new(0, "text", "q", "café", "", false, false, true, 10, 0, new(0, 0, 2, 1)),
            new(0, "hidden", "h", "v", "", false, false, false, -1, 0, null),
            new(0, "submit", "go", "", "Submit", false, false, false, -1, 1, new(0, 1, 2, 1)),
            new(1, "button", "", "", "", true, false, false, -1, 1, null),
            new(-1, "search", "", "", "", false, true, false, -1, 1, null)
        ]
    };

    [Fact]
    public void FormMetadataIsRequiredOnFramesOnlyAndRoundTrips()
    {
        RendererProtocol.Validate(FormFrame, 16);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(FormFrame with { TextTargets = null }, 16));
        RendererProtocol.Validate(FormFrame with
        {
            FormControls = [FormFrame.FormControls![3] with { Label = "Search", Rect = new(0, 1, 2, 1) }]
        }, 16);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(FormFrame with { Forms = null }, 16));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(FormFrame with { FormControls = null }, 16));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Forms = [] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { FormControls = [] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { TextTargets = [] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", Forms = [] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", TextTargets = [] }, 0));
        var json = JsonSerializer.Serialize(FormFrame);
        var parsed = JsonSerializer.Deserialize<RendererMessage>(json)!;
        Assert.Equal(FormFrame.FormControls!, parsed.FormControls!);
        Assert.Equal(FormFrame.Forms!, parsed.Forms!);
        Assert.Equal(FormFrame.TextTargets!, parsed.TextTargets!);
    }

    public static TheoryData<int> InvalidFormCases() => new(Enumerable.Range(0, 21));

    [Theory]
    [MemberData(nameof(InvalidFormCases))]
    public void InvalidFormMetadataFails(int index)
    {
        var form = FormFrame.Forms![0];
        var control = FormFrame.FormControls![0];
        var frame = index switch
        {
            0 => FormFrame with { Forms = [form with { Action = "../relative" }] },
            1 => FormFrame with { Forms = [form with { Action = "javascript:alert(1)" }] },
            2 => FormFrame with { Forms = [form with { Action = "ftp://example.com/" }] },
            3 => FormFrame with { Forms = [form with { Action = "" }] },
            4 => FormFrame with { Forms = [form with { Action = "https://example.com/", Error = "" }] },
            5 => FormFrame with { Forms = [form with { Error = new string('e', 8193), Action = "" }] },
            6 => FormFrame with { Forms = Enumerable.Repeat(form, 257).ToArray() },
            7 => FormFrame with { FormControls = Enumerable.Repeat(control with { Rect = null }, 1025).ToArray() },
            8 => FormFrame with { FormControls = [control with { Form = 2 }] },
            9 => FormFrame with { FormControls = [control with { Form = -2 }] },
            10 => FormFrame with { FormControls = [control with { Kind = "password" }] },
            11 => FormFrame with { FormControls = [control with { Name = new string('n', 8193) }] },
            12 => FormFrame with { FormControls = [control with { Value = new string('v', 8193) }] },
            13 => FormFrame with { FormControls = [control with { Label = "x" }] },
            14 => FormFrame with { FormControls = [control with { MaxLength = -2 }] },
            15 => FormFrame with { FormControls = [control with { BeforeLink = 2 }] },
            16 => FormFrame with { FormControls = [control with { Rect = new(0, 0, 3, 1) }] },
            17 => FormFrame with { FormControls = [FormFrame.FormControls[1] with { Rect = new(0, 0, 1, 1) }] },
            18 => FormFrame with { FormControls = [control with { BeforeLink = 1 }, control with { BeforeLink = 0 }] },
            19 => FormFrame with { FormControls = [control with { Name = null! }] },
            _ => FormFrame with { FormControls = [null!] },
        };
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(frame, 16));
    }

    [Fact]
    public void TextTargetsAreBoundedAndViewportContained()
    {
        RendererProtocol.ValidateTextTargets([new("word", new(0, 0, 2, 2))], 2, 2);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateTextTargets(null, 2, 2));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateTextTargets([new("", new(0, 0, 1, 1))], 2, 2));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateTextTargets([new("word", new(double.NaN, 0, 1, 1))], 2, 2));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateTextTargets([new("word", new(0, 0, 3, 1))], 2, 2));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateTextTargets(
            Enumerable.Repeat(new PageTextTarget("x", new(0, 0, 1, 1)), RendererProtocol.MaxTextTargets + 1).ToArray(), 2, 2));
    }
    [Fact]
    public void ExactFormCountsAndStringLimitsAreAccepted()
    {
        var form = FormFrame.Forms![0];
        var control = FormFrame.FormControls![0] with { Rect = null, Name = new string('n', 8192), Value = "" };
        RendererProtocol.Validate(FormFrame with { Forms = Enumerable.Repeat(form, 256).ToArray() }, 16);
        RendererProtocol.Validate(FormFrame with { FormControls = Enumerable.Repeat(control, 100).ToArray() }, 16);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(FormFrame with
        { FormControls = Enumerable.Repeat(control with { Value = new string('v', 8192) }, 70).ToArray() }, 16));
        RendererProtocol.Validate(FormFrame with
        { FormControls = Enumerable.Repeat(FormFrame.FormControls[1] with { Name = "" }, 1024).ToArray() }, 16);
    }

    [Theory]
    [InlineData("\"Action\":\"https://example.com/\",\"Error\":null,\"Method\":\"post\"")]
    [InlineData("\"Error\":null")]
    public async Task FormObjectsRejectUnknownAndMissingFields(string fields)
    {
        var json = JsonSerializer.Serialize(FormFrame);
        var start = json.IndexOf("\"Forms\":", StringComparison.Ordinal);
        var end = json.IndexOf("\"FormControls\":", StringComparison.Ordinal);
        json = json[..start] + "\"Forms\":[{" + fields + "}]," + json[end..];
        using var wire = Wire(json, new byte[16]);
        await Assert.ThrowsAsync<IpcProtocolException>(() => new RendererChannel(wire, Stream.Null).ReadAsync(Cancellation));
    }

    [Fact]
    public void LinkedStylesheetsAreRequiredOnRenderRequestsOnly()
    {
        RendererProtocol.Validate(Request, 0);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Stylesheets = null }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", Stylesheets = [] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with { Stylesheets = [] }, 16));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(
            new() { Kind = "error", Id = 1, Error = "x", Stylesheets = [] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Version = 5 }, 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a.css")]
    [InlineData("HTTPS://EXAMPLE.COM/a.css")]
    [InlineData("https://example.com/a b.css")]
    public void LinkedStylesheetUrlsMustBeSerializedAbsoluteUrls(string url) =>
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Stylesheets = [new(url, "")] }, 0));

    [Fact]
    public void LinkedStylesheetCountCharacterUrlAndUtf8WireLimitsAreExact()
    {
        Assert.Equal(32, RendererProtocol.MaxStylesheets);
        Assert.Equal(256 * 1024, RendererProtocol.MaxStylesheetCharacters);
        Assert.Equal(1024 * 1024, RendererProtocol.MaxStylesheetBytes);
        static PageStylesheet Sheet(int index, string css = "") => new($"https://example.com/{index}.css", css);
        RendererProtocol.Validate(Request with { Stylesheets = Enumerable.Range(0, 32).Select(i => Sheet(i)).ToArray() }, 0);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(
            Request with { Stylesheets = Enumerable.Range(0, 33).Select(i => Sheet(i)).ToArray() }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Stylesheets = [Sheet(0), Sheet(0)] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Stylesheets = [Sheet(0, null!)] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Stylesheets = [null!] }, 0));
        RendererProtocol.Validate(Request with { Stylesheets = [Sheet(0, new string('a', 256 * 1024))] }, 0);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(
            Request with { Stylesheets = [Sheet(0, new string('a', 256 * 1024 + 1))] }, 0));
        RendererProtocol.Validate(Request with { Stylesheets = [new("data:," + new string('a', 8186), "")] }, 0);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(
            Request with { Stylesheets = [new("data:," + new string('a', 8187), "")] }, 0));

        var sheets = Enumerable.Range(0, 3).Select(i => Sheet(i, new string('a', 256 * 1024))).ToList();
        sheets.Add(Sheet(3));
        var remaining = RendererProtocol.MaxStylesheetBytes - JsonSerializer.SerializeToUtf8Bytes(sheets).Length;
        Assert.InRange(remaining, 1, 256 * 1024);
        sheets[^1] = Sheet(3, new string('a', remaining));
        Assert.Equal(RendererProtocol.MaxStylesheetBytes, JsonSerializer.SerializeToUtf8Bytes(sheets).Length);
        RendererProtocol.Validate(Request with { Stylesheets = sheets.ToArray() }, 0);
        sheets[^1] = Sheet(3, new string('a', remaining + 1));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Stylesheets = sheets.ToArray() }, 0));
    }

    [Fact]
    public void EscapedCssCharactersCountTowardTheStylesheetWireBudget()
    {
        var sheets = Enumerable.Range(0, 4).Select(i => new PageStylesheet($"https://example.com/{i}.css",
            new string('<', 50_000))).ToArray();
        Assert.True(sheets.Sum(sheet => Encoding.UTF8.GetByteCount(sheet.Css)) < RendererProtocol.MaxStylesheetBytes);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(sheets).Length > RendererProtocol.MaxStylesheetBytes);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Stylesheets = sheets }, 0));
    }

    [Fact]
    public void LinkMetadataIsBoundedAndBelongsOnlyToFrames()
    {
        RendererProtocol.Validate(LinkFrame, 16);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { LinkTargets = LinkFrame.LinkTargets }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", LinkTargets = [] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with { LinkTargets = null }, 16));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with
        { LinkTargets = Enumerable.Repeat(LinkFrame.LinkTargets![0], 4097).ToArray() }, 16));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with
        { LinkTargets = [new(0, 0, 1, 1, "https://example.com/" + new string('a', 8192))] }, 16));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with
        { LinkTargets = Enumerable.Repeat(new PageLinkTarget(0, 0, 1, 1, "data:," + new string('a', 8190 - 6)), 200).ToArray() }, 16));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with
        { LinkTargets = [new(0, 0, 1, 1, "../relative")] }, 16));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with { Version = 4 }, 16));
    }

    [Fact]
    public void GroupedLinkRectsAreRequiredBoundedAndDataOnly()
    {
        Assert.Equal(11, RendererProtocol.Version);
        Assert.Equal(64, RendererProtocol.MaxLinkRects);
        var link = LinkFrame.LinkTargets![0];
        var rect = link.Rects[0];
        RendererProtocol.Validate(LinkFrame with
        { LinkTargets = [link with { Rects = Enumerable.Repeat(rect, 64).ToArray() }] }, 16);
        foreach (var rects in new IReadOnlyList<PageLinkRect>[] { [], Enumerable.Repeat(rect, 65).ToArray(), [rect with { X = -1 }] })
        {
            Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with
            { LinkTargets = [link with { Rects = rects }] }, 16));
        }
        var json = JsonSerializer.Serialize(link);
        Assert.Contains("\"Rects\"", json);
        Assert.DoesNotContain("\"X\":0,\"Y\":0,\"Width\":2,\"Height\":2,\"Url\"", json);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PageLinkTarget>("{\"Url\":\"https://example.com/\"}"));
    }

    [Theory]
    [InlineData(double.NaN, 0, 1, 1)]
    [InlineData(0, double.PositiveInfinity, 1, 1)]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, 0, -1, 1)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(1, 0, 2, 1)]
    [InlineData(0, 1, 1, 2)]
    public void InvalidLinkRectanglesFail(double x, double y, double width, double height) =>
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with
        { LinkTargets = [new(x, y, width, height, "https://example.com/")] }, 16));

    [Fact]
    public void ExactLinkCountUrlAndUtf8WireLimitsAreAccepted()
    {
        var link = LinkFrame.LinkTargets![0];
        RendererProtocol.Validate(LinkFrame with { LinkTargets = Enumerable.Repeat(link, 4096).ToArray() }, 16);
        RendererProtocol.Validate(LinkFrame with { LinkTargets = [link with { Url = "data:," + new string('a', 8186) }] }, 16);
        var links = Enumerable.Repeat(link with { Url = "data:," + new string('a', 8186) }, 126).ToList();
        links.Add(link with { Url = "data:," });
        var remaining = RendererProtocol.MaxLinkMetadataBytes - JsonSerializer.SerializeToUtf8Bytes(links).Length;
        while (remaining > 8186)
        {
            links.Add(link with { Url = "data:," });
            remaining = RendererProtocol.MaxLinkMetadataBytes - JsonSerializer.SerializeToUtf8Bytes(links).Length;
        }
        links[^1] = links[^1] with { Url = "data:," + new string('a', remaining) };
        Assert.Equal(RendererProtocol.MaxLinkMetadataBytes, JsonSerializer.SerializeToUtf8Bytes(links).Length);
        RendererProtocol.Validate(LinkFrame with { LinkTargets = links.ToArray() }, 16);
        links[^1] = links[^1] with { Url = links[^1].Url + "a" };
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with { LinkTargets = links.ToArray() }, 16));
    }

    [Fact]
    public void EscapedUnicodeBytesCountTowardTheLinkWireBudget()
    {
        var url = "data:," + new string('\u00e9', 2000);
        var links = Enumerable.Repeat(new PageLinkTarget(0, 0, 1, 1, url), 100).ToArray();
        Assert.True(Encoding.UTF8.GetByteCount(url) * links.Length < RendererProtocol.MaxLinkMetadataBytes);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(links).Length > RendererProtocol.MaxLinkMetadataBytes);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(LinkFrame with { LinkTargets = links }, 16));
    }

    [Theory]
    [InlineData("\"Rects\":[{\"X\":0,\"Y\":0,\"Width\":1,\"Height\":1,\"X\":1}],\"Url\":\"https://example.com/\"")]
    [InlineData("\"Rects\":[{\"X\":0,\"Y\":0,\"Height\":1}],\"Url\":\"https://example.com/\"")]
    [InlineData("\"Rects\":[{\"X\":0,\"Y\":0,\"Width\":1,\"Height\":1,\"NativeHandle\":1}],\"Url\":\"https://example.com/\"")]
    [InlineData("\"Rects\":[],\"Url\":\"https://example.com/\"")]
    [InlineData("\"Rects\":null,\"Url\":\"https://example.com/\"")]
    [InlineData("\"Rects\":[null],\"Url\":\"https://example.com/\"")]
    [InlineData("\"Rects\":[{\"X\":0,\"Y\":0,\"Width\":1,\"Height\":1}],\"Url\":\"https://example.com/\",\"Url\":\"https://example.com/\"")]
    [InlineData("\"Rects\":[{\"X\":0,\"Y\":0,\"Width\":1,\"Height\":1}],\"Url\":\"https://example.com/\",\"ElementId\":1")]
    public async Task LinkObjectsRejectDuplicateMissingAndUnknownFields(string fields)
    {
        var json = JsonSerializer.Serialize(LinkFrame);
        var start = json.IndexOf("\"LinkTargets\":", StringComparison.Ordinal);
        json = json[..start] + "\"LinkTargets\":[{" + fields + "}]}";
        using var wire = Wire(json, new byte[16]);
        await Assert.ThrowsAsync<IpcProtocolException>(() => new RendererChannel(wire, Stream.Null).ReadAsync(Cancellation));
    }
    private static RendererMessage Request => new()
    {
        Kind = "render",
        DocumentId = Guid.Parse("70e33750-276f-48cb-9a3c-b5e4b51ecc49"),
        Id = 1,
        Url = "https://example.com",
        Html = "<!doctype html><p>café</p>",
        StatusCode = 200,
        Diagnostics = ["UTF-8"],
        Stylesheets = [],
        Width = 2,
        Height = 2,
        Scale = 1
    };
    [Fact]
    public async Task MessagesAndRawPixelsRoundTripWithoutUnicodeOrBgraLoss()
    {
        using var wire = new MemoryStream();
        var writer = new RendererChannel(Stream.Null, wire);
        await writer.WriteAsync(new() { Kind = "hello", SandboxProfile = "linux-bwrap-seccomp-cgroup-v2" }, cancellationToken: Cancellation);
        var input = Request with
        {
            ExecuteInlineScripts = true,
            ReuseDocument = true,
            CommittedDocumentId = Request.DocumentId,
            ScrollY = 48,
            Stylesheets = [new("https://example.com/a.css", "p{content:\"café <&>\"}")]
        };
        await writer.WriteAsync(input, cancellationToken: Cancellation);
        var pixels = new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 };
        var reply = new RendererMessage
        {
            Kind = "frame",
            Id = 1,
            Width = 2,
            Height = 1,
            Scale = 1,
            LinkTargets = [],
            Forms = [],
            FormControls = [],
            TextTargets = [],
            PixelWidth = 2,
            PixelHeight = 1,
            Stride = 8,
            Title = "café",
            Status = "Ready",
            ScrollHeight = 100
        };
        await writer.WriteAsync(reply, pixels, Cancellation);
        await writer.WriteAsync(new() { Kind = "error", Id = 2, Error = "Unsupported page" }, cancellationToken: Cancellation);
        wire.Position = 0;
        var reader = new RendererChannel(wire, Stream.Null);
        var handshake = (await reader.ReadAsync(Cancellation))!.Message;
        Assert.Equal("hello", handshake.Kind);
        Assert.Equal("linux-bwrap-seccomp-cgroup-v2", handshake.SandboxProfile);
        var request = (await reader.ReadAsync(Cancellation))!.Message;
        Assert.Equal(Request.Html, request.Html); Assert.Equal(Request.Url, request.Url);
        Assert.Equal(Request.Diagnostics, request.Diagnostics);
        Assert.Equal(Request.DocumentId, request.DocumentId);
        Assert.Equal(input.CommittedDocumentId, request.CommittedDocumentId);
        Assert.True(request.ExecuteInlineScripts); Assert.True(request.ReuseDocument);
        Assert.Equal(input.ScrollY, request.ScrollY);
        Assert.Equal(input.Stylesheets, request.Stylesheets);
        var frame = (await reader.ReadAsync(Cancellation))!;
        Assert.Equal(pixels, frame.Pixels); Assert.Equal(reply.Title, frame.Message.Title);
        Assert.Equal(reply.ScrollHeight, frame.Message.ScrollHeight);
        Assert.Equal("error", (await reader.ReadAsync(Cancellation))!.Message.Kind);
        Assert.Null(await reader.ReadAsync(Cancellation));
    }
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(33 * 1024 * 1024, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 17 * 1024 * 1024)]
    public async Task InvalidLengthsAreRejectedBeforeReadingOrAllocatingBodies(int header, int pixels)
    {
        using var wire = new MemoryStream(Prefix(header, pixels));
        var channel = new RendererChannel(wire, Stream.Null);
        await Assert.ThrowsAsync<IpcProtocolException>(() => channel.ReadAsync(Cancellation));
        Assert.Equal(12, wire.Position);
    }
    [Theory]
    [InlineData("{\"Version\":1,\"Kind\":\"hello\"}")]
    [InlineData("{\"Version\":2,\"Kind\":\"hello\"}")]
    [InlineData("{\"Version\":3,\"Kind\":\"hello\"}")]
    [InlineData("{\"Version\":4,\"Kind\":\"unknown\",\"Id\":1}")]
    [InlineData("{\"Version\":4,\"Kind\":\"hello\",\"Unknown\":true}")]
    [InlineData("null")]
    [InlineData("{invalid")]
    [InlineData("{\"Kind\":\"hello\"}")]
    [InlineData("{\"Version\":4,\"Kind\":\"hello\",\"Kind\":\"hello\"}")]
    public async Task VersionKindsUnknownMembersAndMalformedJsonFail(string json)
    {
        using var wire = Wire(json, []);
        await Assert.ThrowsAsync<IpcProtocolException>(() => new RendererChannel(wire, Stream.Null).ReadAsync(Cancellation));
    }
    [Fact]
    public async Task FragmentedReadsAreReassembledAndTruncatedReadsFail()
    {
        using var wire = Wire(JsonSerializer.Serialize(Request), []);
        using var chunks = new Chunks(wire.ToArray());
        Assert.Equal(Request.Html, (await new RendererChannel(chunks, Stream.Null).ReadAsync(Cancellation))!.Message.Html);
        using var truncated = new MemoryStream(wire.ToArray()[..^1]);
        await Assert.ThrowsAsync<IpcProtocolException>(() => new RendererChannel(truncated, Stream.Null).ReadAsync(Cancellation));
        using var prefix = new MemoryStream([1, 2]);
        await Assert.ThrowsAsync<IpcProtocolException>(() => new RendererChannel(prefix, Stream.Null).ReadAsync(Cancellation));
    }
    [Fact]
    public async Task WrongMagicAndNonOpaquePixelsFail()
    {
        var prefix = Prefix(1, 0); prefix[0] = 0;
        using var wrong = new MemoryStream(prefix);
        await Assert.ThrowsAsync<IpcProtocolException>(() => new RendererChannel(wrong, Stream.Null).ReadAsync(Cancellation));
        var frame = new RendererMessage
        {
            Kind = "frame",
            Id = 1,
            Width = 1,
            Height = 1,
            Scale = 1,
            LinkTargets = [],
            Forms = [],
            FormControls = [],
            TextTargets = [],
            PixelWidth = 1,
            PixelHeight = 1,
            Stride = 4,
            Title = "",
            Status = ""
        };
        using var transparent = Wire(JsonSerializer.Serialize(frame), [0, 0, 0, 0]);
        await Assert.ThrowsAsync<IpcProtocolException>(() => new RendererChannel(transparent, Stream.Null).ReadAsync(Cancellation));
    }
    [Fact]
    public void ExactViewportAndMessageBudgetsAreValidated()
    {
        Assert.Equal((2048, 2048), RendererProtocol.Dimensions(2048, 2048, 1));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Dimensions(2049, 2048, 1));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Dimensions(double.NaN, 1, 1));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Dimensions(1, 1, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Html = new('x', RendererProtocol.MaxHtmlCharacters + 1) }, 0));
        RendererProtocol.Validate(Request with { Html = new('x', RendererProtocol.MaxHtmlCharacters) }, 0);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Diagnostics = [null!] }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { Id = 0 }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { DocumentId = Guid.Empty }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", ExecuteInlineScripts = true }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", DocumentId = Request.DocumentId }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "error", Id = 1, Error = "fixture", ReuseDocument = true }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new()
        {
            Kind = "frame",
            Id = 1,
            PixelWidth = int.MaxValue,
            PixelHeight = int.MaxValue,
            Stride = int.MaxValue,
            Title = "",
            Status = ""
        }, 4));
    }
    [Fact]
    public async Task FrameByteCountAndStrideAreNotAllowedToPadOrUnderflow()
    {
        var frame = new RendererMessage { Kind = "frame", Id = 1, PixelWidth = 1, PixelHeight = 1, Stride = 8, Title = "", Status = "" };
        using var wire = Wire(JsonSerializer.Serialize(frame), [0, 0, 0, 255, 0, 0, 0, 255]);
        await Assert.ThrowsAsync<IpcProtocolException>(() => new RendererChannel(wire, Stream.Null).ReadAsync(Cancellation));
        Assert.True(wire.Position < wire.Length);
    }
    [Fact]
    public async Task CancellationInterruptsChannelReadsAndWrites()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        using var wire = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RendererChannel(wire, wire).ReadAsync(canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RendererChannel(wire, wire).WriteAsync(Request, cancellationToken: canceled.Token));
    }
    private static byte[] Prefix(int header, int pixels)
    {
        var prefix = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, 0x31525756);
        BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(4), header);
        BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(8), pixels);
        return prefix;
    }
    private static MemoryStream Wire(string json, byte[] pixels)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return new([.. Prefix(bytes.Length, pixels.Length), .. bytes, .. pixels]);
    }
    private sealed class Chunks(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
    }
}
