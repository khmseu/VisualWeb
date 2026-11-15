using System.Text;

namespace VisualWeb.Core.Encoding;

/// <summary>Implements the UTF-8 <c>TextEncoder.encode()</c> operation.</summary>
/// <remarks>Spec: encoding; <see href="https://encoding.spec.whatwg.org/#interface-textencoder">TextEncoder</see>
/// and <see href="https://encoding.spec.whatwg.org/#dom-textencoder-encode">encode()</see>.
/// This core API returns a byte array and does not provide a Web IDL binding.</remarks>
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
}
