namespace VisualWeb.Core.Encoding;

/// <summary>The step of the encoding sniffing subset that chose the encoding.</summary>
public enum HtmlEncodingSource { ByteOrderMark, TransportLayer, Prescan, Default }

/// <summary>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#concept-encoding-confidence">confidence</see>.</summary>
public enum HtmlEncodingConfidence { Tentative, Certain }

/// <summary>Result of <see cref="HtmlEncodingSniffer.Sniff"/>.</summary>
/// <param name="Prescan">The prescan result when the prescan step ran, otherwise null.</param>
/// <param name="UnsupportedTransportLabel">A transport charset label that did not resolve; the spec skips it,
/// and callers that prefer a visible failure can inspect it.</param>
public sealed record HtmlEncodingSniffResult(WebEncoding Encoding, HtmlEncodingSource Source, HtmlEncodingConfidence Confidence,
    HtmlPrescanResult? Prescan, string? UnsupportedTransportLabel);

/// <summary>A deliberate subset of the HTML encoding sniffing algorithm for a complete buffered response.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#encoding-sniffing-algorithm">encoding sniffing algorithm</see>.
/// Implemented steps, in order: BOM sniffing (certain), a supported transport-layer charset (certain), the
/// <see cref="HtmlEncodingPrescanner"/> prescan of the first 1024 bytes (tentative), then the caller's default (tentative).
/// Not implemented: user override, waiting for more bytes, parent-document (container) inheritance, remembered
/// per-page encodings, frequency-analysis autodetection, locale-dependent defaults, and the parser's later
/// "change the encoding" reparse when a conflicting declaration is seen after the prescan.</remarks>
public static class HtmlEncodingSniffer
{
    public static HtmlEncodingSniffResult Sniff(ReadOnlySpan<byte> bytes, string? transportLabel, WebEncoding defaultEncoding)
    {
        ArgumentNullException.ThrowIfNull(defaultEncoding);
        string? unsupported = null;
        WebEncoding? transport = null;
        if (transportLabel is not null && !WebEncoding.TryForLabel(transportLabel, out transport))
        {
            unsupported = transportLabel;
        }

        if (WebEncoding.SniffBom(bytes, out _) is { } bom)
        {
            return new(bom, HtmlEncodingSource.ByteOrderMark, HtmlEncodingConfidence.Certain, null, unsupported);
        }

        if (transport is not null)
        {
            return new(transport, HtmlEncodingSource.TransportLayer, HtmlEncodingConfidence.Certain, null, null);
        }

        var prescan = HtmlEncodingPrescanner.Prescan(bytes);
        return prescan.Encoding is { } declared
            ? new(declared, HtmlEncodingSource.Prescan, HtmlEncodingConfidence.Tentative, prescan, unsupported)
            : new(defaultEncoding, HtmlEncodingSource.Default, HtmlEncodingConfidence.Tentative, prescan, unsupported);
    }
}
