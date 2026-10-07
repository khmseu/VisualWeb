using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.Ipc.Transport;

public sealed record RendererPacket(RendererMessage Message, byte[] Pixels);

/// <summary>Length-prefixed JSON metadata and raw pixels over private inherited streams.</summary>
/// <remarks>One reader and one writer per channel; caller serializes exchanges.
/// Prefix is magic VWR1, little-endian metadata length and payload length.
/// Streams are borrowed. A canceled/failed exchange invalidates the channel.</remarks>
/// <remarks>Reference: dotnet-stream-read;
/// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.io.stream.readexactlyasync">exact stream reads</see>.</remarks>
public sealed class RendererChannel(Stream input, Stream output)
{
    private const uint Magic = 0x31525756;
    private static readonly JsonSerializerOptions Json = new()
    {
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public async Task WriteAsync(RendererMessage message, ReadOnlyMemory<byte> pixels = default,
        CancellationToken cancellationToken = default)
    {
        RendererProtocol.Validate(message, pixels.Length);
        var header = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        if (header.Length > RendererProtocol.MaxHeaderBytes) { throw new IpcProtocolException("Renderer metadata byte limit exceeded."); }
        var prefix = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(4), header.Length);
        BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(8), pixels.Length);
        await output.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (!pixels.IsEmpty) { await output.WriteAsync(pixels, cancellationToken).ConfigureAwait(false); }
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<RendererPacket?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var prefix = new byte[12];
        var first = await input.ReadAsync(prefix.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        if (first == 0) { return null; }
        try
        {
            await input.ReadExactlyAsync(prefix.AsMemory(1), cancellationToken).ConfigureAwait(false);
            var headerLength = BinaryPrimitives.ReadInt32LittleEndian(prefix.AsSpan(4));
            var pixelLength = BinaryPrimitives.ReadInt32LittleEndian(prefix.AsSpan(8));
            if (BinaryPrimitives.ReadUInt32LittleEndian(prefix) != Magic || headerLength <= 0
                || headerLength > RendererProtocol.MaxHeaderBytes || pixelLength < 0 || pixelLength > RendererProtocol.MaxPayloadBytes)
            {
                throw new IpcProtocolException("Invalid renderer framing or byte limits.");
            }
            var header = new byte[headerLength];
            await input.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
            ValidateFields(header);
            var message = JsonSerializer.Deserialize<RendererMessage>(header, Json)
                ?? throw new IpcProtocolException("Null renderer metadata.");
            RendererProtocol.Validate(message, pixelLength);
            var pixels = new byte[pixelLength];
            if (pixelLength > 0)
            {
                await input.ReadExactlyAsync(pixels, cancellationToken).ConfigureAwait(false);
                for (var i = 3; i < pixels.Length; i += 4)
                {
                    if (pixels[i] != 255) { throw new IpcProtocolException("Renderer frame is not opaque BGRA."); }
                    if ((i & 0xffff) == 3) { cancellationToken.ThrowIfCancellationRequested(); }
                }
            }
            return new(message, pixels);
        }
        catch (EndOfStreamException exception) { throw new IpcProtocolException("Truncated renderer message: " + exception.Message); }
        catch (JsonException exception) { throw new IpcProtocolException("Invalid renderer JSON: " + exception.Message); }
    }
    private static void ValidateFields(ReadOnlySpan<byte> header)
    {
        var reader = new Utf8JsonReader(header, new JsonReaderOptions { MaxDepth = 16 });
        var fields = new HashSet<string>(StringComparer.Ordinal);
        var objects = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            { objects.Push(reader.CurrentDepth == 0 ? fields : new(StringComparer.Ordinal)); }
            else if (reader.TokenType == JsonTokenType.EndObject) { objects.Pop(); }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (!objects.Peek().Add(reader.GetString()!)) { throw new IpcProtocolException("Duplicate renderer metadata field."); }
            }
        }
        if (!fields.Contains("Version") || !fields.Contains("Kind"))
        {
            throw new IpcProtocolException("Explicit renderer version and kind are required.");
        }
    }
}
