# Engine.Layout

Phase-7 finite horizontal LTR normal-flow block/inline geometry and space-based
line wrapping. Inputs are renderer-local DOM, computed CSS snapshots and an
injected text shaper. Outputs are box/line/glyph-run snapshots, not pixels.
Depends on Engine.Dom/Css/Text, not native OS backends, loading or chrome.

Default unordered lists now use block list-item boxes with separate outside
disc-marker geometry; nested/empty items and inherited marker suppression are
bounded. Ordered lists add shaped decimal markers with independent `start`,
`reversed` and `li value` numbering; scans and output share existing budgets.
Inside markers, other marker styles and arbitrary
list-item elements remain unsupported. See the guide for exact geometry.

Inputs: DOM, computed CSS and text metrics. Official document IDs: `css-box`,
`css-display`, `css-inline`, `css-text`, `css-values`, `css-sizing`,
`css-overflow`, `css2-visual`, `css2-sizing`, `css2-lists`.
See the [standards workflow](../../../docs/standards.md).
Read the [text/layout guide](../../../docs/text-layout.md) for exact scope:
non-root vertical margins must be zero while margin collapse is deferred;
unsupported advanced layouts throw rather than return approximate geometry.
