# Phase-8 painting and offline page rendering

The approved subset is immutable display commands for canvas/block backgrounds,
solid and selected non-solid borders, and shaped text; Skia CPU rendering into
opaque BGRA frames; portable surface presentation; and a decoded-HTML-to-frame
pipeline. It does not add a browser shell, navigation, resource loading or
process isolation.

## Entry points and ownership

```csharp
using var font = new TextFont(pathToTrustedRegularFont);
var text = new FontSet();
text.Register("Example", font);
text.Register("serif", font);
using var paintFonts = new PaintFontRegistry();
paintFonts.Register(font);

var page = OfflinePageRenderer.Render(
    "<!doctype html><p>office</p>",
    [new CssStyleSource("""
        * { margin-top: 0; margin-bottom: 0 }
        p { font-family: Example; font-weight: 400; font-style: normal }
        """)],
    text, paintFonts, 800, 600,
    new PageRenderOptions { Scale = 1 },
    cancellationToken);
page.Frame.Present(window.Surface);
```

Use the [text/layout guide](text-layout.md) and [CSS guide](css.md) for the
necessary font registrations and finite layout restrictions, including the
current limits on vertical margin collapsing. Callers open/select windows through platform services separately.
Presentation requires a frame matching the surface's current physical pixel
size; the caller chooses CSS viewport and scale and rerenders after a resize.
Presentation exceptions propagate to the caller.

Engine.Content consumes an **already-decoded string** and caller-ordered CSS
sources. Inline style attributes still participate in the cascade. Embedded
`style` elements and linked stylesheets are **not automatically collected** by
Engine.Content; browser-shell linked stylesheet discovery and brokerage is a
separate app-layer policy.
No links, images or imports are fetched, and no scripts or navigation run.
(The shell's app-layer PageRendering collects embedded and browser-fetched linked
sheets; that policy does not live in Engine.Content.)
RenderParsed accepts caller-parsed HTML for integrations such as the
[development shell](browser-shell.md); it still does not discover styles.
Networking/encoding/MIME policy and source discovery remain caller/future
orchestration responsibilities. RenderedPage retains parse recovery diagnostics,
computed styles, layout, commands and pixels. CSS diagnostics stop layout;
unsupported HTML/layout/text/paint and resource failures propagate, rather
than returning an approximate successful frame. Recompute every stage after
DOM/style mutations; these snapshots are not live documents.

TextFont retains a bounded managed copy of the exact loaded bytes and uses
native-owned HarfBuzz blob memory independently. CopyFontData returns an
independent copy on the owning thread. Font identity is SHA-256 of the loaded
bytes plus face index, not a file path to reopen. PaintFontRegistry registers
those exact bytes with Skia and owns its native typefaces. It is thread-affine
and disposable, does not dispose borrowed TextFont objects, and remains usable
after the shaping font is disposed. Identical bytes/face cannot be registered
twice. Font resolution never accesses arbitrary filesystem paths, downloads
fonts, performs fallback or synthesizes styles.

DisplayList and DrawGlyphRun snapshot bounded command/glyph collections.
Commands contain values and resource identities, never DOM nodes or native
handles. They are not yet a serialized IPC format or an untrusted-process
validation boundary. RasterFrame owns tightly packed pixels and exposes
ReadOnlyMemory; IPixelSurface does not retain presentation memory.

## Paint order and supported output

