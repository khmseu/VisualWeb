# Engine.Css

Phase-6 first-party CSS Syntax tokenization/parsing, static HTML selectors and
typed computed styles. UA/user/author/inline cascade, !important, specificity,
inheritance, CSS-wide keywords and initial block/inline properties are supported.
Selectors include `:scope` with explicit immutable `CssSelectorScope` roots
(Element, Document, DocumentFragment virtual root); stylesheets default to `:root`.
`:nth-child(An+B of S)` and `:nth-last-child(An+B of S)` support strict complex
selector lists, filtered inclusive element siblings and maximum argument
specificity, sharing the original scope and matching budgets.
Depends on Engine.Dom only; no native, networking or script dependency.

Official documents: manifest IDs `css-syntax`, `selectors`, `css-cascade`,
`css-values`, `css-box`, `css-display`, `css-overflow`, `css-fonts`, `css-text`,
`css-color`, `css-sizing`, `css-backgrounds` and `html`.
Sources are in the [registry](../../../specs/manifest.json).
See the [standards workflow](../../../docs/standards.md).

Read the [CSS guide](../../../docs/css.md) for entry points, source ordering,
property/value limits, UA policies and explicit deferred features. Style results
are renderer-local snapshots; the caller must recompute after mutations and
coordinate style-element/link loading separately. Unsupported features produce
diagnostics rather than approximate behavior.

The bounded list subset adds `display:list-item` and inherited longhands
`list-style-type:disc/decimal/none`, `list-style-position:outside`, and
`list-style-image:none` (official ID `css2-lists`). UA `li` defaults to
list-item. The `list-style` shorthand expands the same finite longhands,
including resets, CSS-wide keywords and `none` ambiguity handling.
Broader list styles remain unsupported.

The finite table properties add canonical table/group/row/cell displays,
`table-layout:auto`, inherited `border-collapse:separate` and single-length
`border-spacing`, plus `vertical-align:baseline/top` (ID `css2-tables`).
UA cells use 1px padding and top alignment; UA tables use 2px spacing.
See the [layout subset](../../../docs/text-layout.md#basic-automatic-tables)
for accepted geometry and explicit downstream failures.
