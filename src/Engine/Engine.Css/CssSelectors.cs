using System.Globalization;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Css;

/// <summary>Lexicographic selector specificity, not a packed decimal score.</summary>
/// <remarks>Spec: selectors; <see href="https://www.w3.org/TR/selectors-4/#specificity-rules">specificity</see>.</remarks>
public readonly record struct CssSpecificity(int Ids, int Classes, int Types) : IComparable<CssSpecificity>
{
    public int CompareTo(CssSpecificity other)
    {
        var result = Ids.CompareTo(other.Ids);
        if (result == 0) { result = Classes.CompareTo(other.Classes); }
        return result == 0 ? Types.CompareTo(other.Types) : result;
    }
    public static CssSpecificity operator +(CssSpecificity a, CssSpecificity b)
        => new(checked(a.Ids + b.Ids), checked(a.Classes + b.Classes), checked(a.Types + b.Types));
}

/// <summary>An immutable, reusable scoping root for <c>:scope</c> and scoped selector matching.</summary>
/// <remarks>Spec: selectors; <see href="https://www.w3.org/TR/selectors-4/#the-scope-pseudo">:scope</see> and
/// <see href="https://www.w3.org/TR/selectors-4/#scoping-root">scoping root</see>. An Element root is matched by
/// <c>:scope</c>; a Document root resolves <c>:scope</c> to its document element; a DocumentFragment root is a
/// featureless virtual scoping root that is never a match subject but acts as the parent of its top-level elements.
/// The scope does not filter candidates; callers supply descendants as DOM's
/// <see href="https://dom.spec.whatwg.org/#scope-match-a-selectors-string">scope-match</see> requires (spec ID: dom).</remarks>
public sealed class CssSelectorScope
{
    private CssSelectorScope(DomNode root) => Root = root;

    /// <summary>The Element, Document or DocumentFragment scoping root.</summary>
    public DomNode Root { get; }

    public static CssSelectorScope For(DomNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root is DomElement or DomDocument or DomDocumentFragment ? new(root)
            : throw new ArgumentException("A selector scoping root must be an Element, Document or DocumentFragment.", nameof(root));
    }

    internal bool IsVirtualRoot(DomNode node) => Root is DomDocumentFragment && node == Root;

    internal bool Matches(DomElement element) => Root switch
    {
        DomElement scope => element == scope,
        DomDocument document => element.ParentNode == document,
        _ => false
    };
}

/// <summary>A compiled static HTML selector list. Invalid syntax throws; unsupported features are distinct.</summary>
/// <remarks>Spec: selectors; <see href="https://www.w3.org/TR/selectors-4/#match">matching</see>.
/// No namespaces, pseudo-elements, :has() or dynamic state. Without an explicit
/// <see cref="CssSelectorScope"/>, <c>:scope</c> is equivalent to <c>:root</c>.</remarks>
public sealed class CssSelectorList
{
    private readonly IReadOnlyList<ComplexSelector> selectors;
    private readonly CssOptions options;
    public IReadOnlyList<CssSpecificity> Specificities { get; }
    public IReadOnlyList<CssDiagnostic> Diagnostics { get; }

    private CssSelectorList(List<ComplexSelector> selectors, CssOptions options, IReadOnlyList<CssDiagnostic> diagnostics)
    {
        this.selectors = selectors.AsReadOnly();
        this.options = options;
        Diagnostics = diagnostics;
        Specificities = selectors.Select(s => s.Specificity).ToList().AsReadOnly();
    }

    public static CssSelectorList Parse(string selector, CssOptions? options = null, CancellationToken cancellationToken = default)
    {
        var context = new CssContext(options, cancellationToken);
        var tokens = CssSyntax.Tokens(selector, context);
        return Compile(tokens, context, includeDiagnostics: true);
    }

    public CssSpecificity? Match(DomElement element, CancellationToken cancellationToken = default)
        => Match(element, null, cancellationToken);

    /// <summary>Match one element, resolving <c>:scope</c> against an explicit scoping root (or <c>:root</c> when null).</summary>
    /// <remarks>Spec: selectors; <see href="https://www.w3.org/TR/selectors-4/#match-a-selector-against-an-element">match a selector against an element</see>.</remarks>
    public CssSpecificity? Match(DomElement element, CssSelectorScope? scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Match(element, new CssContext(options, cancellationToken, scope));
    }

