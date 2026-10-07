# Phase-8 painting and offline page rendering

The approved subset is immutable display commands for canvas/block backgrounds,
solid borders and shaped text; Skia CPU rendering into opaque BGRA frames; portable
surface presentation; and a decoded-HTML-to-frame pipeline. It does not add a
browser shell, navigation, resource loading or process isolation.

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

- The root background covers the viewport and is not repainted on its box.
  For an HTML root with transparent background, the first body child's
  background is propagated instead; a display:none body does not paint.
  Other block backgrounds cover their border box.
- Normal-flow block backgrounds/borders are painted in tree order before
  inline text. LayoutBox.Flow preserves anonymous inline groups interleaved
  with block children, avoiding a geometry sort or painting all parent text
  before its intervening child block.
- Physical solid borders use nonoverlapping rectangular strips. Visible sides
  must have the same color; differing colors need deferred corner joins.
  Translucent same-color corners are composited once, not double blended.
- Positioned glyph IDs and absolute CSS baseline positions come directly from
  HarfBuzz-shaped fragments, including ligatures and combining offsets. Skia
  draws glyph blobs, **not reshaped Unicode strings**. Both use font sizes
  quantized to 1/64 CSS pixel. Text uses grayscale antialiasing, subpixel
  positions and no hinting; rectangles are not antialiased.
- Scale is an explicit finite positive CSS-to-physical multiplier. Physical
  dimensions are `ceil(viewport * scale)`. Drawing is clipped to the CSS
  viewport. Fractional physical coverage uses Skia's non-antialiased rectangle/
  clip rules; no CSS device-pixel border snapping is claimed.
- The configurable backdrop must be opaque (white by default). Source-over
  color alpha is composited over it. The resulting frame is opaque BGRA32
  with positive physical dimensions and stride `width * 4`, copied row-by-row
  from the native bitmap's actual stride.

Images, gradients, advanced/differently colored borders, border radius,
decorated inline boxes, text decoration, transforms, CSS opacity/blending,
stacking contexts, general/nested overflow clipping, GPU rendering and
browser UI are deferred. Unsupported CSS receives diagnostics and prevents
rendering; unsupported visible border styles throw UnsupportedPaintException.
This is a finite static subset, not general CSS painting conformance.

## Budgets and native boundaries

`PageRenderOptions.ScrollY` requests a vertical CSS-pixel offset (finite,
nonnegative, at most 1e9). Offline rendering keeps the original layout viewport,
returns `RenderedPage.ScrollHeight` as the larger of viewport height and root
border-box bottom, and clamps the offset before painting. `CpuRasterizer.Render`
also accepts `scrollY` for callers supplying display lists: it scales, establishes
the fixed viewport clip, then translates the canvas upward. No taller bitmap is
allocated and display-command coordinates remain unchanged. Canvas backgrounds
cover the scrollable extent. Root scroll height, raster viewport height and
vertical paint geometry are bounded to 10,000,000 CSS pixels before raster allocation.
This is a bounded document-scrolling primitive, not CSS overflow conformance.

PaintOptions defaults:

| Limit | Default |
| --- | ---: |
| Display/raster commands | 1,000,000 |
| Display/raster glyphs | 1,000,000 |
| Layout paint nesting | 128 |
| Physical framebuffer pixels | 16,777,216 |
| Registered fonts | 256 |
| Bytes per font | 32 MiB |
| Registered font bytes | 128 MiB |

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
`css-backgrounds` (canvas/body background propagation), and `skia-canvas`
(official native canvas API), alongside the text/layout references in the
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
