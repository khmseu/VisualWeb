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
`Parse` and `ParseResult` accept an optional `System.Text.Encoding` query
override: special schemes other than `ws`/`wss` use it to encode query input
before percent-encoding; the default and all other schemes use UTF-8. The
caller supplies the encoding, so this does not add document-encoding discovery
or HTML URL-attribute integration.
`Parse` and `ParseResult` also accept a resolved `blobUrlEntryOrigin`; it
overrides the blob path-URL origin fallback for `blob:` URLs. Callers must do
the entry lookup themselves; Core does not implement a Blob URL registry.
`ToDisplayString()` omits credentials, renders valid IDN domains with the
Unicode 17 URL mapping, and truncates URLs containing bidi/invisible
characters to the host/origin prefix. It is display-only, not a navigation or
identity value; confusable-character detection and browser-specific scheme
indicators remain out of scope.

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
leaves it implementation-defined). `blob:` uses an injected entry origin when
provided; otherwise it takes the parsed path URL's origin only when its scheme
is `http`, `https` or `file`. With this Core's file-origin policy, `blob:file:`
remains opaque. Invalid or other inner URLs yield a fresh opaque origin. There
is no blob URL entry registry. Origin is computed once per `BrowserUrl`, so
re-parsing an opaque URL gives a different origin.
The app-layer `LoadedPage` now associates each new document with the final
response URL's tuple origin or a fresh opaque identity, independent of opaque URL
snapshot reuse. Retained repaint and metadata record copies preserve that
document identity. The browser shell publishes it as the tab's read-only
committed origin only with a successfully rendered document. The browser also
supports page-initiated `about:blank`: it inherits the initiating committed
origin, and that selection is retained across reload and history traversal.
Browser-UI `about:blank` loads receive fresh opaque origins. This bounded
behavior does not add nested browsing contexts or sandbox flags; other
inherited-origin selection, `document.domain` and enforcement remain deferred.

`BrowserUrl.CanParse(input, base)` implements `URL.canParse()`: a non-null base
is parsed first and any base failure (including `""`) returns false.
`BrowserUrl.ParseOrNull(input, base)` implements `URL.parse()`, returning null
for invalid input or base.
`BrowserUrl.WithHash(value)` and `WithSearch(value)` implement the `hash` and
`search` setters on the immutable facade by re-parsing an encoded component and
returning a new snapshot (the original and its opaque-origin identity are unchanged).
`WithHref(value)` parses a new absolute URL without a base, returning a snapshot
and leaving the original unchanged.
`ToJson()` returns the serialized URL string for the standard `toJSON()` method.
`WithProtocol(value)` changes the scheme when it is valid and obeys the
special-scheme transition restrictions; invalid values leave the snapshot
unchanged. The value may omit its trailing colon, and ASCII tabs/newlines are
removed as by the URL parser. `WithUsername(value)` and `WithPassword(value)`
apply the URL credential setters with the standard's UTF-8 userinfo
percent-encode set; URLs without a host and `file:` URLs remain unchanged.
`WithHost(value)` applies the host state override to a new snapshot, retaining
the current port when the input has no valid port and preserving the URL's
credentials and path/query/fragment. File URLs use file-host parsing, which
normalizes `localhost` to an empty host and does not accept a port. Opaque-path
URLs are unchanged.
`WithHostname(value)` applies the hostname state override, preserving the
current port and other URL components; values containing a port are ignored.
Opaque-path URLs are unchanged.
`WithPort(value)` applies the port setter to a new snapshot, clearing the port
for an empty value and leaving values without a leading ASCII digit or with an
out-of-range numeric prefix, and URLs that cannot have a port, unchanged. Like
the port state override, a leading digit run is committed before later input.
`WithPathname(value)` applies the pathname setter to a new snapshot, preserving
the query and fragment; opaque-path URLs are unchanged.
`CreateSearchParams()` returns a detached `UrlSearchParams` parsed from the query and
`WithSearchParams()` writes its serialization back as a new snapshot; there is no live
link between a URL and its parameters.

[`MutableBrowserUrl`](../src/Core/Core.Url/MutableBrowserUrl.cs) is the minimal
mutable Core state for consumers that need live query linkage. Its `Snapshot`
is always an immutable `BrowserUrl`; `Href`, `Protocol`, `Username`, `Password`,
`Host`, `Hostname`, `Port`, `Pathname`, `Search`, and `Hash` setters replace
that snapshot using the corresponding immutable setter. `Href` and `Search`
refresh the same `SearchParams` object in place; other component setters retain
its tuple list. Mutating `SearchParams` updates the current snapshot's query
while retaining its other URL components. An empty parameter serialization
removes the query delimiter; setting `Search` to `"?"` retains an empty query
until a parameter update occurs. Failed `Href` parsing leaves the existing
state unchanged. This is a Core state model, not JavaScript/Web IDL bindings.

