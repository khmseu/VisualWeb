using System.Text;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Html;

/// <summary>Static HTML document tree construction with explicit unsupported-feature failures.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#tree-construction">tree construction</see>,
/// including <see href="https://html.spec.whatwg.org/multipage/parsing.html#the-initial-insertion-mode">doctype mode selection</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inbody">in body</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-intable">in table</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-intabletext">in table text</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-incaption">in caption</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#foster-parent">foster parenting</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-incolgroup">in column group</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-intbody">in table body</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-intr">in row</see>,
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-intd">in cell</see>,
/// and <see href="https://html.spec.whatwg.org/multipage/parsing.html#reset-the-insertion-mode-appropriately">insertion-mode reset</see>.
/// Basic captions, table sections, rows, cells, column groups, text-only foster parenting, non-hidden inputs,
/// <c>br</c>/<c>hr</c> starts, the <c>image</c> alias, supported void-element start/end tokens, and
/// supported block/list-item start/end tags in table, table-body, and row modes with
/// bounded foster-parent insertion are supported. <c>span</c> start/end tags also use bounded foster-parent
/// insertion in table, column-group, table-body, and row modes; other formatting-element adoption algorithms
/// in those modes remain unsupported;
/// nested table starts, nested tables in cells, the in-table form-pointer exception and root-attribute
/// merging for in-table <c>html</c> starts are supported; bounded table insertion modes apply within template
/// content, including template EOF recovery;
/// other foster-parenting cases, nested tables in captions, select template content, unsupported fragment contexts, scripting,
/// and adoption-agency reparenting requiring table foster parenting are unsupported.</remarks>
public static class HtmlParser
{
    public static HtmlParseResult Parse(string input, HtmlParserOptions? options = null, CancellationToken cancellationToken = default) =>
        new Builder(input, options ?? new(), cancellationToken).Run();

    /// <summary>Parses an HTML fragment using the context element's supported insertion mode.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#html-fragment-parsing-algorithm">HTML fragment parsing algorithm</see>.</remarks>
    public static HtmlFragmentParseResult ParseFragment(string input, DomElement contextElement,
        HtmlParserOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(contextElement);
        if (UnsupportedFragmentContexts.Contains(contextElement.LocalName))
        {
            throw new UnsupportedHtmlException($"Fragment parsing for <{contextElement.LocalName}> contexts is not implemented.");
        }

        return new Builder(input, options ?? new(), cancellationToken, fragmentContext: contextElement).RunFragment();
    }

