using System.Text;

namespace VisualWeb.Core.Encoding;

internal static class Iso2022JpDecoder
{
    private enum State { Ascii, Roman, Katakana, Leading, Trailing, EscapeStart, Escape }

    public static string Decode(ReadOnlySpan<byte> bytes, bool fatal)
    {
        var output = new StringBuilder();
        var state = State.Ascii;
        var outputState = State.Ascii;
        var leading = 0;
        var outputFlag = false;
        for (var position = 0; position <= bytes.Length;)
        {
            var b = position == bytes.Length ? -1 : bytes[position++];
            switch (state)
            {
                case State.EscapeStart:
                    if (b is 0x24 or 0x28)
                    {
                        leading = b;
                        state = State.Escape;
                        continue;
                    }

                    if (b != -1)
                    {
                        position--;
                    }

                    state = outputState;
                    outputFlag = false;
                    output.Append(WebEncoding.Error(fatal));
                    continue;
                case State.Escape:
                    var target = (leading, b) switch
                    {
                        (0x28, 0x42) => State.Ascii,
                        (0x28, 0x4A) => State.Roman,
                        (0x28, 0x49) => State.Katakana,
                        (0x24, 0x40 or 0x42) => State.Leading,
                        _ => (State?)null
                    };
                    if (target is { } next)
                    {
                        state = outputState = next;
                        if (outputFlag)
                        {
                            output.Append(WebEncoding.Error(fatal));
                        }

                        outputFlag = true;
                    }
                    else
                    {
                        position -= b == -1 ? 1 : 2;
                        state = outputState;
                        outputFlag = false;
                        output.Append(WebEncoding.Error(fatal));
                    }

                    leading = 0;
                    continue;
                case State.Trailing:
                    state = b == 0x1B ? State.EscapeStart : State.Leading;
                    var mapped = b is >= 0x21 and <= 0x7E
                        ? LegacyDecoder.Lookup("jis0208", (leading - 0x21) * 94 + b - 0x21) : null;
                    output.Append(mapped ?? WebEncoding.Error(fatal));
                    if (b == -1)
                    {
                        return output.ToString();
                    }

                    continue;
                default:
                    if (b == -1)
                    {
                        return output.ToString();
                    }

                    if (b == 0x1B)
                    {
                        state = State.EscapeStart;
                        continue;
                    }

                    outputFlag = false;
                    switch (state)
                    {
                        case State.Ascii or State.Roman:
                            if (b < 0x80 && b is not (0x0E or 0x0F))
                            {
                                output.Append((char)(state == State.Roman ? b switch
                                {
                                    0x5C => 0xA5,
                                    0x7E => 0x203E,
                                    _ => b
                                } : b));
                            }
                            else
                            {
                                output.Append(WebEncoding.Error(fatal));
                            }

                            break;
                        case State.Katakana:
                            output.Append(b is >= 0x21 and <= 0x5F
                                ? ((char)(0xFF61 + b - 0x21)).ToString() : WebEncoding.Error(fatal));
                            break;
                        case State.Leading:
                            if (b is >= 0x21 and <= 0x7E)
                            {
                                leading = b;
                                state = State.Trailing;
                            }
                            else
                            {
                                output.Append(WebEncoding.Error(fatal));
                            }

                            break;
                    }

                    break;
            }
        }

        return output.ToString();
    }
}
