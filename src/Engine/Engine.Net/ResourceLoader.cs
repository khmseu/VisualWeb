using System.Net;
using VisualWeb.Core.Mime;
using VisualWeb.Core.Url;

namespace VisualWeb.Engine.Net;

/// <summary>A bounded GET-only loader for trusted browser-side callers.</summary>
/// <remarks>Spec: fetch; <see href="https://fetch.spec.whatwg.org/#scheme-fetch">scheme fetch</see>
/// and <see href="https://fetch.spec.whatwg.org/#http-redirect-fetch">HTTP redirects</see>.
/// HTTP(S) <see href="https://fetch.spec.whatwg.org/#block-bad-port">bad ports</see> are blocked for every request.
/// <see cref="LoadSameOriginAsync"/> is an opt-in same-origin restricted mode; CORS, other origin policy,
/// SameSite, caching and script-visible Fetch are not implemented.
/// Cookies require explicit opt-in and are isolated per loader.</remarks>
public sealed class ResourceLoader : IDisposable
{
    private readonly ResourceLoaderOptions options;
    private readonly HttpClient client;
    private readonly SessionCookies cookies = new();
    private bool disposed;

    /// <summary>Creates a loader which owns its transport handler and session cookie store.</summary>
    /// <remarks>An injected custom handler must not follow redirects, handle cookies,
    /// supply ambient credentials or buffer unbounded response bodies. Built-in HTTP
    /// handlers are configured accordingly before use. Transport URI conversion occurs
    /// only after WHATWG parsing and is not a replacement for Core.Url.</remarks>
    public ResourceLoader(ResourceLoaderOptions? options = null, HttpMessageHandler? handler = null)
    {
        this.options = options ?? new();
        this.options.Validate();
        handler ??= new SocketsHttpHandler();
        switch (handler)
        {
            case SocketsHttpHandler sockets:
                sockets.AllowAutoRedirect = false;
                sockets.UseCookies = false;
                sockets.Credentials = null;
                sockets.DefaultProxyCredentials = null;
                sockets.AutomaticDecompression = DecompressionMethods.All;
                break;
            case HttpClientHandler http:
                http.AllowAutoRedirect = false;
                http.UseCookies = false;
                http.Credentials = null;
                http.DefaultProxyCredentials = null;
                http.UseDefaultCredentials = false;
                http.AutomaticDecompression = DecompressionMethods.All;
                break;
        }

        client = new(handler, disposeHandler: true) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    /// <summary>Trusted, unrestricted developer/browser navigation: HTTP(S), local file and data URLs, with
    /// redirects to any HTTP(S) origin. Not an authorization boundary; see <see cref="LoadSameOriginAsync"/>.</summary>
    /// <remarks>Spec: fetch; HTTP(S) URLs and redirect targets on a
    /// <see href="https://fetch.spec.whatwg.org/#bad-port">bad port</see> throw <see cref="ResourceLoadException"/> with
    /// <see cref="ResourceError.BlockedPort"/> before any transport or cookie work.</remarks>
    public async Task<ResourceResponse> LoadAsync(BrowserUrl url, bool includeCookies = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(url);
        BadPortPolicy.ThrowIfBlocked(url);
        return await LoadCoreAsync(url, null, includeCookies, cancellationToken);
    }

    /// <summary>Opt-in restricted GET: loads only HTTP(S) URLs same origin with a fixed HTTP(S) tuple request origin.</summary>
    /// <remarks>Spec: fetch; models only the origin check of
    /// <see href="https://fetch.spec.whatwg.org/#concept-main-fetch">main fetch</see> for request mode
    /// <see href="https://fetch.spec.whatwg.org/#concept-request-mode">"same-origin"</see>, re-applied before every
    /// <see href="https://fetch.spec.whatwg.org/#http-redirect-fetch">HTTP redirect</see> hop.
    /// Spec: html; <see href="https://html.spec.whatwg.org/multipage/browsers.html#same-origin">same origin</see>.
    /// Stricter subset: data, file, blob and other non-HTTP(S) URLs, opaque request origins and non-HTTP(S) tuple
    /// request origins are denied before any I/O. Each redirect target is compared with the original
    /// <paramref name="requestOrigin"/> (never the preceding URL) before its request is sent. No CORS, Origin header,
    /// referrer, CSP or response tainting is implemented; this is not script-visible Fetch or a complete
    /// browser-wide policy. Denials throw <see cref="ResourceLoadException"/> with
    /// <see cref="ResourceError.SameOriginDenied"/>. As in main fetch, the
    /// <see href="https://fetch.spec.whatwg.org/#block-bad-port">bad-port check</see> precedes the origin check, so a
    /// bad-port URL or redirect target throws <see cref="ResourceError.BlockedPort"/>.</remarks>
    public async Task<ResourceResponse> LoadSameOriginAsync(BrowserUrl url, SecurityOrigin requestOrigin,
        bool includeCookies = false, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(requestOrigin);
        if (requestOrigin.IsOpaque || requestOrigin.Scheme is not ("http" or "https"))
        {
            throw new ResourceLoadException(ResourceError.SameOriginDenied,
                "Same-origin loads require an HTTP(S) tuple request origin.");
        }

        BadPortPolicy.ThrowIfBlocked(url);
        EnforceSameOrigin(url, requestOrigin);
        return await LoadCoreAsync(url, requestOrigin, includeCookies, cancellationToken);
    }

    private async Task<ResourceResponse> LoadCoreAsync(BrowserUrl url, SecurityOrigin? requestOrigin, bool includeCookies,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Timeout);
        try
        {
            return url.Protocol switch
            {
                "data:" => DataUrlProcessor.Load(url, options.MaxResponseBytes, deadline.Token),
                "file:" => await LoadFileAsync(url, deadline.Token),
                "http:" or "https:" => await LoadHttpAsync(url, requestOrigin, includeCookies, deadline.Token),
                _ => throw new ResourceLoadException(ResourceError.UnsupportedScheme, "Unsupported resource URL scheme.")
            };
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new ResourceLoadException(ResourceError.Timeout, "Resource load exceeded its deadline.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ResourceLoadException(ResourceError.Network, "HTTP transport failed.", exception);
        }
    }

    private static void EnforceSameOrigin(BrowserUrl url, SecurityOrigin requestOrigin)
    {
        if (url.Protocol is not ("http:" or "https:") || !requestOrigin.IsSameOrigin(url.Origin))
        {
            throw new ResourceLoadException(ResourceError.SameOriginDenied,
                "Resource URL is not same origin with the request origin.");
        }
    }

    private async Task<ResourceResponse> LoadHttpAsync(BrowserUrl initial, SecurityOrigin? requestOrigin, bool includeCookies,
        CancellationToken token)
    {
        var url = initial;
        var diagnostics = new List<string>();
        for (var redirects = 0; ; redirects++)
        {
            RejectCredentials(url);
            var uri = TransportUri(url);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (includeCookies && cookies.Header(uri) is { Length: > 0 } cookie)
            {
                request.Headers.Add("Cookie", cookie);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (includeCookies && response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                cookies.Store(uri, setCookies, diagnostics);
            }

            var status = (int)response.StatusCode;
            if (status is 301 or 302 or 303 or 307 or 308 && response.Headers.TryGetValues("Location", out var locations))
            {
                var values = locations.ToArray();
                if (values.Length != 1)
                {
                    throw new ResourceLoadException(ResourceError.InvalidRedirect, "Redirect requires exactly one Location value.");
                }

                if (redirects >= options.MaxRedirects)
                {
                    throw new ResourceLoadException(ResourceError.RedirectLimit, "HTTP redirect limit exceeded.");
                }

                var parsed = BrowserUrl.ParseResult(values[0], url);
                var next = parsed.Url ?? throw new ResourceLoadException(ResourceError.InvalidRedirect, "Invalid redirect URL: " + parsed.Error);
                if (next.Protocol is not ("http:" or "https:"))
                {
                    throw new ResourceLoadException(ResourceError.UnsupportedRedirect, "HTTP redirects must target HTTP(S).");
                }

                if (!values[0].Contains('#') && WithoutFragment(url).Length != url.Href.Length)
                {
                    next = BrowserUrl.Parse(next.Href + url.Href[WithoutFragment(url).Length..]);
                }

                BadPortPolicy.ThrowIfBlocked(next);
                if (requestOrigin is not null)
                {
                    EnforceSameOrigin(next, requestOrigin);
                }

                url = next;
                continue;
            }

            var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers.Concat(response.Content.Headers))
            {
                headers[header.Key] = Array.AsReadOnly(header.Value.ToArray());
            }

            MimeType? mime = null;
            if (headers.TryGetValue("Content-Type", out var types))
            {
                if (types.Count == 1)
                {
                    var parsed = MimeType.ParseResult(types[0]);
                    mime = parsed.Value;
                    if (!parsed.Success)
                    {
                        diagnostics.Add("Invalid HTTP Content-Type: " + parsed.Error);
                    }
                }
                else
                {
                    diagnostics.Add("Multiple HTTP Content-Type values are not supported.");
                }
            }

            if (response.Content.Headers.ContentLength > options.MaxResponseBytes)
            {
                throw BodyLimit();
            }

            byte[] body;
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                body = await ReadBodyAsync(stream, token);
            }
            catch (IOException exception) when (exception is not ResourceLoadException)
            {
                throw new ResourceLoadException(ResourceError.Network, "HTTP response body read failed.", exception);
            }

            return new(url, status, redirects > 0, headers, mime, body, diagnostics);
        }
    }

