# Phase-5 HTML parsing and DOM

The user chose a first-party tokenizer and an explicit static-document
tree-construction subset rather than wrapping an existing managed HTML parser.
[Engine.Html](../src/Engine/Engine.Html/) references
[Engine.Dom](../src/Engine/Engine.Dom/), with no networking, native package,
browser-shell or scripting dependency.

## Entry points

```csharp
var result = HtmlParser.Parse(
    "<!doctype html><title>Example</title><p id=welcome>Hello &amp; welcome",
    cancellationToken: cancellationToken);
var paragraph = result.Document.GetElementById("welcome");
```

Callers supply **already-decoded text**. The parser has no encoding confidence
or "change the encoding" reparse, and no automatic resource loading. For bytes,
use Core.Encoding's `HtmlEncodingSniffer` (BOM, transport charset, prescan,
explicit default) first, as the browser shell does. Engine.Content will
coordinate that pipeline later.

`HtmlParseResult` contains the mutable renderer-local document and read-only
recoverable diagnostics. `HtmlParseError` records a code and a normalized UTF-16
input offset (not a byte offset or exact upstream line/column). Tokenizer codes
follow the HTML Standard; some tree diagnostics are subset-specific. Errors do
not execute scripts, fetch URLs or expose native capabilities.

Cancellation propagates as `OperationCanceledException`. `HtmlLimitException`
and `UnsupportedHtmlException` stop parsing without returning a success-shaped
partial tree. This is deliberate: rendering arbitrary approximate recovery
would hide missing standards algorithms.

## Tokenizer

[HtmlTokenizer](../src/Engine/Engine.Html/HtmlTokenizer.cs) is a pull tokenizer
with CR/CRLF input normalization and diagnostics for nulls, controls,
noncharacters and lone surrogates. It emits characters, start/end tags,
comments, doctypes, processing instructions and EOF. Tag/attribute names fold
ASCII only; the first duplicate attribute wins.

Named references use the complete official entity table, longest-match rules
and attribute ambiguity handling. Numeric references implement replacement,
C1 remapping and errors for invalid scalars/noncharacters/control references.
The tokenizer supports Data, RCDATA, RAWTEXT, ScriptData, PLAINTEXT and CDATA
initial modes. ScriptData includes escaped/double-escaped handling; it never
executes code. Appropriate end tags return the tokenizer to Data.

The tree builder, not the tokenizer, selects title/textarea RCDATA,
style/xmp/iframe/noembed/noframes RAWTEXT and script ScriptData modes.
The public CDATA initial mode supports standalone token tests; the document
builder still rejects SVG/MathML rather than pretending to support foreign
content. CDATA-like declarations in ordinary HTML are bogus comments.

The cached current HTML Standard includes processing-instruction tokenization.
Valid instructions produce ProcessingInstruction nodes; XML/xml-stylesheet
targets and invalid targets recover as comments according to that standard.
This intentionally follows the cached standard, not assumptions from older
browser/parser versions.

## Static document tree subset

[HtmlParser](../src/Engine/Engine.Html/HtmlParser.cs) implements initial,
before-html, before-head, in-head, after-head, in-body, text, after-body and
after-after-body modes. Supported behavior includes:

- Explicit or implied html/head/body; first-value root/body attribute merging.
- HTML5/legacy-compat and legacy public/system doctypes, preserving identifiers
  and selecting no-quirks, limited-quirks or quirks mode using the complete
  initial-mode identifier tables. Comparisons are ASCII-case-insensitive;
  force-quirks takes precedence. Nonconforming doctypes report
  `invalid-doctype` instead of aborting. DTD identifiers never cause fetching.
