# Core.Url

Immutable `BrowserUrl` provides WHATWG parsing, relative resolution,
serialization and URL components. `ParseResult` reports errors; `Parse` throws.
The managed [vendored MIT parser](../../../third_party/Dubzer.WhatwgUrl/README.md)
is isolated behind this facade; do not substitute System.Uri.

`BrowserUrl.Origin` is an immutable `SecurityOrigin`: a scheme/host/effective-port
tuple for http, https, ftp, ws, wss (and `blob:` wrapping http/https), otherwise
a unique opaque origin (always for `file:`; no blob registry). Compare origins
with `IsSameOrigin`/`Equals`, never `SerializedOrigin`, whose opaque form is `"null"`.
No setters or URLSearchParams are exposed; document origins are a later phase.
See the [core guide](../../../docs/core.md) for conformance evidence and scope.

Official source: https://url.spec.whatwg.org/ (manifest ID `url`, local
`specs/cache/url.html`). See the [standards workflow](../../../docs/standards.md).
