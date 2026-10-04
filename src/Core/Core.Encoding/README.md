# Core.Encoding

`WebEncoding.ForLabel` resolves all official labels. `Decode` provides
whole-buffer Unicode, single-byte and legacy multibyte decoding with replacement
or fatal errors. `DecodeWithBom` detects UTF-8/UTF-16 BOMs before the caller's
fallback; raw `Decode` preserves BOM characters.

Tables are [pinned official data](Data/README.md). Streaming decoders, encoders,
TextDecoder bindings and HTML charset prescanning are not implemented.
See the [core guide](../../../docs/core.md) for details and tests.

Official source: https://encoding.spec.whatwg.org/ (ID `encoding`).
See the [standards workflow](../../../docs/standards.md).
