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
text, table `type=hidden` inputs, non-hidden input and `br`/`hr` starts and
ends in table/table-body/row modes with bounded foster-parent insertion,
`img` and `wbr` start/end tags, the `image` start-tag alias, and other
supported void-element tokens in those modes, `textarea`/`title` RCDATA and
`iframe`/`noembed`/`noframes`/`xmp` RAWTEXT starts with unmatched end-tag
recovery;
only `textarea` suppresses an initial LF. `noscript` starts in table,
column-group, table-body and row modes use fostered in-body insertion.
With scripting disabled, end tags use the same path; with scripting enabled,
the start enters RAWTEXT and its matching end restores the originating mode.
`frame` and `frameset` starts use the generic fostered in-body fallback in
table, table-body and row modes; this does not create browsing contexts or load
or render frames. Column-group mode closes `colgroup` and reprocesses them in
table mode.

The bounded `HtmlFragmentSerializer.SerializeChildren` API implements native
HTML fragment serialization for supported HTML Element/Text/Comment/
ProcessingInstruction trees, including ordered attributes, escaping, raw-text
parents, void elements and template contents. It is used by script-facing
`Element.innerHTML`; the setter parses with the receiver context and replaces
children only after parsing and limit preflight. This output is **not
sanitized**. Frame, frameset, SVG, MathML and other unsupported parser contexts
or algorithms fail explicitly; fragment insertion does not execute scripts or
load resources.
Text-only table
foster parenting, bounded foster-parent insertion for block, list-item
and button start/end tags, nested-table recovery, the in-table form-pointer
exception, formatting-element and `span` foster parenting, `option`/`optgroup`
and `label` start/end foster parenting in table/column-group/table-body/row
modes, `plaintext` starts and unmatched end tags in those modes, root-attribute merging for in-table
`html` starts, body/head starts and unmatched head ends in table, table-body
and row modes, and template start/end processing in those modes.
Document `noscript` uses the parser scripting state: disabled scripting selects
the in-head-noscript insertion mode in head and ordinary in-body parsing in
body, while enabled scripting selects RAWTEXT in head and body. This does not
add full script preparation.
`applet`/`marquee`/`object` start/end tags use bounded in-body handling with
active-formatting markers, including fostered processing in table,
table-body and row modes. Plugin behavior and external resources remain
unsupported. Fragment parsing with these contexts uses the same bounded
in-body handling.
Static document-mode framesets build nested `<frameset>` and `<frame>` nodes
with frameset-ok gating and after-frameset processing. Frames do not create
browsing contexts, load resources or render; frameset/frame fragment contexts
remain unsupported.
Other end tags not handled by table-specific rules use the generic fostered
in-body fallback in table, table-body and row modes; unsupported table starts
remain explicit failures.
In ordinary in-body parsing, `caption`, `col`, `colgroup`, `tbody`, `thead`,
`tfoot`, `tr`, `td` and `th` starts report a parse error and are ignored.
Formatting-element start/end tags use the bounded in-body path, including
existing nested-anchor and nested-`nobr` recovery and fostered adoption
reparenting. `select` starts and
unmatched end tags use bounded fostered in-body handling; an open select uses
the existing bounded select-token rules. `ruby`, `rb`, `rtc`, `rp` and `rt`
start/end tags use the same path with existing bounded ruby recovery.
Column-group fallback closes the open `colgroup` before reprocessing a `label`
token in table mode; unmatched `label` end tags receive in-body recovery.
`abbr`, `cite`, `data`, `dfn`, `kbd`, `mark`, `q`, `samp`, `sub`, `sup`,
`time` and `var` start/end tags also use bounded fostered in-body handling in
these modes; other non-special names, including unknown and custom element
names, use the same path. Unmatched ends receive in-body recovery. This adds
no custom-element reactions or scripting.
Table starts inside captions use the bounded in-body table rules; closing the
nested table restores caption mode in documents and caption fragments.
`plaintext` starts switch to the literal-text tokenizer state for the remainder
of the input. Other unimplemented table start tags requiring foster parenting
remain unsupported.
Legacy doctypes preserve public/system identifiers and recover
with explicit diagnostics, using the complete HTML initial-mode tables for
no-quirks/limited-quirks/quirks classification. No DTD resources are loaded and
additional quirks-layout behavior is not implied. In-body table starts close
an in-scope paragraph outside quirks mode, but leave it open in quirks mode.
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
Non-whitespace table text reconstructs active formatting at the foster insertion
location when needed; whitespace-only table text does not reconstruct it. If
the last open template is later than any table (or no table is open), fostered
table text goes into the template content, merging adjacent text.
Adoption selects the first special element below the formatting element and uses
the foster insertion location for reparenting when required. This is not the
full adoption-agency algorithm or full anchor semantics.
`listing` parses normal markup and strips its first LF; `xmp` is RAWTEXT until
its closing tag, and `plaintext` consumes the rest as literal text. Apparent
links, scripts and styles in those literal modes are not DOM elements.
Document templates in head/body use the bounded template insertion-mode stack:
ordinary supported in-body content and nested templates build under each
`DomTemplateElement.Content` fragment, then insertion-mode reset resumes the
surrounding head/body mode. Template content is inert; its scripts do not invoke
the parser completion callback. Template starts and matching ends in table,
table-body and row modes use in-head/template processing and restore the table
insertion mode. Within template content, supported table starts select the
bounded in-table, in-column-group, in-table-body and in-row algorithms, and
nested tables use the existing table subset. EOF in these modes closes open
templates with diagnostics. A `select` start uses the bounded in-body/select
parser; templates started inside select content use in-body/template
processing, keep their contents out of the enclosing select's token handling,
and resume the enclosing select's token handling on close, including in
select-context fragments.
Nested selects, foreign content and other unsupported template algorithms
remain explicit failures.
Inside template contents, including template-context fragments, form starts
ignore and preserve the outer form pointer; in-body form ends close the
in-scope form, and in-table form starts are inserted and popped without
changing the pointer. DOM form-owner APIs and scripting remain deferred.
Outside template contents, a form end clears the form pointer even when the
form is absent or out of scope; an out-of-scope form remains on the stack.
In-scope form ends generate implied end tags and remove only the pointed-to
form, reporting misnesting while leaving remaining descendants open. Other
in-body end-tag closures may pop a pointed-to form from the open stack while
leaving the form pointer set, so additional form starts remain ignored until a
later form end clears it. Adoption-agency removal guards remain, but are
unreachable with the current HTML-only stack: `form` is a special element and
therefore becomes the furthest block instead of a removed intermediate node.

`HtmlParser.ParseFragment` returns a `DocumentFragment` owned by the context
element's document and parses with the supported in-body, table, select,
template, `html`-context before-head, RCDATA, RAWTEXT, ScriptData or PLAINTEXT
behavior. `noscript` contexts use the fragment algorithm's scripting-disabled
Data state; this API does not expose a scripting-enabled fragment mode.
Fragment text contexts have no appropriate end tag, so their
tokenizer state consumes matching-looking markup as text through the end of
input. Fragment content uses
the configured input, node, depth, error and cancellation limits.
Unsupported fragment contexts (including SVG, MathML and `frameset`) fail
explicitly; unsupported tree-construction algorithms remain
unsupported.
Advanced features throw `UnsupportedHtmlException`; safety limits throw
`HtmlLimitException`. Results contain a renderer-local DOM and diagnostics.
Scripts are tokenized as text, never executed.

See the [HTML/DOM guide](../../../docs/html-dom.md) for exact supported/deferred
features, caller decoding policy, limits and conformance evidence.

Official source: <https://html.spec.whatwg.org/multipage/parsing.html> (ID `html`;
the cache holds the single-page standard).
See the [standards workflow](../../../docs/standards.md).
