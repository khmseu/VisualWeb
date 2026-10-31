# Engine.Layout

Phase-7 finite horizontal LTR normal-flow block/inline geometry and bounded
space/internal-ASCII-hyphen line wrapping, with internal ASCII-letter solidi
retained in unbroken words. Inputs are renderer-local DOM,
computed CSS snapshots and an injected text shaper. Outputs are
box/line/glyph-run snapshots, not pixels.
Depends on Engine.Dom/Css/Text, not native OS backends, loading or chrome.

Unordered lists use block list-item boxes with separate outside marker geometry;
nested/empty items and inherited marker suppression are bounded. HTML UA defaults
use discs, circles and squares at increasing list depth. Circles and squares use
the same outside geometry as discs; circles are hollow.
Lists also support shaped decimal and lower/upper ASCII alphabetic markers,
including the `lower-latin`/`upper-latin` aliases, with independent `start`,
`reversed` and `li value` numbering; nonpositive alphabetic ordinals use
decimal fallback. Scans and output share existing budgets.
Legacy `type` hints support disc/circle/square/none on `ul`/`li` and exact
`1`/`a`/`A` on `ol`/`li`; unsupported values still fail explicitly. Inside markers, other
marker styles and arbitrary list-item elements remain unsupported. See the
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
geometry and shared shaping/text limits. In wrapping modes, only hyphens
between ASCII letters in one text node add a break after the hyphen; a solidus
between ASCII letters in one text node is retained without a soft break.
Repeated hyphen break analysis uses a shared work budget; leading/trailing,
repeated, non-ASCII-adjacent and digit-adjacent solidi, other punctuation/Unicode-break
exclusions and cross-node/style shaping restrictions remain.
