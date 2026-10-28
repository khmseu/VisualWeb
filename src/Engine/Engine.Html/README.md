# Engine.Html

First-party whole-string `HtmlTokenizer` and bounded static `HtmlParser`.
Tokenization covers tags/attributes, comments, doctypes, current processing
instructions, references and contextual text/script/CDATA modes. Official named
references are embedded [pinned data](Data/README.md).

Tree construction supplies implicit html/head/body, head metadata/text elements,
ordinary body elements, lists, paragraphs, headings, void elements, bounded
select/optgroup parsing, a simple table caption/column-group/section/row/cell
subset, inert in-table `script`/`style` text, table `type=hidden` inputs and
text-only table foster parenting, bounded nested-table recovery and the
in-table form-pointer exception, and root-attribute merging for in-table
`html` starts. Other tokens requiring table foster parenting remain
unsupported. Legacy doctypes preserve public/system identifiers and recover
with explicit diagnostics, using the complete HTML initial-mode tables for
no-quirks/limited-quirks/quirks classification. No DTD resources are loaded and
additional quirks-layout behavior is not implied.
Advanced features throw `UnsupportedHtmlException`; safety limits throw
`HtmlLimitException`. Results contain a renderer-local DOM and diagnostics.
Scripts are tokenized as text, never executed.

See the [HTML/DOM guide](../../../docs/html-dom.md) for exact supported/deferred
features, caller decoding policy, limits and conformance evidence.

Official source: https://html.spec.whatwg.org/multipage/parsing.html (ID `html`;
the cache holds the single-page standard).
See the [standards workflow](../../../docs/standards.md).
