using System.Text;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Html;

/// <summary>Static HTML document tree construction with explicit unsupported-feature failures.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#tree-construction">tree construction</see>.
/// No tables, templates, foreign content beyond the basic form tree subset, frames, fragment parsing, scripting,
/// active-formatting reconstruction or adoption agency algorithm are implemented.</remarks>
public static class HtmlParser
{
    public static HtmlParseResult Parse(string input, HtmlParserOptions? options = null, CancellationToken cancellationToken = default) =>
        new Builder(input, options ?? new(), cancellationToken).Run();

    private enum Mode { Initial, BeforeHtml, BeforeHead, InHead, AfterHead, InBody, Text, AfterBody, AfterAfterBody }

    private sealed class Builder
    {
        private readonly DomDocument document = new();
        private readonly List<DomElement> open = [];
        private readonly List<HtmlParseError> errors = [];
        private readonly HtmlParserOptions options;
        private readonly HtmlTokenizer tokenizer;
        private readonly CancellationToken cancellationToken;
        private Mode mode;
        private Mode originalMode;
        private int nodes = 1;
        private bool ignoreNextLineFeed;
        private DomElement? formPointer;
        private DomText? bufferedText;
        private readonly StringBuilder textBuffer = new();

        public Builder(string input, HtmlParserOptions options, CancellationToken cancellationToken)
        {
            options.Validate();
            this.options = options;
            this.cancellationToken = cancellationToken;
            tokenizer = new(input, options: options, cancellationToken: cancellationToken);
        }

        public HtmlParseResult Run()
        {
            while (true)
            {
                var token = tokenizer.Read();
                if (token is HtmlCharacters characters)
                {
                    var text = characters.Data;
                    if (ignoreNextLineFeed && text.StartsWith('\n'))
                    {
                        text = text[1..];
                    }

                    ignoreNextLineFeed = false;
                    // Whitespace and non-whitespace have distinct rules in document/head modes.
                    for (var at = 0; at < text.Length;)
                    {
                        if (mode is Mode.InBody or Mode.Text)
                        {
                            Process(new HtmlCharacters(text[at..]));
                            break;
                        }

                        var end = at + 1;
                        var whitespace = HtmlTokenizer.Whitespace(text[at]);
                        while (end < text.Length && HtmlTokenizer.Whitespace(text[end]) == whitespace) { end++; }
                        Process(new HtmlCharacters(text[at..end]));
                        at = end;
                    }
                }
                else
                {
                    ignoreNextLineFeed = false;
                    Process(token);
                }

                CheckErrors();
                if (token is HtmlEndOfFile)
                {
                    FlushText();
                    var allErrors = tokenizer.Errors.Concat(errors).OrderBy(e => e.Offset).ToList().AsReadOnly();
                    return new(document, allErrors);
                }
            }
        }

        private DomNode Current => open.Count == 0 ? document : open[^1];
        private bool IsWhitespace(HtmlToken token) => token is HtmlCharacters text && text.Data.All(HtmlTokenizer.Whitespace);

        private void Process(HtmlToken token)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (token is HtmlTag tag && UnsupportedTags.Contains(tag.Name))
                {
                    throw Unsupported($"Tree construction for <{tag.Name}> is not implemented.");
                }

