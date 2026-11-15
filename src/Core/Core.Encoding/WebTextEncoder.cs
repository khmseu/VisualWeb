using System.Text;

namespace VisualWeb.Core.Encoding;

/// <summary>Implements the UTF-8 <c>TextEncoder.encode()</c> and <c>encodeInto()</c> operations.</summary>
/// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#interface-textencoder">TextEncoder</see>
/// and <see href="https://encoding.spec.whatwg.org/#dom-textencoder-encode">encode()</see>.
/// This core API uses .NET arrays and spans and does not provide Web IDL bindings.</remarks>
public sealed class WebTextEncoder
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    /// <summary>Encodes a string as UTF-8, replacing unpaired surrogates with U+FFFD.</summary>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#dom-textencoder-encode">TextEncoder.encode()</see>.</remarks>
    public byte[] Encode(string input = "")
    {
        ArgumentNullException.ThrowIfNull(input);
        return Utf8.GetBytes(input);
    }

    /// <summary>Encodes complete UTF-8 scalars that fit in the destination.</summary>
    /// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#dom-textencoder-encodeinto">TextEncoder.encodeInto()</see>.
    /// Source counts are UTF-16 code units; the returned tuple mirrors the standard result dictionary and is bounded by .NET span lengths.</remarks>
    public (int Read, int Written) EncodeInto(ReadOnlySpan<char> source, Span<byte> destination)
    {
        var read = 0;
        var written = 0;

        while (read < source.Length)
        {
            var first = source[read];
            Rune scalar;
            var codeUnits = 1;
            if (char.IsHighSurrogate(first) && read + 1 < source.Length && char.IsLowSurrogate(source[read + 1]))
            {
                scalar = new Rune(first, source[read + 1]);
                codeUnits = 2;
            }
            else
            {
                scalar = char.IsSurrogate(first) ? Rune.ReplacementChar : new Rune(first);
            }

            if (destination.Length - written < scalar.Utf8SequenceLength) { break; }

            written += scalar.EncodeToUtf8(destination[written..]);
            read += codeUnits;
        }

        return (read, written);
    }
}
