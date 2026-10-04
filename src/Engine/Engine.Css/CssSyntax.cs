using System.Globalization;
using System.Text;

namespace VisualWeb.Engine.Css;

/// <summary>CSS Syntax tokens; offsets and source text refer to normalized UTF-16 input.</summary>
/// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#tokenization">tokenization</see>.
/// Number representation is retained even when outside the finite double range.</remarks>
public enum CssTokenKind
{
    Ident, Function, AtKeyword, Hash, String, BadString, Url, BadUrl, Delim,
    Number, Percentage, Dimension, Whitespace, Colon, Semicolon, Comma,
    OpenSquare, CloseSquare, OpenParen, CloseParen, OpenBrace, CloseBrace, Cdo, Cdc, Eof
}

/// <summary>A decoded token with its normalized spelling and Syntax flags.</summary>
/// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#tokenization">token types</see>.</remarks>
public sealed record CssToken(CssTokenKind Kind, string Value, int Offset, string Text,
    string Unit = "", bool IsId = false, bool IsInteger = false)
{
    public bool IsDelimiter(string value) => Kind == CssTokenKind.Delim && Value == value;
}

public sealed record CssDiagnostic(string Code, int Offset, string Message);

/// <summary>Positive resource limits for whole-string syntax, selector and style processing.</summary>
public sealed record CssOptions
{
    public int MaxInputCharacters { get; init; } = 4 * 1024 * 1024;
    public int MaxTokens { get; init; } = 1_000_000;
    public int MaxDepth { get; init; } = 128;
    public int MaxDiagnostics { get; init; } = 10_000;
    public int MaxElements { get; init; } = 100_000;
    public int MaxStyleSources { get; init; } = 256;
    public int MaxRules { get; init; } = 100_000;
    public int MaxAssignments { get; init; } = 1_000_000;
    public long MaxMatchOperations { get; init; } = 10_000_000;

    internal void Validate()
    {
        if (MaxInputCharacters <= 0 || MaxTokens <= 0 || MaxDepth <= 0 || MaxDiagnostics <= 0
            || MaxElements <= 0 || MaxStyleSources <= 0 || MaxRules <= 0 || MaxAssignments <= 0 || MaxMatchOperations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CssOptions), "All CSS limits must be positive.");
        }
    }
}

public sealed class CssLimitException(string message) : Exception(message);
public sealed class UnsupportedCssException(string message, int offset) : Exception(message)
{
    public int Offset { get; } = offset;
}

internal sealed class CssContext
{
    internal CssOptions Options { get; }
    internal CancellationToken Cancellation { get; }
    internal List<CssDiagnostic> Diagnostics { get; } = [];
    private long operations;
    private int matchDepth;
    private int assignments;

    internal CssContext(CssOptions? options, CancellationToken cancellation)
    {
        Options = options ?? new();
        Options.Validate();
        Cancellation = cancellation;
        cancellation.ThrowIfCancellationRequested();
    }

    internal void Error(string code, int offset, string message)
    {
        if (Diagnostics.Count >= Options.MaxDiagnostics) { throw new CssLimitException("CSS diagnostic limit exceeded."); }
        Diagnostics.Add(new(code, offset, message));
    }

    internal void MatchStep()
    {
        Cancellation.ThrowIfCancellationRequested();
        if (++operations > Options.MaxMatchOperations) { throw new CssLimitException("CSS selector operation limit exceeded."); }
    }

    internal void EnterMatch()
    {
        MatchStep();
        if (++matchDepth > Options.MaxDepth) { throw new CssLimitException("CSS selector evaluation depth limit exceeded."); }
    }
    internal void ExitMatch() => matchDepth--;
    internal void Assignment()
    {
        Cancellation.ThrowIfCancellationRequested();
        if (++assignments > Options.MaxAssignments) { throw new CssLimitException("CSS expanded assignment limit exceeded."); }
    }
}

