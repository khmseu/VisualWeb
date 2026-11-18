# Core.Url

Immutable `BrowserUrl` provides WHATWG parsing, relative resolution,
serialization and URL components. `ParseResult` reports errors; `Parse` throws.
Both accept an optional `System.Text.Encoding` query override. It is used only
for special schemes other than `ws:`/`wss:`; otherwise query input is encoded
as UTF-8. The caller supplies the encoding; document-encoding discovery and
HTML URL-attribute integration are not included.
The managed [vendored MIT parser](../../../third_party/Dubzer.WhatwgUrl/README.md)
is isolated behind this facade; do not substitute System.Uri.

`BrowserUrl.Origin` is an immutable `SecurityOrigin`: a scheme/host/effective-port
tuple for http, https, ftp, ws and wss. A caller-supplied resolved blob-entry
origin overrides `blob:` path-URL fallback; without one, `blob:` inherits its
parsed path URL's origin only for http, https or file. Under this Core's
file-origin policy, `blob:file:` remains opaque. Other schemes and invalid
inner URLs yield opaque origins; no blob registry is implemented. Compare origins with
`IsSameOrigin`/`Equals`, never `SerializedOrigin`, whose opaque form is `"null"`.
`about:blank` URL snapshots are opaque; the browser's separate, bounded
page-initiated document-origin inheritance is selected by `LoadedPage`, not by
the URL facade. The immutable facade provides snapshot `WithHost`,
`WithHostname`, `WithPort`, `WithPathname`, `WithUsername` and `WithPassword`
setters. `WithHost` preserves the current port if no valid new port is
supplied; `WithHostname` preserves the current port and ignores values that
contain one; `WithPort` clears the port for empty input and commits the leading
ASCII digit run, ignoring missing or out-of-range numeric prefixes;
`WithPathname` leaves opaque-path URLs unchanged. Credential setters
use URL-standard userinfo encoding and are no-ops for URLs without a host and
`file:` URLs.
`MutableBrowserUrl` supplies mutable `Href`/`Search` state and one stable
`SearchParams` instance whose mutations update the current URL snapshot and
whose contents are refreshed in place when the URL is replaced. Its `Snapshot`
is still an immutable `BrowserUrl`; failed `Href` parsing leaves state unchanged.
An empty linked parameter serialization removes the query delimiter, while
assigning `Search = "?"` preserves an empty query until a parameter update occurs.
This is a focused Core state model, not the JavaScript `URL` binding or general
mutable component setters.
`ToDisplayString()` is a conservative browser-chrome form: it omits credentials,
uses Unicode IDN display, and elides path/query/fragment when the URL contains
bidirectional or invisible characters. It is not suitable for navigation,
origin comparison, or copying a canonical URL; confusable detection is not
implemented.

`UrlSearchParams` provides string
initialization, ordered
`Size`, `Get`/`GetAll` reads, `Has` by name or name/value, `Append`,
`Delete(name)`/`Delete(name, value)`, `Set(name, value)`, and stable
`Sort()` by UTF-16 code-unit name order. Instances created independently are
detached. Other mutations are not implemented. `ToString()` returns the UTF-8
`application/x-www-form-urlencoded` serialization of the ordered tuples.
See the [core guide](../../../docs/core.md) for conformance evidence and scope.
The browser-facing JavaScript `URL` and `URLSearchParams` facades live in
Engine.Scripting and use these Core APIs through a primitive-only bridge; see
the [scripting guide](../../../docs/scripting.md#url-interfaces). This does not
add JavaScript objects to Core.Url or claim full URL conformance.

Official source: <https://url.spec.whatwg.org/> (manifest ID `url`, local
`specs/cache/url.html`). See the [standards workflow](../../../docs/standards.md).