    private async Task<ResourceResponse> LoadFileAsync(BrowserUrl url, CancellationToken token)
    {
        if (url.Hostname.Length != 0)
        {
            throw new ResourceLoadException(ResourceError.RemoteFile, "Only local file URLs are supported.");
        }

        try
        {
            var uri = TransportUri(url);
            var path = uri.LocalPath;
            if (uri.IsUnc || path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                throw new ResourceLoadException(ResourceError.RemoteFile, "UNC file paths are not supported.");
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > options.MaxResponseBytes)
            {
                throw BodyLimit();
            }

            var body = await ReadBodyAsync(stream, token);
            return new(url, 200, false, new(StringComparer.OrdinalIgnoreCase), null, body, []);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or ArgumentException
            || exception is IOException and not ResourceLoadException)
        {
            throw new ResourceLoadException(ResourceError.FileAccess, "Local file could not be read.", exception);
        }
    }

    private async Task<byte[]> ReadBodyAsync(Stream stream, CancellationToken token)
    {
        using var body = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0,
                (int)Math.Min(buffer.Length, (long)options.MaxResponseBytes - body.Length + 1)), token);
            if (read == 0)
            {
                return body.ToArray();
            }

            if (body.Length + read > options.MaxResponseBytes)
            {
                throw BodyLimit();
            }

            body.Write(buffer, 0, read);
        }
    }

    internal static string WithoutFragment(BrowserUrl url)
    {
        var hash = url.Href.IndexOf('#');
        return hash < 0 ? url.Href : url.Href[..hash];
    }

    private static Uri TransportUri(BrowserUrl url) =>
        Uri.TryCreate(WithoutFragment(url), UriKind.Absolute, out var uri) ? uri
            : throw new ResourceLoadException(ResourceError.UnsupportedScheme, "Transport cannot represent this parsed URL.");

    private static void RejectCredentials(BrowserUrl url)
    {
        if (url.Username.Length != 0 || url.Password.Length != 0)
        {
            throw new ResourceLoadException(ResourceError.UrlCredentials, "Credentials in resource URLs are not supported.");
        }
    }

    internal static ResourceLoadException BodyLimit() => new(ResourceError.BodyLimit, "Resource body exceeds the configured byte limit.");

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            client.Dispose();
        }
    }
}
