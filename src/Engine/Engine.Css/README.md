# Engine.Css

Phase-6 first-party CSS Syntax tokenization/parsing, static HTML selectors and
typed computed styles. UA/user/author/inline cascade, !important, specificity,
inheritance, CSS-wide keywords and initial block/inline properties are supported.
Selectors include `:scope` with explicit immutable `CssSelectorScope` roots
(Element, Document, DocumentFragment virtual root); stylesheets default to `:root`.
`:any-link` matches HTML `a` and `area` elements with an `href` attribute,
regardless of its value or navigation policy. `:link` matches the same elements,
treating every link as unvisited without history access; `:visited` remains
explicitly unsupported.
`:nth-child(An+B of S)` and `:nth-last-child(An+B of S)` support strict complex
selector lists, filtered inclusive element siblings and maximum argument
specificity, sharing the original scope and matching budgets.
Depends on Engine.Dom only; no native, networking or script dependency.
Color values include the complete 148-name opaque sRGB table in CSS Color 4
(`css-color`), including gray/grey and cyan/magenta aliases. Names remain
ASCII case-insensitive; transparent/currentColor retain their separate behavior.
System colors and color spaces beyond the bounded RGB and HSL forms remain unsupported.
The existing 8-bit sRGB parser accepts modern space-separated `rgb()`/`rgba()`
with mixed numeric/percentage channels, optional slash alpha and `none` as
zero for static output, alongside the existing comma-separated syntax.
Distinct adjacent tokens, including comment-separated channels, are accepted.
In-gamut `hsl()`/`hsla()` colors also convert to this sRGB representation.
Saturation above 100 and lightness outside 0..100 remain explicit diagnostics.
Supported lengths also include the CSS absolute units `in`, `cm`, `mm`, `Q`,
`pt` and `pc`, converted to CSS pixels for all existing length-valued
properties. This does not add viewport or additional font-relative units.
Nested `@media` rules support only empty or comma-separated `all`, `screen`
and `print` media-type lists in the screen-only renderer. Active groups retain
cascade order; print-only groups are skipped. Feature queries, modifiers and
other at-rules remain diagnostics; nested processing has its own shared budget.
The finite color-only `background` shorthand accepts one color plus optional
`none`, or `none` alone, and expands the supported background-color longhand.
Images/layers and other background components remain explicit diagnostics;
invalid declarations never partially override a previous color.
The finite `font` shorthand atomically resets/sets style, weight, size,
line-height and family using the existing longhand parsers. Size/family are
mandatory; optional style/weight and slash line-height are supported.
Non-normal variants/widths, oblique angles and system-font keywords remain
diagnostics. This does not add font fallback or change native face selection.
`text-decoration` and `text-decoration-line` accept only `none` or the supported
CSS-wide keywords (`initial`, `inherit`, `unset`, and `revert`), with a
non-inherited line value and the normal cascade. `revert-layer` remains
unsupported. `none` neither adds nor inhibits ancestor decoration; visible
lines and decoration style/color/thickness remain diagnostics, and the UA sheet
does not underline links.

Official documents: manifest IDs `css-syntax`, `selectors`, `css-cascade`,
`css-values`, `css-box`, `css-display`, `css-overflow`, `css-fonts`, `css-text`,
`css-color`, `css-sizing`, `css-backgrounds`, `css-conditional-3`,
`css-mediaqueries-5`, `css-text-decoration`, `css2-lists`,
`css-counter-styles` and `html`.
Sources are in the [registry](../../../specs/manifest.json).
See the [standards workflow](../../../docs/standards.md).

Read the [CSS guide](../../../docs/css.md) for entry points, source ordering,
property/value limits, UA policies and explicit deferred features. Style results
are renderer-local snapshots; the caller must recompute after mutations and
coordinate style-element/link loading separately. Unsupported features produce
diagnostics rather than approximate behavior.

The bounded list subset adds `display:list-item` and inherited longhands
`list-style-type:disc/circle/square/decimal/none/lower-alpha/upper-alpha/lower-latin/upper-latin/lower-roman/upper-roman`,
`list-style-position:outside`, and
`list-style-image:none` (official IDs `css2-lists` and `css-counter-styles`). UA `li` defaults to
list-item. The `list-style` shorthand expands the same finite longhands,
including resets, CSS-wide keywords and `none` ambiguity handling.
HTML UA marker defaults select discs, circles or squares according to list
ancestor depth; nested ordered lists remain decimal. `dir`/`menu` participate
in these CSS rules without adding support for their list layout. Broader list
styles remain unsupported.
For horizontal writing, HTML UA list block margins are represented by 1em
top/bottom margins on outer `dir`/`dl`/`menu`/`ol`/`ul` and zero on list
descendants of any of those types. Author and user cascade remain unchanged.
List `type` hints support `disc`/`circle`/`square`/`none` on `ul`/`li` and
exact `1`, `a`, `A`, `i` and `I` on `ol`/`li`, with the author presentational hint origin, `revert` semantics
and shared expanded-assignment budget described in the guide.

The finite table properties add canonical table/group/row/cell displays,
`table-layout:auto`, inherited `border-collapse:separate` and single-length
`border-spacing`, plus `vertical-align:baseline/top` (ID `css2-tables`).
UA cells use 1px padding and top alignment; UA tables use 2px spacing.
See the [layout subset](../../../docs/text-layout.md#basic-automatic-tables)
for accepted geometry and explicit downstream failures.