Outermost default `ul` and `menu` markers are separate `FillEllipse` commands
using the item's computed `color` and layout's bounded outside-marker rectangle.
Skia draws antialiased solid discs after block backgrounds and before that item's
text, without synthesizing DOM text, reshaping a bullet glyph or creating font
resources. Square markers, including the default for deeper nested unordered
lists, instead use the same bounds and color in non-antialiased `FillRectangle`
commands, including source-over alpha compositing. Circles, including the
one-ancestor nested default, use one data-only `StrokeEllipse` command
with a stroke one quarter of the diameter: its antialiased centerline is inset
by half the stroke, clipped to the declared bounds and drawn once with
source-over color. Its center therefore retains the actual underlying pixels,
including on nonwhite or translucent-painted backgrounds; no backdrop-colored
erasure or second color composite is used. Raster preflight checks positive
finite geometry, stroke width below half the smaller dimension and representable
Skia coordinates and device-scaled stroke before allocating the framebuffer;
outlines are rebased against root and nested scroll offsets before native float
conversion to retain small markers on long pages. Fill and clip state are
restored afterward. Marker commands share command
limits, geometry preflight, viewport transforms and cancellation with other
commands. The finite list scope is described in the
[text/layout guide](text-layout.md#default-unordered-lists).
Decimal (including zero-padded), ASCII alphabetic and Roman markers use the same bounded glyph-run command builder as page text,
with the item's exact font and color, but remain separate from selectable DOM
fragments. They paint before the item's own scroll clip, like disc markers;
ancestor clips and viewport transforms still apply.

The [basic table subset](text-layout.md#basic-automatic-tables) reuses ordered
group/row/cell block flow for table/cell backgrounds, solid borders and text.
Separate-border spacing remains transparent to the table background.
Table, column-group, column, row-group, row and cell backgrounds paint in
layer order. Column/group colors produce one rectangle for each originating
cell, covering its full bounds even when a colspan crosses the source
column/group boundary; separate-border gaps remain transparent. Alpha uses
the normal source-over raster path, and these rectangles share the layout
column-operation and paint-command budgets.
A top caption has separate wrapper and table-box geometry, so table backgrounds
and borders do not extend over the caption. Column/group background images and
other effects, collapsed-border painting and broader table-layer behavior
remain unsupported.

- The root background covers the viewport and is not repainted on its box.
  For an HTML root with transparent background, the first body child's
  background is propagated instead; a display:none body does not paint.
  Other block backgrounds cover their border box.
- Normal-flow block backgrounds/borders are painted in tree order before
  inline text. LayoutBox.Flow preserves anonymous inline groups interleaved
  with block children, avoiding a geometry sort or painting all parent text
  before its intervening child block.
- Solid backgrounds on bounded text-backed inline fragments are emitted as
  `FillRectangle` commands before all text on their line, with ancestor
  fragments before nested descendants. Their line-flow placement keeps them
  inside existing scroll clips; layout's fragment-work limit and
  `PaintOptions.MaxCommands` bound generation and display-list storage. Inline
  widget/line-break backgrounds, padding/borders/margins, images, gradients,
  clipping and corner effects remain unsupported.
- Physical solid, inset and outset borders use nonoverlapping rectangular
  strips. Dotted borders use centered antialiased circles with diameters and
  gaps equal to the border thickness; each dot consumes the display-command
  budget, and a side narrower than one dot fails explicitly. Dashed borders use
  square-ended, nonoverlapping segments with dash and gap lengths of three and
  two border widths respectively, starting at each side's origin. Every segment consumes the display-command budget. Groove and
  ridge borders split each side into adjacent outer and inner bands; groove
  applies inset shading to the outer band and outset shading to the inner, while
  ridge reverses them. Every band consumes the display-command budget. Visible
  Double borders use two solid bands, each one third of the border width, with
  the remaining third transparent between them. Each band consumes the
  display-command budget. Visible sides must have the same color; differing
  colors need deferred corner joins.
  Inset borders darken top/left channels by half and lighten bottom/right
  channels halfway toward 255, preserving alpha; this deterministic shade
  choice supplies the 3D effect without claiming exact CSS-specified shades.
  Outset borders reverse those light/dark sides to give the raised effect.
  Translucent same-color corners are composited once, not double blended.
- Positioned glyph IDs and absolute CSS baseline positions come directly from
  HarfBuzz-shaped fragments, including ligatures and combining offsets. Bounded
  inter-word justification expands the already-shaped space advances before
  painting; no additional text is inserted. Skia draws glyph blobs, **not
  reshaped Unicode strings**. Both use font sizes
  quantized to 1/64 CSS pixel. Text uses grayscale antialiasing, subpixel
  positions and no hinting; rectangles are not antialiased.
- The bounded CSS `underline` line uses a `DrawUnderline` command after each
  qualifying text fragment. It resolves the exact registered shaping font and
  uses that face's OpenType underline position/thickness plus the decorating
  ancestor's computed `text-decoration-color` (defaulting to its computed
  `color`); no fallback metric is invented. `text-decoration-style:double`
  divides the metric thickness into two solid bands and an equal gap; both
  bands remain within one command and the metric envelope. Raster preflight
  validates the font, metric and finite geometry before framebuffer allocation.
  Ancestor decoration continues through in-flow text even when a descendant
  computes `text-decoration-line:none`. The bounded CSS `line-through` uses a
  `DrawLineThrough` command and the exact registered font's OpenType strikeout
  position/thickness metrics with the decorating ancestor's computed
  `text-decoration-color`; missing
  metrics fail before framebuffer allocation. It likewise propagates through
  in-flow text despite descendant `none`. Both decoration types use the same
  originating-element style, including across descendants. The bounded CSS
  `overline` uses a `DrawOverline` command at the registered font's cap height
  and uses its underline thickness; CSS leaves exact overline metrics UA-defined.
  `text-decoration-thickness` supports `auto`, `from-font`, `thin`/`medium`/
  `thick` and nonnegative computed lengths; `from-font` uses the registered
  underline thickness, with line-through falling back to its strikeout metric.
  Explicit zero emits no decoration. Custom thickness is carried in the same
  single command and preflighted before framebuffer allocation. All three lines
  may be combined on one decorating element and are emitted as separate,
  command-budgeted paint operations in underline/overline/line-through order.
  They support only `solid` and `double`. Decorations do not cover atomic inline
  content or support skip-ink or dotted/dashed/wavy styles.
- Scale is an explicit finite positive CSS-to-physical multiplier. Physical
  dimensions are `ceil(viewport * scale)`. Drawing is clipped to the CSS
  viewport. Fractional physical coverage uses Skia's non-antialiased rectangle/
  clip rules; no CSS device-pixel border snapping is claimed.
- The configurable backdrop must be opaque (white by default). Source-over
  color alpha is composited over it. The resulting frame is opaque BGRA32
  with positive physical dimensions and stride `width * 4`, copied row-by-row
  from the native bitmap's actual stride.

Images, gradients, advanced/differently colored borders, border radii,
dotted/dashed/wavy decoration styles, custom decoration thicknesses and skip-ink,
transforms, CSS opacity/blending, stacking contexts, general/nested overflow
clipping, GPU rendering and browser UI are deferred. Unsupported CSS receives
diagnostics and prevents rendering; unsupported visible border styles throw
UnsupportedPaintException. This is a finite static subset, not general CSS
painting conformance.

## Budgets and native boundaries

`PageRenderOptions.ScrollY` and `ScrollX` request vertical/horizontal CSS-pixel
offsets (finite, nonnegative, at most 1e9). Offline rendering keeps the original
layout viewport, returns `RenderedPage.ScrollHeight` and `ScrollWidth` as bounded
root extents at least as large as the viewport, and clamps offsets before painting.
The vertical extent uses the root border-box bottom; horizontal extent uses the
maximum measurable right edge of layout border boxes, lines, text fragments and
inline widgets. Negative-left-origin overflow is not included.
`CpuRasterizer.Render` also accepts `scrollY` and `scrollX` for callers supplying
display lists: it scales, establishes the fixed viewport clip, then translates
the canvas upward/leftward. No taller or wider bitmap is allocated and
display-command coordinates remain unchanged. Canvas backgrounds cover the
scrollable extent. Root extents and paint geometry are bounded to 10,000,000 CSS
pixels before raster allocation. This is bounded root document scrolling, not
CSS overflow conformance or nested horizontal scrolling.

PaintOptions defaults:

| Limit                       |    Default |
| --------------------------- | ---------: |
| Display/raster commands     |  1,000,000 |
| Display/raster glyphs       |  1,000,000 |
| Layout paint nesting        |        128 |
| Physical framebuffer pixels | 16,777,216 |
| Registered fonts            |        256 |
| Bytes per font              |     32 MiB |
| Registered font bytes       |    128 MiB |

All limits must be positive. Constructors bound their collection snapshots;
generation and rasterization enforce aggregate limits. Native framebuffer
allocation also respects the managed BGRA byte-array integer limit. Registered
font byte limits are checked before allocating the transfer copy. Raster font
sizes are restricted to 0-4096px and native float coordinates to finite
values within +/-1e9, without nonzero-to-zero float underflow.

Commands/resources are validated before framebuffer allocation. Cancellation
is checked during tree/glyph processing, commands and row copying. Individual
native Skia/HarfBuzz calls are not interruptible mid-call. These budgets are
not a total memory bound, font-file sanitizer or security sandbox. Trusted
explicit fonts and future renderer confinement remain separate concerns.

SkiaSharp and Linux.NoDependencies/Win32 native assets are pinned at
**4.153.1**. No system Fontconfig dependency or display is needed for explicitly
registered CPU font rendering. Linux x64 execution is validated; package
Linux/Windows x64/arm64 assets do not certify execution on untested targets.

## Standards and offline evidence

Read cached IDs `css2-paint` (CSS 2.2 Appendix E normal-flow painting),
`css-backgrounds` (line-style definitions in [§3.2](https://drafts.csswg.org/css-backgrounds-3/#valdef-line-style-groove)
including `double`'s line-and-gap width constraint, and canvas/body background
propagation), and `skia-canvas` (official native canvas API), alongside the
text/layout references in the
[registry](../specs/manifest.json). Documentation refresh is independent of tests.

[Engine.Paint.Tests](../tests/Engine.Paint.Tests/) checks exact command order,
glyph/offset preservation, opaque BGRA/alpha/stride/scale/clipping pixels,
an independently authored 4x4 RGBA reference, translucent corners, native
ligatures/combining marks, font lifetime/GC, thread affinity, portable fake
presentation, invalid inputs and exact limits. [Engine.Content.Tests](../tests/Engine.Content.Tests/)
exercises offline HTML-to-native-frame integration, caller stylesheet order,
hidden pages and explicit failures. The existing licensed pinned Noto Sans
fixture is linked, not duplicated. No test fetches resources, initializes a
window or runs scripts.

```sh
dotnet test tests/Engine.Paint.Tests/Engine.Paint.Tests.csproj
dotnet test tests/Engine.Content.Tests/Engine.Content.Tests.csproj
dotnet build VisualWeb.slnx
dotnet test VisualWeb.slnx --no-build
```

### Phase-8 validation outcome

Validated on Linux x64: all **35 projects** build and all **11,573 tests** pass,
without failures or skipped cases. Totals: Paint 24, Content 4, Text 17,
Layout 39, CSS 203, HTML 7,202, DOM 17, Core 3,815, Networking 210,
Platform 19 and SpecCache 23. First-party formatting and editor diagnostics
are clean. Independently, all **43 cached references** are fresh.

Native Windows/arm64 execution and real-window presentation of page frames
remain target/integration checks; phase-8 presentation uses the portable fake
surface, while existing platform smoke tooling tests SDL surfaces separately.
