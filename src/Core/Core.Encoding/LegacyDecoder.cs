using System.Text;
using System.Text.Json;

namespace VisualWeb.Core.Encoding;

internal static class LegacyDecoder
{
    private static readonly (int Pointer, int CodePoint)[] GbRanges = LoadGbRanges();

    public static string Decode(ReadOnlySpan<byte> bytes, string name, bool fatal) => name switch
    {
        "GBK" or "gb18030" => DecodeGb(bytes, fatal),
        "Big5" => DecodePairs(bytes, PairEncoding.Big5, fatal),
        "Shift_JIS" => DecodePairs(bytes, PairEncoding.ShiftJis, fatal),
        "EUC-KR" => DecodePairs(bytes, PairEncoding.EucKr, fatal),
        "EUC-JP" => DecodeEucJp(bytes, fatal),
        "ISO-2022-JP" => Iso2022JpDecoder.Decode(bytes, fatal),
        _ => throw new NotSupportedException($"No decoder implemented for {name}.")
    };

    private enum PairEncoding { Big5, ShiftJis, EucKr }

    private static string DecodePairs(ReadOnlySpan<byte> bytes, PairEncoding kind, bool fatal)
    {
        var lead = 0;
        return DecodePairChunk(bytes, kind, fatal, ref lead, flush: true);
    }

    internal static bool IsPairEncoding(string name) =>
        name is "Big5" or "Shift_JIS" or "EUC-KR";

    internal static bool IsEucJpEncoding(string name) => name == "EUC-JP";

    internal static string DecodePairChunk(ReadOnlySpan<byte> bytes, string name, bool fatal, ref int lead, bool flush)
    {
        var kind = name switch
        {
            "Big5" => PairEncoding.Big5,
            "Shift_JIS" => PairEncoding.ShiftJis,
            "EUC-KR" => PairEncoding.EucKr,
            _ => throw new NotSupportedException($"No pair decoder implemented for {name}.")
        };
        return DecodePairChunk(bytes, kind, fatal, ref lead, flush);
    }

    private static string DecodePairChunk(ReadOnlySpan<byte> bytes, PairEncoding kind, bool fatal, ref int lead, bool flush)
    {
        var output = new StringBuilder();
        foreach (var b in bytes)
        {
            if (lead != 0)
            {
                var pointer = PairPointer(lead, b, kind);
                lead = 0;
                var decoded = PairCodePoints(pointer, kind);
                if (decoded is not null)
                {
                    output.Append(decoded);
                    continue;
                }

                output.Append(WebEncoding.Error(fatal));
                if (b >= 0x80)
                {
                    continue;
                }

                // ASCII trails are restored to the input queue after an error.
            }

            if (b < 0x80 || kind == PairEncoding.ShiftJis && b == 0x80)
            {
                output.Append((char)b);
            }
            else if (kind == PairEncoding.ShiftJis && b is >= 0xA1 and <= 0xDF)
            {
                output.Append((char)(0xFF61 + b - 0xA1));
            }
            else if (kind == PairEncoding.ShiftJis
                ? b is >= 0x81 and <= 0x9F or >= 0xE0 and <= 0xFC
                : b is >= 0x81 and <= 0xFE)
            {
                lead = b;
            }
            else
            {
                output.Append(WebEncoding.Error(fatal));
            }
        }

        if (flush && lead != 0)
        {
            lead = 0;
            output.Append(WebEncoding.Error(fatal));
        }

        return output.ToString();
    }

    private static int PairPointer(int lead, int trail, PairEncoding kind) => kind switch
    {
        PairEncoding.Big5 when trail is >= 0x40 and <= 0x7E or >= 0xA1 and <= 0xFE =>
            (lead - 0x81) * 157 + trail - (trail < 0x7F ? 0x40 : 0x62),
        PairEncoding.ShiftJis when trail is >= 0x40 and <= 0x7E or >= 0x80 and <= 0xFC =>
            (lead - (lead < 0xA0 ? 0x81 : 0xC1)) * 188 + trail - (trail < 0x7F ? 0x40 : 0x41),
        PairEncoding.EucKr when trail is >= 0x41 and <= 0xFE => (lead - 0x81) * 190 + trail - 0x41,
        _ => -1
    };

    private static string? PairCodePoints(int pointer, PairEncoding kind)
    {
        if (kind == PairEncoding.Big5)
        {
            var special = pointer switch
            {
                1133 => "\u00CA\u0304",
                1135 => "\u00CA\u030C",
                1164 => "\u00EA\u0304",
                1166 => "\u00EA\u030C",
                _ => null
            };
            if (special is not null)
            {
                return special;
            }
        }

        if (kind == PairEncoding.ShiftJis && pointer is >= 8836 and <= 10715)
        {
            return char.ConvertFromUtf32(0xE000 + pointer - 8836);
        }

        return Lookup(kind switch
        {
            PairEncoding.Big5 => "big5",
            PairEncoding.ShiftJis => "jis0208",
            _ => "euc-kr"
        }, pointer);
    }

    internal static string? Lookup(string index, int pointer)
    {
        var table = WebEncoding.Indexes[index];
        return pointer >= 0 && pointer < table.Length && table[pointer] is { } point ? char.ConvertFromUtf32(point) : null;
    }

    private static string DecodeEucJp(ReadOnlySpan<byte> bytes, bool fatal)
    {
        var lead = 0;
        var jis0212 = false;
        return DecodeEucJpChunk(bytes, fatal, ref lead, ref jis0212, flush: true);
    }

