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
        var output = new StringBuilder();
        var lead = 0;
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

        if (lead != 0)
        {
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
        var output = new StringBuilder();
        var lead = 0;
        var jis0212 = false;
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

        if (lead != 0)
        {
            output.Append(WebEncoding.Error(fatal));
        }

        return output.ToString();
    }

    private static string DecodeGb(ReadOnlySpan<byte> bytes, bool fatal)
    {
        var output = new StringBuilder();
        for (var position = 0; position < bytes.Length;)
        {
            var first = bytes[position++];
            if (first < 0x80)
            {
                output.Append((char)first);
                continue;
            }

            if (first == 0x80)
            {
                output.Append('\u20AC');
                continue;
            }

            if (first == 0xFF || position == bytes.Length)
            {
                output.Append(WebEncoding.Error(fatal));
                continue;
            }

            var second = bytes[position++];
            if (second is >= 0x30 and <= 0x39)
            {
                if (position == bytes.Length)
                {
                    output.Append(WebEncoding.Error(fatal));
                    break;
                }

                if (bytes[position] is < 0x81 or > 0xFE)
                {
                    position--;
                    output.Append(WebEncoding.Error(fatal));
                    continue;
                }

                var third = bytes[position++];
                if (position == bytes.Length)
                {
                    output.Append(WebEncoding.Error(fatal));
                    break;
                }

                if (bytes[position] is < 0x30 or > 0x39)
                {
                    position -= 2;
                    output.Append(WebEncoding.Error(fatal));
                    continue;
                }

                var fourth = bytes[position++];
                var pointer = (first - 0x81) * 12600 + (second - 0x30) * 1260 + (third - 0x81) * 10 + fourth - 0x30;
                output.Append(GbRangeCodePoint(pointer) is { } point ? char.ConvertFromUtf32(point) : WebEncoding.Error(fatal));
                continue;
            }

            var mapped = second is >= 0x40 and <= 0x7E or >= 0x80 and <= 0xFE
                ? Lookup("gb18030", (first - 0x81) * 190 + second - (second < 0x7F ? 0x40 : 0x41)) : null;
            output.Append(mapped ?? WebEncoding.Error(fatal));
            if (mapped is null && second < 0x80)
            {
                position--;
            }
        }

        return output.ToString();
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
