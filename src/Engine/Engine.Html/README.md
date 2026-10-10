# Engine.Html

First-party whole-string `HtmlTokenizer` and bounded static `HtmlParser`.
Tokenization covers tags/attributes, comments, doctypes, current processing
instructions, references and contextual text/script/CDATA modes. Official named
references are embedded [pinned data](Data/README.md).

Tree construction supplies implicit html/head/body, head metadata/text elements,
ordinary body elements, lists, paragraphs, headings, void elements, bounded
select/optgroup parsing and documented recovery rules. Advanced features throw `UnsupportedHtmlException`; safety limits
throw `HtmlLimitException`. Results contain a renderer-local DOM and diagnostics.
Scripts are tokenized as text, never executed.

See the [HTML/DOM guide](../../../docs/html-dom.md) for exact supported/deferred
features, caller decoding policy, limits and conformance evidence.

Official source: https://html.spec.whatwg.org/multipage/parsing.html (ID `html`;
the cache holds the single-page standard).
See the [standards workflow](../../../docs/standards.md).