    public static HtmlParseResult Parse(string input, HtmlParserOptions? options, CancellationToken cancellationToken,
        DomDocument document, Action<DomElement>? scriptCompleted)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.ChildNodes.Count != 0) { throw new ArgumentException("The parser destination document must be empty.", nameof(document)); }
        return new Builder(input, options ?? new(), cancellationToken, document, scriptCompleted).Run();
    }

    private enum Mode
    {
        Initial, BeforeHtml, BeforeHead, InHead, AfterHead, InBody, Text,
        InTable, InTableText, InCaption, InColumnGroup, InTableBody, InRow, InCell, InTemplate, AfterBody, AfterAfterBody
    }

    private sealed class Builder
    {
        private readonly DomDocument document;
        private readonly DomDocument ownerDocument;
        private readonly List<DomElement> open = [];
        private readonly List<FormattingEntry?> formatting = [];
        private readonly List<Mode> templateModes = [];
        private readonly List<HtmlParseError> errors = [];
        private readonly HtmlParserOptions options;
        private readonly HtmlTokenizer tokenizer;
        private readonly CancellationToken cancellationToken;
        private readonly Action<DomElement>? scriptCompleted;
        private readonly DomElement? fragmentContext;
        private readonly DomDocumentFragment? fragment;
        private DomElement? fragmentRoot;
        private Mode mode;
        private Mode originalMode;
        private int nodes = 1;
        private bool ignoreNextLineFeed;
        private bool fosterParenting;
        private DomElement? formPointer;
        private DomElement? headElementPointer;
        private DomText? bufferedText;
        private readonly StringBuilder textBuffer = new();
        private readonly StringBuilder tableTextBuffer = new();

        private sealed record FormattingEntry(DomElement Element, HtmlTag Token);

        public Builder(string input, HtmlParserOptions options, CancellationToken cancellationToken,
            DomDocument? document = null, Action<DomElement>? scriptCompleted = null, DomElement? fragmentContext = null)
        {
            options.Validate();
            this.options = options;
            this.cancellationToken = cancellationToken;
            this.fragmentContext = fragmentContext;
            if (fragmentContext is null)
            {
                this.document = document ?? new DomDocument();
                ownerDocument = this.document;
                tokenizer = new(input, options: options, cancellationToken: cancellationToken);
            }
            else
            {
                var contextDocument = fragmentContext.OwnerDocument
                    ?? throw new ArgumentException("The fragment context must have an owner document.", nameof(fragmentContext));
                ownerDocument = contextDocument;
                this.document = new DomDocument { Mode = contextDocument.Mode };
                fragment = ownerDocument.CreateDocumentFragment();
                nodes = 0;
                tokenizer = new(input, InitialFragmentTextMode(fragmentContext.LocalName),
                    options: options, cancellationToken: cancellationToken);
                for (DomNode? ancestor = fragmentContext; ancestor is not null; ancestor = ancestor.ParentNode)
                {
                    if (ancestor is DomElement { LocalName: "form" } form)
                    {
                        formPointer = form;
                        break;
                    }
                }
            }

            this.scriptCompleted = scriptCompleted;
        }

        public HtmlParseResult Run()
        {
            RunTokens();
            return new(document, AllErrors());
        }

        public HtmlFragmentParseResult RunFragment()
        {
            if (fragmentContext is null || fragment is null)
            {
                throw new InvalidOperationException("The builder was not initialized for fragment parsing.");
            }

            fragmentRoot = document.CreateElement("html");
            document.AppendChild(fragmentRoot);
            open.Add(fragmentRoot);
            if (fragmentContext.LocalName == "template") { templateModes.Add(Mode.InTemplate); }
            mode = FragmentInsertionMode(fragmentContext.LocalName);
            RunTokens();
            return new(fragment, AllErrors());
        }

        private void RunTokens()
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
                    return;
                }
            }
        }

        private DomNode Current => open.Count == 0 ? document : open[^1];
        private DomNode AdjustedCurrentNode =>
            fragmentRoot is not null && Current == fragmentRoot
                ? fragmentContext ?? throw new InvalidOperationException("The fragment context is not set.")
                : Current;
        private DomElement HeadElement => headElementPointer ?? document.Head
            ?? throw new InvalidOperationException("The parser head element pointer is not set.");
        private DomNode FragmentOrDocumentElement => (DomNode?)fragment ?? document.DocumentElement!;
        private DomNode InsertionParent => AdjustedInsertionParent(Current);
        private DomNode AdjustedInsertionParent(DomNode intendedParent) =>
            fragmentRoot is not null && intendedParent == fragmentRoot
                ? fragment! : intendedParent is DomTemplateElement template ? template.Content : intendedParent;
        private bool IsFragment => fragment is not null;
        private IReadOnlyList<HtmlParseError> AllErrors() =>
            tokenizer.Errors.Concat(errors).OrderBy(error => error.Offset).ToList().AsReadOnly();

        private static HtmlTextMode InitialFragmentTextMode(string contextName) => contextName switch
        {
            "title" or "textarea" => HtmlTextMode.Rcdata,
            "style" or "xmp" or "iframe" or "noembed" or "noframes" => HtmlTextMode.Rawtext,
            "script" => HtmlTextMode.ScriptData,
            "plaintext" => HtmlTextMode.Plaintext,
            _ => HtmlTextMode.Data
        };

        private Mode FragmentInsertionMode(string contextName) => contextName switch
        {
            "html" => headElementPointer is null ? Mode.BeforeHead : Mode.AfterHead,
            "table" => Mode.InTable,
            "caption" => Mode.InCaption,
            "colgroup" => Mode.InColumnGroup,
            "tbody" or "thead" or "tfoot" => Mode.InTableBody,
            "tr" => Mode.InRow,
            "td" or "th" => Mode.InCell,
            "template" => Mode.InTemplate,
            _ => Mode.InBody
        };

        private void CompleteScript()
        {
            if (!IsFragment && !open.Any(element => element.LocalName == "template")
                && Current is DomElement { LocalName: "script" } script)
            {
                scriptCompleted?.Invoke(script);
            }
        }
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

                if (token is HtmlTag contextTag)
                {
                    if (open.Any(element => element.LocalName == "template"))
                    {
                        if (contextTag.Name == "select")
                        {
                            throw Unsupported("Select insertion modes inside template content are not implemented.");
                        }
                    }

                    if (contextTag.Name == "template" && open.Any(element => element.LocalName == "select"))
                    {
                        throw Unsupported("Template insertion inside select content is not implemented.");
                    }
                }

                if (token is HtmlTag tableTag && TableTags.Contains(tableTag.Name)
                    && !IsTableMode(mode) && mode != Mode.InTemplate
                    && !(tableTag.Name == "table"
                        && mode is Mode.Initial or Mode.BeforeHtml or Mode.BeforeHead or Mode.InHead or Mode.AfterHead or Mode.InBody))
                {
                    throw Unsupported($"Table tag <{tableTag.Name}> is outside the supported table tree-building subset.");
                }

                if (token is HtmlEndOfFile
                    && mode is Mode.InBody or Mode.InTable or Mode.InCaption or Mode.InColumnGroup
                        or Mode.InTableBody or Mode.InRow or Mode.InCell
                    && open.Any(element => element.LocalName == "template"))
                {
                    mode = Mode.InTemplate;
                }

                switch (mode)
                {
                    case Mode.Initial:
                        if (IsWhitespace(token)) { return; }
                        if (Miscellaneous(token, document)) { return; }
                        if (token is HtmlDoctype doctype)
                        {
                            if (doctype.Name != "html" || doctype.PublicId is not null
                                || doctype.SystemId is not null && doctype.SystemId != "about:legacy-compat")
                            {
                                Error("invalid-doctype");
                            }

                            CountNode();
                            document.AppendChild(document.CreateDocumentType(doctype.Name ?? "", doctype.PublicId ?? "", doctype.SystemId ?? ""));
                            document.Mode = DoctypeMode(doctype);
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
                        if (Miscellaneous(token, InsertionParent)) { return; }
                        if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
                        if (token is HtmlTag { IsEndTag: false, Name: "html" } beforeHeadHtml)
                        {
                            Merge(document.DocumentElement!, beforeHeadHtml);
                            return;
                        }

                        if (token is HtmlTag { IsEndTag: false, Name: "head" } head)
                        {
                            headElementPointer = Insert(head);
                            mode = Mode.InHead;
                            return;
                        }

                        if (token is HtmlTag { IsEndTag: true } beforeHeadEnd && beforeHeadEnd.Name is not ("head" or "body" or "html" or "br"))
                        {
                            Error("unexpected-end-tag");
                            return;
                        }

                        headElementPointer = Insert(Synthetic("head"));
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
                                case "template": StartTemplate(headStart); return;
                                case "head": Error("unexpected-head-start-tag"); return;
                            }
                        }

                        if (token is HtmlTag { IsEndTag: true, Name: "head" })
                        {
                            open.RemoveAt(open.Count - 1);
                            mode = Mode.AfterHead;
                            return;
                        }

                        if (token is HtmlTag { IsEndTag: true, Name: "template" })
                        {
                            CloseTemplate();
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
                        if (Miscellaneous(token, InsertionParent)) { return; }
                        if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
                        if (token is HtmlTag { IsEndTag: false } afterHeadStart)
                        {
                            if (afterHeadStart.Name == "html") { Merge(document.DocumentElement!, afterHeadStart); return; }
                            if (afterHeadStart.Name == "body") { Insert(afterHeadStart); mode = Mode.InBody; return; }
                            if (afterHeadStart.Name == "head") { Error("unexpected-head-start-tag"); return; }
                            if (HeadTags.Contains(afterHeadStart.Name))
                            {
                                Error("head-content-after-head");
                                open.Add(HeadElement);
                                mode = Mode.InHead;
                                Process(token);
                                if (mode == Mode.Text)
                                {
                                    originalMode = Mode.AfterHead;
                                }
                                else if (mode == Mode.InTemplate)
                                {
                                    open.Remove(HeadElement);
                                }
                                else
                                {
                                    open.Remove(HeadElement);
                                    mode = Mode.AfterHead;
                                }

                                return;
                            }
                        }

                        if (token is HtmlTag { IsEndTag: true, Name: "template" })
                        {
                            mode = Mode.InHead;
                            Process(token);
                            if (mode == Mode.InHead) { mode = Mode.AfterHead; }
                            return;
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
                            FlushText();
                            CompleteScript();
                            open.RemoveAt(open.Count - 1);
                            if (originalMode == Mode.AfterHead) { open.Remove(HeadElement); }
                            mode = originalMode;
                            continue;
                        }

                        if (token is HtmlTag { IsEndTag: true })
                        {
                            FlushText();
                            CompleteScript();
                            open.RemoveAt(open.Count - 1);
                            if (originalMode == Mode.AfterHead) { open.Remove(HeadElement); }
                            mode = originalMode;
                            return;
                        }

                        throw new InvalidOperationException("Text tokenizer emitted an unexpected token.");
                    case Mode.AfterBody:
                        if (IsWhitespace(token)) { BodyToken(token); return; }
                        if (Miscellaneous(token, FragmentOrDocumentElement)) { return; }
                        if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
                        if (token is HtmlTag { IsEndTag: false, Name: "html" } afterBodyHtml) { Merge(document.DocumentElement!, afterBodyHtml); return; }
                        if (token is HtmlTag { IsEndTag: true, Name: "html" })
                        {
                            if (fragmentContext is not null) { Error("unexpected-html-end-tag"); }
                            else { mode = Mode.AfterAfterBody; }

                            return;
                        }
                        if (token is HtmlEndOfFile) { return; }
                        Error("content-after-body");
                        mode = Mode.InBody;
                        continue;
                    case Mode.AfterAfterBody:
                        if (Miscellaneous(token, (DomNode?)fragment ?? document)) { return; }
                        if (token is HtmlEndOfFile) { return; }
                        if (IsWhitespace(token)) { BodyToken(token); return; }
                        if (token is HtmlTag { IsEndTag: false, Name: "html" } finalHtml) { Merge(document.DocumentElement!, finalHtml); return; }
                        Error("content-after-html");
                        mode = Mode.InBody;
                        continue;
                    case Mode.InBody:
                        BodyToken(token);
                        return;
                    case Mode.InTemplate:
                        if (TemplateToken(token)) { continue; }
                        return;
                    case Mode.InTable:
                        TableToken(token);
                        return;
                    case Mode.InTableText:
                        TableTextToken(token);
                        return;
                    case Mode.InCaption:
                        CaptionToken(token);
                        return;
                    case Mode.InColumnGroup:
                        ColumnGroupToken(token);
                        return;
                    case Mode.InTableBody:
                        TableBodyToken(token);
                        return;
                    case Mode.InRow:
                        TableRowToken(token);
                        return;
                    case Mode.InCell:
                        TableCellToken(token);
                        return;
                }
            }
        }

        private void BodyToken(HtmlToken token, bool fromSelectContext = false)
        {
            if (token is HtmlCharacters text)
            {
                if (text.Data.Contains('\0')) { Error("null-in-body"); }
                var data = text.Data.Replace("\0", "", StringComparison.Ordinal);
                if (data.Length > 0 && !fromSelectContext && fragmentContext?.LocalName != "select"
                    && !open.Any(element => element.LocalName == "select"))
                {
                    ReconstructFormatting();
                }

                Text(data);
                return;
            }

            if (Miscellaneous(token, InsertionParent)) { return; }
            if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
            if (token is HtmlEndOfFile)
            {
                var selectIndexAtEof = open.FindLastIndex(element => element.LocalName == "select");
                if (!fromSelectContext && selectIndexAtEof >= 0) { SelectToken(token, selectIndexAtEof); return; }
                if (!fromSelectContext && fragmentContext?.LocalName == "select")
                {
                    SelectToken(token, -1);
                    return;
                }

                if (!IsFragment && open.Any(e => !ImplicitlyClosable.Contains(e.LocalName)))
                {
                    Error("unclosed-elements-at-eof");
                }

                return;
            }

            if (token is not HtmlTag tag) { throw new InvalidOperationException("Unknown HTML token."); }
            var selectIndex = open.FindLastIndex(element => element.LocalName == "select");
            if (!fromSelectContext && selectIndex >= 0)
            {
                SelectToken(token, selectIndex);
                return;
            }
            if (!fromSelectContext && fragmentContext?.LocalName == "select")
            {
                SelectToken(token, -1);
                return;
            }

            if (!tag.IsEndTag)
            {
                switch (tag.Name)
                {
                    case "html":
                        if (document.DocumentElement is { } root) { Merge(root, tag); }
                        Error("html-start-in-body");
                        return;
                    case "body":
                        if (document.Body is { } body) { Merge(body, tag); }
                        Error("body-start-in-body");
                        return;
                    case "head": Error("unexpected-head-start-tag"); return;
                    case "title": StartText(tag, HtmlTextMode.Rcdata); return;
                    case "style" or "noframes": StartText(tag, HtmlTextMode.Rawtext); return;
                    case "script": StartText(tag, HtmlTextMode.ScriptData); return;
                    case "template": StartTemplate(tag); return;
                    case "textarea": StartTextarea(tag); return;
                    case "xmp":
                        CloseParagraph();
                        ReconstructFormatting();
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
                        if (open.Any(element => element.LocalName == "template"))
                        {
                            Insert(tag);
                            return;
                        }

                        CloseParagraph();
                        formPointer = Insert(tag);
                        return;
                    case "button":
                        if (InScope("button")) { Error("nested-button"); Close("button"); }
                        ReconstructFormatting();
                        Insert(tag);
                        return;
                    case "select":
                        if (InScope("select")) { throw Unsupported("Nested select elements are not implemented."); }
                        ReconstructFormatting();
                        Insert(tag);
                        return;
                    case "rb":
                    case "rtc":
                        if (InScope("ruby")) { GenerateImpliedEndTags(); }
                        if (open[^1].LocalName != "ruby") { Error("ruby-base-start-tag-without-current-ruby"); }
                        break;
                    case "rp":
                    case "rt":
                        if (InScope("ruby")) { GenerateImpliedEndTags(except: "rtc"); }
                        if (open[^1].LocalName is not ("ruby" or "rtc"))
                        {
                            Error("ruby-annotation-start-tag-without-current-ruby-or-rtc");
                        }
                        break;
                    case "table":
                        CloseParagraph();
                        Insert(tag);
                        mode = Mode.InTable;
                        return;
                    case "option":
                    case "optgroup":
                        if (open.LastOrDefault()?.LocalName == "option") { Close("option"); }
                        break;
                    case "hr":
                        CloseParagraph();
                        Insert(tag, push: false);
                        return;
                    case "image":
                        Error("image-start-tag");
                        ReconstructFormatting();
                        Insert(tag with { Name = "img" }, push: false);
                        return;
                    case "a":
                        var anchorIndex = FindFormatting("a");
                        if (anchorIndex >= 0)
                        {
                            Error("nested-a-start-tag");
                            var originalAnchor = formatting[anchorIndex]!.Element;
                            AdoptionEndTag("a");
                            var remainingEntry = FindFormattingElementIndex(originalAnchor);
                            if (remainingEntry >= 0) { formatting.RemoveAt(remainingEntry); }
                            var remainingOpen = open.IndexOf(originalAnchor);
                            if (remainingOpen >= 0)
                            {
                                FlushText();
                                open.RemoveAt(remainingOpen);
                            }
                        }
                        break;
                    case "nobr":
                        ReconstructFormatting();
                        if (InScope("nobr"))
                        {
                            Error("nested-nobr-start-tag");
                            AdoptionEndTag("nobr");
                        }
                        break;
                }

                if (BlockTags.Contains(tag.Name)) { CloseParagraph(); }
                if (HeadingTags.Contains(tag.Name) && open.LastOrDefault() is { } current && HeadingTags.Contains(current.LocalName))
                {
                    Error("nested-heading");
                    open.RemoveAt(open.Count - 1);
                }

                if (FormattingTags.Contains(tag.Name))
                {
                    // For nobr this is the second reconstruction after adoption, if needed.
                    ReconstructFormatting();
                    var element = Insert(tag);
                    PushFormatting(element, tag);
                    return;
                }

                if (!BlockTags.Contains(tag.Name) && tag.Name is not ("rb" or "rtc" or "rp" or "rt"
                    or "param" or "source" or "track"))
                {
                    ReconstructFormatting();
                }

                Insert(tag, push: !VoidTags.Contains(tag.Name));
                return;
            }

            switch (tag.Name)
            {
                case "template":
                    CloseTemplate();
                    return;
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
                        if (node is null || !InScope("form")) { Error("unexpected-form-end-tag"); return; }
                        formPointer = null;
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
                    ReconstructFormatting();
                    Insert(Synthetic("br"), push: false);
                    return;
            }

            if (FormattingTags.Contains(tag.Name))
            {
                AdoptionEndTag(tag.Name);
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

            AnyOtherEndTag(tag.Name);
        }

        private void AnyOtherEndTag(string name)
        {
            for (var index = open.Count - 1; index >= 0; index--)
            {
                if (open[index].LocalName == name)
                {
                    CloseAt(index);
                    return;
                }

                if (SpecialTags.Contains(open[index].LocalName)) { break; }
            }

            Error("unexpected-end-tag");
        }

        private int FindFormatting(string name)
        {
            for (var index = formatting.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = formatting[index];
                if (entry is null) { break; }
                if (entry.Token.Name == name) { return index; }
            }

            return -1;
        }

        private bool IsFormattingElement(DomElement element)
        {
            return FindFormattingElementIndex(element) >= 0;
        }

        private int FindFormattingElementIndex(DomElement element)
        {
            for (var index = formatting.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (formatting[index]?.Element == element) { return index; }
            }

            return -1;
        }

        private void PushFormatting(DomElement element, HtmlTag token)
        {
            var snapshot = token with
            {
                Attributes = new Dictionary<string, string>(token.Attributes, StringComparer.Ordinal),
                SelfClosing = false
            };
            var count = 0;
            var earliest = -1;
            for (var index = formatting.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = formatting[index];
                if (entry is null) { break; }
                if (entry.Token.Name == snapshot.Name && entry.Token.Attributes.Count == snapshot.Attributes.Count
                    && entry.Token.Attributes.All(attribute => snapshot.Attributes.TryGetValue(attribute.Key, out var value)
                        && value == attribute.Value))
                {
                    earliest = index;
                    if (++count == 3) { break; }
                }
            }

            if (earliest >= 0 && count == 3) { formatting.RemoveAt(earliest); }
            formatting.Add(new(element, snapshot));
        }

        private void ReconstructFormatting()
        {
            var index = formatting.Count - 1;
            if (index < 0 || formatting[index] is not { } last || open.Contains(last.Element)) { return; }
            while (index > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var previous = formatting[index - 1];
                if (previous is null || open.Contains(previous.Element)) { break; }
                index--;
            }

            for (; index < formatting.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = formatting[index] ?? throw new InvalidOperationException("Unexpected formatting marker.");
                formatting[index] = entry with { Element = Insert(entry.Token) };
            }
        }

        private void ClearFormattingToMarker()
        {
            for (var index = formatting.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isMarker = formatting[index] is null;
                formatting.RemoveAt(index);
                if (isMarker) { return; }
            }
        }

        private void AdoptionEndTag(string name)
        {
            if (open.Count > 0 && open[^1].LocalName == name
                && !IsFormattingElement(open[^1]))
            {
                FlushText();
                open.RemoveAt(open.Count - 1);
                return;
            }

            for (var outerLoopCounter = 0; outerLoopCounter < 8; outerLoopCounter++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entryIndex = FindFormatting(name);
                if (entryIndex < 0)
                {
                    AnyOtherEndTag(name);
                    return;
                }

                var formattingEntry = formatting[entryIndex]!;
                var formattingElement = formattingEntry.Element;
                var stackIndex = open.IndexOf(formattingElement);
                if (stackIndex < 0)
                {
                    Error("formatting-element-not-open");
                    formatting.RemoveAt(entryIndex);
                    return;
                }

                for (var index = open.Count - 1; index > stackIndex; index--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (open[index].LocalName is "html" or "caption" or "table" or "td" or "th" or "marquee" or "object" or "applet" or "template")
                    {
                        Error("formatting-element-not-in-scope");
                        return;
                    }
                }

                if (stackIndex != open.Count - 1) { Error("misnested-formatting-end-tag"); }
                var furthestBlockIndex = -1;
                for (var index = open.Count - 1; index > stackIndex; index--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (SpecialTags.Contains(open[index].LocalName))
                    {
                        furthestBlockIndex = index;
                        break;
                    }
                }

                if (furthestBlockIndex < 0)
                {
                    if (formPointer is not null && open.Skip(stackIndex).Contains(formPointer))
                    {
                        throw Unsupported("Adoption agency would remove the active form pointer from the open-element stack.");
                    }

                    FlushText();
                    open.RemoveRange(stackIndex, open.Count - stackIndex);
                    formatting.RemoveAt(entryIndex);
                    return;
                }

                if (stackIndex == 0) { throw new InvalidOperationException("A formatting element has no common ancestor."); }
                var commonAncestor = open[stackIndex - 1];
                if (commonAncestor.LocalName is "table" or "tbody" or "tfoot" or "thead" or "tr")
                {
                    throw Unsupported("Adoption-agency reparenting would require table foster parenting.");
                }

                FlushText();
                var bookmark = entryIndex;
                var nodeIndex = furthestBlockIndex;
                var furthestBlock = open[furthestBlockIndex];
                DomElement lastNode = furthestBlock;
                var innerLoopCounter = 0;
                var bookmarkMoved = false;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    nodeIndex--;
                    var node = open[nodeIndex];
                    if (node == formattingElement) { break; }

                    innerLoopCounter++;
                    var nodeEntryIndex = FindFormattingElementIndex(node);
                    if (innerLoopCounter > 3 && nodeEntryIndex >= 0)
                    {
                        if (nodeEntryIndex < bookmark) { bookmark--; }
                        formatting.RemoveAt(nodeEntryIndex);
                        nodeEntryIndex = -1;
                    }

                    if (nodeEntryIndex < 0)
                    {
                        if (node == formPointer)
                        {
                            throw Unsupported("Adoption agency would remove the active form pointer from the open-element stack.");
                        }

                        open.RemoveAt(nodeIndex);
                        continue;
                    }

                    var nodeEntry = formatting[nodeEntryIndex]!;
                    var newNode = CreateElementFromToken(nodeEntry.Token);
                    formatting[nodeEntryIndex] = nodeEntry with { Element = newNode };
                    open[nodeIndex] = newNode;
                    if (!bookmarkMoved)
                    {
                        bookmark = nodeEntryIndex + 1;
                        bookmarkMoved = true;
                    }

                    newNode.AppendChild(lastNode);
                    lastNode = newNode;
                }

                AdjustedInsertionParent(commonAncestor).AppendChild(lastNode);
                var newFormattingElement = CreateElementFromToken(formattingEntry.Token);
                while (furthestBlock.FirstChild is { } child)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    newFormattingElement.AppendChild(child);
                }

                furthestBlock.AppendChild(newFormattingElement);

                entryIndex = FindFormattingElementIndex(formattingElement);
                if (entryIndex < 0) { throw new InvalidOperationException("The formatting element disappeared during adoption."); }
                if (entryIndex < bookmark) { bookmark--; }
                formatting.RemoveAt(entryIndex);
                bookmark = Math.Clamp(bookmark, 0, formatting.Count);
                formatting.Insert(bookmark, new(newFormattingElement, formattingEntry.Token));

                open.RemoveAt(stackIndex);
                var updatedFurthestBlockIndex = open.IndexOf(furthestBlock);
                if (updatedFurthestBlockIndex < 0) { throw new InvalidOperationException("The furthest block disappeared during adoption."); }
                open.Insert(updatedFurthestBlockIndex + 1, newFormattingElement);
            }
        }

        private void TableToken(HtmlToken token)
        {
            if (token is HtmlCharacters characters)
            {
                if (AdjustedCurrentNode is not DomElement
                    { LocalName: "table" or "tbody" or "template" or "tfoot" or "thead" or "tr" })
                {
                    ProcessFosterParentedBodyToken(characters, "unexpected-character-in-table");
                    return;
                }

                originalMode = mode;
                mode = Mode.InTableText;
                Process(characters);
                return;
            }

            if (Miscellaneous(token, InsertionParent)) { return; }
            if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
            if (token is HtmlEndOfFile) { BodyToken(token); return; }

            if (token is not HtmlTag tag) { throw new InvalidOperationException("Unknown HTML token in table."); }
            if (tag.Name == "template")
            {
                if (tag.IsEndTag) { CloseTemplate(); }
                else { StartTemplate(tag); }

                return;
            }

            if (!tag.IsEndTag)
            {
                if (FosterParentedVoidTags.Contains(tag.Name) || BlockTags.Contains(tag.Name)
                    || tag.Name is "p" or "li" or "dd" or "dt" or "button" or "img" or "image" or "wbr" or "textarea" or "title"
                        or "iframe" or "noembed" or "noframes" or "xmp" or "span")
                {
                    ProcessFosterParentedBodyToken(tag, $"fostered-{tag.Name}-start-in-table");
                    return;
                }

                switch (tag.Name)
                {
                    case "html":
                        BodyToken(tag);
                        return;
                    case "table":
                        if (!InTableScope("table"))
                        {
                            Error("unexpected-table-start-tag");
                            return;
                        }

                        Error("nested-table-start-tag");
                        CloseAt(open.FindLastIndex(element => element.LocalName == "table"));
                        ResetInsertionModeAppropriately();
                        Process(tag);
                        return;
                    case "tbody" or "tfoot" or "thead":
                        ClearStackToTableContext();
                        Insert(tag);
                        mode = Mode.InTableBody;
                        return;
                    case "caption":
                        ClearStackToTableContext();
                        formatting.Add(null);
                        Insert(tag);
                        mode = Mode.InCaption;
                        return;
                    case "colgroup":
                        ClearStackToTableContext();
                        Insert(tag);
                        mode = Mode.InColumnGroup;
                        return;
                    case "col":
                        ClearStackToTableContext();
                        Insert(Synthetic("colgroup"));
                        mode = Mode.InColumnGroup;
                        Process(tag);
                        return;
                    case "tr" or "td" or "th":
                        ClearStackToTableContext();
                        Insert(Synthetic("tbody"));
                        mode = Mode.InTableBody;
                        Process(tag);
                        return;
                    case "input":
                        if (!tag.Attributes.TryGetValue("type", out var inputType)
                            || !AsciiEqualsIgnoreCase(inputType, "hidden"))
                        {
                            ProcessFosterParentedBodyToken(tag, "non-hidden-input-in-table");
                            return;
                        }

                        Error("hidden-input-in-table");
                        Insert(tag, push: false);
                        return;
                    case "br" or "hr":
                        ProcessFosterParentedBodyToken(tag, $"fostered-{tag.Name}-in-table");
                        return;
                    case "form":
                        Error("form-start-in-table");
                        if (formPointer is not null) { return; }
                        formPointer = Insert(tag);
                        open.RemoveAt(open.Count - 1);
                        return;
                    case "style":
                        StartText(tag, HtmlTextMode.Rawtext);
                        return;
                    case "script":
                        StartText(tag, HtmlTextMode.ScriptData);
                        return;
                    default:
                        throw Unsupported("Foster parenting for table content is not implemented.");
                }
            }

            if (FosterParentedVoidTags.Contains(tag.Name) || BlockTags.Contains(tag.Name)
                || tag.Name is "p" or "li" or "dd" or "dt" or "button" or "img" or "image" or "wbr" or "textarea" or "title"
                    or "iframe" or "noembed" or "noframes" or "xmp" or "span")
            {
                ProcessFosterParentedBodyToken(tag, $"fostered-{tag.Name}-end-in-table");
                return;
            }

            if (tag.Name == "form")
            {
                BodyToken(tag);
                return;
            }

            if (tag.Name == "table")
            {
                if (!InTableScope("table")) { Error("unexpected-table-end-tag"); return; }
                CloseAt(open.FindLastIndex(element => element.LocalName == "table"));
                ResetInsertionModeAppropriately();
                return;
            }

            if (tag.Name is "body" or "caption" or "col" or "colgroup" or "html"
                or "tbody" or "td" or "tfoot" or "th" or "thead" or "tr")
            {
                Error("unexpected-table-end-tag");
                return;
            }

            throw Unsupported("Foster parenting for table content is not implemented.");
        }

        private void ProcessFosterParentedBodyToken(HtmlToken token, string errorCode)
        {
            Error(errorCode);
            var previousFosterParenting = fosterParenting;
            fosterParenting = true;
            try { BodyToken(token); }
            finally { fosterParenting = previousFosterParenting; }
        }

        private void CaptionToken(HtmlToken token)
        {
            if (token is HtmlTag { IsEndTag: true, Name: "caption" })
            {
                CloseCaption();
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "table" }
                || token is HtmlTag { IsEndTag: false, Name: "caption" or "col" or "colgroup" or "tbody" or "td" or "tfoot" or "th" or "thead" or "tr" })
            {
                if (!InTableScope("caption")) { Error("unexpected-caption-tag"); return; }
                Error("caption-closed-by-table-token");
                if (!CloseCaption()) { return; }
                Process(token);
                return;
            }

            if (token is HtmlTag { IsEndTag: false, Name: "table" })
            {
                throw Unsupported("Nested tables inside captions are not implemented.");
            }

            if (token is HtmlTag { IsEndTag: true, Name: "body" or "col" or "colgroup" or "html" or "tbody" or "td" or "tfoot" or "th" or "thead" or "tr" })
            {
                Error("unexpected-caption-end-tag");
                return;
            }

            BodyToken(token);
        }

        private bool CloseCaption()
        {
            var captionIndex = open.FindLastIndex(element => element.LocalName == "caption");
            if (captionIndex < 0 || !InTableScope("caption"))
            {
                Error("unexpected-caption-end-tag");
                return false;
            }

            GenerateImpliedEndTags();
            CloseAt(captionIndex);
            ClearFormattingToMarker();
            mode = Mode.InTable;
            return true;
        }

        private void TableTextToken(HtmlToken token)
        {
            if (token is HtmlCharacters characters)
            {
                var data = characters.Data;
                if (data.Contains('\0'))
                {
                    Error("null-in-table-text");
                    data = data.Replace("\0", "", StringComparison.Ordinal);
                }

                tableTextBuffer.Append(data);
                return;
            }

            FlushTableText();
            mode = originalMode;
            Process(token);
        }

        private void FlushTableText()
        {
            if (tableTextBuffer.Length == 0) { return; }
            var data = tableTextBuffer.ToString();
            tableTextBuffer.Clear();
            if (data.All(HtmlTokenizer.Whitespace))
            {
                Text(data);
                return;
            }

            Error("non-whitespace-table-text");
            FosterTableText(data);
        }

        private void FosterTableText(string data)
        {
            if (formatting.LastOrDefault() is { } entry && !open.Contains(entry.Element))
            {
                throw Unsupported("Foster parenting requiring active-formatting reconstruction is not implemented.");
            }

            var tableIndex = open.FindLastIndex(element => element.LocalName == "table");
            if (tableIndex < 0 && fragmentRoot is not null)
            {
                FlushText();
                var fragmentText = fragment!.LastChild as DomText;
                if (fragmentText is not null)
                {
                    fragmentText.Data += data;
                    return;
                }

                CountNode();
                fragment.AppendChild(ownerDocument.CreateTextNode(data));
                return;
            }

            if (tableIndex < 0 || open[tableIndex].ParentNode is not { } parent || parent is DomDocument)
            {
                throw Unsupported("The foster-parent insertion location for this table is not implemented.");
            }

            var table = open[tableIndex];
            FlushText();
            if (table.PreviousSibling is DomText preceding)
            {
                preceding.Data += data;
                return;
            }

            CountNode();
            parent.InsertBefore(ownerDocument.CreateTextNode(data), table);
        }

        private void ColumnGroupToken(HtmlToken token)
        {
            if (token is HtmlTag { Name: "template" } templateTag)
            {
                if (templateTag.IsEndTag) { CloseTemplate(); }
                else { StartTemplate(templateTag); }

                return;
            }

            if (token is HtmlCharacters characters)
            {
                if (characters.Data.All(HtmlTokenizer.Whitespace))
                {
                    Text(characters.Data);
                    return;
                }

                if (Current is not DomElement { LocalName: "colgroup" })
                {
                    Error("unexpected-column-group-content");
                    return;
                }

                Error("non-whitespace-column-group-content");
                open.RemoveAt(open.Count - 1);
                mode = Mode.InTable;
                Process(token);
                return;
            }

            if (Miscellaneous(token, InsertionParent)) { return; }
            if (token is HtmlDoctype) { Error("unexpected-doctype"); return; }
            if (token is HtmlEndOfFile) { BodyToken(token); return; }
            if (token is HtmlTag { IsEndTag: false, Name: "html" }) { BodyToken(token); return; }
            if (token is HtmlTag { IsEndTag: false, Name: "col" } column)
            {
                Insert(column, push: false);
                return;
            }
            if (token is HtmlTag { IsEndTag: true, Name: "colgroup" })
            {
                if (Current is not DomElement { LocalName: "colgroup" })
                {
                    Error("unexpected-colgroup-end-tag");
                    return;
                }

                open.RemoveAt(open.Count - 1);
                mode = Mode.InTable;
                return;
            }
            if (token is HtmlTag { IsEndTag: true, Name: "col" })
            {
                Error("unexpected-col-end-tag");
                return;
            }

            if (Current is not DomElement { LocalName: "colgroup" })
            {
                Error("unexpected-column-group-content");
                return;
            }

            open.RemoveAt(open.Count - 1);
            mode = Mode.InTable;
            Process(token);
        }

        private void TableBodyToken(HtmlToken token)
        {
            if (token is HtmlTag { IsEndTag: false, Name: "tr" } row)
            {
                ClearStackToTableBodyContext();
                Insert(row);
                mode = Mode.InRow;
                return;
            }

            if (token is HtmlTag { IsEndTag: false, Name: "td" or "th" } cell)
            {
                Error("cell-start-without-row");
                ClearStackToTableBodyContext();
                Insert(Synthetic("tr"));
                mode = Mode.InRow;
                Process(cell);
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "tbody" or "tfoot" or "thead" } sectionEnd)
            {
                if (!InTableScope(sectionEnd.Name)) { Error("unexpected-table-section-end-tag"); return; }
                ClearStackToTableBodyContext();
                CloseAt(open.Count - 1);
                mode = Mode.InTable;
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "table" } tableEnd)
            {
                var sectionIndex = open.FindLastIndex(element => element.LocalName is "tbody" or "tfoot" or "thead");
                if (sectionIndex < 0 || !InTableScope(open[sectionIndex].LocalName))
                {
                    Error("unexpected-table-end-tag");
                    return;
                }

                ClearStackToTableBodyContext();
                CloseAt(open.Count - 1);
                mode = Mode.InTable;
                Process(tableEnd);
                return;
            }

            if (token is HtmlTag { IsEndTag: false, Name: "caption" or "col" or "colgroup" or "tbody" or "tfoot" or "thead" })
            {
                Error("unexpected-table-section-start-tag");
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "body" or "caption" or "col" or "colgroup" or "html" or "td" or "th" or "tr" })
            {
                Error("unexpected-table-end-tag");
                return;
            }

            TableToken(token);
        }

        private void TableRowToken(HtmlToken token)
        {
            if (token is HtmlTag { IsEndTag: false, Name: "td" or "th" } cell)
            {
                ClearStackToTableRowContext();
                Insert(cell);
                formatting.Add(null);
                mode = Mode.InCell;
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "tr" })
            {
                if (!InTableScope("tr")) { Error("unexpected-row-end-tag"); return; }
                ClearStackToTableRowContext();
                CloseAt(open.Count - 1);
                mode = Mode.InTableBody;
                return;
            }

            if (token is HtmlTag { IsEndTag: false, Name: "caption" or "col" or "colgroup" or "tbody" or "tfoot" or "thead" or "tr" })
            {
                Error("unexpected-table-start-tag-in-row");
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "table" } tableEnd)
            {
                if (!InTableScope("tr")) { Error("unexpected-table-end-tag"); return; }
                ClearStackToTableRowContext();
                CloseAt(open.Count - 1);
                mode = Mode.InTableBody;
                Process(tableEnd);
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "tbody" or "tfoot" or "thead" } sectionEnd)
            {
                if (!InTableScope(sectionEnd.Name)) { Error("unexpected-table-section-end-tag"); return; }
                if (!InTableScope("tr")) { Error("unexpected-row-end-tag"); return; }
                ClearStackToTableRowContext();
                CloseAt(open.Count - 1);
                mode = Mode.InTableBody;
                Process(sectionEnd);
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "body" or "caption" or "col" or "colgroup" or "html" or "td" or "th" })
            {
                Error("unexpected-table-end-tag");
                return;
            }

            TableToken(token);
        }

        private void TableCellToken(HtmlToken token)
        {
            if (token is HtmlTag { IsEndTag: true, Name: "td" or "th" } cellEnd)
            {
                if (!InTableScope(cellEnd.Name)) { Error("unexpected-cell-end-tag"); return; }
                CloseTableCell(cellEnd.Name);
                return;
            }

            if (token is HtmlTag start && !start.IsEndTag
                && CellClosingTableTags.Contains(start.Name))
            {
                CloseTableCell();
                Process(token);
                return;
            }

            if (token is HtmlTag { IsEndTag: true, Name: "table" or "tbody" or "tfoot" or "thead" or "tr" } tableEnd)
            {
                if (!InTableScope(tableEnd.Name)) { Error("unexpected-table-end-tag"); return; }
                CloseTableCell();
                Process(tableEnd);
                return;
            }

            BodyToken(token);
        }

        private void CloseTableCell(string? expectedName = null)
        {
            var cellIndex = open.FindLastIndex(element => element.LocalName is "td" or "th");
            if (cellIndex < 0 || expectedName is not null && open[cellIndex].LocalName != expectedName)
            {
                Error("unexpected-cell-end-tag");
                return;
            }

            GenerateImpliedEndTags();
            if (Current is not DomElement current || current.LocalName != open[cellIndex].LocalName)
            {
                Error("cell-closed-with-open-descendants");
            }

            CloseAt(cellIndex);
            ClearFormattingToMarker();
            mode = Mode.InRow;
        }

        private void GenerateImpliedEndTags(string? except = null)
        {
            while (open.Count > 0 && ImpliedEndTags.Contains(open[^1].LocalName) && open[^1].LocalName != except)
            {
                open.RemoveAt(open.Count - 1);
            }
        }

        private bool InTableScope(string name)
        {
            for (var index = open.Count - 1; index >= 0; index--)
            {
                var current = open[index].LocalName;
                if (current == name) { return true; }
                if (current is "table" or "template" or "html") { return false; }
            }

            return false;
        }

        private void ClearStackToTableContext() => ClearStackToContext(["table", "template", "html"]);
        private void ClearStackToTableBodyContext() => ClearStackToContext(["tbody", "tfoot", "thead", "template", "html"]);
        private void ClearStackToTableRowContext() => ClearStackToContext(["tr", "template", "html"]);

        private void ClearStackToContext(string[] context)
        {
            while (open.Count > 0 && !context.Contains(open[^1].LocalName, StringComparer.Ordinal))
            {
                open.RemoveAt(open.Count - 1);
            }
        }

        private bool TemplateToken(HtmlToken token)
        {
            if (token is HtmlCharacters or HtmlComment or HtmlProcessingInstruction or HtmlDoctype)
            {
                BodyToken(token);
                return false;
            }

            if (token is HtmlEndOfFile)
            {
                var templateIndex = open.FindLastIndex(element => element.LocalName == "template");
                if (templateIndex < 0) { return false; }

                Error("eof-in-template");
                open.RemoveRange(templateIndex, open.Count - templateIndex);
                ClearFormattingToMarker();
                templateModes.RemoveAt(templateModes.Count - 1);
                ResetInsertionModeAppropriately();
                return true;
            }

            if (token is not HtmlTag tag) { throw new InvalidOperationException("Unknown HTML token in template."); }
            if (tag.IsEndTag)
            {
                if (tag.Name == "template") { CloseTemplate(); }
                else { Error("unexpected-end-tag-in-template"); }

                return false;
            }

            switch (tag.Name)
            {
                case "base" or "basefont" or "bgsound" or "link" or "meta":
                    Insert(tag, push: false);
                    return false;
                case "noframes" or "style":
                    StartText(tag, HtmlTextMode.Rawtext);
                    return false;
                case "script":
                    StartText(tag, HtmlTextMode.ScriptData);
                    return false;
                case "title":
                    StartText(tag, HtmlTextMode.Rcdata);
                    return false;
                case "template":
                    StartTemplate(tag);
                    return false;
                case "caption" or "colgroup" or "tbody" or "tfoot" or "thead":
                    return ReprocessTemplateTokenIn(Mode.InTable);
                case "col":
                    return ReprocessTemplateTokenIn(Mode.InColumnGroup);
                case "tr":
                    return ReprocessTemplateTokenIn(Mode.InTableBody);
                case "td" or "th":
                    return ReprocessTemplateTokenIn(Mode.InRow);
                case "select":
                    throw Unsupported("Select insertion modes inside template content are not implemented.");
                default:
                    return ReprocessTemplateTokenIn(Mode.InBody);
            }
        }

        private bool ReprocessTemplateTokenIn(Mode insertionMode)
        {
            templateModes[^1] = insertionMode;
            mode = insertionMode;
            return true;
        }

        private void StartTemplate(HtmlTag tag)
        {
            Insert(tag);
            formatting.Add(null);
            templateModes.Add(Mode.InTemplate);
            mode = Mode.InTemplate;
        }

        private void CloseTemplate()
        {
            var templateIndex = open.FindLastIndex(element => element.LocalName == "template");
            if (templateIndex < 0 || !InScope("template"))
            {
                Error("unexpected-template-end-tag");
                return;
            }

            GenerateImpliedEndTags();
            if (Current is not DomElement { LocalName: "template" })
            {
                Error("template-end-tag-with-open-elements");
            }

            FlushText();
            open.RemoveRange(templateIndex, open.Count - templateIndex);
            ClearFormattingToMarker();
            templateModes.RemoveAt(templateModes.Count - 1);
            ResetInsertionModeAppropriately();
        }

        private void ResetInsertionModeAppropriately()
        {
            for (var index = open.Count - 1; index >= 0; index--)
            {
                switch (open[index].LocalName)
                {
                    case "td" or "th" when index > 0:
                        mode = Mode.InCell;
                        return;
                    case "tr":
                        mode = Mode.InRow;
                        return;
                    case "tbody" or "thead" or "tfoot":
                        mode = Mode.InTableBody;
                        return;
                    case "caption":
                        mode = Mode.InCaption;
                        return;
                    case "colgroup":
                        mode = Mode.InColumnGroup;
                        return;
                    case "table":
                        mode = Mode.InTable;
                        return;
                    case "template":
                        if (templateModes.Count == 0)
                        {
                            throw new InvalidOperationException("The template insertion-mode stack is empty.");
                        }

                        mode = templateModes[^1];
                        return;
                    case "head" when index > 0:
                        mode = Mode.InHead;
                        return;
                    case "html" when index == 0 && fragmentContext is not null:
                        mode = fragmentContext.LocalName == "template" && templateModes.Count > 0
                            ? templateModes[^1]
                            : FragmentInsertionMode(fragmentContext.LocalName);
                        return;
                    case "html" when index == 0:
                        mode = headElementPointer is null && document.Head is null ? Mode.BeforeHead : Mode.AfterHead;
                        return;
                    case "body":
                        mode = Mode.InBody;
                        return;
                }

                if (index == 0)
                {
                    mode = Mode.InBody;
                    return;
                }
            }

            mode = Mode.InBody;
        }

        private static bool IsTableMode(Mode value) =>
            value is Mode.InTable or Mode.InTableText or Mode.InCaption or Mode.InColumnGroup or Mode.InTableBody or Mode.InRow or Mode.InCell;

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
                if (selectIndex >= 0)
                {
                    Error("unclosed-select");
                    open.RemoveRange(selectIndex, open.Count - selectIndex);
                }

                return;
            }
            if (token is not HtmlTag tag) { throw new InvalidOperationException("Unknown HTML token in select."); }
            if (!tag.IsEndTag && tag.Name == "input")
            {
                Error("input-start-tag-in-select");
                if (selectIndex >= 0)
                {
                    CloseAt(selectIndex);
                    BodyToken(tag);
                }

                return;
            }
            if (!tag.IsEndTag && tag.Name == "select")
            {
                Error("nested-select-start-tag");
                if (selectIndex >= 0) { CloseAt(selectIndex); }
                return;
            }
            if (!tag.IsEndTag && tag.Name == "hr")
            {
                GenerateImpliedEndTags();
                if (open.Skip(selectIndex + 1).Any(element => element.LocalName is "option" or "optgroup"))
                {
                    Error("hr-start-tag-with-open-option-or-optgroup");
                }

                Insert(tag, push: false);
                return;
            }
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
                if (selectIndex >= 0) { CloseAt(selectIndex); }
                else { Error("unexpected-select-end-tag"); }
                return;
            }
            BodyToken(token, fromSelectContext: true);
        }

        private DomElement Insert(HtmlTag token, bool push = true)
        {
            FlushText();
            var depth = open.Count - (fragmentRoot is null ? 0 : 1);
            if (push && depth >= options.MaxDepth) { throw new HtmlLimitException("HTML element depth limit exceeded."); }
            var element = CreateElementFromToken(token);
            if (fosterParenting && Current is DomElement { LocalName: "table" or "tbody" or "tfoot" or "thead" or "tr" })
            {
                InsertAtFosterParent(element);
            }
            else
            {
                InsertionParent.AppendChild(element);
            }

            if (push)
            {
                open.Add(element);
                if (token.SelfClosing) { Error("non-void-html-element-start-tag-with-trailing-solidus"); }
            }

            return element;
        }

        private void InsertAtFosterParent(DomElement element)
        {
            var targetIndex = open.FindLastIndex(candidate => candidate.LocalName is "table" or "template");
            if (targetIndex < 0)
            {
                if (open.Count == 0) { throw new InvalidOperationException("The foster-parent insertion stack is empty."); }
                AdjustedInsertionParent(open[0]).AppendChild(element);
                return;
            }

            var target = open[targetIndex];
            if (target.LocalName == "template")
            {
                AdjustedInsertionParent(target).AppendChild(element);
                return;
            }

            if (target.ParentNode is { } parent && parent is not DomDocument)
            {
                parent.InsertBefore(element, target);
                return;
            }

            if (targetIndex == 0) { throw Unsupported("The foster-parent insertion location for this table is not implemented."); }
            AdjustedInsertionParent(open[targetIndex - 1]).AppendChild(element);
        }

        private DomElement CreateElementFromToken(HtmlTag token)
        {
            CountNode();
            var element = ownerDocument.CreateElement(token.Name);
            if (element is DomTemplateElement) { CountNode(); }
            foreach (var (name, value) in token.Attributes) { element.SetAttributeFromParser(name, value); }
            return element;
        }

        private void StartText(HtmlTag tag, HtmlTextMode textMode)
        {
            Insert(tag);
            originalMode = mode;
            mode = Mode.Text;
            tokenizer.SetTextMode(textMode, tag.Name);
        }

        private void StartTextarea(HtmlTag tag)
        {
            StartText(tag, HtmlTextMode.Rcdata);
            ignoreNextLineFeed = true;
        }

        private void Text(string data)
        {
            if (data.Length == 0) { return; }
            var parent = InsertionParent;
            var text = parent.LastChild as DomText;
            if (text is null)
            {
                FlushText();
                CountNode();
                text = ownerDocument.CreateTextNode("");
                parent.AppendChild(text);
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
                parent.AppendChild(ownerDocument.CreateComment(comment.Data));
                return true;
            }

            if (token is HtmlProcessingInstruction instruction)
            {
                FlushText();
                CountNode();
                parent.AppendChild(ownerDocument.CreateProcessingInstruction(instruction.Target, instruction.Data));
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
                if (current is "html" or "caption" or "table" or "td" or "th" or "marquee" or "object" or "applet" or "template"
                    || buttonScope && current == "button" || listScope && current is "ol" or "ul") { return false; }
            }

            return false;
        }

        private void Close(string name) => CloseAt(open.FindLastIndex(e => e.LocalName == name));
        private void CloseAt(int index)
        {
            FlushText();
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

        // HTML initial insertion mode: identifiers select a mode but never load a DTD.
        private static DomDocumentMode DoctypeMode(HtmlDoctype token)
        {
            if (token.ForceQuirks || token.Name != "html"
                || token.PublicId is { } publicId && (AsciiEqualsIgnoreCase(publicId, "-//w3o//dtd w3 html strict 3.0//en//")
                    || AsciiEqualsIgnoreCase(publicId, "-/w3c/dtd html 4.0 transitional/en")
                    || AsciiEqualsIgnoreCase(publicId, "html"))
                || token.SystemId is { } systemId
                    && AsciiEqualsIgnoreCase(systemId, "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd")
                || HasPrefix(token.PublicId, QuirksPublicPrefixes))
            {
                return DomDocumentMode.Quirks;
            }
            if (HasPrefix(token.PublicId, Html4LoosePrefixes))
            {
                return string.IsNullOrEmpty(token.SystemId) ? DomDocumentMode.Quirks : DomDocumentMode.LimitedQuirks;
            }
            return HasPrefix(token.PublicId, XhtmlLoosePrefixes) ? DomDocumentMode.LimitedQuirks : DomDocumentMode.NoQuirks;
        }

        private static bool HasPrefix(string? value, string[] prefixes) => value is not null
            && prefixes.Any(prefix => value.Length >= prefix.Length && AsciiEqualsIgnoreCase(value.AsSpan(0, prefix.Length), prefix));

        private static readonly string[] Html4LoosePrefixes =
        [
            "-//w3c//dtd html 4.01 frameset//",
            "-//w3c//dtd html 4.01 transitional//"
        ];
        private static readonly string[] XhtmlLoosePrefixes =
        [
            "-//w3c//dtd xhtml 1.0 frameset//",
            "-//w3c//dtd xhtml 1.0 transitional//"
        ];
        private static readonly string[] QuirksPublicPrefixes =
        [
            "+//silmaril//dtd html pro v0r11 19970101//",
            "-//as//dtd html 3.0 aswedit + extensions//",
            "-//advasoft ltd//dtd html 3.0 aswedit + extensions//",
            "-//ietf//dtd html 2.0 level 1//",
            "-//ietf//dtd html 2.0 level 2//",
            "-//ietf//dtd html 2.0 strict level 1//",
            "-//ietf//dtd html 2.0 strict level 2//",
            "-//ietf//dtd html 2.0 strict//",
            "-//ietf//dtd html 2.0//",
            "-//ietf//dtd html 2.1e//",
            "-//ietf//dtd html 3.0//",
            "-//ietf//dtd html 3.2 final//",
            "-//ietf//dtd html 3.2//",
            "-//ietf//dtd html 3//",
            "-//ietf//dtd html level 0//",
            "-//ietf//dtd html level 1//",
            "-//ietf//dtd html level 2//",
            "-//ietf//dtd html level 3//",
            "-//ietf//dtd html strict level 0//",
            "-//ietf//dtd html strict level 1//",
            "-//ietf//dtd html strict level 2//",
            "-//ietf//dtd html strict level 3//",
            "-//ietf//dtd html strict//",
            "-//ietf//dtd html//",
            "-//metrius//dtd metrius presentational//",
            "-//microsoft//dtd internet explorer 2.0 html strict//",
            "-//microsoft//dtd internet explorer 2.0 html//",
            "-//microsoft//dtd internet explorer 2.0 tables//",
            "-//microsoft//dtd internet explorer 3.0 html strict//",
            "-//microsoft//dtd internet explorer 3.0 html//",
            "-//microsoft//dtd internet explorer 3.0 tables//",
            "-//netscape comm. corp.//dtd html//",
            "-//netscape comm. corp.//dtd strict html//",
            "-//o'reilly and associates//dtd html 2.0//",
            "-//o'reilly and associates//dtd html extended 1.0//",
            "-//o'reilly and associates//dtd html extended relaxed 1.0//",
            "-//sq//dtd html 2.0 hotmetal + extensions//",
            "-//softquad software//dtd hotmetal pro 6.0::19990601::extensions to html 4.0//",
            "-//softquad//dtd hotmetal pro 4.0::19971010::extensions to html 4.0//",
            "-//spyglass//dtd html 2.0 extended//",
            "-//sun microsystems corp.//dtd hotjava html//",
            "-//sun microsystems corp.//dtd hotjava strict html//",
            "-//w3c//dtd html 3 1995-03-24//",
            "-//w3c//dtd html 3.2 draft//",
            "-//w3c//dtd html 3.2 final//",
            "-//w3c//dtd html 3.2//",
            "-//w3c//dtd html 3.2s draft//",
            "-//w3c//dtd html 4.0 frameset//",
            "-//w3c//dtd html 4.0 transitional//",
            "-//w3c//dtd html experimental 19960712//",
            "-//w3c//dtd html experimental 970421//",
            "-//w3c//dtd w3 html//",
            "-//w3o//dtd w3 html 3.0//",
            "-//webtechs//dtd mozilla html 2.0//",
            "-//webtechs//dtd mozilla html//"
        ];

        private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<char> value, string expected)
        {
            if (value.Length != expected.Length) { return false; }
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character is >= 'A' and <= 'Z') { character = (char)(character + ('a' - 'A')); }
                if (character != expected[index]) { return false; }
            }

            return true;
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
        "svg", "math", "frameset", "frame", "noscript",
        "applet", "marquee", "object"
    };
    private static readonly HashSet<string> UnsupportedFragmentContexts = new(StringComparer.Ordinal)
    {
        "svg", "math", "frameset", "frame", "noscript", "applet", "marquee", "object"
    };
    private static readonly HashSet<string> TableTags = new(StringComparer.Ordinal)
    {
        "table", "caption", "colgroup", "col", "tbody", "thead", "tfoot", "tr", "td", "th"
    };
    private static readonly HashSet<string> CellClosingTableTags = new(StringComparer.Ordinal)
    {
        "caption", "col", "colgroup", "tbody", "td", "tfoot", "th", "thead", "tr"
    };
    private static readonly HashSet<string> ImpliedEndTags = new(StringComparer.Ordinal)
    {
        "dd", "dt", "li", "optgroup", "option", "p", "rb", "rp", "rt", "rtc"
    };
    private static readonly HashSet<string> VoidTags = new(StringComparer.Ordinal)
    {
        "area", "base", "basefont", "bgsound", "br", "embed", "hr", "img", "input", "keygen", "link", "meta",
        "param", "source", "track", "wbr"
    };
    private static readonly HashSet<string> FosterParentedVoidTags = new(StringComparer.Ordinal)
    {
        "area", "base", "basefont", "bgsound", "embed", "keygen", "link", "meta", "param", "source", "track"
    };
    private static readonly HashSet<string> HeadTags = new(StringComparer.Ordinal)
    {
        "base", "basefont", "bgsound", "link", "meta", "title", "style", "script", "noframes", "template"
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
    private static readonly HashSet<string> SpecialTags = new(BlockTags.Where(name => name != "dialog").Concat(VoidTags).Concat(UnsupportedTags).Concat(TableTags)
        .Concat(["html", "head", "body", "p", "li", "dd", "dt", "button", "form", "textarea", "title", "script",
            "style", "xmp", "plaintext", "iframe", "noembed", "noframes", "select", "template"]), StringComparer.Ordinal);
    private static readonly HashSet<string> ImplicitlyClosable = new(StringComparer.Ordinal)
    {
        "html", "body", "p", "li", "dd", "dt"
    };
}

/// <summary>Completed static DOM plus recoverable parse diagnostics.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#parse-errors">parse errors</see>.
/// Unsupported algorithms and limits throw instead of returning a success-shaped partial tree.</remarks>
public sealed record HtmlParseResult(DomDocument Document, IReadOnlyList<HtmlParseError> Errors);

/// <summary>A bounded, context-sensitive HTML fragment and its recoverable parse diagnostics.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#html-fragment-parsing-algorithm">fragment parsing</see>.</remarks>
public sealed record HtmlFragmentParseResult(DomDocumentFragment Fragment, IReadOnlyList<HtmlParseError> Errors);
