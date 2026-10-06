using Dubzer.WhatwgUrl;

namespace VisualWeb.Core.Url;

/// <summary>Immutable snapshot of a parsed WHATWG URL.</summary>
/// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#concept-basic-url-parser">basic URL parser</see>
/// and <see href="https://url.spec.whatwg.org/#concept-url-serializer">URL serializer</see>.
/// Parsing is delegated to the managed MIT-licensed Dubzer.WhatwgUrl implementation.
/// SerializedOrigin is for display/serialization, not an opaque-origin security identity; use <see cref="Origin"/>.
/// Origin is computed once per snapshot, so an opaque origin keeps its identity across reads of the same instance
/// but a re-parse yields a different one. See <see href="https://url.spec.whatwg.org/#concept-url-origin">origin of a URL</see>.</remarks>
public sealed class BrowserUrl
{
    public string Href { get; }
    public string Protocol { get; }
    public string Username { get; }
    public string Password { get; }
    public string Host { get; }
    public string Hostname { get; }
    public string Port { get; }
    public string Pathname { get; }
    public string Search { get; }
    public string Hash { get; }
    public string SerializedOrigin { get; }
    public SecurityOrigin Origin { get; }

    private BrowserUrl(DomUrl url)
    {
        Href = url.Href;
        Protocol = url.Protocol;
        Username = url.Username;
        Password = url.Password;
        Host = url.Host;
        Hostname = url.Hostname;
        Port = url.Port;
        Pathname = url.Pathname;
        Search = url.Search;
        Hash = url.Hash;
        SerializedOrigin = url.Origin;
        Origin = ComputeOrigin(url, allowBlob: true);
    }

    private static SecurityOrigin ComputeOrigin(DomUrl url, bool allowBlob)
    {
        switch (url.Protocol)
        {
            case "http:" or "https:" or "ftp:" or "ws:" or "wss:":
                return SecurityOrigin.FromTuple(url.Protocol[..^1], url.Hostname, url.Port);
            case "blob:" when allowBlob:
                try
                {
                    var inner = new DomUrl(url.Pathname);
                    return inner.Protocol is "http:" or "https:"
                        ? ComputeOrigin(inner, allowBlob: false)
                        : SecurityOrigin.CreateOpaque();
                }
                catch (InvalidUrlException)
                {
                    return SecurityOrigin.CreateOpaque();
                }
            default:
                return SecurityOrigin.CreateOpaque();
        }
    }

    public static UrlParseResult ParseResult(string input, BrowserUrl? baseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            return new(new BrowserUrl(new DomUrl(input, baseUrl?.Href)), null);
        }
        catch (InvalidUrlException exception)
        {
            return new(null, $"{exception.UrlError}: {exception.Message}");
        }
    }

    public static BrowserUrl Parse(string input, BrowserUrl? baseUrl = null)
    {
        var result = ParseResult(input, baseUrl);
        return result.Url ?? throw new UrlParseException(result.Error!);
    }

    public BrowserUrl Resolve(string relative) => Parse(relative, this);
    public override string ToString() => Href;
}

public sealed record UrlParseResult(BrowserUrl? Url, string? Error)
{
    public bool Success => Url is not null;
}

public sealed class UrlParseException(string message) : FormatException(message);
