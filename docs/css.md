# Phase-6 static CSS foundation

The user approved a first-party static CSS subset: syntax, static selectors,
UA/user/author/inline cascade, inheritance and a finite property set for initial
block/inline layout. [Engine.Css](../src/Engine/Engine.Css/) references
[Engine.Dom](../src/Engine/Engine.Dom/) only. No native, networking, browser
chrome or script dependency is introduced.

## Entry points and integration boundary

```csharp
var syntax = CssSyntax.ParseStyleSheet("p { color: red }");
var selector = CssSelectorList.Parse("body > p:first-child");
var specificity = selector.Match(paragraph, cancellationToken);
var styles = CssStyleEngine.Compute(
    document,
    [new CssStyleSource("p { color: red; margin: 1em }")],
    cancellationToken: cancellationToken);
var color = styles.Styles[paragraph]["color"];
```

Callers supply already-decoded CSS text and stylesheets **in document source
order**, with explicit origins. The built-in UA sheet is first unless disabled.
Inline `style` attributes are parsed automatically. Extracting applicable
`style` elements, ordering linked/embedded sheets, byte decoding and loading
remain the responsibility of the caller and the browser's PageRendering layer. This API does
not automatically collect document style elements or fetch `link`/`@import`
URLs. It never executes JavaScript.

The browser's PageRendering layer separately supports recursive leading
`@import` rules from embedded and browser-fetched stylesheets, including exact
`all`, `screen` and `print` media-type lists. The browser broker fetches
applicable resources; Engine.Css still receives an ordered, already-expanded
source list and has no network dependency. Other conditional imports, `layer`
modifiers and late imports fail visibly.

The result is a read-only computed snapshot keyed by renderer-local DOM
elements. It does not observe mutations: call Compute again after a DOM/style
change. Do not mutate the DOM concurrently with matching/computation. Neither
nodes nor style dictionaries are IPC identities or security principals.

## Syntax and recovery

[CssTokenizer](../src/Engine/Engine.Css/CssSyntax.cs) implements CSS Syntax
tokenization: identifiers/functions, hashes and ID flags, at-keywords, strings,
URLs, numbers/percentages/dimensions, delimiters, brackets, CDO/CDC and whitespace.
Comments are removed without merging neighboring tokens. CR/CRLF/form feed
become LF; NUL and lone surrogates become U+FFFD. Escapes decode CSS code points,
including the recoverable escaped-EOF rule. Numeric spelling, integer flags,
dimension units and normalized source text are retained.

[CssSyntax](../src/Engine/Engine.Css/CssSyntax.cs) parses stylesheet rules and
declaration lists, retaining balanced flat component tokens rather than a
CSSOM object model. It handles nested functions/blocks, semicolons within
components, EOF block closure, trailing whitespace and `!important`.
At-rules are retained by stylesheet syntax parsing; the style engine evaluates
only the bounded `@media` subset below. Declaration-block at-rules and nested
style rules produce explicit diagnostics while following declarations remain
parseable. CSS nesting, descriptor contexts and the optional Unicode-range
tokenization extension are not implemented.

Diagnostics contain a code, message and **normalized UTF-16 offset within the
individual input**, not byte offsets or line/column positions. Results expose
diagnostics, including recoverable tokenizer errors on compiled selectors.
Invalid direct selector syntax throws FormatException; unsupported direct
selectors throw UnsupportedCssException. Style computation catches only those
expected selector failures, diagnoses and discards the affected whole rule.
Unsupported properties/values diagnose and discard the declaration, never
override an earlier valid declaration. Limit and cancellation exceptions
propagate and do not return partial success.

## Screen media-type rules

