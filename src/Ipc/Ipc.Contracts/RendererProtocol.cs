namespace VisualWeb.Ipc.Contracts;

public sealed class IpcProtocolException(string message) : IOException(message);

/// <summary>Versioned data-only renderer messages. No DOM, native handles, paths or broker capabilities.</summary>
/// <remarks>Internal protocol; not a web interface. Each channel serves exactly one tab.</remarks>
public sealed record RendererMessage
{
    public int Version { get; init; } = RendererProtocol.Version;
    public string Kind { get; init; } = "";
    public long Id { get; init; }
    public string? Url { get; init; }
    public string? Html { get; init; }
    public int StatusCode { get; init; }
    public string[]? Diagnostics { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Scale { get; init; }
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public int Stride { get; init; }
    public string? Title { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }
    public string? SandboxProfile { get; init; }
}

public static class RendererProtocol
{
    public const int Version = 2;
    public const int MaxHeaderBytes = 32 * 1024 * 1024;
    public const int MaxPixels = 4_194_304;
    public const int MaxPayloadBytes = MaxPixels * 4;
    public const int MaxHtmlCharacters = 4 * 1024 * 1024;
    public const int MaxTextCharacters = 8192;

    public static void Validate(RendererMessage message, int payloadLength)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Version != Version) { throw new IpcProtocolException("Unsupported renderer protocol version."); }
        if (message.Kind == "hello")
        {
            if (message.Id != 0 || payloadLength != 0 || message.SandboxProfile is { Length: > MaxTextCharacters })
            {
                throw new IpcProtocolException("Invalid renderer handshake.");
            }
            return;
        }
        if (message.Id <= 0) { throw new IpcProtocolException("Renderer request identity must be positive."); }
        switch (message.Kind)
        {
            case "render":
                if (payloadLength != 0 || message.Html is null || message.Html.Length > MaxHtmlCharacters
                    || string.IsNullOrEmpty(message.Url) || message.Url.Length > MaxTextCharacters
                    || message.StatusCode is < 100 or > 599 || message.Diagnostics is null || message.Diagnostics.Length > 64
                    || message.Diagnostics.Any(d => d is null || d.Length > MaxTextCharacters))
                {
                    throw new IpcProtocolException("Invalid renderer request fields or limits.");
                }
                Dimensions(message.Width, message.Height, message.Scale);
                break;
            case "frame":
                if (message.PixelWidth <= 0 || message.PixelHeight <= 0
                    || (long)message.PixelWidth * message.PixelHeight > MaxPixels
                    || (long)message.PixelWidth * 4 != message.Stride
                    || (long)message.Stride * message.PixelHeight != payloadLength
                    || message.Title is null || message.Title.Length > MaxTextCharacters
                    || message.Status is null || message.Status.Length > MaxTextCharacters)
                {
                    throw new IpcProtocolException("Invalid renderer frame dimensions, stride, text or bytes.");
                }
                break;
            case "error":
                if (payloadLength != 0 || string.IsNullOrEmpty(message.Error) || message.Error.Length > MaxTextCharacters)
                {
                    throw new IpcProtocolException("Invalid renderer failure reply.");
                }
                break;
            default: throw new IpcProtocolException("Unknown renderer message kind.");
        }
    }
    public static (int Width, int Height) Dimensions(double width, double height, double scale)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(scale)
            || width <= 0 || height <= 0 || scale <= 0 || width > 1e9 || height > 1e9 || scale > 1e9)
        {
            throw new IpcProtocolException("Invalid renderer viewport.");
        }
        var physicalWidth = Math.Ceiling(width * scale);
        var physicalHeight = Math.Ceiling(height * scale);
        if (physicalWidth < 1 || physicalHeight < 1 || physicalWidth > int.MaxValue || physicalHeight > int.MaxValue
            || physicalWidth * physicalHeight > MaxPixels)
        {
            throw new IpcProtocolException("Renderer viewport exceeds the framebuffer limit.");
        }
        return ((int)physicalWidth, (int)physicalHeight);
    }
}
