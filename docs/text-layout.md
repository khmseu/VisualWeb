# Phase-7 text shaping and static layout

The user approved HarfBuzzSharp with Linux/Windows native assets, a licensed
bundled test font and a finite horizontal LTR block/inline layout subset.
Advanced bidi, full Unicode line breaking, floats, general inline-block and
replaced-element layout remain deferred.

[Engine.Text](../src/Engine/Engine.Text/) references Platform.Abstractions,
not Linux/Windows/SDL backends. [Engine.Layout](../src/Engine/Engine.Layout/)
references DOM, CSS and Text. No rasterizer, window/display initialization,
browser chrome, scripting or loading dependency is added.

## Font setup and shaping

```csharp
using var font = new TextFont(pathToTrustedRegularFont);
var fonts = new FontSet();
fonts.Register("Example", font);
fonts.Register("serif", font); // Explicit generic-family mapping.
var run = fonts.Shape(
    "office",
    new TextFontRequest(["Example", "serif"], 16),
    cancellationToken);
```

TextFont owns its HarfBuzz blob/face and must be used/disposed on its creating
renderer thread. FontSet borrows registered fonts; owners dispose fonts after
all shaping users are finished. ShapedRun is a managed snapshot containing
glyph IDs, normalized screen-coordinate offsets, advances, UTF-16 clusters,
font identity/size and horizontal metrics. Native handles never escape.
Identity hashes exact loaded bytes plus face index; CopyFontData transfers an
independent bounded byte copy for explicit [painting registration](painting.md).
Positive y points down; glyph offsets are relative to the baseline. Clusters
index the run's text, not document byte offsets or original whitespace.

HarfBuzzSharp and its Linux/Win32 native assets are pinned together at
**14.2.1.301**. Linux x64 native execution is tested. Packages contain Linux and
Windows x64/arm64 assets, but asset presence is not native execution
certification on those targets. Missing native libraries are surfaced; no
fallback produces success-shaped approximate metrics.

Font bytes are bounded before loading. Native blobs use HarfBuzz
**MemoryMode.Duplicate** while the input is pinned, so the native lifetime
does not depend on managed arrays. The binding's FromStream helper uses
ReadOnly with a temporary managed pin; this implementation deliberately avoids
it. A compacting-GC regression verifies retained fonts remain usable.

FontSet.LoadFromCatalog validates a requested path against the supplied portable
IFontCatalog enumeration, then returns a caller-owned TextFont. This is not a
sandbox or font metadata discovery mechanism. Callers register actual
family/weight/style descriptors and explicit generic aliases. Selection
requires an exact configured weight/style in the requested family list; no
nearest-weight matching, synthetic bold/italic, system fallback, variation-axis
selection or font download is implied. The bundled font is for tests only.

