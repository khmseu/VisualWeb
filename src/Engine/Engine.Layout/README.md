# Engine.Layout

Phase-7 finite horizontal LTR normal-flow block/inline geometry and bounded
space/internal-ASCII-hyphen line wrapping, with selected ASCII solidi
and bounded ASCII infix separators and email joiners retained in unbroken
words. Inherited `text-indent` applies supported length/percentage first-line
offsets; percentage indents in automatic table intrinsic sizing fail explicitly.
Inherited `text-align-last` controls final and forced-break line alignment;
intrinsic table measurement does not expand justified text.
Inherited `text-transform` supports ASCII `uppercase` and `lowercase` before
shaping without mutating source text; non-ASCII case mappings fail explicitly
and locale-sensitive behavior is deferred.
Inherited `word-spacing` applies nonnegative supported lengths to collapsed
and preserved ASCII spaces; negative lengths and non-ASCII word separators
remain unsupported.
Inputs are renderer-local DOM, computed CSS snapshots and an injected
text shaper. Outputs are
box/line/glyph-run snapshots, not pixels.
Depends on Engine.Dom/Css/Text, not native OS backends, loading or chrome.

Normal HTML `hidden` uses the computed `display:none` path; author/user
overrides and already-hidden ancestors behave as usual. Rendered
`hidden=until-found` on non-`embed` elements fails explicitly using an exact
ASCII-only keyword check; content-visibility and reveal are not implemented.
Rendered `<embed hidden>` retains the separate specialized-layout failure.
Atomic button/select descendants are checked under the existing node, depth and
cancellation limits, even offscreen. A rendered select with a display-none
descendant fails rather than altering option selectedness or submission; a
display-none select and its subtree remain exempt.

Unordered `ul`, `menu` and legacy `dir` lists use block list-item boxes with separate outside marker geometry;
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
marker styles and arbitrary list-item elements remain unsupported. `dir` is
presentation-only: its direct `li` children use unordered marker geometry,
without DOM list ownership or ordered-list attributes. See the
guide for exact geometry.
Definition-list descriptions use the HTML start indent as a physical left
margin in this horizontal LTR layout subset.

Inputs: DOM, computed CSS and text metrics. Official document IDs: `css-box`,
`css-display`, `css-inline`, `css-text`, `css-values`, `css-sizing`,
`css-overflow`, `css2-visual`, `css2-sizing`, `css2-lists`,
`css-counter-styles`, `css2-tables`, `unicode-linebreak`.
See the [standards workflow](../../../docs/standards.md).
Read the [text/layout guide](../../../docs/text-layout.md) for exact scope:
non-root vertical margins must be zero while margin collapse is deferred;
unsupported advanced layouts throw rather than return approximate geometry.

