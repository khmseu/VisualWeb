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
