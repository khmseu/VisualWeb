using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisualWeb.Core.Url;

namespace VisualWeb.Ipc.Contracts;

public sealed class IpcProtocolException(string message) : IOException(message);

/// <summary>Data-only visible CSS viewport rectangle and serialized absolute destination.</summary>
public sealed record PageLinkTarget(
    [property: JsonRequired] double X,
    [property: JsonRequired] double Y,
    [property: JsonRequired] double Width,
    [property: JsonRequired] double Height,
    [property: JsonRequired] string Url)
{
    public bool Contains(double x, double y) =>
        x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>Browser-fetched, decoded linked stylesheet text keyed by its serialized absolute request URL.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link type
/// "stylesheet"</see>. Data only: no response headers, final redirect URL, DOM node or fetch capability crosses IPC.</remarks>
public sealed record PageStylesheet(
    [property: JsonRequired] string Url,
    [property: JsonRequired] string Css);

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
    public double ScrollY { get; init; }
    public double ScrollHeight { get; init; }
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public int Stride { get; init; }
    public string? Title { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }
    public string? SandboxProfile { get; init; }
    public Guid DocumentId { get; init; }
    public Guid CommittedDocumentId { get; init; }
    public bool ExecuteInlineScripts { get; init; }
    public bool ReuseDocument { get; init; }
    public PageLinkTarget[]? LinkTargets { get; init; }
    public PageStylesheet[]? Stylesheets { get; init; }
}

public static class RendererProtocol
{
    public const int Version = 6;
    public const double MaxScrollHeight = 10_000_000;
    public const int MaxHeaderBytes = 32 * 1024 * 1024;
    public const int MaxPixels = 4_194_304;
    public const int MaxPayloadBytes = MaxPixels * 4;
    public const int MaxHtmlCharacters = 4 * 1024 * 1024;
    public const int MaxTextCharacters = 8192;
    public const int MaxLinkTargets = 4096;
    public const int MaxLinkMetadataBytes = 1024 * 1024;
    public const int MaxStylesheets = 32;
    public const int MaxStylesheetCharacters = 256 * 1024;
    /// <summary>Budget for the UTF-8 JSON serialization of the whole linked stylesheet collection.</summary>
    public const int MaxStylesheetBytes = 1024 * 1024;