/// <summary>First-party CSS Syntax tokenizer, excluding the optional Unicode-range token extension.</summary>
/// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#consume-token">consume a token</see>.</remarks>
public sealed class CssTokenizer
{
    private readonly string input;
    private readonly CssContext context;
    private int position;
    private int emitted;
    public IReadOnlyList<CssDiagnostic> Diagnostics => context.Diagnostics.AsReadOnly();

    public CssTokenizer(string input, CssOptions? options = null, CancellationToken cancellationToken = default)
        : this(input, new CssContext(options, cancellationToken)) { }

    internal CssTokenizer(string input, CssContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        this.context = context;
        if (input.Length > context.Options.MaxInputCharacters) { throw new CssLimitException("CSS input limit exceeded."); }
        var normalized = new StringBuilder(input.Length);
        for (var index = 0; index < input.Length; index++)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            var c = input[index];
            if (c == '\r')
            {
                if (index + 1 < input.Length && input[index + 1] == '\n') { index++; }
                normalized.Append('\n');
            }
            else if (c == '\f') { normalized.Append('\n'); }
            else if (c == '\0' || char.IsSurrogate(c)
                && !(char.IsHighSurrogate(c) && index + 1 < input.Length && char.IsLowSurrogate(input[index + 1])))
            {
                normalized.Append('\uFFFD');
            }
            else
            {
                normalized.Append(c);
                if (char.IsHighSurrogate(c)) { normalized.Append(input[++index]); }
            }
        }
        this.input = normalized.ToString();
    }

    public CssToken Read()
    {
        context.Cancellation.ThrowIfCancellationRequested();
        while (Starts("/*"))
        {
            position += 2;
            while (position < input.Length && !Starts("*/"))
            {
                context.Cancellation.ThrowIfCancellationRequested();
                position++;
            }
            if (position == input.Length) { context.Error("eof-in-comment", position, "Unclosed CSS comment."); }
            else { position += 2; }
        }
        var start = position;
        if (position == input.Length) { return new(CssTokenKind.Eof, "", position, ""); }
        if (++emitted > context.Options.MaxTokens) { throw new CssLimitException("CSS token limit exceeded."); }
        var c = Peek();
        if (Space(c))
        {
            while (Space(Peek())) { context.Cancellation.ThrowIfCancellationRequested(); position++; }
            return Token(CssTokenKind.Whitespace, " ");
        }
        if (c is '"' or '\'') { return String(c); }
        if (c == '#' && (Name(Peek(1)) || Escape(position + 1)))
        {
            position++;
            var id = StartsIdent(position);
            return Token(CssTokenKind.Hash, Ident(), id: id);
        }
        if (c == '@' && StartsIdent(position + 1))
        {
            position++;
            return Token(CssTokenKind.AtKeyword, Ident());
        }
        if (StartsNumber(position)) { return Numeric(); }
        if (Starts("<!--")) { position += 4; return Token(CssTokenKind.Cdo, "<!--"); }
        if (Starts("-->")) { position += 3; return Token(CssTokenKind.Cdc, "-->"); }
        if (StartsIdent(position))
        {
            var name = Ident();
            if (Peek() != '(') { return Token(CssTokenKind.Ident, name); }
            position++;
            if (CssText.Lower(name) != "url") { return Token(CssTokenKind.Function, name); }
            while (Space(Peek()) && Space(Peek(1))) { context.Cancellation.ThrowIfCancellationRequested(); position++; }
            if (Peek() is '"' or '\'' || Space(Peek()) && Peek(1) is '"' or '\'')
            {
                return Token(CssTokenKind.Function, name);
            }
            return Url();
        }
        position++;
        if (c == '\\') { context.Error("invalid-escape", start, "Backslash does not start a valid CSS escape."); }
        return Token(c switch
        {
            ':' => CssTokenKind.Colon,
            ';' => CssTokenKind.Semicolon,
            ',' => CssTokenKind.Comma,
            '[' => CssTokenKind.OpenSquare,
            ']' => CssTokenKind.CloseSquare,
            '(' => CssTokenKind.OpenParen,
            ')' => CssTokenKind.CloseParen,
            '{' => CssTokenKind.OpenBrace,
            '}' => CssTokenKind.CloseBrace,
            _ => CssTokenKind.Delim
        }, c.ToString());

        CssToken Token(CssTokenKind kind, string value, string unit = "", bool id = false, bool integer = false)
            => new(kind, value, start, input[start..position], unit, id, integer);

        CssToken String(char quote)
        {
            position++;
            var value = new StringBuilder();
            while (position < input.Length)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                var current = Peek();
                if (current == quote) { position++; return Token(CssTokenKind.String, value.ToString()); }
                if (current == '\n')
                {
                    context.Error("newline-in-string", position, "Unescaped newline in CSS string.");
                    return Token(CssTokenKind.BadString, "");
                }
                if (current == '\\')
                {
                    if (Peek(1) == '\n') { position += 2; }
                    else if (position + 1 == input.Length) { position++; }
                    else { value.Append(Escaped()); }
                }
                else { value.Append(current); position++; }
            }
            context.Error("eof-in-string", position, "Unclosed CSS string.");
            return Token(CssTokenKind.String, value.ToString());
        }

        CssToken Numeric()
        {
            if (Peek() is '+' or '-') { position++; }
            while (Digit(Peek())) { context.Cancellation.ThrowIfCancellationRequested(); position++; }
            var integer = true;
            if (Peek() == '.' && Digit(Peek(1)))
            {
                integer = false;
                position += 2;
                while (Digit(Peek())) { context.Cancellation.ThrowIfCancellationRequested(); position++; }
            }
            if (Peek() is 'e' or 'E' && (Digit(Peek(1)) || Peek(1) is '+' or '-' && Digit(Peek(2))))
            {
                integer = false;
                position++;
                if (Peek() is '+' or '-') { position++; }
                while (Digit(Peek())) { context.Cancellation.ThrowIfCancellationRequested(); position++; }
            }
            var number = input[start..position];
            if (StartsIdent(position)) { return Token(CssTokenKind.Dimension, number, Ident(), integer: integer); }
            if (Peek() == '%') { position++; return Token(CssTokenKind.Percentage, number, integer: integer); }
            return Token(CssTokenKind.Number, number, integer: integer);
        }

        CssToken Url()
        {
            var value = new StringBuilder();
            while (Space(Peek())) { context.Cancellation.ThrowIfCancellationRequested(); position++; }
            while (position < input.Length)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                var current = Peek();
                if (current == ')') { position++; return Token(CssTokenKind.Url, value.ToString()); }
                if (Space(current))
                {
                    while (Space(Peek())) { context.Cancellation.ThrowIfCancellationRequested(); position++; }
                    if (Peek() == ')') { position++; return Token(CssTokenKind.Url, value.ToString()); }
                    if (position == input.Length) { break; }
                    return BadUrl();
                }
                if (current is '"' or '\'' or '(' || current is <= '\b' or '\v' or >= '\x0E' and <= '\x1F' or '\x7F')
                {
                    return BadUrl();
                }
                if (current == '\\')
                {
                    if (!Escape(position)) { return BadUrl(); }
                    value.Append(Escaped());
                }
                else { value.Append(current); position++; }
            }
            context.Error("eof-in-url", position, "Unclosed CSS URL.");
            return Token(CssTokenKind.Url, value.ToString());
        }

        CssToken BadUrl()
        {
            context.Error("bad-url", position, "Invalid unquoted CSS URL.");
            while (position < input.Length && Peek() != ')')
            {
                context.Cancellation.ThrowIfCancellationRequested();
                if (Escape(position)) { Escaped(); }
                else { position++; }
            }
            if (Peek() == ')') { position++; }
            return Token(CssTokenKind.BadUrl, "");
        }
    }

    private string Ident()
    {
        var value = new StringBuilder();
        while (position < input.Length)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            if (Name(Peek())) { value.Append(input[position++]); }
            else if (Escape(position)) { value.Append(Escaped()); }
            else { break; }
        }
        return value.ToString();
    }

    private string Escaped()
    {
        position++;
        if (position == input.Length)
        {
            context.Error("eof-in-escape", position, "Escaped EOF becomes a replacement character.");
            return "\uFFFD";
        }
        if (!char.IsAsciiHexDigit(Peek())) { return input[position++].ToString(); }
        var scalar = 0;
        for (var count = 0; count < 6 && char.IsAsciiHexDigit(Peek()); count++)
        {
            scalar = scalar * 16 + int.Parse(input[position++].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        if (Space(Peek())) { position++; }
        return scalar == 0 || scalar > 0x10FFFF || scalar is >= 0xD800 and <= 0xDFFF
            ? "\uFFFD" : char.ConvertFromUtf32(scalar);
    }

    private char Peek(int distance = 0) => position + distance < input.Length ? input[position + distance] : '\0';
    private bool Starts(string text) => input.AsSpan(position).StartsWith(text, StringComparison.Ordinal);
    private bool Escape(int at) => at < input.Length && input[at] == '\\' && (at + 1 == input.Length || input[at + 1] != '\n');
    private bool StartsIdent(int at)
    {
        if (at >= input.Length) { return false; }
        var c = input[at];
        return NameStart(c) || Escape(at) || c == '-' && at + 1 < input.Length
            && (NameStart(input[at + 1]) || input[at + 1] == '-' || Escape(at + 1));
    }
    private bool StartsNumber(int at)
    {
        if (at >= input.Length) { return false; }
        if (input[at] is '+' or '-') { at++; }
        return at < input.Length && (Digit(input[at]) || input[at] == '.' && at + 1 < input.Length && Digit(input[at + 1]));
    }
    internal static bool Space(char c) => c is ' ' or '\t' or '\n';
    private static bool Digit(char c) => c is >= '0' and <= '9';
    private static bool NameStart(char c) => char.IsAsciiLetter(c) || c == '_' || c >= '\x80';
    private static bool Name(char c) => NameStart(c) || Digit(c) || c == '-';
}

internal static class CssText
{
    internal static string Lower(string value) => string.Create(value.Length, value, (span, text) =>
    {
        for (var i = 0; i < text.Length; i++) { span[i] = text[i] is >= 'A' and <= 'Z' ? (char)(text[i] + 32) : text[i]; }
    });
    internal static List<CssToken> Trim(IEnumerable<CssToken> tokens)
    {
        var list = tokens.ToList();
        var start = 0;
        var end = list.Count;
        while (start < end && list[start].Kind == CssTokenKind.Whitespace) { start++; }
        while (end > start && list[end - 1].Kind == CssTokenKind.Whitespace) { end--; }
        return list.GetRange(start, end - start);
    }
    internal static bool Opens(CssTokenKind kind) => kind is CssTokenKind.Function or CssTokenKind.OpenParen
        or CssTokenKind.OpenSquare or CssTokenKind.OpenBrace;
    internal static CssTokenKind Closing(CssTokenKind kind) => kind switch
    {
        CssTokenKind.OpenSquare => CssTokenKind.CloseSquare,
        CssTokenKind.OpenBrace => CssTokenKind.CloseBrace,
        _ => CssTokenKind.CloseParen
    };
}

/// <summary>A syntactic declaration, before property grammar validation.</summary>
/// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#consume-declaration">declarations</see>.</remarks>
public sealed record CssDeclaration(string Name, IReadOnlyList<CssToken> Value, bool Important, int Offset);
/// <summary>A syntactic at-rule or qualified rule, before selector/descriptor interpretation.</summary>
/// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#parsing">rules</see>.</remarks>
public sealed record CssRule(string? AtName, IReadOnlyList<CssToken> Prelude,
    IReadOnlyList<CssToken>? Block, int Offset);
/// <summary>Whole-string stylesheet syntax and recoverable diagnostics.</summary>
/// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#parse-stylesheet">stylesheet parsing</see>.</remarks>
public sealed record CssStyleSheet(IReadOnlyList<CssRule> Rules, IReadOnlyList<CssDiagnostic> Diagnostics);
/// <summary>Whole-string declaration syntax and recoverable diagnostics.</summary>
/// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#consume-declaration">declaration parsing</see>.</remarks>
public sealed record CssDeclarationList(IReadOnlyList<CssDeclaration> Declarations, IReadOnlyList<CssDiagnostic> Diagnostics);

/// <summary>CSS Syntax stylesheet and declaration parsing; property and selector semantics are separate.</summary>
/// <remarks>Spec: css-syntax; <see href="https://www.w3.org/TR/css-syntax-3/#parsing">parsing</see>.
/// Nested component blocks are retained as balanced flat tokens, with EOF closure recovery.</remarks>
public static class CssSyntax
{
    public static CssStyleSheet ParseStyleSheet(string input, CssOptions? options = null, CancellationToken cancellationToken = default)
    {
        var context = new CssContext(options, cancellationToken);
        var tokens = Tokens(input, context);
        var rules = new List<CssRule>();
        var position = 0;
        while (position < tokens.Count)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            var first = tokens[position];
            if (first.Kind is CssTokenKind.Whitespace or CssTokenKind.Cdo or CssTokenKind.Cdc) { position++; continue; }
            var at = first.Kind == CssTokenKind.AtKeyword ? first.Value : null;
            if (at is not null) { position++; }
            var prelude = new List<CssToken>();
            List<CssToken>? block = null;
            var terminated = false;
            while (position < tokens.Count)
            {
                var token = tokens[position++];
                if (token.Kind == CssTokenKind.OpenBrace)
                {
                    block = Component(tokens, ref position, token, context);
                    terminated = true;
                    break;
                }
                if (at is not null && token.Kind == CssTokenKind.Semicolon) { terminated = true; break; }
                prelude.Add(token);
                if (CssText.Opens(token.Kind))
                {
                    prelude.AddRange(Component(tokens, ref position, token, context));
                    prelude.Add(new(CssText.Closing(token.Kind), "", token.Offset, ""));
                }
            }
            if (at is not null || terminated)
            {
                if (rules.Count >= context.Options.MaxRules) { throw new CssLimitException("CSS rule limit exceeded."); }
                rules.Add(new(at, CssText.Trim(prelude).AsReadOnly(), block?.AsReadOnly(), first.Offset));
            }
            else { context.Error("eof-in-qualified-rule", first.Offset, "Qualified rule has no block."); }
        }
        return new(rules.AsReadOnly(), context.Diagnostics.AsReadOnly());
    }

    public static CssDeclarationList ParseDeclarations(string input, CssOptions? options = null, CancellationToken cancellationToken = default)
    {
        var context = new CssContext(options, cancellationToken);
        var tokens = Tokens(input, context);
        var normalized = new List<CssToken>();
        var position = 0;
        while (position < tokens.Count)
        {
            var token = tokens[position++];
            normalized.Add(token);
            if (CssText.Opens(token.Kind))
            {
                normalized.AddRange(Component(tokens, ref position, token, context));
                normalized.Add(new(CssText.Closing(token.Kind), "", token.Offset, ""));
            }
        }
        return new(Declarations(normalized, context).AsReadOnly(), context.Diagnostics.AsReadOnly());
    }

    internal static List<CssDeclaration> Declarations(IReadOnlyList<CssToken> tokens, CssContext context)
    {
        var declarations = new List<CssDeclaration>();
        var part = new List<CssToken>();
        var stack = new Stack<CssTokenKind>();
        foreach (var token in tokens)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            if (token.Kind == CssTokenKind.Semicolon && stack.Count == 0) { Consume(); continue; }
            part.Add(token);
            if (CssText.Opens(token.Kind)) { stack.Push(CssText.Closing(token.Kind)); }
            else if (stack.TryPeek(out var close) && token.Kind == close)
            {
                stack.Pop();
                if (stack.Count == 0 && token.Kind == CssTokenKind.CloseBrace
                    && !CssText.Trim(part)[0].Value.StartsWith("--", StringComparison.Ordinal))
                {
                    var first = CssText.Trim(part)[0];
                    context.Error(first.Kind == CssTokenKind.AtKeyword ? "unsupported-at-rule" : "unsupported-nested-rule",
                        first.Offset, "Declaration-block at-rules and nested style rules are not evaluated.");
                    part.Clear();
                }
            }
        }
        Consume();
        return declarations;

        void Consume()
        {
            var value = CssText.Trim(part);
            part.Clear();
            if (value.Count == 0) { return; }
            var first = value[0];
            if (first.Kind == CssTokenKind.AtKeyword)
            {
                context.Error("unsupported-at-rule", first.Offset, "Declaration-block at-rules are not evaluated.");
                return;
            }
            if (first.Kind != CssTokenKind.Ident)
            {
                context.Error("invalid-declaration", first.Offset, "Expected a property name; nested rules and declaration at-rules are unsupported.");
                return;
            }
            var colon = 1;
            while (colon < value.Count && value[colon].Kind == CssTokenKind.Whitespace) { colon++; }
            if (colon == value.Count || value[colon].Kind != CssTokenKind.Colon)
            {
                context.Error("missing-declaration-colon", first.Offset, "Expected ':' after property name.");
                return;
            }
            value = CssText.Trim(value.Skip(colon + 1));
            var important = false;
            if (value.Count > 0 && value[^1].Kind == CssTokenKind.Ident && CssText.Lower(value[^1].Value) == "important")
            {
                var bang = value.Count - 2;
                while (bang >= 0 && value[bang].Kind == CssTokenKind.Whitespace) { bang--; }
                if (bang >= 0 && value[bang].IsDelimiter("!"))
                {
                    important = true;
                    value = CssText.Trim(value.Take(bang));
                }
            }
            var validationStack = new Stack<CssTokenKind>();
            var invalid = false;
            foreach (var token in value)
            {
                if (token.Kind is CssTokenKind.BadString or CssTokenKind.BadUrl) { invalid = true; }
                if (CssText.Opens(token.Kind)) { validationStack.Push(CssText.Closing(token.Kind)); }
                else if (validationStack.TryPeek(out var close) && token.Kind == close) { validationStack.Pop(); }
                else if (token.Kind is CssTokenKind.CloseBrace or CssTokenKind.CloseParen or CssTokenKind.CloseSquare)
                {
                    invalid = true;
                }
                else if (validationStack.Count == 0 && token.IsDelimiter("!")) { invalid = true; }
            }
            if (invalid)
            {
                context.Error("invalid-declaration-value", first.Offset, "Malformed declaration value.");
                return;
            }
            declarations.Add(new(first.Value.StartsWith("--", StringComparison.Ordinal) ? first.Value : CssText.Lower(first.Value),
                value.AsReadOnly(), important, first.Offset));
        }
    }

    internal static List<CssToken> Tokens(string input, CssContext context)
    {
        var tokenizer = new CssTokenizer(input, context);
        var tokens = new List<CssToken>();
        for (var token = tokenizer.Read(); token.Kind != CssTokenKind.Eof; token = tokenizer.Read()) { tokens.Add(token); }
        return tokens;
    }

    private static List<CssToken> Component(List<CssToken> tokens, ref int position, CssToken opening, CssContext context)
    {
        var result = new List<CssToken>();
        var stack = new Stack<CssTokenKind>();
        stack.Push(CssText.Closing(opening.Kind));
        while (position < tokens.Count)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            var token = tokens[position++];
            if (token.Kind == stack.Peek())
            {
                stack.Pop();
                if (stack.Count == 0) { return result; }
            }
            else if (CssText.Opens(token.Kind))
            {
                if (stack.Count >= context.Options.MaxDepth) { throw new CssLimitException("CSS component depth limit exceeded."); }
                stack.Push(CssText.Closing(token.Kind));
            }
            result.Add(token);
        }
        context.Error("eof-in-block", opening.Offset, "Unclosed CSS component block.");
        while (stack.Count > 1)
        {
            var kind = stack.Pop();
            result.Add(new(kind, "", tokens.Count == 0 ? opening.Offset : tokens[^1].Offset, ""));
        }
        return result;
    }
}
