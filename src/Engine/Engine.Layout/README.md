# Engine.Layout

Phase-7 finite horizontal LTR normal-flow block/inline geometry and bounded
space/internal-ASCII-hyphen line wrapping, with selected ASCII solidi
and bounded ASCII infix separators and email joiners retained in unbroken
words. Inputs are renderer-local DOM, computed CSS snapshots and an injected
text shaper. Outputs are
box/line/glyph-run snapshots, not pixels.
Depends on Engine.Dom/Css/Text, not native OS backends, loading or chrome.

Unordered `ul` and `menu` lists use block list-item boxes with separate outside marker geometry;
nested/empty items and inherited marker suppression are bounded. HTML UA defaults
use discs, circles and squares at increasing list depth. Circles and squares use
the same outside geometry as discs; circles are hollow.
Lists also support shaped decimal, zero-padded decimal, lower/upper ASCII alphabetic and Roman markers,
including the `lower-latin`/`upper-latin` aliases, with independent `start`,
`reversed` and `li value` numbering; nonpositive alphabetic ordinals use
decimal fallback. Padded decimal uses two characters including any negative
sign, so `0`/`9` become `00.`/`09.` while `-1` stays `-1.`.
Roman ordinals outside 1..3999 use decimal fallback rather
than extended overlined forms. Scans and output share existing budgets.
Legacy `type` hints support disc/circle/square/none on `ul`/`li` and exact
`1`/`a`/`A`/`i`/`I` on `ol`/`li`; unsupported values still fail explicitly.
`menu` ignores `type`, `start` and `reversed` for list rendering. Inside markers, other
marker styles, `dir` list layout and arbitrary list-item elements remain unsupported. See the
guide for exact geometry.

Inputs: DOM, computed CSS and text metrics. Official document IDs: `css-box`,
`css-display`, `css-inline`, `css-text`, `css-values`, `css-sizing`,
`css-overflow`, `css2-visual`, `css2-sizing`, `css2-lists`,
`css-counter-styles`, `css2-tables`, `unicode-linebreak`.
See the [standards workflow](../../../docs/standards.md).
Read the [text/layout guide](../../../docs/text-layout.md) for exact scope:
non-root vertical margins must be zero while margin collapse is deferred;
unsupported advanced layouts throw rather than return approximate geometry.

Canonical HTML tables support bounded automatic intrinsic columns, separate
borders, one spacing length and top-aligned text/block cells. Table/cell
backgrounds and ordinary borders reuse existing flow. Spans, captions/columns,
row/group backgrounds, nested tables/widgets and other
[listed exclusions](../../../docs/text-layout.md#basic-automatic-tables) fail explicitly.

Nonwrapping `pre`/`nowrap` text accepts supported Latin/common punctuation,
including URLs and hyphens, retaining forced-break/space handling, overflow
geometry and shared shaping/text limits. In wrapping modes, a leading hyphen
before an ASCII digit and internal hyphens between ASCII letters/digits in one
text node are supported; only an internal hyphen before a letter is a soft
break. A solidus between two ASCII letters or two ASCII digits stays unbroken,
allowing dates, negative numbers and fractions without general numeric breaking.
Likewise `.`, `,`, `:` and `;` between ASCII letters or digits in one text
node remain unbroken, including `example.com`, `1,000.25` and `12:30`.
Internal `@`, `_` and `+` between ASCII letters or digits in one text node
also remain unbroken, including `first.last+tag@example.com` and
`user_name@example.com`; this is not general email or URL line breaking.
Repeated hyphen break analysis uses a shared work budget; leading/trailing,
repeated, mixed letter/digit and non-ASCII-adjacent solidi, other punctuation/Unicode-break
exclusions (including adjacent or non-ASCII-adjacent internal infix separators
and email joiners) and cross-node/style shaping restrictions remain.

Supported inline form controls wrap on either side in wrapping whitespace modes,
but not in `pre`/`nowrap`. Direct adjacency to U+035C..U+0362 glue in wrapping
modes fails explicitly because line-break backtracking is not implemented;
separating spaces and the U+00A0 atomic-boundary exception remain supported.
General inline-block layout remains unsupported.

`text-align:justify` distributes positive slack on soft-wrapped English text
lines across collapsed ASCII spaces. Final/forced-break lines and lines without
opportunities remain start-aligned. Expanded glyph advances retain source and
selection geometry; U+00A0 on a line needing expansion and missing space glyphs
fail explicitly. Scans share the line-break work and cancellation budgets.
Other justification methods and properties remain unsupported.