[`UrlSearchParams`](../src/Core/Core.Url/UrlSearchParams.cs) implements the
WHATWG string, sequence-of-pairs and record initializers, `append()`, `delete(name)`,
`delete(name, value)`, `set(name, value)` and stable `sort()` by UTF-16
code-unit name order, ordered `size`, `get()` and `getAll()`, plus `has()` with
name-only and name/value matching, live `Entries()`, `Keys()` and
`Values()` enumerators and `ForEach()`. It decodes
`application/x-www-form-urlencoded` tuples, preserving duplicates, and
`toString()` serializes the ordered tuples with the standard's UTF-8 percent
encoding rules. Standalone instances remain detached; only an instance owned by
`MutableBrowserUrl` is linked. The API does not provide other mutation methods,
ECMAScript iterator objects or Web IDL bindings.
`SerializedOrigin` remains display text only; do not use it as an authorization check.
The browser-facing JavaScript `URL` and `URLSearchParams` facades are implemented
in Engine.Scripting over these Core APIs; see the
[scripting guide](scripting.md#url-interfaces). This does not add script execution
to the static content pipeline.

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
the selected encoding and text. `TryForLabel` is the non-throwing "get an
encoding" (ASCII-only case folding, so e.g. a Kelvin sign never matches `k`).

[WebTextEncoder](../src/Core/Core.Encoding/WebTextEncoder.cs) implements the
UTF-8 [`TextEncoder.encode()`](https://encoding.spec.whatwg.org/#dom-textencoder-encode)
and [`encodeInto()`](https://encoding.spec.whatwg.org/#dom-textencoder-encodeinto)
operations. `Encode` returns a .NET byte array; `EncodeInto` writes complete
scalars into a caller-provided span and reports UTF-16 code units read and bytes
written, bounded by .NET span lengths. Both replace unpaired surrogates with
U+FFFD. These are core APIs, not JavaScript/Web IDL bindings.
`TextEncoderStream`, encoders for other labels and JavaScript/Web IDL encoder
and decoder bindings remain deferred.

[WebTextDecoder](../src/Core/Core.Encoding/WebTextDecoder.cs) provides a bounded
.NET subset of [`TextDecoder`](https://encoding.spec.whatwg.org/#interface-textdecoder):
label resolution, canonical encoding name, fatal error mode and `ignoreBOM`.
Its `stream` option retains UTF-8 and UTF-16LE/BE decoder and BOM state across
chunks; stateless single-byte encodings and the pair-based Big5, Shift_JIS and
EUC-KR decoders, plus EUC-JP, also accept chunked input, preserving each
decoder's invalid-trail consumption and ASCII reprocessing rules. GBK/gb18030
share the incremental gb18030 decoder state machine. ISO-2022-JP retains its
mode, escape, lead/trail and output-flag state across chunks, including
specification-defined reconsume and final-flush behavior.
`TextDecoderStream` and JavaScript/Web IDL bindings remain deferred.

### HTML charset prescan and sniffing subset

[HtmlEncodingPrescanner](../src/Core/Core.Encoding/HtmlEncodingPrescanner.cs)
follows the HTML "prescan a byte stream to determine its encoding" steps over a
read-only span with the end condition fixed at the first **1024 bytes** (bytes,
not characters). It covers the UTF-16 `<?x` prefixes, `<!--` comments, `<!`/`</`/`<?`
skipping, generic tags whose attributes are consumed with "get an attribute",
case-folded `meta` with duplicate-attribute suppression, `charset` versus
`http-equiv="content-type"` + `content` (need/got pragma, order independent),
failure labels that continue scanning at the next byte, UTF-16 → UTF-8 and
x-user-defined → windows-1252 normalization, and the "get an XML encoding"
fallback. Running out of bytes inside any construct — including a declaration
that is incomplete at byte 1024 — aborts exactly as the spec requires; bytes after
the limit are never read. `ExtractFromMetaContent` exposes the meta content
algorithm. Results report source, declaration offset and ignored labels.

[HtmlEncodingSniffer](../src/Core/Core.Encoding/HtmlEncodingSniffer.cs) runs only
these sniffing steps on a complete buffer: BOM (certain), supported transport
charset (certain), prescan (tentative), caller default (tentative). Unsupported
transport labels are skipped as specified and reported for callers. Deliberately
missing: user override, waiting for more bytes, container-document inheritance,
remembered per-page encodings, frequency-analysis autodetection, locale defaults
and the parser's "change the encoding" reparse. There are no pinned html5lib
`encoding/` fixtures; tests are spec-derived cases.

`TextEncoderStream` and JavaScript/Web IDL bindings remain later work.

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
boundaries, malformed sequences, fatal errors, BOM handling and HTML prescan
cases including exact 1024-byte boundaries. Tests never
download data or refresh documentation. These finite checks are conformance
evidence, not a claim to implement every web standard or API.

Third-party sources retain upstream formatting; all projects, including the
vendor, still participate in builds and tests.

Phase-3 validation on Linux x64: all 27 projects build; all 3,857 tests pass
(3,815 core, 19 platform, 23 cache). Formatting verification and editor
diagnostics are clean. The separate cache check reports all 31 references fresh.
This does not certify native Windows/arm64 behavior.
