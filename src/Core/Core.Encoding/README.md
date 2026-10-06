# Core.Encoding

`WebEncoding.ForLabel`/`TryForLabel` resolve all official labels. `Decode` provides
whole-buffer Unicode, single-byte and legacy multibyte decoding with replacement
or fatal errors. `SniffBom` and `DecodeWithBom` detect UTF-8/UTF-16 BOMs before
the caller's fallback; raw `Decode` preserves BOM characters.

`HtmlEncodingPrescanner` implements the HTML byte-level prescan (first 1024
bytes, `meta` charset/pragma, UTF-16 `<?x` prefix, XML-declaration fallback).
`HtmlEncodingSniffer` is a documented subset of HTML encoding sniffing: BOM,
supported transport charset, prescan, then the caller's default. Neither depends
on Engine.Html.

Tables are [pinned official data](Data/README.md). Streaming decoders, encoders,
TextDecoder bindings, statistical/locale sniffing and parser-time encoding
changes are not implemented.
See the [core guide](../../../docs/core.md) for details and tests.

Official sources: https://encoding.spec.whatwg.org/ (ID `encoding`) and
https://html.spec.whatwg.org/multipage/parsing.html#determining-the-character-encoding (ID `html`).
See the [standards workflow](../../../docs/standards.md).
