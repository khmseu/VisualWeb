# Core.Url

Immutable `BrowserUrl` provides WHATWG parsing, relative resolution,
serialization and URL components. `ParseResult` reports errors; `Parse` throws.
The managed [vendored MIT parser](../../../third_party/Dubzer.WhatwgUrl/README.md)
is isolated behind this facade; do not substitute System.Uri.

No setters, URLSearchParams or security-origin identity are exposed yet.
Serialized opaque origins are `"null"` and must not be compared for security.
See the [core guide](../../../docs/core.md) for conformance evidence and scope.

Official source: https://url.spec.whatwg.org/ (manifest ID `url`, local
`specs/cache/url.html`). See the [standards workflow](../../../docs/standards.md).
