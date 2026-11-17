# Core.Encoding

`WebEncoding.ForLabel`/`TryForLabel` resolve all official labels. `Decode` provides
whole-buffer Unicode, single-byte and legacy multibyte decoding with replacement
or fatal errors. `SniffBom` and `DecodeWithBom` detect UTF-8/UTF-16 BOMs before
the caller's fallback; raw `Decode` preserves BOM characters.

`WebTextDecoder` provides a one-shot .NET subset of `TextDecoder`, including
encoding labels, fatal errors and BOM handling. It does not retain decoder state
between calls, implement the `stream` option or provide JavaScript bindings.

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

Tables are [pinned official data](Data/README.md). Streaming decoders,
`TextEncoderStream`, JavaScript/Web IDL bindings, statistical/locale sniffing
and parser-time encoding changes are not implemented.
See the [core guide](../../../docs/core.md) for details and tests.

Official sources: <https://encoding.spec.whatwg.org/> (ID `encoding`) and
<https://html.spec.whatwg.org/multipage/parsing.html#determining-the-character-encoding> (ID `html`).
See the [standards workflow](../../../docs/standards.md).
