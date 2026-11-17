# Core.Encoding

`WebEncoding.ForLabel`/`TryForLabel` resolve all official labels. `Decode` provides
whole-buffer Unicode, single-byte and legacy multibyte decoding with replacement
or fatal errors. `SniffBom` and `DecodeWithBom` detect UTF-8/UTF-16 BOMs before
the caller's fallback; raw `Decode` preserves BOM characters.

`WebTextDecoder` provides a bounded .NET subset of `TextDecoder`, including
encoding labels, fatal errors and BOM handling. Its `stream` option retains
incremental decoder and BOM state for UTF-8 and UTF-16LE/BE; stateless
single-byte encodings and the pair-based Big5, Shift_JIS and EUC-KR decoders
also support chunk-by-chunk decoding. EUC-JP streaming retains its lead and
JIS0212 state across chunks. Legacy decoders preserve invalid-trail consumption
and ASCII trail reprocessing. GBK/gb18030 and ISO-2022-JP remain one-shot.
JavaScript bindings are not provided.

`WebTextEncoder.Encode` and `EncodeInto` implement the UTF-8
`TextEncoder.encode()` and `encodeInto()` operations, including replacement of
unpaired surrogates and scalar-atomic writes to caller-provided spans.
`EncodeInto` returns UTF-16 code units read and bytes written. These core APIs
use counts bounded by .NET span lengths and do not provide JavaScript bindings.
`TextEncoderStream` and encoders for other labels remain unsupported.

`HtmlEncodingPrescanner` implements the HTML byte-level prescan (first 1024
bytes, `meta` charset/pragma, UTF-16 `<?x` prefix, XML-declaration fallback).
`HtmlEncodingSniffer` is a documented subset of HTML encoding sniffing: BOM,
supported transport charset, prescan, then the caller's default. Neither depends
on Engine.Html.

Tables are [pinned official data](Data/README.md). Streaming decoders for
GBK/gb18030 and ISO-2022-JP, `TextEncoderStream`, JavaScript/Web IDL
bindings, statistical/locale sniffing and parser-time encoding changes are not
implemented.
See the [core guide](../../../docs/core.md) for details and tests.

Official sources: <https://encoding.spec.whatwg.org/> (ID `encoding`) and
<https://html.spec.whatwg.org/multipage/parsing.html#determining-the-character-encoding> (ID `html`).
See the [standards workflow](../../../docs/standards.md).
