using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisualWeb.Core.Url;

namespace VisualWeb.Ipc.Contracts;

public sealed class IpcProtocolException(string message) : IOException(message);

/// <summary>Data-only clipped visible CSS viewport rectangle.</summary>
public sealed record PageLinkRect(
    [property: JsonRequired] double X,
    [property: JsonRequired] double Y,
    [property: JsonRequired] double Width,
    [property: JsonRequired] double Height)
{
    public bool Contains(double x, double y) =>
        x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>One textual anchor, with ordered visible rectangles and an absolute destination; no element identity.</summary>
[method: JsonConstructor]
public sealed record PageLinkTarget(
    [property: JsonRequired] IReadOnlyList<PageLinkRect> Rects,
    [property: JsonRequired] string Url)
{
    public PageLinkTarget(double x, double y, double width, double height, string url)
        : this(new[] { new PageLinkRect(x, y, width, height) }, url) { }

    // Compatibility accessors expose the first rectangle only; Rects is the wire and focus geometry.
    [JsonIgnore] public double X => Rects[0].X;
    [JsonIgnore] public double Y => Rects[0].Y;
    [JsonIgnore] public double Width => Rects[0].Width;
    [JsonIgnore] public double Height => Rects[0].Height;
    public bool Contains(double x, double y) => Rects.Any(rect => rect.Contains(x, y));
}

/// <summary>Browser-fetched, decoded linked stylesheet text keyed by its serialized absolute request URL.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link type
/// "stylesheet"</see>. Data only: no response headers, final redirect URL, DOM node or fetch capability crosses IPC.</remarks>
public sealed record PageStylesheet(
    [property: JsonRequired] string Url,
    [property: JsonRequired] string Css);

/// <summary>One bounded visible text fragment and its clipped viewport rectangle for browser-owned selection.</summary>
public sealed record PageTextTarget(
    [property: JsonRequired] string Text,
    [property: JsonRequired] PageLinkRect Rect);

/// <summary>One parsed form owner: an absolute GET action, or a visible unsupported-semantics diagnostic.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm">form
/// submission algorithm</see>. Only the bounded same-tab GET/urlencoded subset is representable; Error is set
/// (and Action empty) for anything else. No element identity or script capability crosses IPC.</remarks>
public sealed record PageForm(
    [property: JsonRequired] string Action,
    [property: JsonRequired] string? Error);

/// <summary>One tree-ordered supported form control with initial state and optional clipped visible border box.</summary>
/// <remarks>Kind is text, search, hidden, submit (input) or button (button type=submit). Form is the owner index or -1.
/// BeforeLink is the number of visible link targets preceding the control in tree order. Value is the initial
/// value; the browser shell owns user edits. Label carries submit-input text or button text.</remarks>
public sealed record PageFormControl(
    [property: JsonRequired] int Form,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Value,
    [property: JsonRequired] string Label,
    [property: JsonRequired] bool Disabled,
    [property: JsonRequired] bool ReadOnly,
    [property: JsonRequired] bool Required,
    [property: JsonRequired] int MaxLength,
    [property: JsonRequired] int BeforeLink,
    [property: JsonRequired] PageLinkRect? Rect);

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
    public PageForm[]? Forms { get; init; }
    public PageFormControl[]? FormControls { get; init; }
    public PageTextTarget[]? TextTargets { get; init; }
}

