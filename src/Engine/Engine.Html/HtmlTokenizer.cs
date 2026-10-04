using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace VisualWeb.Engine.Html;

/// <summary>Whole-string HTML tokenization with explicit tree-selected text modes.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#tokenization">tokenization</see>.
/// Offsets refer to the CR/CRLF-normalized UTF-16 input, not source byte positions.</remarks>
public sealed class HtmlTokenizer
{
    private static readonly IReadOnlyDictionary<string, string> Entities = LoadEntities();
    private static readonly int MaxEntityLength = Entities.Keys.Max(key => key.Length);
    private readonly string input;
    private readonly HtmlParserOptions options;
    private readonly CancellationToken cancellationToken;
    private readonly List<HtmlParseError> errors = [];
    private int position;
    private HtmlTextMode mode;
    private string? lastStartTag;
    private ScriptState scriptState = ScriptState.Data;
    public IReadOnlyList<HtmlParseError> Errors { get; }
    public int Offset => position;

    public HtmlTokenizer(string input, HtmlTextMode mode = HtmlTextMode.Data, string? lastStartTag = null,
        HtmlParserOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(mode)) { throw new ArgumentOutOfRangeException(nameof(mode)); }
        cancellationToken.ThrowIfCancellationRequested();
        this.options = options ?? new();
        this.options.Validate();
        if (input.Length > this.options.MaxInputCharacters)
        {
            throw new HtmlLimitException("HTML input exceeds the configured character limit.");
        }

