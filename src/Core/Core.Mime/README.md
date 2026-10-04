# Core.Mime

Immutable `MimeType` implements MIME parsing and serialization, including
ordered, first-occurrence parameters and quoted-string escaping.
`ParseResult` reports invalid types; `Parse` throws.

Full context-dependent MIME sniffing and MIME minimization/classification are
not implemented. See the [core guide](../../../docs/core.md) for scope and tests.

Official source: https://mimesniff.spec.whatwg.org/ (ID `mime-sniffing`).
See the [standards workflow](../../../docs/standards.md).
