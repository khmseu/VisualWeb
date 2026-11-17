namespace VisualWeb.Core.Encoding;

/// <summary>Provides one-shot decoding with the label and options of WHATWG <c>TextDecoder</c>.</summary>
/// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#interface-textdecoder">TextDecoder</see>.
/// This .NET subset decodes complete buffers and does not provide streaming or Web IDL bindings.</remarks>
public sealed class WebTextDecoder
{
    private readonly WebEncoding _encoding;

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
    /// The buffer is decoded independently; the <c>stream</c> option and decoder state across calls are not implemented.</remarks>
    public string Decode(ReadOnlySpan<byte> input = default)
    {
        var text = _encoding.Decode(input, Fatal);
        var handlesBom = _encoding.Name is "UTF-8" or "UTF-16LE" or "UTF-16BE";
        return handlesBom && !IgnoreBom && text.StartsWith('\uFEFF') ? text[1..] : text;
    }
}