    /// <summary>Filter candidates in caller order under one shared matching budget.</summary>
    /// <remarks>Spec: selectors; <see href="https://www.w3.org/TR/selectors-4/#match">matching</see>.
    /// The caller supplies traversal order/scope; enumeration is lazy and cancellation-aware.</remarks>
    public IEnumerable<DomElement> Filter(IEnumerable<DomElement> candidates, CancellationToken cancellationToken = default)
        => Filter(candidates, null, cancellationToken);

    /// <summary>Filter candidates in caller order with one explicit scoping root for every candidate.</summary>
    /// <remarks>Spec: selectors; <see href="https://www.w3.org/TR/selectors-4/#the-scope-pseudo">:scope</see>.
    /// The scope only resolves <c>:scope</c>; candidate restriction remains the caller's traversal.</remarks>
    public IEnumerable<DomElement> Filter(IEnumerable<DomElement> candidates, CssSelectorScope? scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var context = new CssContext(options, cancellationToken, scope);
        var count = 0;
        foreach (var element in candidates)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            if (++count > options.MaxElements) { throw new CssLimitException("CSS selector candidate limit exceeded."); }
            if (Match(element, context) is not null) { yield return element; }
        }
    }

    internal static CssSelectorList Compile(IReadOnlyList<CssToken> tokens, CssContext context, bool includeDiagnostics = false)
        => new(new Parser(tokens, context, 0).List(false), context.Options,
            includeDiagnostics ? context.Diagnostics.ToList().AsReadOnly() : Array.Empty<CssDiagnostic>());

    internal CssSpecificity? Match(DomElement element, CssContext context)
    {
        CssSpecificity? best = null;
        foreach (var selector in selectors)
        {
            if (selector.Matches(element, context) && (best is null || selector.Specificity.CompareTo(best.Value) > 0))
            {
                best = selector.Specificity;
            }
        }
        return best;
    }

    // Virtual evaluates a featureless virtual scoping root: null means the selector is not allowed to match it.
    private sealed record SimpleSelector(Func<DomElement, CssContext, bool> Matches, CssSpecificity Specificity,
        Func<CssContext, bool?>? Virtual = null);
    private sealed record Compound(IReadOnlyList<SimpleSelector> Tests, char Combinator);
    private sealed class ComplexSelector(List<Compound> parts)
    {
        internal CssSpecificity Specificity { get; } = parts.SelectMany(p => p.Tests)
            .Aggregate(new CssSpecificity(), (a, b) => a + b.Specificity);
        internal bool Matches(DomElement element, CssContext context) => At(element, parts.Count - 1, context);

        // Selectors 4 featureless rule: a complex selector is allowed if its subject compound is; the virtual root has no relatives.
        internal bool? MatchesVirtual(CssContext context)
        {
            var subject = VirtualCompound(parts.Count - 1, context);
            return subject is null ? null : subject.Value && parts.Count == 1;
        }

        private bool AtVirtual(int index, CssContext context) => index == 0 && VirtualCompound(index, context) == true;

        private bool? VirtualCompound(int index, CssContext context)
        {
            context.EnterMatch();
            try
            {
                var result = true;
                foreach (var test in parts[index].Tests)
                {
                    context.MatchStep();
                    if (test.Virtual?.Invoke(context) is not { } matched) { return null; }
                    result &= matched;
                }
                return result;
            }
            finally { context.ExitMatch(); }
        }
        private bool At(DomElement element, int index, CssContext context)
        {
            context.EnterMatch();
            try { return Evaluate(); }
            finally { context.ExitMatch(); }

            bool Evaluate()
            {
                foreach (var test in parts[index].Tests)
                {
                    context.MatchStep();
                    if (!test.Matches(element, context)) { return false; }
                }
                if (index == 0) { return true; }
                switch (parts[index].Combinator)
                {
                    case '>':
                        if (element.ParentNode is DomElement parent) { return At(parent, index - 1, context); }
                        return element.ParentNode is { } root && context.Scope?.IsVirtualRoot(root) == true && AtVirtual(index - 1, context);
                    case '+': return Previous(element, context) is { } previous && At(previous, index - 1, context);
                    case '~':
                        for (var sibling = Previous(element, context); sibling is not null; sibling = Previous(sibling, context))
                        {
                            context.MatchStep();
                            if (At(sibling, index - 1, context)) { return true; }
                        }
                        return false;
                    default:
                        for (var ancestor = element.ParentNode; ancestor is not null; ancestor = ancestor.ParentNode)
                        {
                            context.MatchStep();
                            if (ancestor is DomElement e ? At(e, index - 1, context)
                                : context.Scope?.IsVirtualRoot(ancestor) == true && AtVirtual(index - 1, context)) { return true; }
                        }
                        return false;
                }
            }
        }
    }

    private sealed class Parser(IReadOnlyList<CssToken> tokens, CssContext context, int depth)
    {
        private int position;
        private CssToken? Current => position < tokens.Count ? tokens[position] : null;
        private bool Delim(string value) => Current?.IsDelimiter(value) == true;
        private bool Kind(CssTokenKind kind) => Current?.Kind == kind;
        private void Spaces() { while (Kind(CssTokenKind.Whitespace)) { position++; } }
        private FormatException Invalid() => new($"Invalid selector near offset {Current?.Offset ?? tokens.LastOrDefault()?.Offset ?? 0}.");

        internal List<ComplexSelector> List(bool forgiving)
        {
            if (depth >= context.Options.MaxDepth) { throw new CssLimitException("CSS selector nesting limit exceeded."); }
            var groups = Split(tokens);
            var result = new List<ComplexSelector>();
            foreach (var group in groups)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                try { result.Add(new Parser(group, context, depth).Complex()); }
                catch (FormatException) when (forgiving) { }
            }
            if (!forgiving && result.Count == 0) { throw Invalid(); }
            return result;
        }

        private ComplexSelector Complex()
        {
            Spaces();
            var parts = new List<Compound>();
            var combinator = ' ';
            while (position < tokens.Count)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                if (parts.Count >= context.Options.MaxDepth) { throw new CssLimitException("CSS selector chain limit exceeded."); }
                parts.Add(new(CompoundTests().AsReadOnly(), combinator));
                var beforeSpaces = position;
                Spaces();
                if (position == tokens.Count) { break; }
                if (Delim("|")) { throw new UnsupportedCssException("CSS namespaces and column combinators are unsupported.", Current!.Offset); }
                if (Delim(">") || Delim("+") || Delim("~"))
                {
                    combinator = Current!.Value[0];
                    position++;
                    Spaces();
                    if (position == tokens.Count) { throw Invalid(); }
                }
                else if (position > beforeSpaces) { combinator = ' '; }
                else { throw Invalid(); }
            }
            if (parts.Count == 0) { throw Invalid(); }
            return new(parts);
        }

        private List<SimpleSelector> CompoundTests()
        {
            var tests = new List<SimpleSelector>();
            if (Delim("|")) { throw new UnsupportedCssException("Namespaced types are unsupported.", Current!.Offset); }
            if (Kind(CssTokenKind.Ident))
            {
                var name = CssText.Lower(Current!.Value);
                position++;
                tests.Add(new((e, _) => e.LocalName == name, new(0, 0, 1)));
            }
            else if (Delim("*")) { position++; tests.Add(new((_, _) => true, new())); }
            while (position < tokens.Count)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                if (Kind(CssTokenKind.Hash))
                {
                    var token = Current!;
                    if (!token.IsId) { throw Invalid(); }
                    position++;
                    tests.Add(new((e, _) => CaseEqual(e.GetAttribute("id"), token.Value, Quirks(e)), new(1, 0, 0)));
                }
                else if (Delim("."))
                {
                    position++;
                    if (!Kind(CssTokenKind.Ident)) { throw Invalid(); }
                    var name = Current!.Value;
                    position++;
                    tests.Add(new((e, _) => Words(e.GetAttribute("class") ?? "").Any(c => CaseEqual(c, name, Quirks(e))), new(0, 1, 0)));
                }
                else if (Kind(CssTokenKind.OpenSquare)) { tests.Add(Attribute()); }
                else if (Kind(CssTokenKind.Colon)) { tests.Add(Pseudo()); }
                else { break; }
            }
            if (tests.Count == 0) { throw Invalid(); }
            return tests;
        }

        private SimpleSelector Attribute()
        {
            position++;
            Spaces();
            if (Delim("|") || Delim("*")) { throw new UnsupportedCssException("Namespaced attributes are unsupported.", Current!.Offset); }
            if (!Kind(CssTokenKind.Ident)) { throw Invalid(); }
            var name = CssText.Lower(Current!.Value);
            position++;
            Spaces();
            if (Kind(CssTokenKind.CloseSquare))
            {
                position++;
                return new((e, _) => e.GetAttribute(name) is not null, new(0, 1, 0));
            }
            var op = Current?.Value ?? "";
            var operatorOffset = Current?.Offset ?? 0;
            position++;
            if (op == "|" && !Delim("="))
            {
                throw new UnsupportedCssException("Namespaced attributes are unsupported.", operatorOffset);
            }
            if (op != "=")
            {
                if (op is not ("~" or "|" or "^" or "$" or "*") || !Delim("=")) { throw Invalid(); }
                position++;
                op += "=";
            }
            Spaces();
            if (!Kind(CssTokenKind.Ident) && !Kind(CssTokenKind.String)) { throw Invalid(); }
            var value = Current!.Value;
            position++;
            Spaces();
            bool? insensitive = null;
            if (Kind(CssTokenKind.Ident))
            {
                insensitive = CssText.Lower(Current!.Value) switch { "i" => true, "s" => false, _ => throw Invalid() };
                position++;
                Spaces();
            }
            if (!Kind(CssTokenKind.CloseSquare)) { throw Invalid(); }
            position++;
            return new((e, _) =>
            {
                var actual = e.GetAttribute(name);
                if (actual is null) { return false; }
                var ignoreCase = insensitive ?? HtmlInsensitiveAttributes.Contains(name);
                if (ignoreCase) { actual = CssText.Lower(actual); }
                var expected = ignoreCase ? CssText.Lower(value) : value;
                return op switch
                {
                    "=" => actual == expected,
                    "~=" => expected.Length > 0 && !expected.Any(HtmlSpace) && Words(actual).Contains(expected, StringComparer.Ordinal),
                    "|=" => actual == expected || actual.StartsWith(expected + "-", StringComparison.Ordinal),
                    "^=" => expected.Length > 0 && actual.StartsWith(expected, StringComparison.Ordinal),
                    "$=" => expected.Length > 0 && actual.EndsWith(expected, StringComparison.Ordinal),
                    "*=" => expected.Length > 0 && actual.Contains(expected, StringComparison.Ordinal),
                    _ => false
                };
            }, new(0, 1, 0));
        }

        private SimpleSelector Pseudo()
        {
            position++;
            if (Kind(CssTokenKind.Colon)) { throw new UnsupportedCssException("Pseudo-elements are unsupported.", Current!.Offset); }
            if (Kind(CssTokenKind.Function))
            {
                var function = Current!;
                var name = CssText.Lower(function.Value);
                position++;
                var argument = Argument();
                if (name is "is" or "not" or "where")
                {
                    var selectors = new Parser(argument, context, depth + 1).List(name != "not");
                    var specificity = name == "where" || selectors.Count == 0 ? new CssSpecificity()
                        : selectors.Select(s => s.Specificity).Max();
                    return new((e, ctx) => name == "not" ? !selectors.Any(s => s.Matches(e, ctx))
                        : selectors.Any(s => s.Matches(e, ctx)), specificity, ctx =>
                        {
                            bool? matched = null;
                            foreach (var selector in selectors)
                            {
                                if (selector.MatchesVirtual(ctx) is not { } result) { continue; }
                                matched = result || matched == true;
                                if (result) { break; }
                            }
                            return name == "not" ? !matched : matched;
                        });
                }
                if (name is "nth-child" or "nth-last-child" or "nth-of-type" or "nth-last-of-type")
                {
                    var of = -1;
                    var nesting = new Stack<CssTokenKind>();
                    for (var i = 0; i < argument.Count; i++)
                    {
                        context.Cancellation.ThrowIfCancellationRequested();
                        var part = argument[i];
                        if (nesting.Count == 0 && part.Kind == CssTokenKind.Ident && CssText.Lower(part.Value) == "of")
                        {
                            of = i;
                            break;
                        }
                        if (CssText.Opens(part.Kind)) { nesting.Push(CssText.Closing(part.Kind)); }
                        else if (nesting.TryPeek(out var close) && part.Kind == close) { nesting.Pop(); }
                    }
                    var sameType = name.Contains("of-type", StringComparison.Ordinal);
                    if (of >= 0 && sameType)
                    {
                        throw new UnsupportedCssException("Of clauses on nth-of-type selectors are unsupported.", function.Offset);
                    }
                    var (a, b) = Nth(of < 0 ? argument : argument.GetRange(0, of));
                    var filter = of < 0 ? null : new Parser(argument.GetRange(of + 1, argument.Count - of - 1),
                        context, depth + 1).List(false);
                    var specificity = new CssSpecificity(0, 1, 0)
                        + (filter is null ? new CssSpecificity() : filter.Select(s => s.Specificity).Max());
                    return new((e, ctx) =>
                    {
                        var siblings = Siblings(e, sameType, ctx);
                        if (filter is not null) { siblings = siblings.Where(sibling => filter.Any(s => s.Matches(sibling, ctx))).ToList(); }
                        var index = siblings.IndexOf(e) + 1;
                        // Membership is required before reversing: an absent subject must not become count + 1.
                        if (index == 0) { return false; }
                        if (name.Contains("last", StringComparison.Ordinal)) { index = siblings.Count - index + 1; }
                        var difference = (long)index - b;
                        return a == 0 ? difference == 0 : difference % a == 0 && difference / a >= 0;
                    }, specificity);
                }
                throw new UnsupportedCssException($"Unsupported functional pseudo-class :{name}().", function.Offset);
            }
            if (!Kind(CssTokenKind.Ident)) { throw Invalid(); }
            var token = Current!;
            position++;
            var pseudo = CssText.Lower(token.Value);
            return pseudo switch
            {
                "root" => new((e, _) => e.ParentNode is DomDocument, new(0, 1, 0)),
                "scope" => new((e, ctx) =>
                {
                    ctx.MatchStep();
                    return ctx.Scope is { } scope ? scope.Matches(e) : e.ParentNode is DomDocument;
                }, new(0, 1, 0), ctx => { ctx.MatchStep(); return true; }),
                "empty" => new((e, ctx) => Empty(e, ctx), new(0, 1, 0)),
                "first-child" or "last-child" or "only-child" or "first-of-type" or "last-of-type" or "only-of-type"
                    => new((e, ctx) =>
                    {
                        var siblings = Siblings(e, pseudo.EndsWith("of-type", StringComparison.Ordinal), ctx);
                        return pseudo.StartsWith("first", StringComparison.Ordinal) ? siblings[0] == e
                            : pseudo.StartsWith("last", StringComparison.Ordinal) ? siblings[^1] == e : siblings.Count == 1;
                    }, new(0, 1, 0)),
                _ => throw new UnsupportedCssException($"Unsupported pseudo-class :{pseudo}.", token.Offset)
            };
        }

        private List<CssToken> Argument()
        {
            var result = new List<CssToken>();
            var nesting = 1;
            while (position < tokens.Count)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                var token = tokens[position++];
                if (token.Kind is CssTokenKind.Function or CssTokenKind.OpenParen) { nesting++; }
                if (token.Kind == CssTokenKind.CloseParen)
                {
                    nesting--;
                    if (nesting == 0) { return result; }
                }
                result.Add(token);
            }
            throw Invalid();

        }

        private (int A, int B) Nth(List<CssToken> argument)
        {
            var list = CssText.Trim(argument);
            if (list.Count == 0) { throw Invalid(); }
            if (list.Count == 1 && list[0].Kind == CssTokenKind.Ident)
            {
                var odd = CssText.Lower(list[0].Value);
                if (odd == "odd") { return (2, 1); }
                if (odd == "even") { return (2, 0); }
            }
            if (list.Count == 1 && list[0].Kind == CssTokenKind.Number && list[0].IsInteger)
            {
                return (0, Integer(list[0].Value));
            }
            var index = 0;
            if (list[0].IsDelimiter("+"))
            {
                index++;
                if (index == list.Count || list[index].Kind != CssTokenKind.Ident) { throw Invalid(); }
            }
            var head = list[index++];
            int a;
            string name;
            if (head.Kind == CssTokenKind.Dimension && head.IsInteger)
            {
                a = Integer(head.Value);
                name = CssText.Lower(head.Unit);
            }
            else if (head.Kind == CssTokenKind.Ident)
            {
                name = CssText.Lower(head.Value);
                a = name.StartsWith("-n", StringComparison.Ordinal) ? -1 : 1;
                if (a == -1) { name = name[1..]; }
            }
            else { throw Invalid(); }
            while (index < list.Count && list[index].Kind == CssTokenKind.Whitespace) { index++; }
            if (name.StartsWith("n-", StringComparison.Ordinal) && name.Length > 2)
            {
                if (index != list.Count || !name[2..].All(char.IsAsciiDigit)) { throw Invalid(); }
                return (a, Integer("-" + name[2..]));
            }
            if (name == "n-")
            {
                if (index + 1 != list.Count || !Unsigned(list[index])) { throw Invalid(); }
                return (a, Integer("-" + list[index].Value));
            }
            if (name != "n") { throw Invalid(); }
            if (index == list.Count) { return (a, 0); }
            var next = list[index++];
            if (next.Kind == CssTokenKind.Number && next.IsInteger && next.Value.StartsWith('+')
                || next.Kind == CssTokenKind.Number && next.IsInteger && next.Value.StartsWith('-'))
            {
                if (index != list.Count) { throw Invalid(); }
                return (a, Integer(next.Value));
            }
            if (!next.IsDelimiter("+") && !next.IsDelimiter("-")) { throw Invalid(); }
            while (index < list.Count && list[index].Kind == CssTokenKind.Whitespace) { index++; }
            if (index + 1 != list.Count || !Unsigned(list[index])) { throw Invalid(); }
            return (a, Integer(next.Value + list[index].Value));

            int Integer(string text)
            {
                if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)) { return value; }
                throw new UnsupportedCssException("An+B integers outside the signed 32-bit range are unsupported.",
                    argument[0].Offset);
            }
            static bool Unsigned(CssToken token) => token.Kind == CssTokenKind.Number && token.IsInteger
                && token.Value.Length > 0 && char.IsAsciiDigit(token.Value[0]);
        }

        private List<List<CssToken>> Split(IReadOnlyList<CssToken> input)
        {
            var result = new List<List<CssToken>>();
            var current = new List<CssToken>();
            var nesting = new Stack<CssTokenKind>();
            foreach (var token in input)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                if (token.Kind == CssTokenKind.Comma && nesting.Count == 0) { result.Add(current); current = []; continue; }
                current.Add(token);
                if (CssText.Opens(token.Kind))
                {
                    if (nesting.Count >= context.Options.MaxDepth) { throw new CssLimitException("CSS selector component depth limit exceeded."); }
                    nesting.Push(CssText.Closing(token.Kind));
                }
                else if (nesting.TryPeek(out var close) && token.Kind == close) { nesting.Pop(); }
            }
            result.Add(current);
            return result;
        }
    }

    private static bool Quirks(DomElement e) => e.OwnerDocument?.Mode == DomDocumentMode.Quirks;
    private static bool CaseEqual(string? a, string b, bool insensitive) => a is not null
        && (insensitive ? CssText.Lower(a) == CssText.Lower(b) : a == b);
    private static bool HtmlSpace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';
    private static string[] Words(string text) => text.Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries);
    private static DomElement? Previous(DomElement element, CssContext context)
    {
        DomElement? previous = null;
        if (element.ParentNode is not { } parent) { return null; }
        foreach (var node in parent.ChildNodes)
        {
            context.MatchStep();
            if (node == element) { return previous; }
            if (node is DomElement e) { previous = e; }
        }
        return null;
    }
    private static List<DomElement> Siblings(DomElement element, bool sameType, CssContext context)
    {
        var result = new List<DomElement>();
        if (element.ParentNode is not { } parent) { return [element]; }
        foreach (var node in parent.ChildNodes)
        {
            context.MatchStep();
            if (node is DomElement e && (!sameType || e.LocalName == element.LocalName)) { result.Add(e); }
        }
        return result;
    }
    private static bool Empty(DomElement element, CssContext context)
    {
        foreach (var node in element.ChildNodes)
        {
            context.MatchStep();
            if (node is DomElement || node is DomText text && text.Data.Any(c => !HtmlSpace(c))) { return false; }
        }
        return true;
    }
    // HTML's legacy selector case-insensitive attribute list, not all attribute values.
    private static readonly HashSet<string> HtmlInsensitiveAttributes = new(
        "accept accept-charset align alink axis bgcolor charset checked clear codetype color compact declare defer dir direction disabled enctype face frame hreflang http-equiv lang language link media method multiple nohref noresize noshade nowrap readonly rel rev rules scope scrolling selected shape target text type valign valuetype vlink".Split(' '),
        StringComparer.Ordinal);
}