Canonical HTML tables support bounded automatic intrinsic columns, equal
minimum/maximum constraint distribution for horizontal `colspan`, separate
borders and one or two `border-spacing` lengths (one for both axes, or
horizontal then vertical).
Cells default to top alignment; baseline aligns the first in-flow line (or the
empty cell's content bottom) with the first spanned row baseline. `middle`
centers contents and `bottom` aligns them to the bottom, including across a
cell's full row span and internal row spacing.
Cell `td`/`th` `valign=baseline`, `top`, `middle` and `bottom` map to those
values case-insensitively. Baseline row spans use the existing even row-height
deficit distribution; CSS 2.2 does not specify how baseline row spans affect
row heights, so this is a local layout policy. Positive
`rowspan` and `rowspan=0` are bounded to their row group; row-height deficits
are shared evenly across covered rows. The materialized columns are capped by
`LayoutOptions.MaxTableColumns`, column-track visits by
`LayoutOptions.MaxTableColumnOperations`, and row-span grid/height work by
[`LayoutOptions.MaxTableRowSpanOperations`](./StaticLayout.cs). Table,
row-group and row backgrounds
paint before cell backgrounds so later rows cannot cover spanning cells.
One first-child HTML caption with `display:table-caption` is laid out above or
below the table according to inherited `caption-side:top/bottom` (initially
`top`), at the table's border-edge width; caption content does not affect
intrinsic column sizing. Bounded HTML `colgroup`/`col` spans add tracks, and
computed nonnegative CSS-pixel widths constrain column/group automatic sizing.
Percentage `col`/`colgroup` widths constrain track minimums against the final
table width. HTML `col[width]` values map to CSS width hints, including zero
and percentages; ordinary author CSS overrides the hint, while
`colgroup[width]` remains unsupported. Auto-width tables use a bounded
deterministic solve sharing the table-column operation and cancellation
budgets; constraints without a finite solution fail explicitly.
HTML `td`/`th` width hints contribute to intrinsic cell sizing constraints,
including bounded distribution across spanning columns. Percentage cell widths
remain unsupported by intrinsic table layout.
Solid column/group `background-color` uses bounded cell-origin geometry,
including complete bounds of spanning cells beyond their originating
track/group; these rectangles share the table-column operation limit. See the
layout guide for the layer order and exact limits.
Nested tables/widgets and other
[listed exclusions](../../../docs/text-layout.md#basic-automatic-tables) fail explicitly.
The table's `align=center` hint centers it with auto inline margins. Cell
`td`/`th` `align` hints map the supported left/right/center/middle/justify
values to text alignment; the same values on `tr`, `thead`, `tbody` and
`tfoot` are inherited by their cells. These six table elements also support
the legacy `align=absmiddle` alias for centered text. Unsupported HTML
`valign` values and legacy alignment of over-constrained descendant margins
remain unsupported. Row/group `valign=baseline/top/middle/bottom` supplies a
default to contained cells; a nearer row wins over its group, and cell CSS or
`valign` overrides it.
The HTML table `cellspacing` hint maps values that parse as non-negative
integers to equal horizontal and vertical pixel spacing; values that fail
parsing add no hint, and normal author CSS overrides it.
The HTML table `width` hint maps nonzero dimensions to pixel or percentage
widths, with percentages resolved against the containing block. Zero and
invalid values add no hint, and out-of-range values fail explicitly. Width
attributes on other table parts remain unsupported.
The table `cellpadding` hint maps parsed non-negative integers to equal pixel
padding on all four sides of corresponding cells. Cell padding participates in
the existing intrinsic sizing and arrangement; `cellpadding` on other table
parts remains unsupported.
The boolean `nowrap` attribute on `td`/`th` maps by presence to the existing
nonwrapping text flow; other table parts' `nowrap` attributes remain
unsupported.
Nonzero `td`/`th` `height` dimensions map to minimum cell heights through the
existing layout path; author CSS overrides the hint. Percentage cell heights
fail explicitly because CSS 2.2 does not define their row-sizing behavior.
`tr[height]` maps a dimension value, including zero, to a computed row-height
minimum. Nonnegative pixel heights are combined with cell heights and content;
percentage row heights fail explicitly because CSS 2.2 does not define their
behavior. Author CSS overrides the HTML hint. Table and row-group heights
remain unsupported.

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
lines across collapsed ASCII spaces. `text-align-last:justify` also expands
eligible final and forced-break lines, and centers them when they cannot be
stretched. Final/forced-break lines with `text-align-last:auto` follow
`text-align`, except justified lines are start-aligned; soft-wrapped justify
lines without opportunities also remain start-aligned. Expanded glyph advances
retain source and selection geometry; U+00A0 on a line needing expansion and
missing space glyphs fail explicitly. Scans share the line-break work and
cancellation budgets.
Other justification methods and properties remain unsupported.

Nontransparent solid `background-color` on non-replaced inline text is emitted
as one merged rectangular fragment per inline element and line.
`LayoutOptions.MaxInlineBackgroundOperations` defaults to 1,000,000 and bounds
fragment-owner visits. Inline margins/padding/borders and backgrounds on or
around atomic widgets or line breaks remain explicit failures; the subset adds no image,
gradient, clipping or corner behavior. See the
[layout guide](../../../docs/text-layout.md#ordinary-flow).