The browser's shared page renderer can now explicitly register optional real
700 normal, 400 italic and 700 italic faces through trusted startup paths.
See [shell configuration](browser-shell.md#run-it). This uses the existing
exact matching and exact-byte paint registration, not a change to FontSet's
selection policy; missing faces, 900/other weights and oblique still fail.

This stage shapes Latin/common text, combining marks and a finite punctuation
subset with HarfBuzz's Latin script, English language and LTR direction.
Ligatures, kerning and combining clusters are real native shaping.
Unsupported scripts, bidi controls, soft hyphens, ZWJ/variation handling,
controls, lone surrogates and missing glyphs throw UnsupportedTextException.
Layout handles supported whitespace before calling the shaper.

## Layout entry point

```csharp
var styles = CssStyleEngine.Compute(
    document,
    [new CssStyleSource("""
        * { margin-top: 0; margin-bottom: 0 }
        p { font-family: Example; font-weight: 400; font-style: normal }
        """)],
    cancellationToken: cancellationToken);
var layout = StaticLayout.Layout(
    document, styles, fonts, 800, 600,
    cancellationToken: cancellationToken);
```

Read the [CSS guide](css.md) before supplying styles. Layout requires **zero CSS
diagnostics**, complete current-DOM style coverage, a finite positive viewport
and explicitly configured fonts for all participating styles. Unsupported CSS
declarations must not silently become approximate layout. Recompute styles and
layout after mutation; snapshots are not live and are not thread-safe DOM views.

The root must be block or none. Empty documents / display:none roots produce no
root box. Hidden subtrees produce no geometry and do not require text shaping.
LayoutResult returns nested block boxes with content/padding/border rectangles,
used physical margins, line rectangles/baselines and shaped text fragments
referencing renderer-local DOM text nodes and computed styles.
LayoutBox.Flow records block children and anonymous line groups in original
flow order for painting, alongside the separate Children and Lines collections.
Coordinates are absolute CSS pixels. These are geometry inputs for later paint,
not pixels, hit-testing, accessibility, IPC identities or an OS sandbox.

## Supported geometry

### Default unordered lists

Visible block `ul` containers and their direct `li` children now support
outside disc markers. UA styles give lists 40px left padding and items
`display:list-item`; each nested list uses the same disc default.
Authors may instead select `list-style-type:square` or `list-style:square`
on supported unordered or ordered items; the inherited square marker uses
the same outside bounds as a disc. This does not change nested UA defaults.
`list-style-type:none` suppresses a marker, and `display:block` on an item
removes its list-item semantics. Hidden items produce neither geometry nor
markers. Legacy `type` attributes except `ol type=1`, circle and other marker
styles, inside positioning and arbitrary non-`li` list-item boxes still fail
explicitly.

Marker geometry follows the user-agent-defined latitude in
[CSS 2.2 lists](https://www.w3.org/TR/CSS22/generate.html#lists)
(local ID `css2-lists`): a solid disc or square with a 0.3em diameter or side and a 0.5em gap
before the item's content edge, its top 0.5em above the first in-flow
descendant line baseline. Without such a line it uses the item's font strut;
an empty marked item has that strut's height. Markers do not change wrapping
width or enter selectable DOM text. Their metric shaping shares the existing
shaping-call budgets; traversal and painting retain cancellation and limits.
The LTR-only scope and explicitly registered font policy remain unchanged.

### Decimal ordered lists

Visible block `ol` containers and their direct list-item `li` children support
outside decimal markers (`1.`, `2.`, etc.). Each list has independent numbering;
`start`, boolean `reversed`, and `li value` use the HTML integer-parsing and
[ordinal-value algorithms](https://html.spec.whatwg.org/multipage/grouping-content.html#the-li-element)
(local ID `html`). Only rendered `display:list-item` children participate;
`list-style-type:none` hides a marker without removing its ordinal.
Without a valid `start`, reversed lists start at their rendered item count.
Invalid integer syntax falls back as specified; successfully parsed values
outside signed 32-bit range fail with `LayoutLimitException`. Automatic
increments/decrements can continue beyond that attribute range without wrapping.
`ol type=1` is accepted; other legacy types fail explicitly.

`list-style-type:decimal` also works on supported unordered items, including
`li value` resets. UA rules reset nested `ul` to disc and nested `ol` to decimal.
Decimal markers are shaped with the item's registered font and color, aligned
to its first in-flow baseline (or font strut when empty), and right-aligned
0.5em before the content edge. They do not consume line wrapping width, become
DOM text, or enter selection/hit-test metadata. Empty marked items retain a
font-strut height. Attribute scanning and marker output share text limits;
numbering scans share node/cancellation limits, while marker shaping/painting
share the existing call, glyph and command limits. No CSS counters, inside
markers, alphabetic/Roman markers or general list ownership are implied.

### Basic automatic tables

Canonical HTML `table`/`thead`/`tbody`/`tfoot`/`tr`/`td`/`th` boxes support
content-sized automatic columns, separate borders and one nonnegative
`border-spacing` length. See [CSS 2.2 automatic table layout](https://www.w3.org/TR/CSS22/tables.html#auto-table-layout)
(local ID `css2-tables`). Each column takes its cells' maximum minimum/maximum
intrinsic widths. Auto tables shrink toward maximum content width without
shrinking below minimum content; explicit table width is a minimum. Available
growth is proportional between intrinsic bounds, then equal beyond them.
CSS 2.2 permits UA-defined automatic distribution; this is a finite policy,
not a claim of matching every browser.

Header groups precede ordinary groups and footer groups follow them.
Cells are top-aligned and equalized to the row's maximum border-box height;
specified cell height is a minimum. Cell block margins do not collapse through
cell edges. Table/cell backgrounds and solid borders use ordinary paint flow.
UA spacing is 2px; cells have 1px padding and top alignment. Header font weight
is not synthesized. Measurement and arrangement share node, text, shaping,
line, glyph, box, depth and cancellation budgets.

Explicit failures remain for spans other than absent/exact `1`, captions,
columns/colgroups, anonymous or arbitrary CSS tables, nested tables/widgets,
percentage dimensions inside cells, collapsed borders, fixed layout, two-axis
spacing, other cell alignments, nonzero cell margins, cell maximum height,
nonautomatic table/row/group heights, row/group sizing/decorations/backgrounds,
and legacy presentation attributes (`width`, `height`, `border`, `cellpadding`,
`cellspacing`, `align`, `valign`, `nowrap`). Hidden parts are omitted; missing
cells leave empty grid slots. Empty grids have zero intrinsic content size.

### Ordinary flow

- Horizontal LTR normal-flow block boxes and undecorated inline text elements.
- Auto/px/% widths and min/max constraints; content-box/border-box sizing.
- Horizontal auto margins, centering and LTR overconstraint resolution.
- Percentage padding and margins refer to containing-block width.
- Definite heights with min/max constraints; percentage child height uses the
  parent's definite clamped content height. Against an indefinite height,
  percentage height is auto, percentage min-height is zero and max-height none.
- Auto height from stacked block children and anonymous inline line groups.
- Vertical margins between adjacent block siblings collapse, including
  positive and negative margins. Parent margins also collapse through eligible
  first/last block-child edges without intervening content/borders/padding; bottom
  propagation requires auto height and zero min-height.
- Shaped unbroken words, collapsing spaces, wrapping at spaces and supported
  internal hyphens, and overflow of unbreakable text. Exact fits do not wrap.
  Line-edge collapsible spaces drop.
- In `normal`/`pre-line`, an ASCII hyphen between ASCII letters in one text
  node is a soft break after the hyphen. The whole word stays one shaped run
  if it fits; otherwise the last fitting hyphen is used, or the first
  unbreakable segment overflows. Fragments retain their source text node,
  including during table intrinsic sizing and retained-document reflow.
  See [CSS Text line breaking](https://drafts.csswg.org/css-text-3/#line-breaking)
  (local ID `css-text`) and [Unicode line-break rules](https://www.unicode.org/reports/tr14/tr14-55.html#LB20a)
  (local ID `unicode-linebreak`, Unicode 17, UAX 14 revision 55).
- white-space normal, nowrap, pre and pre-line; preserved ASCII spaces/newlines,
  explicit br breaks. Preserved tabs/form feeds are unsupported.
  `pre`/`nowrap` accept the shaper's supported Latin/common punctuation,
  including URLs, hyphens and internal punctuation, without introducing soft
  breaks. Overflow remains measurable for document scrolling. `pre` still
  preserves spaces/forced newlines; `nowrap` still collapses whitespace.
  See [CSS Text white-space](https://drafts.csswg.org/css-text-3/#white-space-property)
  (local ID `css-text`). Script/bidi/control and missing-glyph failures remain.
- Left/start, right/end and center text alignment; baseline alignment across
  font sizes, line-height multipliers/px/normal, half-leading and inline struts.
  Negative leading is valid; an empty inline strut can affect an existing line
  but does not create a standalone visible line.

Default body and paragraph vertical margins participate in sibling and
eligible parent-edge collapse. Fonts with bold/italic styles must be configured
explicitly or those styles overridden.

## Explicit deferred cases

- Bidi/RTL/auto direction, non-English language-specific layout, script
  itemization, advanced script/fallback/emoji shaping.
- Full Unicode line breaking, hyphenation and other punctuation-internal
  opportunities. In wrapping modes, leading/trailing, adjacent or non-ASCII
  hyphens, hyphens beside digits, slashes and unsupported internal punctuation
  fail; nonwrapping pre/nowrap do not need this break analysis.
  Ordinary Latin words, apostrophes, decimal points and trailing punctuation
  form unbroken runs; no dictionary breaking or grapheme emergency wrapping.
- Mixed whitespace modes inside inline elements, pre-wrap/break-spaces, tab
  stops, justification and cross-node/style shaping inside an unbroken word.
- Margin collapse, floats, positioning, inline-block, decorated inline boxes
  (including backgrounds), tables beyond the subset above, ruby, broader list markers, replaced elements,
  general controls, images/media/iframes and specialized element layout.
  The bounded forms subset lays out `input` text/search/email/tel/url/password/date/time/month/week/number/range/checkbox/radio/submit/reset/button,
  `textarea`, `select`, and `button` controls through finite inline-block fallbacks;
  this is a layout accommodation, not complete replaced-element sizing. Inputs
  ignore children, and author styles may size supported controls within limits.
  Other input types still fail visibly.
- Vertical writing, fragmentation, scrolling/overflow clipping, device-pixel
  border snapping, text decoration and content generation.
  Initial normal-flow painting is now documented in the [painting guide](painting.md).

UnsupportedLayoutException / UnsupportedTextException do not return a partial
successful tree. Invalid/stale input and missing native capabilities surface
their own errors. No script, fetch, window creation or rasterization runs.

## Limits and cancellation

TextOptions defaults: font bytes 32 MiB, UTF-16 characters/run 65,536,
glyphs/run 65,536 and font size 4096px. Font sizes are quantized to HarfBuzz's
1/64px integer scale; negative/nonfinite sizes are invalid, zero is supported.
FontSet registration and caller-owned font counts remain caller resource policy.

LayoutOptions defaults:

| Limit                                            |    Default |
| ------------------------------------------------ | ---------: |
| Nesting, including gathered text nodes           |        128 |
| Generated block boxes                            |    100,000 |
| Visited visible nodes                            |  1,000,000 |
| Generated lines                                  |    100,000 |
| Input text characters across gathered text nodes |  1,000,000 |
| Glyphs returned across all shaping calls         |  1,000,000 |
| Shaping calls, including spaces/struts           |    100,000 |
| Hyphen line-break processing operations          | 10,000,000 |

All limits are positive/configurable, tested at boundaries and throw typed
limit exceptions. The shared line-break budget charges repeated whole-run
shaping, candidate scans/substring shaping and remainder copies, including
intrinsic table measurements. Nonfinite computed geometry also fails explicitly.
Cancellation is checked during managed scanning, gathering and shaping loops
and before/after native shaping. A native HarfBuzz call is not interruptible
mid-call; bounded run/font inputs are not a process-isolation guarantee.
The whole-string/tree strategy is not a total memory budget.

## Standards and evidence

See cached document IDs harfbuzz-shaping, harfbuzz-font, css2-visual, css2-sizing,
css-box, css-sizing, css-inline, css-text, unicode-linebreak, css-fonts and css-values in the
[standards registry](../specs/manifest.json). Official document refresh remains
independent of tests.

The [licensed pinned Noto Sans fixture](../tests/Engine.Text.Tests/Data/README.md)
provides native metric, ligature, clustering, kerning and ownership evidence.
Deterministic injected metrics give exact box/line geometry assertions;
native text-to-layout integration verifies actual shaped runs feed wrapping
and baseline geometry. Tests do not initialize displays or fetch fonts.
These finite tests do not claim full CSS layout or browser conformance.

```sh
dotnet test tests/Engine.Text.Tests/Engine.Text.Tests.csproj
dotnet test tests/Engine.Layout.Tests/Engine.Layout.Tests.csproj
dotnet build VisualWeb.slnx
dotnet test VisualWeb.slnx --no-build
dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party
```

### Phase-7 validation outcome

Validated on Linux x64: all **33 projects** build and all **11,545 tests** pass,
without failures or skipped cases. Totals: Text 17, Layout 39, CSS 203, HTML
7,202, DOM 17, Core 3,815, Networking 210, Platform 19 and SpecCache 23.
The text/layout suites exercise real native HarfBuzz without a display.
First-party formatting and editor diagnostics are clean; independently, all
**41 cached references** are fresh.

Windows/arm64 native execution remains a target check, not a certification
inferred from package asset presence or a Linux build.