public static class RendererProtocol
{
    public const int Version = 9;
    public const double MaxScrollHeight = 10_000_000;
    public const int MaxHeaderBytes = 32 * 1024 * 1024;
    public const int MaxPixels = 4_194_304;
    public const int MaxPayloadBytes = MaxPixels * 4;
    public const int MaxHtmlCharacters = 4 * 1024 * 1024;
    public const int MaxTextCharacters = 8192;
    public const int MaxLinkTargets = 4096;
    public const int MaxLinkRects = 64;
    public const int MaxLinkMetadataBytes = 1024 * 1024;
    public const int MaxForms = 256;
    public const int MaxFormControls = 1024;
    /// <summary>Budget for the UTF-8 JSON serialization of forms plus controls.</summary>
    public const int MaxFormMetadataBytes = 1024 * 1024;
    public const int MaxTextTargets = 32_768;
    /// <summary>Budget for serialized text-selection target data, separate from frame pixels and form metadata.</summary>
    public const int MaxTextMetadataBytes = 1024 * 1024;
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
        if (message.Kind != "frame" && (message.Forms is not null || message.FormControls is not null))
        { throw new IpcProtocolException("Form metadata belongs only to frame replies."); }
        if (message.Kind != "frame" && message.TextTargets is not null)
        { throw new IpcProtocolException("Text targets belong only to frame replies."); }
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
                ValidateForms(message.Forms, message.FormControls, message.LinkTargets!.Length, message.Width, message.Height);
                ValidateTextTargets(message.TextTargets, message.Width, message.Height);
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
            if (link is null || link.Rects is null || link.Rects.Count is < 1 or > MaxLinkRects
                || string.IsNullOrEmpty(link.Url) || link.Url.Length > MaxTextCharacters)
            { throw new IpcProtocolException("Invalid renderer link rectangle count or URL limit."); }
            foreach (var rect in link.Rects)
            {
                if (rect is null || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
                    || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)
                    || rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
                    || rect.X + rect.Width > width || rect.Y + rect.Height > height)
                { throw new IpcProtocolException("Invalid renderer link rectangle."); }
            }
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
    /// <summary>Checks form/control counts, owner indexes, kinds, string limits, action URLs, geometry and the JSON budget.</summary>
    public static void ValidateForms(IReadOnlyList<PageForm>? forms, IReadOnlyList<PageFormControl>? controls,
        int linkCount, double width, double height)
    {
        if (forms is null || controls is null || forms.Count > MaxForms || controls.Count > MaxFormControls)
        { throw new IpcProtocolException("Invalid renderer form metadata collection or count."); }
        foreach (var form in forms)
        {
            if (form is null || form.Action is null || form.Action.Length > MaxTextCharacters
                || form.Error is { Length: 0 or > MaxTextCharacters })
            { throw new IpcProtocolException("Invalid renderer form fields or limits."); }
            if (form.Error is not null)
            {
                if (form.Action.Length != 0) { throw new IpcProtocolException("Unsupported renderer forms carry no action."); }
                continue;
            }
            var parsed = BrowserUrl.ParseResult(form.Action).Url;
            if (parsed?.Href != form.Action || parsed.Protocol is not ("http:" or "https:" or "file:" or "data:"))
            { throw new IpcProtocolException("Renderer form action must be a serialized absolute http, https, file or data URL."); }
        }
        var lastLink = 0;
        foreach (var control in controls)
        {
            if (control is null || control.Form < -1 || control.Form >= forms.Count
                || control.Kind is not ("text" or "search" or "hidden" or "submit" or "button")
                || control.Name is null || control.Name.Length > MaxTextCharacters
                || control.Value is null || control.Value.Length > MaxTextCharacters
                || control.Label is null || control.Label.Length > MaxTextCharacters
                || control.Kind is not ("submit" or "button") && control.Label.Length != 0
                || control.MaxLength < -1 || control.BeforeLink < 0 || control.BeforeLink > linkCount)
            { throw new IpcProtocolException("Invalid renderer form control fields or limits."); }
            if (control.Rect is not { } rect) { continue; }
            if (control.Kind == "hidden" || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
                || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)
                || rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
                || rect.X + rect.Width > width || rect.Y + rect.Height > height || control.BeforeLink < lastLink)
            { throw new IpcProtocolException("Invalid renderer form control rectangle or order."); }
            lastLink = control.BeforeLink;
        }
        if (JsonSerializer.SerializeToUtf8Bytes(forms).Length + (long)JsonSerializer.SerializeToUtf8Bytes(controls).Length
            > MaxFormMetadataBytes)
        { throw new IpcProtocolException("Renderer form metadata byte limit exceeded."); }
    }
    /// <summary>Checks the bounded visible text-selection snapshot and its viewport geometry.</summary>
    public static void ValidateTextTargets(IReadOnlyList<PageTextTarget>? targets, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0
            || targets is null || targets.Count > MaxTextTargets)
        { throw new IpcProtocolException("Invalid renderer text target viewport or count."); }
        long textBytes = 0;
        foreach (var target in targets)
        {
            var rect = target?.Rect;
            if (target is null || string.IsNullOrEmpty(target.Text) || target.Text.Length > MaxTextCharacters
                || rect is null || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
                || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)
                || rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
                || rect.X + rect.Width > width || rect.Y + rect.Height > height)
            { throw new IpcProtocolException("Invalid renderer text target fields or rectangle."); }
            textBytes += Encoding.UTF8.GetByteCount(target.Text);
            if (textBytes > MaxTextMetadataBytes)
            { throw new IpcProtocolException("Renderer text target byte limit exceeded."); }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(targets).Length > MaxTextMetadataBytes)
        { throw new IpcProtocolException("Renderer text target metadata byte limit exceeded."); }
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
