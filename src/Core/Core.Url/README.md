# Core.Url

Immutable `BrowserUrl` provides WHATWG parsing, relative resolution,
serialization and URL components. `ParseResult` reports errors; `Parse` throws.
The managed [vendored MIT parser](../../../third_party/Dubzer.WhatwgUrl/README.md)
is isolated behind this facade; do not substitute System.Uri.

`BrowserUrl.Origin` is an immutable `SecurityOrigin`: a scheme/host/effective-port
tuple for http, https, ftp, ws and wss. `blob:` inherits its parsed path URL's
origin only for http, https or file; under this Core's file-origin policy,
`blob:file:` remains opaque. Other schemes and invalid inner URLs yield opaque
origins; no blob registry is implemented. Compare origins with
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
`UrlSearchParams` provides string
initialization, ordered
`Size`, `Get`/`GetAll` reads, `Has` by name or name/value, `Append`,
`Delete(name)`/`Delete(name, value)`, `Set(name, value)`, and stable
`Sort()` by UTF-16 code-unit name order. Other mutations and URL linkage are
not implemented. `ToString()` returns the UTF-8
`application/x-www-form-urlencoded` serialization of the ordered tuples.
See the [core guide](../../../docs/core.md) for conformance evidence and scope.

Official source: <https://url.spec.whatwg.org/> (manifest ID `url`, local
`specs/cache/url.html`). See the [standards workflow](../../../docs/standards.md).
