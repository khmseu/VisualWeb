using System.Text;

namespace VisualWeb.Core.Encoding;

internal static class Iso2022JpDecoder
{
    internal enum State { Ascii, Roman, Katakana, Leading, Trailing, EscapeStart, Escape }

    internal struct DecoderState
    {
        public State State;
        public State OutputState;
        public int Leading;
        public bool OutputFlag;
    }

    public static string Decode(ReadOnlySpan<byte> bytes, bool fatal)
    {
        var state = default(DecoderState);
        return DecodeChunk(bytes, fatal, ref state, flush: true);
    }

    public static string DecodeChunk(ReadOnlySpan<byte> bytes, bool fatal, ref DecoderState decoder, bool flush)
    {
        var output = new StringBuilder();
        Span<int> reconsume = stackalloc int[2];
        var reconsumeCount = 0;
        var position = 0;
        while (true)
        {
            int b;
            if (reconsumeCount > 0)
            {
                b = reconsume[0];
                if (reconsumeCount == 2)
                {
                    reconsume[0] = reconsume[1];
                }

                reconsumeCount--;
            }
            else if (position < bytes.Length)
            {
                b = bytes[position++];
            }
            else if (flush)
            {
                b = -1;
            }
            else
            {
                break;
            }

            switch (decoder.State)
            {
                case State.EscapeStart:
                    if (b is 0x24 or 0x28)
                    {
                        decoder.Leading = b;
                        decoder.State = State.Escape;
                        continue;
                    }

                    if (b != -1)
                    {
                        Prepend(reconsume, b, ref reconsumeCount);
                    }

                    decoder.State = decoder.OutputState;
                    decoder.OutputFlag = false;
                    output.Append(WebEncoding.Error(fatal));
                    continue;
                case State.Escape:
                    var target = (decoder.Leading, b) switch
                    {
                        (0x28, 0x42) => State.Ascii,
                        (0x28, 0x4A) => State.Roman,
                        (0x28, 0x49) => State.Katakana,
                        (0x24, 0x40 or 0x42) => State.Leading,
                        _ => (State?)null
                    };
                    var leading = decoder.Leading;
                    decoder.Leading = 0;
                    if (target is { } next)
                    {
                        decoder.State = decoder.OutputState = next;
                        if (decoder.OutputFlag)
                        {
                            output.Append(WebEncoding.Error(fatal));
                        }

                        decoder.OutputFlag = true;
                    }
                    else
                    {
                        if (b == -1)
                        {
                            Prepend(reconsume, leading, ref reconsumeCount);
                        }
                        else
                        {
                            Prepend(reconsume, b, ref reconsumeCount);
                            Prepend(reconsume, leading, ref reconsumeCount);
                        }

                        decoder.State = decoder.OutputState;
                        decoder.OutputFlag = false;
                        output.Append(WebEncoding.Error(fatal));
                    }

                    continue;
                case State.Trailing:
                    decoder.State = b == 0x1B ? State.EscapeStart : State.Leading;
                    var mapped = b is >= 0x21 and <= 0x7E
                        ? LegacyDecoder.Lookup("jis0208", (decoder.Leading - 0x21) * 94 + b - 0x21) : null;
                    output.Append(mapped ?? WebEncoding.Error(fatal));
                    if (b == -1)
                    {
                        break;
                    }

                    continue;
                default:
                    if (b == -1)
                    {
                        break;
                    }

                    if (b == 0x1B)
                    {
                        decoder.State = State.EscapeStart;
                        continue;
                    }

                    decoder.OutputFlag = false;
                    switch (decoder.State)
                    {
                        case State.Ascii or State.Roman:
                            if (b < 0x80 && b is not (0x0E or 0x0F))
                            {
                                output.Append((char)(decoder.State == State.Roman ? b switch
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
                                decoder.Leading = b;
                                decoder.State = State.Trailing;
                            }
                            else
                            {
                                output.Append(WebEncoding.Error(fatal));
                            }

                            break;
                    }

                    break;
            }

            if (b == -1)
            {
                break;
            }
        }

        return output.ToString();
    }

    private static void Prepend(Span<int> pending, int value, ref int count)
    {
        if (count == pending.Length)
        {
            throw new InvalidOperationException("ISO-2022-JP reconsume queue exceeded its bound.");
        }

        if (count == 1)
        {
            pending[1] = pending[0];
        }

        pending[0] = value;
        count++;
    }
}