        this.input = input.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        this.mode = mode;
        this.lastStartTag = lastStartTag;
        this.cancellationToken = cancellationToken;
        Errors = errors.AsReadOnly();
        CheckInputCharacters();
    }

    public void SetTextMode(HtmlTextMode textMode, string? appropriateEndTag = null)
    {
        if (!Enum.IsDefined(textMode)) { throw new ArgumentOutOfRangeException(nameof(textMode)); }
        mode = textMode;
        lastStartTag = appropriateEndTag;
        scriptState = ScriptState.Data;
    }

    public HtmlToken Read()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = new StringBuilder();
        while (position < input.Length)
        {
            CheckCancellation();
            var c = input[position];
            if (mode == HtmlTextMode.Cdata)
            {
                if (Starts("]]>"))
                {
                    position += 3;
                    mode = HtmlTextMode.Data;
                    continue;
                }

                text.Append(c);
                position++;
                continue;
            }

            if (mode == HtmlTextMode.ScriptData)
            {
                if (ScriptCharacter(text))
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    return new HtmlCharacters(text.ToString());
                }

                var endTag = ParseTag(true);
                mode = HtmlTextMode.Data;
                return endTag ?? (HtmlToken)new HtmlEndOfFile();
            }

            var markup = mode == HtmlTextMode.Data ? c == '<' : mode is HtmlTextMode.Rcdata or HtmlTextMode.Rawtext
                && AppropriateEndTag(position);
            if (markup)
            {
                if (text.Length > 0)
                {
                    return new HtmlCharacters(text.ToString());
                }

                if (mode != HtmlTextMode.Data)
                {
                    var endTag = ParseTag(true);
                    mode = HtmlTextMode.Data;
                    return endTag ?? (HtmlToken)new HtmlEndOfFile();
                }

                if (position + 1 == input.Length)
                {
                    Error("eof-before-tag-name");
                    position++;
                    return new HtmlCharacters("<");
                }

                var next = input[position + 1];
                if (char.IsAsciiLetter(next))
                {
                    var tag = ParseTag(false);
                    if (tag is not null)
                    {
                        lastStartTag = tag.Name;
                        return tag;
                    }

                    return new HtmlEndOfFile();
                }

                if (Starts("</"))
                {
                    if (position + 2 == input.Length)
                    {
                        Error("eof-before-tag-name");
                        position += 2;
                        return new HtmlCharacters("</");
                    }

                    if (char.IsAsciiLetter(input[position + 2]))
                    {
                        return ParseTag(true) ?? (HtmlToken)new HtmlEndOfFile();
                    }

                    position += 2;
                    if (input[position] == '>')
                    {
                        Error("missing-end-tag-name");
                        position++;
                        continue;
                    }

                    Error("invalid-first-character-of-tag-name");
                    return BogusComment();
                }

                if (Starts("<!--"))
                {
                    position += 4;
                    return Comment();
                }

                if (Starts("<!DOCTYPE", ignoreCase: true))
                {
                    position += 9;
                    return Doctype();
                }

                if (next == '?')
                {
                    position += 2;
                    return ProcessingInstruction();
                }

                if (Starts("<!"))
                {
                    Error(Starts("<![CDATA[") ? "cdata-in-html-content" : "incorrectly-opened-comment");
                    position += 2;
                    return BogusComment();
                }

                Error("invalid-first-character-of-tag-name");
                text.Append('<');
                position++;
                continue;
            }

            if (c == '&' && mode is HtmlTextMode.Data or HtmlTextMode.Rcdata)
            {
                text.Append(CharacterReference(attribute: false));
                continue;
            }

            if (c == '\0')
            {
                Error("unexpected-null-character");
                text.Append(mode == HtmlTextMode.Data ? '\0' : '\uFFFD');
            }
            else
            {
                text.Append(c);
            }

            position++;
        }

        if (mode == HtmlTextMode.Cdata)
        {
            Error("eof-in-cdata");
            mode = HtmlTextMode.Data;
        }

        if (mode == HtmlTextMode.ScriptData && scriptState != ScriptState.Data)
        {
            Error("eof-in-script-html-comment-like-text");
            scriptState = ScriptState.Data;
        }

        return text.Length == 0 ? new HtmlEndOfFile() : new HtmlCharacters(text.ToString());
    }

    private HtmlTag? ParseTag(bool end)
    {
        position += end ? 2 : 1;
        var name = new StringBuilder();
        while (position < input.Length && !Whitespace(input[position]) && input[position] is not ('/' or '>'))
        {
            CheckCancellation();
            name.Append(Lower(ReplaceNull(input[position++])));
        }

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var selfClosing = false;
        while (position < input.Length)
        {
            SkipWhitespace();
            if (position == input.Length)
            {
                break;
            }

            var c = input[position];
            if (c == '>')
            {
                position++;
                if (end && attributes.Count > 0)
                {
                    Error("end-tag-with-attributes");
                }

                if (end && selfClosing)
                {
                    Error("end-tag-with-trailing-solidus");
                }

                return new(name.ToString(), new ReadOnlyDictionary<string, string>(attributes), end, selfClosing);
            }

            if (c == '/')
            {
                position++;
                if (position < input.Length && input[position] == '>')
                {
                    selfClosing = true;
                    continue;
                }

                if (position < input.Length)
                {
                    Error("unexpected-solidus-in-tag");
                }

                continue;
            }

            var attributeName = new StringBuilder();
            if (c == '=')
            {
                Error("unexpected-equals-sign-before-attribute-name");
                attributeName.Append('=');
                position++;
            }

            while (position < input.Length && !Whitespace(input[position]) && input[position] is not ('/' or '>' or '='))
            {
                CheckCancellation();
                c = input[position++];
                if (c is '"' or '\'' or '<')
                {
                    Error("unexpected-character-in-attribute-name");
                }

                attributeName.Append(Lower(ReplaceNull(c)));
            }

            var key = attributeName.ToString();
            var duplicate = attributes.ContainsKey(key);
            if (duplicate)
            {
                Error("duplicate-attribute");
            }

            SkipWhitespace();
            var value = new StringBuilder();
            if (position < input.Length && input[position] == '=')
            {
                position++;
                SkipWhitespace();
                if (position < input.Length && input[position] is '"' or '\'')
                {
                    var quote = input[position++];
                    while (position < input.Length && input[position] != quote)
                    {
                        CheckCancellation();
                        value.Append(input[position] == '&' ? CharacterReference(attribute: true)
                            : ReplaceNull(input[position++]).ToString());
                    }

                    if (position < input.Length)
                    {
                        position++;
                        if (position < input.Length && !Whitespace(input[position]) && input[position] is not ('/' or '>'))
                        {
                            Error("missing-whitespace-between-attributes");
                        }
                    }
                }
                else
                {
                    if (position < input.Length && input[position] == '>')
                    {
                        Error("missing-attribute-value");
                    }

                    while (position < input.Length && !Whitespace(input[position]) && input[position] != '>')
                    {
                        CheckCancellation();
                        if (input[position] is '"' or '\'' or '<' or '=' or '`')
                        {
                            Error("unexpected-character-in-unquoted-attribute-value");
                        }

                        value.Append(input[position] == '&' ? CharacterReference(attribute: true)
                            : ReplaceNull(input[position++]).ToString());
                    }
                }
            }

            if (!duplicate)
            {
                attributes.Add(key, value.ToString());
            }
        }

        Error("eof-in-tag");
        return null;
    }

    private string CharacterReference(bool attribute)
    {
        var start = position++;
        if (position == input.Length)
        {
            return "&";
        }

        if (input[position] == '#')
        {
            position++;
            var hex = position < input.Length && input[position] is 'x' or 'X';
            if (hex)
            {
                position++;
            }

            var digits = position;
            long number = 0;
            while (position < input.Length && Digit(input[position], hex) is >= 0 and var digit)
            {
                CheckCancellation();
                number = Math.Min(0x110000, number * (hex ? 16 : 10) + digit);
                position++;
            }

            if (digits == position)
            {
                Error("absence-of-digits-in-numeric-character-reference");
                return input[start..position];
            }

            if (position < input.Length && input[position] == ';')
            {
                position++;
            }
            else
            {
                Error("missing-semicolon-after-character-reference");
            }

            if (number == 0 || number > 0x10FFFF || number is >= 0xD800 and <= 0xDFFF)
            {
                Error(number == 0 ? "null-character-reference" : number > 0x10FFFF
                    ? "character-reference-outside-unicode-range" : "surrogate-character-reference");
                return "\uFFFD";
            }

            if (number is >= 0xFDD0 and <= 0xFDEF || (number & 0xFFFF) is 0xFFFE or 0xFFFF)
            {
                Error("noncharacter-character-reference");
            }

            if (number == 13 || number is >= 1 and <= 8 or 11 or >= 14 and <= 31 or >= 0x7F and <= 0x9F)
            {
                Error("control-character-reference");
                if (number is >= 0x80 and <= 0x9F)
                {
                    number = C1Replacements[(int)number - 0x80];
                }
            }

            return char.ConvertFromUtf32((int)number);
        }

        string? match = null;
        var length = 0;
        for (var count = 1; count <= MaxEntityLength && position + count <= input.Length; count++)
        {
            var key = input.Substring(position, count);
            if (Entities.TryGetValue(key, out var value))
            {
                match = value;
                length = count;
            }

            if (!char.IsAsciiLetterOrDigit(input[position + count - 1]))
            {
                break;
            }
        }

        if (match is not null)
        {
            var semicolon = input[position + length - 1] == ';';
            var after = position + length;
            if (attribute && !semicolon && after < input.Length
                && (char.IsAsciiLetterOrDigit(input[after]) || input[after] == '='))
            {
                return "&";
            }

            position += length;
            if (!semicolon)
            {
                Error("missing-semicolon-after-character-reference");
            }

            return match;
        }

        var probe = position;
        while (probe < input.Length && char.IsAsciiLetterOrDigit(input[probe]))
        {
            probe++;
        }

        if (probe > position && probe < input.Length && input[probe] == ';')
        {
            Error("unknown-named-character-reference");
        }

        return "&";
    }

    private HtmlComment BogusComment()
    {
        var data = new StringBuilder();
        while (position < input.Length && input[position] != '>')
        {
            CheckCancellation();
            data.Append(ReplaceNull(input[position++]));
        }

        if (position < input.Length)
        {
            position++;
        }

        return new(data.ToString());
    }

    private HtmlToken ProcessingInstruction()
    {
        var target = new StringBuilder();
        if (position == input.Length)
        {
            Error("eof-in-processing-instruction");
            return new HtmlEndOfFile();
        }

        if (!char.IsAsciiLetter(input[position]) && input[position] != '_')
        {
            Error("invalid-first-character-of-processing-instruction-target");
            var bogus = BogusComment();
            return new HtmlComment("?" + bogus.Data);
        }

        while (position < input.Length && !Whitespace(input[position]) && input[position] is not ('?' or '>'))
        {
            CheckCancellation();
            var c = input[position];
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
            {
                Error("invalid-processing-instruction-target");
                var bogus = BogusComment();
                return new HtmlComment("?" + target + bogus.Data);
            }

            target.Append(c);
            position++;
        }

        if (position == input.Length)
        {
            Error("eof-in-processing-instruction");
            return new HtmlEndOfFile();
        }

        var name = target.ToString();
        if (name.Equals("xml", StringComparison.OrdinalIgnoreCase) || name.Equals("xml-stylesheet", StringComparison.OrdinalIgnoreCase))
        {
            Error("disallowed-processing-instruction-target");
            var bogus = BogusComment();
            return new HtmlComment("?" + name + bogus.Data);
        }

        SkipWhitespace();
        var data = new StringBuilder();
        while (position < input.Length)
        {
            CheckCancellation();
            var c = input[position++];
            if (c == '>') { return new HtmlProcessingInstruction(name, data.ToString()); }
            if (c == '?' && position < input.Length && input[position] == '>')
            {
                position++;
                return new HtmlProcessingInstruction(name, data.ToString());
            }

            data.Append(c);
        }

        Error("eof-in-processing-instruction");
        return new HtmlEndOfFile();
    }

    private HtmlComment Comment()
    {
        var data = new StringBuilder();
        var state = "start";
        while (true)
        {
            CheckCancellation();
            var c = position < input.Length ? input[position++] : -1;
            switch (state)
            {
                case "start":
                    if (c == '-') { state = "start-dash"; break; }
                    if (c == '>') { Error("abrupt-closing-of-empty-comment"); return new(""); }
                    state = "comment";
                    Reconsume(c);
                    break;
                case "start-dash":
                    if (c == '-') { state = "end"; break; }
                    if (c == '>') { Error("abrupt-closing-of-empty-comment"); return new(""); }
                    if (c == -1) { Error("eof-in-comment"); return new(data.ToString()); }
                    data.Append('-');
                    state = "comment";
                    Reconsume(c);
                    break;
                case "comment":
                    if (c == '<') { data.Append('<'); state = "less"; break; }
                    if (c == '-') { state = "end-dash"; break; }
                    if (c == -1) { Error("eof-in-comment"); return new(data.ToString()); }
                    data.Append(ReplaceNull((char)c));
                    break;
                case "less":
                    if (c == '!') { data.Append('!'); state = "less-bang"; break; }
                    if (c == '<') { data.Append('<'); break; }
                    state = "comment";
                    Reconsume(c);
                    break;
                case "less-bang":
                    if (c == '-') { state = "less-bang-dash"; break; }
                    state = "comment";
                    Reconsume(c);
                    break;
                case "less-bang-dash":
                    if (c == '-') { state = "less-bang-dash-dash"; break; }
                    state = "end-dash";
                    Reconsume(c);
                    break;
                case "less-bang-dash-dash":
                    if (c is not ('>' or -1)) { Error("nested-comment"); }
                    state = "end";
                    Reconsume(c);
                    break;
                case "end-dash":
                    if (c == '-') { state = "end"; break; }
                    if (c == -1) { Error("eof-in-comment"); return new(data.ToString()); }
                    data.Append('-');
                    state = "comment";
                    Reconsume(c);
                    break;
                case "end":
                    if (c == '>') { return new(data.ToString()); }
                    if (c == '!') { state = "end-bang"; break; }
                    if (c == '-') { data.Append('-'); break; }
                    if (c == -1) { Error("eof-in-comment"); return new(data.ToString()); }
                    data.Append("--");
                    state = "comment";
                    Reconsume(c);
                    break;
                case "end-bang":
                    if (c == '>') { Error("incorrectly-closed-comment"); return new(data.ToString()); }
                    if (c == -1) { Error("eof-in-comment"); return new(data.ToString()); }
                    data.Append("--!");
                    state = c == '-' ? "end-dash" : "comment";
                    if (c != '-') { Reconsume(c); }
                    break;
            }
        }
    }

    private HtmlDoctype Doctype()
    {
        var quirks = false;
        string? name = null;
        string? publicId = null;
        string? systemId = null;
        if (position == input.Length)
        {
            Error("eof-in-doctype");
            return new(null, null, null, true);
        }

        if (!Whitespace(input[position]) && input[position] != '>')
        {
            Error("missing-whitespace-before-doctype-name");
        }

        SkipWhitespace();
        if (position == input.Length || input[position] == '>')
        {
            Error(position == input.Length ? "eof-in-doctype" : "missing-doctype-name");
            if (position < input.Length) { position++; }
            return new(null, null, null, true);
        }

        var buffer = new StringBuilder();
        while (position < input.Length && !Whitespace(input[position]) && input[position] != '>')
        {
            CheckCancellation();
            buffer.Append(Lower(ReplaceNull(input[position++])));
        }

        name = buffer.ToString();
        SkipWhitespace();
        if (position == input.Length)
        {
            Error("eof-in-doctype");
            return new(name, null, null, true);
        }

        if (input[position] == '>') { position++; return new(name, null, null, false); }
        var isPublic = Starts("PUBLIC", ignoreCase: true);
        var isSystem = Starts("SYSTEM", ignoreCase: true);
        if (!isPublic && !isSystem)
        {
            Error("invalid-character-sequence-after-doctype-name");
            BogusDoctype();
            return new(name, null, null, true);
        }

        position += 6;
        if (position < input.Length && !Whitespace(input[position]) && input[position] is '"' or '\'')
        {
            Error(isPublic ? "missing-whitespace-after-doctype-public-keyword" : "missing-whitespace-after-doctype-system-keyword");
        }

        SkipWhitespace();
        if (isPublic)
        {
            publicId = QuotedIdentifier("public", ref quirks);
        }
        else
        {
            systemId = QuotedIdentifier("system", ref quirks);
        }

        if (quirks)
        {
            BogusDoctype();
            return new(name, publicId, systemId, true);
        }

        var hadWhitespace = position < input.Length && Whitespace(input[position]);
        SkipWhitespace();
        if (isPublic && position < input.Length && input[position] is '"' or '\'')
        {
            if (!hadWhitespace) { Error("missing-whitespace-between-doctype-public-and-system-identifiers"); }
            systemId = QuotedIdentifier("system", ref quirks);
            SkipWhitespace();
            if (quirks)
            {
                BogusDoctype();
                return new(name, publicId, systemId, true);
            }
        }

        if (position == input.Length)
        {
            Error("eof-in-doctype");
            quirks = true;
        }
        else if (input[position] != '>')
        {
            Error(isPublic && systemId is null ? "missing-quote-before-doctype-system-identifier" : "unexpected-character-after-doctype-system-identifier");
            quirks |= isPublic && systemId is null;
        }

        BogusDoctype();
        return new(name, publicId, systemId, quirks);
    }

    private string? QuotedIdentifier(string kind, ref bool quirks)
    {
        if (position == input.Length || input[position] is not ('"' or '\''))
        {
            Error(position == input.Length ? "eof-in-doctype" : input[position] == '>'
                ? $"missing-doctype-{kind}-identifier" : $"missing-quote-before-doctype-{kind}-identifier");
            quirks = true;
            return null;
        }

        var quote = input[position++];
        var data = new StringBuilder();
        while (position < input.Length && input[position] != quote && input[position] != '>')
        {
            CheckCancellation();
            data.Append(ReplaceNull(input[position++]));
        }

        if (position == input.Length || input[position] == '>')
        {
            Error(position == input.Length ? "eof-in-doctype" : $"abrupt-doctype-{kind}-identifier");
            quirks = true;
        }
        else
        {
            position++;
        }

        return data.ToString();
    }

    private void BogusDoctype()
    {
        while (position < input.Length && input[position] != '>')
        {
            CheckCancellation();
            if (input[position++] == '\0') { Error("unexpected-null-character"); }
        }

        if (position < input.Length) { position++; }
    }

    private bool AppropriateEndTag(int at)
    {
        if (lastStartTag is null || at + 2 + lastStartTag.Length >= input.Length
            || !lastStartTag.All(char.IsAsciiLetter)
            || !input.AsSpan(at).StartsWith("</", StringComparison.Ordinal))
        {
            return false;
        }

        var name = input.AsSpan(at + 2, lastStartTag.Length);
        for (var index = 0; index < name.Length; index++)
        {
            if (!char.IsAsciiLetter(name[index]) || Lower(name[index]) != Lower(lastStartTag[index])) { return false; }
        }

        return Whitespace(input[at + 2 + name.Length]) || input[at + 2 + name.Length] is '/' or '>';
    }

    private enum ScriptState { Data, Escaped, EscapedDash, EscapedDashDash, Double, DoubleDash, DoubleDashDash }
    private bool ScriptCharacter(StringBuilder output)
    {
        var c = input[position];
        if (scriptState is ScriptState.Data or ScriptState.Escaped or ScriptState.EscapedDash or ScriptState.EscapedDashDash
            && AppropriateEndTag(position))
        {
            return false;
        }

        if (scriptState == ScriptState.Data && Starts("<!--"))
        {
            output.Append("<!--");
            position += 4;
            scriptState = ScriptState.EscapedDashDash;
            return true;
        }

        if (scriptState is ScriptState.Escaped or ScriptState.EscapedDash or ScriptState.EscapedDashDash
            && Starts("<script", ignoreCase: true) && ScriptDelimiter(position + 7))
        {
            output.Append(input.AsSpan(position, 7));
            position += 7;
            scriptState = ScriptState.Double;
            return true;
        }

        if (scriptState is ScriptState.Double or ScriptState.DoubleDash or ScriptState.DoubleDashDash
            && Starts("</script", ignoreCase: true) && ScriptDelimiter(position + 8))
        {
            output.Append(input.AsSpan(position, 8));
            position += 8;
            scriptState = ScriptState.Escaped;
            return true;
        }

        scriptState = (scriptState, c) switch
        {
            (ScriptState.Escaped, '-') => ScriptState.EscapedDash,
            (ScriptState.EscapedDash or ScriptState.EscapedDashDash, '-') => ScriptState.EscapedDashDash,
            (ScriptState.EscapedDashDash or ScriptState.DoubleDashDash, '>') => ScriptState.Data,
            (ScriptState.EscapedDash or ScriptState.EscapedDashDash, _) => ScriptState.Escaped,
            (ScriptState.Double, '-') => ScriptState.DoubleDash,
            (ScriptState.DoubleDash or ScriptState.DoubleDashDash, '-') => ScriptState.DoubleDashDash,
            (ScriptState.DoubleDash or ScriptState.DoubleDashDash, _) => ScriptState.Double,
            _ => scriptState
        };
        output.Append(ReplaceNull(c));
        position++;
        return true;
    }

    private bool ScriptDelimiter(int at) => at < input.Length && (Whitespace(input[at]) || input[at] is '/' or '>');
    private bool Starts(string value, bool ignoreCase = false)
    {
        if (!ignoreCase) { return input.AsSpan(position).StartsWith(value, StringComparison.Ordinal); }
        if (position + value.Length > input.Length) { return false; }
        for (var index = 0; index < value.Length; index++)
        {
            if (Lower(input[position + index]) != Lower(value[index])) { return false; }
        }

        return true;
    }
    private void SkipWhitespace()
    {
        while (position < input.Length && Whitespace(input[position]))
        {
            CheckCancellation();
            position++;
        }
    }

    internal static bool Whitespace(char c) => c is '\t' or '\n' or '\f' or '\r' or ' ';
    private static char Lower(char c) => c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
    private char ReplaceNull(char c)
    {
        if (c != '\0') { return c; }
        Error("unexpected-null-character");
        return '\uFFFD';
    }

    private static int Digit(char c, bool hex) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' when hex => c - 'a' + 10,
        >= 'A' and <= 'F' when hex => c - 'A' + 10,
        _ => -1
    };
    private void Reconsume(int c) { if (c != -1) { position--; } }
    private void CheckCancellation() => cancellationToken.ThrowIfCancellationRequested();
    private void Error(string code)
    {
        if (errors.Count == options.MaxErrors) { throw new HtmlLimitException("HTML parse-error limit exceeded."); }
        errors.Add(new(code, position));
    }

    private static IReadOnlyDictionary<string, string> LoadEntities()
    {
        using var stream = typeof(HtmlTokenizer).Assembly.GetManifestResourceStream("VisualWeb.Engine.Html.Data.entities.json")
            ?? throw new InvalidOperationException("Embedded HTML named references are missing.");
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.EnumerateObject().ToDictionary(e => e.Name[1..],
            e => e.Value.GetProperty("characters").GetString()!, StringComparer.Ordinal);
    }

    private void CheckInputCharacters()
    {
        for (var at = 0; at < input.Length; at++)
        {
            if ((at & 4095) == 0) { cancellationToken.ThrowIfCancellationRequested(); }
            var point = (int)input[at];
            string? error = null;
            if (char.IsHighSurrogate(input[at]) && at + 1 < input.Length && char.IsLowSurrogate(input[at + 1]))
            {
                point = char.ConvertToUtf32(input[at], input[++at]);
            }
            else if (char.IsSurrogate(input[at]))
            {
                error = "surrogate-in-input-stream";
            }

            if (point is >= 0xFDD0 and <= 0xFDEF || (point & 0xFFFF) is 0xFFFE or 0xFFFF)
            {
                error = "noncharacter-in-input-stream";
            }
            else if (point is >= 1 and <= 8 or 11 or >= 14 and <= 31 or >= 0x7F and <= 0x9F)
            {
                error = "control-character-in-input-stream";
            }

            if (error is not null)
            {
                if (errors.Count == options.MaxErrors) { throw new HtmlLimitException("HTML parse-error limit exceeded."); }
                errors.Add(new(error, at));
            }
        }
    }

    private static readonly int[] C1Replacements =
    [
        0x20AC, 0x81, 0x201A, 0x192, 0x201E, 0x2026, 0x2020, 0x2021,
        0x2C6, 0x2030, 0x160, 0x2039, 0x152, 0x8D, 0x17D, 0x8F,
        0x90, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014,
        0x2DC, 0x2122, 0x161, 0x203A, 0x153, 0x9D, 0x17E, 0x178
    ];
}