- Comments/processing instructions at the appropriate document/tree locations.
- Head metadata and title/style/script text; misplaced head content restoration.
- Ordinary HTML body elements, headings, paragraphs, block starts and scoped
  end tags. The active-formatting list retains token attribute snapshots,
  applies the three-equivalent-entry Noah's Ark rule (attribute order
  independent), reconstructs entries and observes template/caption/cell
  markers. Formatting end tags implement the no-furthest-block branch and a
  bounded furthest-block adoption-agency recovery: inner-loop formatting
  clones/removals, bookmark/list-index adjustment, block reparenting, and
  open-stack/list replacement are supported. Generated elements count toward
  `MaxNodes`; the outer loop is capped at eight iterations and the inner-loop
  removal rule applies after its third iteration. Cancellation checks and the
  existing open-stack `MaxDepth` limit remain in force. Adjusted insertion
  parents route through template contents and fragment roots. Nested `a`
  starts report a parse error, run adoption and remove the original anchor
  from the active-formatting list and open stack if still present before
  reconstruction. Nested `nobr` starts reconstruct before the scope check,
  report a parse error and run adoption if in scope, then reconstruct again.
  Scope checks include the `caption` boundary; this parser currently reaches
  captions only through table insertion, where table scope and the caption
  formatting marker constrain outer formatting entries.
- Bounded document `<template>` construction in head and body. Template
  contents use the native `Content` fragment and inert template-contents owner
  document; nested templates stay separate from ordinary children and the
  template insertion-mode stack resets to the surrounding head/body mode.
  Supported content uses the ordinary in-body subset. Each implicit content
  fragment counts toward `MaxNodes`; content descendants count toward the
  existing node/depth/error/cancellation limits. Template-content scripts are
  parsed as inert text and never invoke `scriptCompleted`. Template starts and
  matching ends in table, table-body and row modes use the same in-head/template
  processing and restore the table insertion mode on close. Template content
  also selects the bounded table modes for `caption`/`colgroup`/table sections,
  `col`, `tr` and `td`/`th`; nested tables use the existing bounded table subset.
  EOF in these modes closes open templates and reports template EOF diagnostics.
  A `select` start in template content uses the bounded in-body/select parser;
  nested selects, foreign content and broader template algorithms remain
  explicitly unsupported.
- `HtmlParser.ParseFragment` returns a context-document-owned `DocumentFragment`.
  It selects the bounded insertion mode for supported in-body, table, select and
  template contexts plus the `html` context's before-head mode, and starts
  RCDATA, RAWTEXT, ScriptData or PLAINTEXT tokenization where applicable.
  Text-mode fragments have no appropriate end tag, so matching-looking markup
  remains text through EOF. Synthetic parser-root
  nodes are not returned; the bounded form pointer comes from the context's
  ancestor chain, and fragment nodes are covered by parser limits and
  cancellation. SVG, MathML,
  frameset/frame, `noscript` and other unsupported contexts fail explicitly.
- List-item and definition-list implied closures, pre/listing/textarea first-LF
  handling, void elements (including `<wbr>`) and ignored self-closing flags on
  non-void HTML. Layout interprets `<wbr>` as an optional line-break opportunity
  within its bounded same-style text subset; it does not force a break.
- `listing` parses ordinary child markup; `xmp` uses RAWTEXT until its end tag,
  while `plaintext` consumes the rest of the input as literal text. Neither
  literal mode interprets apparent links, styles or scripts as elements.
- Merged text nodes, null removal in body and recovery diagnostics.
- Raw script/style content without executing it or interpreting its markup.
- Bounded ruby annotation tree construction for `rb`, `rtc`, `rp` and `rt`,
  including the in-body implied-end-tag rules. Ruby-specific styling and
  annotation layout are not implemented.
- A bounded `select` subset with direct `option`/`optgroup` handling and
  ordinary in-body processing for other supported tokens, including block
  children in options and optgroups. `textarea` uses the in-body RCDATA rules
  in the current select context; its initial LF is ignored. The in-body
  recovery rules also insert `hr` inside the select after implied option/group
  closure, close the select and reprocess an `input` start tag, and
  close-and-ignore a nested `select` start tag. Outside selects, `option` and
  `optgroup` use ordinary in-body insertion; starting either while an `option`
  is current closes that option first.
