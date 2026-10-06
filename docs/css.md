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
remain the responsibility of the future Engine.Content pipeline. This API does
not automatically collect document style elements or fetch `link`/`@import`
URLs. It never executes JavaScript.

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
At-rules are retained by stylesheet syntax parsing but not evaluated by the
style engine. Declaration-block at-rules and nested rules produce explicit
diagnostics while following declarations remain parseable. CSS nesting,
descriptor contexts and the optional Unicode-range tokenization extension
are not implemented.

Diagnostics contain a code, message and **normalized UTF-16 offset within the
individual input**, not byte offsets or line/column positions. Results expose
diagnostics, including recoverable tokenizer errors on compiled selectors.
Invalid direct selector syntax throws FormatException; unsupported direct
selectors throw UnsupportedCssException. Style computation catches only those
expected selector failures, diagnoses and discards the affected whole rule.
Unsupported properties/values diagnose and discard the declaration, never
override an earlier valid declaration. Limit and cancellation exceptions
propagate and do not return partial success.

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
- [`:scope`](https://drafts.csswg.org/selectors-4/#the-scope-pseudo) with
  pseudo-class specificity `(0,1,0)`. Without a scoping root (stylesheets and
  scope-less matching) it equals `:root`. An Element scope matches that element;
  a Document scope matches its document element; a DocumentFragment scope is a
  featureless virtual root that only matches `:scope` (and logical
  pseudo-classes over it) as the parent of top-level elements, never as a subject.
- `:nth-child`, `:nth-last-child`, `:nth-of-type`, `:nth-last-of-type`, with
  token-based An+B parsing and signed 32-bit coefficients/constants.
- `:is`, `:not`, `:where`, including nested complex selectors. `:is`/`:where`
  discard syntactically invalid branches as forgiving lists; unsupported
  features still produce an explicit unsupported failure.

Specificity is an `(IDs, classes, types)` tuple compared lexicographically.
Selector lists use the most specific **matching** branch; `:is`/`:not` use their
most specific argument; `:where` contributes zero. HTML ID/class matching is
ASCII-insensitive in quirks mode and sensitive otherwise.

`:empty` follows the **cached current Selectors Level 4** rule: document
whitespace-only text does not prevent a match. This differs from older
Selectors 2/3 and currently deployed browser behavior; the test states the
chosen standard explicitly.

Unsupported: namespaces/column combinators, pseudo-elements, `:has`, filtered
`nth-child(of ...)`, dynamic/interactive pseudo-classes and other pseudo-classes
not listed above. No selector silently approximates these features.

## Cascade and computed values

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

| Group | Properties and supported values |
| --- | --- |
| Display | `display`: none, block, inline, inline-block |
| Sizing | width/height, min-width/min-height, max-width/max-height; nonnegative lengths/percentages, auto for size/min-size, none for max-size |
| Box | margin sides (negative lengths/percentages or auto), padding sides (nonnegative lengths/percentages), box-sizing content-box/border-box |
| Borders | Physical side width/style/color; width nonnegative lengths or thin/medium/thick; none/hidden/solid/dotted/dashed/double/groove/ridge/inset/outset |
| Color | color, background-color, border colors; hex 3/4/6/8, legacy comma rgb/rgba, transparent/currentColor, 16 basic named colors plus orange/rebeccapurple |
| Fonts | font-family ordered quoted/unquoted names; font-size lengths/percentages and size keywords; weight 1..1000/normal/bold/bolder/lighter; style normal/italic/oblique |
| Text | line-height normal/nonnegative multiplier/length/percentage; text-align start/end/left/right/center/justify; white-space normal/pre/nowrap/pre-wrap/pre-line/break-spaces |

Shorthands: margin, padding, border-width, border-style, border-color, border and
physical border sides. Font/background/text-decoration shorthands are deferred.

Length units are **px, em, rem, %**, plus unitless zero. Font-size em/% use the
parent size; other em values use the element's size; rem uses the root size
(the initial 16px size when computing the root's own font-size). Unitless
line-height is inherited as a multiplier; percentage line-height computes to px
before inheritance. Sizing/box percentages remain percentages for layout.
None/hidden borders have computed width zero.
Device-pixel snapping of border widths is deferred to the layout/paint integration;
this stage retains finite CSS-pixel lengths.

UA policies are intentionally explicit: initial medium font size 16px; absolute
size keywords map to 9/10/13/16/18/24/32/48px; larger/smaller scale by 1.2;
thin/medium/thick borders are 1/3/5px. Colors are clamped and rounded to 8-bit
sRGB/alpha channels, not high-precision color-management values. The minimal
UA sheet handles ordinary blocks/head metadata, body margin, headings,
paragraphs/lists, emphasis and monospace text; it is **not** the complete HTML
rendering stylesheet. List markers, decoration and replaced-element sizing are
not yet implemented.

Unsupported: custom properties/var()/env(), calculations, cascade layers,
conditional rules (including media/supports/container), imports/font-face,
logical properties, positioning, flex/grid/table display, animations,
transitions, modern color spaces/functions and unlisted properties/units.
No layout, shaping, painting, used-value resolution or V8 work is performed.

## Limits and cancellation

All CssOptions limits are positive and configurable:

| Limit | Default |
| --- | ---: |
| Input characters per individual CSS string, before normalization | 4 Mi |
| Non-EOF tokens per individual CSS string | 1,000,000 |
| Component/selector nesting and selector evaluation depth | 128 |
| Diagnostics per parse / combined style computation | 10,000 |
| Styled elements | 100,000 |
| Style sources including the optional UA sheet | 256 |
| Rules per parse / compiled rules across sources | 100,000 |
| Expanded assignments across compiled sheets and inline styles | 1,000,000 |
| Selector evaluation operations across a computation / direct match | 10,000,000 |

Exceeding any limit throws CssLimitException. Cancellation is checked during
input/token scanning, parsing, compilation and matching. Finite numeric overflow
during computation also fails explicitly. Whole-string processing and these
limits are not a total memory budget or an OS sandbox. Direct DOM construction
and arbitrarily large DOM attribute values remain the caller's responsibility.

## Offline evidence and standards

The [fixture guide](../tests/Engine.Css.Tests/Data/README.md) pins the complete
WPT An+B parsing family with license and digest. Tests compare all 67 cases,
including invalid syntax, then use an independent integer formula to check
matching positions 1..30. No cases are filtered and no JS is executed.
Additional regressions cover tokens, normalization/escapes, error recovery,
selectors/specificity, cascade origins, inline importance, shorthands,
inheritance/computed units/colors, unsupported features and exact limits.

Official locally cached documents: css-syntax, selectors, css-cascade,
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
