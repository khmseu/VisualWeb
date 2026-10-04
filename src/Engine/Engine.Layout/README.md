# Engine.Layout

Phase-7 finite horizontal LTR normal-flow block/inline geometry and space-based
line wrapping. Inputs are renderer-local DOM, computed CSS snapshots and an
injected text shaper. Outputs are box/line/glyph-run snapshots, not pixels.
Depends on Engine.Dom/Css/Text, not native OS backends, loading or chrome.

Inputs: DOM, computed CSS and text metrics. Official document IDs: `css-box`,
`css-display`, `css-inline`, `css-text`, `css-values`, `css-sizing`,
`css2-visual`, `css2-sizing`.
See the [standards workflow](../../../docs/standards.md).
Read the [text/layout guide](../../../docs/text-layout.md) for exact scope:
non-root vertical margins must be zero while margin collapse is deferred;
unsupported advanced layouts throw rather than return approximate geometry.
