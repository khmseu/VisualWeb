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

The bounded list subset gives `li` a UA `display:list-item` default,
`ul` disc markers and `ol` decimal markers.
List-style longhands participate in normal inheritance and CSS-wide keywords;
only disc/circle/square/decimal/none markers, outside positioning and no marker image
are supported.
The `list-style` shorthand expands this same finite subset in any order,
resets omitted longhands, and supports CSS-wide keywords. `none` suppresses
the type and image unless the type is otherwise specified; `none decimal`
therefore retains decimal markers. Duplicate components or unsupported values
(inside, images, other marker types) diagnose the whole declaration without
partially changing longhands.
Layout currently accepts visible list-item boxes only for direct `li` children
of `ul` or `ol`; accepting a computed value does not imply general list-item layout.
The `circle` keyword is author-selectable, but nested `ul` UA defaults remain
`disc`, and it does not enable legacy HTML `type` attributes.

[CssStyleEngine](../src/Engine/Engine.Css/CssStyleEngine.cs) implements normal
UA < user < author precedence, important author < user < UA precedence, inline
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

| Group   | Properties and supported values                                                                                                                                                                      |
| ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Display | `display`: none, block, inline, inline-block, list-item, table, table-row-group, table-header-group, table-footer-group, table-row, table-cell                                                       |
| Tables  | `table-layout:auto`; inherited `border-collapse:separate`, `border-spacing`: one nonnegative length; `vertical-align:baseline/top`                                                                   |
| Lists   | inherited `list-style-type`: disc/circle/square/decimal/none; `list-style-position`: outside; `list-style-image`: none                                                                               |
| Sizing  | width/height, min-width/min-height, max-width/max-height; nonnegative lengths/percentages, auto for size/min-size, none for max-size                                                                 |
| Box     | margin sides (negative lengths/percentages or auto), padding sides (nonnegative lengths/percentages), box-sizing content-box/border-box                                                              |
| Borders | Physical side width/style/color; width nonnegative lengths or thin/medium/thick; none/hidden/solid/dotted/dashed/double/groove/ridge/inset/outset                                                    |
| Color   | color, background-color, border colors; hex 3/4/6/8, comma and space-separated rgb/rgba and in-gamut hsl/hsla, transparent/currentColor, all 148 standard opaque named colors                        |
| Fonts   | font-family ordered quoted/unquoted names; font-size lengths/percentages and size keywords; weight 1..1000/normal/bold/bolder/lighter; style normal/italic/oblique                                   |
| Text    | line-height normal/nonnegative multiplier/length/percentage; text-align start/end/left/right/center/justify; text-decoration-line none; white-space normal/pre/nowrap/pre-wrap/pre-line/break-spaces |

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
The finite `text-decoration` shorthand and `text-decoration-line` accept only
`none` and the supported CSS-wide keywords (`initial`, `inherit`, `unset`, and
`revert`), expanding to the non-inherited line longhand. `revert-layer` remains
unsupported.
`none` neither adds decoration nor inhibits a decoration from an ancestor;
this subset cannot create any decorations. Visible lines and decoration
style/color/thickness remain explicit diagnostics. The current UA sheet does
not add link underlines. See the [line property](https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration-line)
and [shorthand](https://drafts.csswg.org/css-text-decor-3/#propdef-text-decoration)
(local ID `css-text-decoration`).

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
paragraphs/lists, emphasis and monospace text, plus `form { display: block }`
and `input[type=hidden i] { display: none !important }`; it is **not** the
complete HTML rendering stylesheet (controls keep `display: inline` rather
than inline-block, and have no UA borders/padding/fonts). Broader marker types,
decoration and replaced-element sizing are not yet implemented.

Unsupported in Engine.Css: custom properties/var()/env(), calculations, cascade
layers, conditional rules (including media/supports/container), imports/font-face,
logical properties, positioning, flex/grid and broader table display, animations,
transitions, other modern color spaces/functions and unlisted properties/units.
The browser applies the limited `@import` preprocessing described above before
passing sources to Engine.Css. No layout, shaping, painting, used-value
resolution or V8 work is performed here.

Canonical HTML tables now receive their finite table display defaults, 2px
spacing, and 1px top-aligned cell padding. Automatic column sizing and explicit
downstream exclusions are documented in the
[table layout subset](text-layout.md#basic-automatic-tables). Accepted display
values do not imply anonymous table generation or arbitrary CSS table layout.

## Limits and cancellation

All CssOptions limits are positive and configurable:

| Limit                                                                          |    Default |
| ------------------------------------------------------------------------------ | ---------: |
| Input characters per individual CSS string, before normalization               |       4 Mi |
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
Direct DOM construction and arbitrarily large DOM attribute values remain the
caller's responsibility.

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