    internal static string DecodeEucJpChunk(ReadOnlySpan<byte> bytes, bool fatal, ref int lead, ref bool jis0212, bool flush)
    {
        var output = new StringBuilder();
        foreach (var b in bytes)
        {
            if (lead == 0x8E && b is >= 0xA1 and <= 0xDF)
            {
                lead = 0;
                output.Append((char)(0xFF61 + b - 0xA1));
                continue;
            }

            if (lead == 0x8F && b is >= 0xA1 and <= 0xFE)
            {
                jis0212 = true;
                lead = b;
                continue;
            }

            if (lead != 0)
            {
                var decoded = lead is >= 0xA1 and <= 0xFE && b is >= 0xA1 and <= 0xFE
                    ? Lookup(jis0212 ? "jis0212" : "jis0208", (lead - 0xA1) * 94 + b - 0xA1) : null;
                lead = 0;
                jis0212 = false;
                output.Append(decoded ?? WebEncoding.Error(fatal));
                if (decoded is not null || b >= 0x80)
                {
                    continue;
                }
            }

            if (b < 0x80)
            {
                output.Append((char)b);
            }
            else if (b is 0x8E or 0x8F or >= 0xA1 and <= 0xFE)
            {
                lead = b;
            }
            else
            {
                output.Append(WebEncoding.Error(fatal));
            }
        }

        if (flush && lead != 0)
        {
            lead = 0;
            jis0212 = false;
            output.Append(WebEncoding.Error(fatal));
        }

        return output.ToString();
    }

    private static string DecodeGb(ReadOnlySpan<byte> bytes, bool fatal)
    {
        var first = 0;
        var second = 0;
        var third = 0;
        return DecodeGbChunk(bytes, fatal, ref first, ref second, ref third, flush: true);
    }

    internal static bool IsGbEncoding(string name) => name is "GBK" or "gb18030";

    internal static string DecodeGbChunk(ReadOnlySpan<byte> bytes, bool fatal, ref int first, ref int second,
        ref int third, bool flush)
    {
        var output = new StringBuilder();
        foreach (var value in bytes)
        {
            DecodeGbByte(value, fatal, ref first, ref second, ref third, output);
        }

        if (flush && (first != 0 || second != 0 || third != 0))
        {
            first = second = third = 0;
            output.Append(WebEncoding.Error(fatal));
        }

        return output.ToString();
    }

    private static void DecodeGbByte(byte value, bool fatal, ref int first, ref int second, ref int third,
        StringBuilder output)
    {
        if (third != 0)
        {
            if (value is < 0x30 or > 0x39)
            {
                var secondByte = (byte)second;
                var thirdByte = (byte)third;
                first = second = third = 0;
                output.Append(WebEncoding.Error(fatal));
                DecodeGbByte(secondByte, fatal, ref first, ref second, ref third, output);
                DecodeGbByte(thirdByte, fatal, ref first, ref second, ref third, output);
                DecodeGbByte(value, fatal, ref first, ref second, ref third, output);
                return;
            }

            var pointer = (first - 0x81) * 12600 + (second - 0x30) * 1260
                + (third - 0x81) * 10 + value - 0x30;
            first = second = third = 0;
            output.Append(GbRangeCodePoint(pointer) is { } codePoint
                ? char.ConvertFromUtf32(codePoint) : WebEncoding.Error(fatal));
            return;
        }

        if (second != 0)
        {
            if (value is >= 0x81 and <= 0xFE)
            {
                third = value;
                return;
            }

            var secondByte = (byte)second;
            first = second = 0;
            output.Append(WebEncoding.Error(fatal));
            DecodeGbByte(secondByte, fatal, ref first, ref second, ref third, output);
            DecodeGbByte(value, fatal, ref first, ref second, ref third, output);
            return;
        }

        if (first != 0)
        {
            if (value is >= 0x30 and <= 0x39)
            {
                second = value;
                return;
            }

            var leading = first;
            first = 0;
            var offset = value < 0x7F ? 0x40 : 0x41;
            var pointer = value is >= 0x40 and <= 0x7E or >= 0x80 and <= 0xFE
                ? (leading - 0x81) * 190 + value - offset : -1;
            var mapped = pointer < 0 ? null : Lookup("gb18030", pointer);
            if (mapped is not null)
            {
                output.Append(mapped);
                return;
            }

            output.Append(WebEncoding.Error(fatal));
            if (value < 0x80)
            {
                DecodeGbByte(value, fatal, ref first, ref second, ref third, output);
            }

            return;
        }

        if (value < 0x80)
        {
            output.Append((char)value);
        }
        else if (value == 0x80)
        {
            output.Append('\u20AC');
        }
        else if (value is >= 0x81 and <= 0xFE)
        {
            first = value;
        }
        else
        {
            output.Append(WebEncoding.Error(fatal));
        }
    }

    private static int? GbRangeCodePoint(int pointer)
    {
        if (pointer is > 39419 and < 189000 or > 1237575)
        {
            return null;
        }

        if (pointer == 7457)
        {
            return 0xE7C7;
        }

        var low = 0;
        var high = GbRanges.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (GbRanges[middle].Pointer <= pointer)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return GbRanges[low].CodePoint + pointer - GbRanges[low].Pointer;
    }

    private static (int Pointer, int CodePoint)[] LoadGbRanges()
    {
        using var stream = WebEncoding.OpenData("indexes.json");
        using var data = JsonDocument.Parse(stream);
        return data.RootElement.GetProperty("gb18030-ranges").EnumerateArray()
            .Select(pair => (pair[0].GetInt32(), pair[1].GetInt32())).ToArray();
    }
}