                switch (mode)
                {
                    case Mode.Initial:
                        if (IsWhitespace(token)) { return; }
                        if (Miscellaneous(token, document)) { return; }
                        if (token is HtmlDoctype doctype)
                        {
                            if (!string.IsNullOrEmpty(doctype.PublicId)
                                || !string.IsNullOrEmpty(doctype.SystemId) && doctype.SystemId != "about:legacy-compat")
                            {
                                throw Unsupported("Legacy doctype mode selection is not implemented.");
                            }

                            CountNode();
                            document.AppendChild(document.CreateDocumentType(doctype.Name ?? "", doctype.PublicId ?? "", doctype.SystemId ?? ""));
                            if (doctype.ForceQuirks || doctype.Name != "html") { document.Mode = DomDocumentMode.Quirks; Error("invalid-doctype"); }
                            mode = Mode.BeforeHtml;
                            return;
                        }

                        document.Mode = DomDocumentMode.Quirks;
                        Error("missing-doctype");
                        mode = Mode.BeforeHtml;
                        continue;
                    case Mode.BeforeHtml:
                        if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
                        if (IsWhitespace(token)) { return; }
                        if (Miscellaneous(token, document)) { return; }
                        if (token is HtmlTag { IsEndTag: false, Name: "html" } html)
                        {
                            Insert(html);
                            mode = Mode.BeforeHead;
                            return;
                        }

                        if (token is HtmlTag { IsEndTag: true } beforeHtmlEnd && beforeHtmlEnd.Name is not ("head" or "body" or "html" or "br"))
                        {
                            Error("unexpected-end-tag");
                            return;
                        }

                        Insert(Synthetic("html"));
                        mode = Mode.BeforeHead;
                        continue;
                    case Mode.BeforeHead:
                        if (IsWhitespace(token)) { return; }
                        if (Miscellaneous(token, Current)) { return; }
                        if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
                        if (token is HtmlTag { IsEndTag: false, Name: "html" } beforeHeadHtml)
                        {
                            Merge(document.DocumentElement!, beforeHeadHtml);
                            return;
                        }

                        if (token is HtmlTag { IsEndTag: false, Name: "head" } head)
                        {
                            Insert(head);
                            mode = Mode.InHead;
                            return;
                        }

                        if (token is HtmlTag { IsEndTag: true } beforeHeadEnd && beforeHeadEnd.Name is not ("head" or "body" or "html" or "br"))
                        {
                            Error("unexpected-end-tag");
                            return;
                        }

                        Insert(Synthetic("head"));
                        mode = Mode.InHead;
                        continue;
                    case Mode.InHead:
                        if (IsWhitespace(token)) { Text(((HtmlCharacters)token).Data); return; }
                        if (Miscellaneous(token, Current)) { return; }
                        if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
                        if (token is HtmlTag { IsEndTag: false } headStart)
                        {
                            switch (headStart.Name)
                            {
                                case "html": Merge(document.DocumentElement!, headStart); return;
                                case "base" or "basefont" or "bgsound" or "link" or "meta":
                                    Insert(headStart, push: false);
                                    return;
                                case "title": StartText(headStart, HtmlTextMode.Rcdata); return;
                                case "style" or "noframes": StartText(headStart, HtmlTextMode.Rawtext); return;
                                case "script": StartText(headStart, HtmlTextMode.ScriptData); return;
                                case "head": Error("unexpected-head-start-tag"); return;
                            }
                        }

                        if (token is HtmlTag { IsEndTag: true, Name: "head" })
                        {
                            open.RemoveAt(open.Count - 1);
                            mode = Mode.AfterHead;
                            return;
                        }

                        if (token is HtmlTag { IsEndTag: true } headEnd && headEnd.Name is not ("body" or "html" or "br"))
                        {
                            Error("unexpected-end-tag");
                            return;
                        }

                        open.RemoveAt(open.Count - 1);
                        mode = Mode.AfterHead;
                        continue;
                    case Mode.AfterHead:
                        if (IsWhitespace(token)) { Text(((HtmlCharacters)token).Data); return; }
                        if (Miscellaneous(token, Current)) { return; }
                        if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
                        if (token is HtmlTag { IsEndTag: false } afterHeadStart)
                        {
                            if (afterHeadStart.Name == "html") { Merge(document.DocumentElement!, afterHeadStart); return; }
                            if (afterHeadStart.Name == "body") { Insert(afterHeadStart); mode = Mode.InBody; return; }
                            if (afterHeadStart.Name == "head") { Error("unexpected-head-start-tag"); return; }
                            if (HeadTags.Contains(afterHeadStart.Name))
                            {
                                Error("head-content-after-head");
                                open.Add(document.Head!);
                                mode = Mode.InHead;
                                Process(token);
                                if (mode == Mode.Text)
                                {
                                    originalMode = Mode.AfterHead;
                                }
                                else
                                {
                                    open.Remove(document.Head!);
                                    mode = Mode.AfterHead;
                                }

                                return;
                            }
                        }

                        if (token is HtmlTag { IsEndTag: true } afterHeadEnd && afterHeadEnd.Name is not ("body" or "html" or "br"))
                        {
                            Error("unexpected-end-tag");
                            return;
                        }

                        Insert(Synthetic("body"));
                        mode = Mode.InBody;
                        continue;
                    case Mode.Text:
                        if (token is HtmlCharacters raw) { Text(raw.Data); return; }
                        if (token is HtmlEndOfFile)
                        {
                            Error("eof-in-text-element");
                            open.RemoveAt(open.Count - 1);
                            if (originalMode == Mode.AfterHead) { open.Remove(document.Head!); }
                            mode = originalMode;
                            continue;
                        }

                        if (token is HtmlTag { IsEndTag: true })
                        {
                            open.RemoveAt(open.Count - 1);
                            if (originalMode == Mode.AfterHead) { open.Remove(document.Head!); }
                            mode = originalMode;
                            return;
                        }

                        throw new InvalidOperationException("Text tokenizer emitted an unexpected token.");
                    case Mode.AfterBody:
                        if (IsWhitespace(token)) { Text(((HtmlCharacters)token).Data); return; }
                        if (Miscellaneous(token, document.DocumentElement!)) { return; }
                        if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
                        if (token is HtmlTag { IsEndTag: false, Name: "html" } afterBodyHtml) { Merge(document.DocumentElement!, afterBodyHtml); return; }
                        if (token is HtmlTag { IsEndTag: true, Name: "html" }) { mode = Mode.AfterAfterBody; return; }
                        if (token is HtmlEndOfFile) { return; }
                        Error("content-after-body");
                        mode = Mode.InBody;
                        continue;
                    case Mode.AfterAfterBody:
                        if (Miscellaneous(token, document)) { return; }
                        if (token is HtmlEndOfFile) { return; }
                        if (IsWhitespace(token)) { Text(((HtmlCharacters)token).Data); return; }
                        if (token is HtmlTag { IsEndTag: false, Name: "html" } finalHtml) { Merge(document.DocumentElement!, finalHtml); return; }
                        Error("content-after-html");
                        mode = Mode.InBody;
                        continue;
                    case Mode.InBody:
                        BodyToken(token);
                        return;
                }
            }
        }

        private void BodyToken(HtmlToken token)
        {
            if (token is HtmlCharacters text)
            {
                if (text.Data.Contains('\0')) { Error("null-in-body"); }
                Text(text.Data.Replace("\0", "", StringComparison.Ordinal));
                return;
            }

            if (Miscellaneous(token, Current)) { return; }
            if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
            if (token is HtmlEndOfFile)
            {
                var selectIndexAtEof = open.FindLastIndex(element => element.LocalName == "select");
                if (selectIndexAtEof >= 0) { SelectToken(token, selectIndexAtEof); return; }
                if (open.Any(e => !ImplicitlyClosable.Contains(e.LocalName))) { Error("unclosed-elements-at-eof"); }
                return;
            }

            if (token is not HtmlTag tag) { throw new InvalidOperationException("Unknown HTML token."); }
            var selectIndex = open.FindLastIndex(element => element.LocalName == "select");
            if (selectIndex >= 0)
            {
                SelectToken(token, selectIndex);
                return;
            }
            if (!tag.IsEndTag)
            {
                switch (tag.Name)
                {
                    case "html": Merge(document.DocumentElement!, tag); Error("html-start-in-body"); return;
                    case "body": Merge(document.Body!, tag); Error("body-start-in-body"); return;
                    case "head": Error("unexpected-head-start-tag"); return;
                    case "title": StartText(tag, HtmlTextMode.Rcdata); return;
                    case "style" or "noframes": StartText(tag, HtmlTextMode.Rawtext); return;
                    case "script": StartText(tag, HtmlTextMode.ScriptData); return;
                    case "textarea":
                        StartText(tag, HtmlTextMode.Rcdata);
                        ignoreNextLineFeed = true;
                        return;
                    case "xmp":
                        CloseParagraph();
                        StartText(tag, HtmlTextMode.Rawtext);
                        return;
                    case "iframe" or "noembed":
                        StartText(tag, HtmlTextMode.Rawtext);
                        return;
                    case "plaintext":
                        CloseParagraph();
                        Insert(tag);
                        tokenizer.SetTextMode(HtmlTextMode.Plaintext, tag.Name);
                        return;
                    case "li":
                        CloseListItem("li");
                        CloseParagraph();
                        Insert(tag);
                        return;
                    case "dd" or "dt":
                        CloseListItem("dd", "dt");
                        CloseParagraph();
                        Insert(tag);
                        return;
                    case "p":
                        CloseParagraph();
                        Insert(tag);
                        return;
                    case "pre" or "listing":
                        CloseParagraph();
                        Insert(tag);
                        ignoreNextLineFeed = true;
                        return;
                    case "form":
                        // Spec: html; https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inbody (start tag "form").
                        if (formPointer is not null) { Error("nested-form"); return; }
                        CloseParagraph();
                        formPointer = Insert(tag);
                        return;
                    case "button":
                        if (InScope("button")) { Error("nested-button"); Close("button"); }
                        Insert(tag);
                        return;
                    case "select":
                        if (InScope("select")) { throw Unsupported("Nested select elements are not implemented."); }
                        Insert(tag);
                        return;
                    case "option":
                        throw Unsupported("Option elements outside the supported select subset are not implemented.");
                    case "optgroup":
                        throw Unsupported("Optgroup elements outside the supported select subset are not implemented.");
                    case "hr":
                        CloseParagraph();
                        Insert(tag, push: false);
                        return;
                    case "image":
                        Error("image-start-tag");
                        Insert(tag with { Name = "img" }, push: false);
                        return;
                    case "a":
                        if (open.Any(e => e.LocalName == "a")) { throw Unsupported("Nested anchor adoption is not implemented."); }
                        break;
                    case "nobr":
                        if (InScope("nobr")) { throw Unsupported("Nested nobr adoption is not implemented."); }
                        break;
                }

                if (BlockTags.Contains(tag.Name)) { CloseParagraph(); }
                if (HeadingTags.Contains(tag.Name) && open.LastOrDefault() is { } current && HeadingTags.Contains(current.LocalName))
                {
                    Error("nested-heading");
                    open.RemoveAt(open.Count - 1);
                }

                Insert(tag, push: !VoidTags.Contains(tag.Name));
                return;
            }

            switch (tag.Name)
            {
                case "body" or "html":
                    if (!InScope("body")) { Error("unexpected-end-tag"); return; }
                    mode = Mode.AfterBody;
                    if (tag.Name == "html") { Process(tag); }
                    return;
                case "p":
                    if (!InScope("p", buttonScope: true)) { Error("unexpected-p-end-tag"); Insert(Synthetic("p")); }
                    Close("p");
                    return;
                case "li":
                    if (!InScope("li", listScope: true)) { Error("unexpected-end-tag"); return; }
                    Close("li");
                    return;
                case "dd" or "dt":
                    if (!InScope(tag.Name)) { Error("unexpected-end-tag"); return; }
                    Close(tag.Name);
                    return;
                case "form":
                    {
                        // Spec: html; https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inbody (end tag "form").
                        var node = formPointer;
                        formPointer = null;
                        if (node is null || !InScope("form")) { Error("unexpected-form-end-tag"); return; }
                        FlushText();
                        while (open[^1] != node && open[^1].LocalName is "dd" or "dt" or "li" or "p") { open.RemoveAt(open.Count - 1); }
                        if (open[^1] != node)
                        {
                            throw Unsupported("Misnested form end tags would split form ownership from tree ancestry.");
                        }

                        open.RemoveAt(open.Count - 1);
                        return;
                    }
                case "br":
                    Error("br-end-tag");
                    Insert(Synthetic("br"), push: false);
                    return;
            }

            if (BlockTags.Contains(tag.Name) || tag.Name == "button")
            {
                if (!InScope(tag.Name)) { Error("unexpected-end-tag"); return; }
                Close(tag.Name);
                return;
            }

            if (HeadingTags.Contains(tag.Name))
            {
                var heading = open.FindLastIndex(e => HeadingTags.Contains(e.LocalName));
                if (heading < 0 || !InScope(open[heading].LocalName)) { Error("unexpected-end-tag"); return; }
                Close(open[heading].LocalName);
                return;
            }

            for (var index = open.Count - 1; index >= 0; index--)
            {
                if (open[index].LocalName == tag.Name)
                {
                    if (FormattingTags.Contains(tag.Name) && index != open.Count - 1)
                    {
                        throw Unsupported("Misnested formatting requires the adoption agency algorithm.");
                    }

                    CloseAt(index);
                    return;
                }

                if (SpecialTags.Contains(open[index].LocalName)) { break; }
            }

            Error("unexpected-end-tag");
        }

        private void SelectToken(HtmlToken token, int selectIndex)
        {
            if (token is HtmlCharacters characters)
            {
                if (characters.Data.Contains('\0')) { Error("null-in-body"); }
                Text(characters.Data.Replace("\0", "", StringComparison.Ordinal));
                return;
            }
            if (Miscellaneous(token, Current)) { return; }
            if (token is HtmlEndOfFile)
            {
                Error("unclosed-select");
                open.RemoveRange(selectIndex, open.Count - selectIndex);
                return;
            }
            if (token is not HtmlTag tag) { throw new InvalidOperationException("Unknown HTML token in select."); }
            if (!tag.IsEndTag && tag.Name == "option")
            {
                if (open[^1].LocalName == "option") { Close("option"); }
                Insert(tag);
                return;
            }
            if (!tag.IsEndTag && tag.Name == "optgroup")
            {
                if (open[^1].LocalName == "option") { Close("option"); }
                if (open[^1].LocalName == "optgroup") { Close("optgroup"); }
                Insert(tag);
                return;
            }
            if (tag.IsEndTag && tag.Name == "option")
            {
                if (open[^1].LocalName == "option") { Close("option"); }
                else { Error("unexpected-option-end-tag"); }
                return;
            }
            if (tag.IsEndTag && tag.Name == "optgroup")
            {
                if (open[^1].LocalName == "option" && open.Count > selectIndex + 1
                    && open[^2].LocalName == "optgroup") { Close("option"); }
                if (open[^1].LocalName == "optgroup") { Close("optgroup"); }
                else { Error("unexpected-optgroup-end-tag"); }
                return;
            }
            if (tag.IsEndTag && tag.Name == "select")
            {
                CloseAt(selectIndex);
                return;
            }
            throw Unsupported("Only option and optgroup children with text are supported inside select elements.");
        }

        private DomElement Insert(HtmlTag token, bool push = true)
        {
            FlushText();
            if (push && open.Count >= options.MaxDepth) { throw new HtmlLimitException("HTML element depth limit exceeded."); }
            CountNode();
            var element = document.CreateElement(token.Name);
            foreach (var (name, value) in token.Attributes) { element.SetAttributeFromParser(name, value); }
            Current.AppendChild(element);
            if (push)
            {
                open.Add(element);
                if (token.SelfClosing) { Error("non-void-html-element-start-tag-with-trailing-solidus"); }
            }

            return element;
        }

        private void StartText(HtmlTag tag, HtmlTextMode textMode)
        {
            Insert(tag);
            originalMode = mode;
            mode = Mode.Text;
            tokenizer.SetTextMode(textMode, tag.Name);
        }

        private void Text(string data)
        {
            if (data.Length == 0) { return; }
            var text = Current.LastChild as DomText;
            if (text is null)
            {
                FlushText();
                CountNode();
                text = document.CreateTextNode("");
                Current.AppendChild(text);
            }

            if (bufferedText != text)
            {
                FlushText();
                bufferedText = text;
                textBuffer.Append(text.Data);
            }

            textBuffer.Append(data);
        }

        private void FlushText()
        {
            if (bufferedText is null) { return; }
            bufferedText.Data = textBuffer.ToString();
            textBuffer.Clear();
            bufferedText = null;
        }

        private bool Miscellaneous(HtmlToken token, DomNode parent)
        {
            if (token is HtmlComment comment)
            {
                FlushText();
                CountNode();
                parent.AppendChild(document.CreateComment(comment.Data));
                return true;
            }

            if (token is HtmlProcessingInstruction instruction)
            {
                FlushText();
                CountNode();
                parent.AppendChild(document.CreateProcessingInstruction(instruction.Target, instruction.Data));
                return true;
            }

            return false;
        }

        private static void Merge(DomElement element, HtmlTag token)
        {
            foreach (var (name, value) in token.Attributes)
            {
                if (element.GetAttribute(name) is null) { element.SetAttributeFromParser(name, value); }
            }
        }

        private void CloseParagraph()
        {
            if (InScope("p", buttonScope: true)) { Close("p"); }
        }

        private void CloseListItem(params string[] names)
        {
            for (var index = open.Count - 1; index >= 0; index--)
            {
                if (names.Contains(open[index].LocalName)) { CloseAt(index); return; }
                if (SpecialTags.Contains(open[index].LocalName) && open[index].LocalName is not ("address" or "div" or "p")) { return; }
            }
        }

        private bool InScope(string name, bool buttonScope = false, bool listScope = false)
        {
            for (var index = open.Count - 1; index >= 0; index--)
            {
                var current = open[index].LocalName;
                if (current == name) { return true; }
                if (current is "html" or "table" or "td" or "th" or "marquee" or "object" or "applet" or "template"
                    || buttonScope && current == "button" || listScope && current is "ol" or "ul") { return false; }
            }

            return false;
        }

        private void Close(string name) => CloseAt(open.FindLastIndex(e => e.LocalName == name));
        private void CloseAt(int index)
        {
            FlushText();
            if (open.Skip(index + 1).Any(e => FormattingTags.Contains(e.LocalName)))
            {
                throw Unsupported("Closing this element requires active-formatting reconstruction.");
            }

            if (formPointer is not null && open.Skip(index).Contains(formPointer))
            {
                throw Unsupported("Implicitly closing an open form would split form ownership from tree ancestry.");
            }

            if (index != open.Count - 1) { Error("implicitly-closed-descendants"); }
            open.RemoveRange(index, open.Count - index);
        }

        private void CountNode()
        {
            if (nodes == options.MaxNodes) { throw new HtmlLimitException("HTML node count limit exceeded."); }
            nodes++;
        }

        private void Error(string code)
        {
            errors.Add(new(code, tokenizer.Offset));
            CheckErrors();
        }

        private void CheckErrors()
        {
            if ((long)errors.Count + tokenizer.Errors.Count > options.MaxErrors) { throw new HtmlLimitException("HTML parse-error limit exceeded."); }
        }

        private UnsupportedHtmlException Unsupported(string message) => new($"{message} At normalized input offset {tokenizer.Offset}.");
    }

    private static HtmlTag Synthetic(string name) => new(name, new Dictionary<string, string>(), false, false);
    private static readonly HashSet<string> UnsupportedTags = new(StringComparer.Ordinal)
    {
        "table", "caption", "colgroup", "col", "tbody", "thead", "tfoot", "tr", "td", "th", "template",
        "svg", "math", "frameset", "frame", "noscript",
        "applet", "marquee", "object", "ruby", "rb", "rt", "rtc", "rp"
    };
    private static readonly HashSet<string> VoidTags = new(StringComparer.Ordinal)
    {
        "area", "base", "basefont", "bgsound", "br", "embed", "hr", "img", "input", "keygen", "link", "meta",
        "param", "source", "track", "wbr"
    };
    private static readonly HashSet<string> HeadTags = new(StringComparer.Ordinal)
    {
        "base", "basefont", "bgsound", "link", "meta", "title", "style", "script", "noframes"
    };
    private static readonly HashSet<string> HeadingTags = new(StringComparer.Ordinal) { "h1", "h2", "h3", "h4", "h5", "h6" };
    private static readonly HashSet<string> FormattingTags = new(StringComparer.Ordinal)
    {
        "a", "b", "big", "code", "em", "font", "i", "nobr", "s", "small", "strike", "strong", "tt", "u"
    };
    private static readonly HashSet<string> BlockTags = new(StringComparer.Ordinal)
    {
        "address", "article", "aside", "blockquote", "center", "details", "dialog", "dir", "div", "dl",
        "fieldset", "figcaption", "figure", "footer", "header", "hgroup", "main", "menu", "nav", "ol",
        "search", "section", "summary", "ul", "h1", "h2", "h3", "h4", "h5", "h6", "pre", "listing"
    };
    private static readonly HashSet<string> SpecialTags = new(BlockTags.Where(name => name != "dialog").Concat(VoidTags).Concat(UnsupportedTags)
        .Concat(["html", "head", "body", "p", "li", "dd", "dt", "button", "form", "textarea", "title", "script",
            "style", "xmp", "plaintext", "iframe", "noembed", "noframes", "select", "option"]), StringComparer.Ordinal);
    private static readonly HashSet<string> ImplicitlyClosable = new(StringComparer.Ordinal)
    {
        "html", "body", "p", "li", "dd", "dt"
    };
}

/// <summary>Completed static DOM plus recoverable parse diagnostics.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#parse-errors">parse errors</see>.
/// Unsupported algorithms and limits throw instead of returning a success-shaped partial tree.</remarks>
public sealed record HtmlParseResult(DomDocument Document, IReadOnlyList<HtmlParseError> Errors);