- Basic static table construction for `table`, `caption`, `colgroup`/`col`,
  `tbody`/`thead`/`tfoot`, `tr`, `td` and `th`, including implied
  `colgroup`/`tbody`/`tr`, scoped closure, whitespace and comments. Cell
  and caption contents use the existing in-body subset. Captions close and
  reprocess table tokens; a nested table start in table mode closes the current
  in-scope table and reprocesses the token, while nested tables in cells and
  captions remain descendants and restore the enclosing insertion mode when
  closed. This includes supported template and caption-fragment contexts.
  In-table form starts follow the form-pointer
  exception: the form is inserted at the current table-mode location, then
  popped from the open-element stack; repeated starts are ignored while the
  pointer is set. In-table form end tags use in-body handling and cannot pop
  the already-popped form from the stack. An `html` start token in table,
  section or row mode uses the in-body root-attribute merge without inserting
  another element.
  In-table `script`/`style` start tags use the existing script-data/raw-text
  modes and return to the originating table insertion mode; script content
  remains inert. A `type=hidden` input is inserted as a void table child with
  a parse-error diagnostic. Non-hidden input start tags in table, table-body and
  row modes follow in-body handling with bounded foster-parent insertion at the
  table's adjusted insertion location and report a parse error. Inputs inside
  cells use ordinary in-body insertion. `br` and `hr` start tags in table,
  table-body and row modes also use in-body handling with bounded foster-parent
  insertion and a parse-error diagnostic. `img` and `wbr` starts and end tags
  in those modes also use in-body handling with bounded foster-parent insertion.
  `image` start and end tokens use the same path; the start-tag alias is
  recovered as `img`.
  Template starts and matching ends in table, table-body and row modes use
  in-head/template processing and restore the originating table mode on close.
  Template content selects in-table, in-column-group, in-table-body and in-row
  modes for supported start tags and uses the existing bounded table
  construction. A `select` start uses bounded in-body/select parsing; nested
  selects and other unsupported table/template algorithms remain explicit
  failures.
  `textarea` starts and unmatched end tags use the same fostered in-body path;
  starts enter RCDATA with initial-line-feed suppression, and the matching end
  tag restores the originating table insertion mode.
  `title` starts and unmatched end tags follow the same path; starts enter
  RCDATA without `textarea`'s initial-line-feed suppression.
  `iframe`, `noembed` and `noframes` starts and unmatched end tags also follow
  the same path, with RAWTEXT tokenization; their content is text, not an
  embedded browsing context or resource load.
  `xmp` starts and unmatched end tags use the same path and preserve the
  existing in-body paragraph/formatting handling before entering RAWTEXT.
  `area`, `base`, `basefont`, `bgsound`, `embed`, `keygen`, `link`, `meta`,
  `param`, `source` and `track` starts and end tags use the same bounded path;
  this does not add resource loading.
  `div`, `p`, `pre`, `listing`,
  `h1`-`h6`, `li`, `dd`, `dt`, `button` and the other supported in-body block
  elements (`address`, `article`, `aside`, `blockquote`, `center`, `details`,
  `dialog`, `dir`, `dl`, `fieldset`, `figcaption`, `figure`, `footer`, `header`,
  `hgroup`, `main`, `menu`, `nav`, `ol`, `search`, `section`, `summary` and
  `ul`) in those modes use in-body handling with bounded foster-parent
  insertion. Formatting-element start/end tags, including `a`, and `span`
  start/end tags
  in table, column-group, table-body and row modes use the same bounded in-body
  foster-parent path. This includes the existing bounded nested-anchor and
  nested-`nobr` recovery. `option`, `optgroup` and `label` starts and ends use
  that path in the same modes. Column-group fallback closes the open `colgroup`
  before reprocessing `label` in table mode; an unmatched `label` end tag
  reports the fostered-table and in-body unexpected-end diagnostics. Adoption
  reparenting that itself requires table foster parenting remains unsupported.
  `abbr`, `cite`, `data`, `dfn`, `kbd`, `mark`, `q`, `samp`, `sub`, `sup`,
  `time` and `var` starts and ends also use the bounded fostered in-body path;
  unmatched ends retain the in-body unexpected-end diagnostic.
  Other non-special element names, including unknown and custom element names,
  use the same path without adding custom-element reactions or scripting.
  `select` starts and unmatched end tags in
  those modes also use bounded fostered in-body handling; an open select uses
  the existing bounded select-token rules. `ruby`, `rb`, `rtc`, `rp` and `rt`
  start/end tags also use this path; ruby annotation recovery remains bounded.
  `plaintext` starts and
  unmatched end tags in those modes use the fostered in-body path; a start
  switches to PLAINTEXT so the rest of the input is literal text. Character
  tokens are buffered as table text only when the current
  node is `table`, `tbody`, `template`, `tfoot`, `thead` or `tr`; otherwise
  they use in-body handling, preserving text inside a fostered element.
  `pre` and `listing` retain the in-body initial-line-feed suppression. This
  does not implement foster parenting for other table tokens.
  Contiguous non-whitespace table-character runs (including their adjacent
  whitespace) are foster-parented before the nearest table when the current
  node is table-related. When active formatting needs reconstruction, its
  bounded clones are inserted at the foster location and the text is inserted
  into the reconstructed current node. Whitespace-only runs stay in the table
  without reconstruction. Rendering is separate:
  `Engine.Layout` supports a [bounded automatic table subset](text-layout.md#basic-automatic-tables);
  parser support for captions/columns does not imply layout support.

The existing block-start parsing of `<search>` closes an open `p` and retains
ordinary nested content; its block rendering is a separate UA CSS default.

Simple `form` elements use the form element pointer: `<form>` closes an open
`p`, and outside template contents a `<form>` reports `nested-form` and is
ignored while the pointer is set. Outside template contents, `</form>` clears
the pointer before the scope check, reporting `unexpected-form-end-tag` if the
pointed-to form is absent or out of scope. An out-of-scope form is not popped,
so a later form start can produce nested forms. A matching
`</form>` closes implied `dd`/`dt`/`li`/`p` ends; any other misnesting, or an
implied closure (for example `</div>` or `</dl>`) that would pop an open form,
throws instead of applying that recovery. `input` stays a
void element and `button` an ordinary element, so control text never renders
as raw markup. The parser does not expose a form-owner API; the browser's
bounded form ownership is described in the [shell guide](browser-shell.md).
Inside template contents, including template-context
fragment parsing, form starts ignore and do not change the outer form pointer;
in-body starts still close a `p`, and in-scope form ends close the innermost
form after implied end tags. In-table starts are inserted and popped without
changing the pointer. This adds no form-control ownership or script APIs.

Other tokens that require table foster parenting throw explicitly, as do
unsupported table tags outside the contexts covered above. Template content
supports the bounded table and in-body/select paths described above; additional
table/select algorithms remain explicitly unsupported. The
following additionally throw explicitly: SVG/MathML,
unsupported select contents whose ordinary in-body processing requires
unimplemented algorithms; textarea-specific form semantics beyond first-LF
handling; foreign content; framesets/frames; noscript and applet/marquee/object
specific algorithms. Fragment contexts outside the supported parser subset are
rejected. Adoption-agency reparenting whose common ancestor requires table
foster parenting throws instead of returning an incorrect tree.
The adoption agency remains a bounded subset, not full HTML tree-construction
conformance; this recovery does not implement full anchor semantics.

Doctype classification follows the cached
[initial insertion mode](https://html.spec.whatwg.org/multipage/parsing.html#the-initial-insertion-mode)
(local ID `html`), including HTML 4.01 loose doctypes with missing/empty versus
nonempty system identifiers and XHTML 1.0 loose doctypes. Later doctypes cannot
replace the original doctype or mode. This document-only parser does not expose
srcdoc/parser-mode-lock flags. Mode selection does not add legacy CSS sizing,
table/image-baseline quirks or other unsupported rendering algorithms.

`DomFormControls` exposes only `input type` and `button type` keyword
normalization. Apart from the bounded native template element described below,
there are no specialized HTML element classes, custom-element reactions,
DOM form state, form-associated custom elements, constraint-validation APIs, stylesheet application, navigation or V8 integration.
Well-formed unsupported tags are still unsupported; this is a finite static
subset, not general browser HTML tree-construction conformance.
Native DOM creation of a template element is supported independently: it has a
stable, fragment-backed `Content` property whose children are separate from
the element's ordinary child list. Template contents use the document's inert
template-contents owner document, including nested templates, and adoption and
deep cloning preserve that separation. The parser constructs ordinary document
templates within the bounded subset above; parser construction does not add
script-facing template bindings or an `innerHTML` API.

## DOM boundary and invariants

Nodes include Document, DocumentFragment, HTML Element, Text, Comment,
ProcessingInstruction and DocumentType. Public operations include append,
insert-before, replace-child, remove-child, explicit adoption, attributes,
textContent, sibling/parent links, connection status and first matching ID.

Insertion/replacement validates the final shape **before** detaching nodes:
no cycles, no children on leaves, one document element, at most one doctype
before the element, no direct document text, and no doctype outside Document.
Moving a node adopts every descendant into the destination document.
Fragments transfer their children and become empty. Removal preserves ownership
but clears parent/sibling connectivity.

`DomDocument.CreateElement("template")` returns a native
`DomTemplateElement`. Its stable `Content` fragment is not a child or descendant
of the template; its owner document is the document's inert template-contents
owner document. Adopting a template also rehomes its contents and any nested
template contents. Shallow clone creates a fresh empty content fragment; deep
clone copies that separate subtree, and bounded clone preflight includes each
template fragment and its contents.

HTML factories validate element/attribute local names with their distinct DOM
rules. Processing-instruction targets use XML Name validation. HTML parser
recovery can emit malformed attribute names that a public DOM setter rejects;
an internal friend-assembly path preserves those recovered names without
weakening the public API. Node/attribute case folding is ASCII-only.

Child and attribute collections have read-only public views; node/data/attribute
mutations remain explicit. These objects are **not thread-safe** and must not be
shared between tabs, passed over IPC or treated as security principals.
Mutation observers, native event machinery, ranges, shadow DOM, namespace-aware APIs,
most special element behavior and full script bindings are deferred. Additional
table/select template algorithms, script-facing template bindings and
`innerHTML` remain unsupported. The static fragment parser is not exposed as a
script-facing `innerHTML` API.
The native document title property follows first-title tree order and ASCII
whitespace normalization; its setter creates a title only with an existing head.
The optional [V8 binding foundation](scripting.md) exposes live title, ID lookup
and element textContent through private primitive-only callbacks, not CLR nodes.
With explicit page-script opt-in, PageRendering invokes a bounded callback
when the parser completes each eligible classic script element. Inline and
browser-brokered external classics execute in parser order before parsing
continues, so later input is not yet in the DOM. This is a finite subset, not
the complete HTML script preparation, render-blocking stylesheet, scheduling
or error-reporting algorithms; without opt-in, scripts remain inert.
Loaded-page script hosts also expose readonly `document.URL` and
`document.documentURI`, both serialized from the final page URL. This does not
implement the complete document-base URL model. In these hosts, `Node.baseURI`
is recomputed from the current first HTML `<base href>` in tree order, resolved
against the fallback base URL (the final page URL ordinarily, or the inherited
snapshot for a page-created `about:blank`), with the browser-approved base URL
policy applied.
The current value is recomputed on each access; a changed `href` that does not
match a browser-approved base falls back to the document fallback base URL.
Invalid base URLs and `data:`/`javascript:` bases also fall back to that URL,
consistently with stylesheet discovery.
The opted-in loaded-page facade also exposes `HTMLBaseElement.href`: it
resolves the content attribute against the document fallback base URL, returns the raw
attribute when parsing fails, and reflects USVString writes. Its URL parser uses
the document's fallback base URL, so an `about:blank` base element uses the
initiating document's inherited snapshot. This URL reflection does not implement
the frozen-base lifecycle.
See the [HTMLBaseElement href algorithm](https://html.spec.whatwg.org/multipage/semantics.html#dom-base-href).
The script facade also reflects `HTMLBaseElement.target` as a `DOMString`,
including when no page URL is associated with the host. This property binding
does not update the shell's already-collected hyperlink targeting metadata.
Page-created `about:blank` has a bounded inherited fallback-base snapshot;
browser-UI-created blank documents do not. `srcdoc` and the full frozen-base
lifecycle remain unsupported. In opted-in loaded-page script hosts, `<a>` and `<area>`
facades expose the `href` stringifier and the bounded `HyperlinkElementUtils`
URL components (`origin`, `protocol`, `username`, `password`, `host`,
`hostname`, `port`, `pathname`, `search`, and `hash`). URL getters reparse the
current content attribute against the bounded document base URL; a failed
`href` parse returns the raw attribute, while component getters return the
specified empty/default values. Document-bound hosts also reflect the
`target` content attribute as a `DOMString` on both elements, independently of
page URL binding; script mutations after shell link metadata collection do not
update that snapshot. Setters use Core URL component mutation and
write the serialized URL back to `href`; `origin` is readonly. `toString()`
returns the same value as `href`. Other link properties and complete Web IDL
behavior remain unsupported.
Phase 11w queues inline `type="module"` sources at the same parser boundary,
then executes them in source order after parsing and after the `interactive`
readiness transition, before `DOMContentLoaded`. Top-level `await` must settle
within the shared page-script deadline; unresolved or rejected module
evaluation fails the candidate. Static/dynamic imports, external modules and a
module loader remain unsupported. This scheduling remains in PageRendering;
Engine.Html still constructs a static tree without executing script.
Phase 11e also exposes bounded native attribute operations, element/text/fragment
factories and Node mutation/navigation through branded wrappers. It reuses these
native checked algorithms without adding events or dynamic script execution.
Phase 11g adds optional synchronous synthetic Node/document event dispatch
in native JavaScript facades, not in these C# nodes. Listener identities and
snapshotted ancestor paths stay in the owning V8 isolate; no automatic browser
events, persistent event loop or dynamic script execution is added.
Phase 11h opts page execution into finite readiness/DOMContentLoaded on the
script facade only; native C# nodes remain free of script lifecycle state.
Phase 11i exposes bounded querySelector/querySelectorAll, matches and closest
through the same hidden primitive bridge and existing CSS selector matcher.
Static NodeList facades retain live node identities without exposing CLR lists.
Phase 11j exposes className and a bounded live classList facade in native
JavaScript. Ordered token mutations reuse attribute preflight; the C# DOM
continues storing ordinary raw class attributes.
Phase 11k exposes bounded contains/isSameNode/getRootNode, parentElement,
hasChildNodes and element-only child/sibling navigation over these live trees.
No shadow DOM or live child collection is implied.
Phase 11l adds native checked ToggleAttribute and script id/toggleAttribute
bindings. Ordered attributes retain replacement position and append on
removal/readdition; the bridge preserves existing storage preflight.
Phase 11m exposes CharacterData/nodeValue bindings using native UTF-16
substring/replacement primitives. No observer, live-range or processing-
instruction pseudoattribute reactions are implied.
Phase 11n adds native/script Text splitting and contiguous wholeText reads.
The script bridge preflights result identities and subtree limits before
inserting a suffix; no live-range repair is implied.
Phase 11o adds native/script Node normalization: remove empty descendant Text
nodes and merge adjacent runs into the first nonempty identity, preserving
detached node data/ownership. The iterative native plan preflights the entire
subtree before mutation; the script bridge supplies finite traversal/storage
and shared text-work limits. Observers/live-range reactions remain deferred.
Phase 11p adds native/script structural equality: exact interface payloads,
unordered attributes and ordered child trees. Native comparisons can span
documents; script comparisons preserve single-document ownership and preflight
both complete trees/payloads. Parent, identity and document mode are not equality
criteria; no descendant wrappers are allocated.
Phase 11q exposes native NodeName/OwnerDocument and HTML Element names/namespace
through readonly script metadata getters. Names fold ASCII only; prefix is
null even when the local name contains a colon. Detached nodes retain their
owner; adopted wrappers reject rather than exposing another document.
Phase 11r exposes ordered native attribute names as bounded, fresh mutable
script arrays and constant-time presence. Recovered parser names are preserved;
values, Attr objects and live NamedNodeMap remain outside this API.
Phase 11s exposes native comment/instruction factories and readonly target to
scripts. XML Name/initial-data validation precedes identity registration;
empty targets explicitly use DomError.InvalidCharacter. Raw UTF-16 comments
and instruction data reuse CharacterData, with pseudoattributes/reactions deferred.
Phase 11t exposes checked ChildNode.remove for Element, CharacterData and
DocumentType with bounded parent-subtree preflight. Detached nodes keep
ownership, children, data and listeners; Document/Fragment receivers reject.
No observer, range or custom-element reactions are introduced.
Phase 11u exposes live document.doctype and native name/publicId/systemId as
readonly privately branded script getters. The lookup scans only document
children; detached metadata retains its values and adopted wrappers reject.
Parser mode selection is unchanged: public and non-legacy-compatible system
identifiers still fail explicitly in the static tree builder, while native
nodes can retain arbitrary raw identifier strings. Identifiers are not fetched.
Phase 11v adds checked native `cloneNode(deep)` for supported node kinds.
Clones preserve element attributes/order, CharacterData, PI and doctype
payloads, fragment children and document mode for native Document copies.
Parent links and synthetic listeners are never copied. Script-facing clone
preflights complete deep subtrees, per-element attributes and text/callback
budgets before allocation; Document wrappers are explicitly unsupported in
the one-bound-document bridge.

## Safety limits

`HtmlParserOptions` defaults:

| Limit                                                  | Default   |
| ------------------------------------------------------ | --------- |
| Input UTF-16 characters before newline normalization   | 16 Mi     |
| Created document/element/text/comment/PI/doctype nodes | 1,000,000 |
| Open element stack depth, including html/head/body     | 512       |
| Combined input/tokenizer/tree diagnostics              | 10,000    |

Limits are validated and configurable. The four-node implicit skeleton and
two-open-element depth are tested at exact boundaries. Whole-string parsing
buffers input/tokens/tree data; these limits are not a total memory budget.
Cancellation is checked in input/token scanning and tree processing.
Text coalescing uses a builder rather than repeated quadratic string appends.
Direct DOM construction has no parser limits; its owner controls resource use.

## Pinned data and conformance evidence

- [Official entities](../src/Engine/Engine.Html/Data/README.md): unmodified
  WHATWG `entities.json`, pinned by SHA-256, with WHATWG CC BY/BSD license.
- [html5lib fixtures](../tests/Engine.Html.Tests/Data/README.md): MIT-licensed
  normal HTML tokenizer files at
  `c777c408b61078ea2eb4acefc2535f54dbc8b28a`, and four complete static tree
  families at `9329e64694e7835d0dcff9811e22856ef6ad16f9`.

All **7,048** tokenizer vector/state combinations across 12 files run with appropriate
last-start-tag/double-escape handling. Token output and error-code multisets
are compared; exact error coordinates are not claimed. Three older domjs
error names are translated to the current processing-instruction error name,
as documented in the fixture guide. No vectors/token outputs are dropped.
XML infoset-coercion tests have a different output contract and are excluded;
the pendingSpecChanges file is empty.

Tree tests compare the **121** complete cases in comments01, entities01,
entities02 and inbody01 without filtering. Focused tests cover the implemented
simple table subset; broader table/template/foreign/fragment/adoption corpora
are outside it. Additional tests verify mutations, unsupported failures,
cancellation and exact limits. This is finite evidence, not a claim to
implement all of HTML or DOM.

```sh
dotnet test tests/Engine.Dom.Tests/Engine.Dom.Tests.csproj
dotnet test tests/Engine.Html.Tests/Engine.Html.Tests.csproj
dotnet build VisualWeb.slnx
dotnet test VisualWeb.slnx --no-build
dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party
```

Tests do not refresh/download standards or datasets. Continue using the
[independent cache workflow](standards.md) before standards-dependent changes.

### Phase-5 validation outcome

Validated on Linux x64: all **30 projects** build and all **11,286 tests** pass,
with no failed or skipped tests:

| Test project | Passed |
| ------------ | -----: |
| HTML         |  7,202 |
| DOM          |     17 |
| Core         |  3,815 |
| Networking   |    210 |
| Platform     |     19 |
| SpecCache    |     23 |

First-party formatting verification and editor diagnostics are clean. A
separate standards-cache check reports all **34 documents** fresh; it is not
part of the test run. These results do not certify Windows/arm64 execution or
full HTML/DOM conformance.
