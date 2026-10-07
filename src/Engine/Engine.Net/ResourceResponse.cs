using System.Collections.ObjectModel;
using VisualWeb.Core.Encoding;
using VisualWeb.Core.Mime;
using VisualWeb.Core.Url;

namespace VisualWeb.Engine.Net;

/// <summary>A buffered, unfiltered response for privileged resource loading.</summary>
/// <remarks>Spec: fetch; <see href="https://fetch.spec.whatwg.org/#concept-response">response</see>.
/// This is not a script-visible Fetch response and does not enforce CORS.</remarks>
public sealed class ResourceResponse
{
    public BrowserUrl Url { get; }
    public int StatusCode { get; }
    public bool Redirected { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
    public MimeType? ContentType { get; }
    public ReadOnlyMemory<byte> Body { get; }
    public IReadOnlyList<string> Diagnostics { get; }

    internal ResourceResponse(BrowserUrl url, int statusCode, bool redirected,
        Dictionary<string, IReadOnlyList<string>> headers, MimeType? contentType, byte[] body, List<string> diagnostics)
    {
        Url = url;
        StatusCode = statusCode;
        Redirected = redirected;
        Headers = new ReadOnlyDictionary<string, IReadOnlyList<string>>(headers);
        ContentType = contentType;
        Body = body;
        Diagnostics = diagnostics.AsReadOnly();
    }

    /// <summary>Decodes with BOM precedence and an explicit caller-selected fallback.</summary>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#decode">decode</see>.
    /// This does not choose an HTML charset or perform MIME sniffing.</remarks>
    public DecodedText DecodeText(WebEncoding fallback, bool fatal = false) =>
        WebEncoding.DecodeWithBom(Body.Span, fallback, fatal);
}

/// <summary>Implementation limits, not web-standard limits except the redirect default.</summary>
/// <remarks>Spec: fetch; <see href="https://fetch.spec.whatwg.org/#http-redirect-fetch">HTTP-redirect fetch</see>.</remarks>
public sealed record ResourceLoaderOptions
{
    public int MaxRedirects { get; init; } = 20;
    public int MaxResponseBytes { get; init; } = 32 * 1024 * 1024;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRedirects);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxResponseBytes);
        if (Timeout <= TimeSpan.Zero || Timeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }
    }
}

public enum ResourceError
{
    UnsupportedScheme,
    UrlCredentials,
    InvalidDataUrl,
    RemoteFile,
    FileAccess,
    InvalidRedirect,
    UnsupportedRedirect,
    RedirectLimit,
    BodyLimit,
    Network,
    Timeout,

    /// <summary>A restricted same-origin load was denied by origin or scheme policy before the request was sent.</summary>
    SameOriginDenied,

    /// <summary>An HTTP(S) URL targeted a Fetch <see href="https://fetch.spec.whatwg.org/#bad-port">bad port</see>;
    /// the request was blocked before transport or cookie work.</summary>
    BlockedPort,

    /// <summary>The finite session HSTS store is full; no active protection was evicted.</summary>
    HstsCapacity,

    /// <summary>A caller's secure-transport policy denied an HTTP load or HTTPS-to-HTTP redirect before sending it.</summary>
    InsecureTransport
}

public sealed class ResourceLoadException(ResourceError error, string message, Exception? innerException = null)
    : IOException(message, innerException)
{
    public ResourceError Error { get; } = error;
}
