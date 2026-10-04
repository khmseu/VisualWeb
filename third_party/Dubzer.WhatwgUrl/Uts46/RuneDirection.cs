using System;
using System.Text;

namespace Dubzer.WhatwgUrl.Uts46;

internal static class RuneDirection
{
    private readonly struct DirectionDataComparable(int value) : IComparable<DirectionData>
    {
        public int CompareTo(DirectionData other) => value.CompareTo(other.RangeEnd);
    }

    internal static Direction GetDirection(this Rune rune)
    {
        var codepoint = rune.Value;
        var index = UnicodeTables.DirectionTable.AsSpan().BinarySearch(new DirectionDataComparable(codepoint));

        if (index > -1)
            return UnicodeTables.DirectionTable[index].Direction;

        var nearest = ~index;
        if (nearest < UnicodeTables.DirectionTable.Length &&
            UnicodeTables.DirectionTable[nearest].RangeStart <= rune.Value)
            return UnicodeTables.DirectionTable[nearest].Direction;

        // Default values for unlisted code points from Unicode 17 DerivedBidiClass.txt.
        // https://www.unicode.org/Public/17.0.0/ucd/extracted/DerivedBidiClass.txt
        return codepoint switch
        {
            >= 0x0590 and <= 0x05FF or >= 0x07C0 and <= 0x085F or >= 0xFB1D and <= 0xFB4F
                or >= 0x10800 and <= 0x10CFF or >= 0x10D40 and <= 0x10EBF
                or >= 0x10F00 and <= 0x10F2F or >= 0x10F70 and <= 0x10FFF
                or >= 0x1E800 and <= 0x1EC6F or >= 0x1ECC0 and <= 0x1ECFF
                or >= 0x1ED50 and <= 0x1EDFF or >= 0x1EF00 and <= 0x1EFFF => Direction.R,
            >= 0x0600 and <= 0x07BF or >= 0x0860 and <= 0x08FF
                or >= 0xFB50 and <= 0xFDCF or >= 0xFDF0 and <= 0xFDFF
                or >= 0xFE70 and <= 0xFEFF or >= 0x10D00 and <= 0x10D3F
                or >= 0x10EC0 and <= 0x10EFF or >= 0x10F30 and <= 0x10F6F
                or >= 0x1EC70 and <= 0x1ECBF or >= 0x1ED00 and <= 0x1ED4F
                or >= 0x1EE00 and <= 0x1EEFF => Direction.Al,
            >= 0x20A0 and <= 0x20CF => Direction.Et,
            _ => Direction.L
        };
    }
}
