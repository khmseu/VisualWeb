namespace VisualWeb.Core.Url;

/// <summary>Mutable URL state with a stable, live-linked <see cref="UrlSearchParams"/> object.</summary>
/// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#interface-url">URL interface</see>,
/// <see href="https://url.spec.whatwg.org/#dom-url-searchparams">URL.searchParams</see>, and
/// <see href="https://url.spec.whatwg.org/#concept-urlsearchparams-update">URLSearchParams update steps</see>.
/// <see cref="Snapshot"/> remains an immutable <see cref="BrowserUrl"/>; changing this object's URL does not mutate
/// any previously returned snapshot.</remarks>
public sealed class MutableBrowserUrl
{
    private BrowserUrl _snapshot;
    private readonly UrlSearchParams _searchParams;

    /// <summary>Creates mutable URL state from an immutable URL snapshot.</summary>
    /// <param name="url">The initial URL snapshot.</param>
    public MutableBrowserUrl(BrowserUrl url)
    {
        ArgumentNullException.ThrowIfNull(url);
        _snapshot = url;
        _searchParams = url.CreateSearchParams();
        _searchParams.Changed += OnSearchParamsChanged;
    }

    /// <summary>Gets the current immutable URL snapshot.</summary>
    public BrowserUrl Snapshot => _snapshot;

    /// <summary>Gets or replaces the serialized URL.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-href">URL.href setter</see>.
    /// Replacement parses an absolute URL before changing state and updates the existing
    /// <see cref="SearchParams"/> object rather than replacing it.</remarks>
    public string Href
    {
        get => _snapshot.Href;
        set
        {
            var replacement = BrowserUrl.Parse(value);
            ReplaceSnapshot(replacement);
        }
    }

    /// <summary>Gets or replaces the URL scheme.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-protocol">URL.protocol setter</see>.
    /// A rejected scheme transition leaves the current URL unchanged.</remarks>
    public string Protocol
    {
        get => _snapshot.Protocol;
        set => _snapshot = _snapshot.WithProtocol(value);
    }

    /// <summary>Gets or replaces the URL username.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-username">URL.username setter</see>.</remarks>
    public string Username
    {
        get => _snapshot.Username;
        set => _snapshot = _snapshot.WithUsername(value);
    }

    /// <summary>Gets or replaces the URL password.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-password">URL.password setter</see>.</remarks>
    public string Password
    {
        get => _snapshot.Password;
        set => _snapshot = _snapshot.WithPassword(value);
    }

    /// <summary>Gets or replaces the URL host, including its port when present.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-host">URL.host setter</see>.</remarks>
    public string Host
    {
        get => _snapshot.Host;
        set => _snapshot = _snapshot.WithHost(value);
    }

    /// <summary>Gets or replaces the URL hostname, preserving the current port.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-hostname">URL.hostname setter</see>.</remarks>
    public string Hostname
    {
        get => _snapshot.Hostname;
        set => _snapshot = _snapshot.WithHostname(value);
    }

    /// <summary>Gets or replaces the URL port.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-port">URL.port setter</see>.</remarks>
    public string Port
    {
        get => _snapshot.Port;
        set => _snapshot = _snapshot.WithPort(value);
    }

    /// <summary>Gets or replaces the URL path.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-pathname">URL.pathname setter</see>.</remarks>
    public string Pathname
    {
        get => _snapshot.Pathname;
        set => _snapshot = _snapshot.WithPathname(value);
    }

    /// <summary>Gets or replaces the URL query using the URL search setter semantics.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-search">URL.search setter</see>.
    /// The immutable snapshot is replaced; the current <see cref="SearchParams"/> object is retained
    /// and reinitialized from the new query.</remarks>
    public string Search
    {
        get => _snapshot.Search;
        set
        {
            var replacement = _snapshot.WithSearch(value);
            ReplaceSnapshot(replacement);
        }
    }

    /// <summary>Gets or replaces the URL fragment.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-hash">URL.hash setter</see>.</remarks>
    public string Hash
    {
        get => _snapshot.Hash;
        set => _snapshot = _snapshot.WithHash(value);
    }

    /// <summary>Gets the stable query object linked to this URL's query.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-url-searchparams">URL.searchParams</see>.</remarks>
    public UrlSearchParams SearchParams => _searchParams;

    private void ReplaceSnapshot(BrowserUrl replacement)
    {
        _snapshot = replacement;
        _searchParams.ReplaceWith(replacement.CreateSearchParams());
    }

    private void OnSearchParamsChanged() => _snapshot = _snapshot.WithSearchParams(_searchParams);
}