    public static void Validate(RendererMessage message, int payloadLength)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Version != Version) { throw new IpcProtocolException("Unsupported renderer protocol version."); }
        if (message.Kind != "frame" && message.LinkTargets is not null)
        { throw new IpcProtocolException("Link targets belong only to frame replies."); }
        if (message.Kind != "render" && message.Stylesheets is not null)
        { throw new IpcProtocolException("Linked stylesheets belong only to render requests."); }
        if ((message.Kind != "render" && message.ScrollY != 0) || (message.Kind != "frame" && message.ScrollHeight != 0))
        { throw new IpcProtocolException("Scroll fields belong only to render requests and frame replies respectively."); }
        if (message.Kind != "render" && (message.DocumentId != Guid.Empty || message.CommittedDocumentId != Guid.Empty
            || message.ExecuteInlineScripts || message.ReuseDocument))
        { throw new IpcProtocolException("Document policy fields belong only to render requests."); }
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
                if (payloadLength != 0 || message.DocumentId == Guid.Empty || message.Html is null || message.Html.Length > MaxHtmlCharacters
                    || string.IsNullOrEmpty(message.Url) || message.Url.Length > MaxTextCharacters
                    || message.StatusCode is < 100 or > 599 || message.Diagnostics is null || message.Diagnostics.Length > 64
                    || message.Diagnostics.Any(d => d is null || d.Length > MaxTextCharacters))
                {
                    throw new IpcProtocolException("Invalid renderer request fields or limits.");
                }
                ValidateStylesheets(message.Stylesheets);
                Dimensions(message.Width, message.Height, message.Scale);
                if (!double.IsFinite(message.ScrollY) || message.ScrollY < 0 || message.ScrollY > 1e9)
                { throw new IpcProtocolException("Invalid renderer scroll offset."); }
                break;
            case "frame":
                var dimensions = Dimensions(message.Width, message.Height, message.Scale);
                if (message.PixelWidth <= 0 || message.PixelHeight <= 0
                    || message.PixelWidth != dimensions.Width || message.PixelHeight != dimensions.Height
                    || (long)message.PixelWidth * message.PixelHeight > MaxPixels
                    || (long)message.PixelWidth * 4 != message.Stride
                    || (long)message.Stride * message.PixelHeight != payloadLength
                    || message.Title is null || message.Title.Length > MaxTextCharacters
                    || message.Status is null || message.Status.Length > MaxTextCharacters)
                {
                    throw new IpcProtocolException("Invalid renderer frame dimensions, stride, text or bytes.");
                }
                if (!double.IsFinite(message.ScrollHeight) || message.ScrollHeight < 0 || message.ScrollHeight > MaxScrollHeight)
                { throw new IpcProtocolException("Invalid renderer scroll height."); }
                ValidateLinks(message.LinkTargets, message.Width, message.Height);
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
    public static void ValidateLinks(IReadOnlyList<PageLinkTarget>? links, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0
            || links is null || links.Count > MaxLinkTargets)
        { throw new IpcProtocolException("Invalid renderer link viewport or count."); }
        long urlBytes = 0;
        foreach (var link in links)
        {
            if (link is null || !double.IsFinite(link.X) || !double.IsFinite(link.Y)
                || !double.IsFinite(link.Width) || !double.IsFinite(link.Height)
                || link.X < 0 || link.Y < 0 || link.Width <= 0 || link.Height <= 0
                || link.X + link.Width > width || link.Y + link.Height > height
                || string.IsNullOrEmpty(link.Url) || link.Url.Length > MaxTextCharacters)
            { throw new IpcProtocolException("Invalid renderer link rectangle or URL limit."); }
            urlBytes += Encoding.UTF8.GetByteCount(link.Url);
            if (urlBytes > MaxLinkMetadataBytes)
            { throw new IpcProtocolException("Renderer link metadata byte limit exceeded."); }
            var parsed = BrowserUrl.ParseResult(link.Url);
            if (parsed.Url?.Href != link.Url)
            { throw new IpcProtocolException("Renderer link URL must be serialized and absolute."); }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(links).Length > MaxLinkMetadataBytes)
        { throw new IpcProtocolException("Renderer link metadata byte limit exceeded."); }
    }
    /// <summary>Checks count, per-sheet characters, unique serialized absolute URLs and the UTF-8 JSON wire budget.</summary>
    public static void ValidateStylesheets(IReadOnlyList<PageStylesheet>? stylesheets)
    {
        if (stylesheets is null || stylesheets.Count > MaxStylesheets)
        { throw new IpcProtocolException("Invalid linked stylesheet collection or count."); }
        var urls = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheet in stylesheets)
        {
            if (sheet is null || string.IsNullOrEmpty(sheet.Url) || sheet.Url.Length > MaxTextCharacters
                || sheet.Css is null || sheet.Css.Length > MaxStylesheetCharacters)
            { throw new IpcProtocolException("Invalid linked stylesheet URL or character limit."); }
            if (BrowserUrl.ParseResult(sheet.Url).Url?.Href != sheet.Url)
            { throw new IpcProtocolException("Linked stylesheet URL must be serialized and absolute."); }
            if (!urls.Add(sheet.Url)) { throw new IpcProtocolException("Linked stylesheet URLs must be unique."); }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(stylesheets).Length > MaxStylesheetBytes)
        { throw new IpcProtocolException("Linked stylesheet byte limit exceeded."); }
    }
    public static (int Width, int Height) Dimensions(double width, double height, double scale)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(scale)
            || width <= 0 || height <= 0 || scale <= 0 || width > 1e9 || height > MaxScrollHeight || scale > 1e9)
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
