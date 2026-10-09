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
- Shaped unbroken words, collapsing spaces, space-only wrapping and overflow of
  unbreakable text. Exact fits do not wrap. Line-edge collapsible spaces drop.
- white-space normal, nowrap, pre and pre-line; preserved ASCII spaces/newlines,
  explicit br breaks. Preserved tabs/form feeds are unsupported.
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
- Full Unicode line breaking, hyphenation and punctuation-internal
  opportunities. Hyphens/slashes and unsupported internal punctuation fail.
  Ordinary Latin words, apostrophes, decimal points and trailing punctuation
  form unbroken runs; no dictionary breaking or grapheme emergency wrapping.
- Mixed whitespace modes inside inline elements, pre-wrap/break-spaces, tab
  stops, justification and cross-node/style shaping inside an unbroken word.
- Margin collapse, floats, positioning, inline-block, decorated inline boxes
  (including backgrounds), tables, ruby, lists/markers, replaced elements,
  general controls, images/media/iframes and specialized element layout.
  The bounded forms subset lays out `input` text/search/email/tel/url/password/date/time/number/range/checkbox/radio/submit,
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

| Limit | Default |
| --- | ---: |
| Nesting, including gathered text nodes | 128 |
| Generated block boxes | 100,000 |
| Visited visible nodes | 1,000,000 |
| Generated lines | 100,000 |
| Input text characters across gathered text nodes | 1,000,000 |
| Glyphs returned across all shaping calls | 1,000,000 |
| Shaping calls, including spaces/struts | 100,000 |

All limits are positive/configurable, tested at boundaries and throw typed
limit exceptions. Nonfinite computed geometry also fails explicitly.
Cancellation is checked during managed scanning, gathering and shaping loops
and before/after native shaping. A native HarfBuzz call is not interruptible
mid-call; bounded run/font inputs are not a process-isolation guarantee.
The whole-string/tree strategy is not a total memory budget.

## Standards and evidence

See cached document IDs harfbuzz-shaping, harfbuzz-font, css2-visual, css2-sizing,
css-box, css-sizing, css-inline, css-text, css-fonts and css-values in the
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
