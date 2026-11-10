# Engine.Paint

Immutable display lists for backgrounds, solid/inset/outset/dotted/dashed/groove/ridge
borders and glyphs; SkiaSharp CPU rasterization and portable opaque BGRA
presentation. Registers exact shaping font bytes explicitly; no font fallback,
GPU, resource loading or browser UI.
Inset borders use deterministic half-channel darkening on top/left sides and
lightening on bottom/right sides; outset reverses those sides. Both preserve
alpha. Dashed borders use square-ended segments at a bounded three-to-two
dash-gap ratio, charged to the command budget. Groove and ridge use two
command-budgeted shade bands per physical side, with inverse inset/outset
shading. Double borders use two command-budgeted lines separated by a
transparent gap. Dotted borders use centered antialiased circles whose diameter
and gap equal the border thickness; each dot uses the command budget, and a side
too short to contain one full dot fails explicitly.
Outside list markers use bounded filled ellipses for discs, a single
stroke-ellipse command for hollow circles, and existing rectangle commands
for squares. Nested-list UA defaults and author styles use the same commands.
Circle strokes remain inside their bounds and
preserve the actual underlying center pixels.
Decimal (including zero-padded), ASCII alphabetic and Roman markers reuse the bounded glyph-run paint path.
Finite `underline` and `line-through` decorations use the exact registered
font's corresponding OpenType metrics and the decorating ancestor's computed
color. Missing required metrics fail explicitly; other decoration lines and
styles remain unsupported.
Text-backed inline solid backgrounds use ordered `FillRectangle` fragments
before line text and inside existing scroll clips. They share the display-list
command budget; inline-widget/line-break backgrounds and non-color background
effects remain unsupported.
Table captions use separate wrapper and table-box geometry, keeping the table's
background and border from covering its supported top caption.
Canonical table column-group and column `background-color` layers paint after
the table and before row-group/row/cell layers. Their bounded rectangles follow
cell origins, including spanning cells beyond the source track; alpha is
composited by the existing raster path.

See the [painting guide](../../../docs/painting.md) for scope, ownership, budgets
and offline evidence, the [project map](../../../docs/project-map.md) and
[standards workflow](../../../docs/standards.md).