The static style engine supports [`@media`](https://www.w3.org/TR/css-conditional-3/#at-ruledef-media)
using only comma-separated `all`, `screen` and `print`
[media types](https://www.w3.org/TR/mediaqueries-5/#media-types), with
ASCII-case-insensitive identifiers and CSS escapes (official IDs
`css-conditional-3` and `css-mediaqueries-5`). An empty media query list
matches, as does a list containing `all` or `screen`; print-only lists do not.
Matching groups may nest and are compiled at their original cascade positions.
All enclosing groups must match. Print-only contents are not semantically
evaluated, although the whole input is tokenized and component-depth limits
apply. Conditions, modifiers, unknown media types and malformed lists produce
diagnostics instead of a partial match. Other active nested at-rules, including
`@import`, are diagnosed and never fetched. The browser's HTML media-attribute
policy remains separate and unchanged.

The shared `CssMediaTypeList.TryMatchesScreen` accepts already-tokenized lists,
enforces `CssOptions.MaxTokens`, and distinguishes an unsupported list from a
valid nonmatching one. Cancellation propagates; callers must surface a false
return rather than treating it as a print-only match.

## Static selectors

`CssSelectorList.Filter(candidates, cancellationToken)` lazily returns matches
in supplied order under one shared candidate/operation budget, rather than
resetting counters for each candidate. Phase-11i DOM queries reuse this API
with stricter bounds; see the [scripting guide](scripting.md) for scope,
static-list behavior and unsupported selector errors.
`Match`/`Filter` overloads accept an explicit immutable `CssSelectorScope`
(Element, Document or DocumentFragment scoping root) that is fixed for one
call, so reusing a selector list cannot leak a scope. The scope resolves
`:scope` only; restricting candidates to descendants remains the caller's job.

Supported:

- HTML type/universal, ID, class and attribute selectors.
- Attribute existence, `=`, `~=`, `|=`, `^=`, `$=`, `*=`, and explicit `i`/`s`
  flags. Case-insensitive comparison is **ASCII-only**; HTML's legacy
  case-insensitive attribute list is honored by default.
- Descendant, child, adjacent sibling and general sibling combinators;
  non-element siblings do not affect element relationships.
- `:root`, `:empty`, first/last/only-child and first/last/only-of-type.
- [`:any-link`](https://drafts.csswg.org/selectors-4/#the-any-link-pseudo)
  matches HTML `a` and `area` elements with an `href` attribute, including
  empty or non-navigable values. Matching follows current attribute presence,
  independently of history and navigation authorization.
- [`:link`](https://drafts.csswg.org/selectors-4/#link-pseudo) matches the
  same hyperlinks as `:any-link`, treating all links as unvisited. No browsing
  history is supplied to the matcher. This privacy-preserving policy is
  described in Selectors Level 4 (local ID `selectors`); `:visited` remains
  explicitly unsupported, including in selector lists containing `:link`.
- [`:scope`](https://drafts.csswg.org/selectors-4/#the-scope-pseudo) with
  pseudo-class specificity `(0,1,0)`. Without a scoping root (stylesheets and
  scope-less matching) it equals `:root`. An Element scope matches that element;
  a Document scope matches its document element; a DocumentFragment scope is a
  featureless virtual root that only matches `:scope` (and logical
  pseudo-classes over it) as the parent of top-level elements, never as a subject.
- `:nth-child`, `:nth-last-child`, `:nth-of-type`, `:nth-last-of-type`, with
  token-based An+B parsing and signed 32-bit coefficients/constants.
  [`:nth-child(An+B of S)`](https://drafts.csswg.org/selectors-4/#the-nth-child-pseudo)
  and [`:nth-last-child(An+B of S)`](https://drafts.csswg.org/selectors-4/#the-nth-last-child-pseudo)
  accept strict complex-real-selector lists (not forgiving lists). They index
  only inclusive element siblings matching `S`, forward or backward respectively;
  the subject must belong to that filtered list. Text/comments are ignored and
  detached elements form singleton sibling lists. Nested filters share the
  original immutable scope, operation/depth budgets and cancellation.
- `:is`, `:not`, `:where`, including nested complex selectors. `:is`/`:where`
  discard syntactically invalid branches as forgiving lists; unsupported
  features still produce an explicit unsupported failure.

Specificity is an `(IDs, classes, types)` tuple compared lexicographically.
Selector lists use the most specific **matching** branch; `:is`/`:not` use their
most specific argument; filtered nth selectors add `(0,1,0)` to the maximum
argument specificity, even when that argument branch does not match.
`:where` contributes zero. HTML ID/class matching is
ASCII-insensitive in quirks mode and sensitive otherwise.

`:empty` follows the **cached current Selectors Level 4** rule: document
whitespace-only text does not prevent a match. This differs from older
Selectors 2/3 and currently deployed browser behavior; the test states the
chosen standard explicitly.

Unsupported: namespaces/column combinators, pseudo-elements, `:has`, `of` clauses
on `:nth-of-type`/`:nth-last-of-type`, the visited-state selector `:visited`,
dynamic/interactive pseudo-classes and other pseudo-classes
not listed above. No selector silently approximates these features.

## Cascade and computed values

The bounded list subset gives `li` a UA `display:list-item` default and
`ol` decimal markers. Following the [HTML list rendering rules](https://html.spec.whatwg.org/multipage/rendering.html#lists)
(local ID `html`), `dir`, `ul` and `menu` use block display and 40px start
padding. Layout supports direct `li` children of `dir` using the existing
bounded unordered-list geometry; this does not add DOM list ownership or
ordered-list semantics. `ul`/`dir`/`menu` default to disc with no list ancestor,
circle with one, and square with two or more; `dir`, `menu`, `ol` and `ul`
all count as list ancestors, including across intermediate elements.
Ordered-list UA defaults remain decimal at every depth. Author/user cascade and disabling
the UA sheet retain their existing semantics.
The same HTML rules give outer `dir`, `dl`, `menu`, `ol` and `ul` 1em top and
bottom margins and reset both margins to zero for descendants of any of those
five list types, including across intermediate elements. These physical
properties are the horizontal-writing equivalent of HTML's block margins;
they do not add vertical writing support.
HTML's `dd` default maps `margin-inline-start:40px` to `margin-left:40px` in
the supported horizontal LTR layout. Author styles override it normally;
RTL and vertical writing mappings remain unsupported.
List-style longhands participate in normal inheritance and CSS-wide keywords;
only disc/circle/square/decimal/none, `decimal-leading-zero`, the alphabetic `lower-alpha`,
`upper-alpha`, `lower-latin` and `upper-latin`, and the `lower-roman`/
`upper-roman` markers, outside positioning and no marker image are supported.
The two `latin` names are aliases for the
corresponding `alpha` styles, but computed values retain the specified name.
The `list-style` shorthand expands this same finite subset in any order,
resets omitted longhands, and supports CSS-wide keywords. `none` suppresses
the type and image unless the type is otherwise specified; `none decimal`
therefore retains decimal markers. Duplicate components or unsupported values
(inside, images, other marker types) diagnose the whole declaration without
partially changing longhands.
The UA sheet gives `menu` the same block display and 40px start padding as
`ul`; legacy `dir` uses the HTML block, start-padding and unordered-marker
defaults. Layout accepts visible list-item boxes only for direct `li` children
of `dir`, `ul`, `ol` or `menu`. `dir` support is presentation-only and does
not add DOM list ownership or ordered-list behavior.

The supported [HTML list `type` hints](https://html.spec.whatwg.org/multipage/rendering.html#lists)
(local ID `html`) map ASCII-case-insensitive `disc`, `circle`, `square` and
`none` on `ul` or `li` to their corresponding styles, and exact `1`, `a`,
`A`, `i` and `I` on `ol` or `li` to decimal, lower-alpha, upper-alpha,
lower-roman and upper-roman respectively.
The letter values are case-sensitive. They enter the
[author presentational hint origin](https://www.w3.org/TR/css-cascade-5/#preshint)
(local ID `css-cascade`): above normal user styles, below all normal author
styles, and grouped with author origin for `revert`. Important origins keep
their normal ordering. Hints apply even when the UA stylesheet is disabled;
each supported hint consumes one expanded-assignment budget unit. Recompute
styles after attribute mutations. Unsupported or malformed list `type` values
remain explicit layout failures, even if an author rule overrides them.

The [HTML paragraph and heading alignment hints](https://html.spec.whatwg.org/multipage/rendering.html#tables-2)
(local ID `html`) map `align=left`, `right`, `center` and `justify` on `p` and
`h1` through `h6` to `text-align`. The [flow-content div hints](https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3)
map `align=left`, `right`, `center`, `middle` and `justify` on `div`, with
`middle` mapping to `center`. The [table-cell hints](https://html.spec.whatwg.org/multipage/rendering.html#tables-2)
map `align=left`, `right`, `center`, `middle` and `justify` on `td` and `th`
to `text-align`, with `middle` mapping to `center`. Matching is
ASCII-case-insensitive and does not trim; unsupported values are ignored.
The same values map on `tr`, `thead`, `tbody` and `tfoot`; `text-align`
inherits through their cells. The separate legacy behavior that adjusts
over-constrained descendant margins is not implemented.
These hints use the same cascade, assignment budget and recomputation rules as
list hints. Alignment on other elements remains unsupported. The HTML `center`
element is a block by default and supplies a `text-align:center` presentational
hint.
The same table rendering rules map `valign=baseline`, `top`, `middle` and `bottom` on
`td`/`th` to `vertical-align` as ASCII-case-insensitive author presentational
hints. They share the same cascade, assignment budget and recomputation rules.
The same values on `tr`, `thead`, `tbody` and `tfoot` act as defaults for
contained cells; the nearest row hint wins over its group, and cell CSS or a
cell `valign` hint overrides it. This bounded HTML compatibility behavior does
not make CSS `vertical-align` generally inherited. Other `valign` values
remain unsupported and fail explicitly when table layout is requested.
Baseline row-spans use the documented local even row-height policy; CSS 2.2
leaves that height effect undefined.
The table rendering rules also support ASCII-case-insensitive
`table[align=center]` as `margin-left:auto;margin-right:auto` in the
horizontal LTR layout subset. Other table alignment values and legacy
descendant-margin behavior remain unsupported. Justification retains the
layout limitations documented in the text guide.
For `td`, `th`, `tr`, `thead`, `tbody` and `tfoot`, the additional legacy
`align=absmiddle` value maps to `text-align:center`; it does not apply to
other elements.
The [HTML `table[cellspacing]` hint](https://html.spec.whatwg.org/multipage/rendering.html#tables-2)
(local ID `html`) maps values parsed as non-negative integers to equal
horizontal and vertical pixel lengths for `border-spacing`; values that fail
parsing add no hint.
The same HTML table rules map a nonzero `table[width]` dimension to a `width`
presentational hint. Positive lengths become pixels and percentages remain
percentages; zero and invalid values add no hint, while values outside the
finite numeric range fail explicitly. Raw attribute scanning has a separate
aggregate limit, and ordinary author CSS overrides the hint.
The hint shares the author presentational-hint cascade, and normal author CSS
overrides it.
The HTML table `cellpadding` attribute maps a successfully parsed
non-negative integer to equal pixel values for all four padding properties of
each corresponding `td`/`th` cell. Each table attribute is counted once toward
the independent aggregate raw UTF-16 limit
`MaxTableCellPaddingAttributeCharacters`, including invalid values. The hint
uses the author presentational-hint cascade, so ordinary author CSS overrides
it; `cellpadding` on other elements remains unsupported.
HTML `td[nowrap]` and `th[nowrap]` map the boolean attribute's presence to
`white-space:nowrap` through the same author hint cascade. The attribute's
value is ignored; ordinary author CSS can override the hint. Other table
elements' `nowrap` attributes remain unsupported.
HTML `td[height]` and `th[height]` map nonzero dimension values to a `height`
presentational hint, with a separate aggregate raw UTF-16 limit
`MaxTableCellHeightAttributeCharacters`. Zero and invalid values add no hint;
ordinary author CSS overrides it. Percentage cell heights fail explicitly at
layout because CSS 2.2 does not define their row-sizing behavior.
HTML `tr[height]` maps dimension values, including zero, to the row's `height`
presentational hint, bounded by the separate aggregate raw UTF-16 limit
`MaxTableRowHeightAttributeCharacters`. Nonnegative pixel heights set a
minimum row height; percentage row heights fail explicitly because CSS 2.2
does not define their behavior. Ordinary author CSS overrides either hint.
Height attributes on tables and row groups remain unsupported.
HTML `col[width]` maps dimension values, including zero and percentages, to a
`width` presentational hint with a separate aggregate raw UTF-16 limit
`MaxTableColumnWidthAttributeCharacters`. Ordinary author CSS overrides the
hint. The CSS table-column sizing subset uses pixel widths as track
constraints and resolves percentage widths against the final table width.
`colgroup[width]` and width attributes on other table parts remain unsupported.
HTML `td[width]` and `th[width]` map nonzero dimension values to a `width`
presentational hint, with a separate aggregate raw UTF-16 limit
`MaxTableCellWidthAttributeCharacters`. Zero and invalid values add no hint;
ordinary author CSS overrides it. Percentage cell widths remain explicitly
unsupported by intrinsic table layout, as do `width` attributes on other table
parts.
The [HTML page background rule](https://html.spec.whatwg.org/multipage/rendering.html#the-page)
(local ID `html`) maps a successfully parsed `body[bgcolor]` value through the
HTML legacy-colour algorithm to `background-color`. This uses the normal
presentational-hint cascade; empty and ASCII-case-insensitive `transparent`
values add no hint, and raw attribute length has its own aggregate
per-computation limit. The offline renderer's existing body-background paint
behavior handles the computed color.
The [HTML table rendering rules](https://html.spec.whatwg.org/multipage/tables.html#tables-2)
(local ID `html`) also map `bgcolor` on `table`, `thead`, `tbody`, `tfoot`,
`tr`, `td` and `th` to `background-color` using the same legacy-colour
algorithm and author presentational-hint cascade. These attributes share a
separate aggregate raw UTF-16 limit across one style computation; empty and
ASCII-case-insensitive `transparent` values consume that budget but add no
hint. The existing table background layers carry the computed colors through
layout and paint. `background` image attributes and other elements' `bgcolor`
attributes remain unsupported.
The same HTML page rule maps a successfully parsed `body[text]` value through
the legacy-colour algorithm to the body's `color`; descendants use ordinary
CSS inheritance. It has its own aggregate raw-attribute limit and shares the
presentational-hint cascade and assignment budget. Empty and
ASCII-case-insensitive `transparent` values add no hint.
The rule maps `body[link]` through the same algorithm to the `color` of
document elements matching the static `:link` selector. Link matching never
reads navigation history and treats every link as unvisited. This is a
presentational hint with its own aggregate raw-attribute limit; normal author
rules override it. The privacy-preserving unsupported `:visited` state and
dynamic `:active` state remain unsupported, so `vlink` and `alink` are not
implemented.
The UA stylesheet gives `caption` `display:table-caption`; layout supports
one first-element-child caption. `caption-side` supports inherited `top` and
`bottom` values with the CSS 2.2 initial value `top`; bottom captions follow
the table box without changing its intrinsic column sizing
([`caption-side`](https://www.w3.org/TR/CSS22/tables.html#caption-position);
local ID `css2-tables`). The UA stylesheet also gives `colgroup` and `col`
their canonical
`table-column-group` and `table-column` display values; layout supports their
bounded HTML spans and the documented automatic-table width constraints,
including percentages solved against the final auto table width.

The [HTML `font color` hint](https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3)
(local ID `html`) uses the full
[legacy color algorithm](https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-a-legacy-colour-value),
not CSS color syntax: for example `chucknorris` becomes `#c00000`, while
`currentcolor` and alpha-looking hex strings are not CSS values here. Raw
empty and ASCII-case-insensitive `transparent` fail; whitespace-only inputs
produce black. The hint uses the same cascade, assignment budget, inheritance,
and recomputation behavior as the other hints, including without the UA sheet.
The [HTML `font size` hint](https://html.spec.whatwg.org/multipage/rendering.html#rules-for-parsing-a-legacy-font-size)
(local ID `html`) skips ASCII whitespace, accepts an optional sign and initial
ASCII digits, and ignores trailing characters. Unsigned values are absolute;
signed values are relative to 3, not the parent's size. The result is clamped
to 1–7 and mapped from `x-small` through `xxx-large` using the existing CSS
absolute-size policy. Missing digits produce no hint. The hint uses the same
cascade, inheritance, mutation and assignment rules, independent of the UA
sheet. The legacy `font face` attribute is parsed as a bounded CSS font-family
list and applied at the author presentational-hint origin. Malformed lists
create no hint; descendants inherit valid lists, and later style recomputation
reads current attributes. The shaper checks listed families in order against
explicitly registered faces only. No implicit family fallback, font loading or
synthetic face selection is added; if no listed family has a matching registered
face, text shaping fails explicitly. This does not implement CSS `@font-face`.

[CssStyleEngine](../src/Engine/Engine.Css/CssStyleEngine.cs) implements normal
UA < user < author presentational hint < author precedence, important author < user < UA precedence, inline
styles above selector-based declarations of the same origin/importance,
specificity, then source/declaration order. Validating values and expanding
shorthands happens before deciding winners. Invalid shorthands never apply
partially.

`initial`, `inherit`, `unset` and origin-based `revert` are supported, including
shorthands and `all` for every supported longhand. Reverting user-origin values
also excludes author rules; UA revert behaves as unset. Inherited values use the
parent's **computed** values. `currentColor` resolves against the element's
computed color (or inherited color for the color property itself).

Supported longhands:

| Group   | Properties and supported values                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Display | `display`: none, block, inline, inline-block, list-item, table, table-caption, table-column-group, table-column, table-row-group, table-header-group, table-footer-group, table-row, table-cell                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| Tables  | `table-layout:auto`; inherited `border-collapse:separate`, `border-spacing`: one or two nonnegative lengths (one sets both axes; two are horizontal, then vertical); `vertical-align:baseline/top/middle/bottom`                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Lists   | inherited `list-style-type`: disc/circle/square/decimal/decimal-leading-zero/none/lower-alpha/upper-alpha/lower-latin/upper-latin/lower-roman/upper-roman; `list-style-position`: outside; `list-style-image`: none                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| Sizing  | width/height, min-width/min-height, max-width/max-height; nonnegative lengths/percentages, auto for size/min-size, none for max-size                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| Box     | margin sides (negative lengths/percentages or auto), padding sides (nonnegative lengths/percentages), box-sizing content-box/border-box                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| Borders | Physical side width/style/color; width nonnegative lengths or thin/medium/thick; none/hidden/solid/dotted/dashed/double/groove/ridge/inset/outset                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| Color   | color, background-color, border colors; hex 3/4/6/8, comma and space-separated rgb/rgba and in-gamut hsl/hsla, transparent/currentColor, all 148 standard opaque named colors                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| Fonts   | font-family ordered quoted/unquoted names; font-size lengths/percentages and size keywords; weight 1..1000/normal/bold/bolder/lighter; style normal/italic/oblique                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| Text    | line-height normal/nonnegative multiplier/length/percentage; inherited text-indent length/percentage including negatives; text-align start/end/left/right/center/justify; inherited text-align-last auto/start/end/left/right/center/justify; inherited text-transform none/uppercase/lowercase; inherited word-spacing normal/nonnegative supported lengths; text-decoration-line none/underline/overline/line-through; text-decoration-thickness auto/from-font/thin/medium/thick/nonnegative supported lengths and percentages; text-decoration-style solid/double; text-decoration-color supported colors; white-space normal/pre/nowrap/pre-wrap/pre-line/break-spaces |

The layout stage implements `text-align:justify` only for the bounded English
inter-word policy described in [ordinary text flow](text-layout.md#ordinary-flow).
Inherited `text-align-last` defaults to `auto` and supports start/end, physical
left/right, center and justify alignment for the final line and lines before
forced breaks. `auto` follows `text-align`, except a justified last line is
start-aligned. Explicit `justify` expands supported spaces and centers content
when it cannot be stretched; `match-parent` remains unsupported. This subset
does not add directionality or international justification. `text-justify`
remains an unsupported declaration.
`text-indent` inherits with an initial value of zero and accepts one supported
length or percentage, including negative values. Layout indents only the first
formatted line of ordinary block flow; `hanging` and `each-line` are unsupported.
Percentages resolve against the block's own content width. Automatic table
intrinsic sizing includes pixel indents but rejects percentage indents explicitly.
Inherited `text-transform` applies ASCII `uppercase` and `lowercase` mappings
before shaping without changing DOM text or UTF-16 cluster positions.
Non-ASCII letters fail explicitly; locale-sensitive mappings, `capitalize`,
`full-width` and `full-size-kana` remain unsupported.
Inherited `word-spacing` defaults to `normal` and accepts nonnegative supported
lengths, resolved to absolute lengths before inheritance. Layout adds that
length to collapsed or preserved ASCII spaces. Negative lengths, percentage
values and non-ASCII word separators remain unsupported.
Layout also supports `white-space:pre-wrap` for preserved ASCII spaces and
segment-break newlines, wrapping after each space sequence and hanging
end-of-line spaces. Bounded `break-spaces` preserves the same characters but
wraps after each ASCII space, including between adjacent spaces; trailing spaces
count toward line alignment and automatic table intrinsic width. Preserved tabs/
form feeds, non-ASCII space separators, `break-spaces` around atomic widgets,
mixed whitespace modes inside inline content and preserved space runs adjacent
to Unicode glue remain explicit layout failures.

Shorthands: margin, padding, border-width, border-style, border-color, border and
physical border sides. The finite `background` shorthand accepts one supported
color and optional `none` in either order; `none` alone resets background-color
to transparent. It expands the currently supported background longhand before
normal cascade, including CSS-wide keywords, currentColor and importance.
Unsupported images, layers, repeat/position/size/attachment/box components and
duplicate values diagnose the entire declaration without partial assignments.
The finite `font` shorthand requires size and family, accepts optional
style/weight before size and `/ line-height` after size, and resets all five
supported font longhands before cascade. Default `normal` placeholders for
style/variant/weight/width are accepted (at most four prefix components);
non-normal variants/widths, oblique angles and system-font keywords remain
unsupported whole-declaration diagnostics. Existing font units, family lists,
CSS-wide keywords and relative weight/size computation are reused. The engine
does not add other font longhands or expand its exact native face policy.
The finite `text-decoration-line` accepts `none` alone or any distinct
combination of `underline`, `overline` and `line-through`, plus the supported
CSS-wide keywords. The `text-decoration` shorthand accepts that line set with
at most one thickness, style and color component each. Overlines use the
registered font's cap height and underline thickness as the bounded UA metric
policy; exact positions and thicknesses are UA-defined by the source
specification. The non-inherited `text-decoration-thickness` accepts `auto`,
`from-font`, `thin`/`medium`/`thick` and nonnegative supported
length/percentage values. The named widths use the existing 1/3/5 CSS-pixel
UA policy; percentages resolve against the decorating element's font size.
`auto` uses the line's existing font metric policy; `from-font` uses the
registered font's underline thickness and falls back to the line metric where
available. Explicit zero suppresses the decoration. Negative lengths and
unlisted units remain unsupported. The non-inherited `text-decoration-style`
starts at `solid` and accepts `solid`, `double` and the supported CSS-wide
keywords. The `text-decoration` shorthand accepts at most one supported line,
thickness, style and color component each, in any order; omitted components
reset to `none`, `auto`, `solid` and `currentColor`. CSS-wide keywords apply
to all four longhands.
`revert-layer` remains unsupported. Duplicate lines and `none` combined with a
visible line are invalid.
`none` does not inhibit a decoration originating on an ancestor. `double`
paints two solid bands, each one third of the registered font metric thickness,
separated by a one-third gap. Style and color come from the element that
originates a decoration, so descendant values do not recolor or restyle it. The
finite `text-decoration-color` longhand accepts the supported color syntax and
CSS-wide keywords, is non-inherited, and defaults to `currentColor`.
The UA `:link` rule supplies both the standard
unvisited blue color and an underline through the ordinary cascade. Underlines
are painted on static text fragments using the decorating ancestor's computed
`text-decoration-color` and the exact registered font's OpenType underline
metrics; they continue
through in-flow descendants, and descendant `none` cannot cancel them. Overlines
use the same origin color/style propagation and double-band behavior, positioned
at the registered font's cap height. The
HTML [phrasing-content UA stylesheet](https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3)
also underlines `ins` and `u`.
Line-throughs likewise use the decorating ancestor's computed
`text-decoration-color` and exact registered
font's OpenType strikeout position/thickness metrics; a font without usable
position metrics fails during painting. The HTML UA defaults give `del`, `s` and
`strike` line-through decoration. Dotted/dashed/wavy styles,
decoration of atomic inline content and skip-ink
are unsupported diagnostics. See the [line property](https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration-line),
[style property](https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration-style),
[color property](https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration-color)
and [shorthand](https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration)
(local ID `css-text-decoration`), plus the
[thickness property](https://drafts.csswg.org/css-text-decor-4/#propdef-text-decoration-thickness)
(local ID `css-text-decoration-4`).

Length units are **px, in, cm, mm, Q, pt, pc, em, rem, %**, plus unitless zero.
The [absolute units](https://www.w3.org/TR/css-values-4/#absolute-lengths)
use the standard fixed CSS-pixel ratios for every supported length property;
viewport and additional font-relative units remain unsupported. Font-size em/% use the
parent size; other em values use the element's size; rem uses the root size
(the initial 16px size when computing the root's own font-size). Unitless
line-height is inherited as a multiplier; percentage line-height computes to px
before inheritance. Sizing/box percentages remain percentages for layout.
Modern [`rgb()`/`rgba()`](https://www.w3.org/TR/css-color-4/#rgb-functions)
(`css-color`) permits mixed number/percentage components, optional
slash-separated alpha, and `none` components treated as zero for this static
8-bit output. CSS token boundaries also permit adjacent components where they
remain distinct tokens, including comment-separated components. Color
interpolation and missing-component preservation remain unsupported.
[`hsl()`/`hsla()`](https://www.w3.org/TR/css-color-4/#the-hsl-notation)
(`css-color`) accepts a numeric hue in degrees or `deg`, `grad`, `rad` and
`turn` angles. Modern syntax accepts numeric/percentage saturation and
lightness plus static `none` components; comma syntax requires percentage
saturation/lightness and does not accept `none`. Negative saturation clamps to
zero. Saturation above 100 and lightness outside 0..100 are diagnosed rather
than approximating out-of-gamut color mapping; interpolation and
higher-precision storage remain unsupported.
None/hidden borders have computed width zero.
Device-pixel snapping of border widths is deferred to the layout/paint integration;
this stage retains finite CSS-pixel lengths.

UA policies are intentionally explicit: initial medium font size 16px; absolute
size keywords map to 9/10/13/16/18/24/32/48px; larger/smaller scale by 1.2;
thin/medium/thick borders are 1/3/5px. Colors are clamped and rounded to 8-bit
sRGB/alpha channels, not high-precision color-management values. The minimal
UA sheet handles ordinary blocks/head metadata, body margin, headings,
paragraphs/lists, emphasis and monospace text, plus `form` and `search` as
normal UA `display: block` elements ([HTML flow-content defaults](https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3),
local ID `html`, checked 2026-10-04), and
`input[type=hidden i] { display: none !important }`; it is **not** the
complete HTML rendering stylesheet (controls have limited inline-block defaults
and no UA borders/padding/fonts). Broader marker types,
decoration and replaced-element sizing are not yet implemented.

The HTML [horizontal-rule UA rule](https://html.spec.whatwg.org/multipage/rendering.html#the-hr-element-2)
(local ID `html`, checked 2026-11-07) gives `<hr>` gray `color`, a 1px `inset`
border, and 0.5em vertical plus automatic horizontal margins. The horizontal
margins map the standard logical margins to physical sides in this LTR subset.
`overflow:hidden` is omitted because `<hr>` is void and has no child content.
The presentational hints for legacy `align`, `color`, `noshade`, `size` and
`width` attributes remain unsupported.

The [HTML flow-content defaults](https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3)
(local ID `html`) style `listing`, `xmp` and `plaintext` like `pre`: block
display, 1em physical top and bottom margins, monospace family and
`white-space:pre`. These are normal UA declarations, subject to the usual
cascade, `hidden` and UA-sheet disabling. The family still requires an
explicitly registered face.

The [HTML hidden rendering rule](https://html.spec.whatwg.org/multipage/rendering.html#hiddenCSS)
(local ID `html`) is
`[hidden]:not([hidden=until-found i]):not(embed) { display:none }`. Attribute
presence, including empty, invalid and whitespace-padded values, selects the
ordinary hidden state; only the exact ASCII-case-insensitive `until-found`
keyword selects the separate state. The declaration is normal UA CSS, not
`!important`: user, author and inline declarations, importance and `revert`
follow the existing cascade, and disabling the UA sheet disables the rule.
`input[type=hidden i]` retains its separate important UA rule. `<embed>` is
excluded because its specialized HTML layout is not implemented. Visible
`hidden=until-found` also fails explicitly in layout rather than approximating
content visibility or reveal; see [layout](text-layout.md#layout-entry-point).
Style snapshots must be recomputed after attribute mutations. The rule only
affects rendering, not discovery or processing of styles/scripts or successful
form controls.

The [HTML phrasing-content rendering rules](https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3)
(local ID `html`) include `:link { color: #0000EE; text-decoration: underline }`
in the UA sheet. The existing `:link` matcher applies it to HTML `a` and `area` elements with an
`href`, including empty, invalid or navigation-blocked values; it never
consults history and treats every link as unvisited. Descendants inherit the
color unless overridden. User, author, inline and important declarations, as
well as `revert`, use the normal cascade. With the UA sheet disabled and no
other color declaration, links inherit their parent's color or use the initial
color. `:visited`, active and hover styling and cursor behavior remain
unsupported.

The [HTML phrasing-content defaults](https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3)
(local ID `html`) set `big` to `font-size:larger`, `small` to
`font-size:smaller`, `dfn` to italic alongside `cite`/`em`/`i`/`var`, and
`tt` to monospace alongside `code`/`kbd`/`samp`. Relative sizes multiply or
divide the parent's computed size by 1.2, including when nested; the resulting
values inherit normally. User, author, inline and important declarations retain
the normal cascade, and disabling the UA sheet disables these defaults.
These rules do not add font fallback or synthesis: shaping still needs an exact
configured family/weight/style face, including an italic face for default
`dfn`. Other HTML phrasing rules, unsupported text-node/style boundaries inside
unbroken words, and the existing text/layout exclusions remain unchanged.

The same HTML phrasing-content section gives `mark` a yellow background and
black text through ordinary UA declarations. Author rules can override both
properties. Its background paints only for the bounded text-backed inline
fragment subset documented in the
[layout guide](text-layout.md#ordinary-flow); inline widgets and other
unsupported inline decoration still fail explicitly.

Unsupported in Engine.Css: custom properties/var()/env(), calculations, cascade
layers, conditional rules (including media/supports/container), imports/`@font-face`,
logical properties, positioning, flex/grid and broader table display, animations,
transitions, other modern color spaces/functions and unlisted properties/units.
The browser applies the limited `@import` preprocessing described above before
passing sources to Engine.Css. No layout, shaping, painting, used-value
resolution or V8 work is performed here.

Canonical HTML tables now receive their finite table display defaults, 2px
spacing, and 1px top-aligned cell padding. The layout subset also supports
`vertical-align:baseline`, `middle` and `bottom` on table cells, including row
spans. Automatic column sizing and explicit
downstream exclusions are documented in the
[table layout subset](text-layout.md#basic-automatic-tables). Accepted display
values include captions and column/group boxes, but do not imply anonymous
table generation or arbitrary CSS table layout.

The [HTML flow-content defaults](https://html.spec.whatwg.org/multipage/rendering.html#flow-content-3)
(local ID `html`) give `figure` 40px inline margins. The horizontal LTR subset
maps these to `margin-left` and `margin-right`; the regular cascade can override
them, and disabling the UA sheet removes them.
The same rules set `address` to italic. This does not synthesize a face: page
rendering requires an explicitly registered italic font, as it does for other
default-italic elements.

## Limits and cancellation

All CssOptions limits are positive and configurable:

| Limit                                                                          |    Default |
| ------------------------------------------------------------------------------ | ---------: |
| Input characters per individual CSS string, before normalization               |       4 Mi |
| Aggregate raw UTF-16 characters in `body[bgcolor]` per computation             |       4 Mi |
| Aggregate raw UTF-16 characters in `body[text]` per computation                |       4 Mi |
| Aggregate raw UTF-16 characters in `body[link]` per computation                |       4 Mi |
| Aggregate raw UTF-16 characters in `font color` attributes per computation     |       4 Mi |
| Aggregate raw UTF-16 characters in `font face` attributes per computation      |       4 Mi |
| Aggregate raw UTF-16 characters in `font size` attributes per computation      |       4 Mi |
| Non-EOF tokens per individual CSS string                                       |  1,000,000 |
| Component/selector nesting and selector evaluation depth                       |        128 |
| Diagnostics per parse / combined style computation                             |     10,000 |
| Styled elements                                                                |    100,000 |
| Style sources including the optional UA sheet                                  |        256 |
| Rules per parse, including active nested rules / compiled rules across sources |    100,000 |
| Expanded assignments across compiled sheets and inline styles                  |  1,000,000 |
| Nested rule token visits across all sources                                    | 10,000,000 |
| Selector evaluation operations across a computation / direct match             | 10,000,000 |

Exceeding any limit throws CssLimitException. Cancellation is checked during
input/token scanning, parsing, compilation and matching. Numeric values outside
the finite `double` range are diagnosed, including color components; finite
numeric overflow during computation also fails explicitly. Whole-string
processing and these limits are not a total memory budget or an OS sandbox.
The independent body background-color, body text-color, body link-color, font
color, face, size and table-spacing limits (`MaxBodyBackgroundColorAttributeCharacters`,
`MaxBodyTextColorAttributeCharacters`, `MaxBodyLinkColorAttributeCharacters`,
`MaxFontColorAttributeCharacters`, `MaxFontFaceAttributeCharacters`,
`MaxFontSizeAttributeCharacters` and `MaxTableSpacingAttributeCharacters`) are
checked before processing their attributes, including values that do not yield
a hint; long scans honor cancellation. Table-column and table-cell width
attributes also have separate aggregate raw UTF-16 budgets. Other direct DOM
construction and arbitrarily large DOM attribute values remain the caller's
responsibility.

## Offline evidence and standards

The [fixture guide](../tests/Engine.Css.Tests/Data/README.md) pins the complete
WPT An+B parsing family with license and digest. Tests compare all 67 cases,
including invalid syntax, then use an independent integer formula to check
matching positions 1..30. No cases are filtered and no JS is executed.
Additional regressions cover tokens, normalization/escapes, error recovery,
selectors/specificity, cascade origins, inline importance, shorthands,
inheritance/computed units/colors, unsupported features and exact limits.

Official locally cached documents: css-syntax, selectors, css-cascade,
css-conditional-3, css-mediaqueries-5,
css-values, css-box, css-display, css-fonts, css-text, css-color, css-sizing,
css-backgrounds, and html's rendering chapter. See the
[standards registry](../specs/manifest.json). Cache refresh is independent of
tests; tests never fetch references or fixtures.

```sh
dotnet test tests/Engine.Css.Tests/Engine.Css.Tests.csproj
dotnet build VisualWeb.slnx
dotnet test VisualWeb.slnx --no-build
dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party
```

### Phase-6 validation outcome

On Linux x64, all **31 projects** build and all **11,489 tests** pass, without
failures or skipped cases: CSS 203, HTML 7,202, DOM 17, Core 3,815, Networking
210, Platform 19 and SpecCache 23. First-party formatting and editor diagnostics
are clean. Independently, all **37 cached references** are fresh.

These are finite subset/regression results, not full CSS/browser conformance
or Windows/arm64 execution certification.
