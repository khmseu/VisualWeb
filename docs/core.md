# Core standards utilities

Phase 3 implements three independent managed libraries. They do not introduce
native dependencies or browser process state. Public APIs cite the official
standards; the [standards cache](standards.md) is separate from tests.

## URL

[BrowserUrl](../src/Core/Core.Url/BrowserUrl.cs) snapshots URL components,
serialized URL and origin. `ParseResult` returns a diagnostic on invalid URLs;
`Parse` throws `UrlParseException`; `Resolve` creates a new URL without mutating
the base. It handles special/non-special schemes, relative references,
credentials, IPv4/IPv6 and Unicode domains through the vendored parser.

The user approved [Dubzer.WhatwgUrl](../third_party/Dubzer.WhatwgUrl/README.md)
behind our immutable facade and subsequently approved MIT source vendoring
after release 1.1.0 failed nine URL vectors. Local changes correct non-strict
ASCII domain handling, leading-zero embedded IPv4 and default bidi classes.
The vendor README is the patch ledger and update guide.

[SecurityOrigin](../src/Core/Core.Url/SecurityOrigin.cs), exposed as `BrowserUrl.Origin`,
is the immutable URL-level origin identity ([URL origin](https://url.spec.whatwg.org/#concept-url-origin),
[HTML origin](https://html.spec.whatwg.org/multipage/browsers.html#concept-origin)).
`http`, `https`, `ftp`, `ws` and `wss` URLs give a tuple of scheme, normalized host
(IDNA/IPv4/IPv6 as serialized) and effective port; equality uses those three
values. Everything else gives a unique opaque origin equal only to the same
instance, so `"null"` is never an identity. `file:` is always opaque (the URL spec
leaves it implementation-defined). `blob:` takes the origin of its path only when
that parses as `http`/`https`; there is no blob URL entry registry. Origin is
computed once per `BrowserUrl`, so re-parsing an opaque URL gives a different
origin. The app-layer `LoadedPage` now associates each new document with the final
response URL's tuple origin or a fresh opaque identity, independent of opaque URL
snapshot reuse. Retained repaint and metadata record copies preserve that
document identity. The browser shell publishes it as the tab's read-only
committed origin only with a successfully rendered document. Sandbox/inherited
origin selection, `document.domain` and enforcement remain deferred.

No mutable DOM URL API or URLSearchParams is implemented.
`SerializedOrigin` remains display text only; do not use it as an authorization check.

## Encoding

[WebEncoding](../src/Core/Core.Encoding/WebEncoding.cs) resolves all 228
official labels to 40 canonical encodings, trimming only ASCII whitespace.
Unknown labels fail explicitly.

Whole-buffer decoding covers UTF-8, UTF-16LE/BE, all single-byte encodings,
GBK/gb18030, Big5, Shift_JIS, EUC-KR, EUC-JP, ISO-2022-JP, replacement and
x-user-defined. Default decoding emits U+FFFD on errors; `fatal: true` throws
`WebDecodingException`. Legacy decoders use official index data and the
standard's reconsume/EOF rules, rather than OS code-page approximations.

`Decode` does not sniff or strip a BOM. `DecodeWithBom` applies UTF-8/UTF-16 BOM
precedence over a supplied fallback and strips exactly one BOM. It returns both
the selected encoding and text. HTML charset prescanning, transport metadata
policy, incremental decoding, encoders and JavaScript TextDecoder bindings
remain later work; callers must collect a complete buffer for this API.

## MIME

[MimeType](../src/Core/Core.Mime/MimeType.cs) parses HTTP token type/subtype and
ordered parameters, lowercases names, keeps the first accepted duplicate and
handles quoted-string escapes. Records are immutable. Serialization quotes and
escapes parameter values when necessary. `ParseResult` reports invalid
type/subtype with a diagnostic; `Parse` throws.

This is MIME parsing/serialization, not full MIME Sniffing Standard conformance.
Context-dependent byte sniffing and minimization/classification are deferred.

## Pinned sources and licenses

- URL parser: release 1.1.0, commit
  `d8483ab0a64085b5642402e27e2671fddbc412da`;
  [MIT license and patch notes](../third_party/Dubzer.WhatwgUrl/README.md).
- Encoding labels/indexes: WHATWG Encoding commit
  `a985b62a9b45c17da3e17a9f0a0b4e30c34c4a8a`;
  [data provenance and license](../src/Core/Core.Encoding/Data/README.md).
- WPT: commit `8e9969fd5559dcff933a1cf4e62e9f5bc16b7231`, except the
  full IDNA fixture uses `b63305b743ed9ce2725d4ae09c5c8f0c40d8e6e1`;
  [fixture paths and BSD-3-Clause license](../tests/Core.Tests/Data/README.md).

The user selected stable Unicode 17 IDNA over Unicode 18 preview data.
The full IDNA fixture therefore deliberately uses its last Unicode 17 revision.
No individual vectors are modified or suppressed. Empty domain inputs are
excluded exactly as in the official WPT driver because they cannot be tested
through a constructed HTTPS URL. Test-only JSON string reading reproduces
JavaScript USVString replacement for lone UTF-16 surrogates.

## Validation

```sh
dotnet test tests/Core.Tests/Core.Tests.csproj
dotnet build VisualWeb.slnx
dotnet test VisualWeb.slnx --no-build
dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party
```

The suite checks 896 URL parsing vectors, 87 additional domain vectors, the full
pinned Unicode 17 IDNA corpus, 74 MIME records, official ISO-2022-JP vectors,
all encoding labels, every mapped single/multibyte pointer, gb18030 range
boundaries, malformed sequences, fatal errors and BOM handling. Tests never
download data or refresh documentation. These finite checks are conformance
evidence, not a claim to implement every web standard or API.

Third-party sources retain upstream formatting; all projects, including the
vendor, still participate in builds and tests.

Phase-3 validation on Linux x64: all 27 projects build; all 3,857 tests pass
(3,815 core, 19 platform, 23 cache). Formatting verification and editor
diagnostics are clean. The separate cache check reports all 31 references fresh.
This does not certify native Windows/arm64 behavior.
