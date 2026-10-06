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

Callers supply **already-decoded text**. There is no HTML encoding prescan,
transport charset policy or automatic resource loading yet. For bytes, use
Core.Encoding with a deliberately chosen fallback first. Engine.Content will
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
- HTML5/legacy-compat doctype and missing/invalid doctype quirks mode.
- Comments/processing instructions at the appropriate document/tree locations.
- Head metadata and title/style/script text; misplaced head content restoration.
- Ordinary HTML body elements, correctly nested inline formatting, headings,
  paragraphs, block starts and scoped end tags.
- List-item and definition-list implied closures, pre/listing/textarea first-LF
  handling, void elements and ignored self-closing flags on non-void HTML.
- Merged text nodes, null removal in body and recovery diagnostics.
- Raw script/style content without executing it or interpreting its markup.

The following throw explicitly: tables and table parts, templates, SVG/MathML,
select/options, forms, framesets/frames, noscript, ruby and
applet/marquee/object-specific algorithms. Legacy public/system doctype mode
selection (except `about:legacy-compat`) also throws. Fragment parsing is not
offered. Nested anchors/nobr, misnested formatting and closures requiring
active-formatting reconstruction/adoption agency throw instead of returning
an incorrect tree.

There are no specialized HTML element classes, custom-element reactions,
form state, stylesheet application, navigation or V8 integration.
Well-formed unsupported tags are still unsupported; this is a finite static
subset, not general browser HTML tree-construction conformance.

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

HTML factories validate element/attribute local names with their distinct DOM
rules. Processing-instruction targets use XML Name validation. HTML parser
recovery can emit malformed attribute names that a public DOM setter rejects;
an internal friend-assembly path preserves those recovered names without
weakening the public API. Node/attribute case folding is ASCII-only.

Child and attribute collections have read-only public views; node/data/attribute
mutations remain explicit. These objects are **not thread-safe** and must not be
shared between tabs, passed over IPC or treated as security principals.
Mutation observers, native event machinery, ranges, shadow DOM, namespace-aware APIs, cloning,
most special element behavior and full script bindings are deferred.
The native document title property follows first-title tree order and ASCII
whitespace normalization; its setter creates a title only with an existing head.
The optional [V8 binding foundation](scripting.md) exposes live title, ID lookup
and element textContent through private primitive-only callbacks, not CLR nodes.
The parser itself never executes scripts. PageRendering's optional phase-11d
inline classic batch runs only after whole-document parsing; this is not
parser-blocking script integration.
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

| Limit | Default |
| --- | --- |
| Input UTF-16 characters before newline normalization | 16 Mi |
| Created document/element/text/comment/PI/doctype nodes | 1,000,000 |
| Open element stack depth, including html/head/body | 512 |
| Combined input/tokenizer/tree diagnostics | 10,000 |

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
entities02 and inbody01 without filtering. Broader table/template/foreign/
fragment/adoption corpora are outside this subset. Additional tests verify
mutations, unsupported failures, cancellation and exact limits. This is finite
evidence, not a claim to implement all of HTML or DOM.

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
| --- | ---: |
| HTML | 7,202 |
| DOM | 17 |
| Core | 3,815 |
| Networking | 210 |
| Platform | 19 |
| SpecCache | 23 |

First-party formatting verification and editor diagnostics are clean. A
separate standards-cache check reports all **34 documents** fresh; it is not
part of the test run. These results do not certify Windows/arm64 execution or
full HTML/DOM conformance.
