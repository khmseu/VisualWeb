# Engine.Css

Phase-6 first-party CSS Syntax tokenization/parsing, static HTML selectors and
typed computed styles. UA/user/author/inline cascade, !important, specificity,
inheritance, CSS-wide keywords and initial block/inline properties are supported.
HTML `font color` uses the full bounded legacy-color algorithm at the author
presentational hint origin. HTML `font size` uses the
[legacy font size algorithm](https://html.spec.whatwg.org/multipage/rendering.html#rules-for-parsing-a-legacy-font-size)
(local ID `html`) and the existing CSS absolute-size mapping; `font face` is
parsed as a bounded CSS font-family list at the author presentational-hint
origin. The text shaper checks listed families in order against explicitly
registered faces; there is no implicit fallback, font loading, synthesis or CSS
`@font-face` support. Independent per-computation
`MaxFontColorAttributeCharacters`, `MaxFontFaceAttributeCharacters` and
`MaxFontSizeAttributeCharacters` limits count raw UTF-16 attribute units,
including invalid values, before parsing.
The [HTML flow-content defaults](https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3)
(local ID `html`, checked 2026-10-04) include `search { display:block }`
as a normal, disableable UA declaration; it introduces no search widget.
The [HTML hidden UA rule](https://html.spec.whatwg.org/multipage/rendering.html#hiddenCSS)
(ID `html`) sets `[hidden]:not([hidden=until-found i]):not(embed)` to
`display:none` through the normal cascade, without `!important`. Empty, invalid
and whitespace-padded values are ordinary hidden; the separate `until-found`
keyword matches ASCII case only. Author/user/inline overrides, importance,
`revert` and disabling the UA sheet retain their normal behavior. Rendered
`until-found` elements and `<embed hidden>` still fail at layout's explicit
unsupported boundaries; this rule does not change script/style processing or
successful form values.
Selectors include `:scope` with explicit immutable `CssSelectorScope` roots
(Element, Document, DocumentFragment virtual root); stylesheets default to `:root`.
`:any-link` matches HTML `a` and `area` elements with an `href` attribute,
regardless of its value or navigation policy. `:link` matches the same elements,
treating every link as unvisited without history access; `:visited` remains
explicitly unsupported. The [HTML phrasing-content UA rule](https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3)
(ID `html`) sets `:link { color: #0000EE }`. It uses the normal cascade and
inheritance. The same rule sets `text-decoration:underline`; the non-inherited
decoration is painted on static text fragments and cannot be canceled by a
descendant's `none`. User/author/inline overrides and `revert` use the normal
cascade; disabling the UA sheet removes both defaults. Active/hover styling
and cursor behavior remain unsupported.
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
The [HTML phrasing-content UA rules](https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3)
(ID `html`) give `big`/`small` the existing `larger`/`smaller` relative sizes,
`dfn` the existing italic default and `tt` the existing monospace default.
They use the parent's computed size and normal inheritance/cascade, and are
absent when the UA sheet is disabled. Text shaping still requires exact
configured faces; no fallback, synthesis or additional layout/text support is
implied.
The [HTML flow-content defaults](https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3)
(ID `html`) give `listing`, `xmp` and `plaintext` the same block display,
1em vertical margins, monospace family and preserved whitespace as `pre`.
They use the normal cascade and require an explicitly configured face.
`text-decoration` and `text-decoration-line` accept `none`, `underline`,
`line-through` and
the supported CSS-wide keywords (`initial`, `inherit`, `unset`, and `revert`),
with a non-inherited line value and the normal cascade. `revert-layer` remains
unsupported. `none` neither adds nor inhibits ancestor decoration. Paint uses
the decorating ancestor's computed color and the registered shaping font's
OpenType underline metrics; line-through uses the exact registered font's
OpenType strikeout metrics. HTML `del`, `s` and `strike` receive the
line-through UA default. Overline, other decoration
style/color/thickness values, atomic-inline decoration and skip-ink remain
unsupported.

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
`list-style-type:disc/circle/square/decimal/decimal-leading-zero/none/lower-alpha/upper-alpha/lower-latin/upper-latin/lower-roman/upper-roman`,
`list-style-position:outside`, and
`list-style-image:none` (official IDs `css2-lists` and `css-counter-styles`). UA `li` defaults to
list-item. The `list-style` shorthand expands the same finite longhands,
including resets, CSS-wide keywords and `none` ambiguity handling.
HTML UA marker defaults select discs, circles or squares according to list
ancestor depth; nested ordered lists remain decimal. `menu` receives the same
UA block display and start padding as `ul`; `dir` participates in the marker
rules without adding support for its list layout. Broader list styles remain
unsupported.
For horizontal writing, HTML UA list block margins are represented by 1em
top/bottom margins on outer `dir`/`dl`/`menu`/`ol`/`ul` and zero on list
descendants of any of those types. Author and user cascade remain unchanged.
List `type` hints support `disc`/`circle`/`square`/`none` on `ul`/`li` and
exact `1`, `a`, `A`, `i` and `I` on `ol`/`li`, with the author presentational hint origin, `revert` semantics
and shared expanded-assignment budget described in the guide.
HTML `align` on `p` and `h1`–`h6` supports the exact ASCII-case-insensitive
values left/right/center/justify as `text-align` hints. The `div` flow-content
rule supports left/right/center/middle/justify; `middle` maps to `center`.
The `center` element is block-level by default and supplies a `center`
`text-align` hint. These hints use the same cascade and budget. The separate
legacy behavior that adjusts over-constrained descendant margins is not
implemented. See the
[HTML rendering hints](https://html.spec.whatwg.org/multipage/rendering.html#tables-2)
and [flow-content hints](https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3)
(local ID `html`).

HTML table rendering also maps ASCII-case-insensitive `table[align=center]`
to `margin-left:auto` and `margin-right:auto` hints in the supported horizontal
LTR subset. Other table alignment values remain unsupported.

The HTML flow-content defaults give `figure` 40px left/right margins in the
supported horizontal LTR subset; these remain normal UA declarations that
author CSS may override. `address` defaults to italic, subject to the same
normal cascade and exact-face font requirements as other italic elements.

The finite table properties add canonical table/group/row/cell displays,
`table-layout:auto`, inherited `border-collapse:separate` and single-length
`border-spacing`, plus `vertical-align:baseline/top` (ID `css2-tables`).
UA cells use 1px padding and top alignment; UA tables use 2px spacing.
See the [layout subset](../../../docs/text-layout.md#basic-automatic-tables)
for accepted geometry and explicit downstream failures.
