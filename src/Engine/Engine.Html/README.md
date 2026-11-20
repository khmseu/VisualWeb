# Engine.Html

First-party whole-string `HtmlTokenizer` and bounded static `HtmlParser`.
Tokenization covers tags/attributes, comments, doctypes, current processing
instructions, references and contextual text/script/CDATA modes. Official named
references are embedded [pinned data](Data/README.md).

Tree construction supplies implicit html/head/body, head metadata/text elements,
ordinary body elements, lists, paragraphs, headings, void elements, bounded
select/optgroup parsing with ordinary in-body handling for the supported
content subset, bounded textarea RCDATA handling, and `hr`/`input`/nested
`select` recovery, bounded ruby annotation start-tag recovery, a simple table
caption/column-group/section/row/cell subset, inert in-table `script`/`style`
text, table `type=hidden` inputs, non-hidden input and `br`/`hr` starts in
table/table-body/row modes with bounded foster-parent insertion, text-only
table foster parenting, bounded div/p start/end foster-parent insertion and
nested-table recovery, the in-table form-pointer exception, and root-attribute
merging for in-table `html` starts.
Other tokens requiring table foster parenting remain unsupported. Legacy
doctypes preserve public/system identifiers and recover
with explicit diagnostics, using the complete HTML initial-mode tables for
no-quirks/limited-quirks/quirks classification. No DTD resources are loaded and
additional quirks-layout behavior is not implied.
The active-formatting list uses token attribute snapshots, the
order-independent three-entry Noah's Ark rule, reconstruction and
template/caption/cell markers. Formatting end tags support the no-furthest-block
branch and bounded furthest-block adoption-agency recovery, including inner
clone/removal, bookmark shifts, reparenting and stack/list replacement. Clones
count toward node limits; the outer loop is capped at eight iterations and
inner-loop list removal follows the after-three-iterations rule. Adjusted
parents cover fragments and template contents. Nested anchor starts report an
error, run adoption and remove the original anchor from the list and stack if
still present before reconstruction. Nested `nobr` starts reconstruct before
the scope check and again after reporting an error and running adoption.
Scope checks include the `caption` boundary; supported captions are created
through table insertion and add a formatting marker.
Reparenting that requires table foster parenting and table-text foster parenting
requiring reconstruction throw explicitly. This is not the full adoption-agency
algorithm or full anchor semantics.
`listing` parses normal markup and strips its first LF; `xmp` is RAWTEXT until
its closing tag, and `plaintext` consumes the rest as literal text. Apparent
links, scripts and styles in those literal modes are not DOM elements.
Document templates in head/body use the bounded template insertion-mode stack:
ordinary supported in-body content and nested templates build under each
`DomTemplateElement.Content` fragment, then insertion-mode reset resumes the
surrounding head/body mode. Template content is inert; its scripts do not invoke
the parser completion callback. Table/select template contexts, foreign content,
and broader template algorithms remain unsupported.

`HtmlParser.ParseFragment` returns a `DocumentFragment` owned by the context
element's document and parses with the supported in-body, table, select,
template, `html`-context before-head, RCDATA, RAWTEXT, ScriptData or PLAINTEXT
behavior. Fragment text contexts have no appropriate end tag, so their
tokenizer state consumes matching-looking markup as text through the end of
input. Fragment content uses
the configured input, node, depth, error and cancellation limits.
Unsupported contexts (including SVG, MathML, `noscript` and `frameset`) fail
explicitly; unsupported tree-construction algorithms remain unsupported.
Advanced features throw `UnsupportedHtmlException`; safety limits throw
`HtmlLimitException`. Results contain a renderer-local DOM and diagnostics.
Scripts are tokenized as text, never executed.

See the [HTML/DOM guide](../../../docs/html-dom.md) for exact supported/deferred
features, caller decoding policy, limits and conformance evidence.

Official source: <https://html.spec.whatwg.org/multipage/parsing.html> (ID `html`;
the cache holds the single-page standard).
See the [standards workflow](../../../docs/standards.md).
