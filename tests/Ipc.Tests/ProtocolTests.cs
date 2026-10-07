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
        Assert.Equal(4, RendererProtocol.Version);
        RendererProtocol.Validate(Request with { ScrollY = 1e9 }, 0);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(Request with { ScrollHeight = 1 }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", ScrollY = 1 }, 0));
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.Validate(new() { Kind = "hello", ScrollHeight = 1 }, 0));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static RendererMessage Request => new()
    {
        Kind = "render",
        DocumentId = Guid.Parse("70e33750-276f-48cb-9a3c-b5e4b51ecc49"),
        Id = 1,
        Url = "https://example.com",
        Html = "<!doctype html><p>café</p>",
        StatusCode = 200,
        Diagnostics = ["UTF-8"],
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
        var input = Request with { ExecuteInlineScripts = true, ReuseDocument = true, CommittedDocumentId = Request.DocumentId, ScrollY = 48 };
        await writer.WriteAsync(input, cancellationToken: Cancellation);
        var pixels = new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 };
        var reply = new RendererMessage { Kind = "frame", Id = 1, PixelWidth = 2, PixelHeight = 1, Stride = 8, Title = "café", Status = "Ready", ScrollHeight = 100 };
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
        var frame = new RendererMessage { Kind = "frame", Id = 1, PixelWidth = 1, PixelHeight = 1, Stride = 4, Title = "", Status = "" };
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
