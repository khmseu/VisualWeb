using System.Text;

namespace VisualWeb.Core.Encoding;

/// <summary>Provides bounded decoding with the label and options of WHATWG <c>TextDecoder</c>.</summary>
/// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#interface-textdecoder">TextDecoder</see>.
/// The stream option is supported for UTF-8 only; other encodings decode complete buffers. Web IDL bindings are not provided.</remarks>
public sealed class WebTextDecoder
{
    private readonly WebEncoding _encoding;
    private Decoder? _utf8StreamDecoder;
    private bool _utf8StreamBomSeen;

    /// <summary>Creates a decoder for a supported web encoding.</summary>
    /// <param name="label">An Encoding Standard label, defaulting to UTF-8.</param>
    /// <param name="fatal">Whether malformed input throws instead of producing U+FFFD.</param>
    /// <param name="ignoreBom">Whether to preserve an initial U+FEFF in decoded output.</param>
    /// <exception cref="ArgumentException">The label is unknown or resolves to the replacement encoding.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is null.</exception>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#dom-textdecoder">TextDecoder constructor</see>.</remarks>
    public WebTextDecoder(string label = "utf-8", bool fatal = false, bool ignoreBom = false)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (!WebEncoding.TryForLabel(label, out var encoding) || encoding.Name == "replacement")
        {
            throw new ArgumentException($"Unsupported web decoder encoding label: {label}", nameof(label));
        }

        _encoding = encoding;
        Fatal = fatal;
        IgnoreBom = ignoreBom;
    }

    /// <summary>Gets the canonical encoding name in ASCII lowercase.</summary>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#dom-textdecoder-encoding">TextDecoder.encoding</see>.</remarks>
    public string Encoding => _encoding.Name.ToLowerInvariant();

    /// <summary>Gets whether malformed byte sequences cause a decoding exception.</summary>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#dom-textdecoder-fatal">TextDecoder.fatal</see>.</remarks>
    public bool Fatal { get; }

    /// <summary>Gets whether an initial U+FEFF is preserved rather than stripped.</summary>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#dom-textdecoder-ignorebom">TextDecoder.ignoreBOM</see>.</remarks>
    public bool IgnoreBom { get; }

    /// <summary>Decodes a complete byte buffer, returning an empty string for empty input.</summary>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#dom-textdecoder-decode">TextDecoder.decode()</see>.
    /// This overload decodes the buffer independently.</remarks>
    public string Decode(ReadOnlySpan<byte> input = default) => Decode(input, stream: false);

    /// <summary>Decodes a byte buffer, optionally retaining UTF-8 decoder state for the next call.</summary>
    /// <param name="input">The next bytes to decode.</param>
    /// <param name="stream">Whether more bytes will follow in a later call.</param>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#dom-textdecoder-decode">TextDecoder.decode()</see>.
    /// Streaming is supported only for UTF-8. A call with <paramref name="stream"/> false flushes pending input and resets the stream.</remarks>
    /// <exception cref="NotSupportedException">Streaming is requested for an encoding other than UTF-8.</exception>
    public string Decode(ReadOnlySpan<byte> input, bool stream)
    {
        if (stream && _encoding.Name != "UTF-8")
        {
            throw new NotSupportedException($"Streaming is not supported for {_encoding.Name}.");
        }

        if (stream || _utf8StreamDecoder is not null)
        {
            return DecodeUtf8Chunk(input, stream);
        }

        var text = _encoding.Decode(input, Fatal);
        var handlesBom = _encoding.Name is "UTF-8" or "UTF-16LE" or "UTF-16BE";
        return handlesBom && !IgnoreBom && text.StartsWith('\uFEFF') ? text[1..] : text;
    }

    private string DecodeUtf8Chunk(ReadOnlySpan<byte> input, bool stream)
    {
        if (_utf8StreamDecoder is null)
        {
            _utf8StreamDecoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: Fatal).GetDecoder();
            _utf8StreamBomSeen = false;
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: Fatal);
        var output = new char[encoding.GetMaxCharCount(input.Length)];
        try
        {
            var count = _utf8StreamDecoder.GetChars(input, output, flush: !stream);
            var text = new string(output, 0, count);
            if (!_utf8StreamBomSeen && text.Length > 0)
            {
                _utf8StreamBomSeen = true;
                if (!IgnoreBom && text[0] == '\uFEFF')
                {
                    text = text[1..];
                }
            }

            return text;
        }
        catch (DecoderFallbackException exception)
        {
            _utf8StreamDecoder = null;
            _utf8StreamBomSeen = false;
            throw new WebDecodingException(_encoding.Name, exception.Message);
        }
        finally
        {
            if (!stream)
            {
                _utf8StreamDecoder = null;
                _utf8StreamBomSeen = false;
            }
        }
    }
}
