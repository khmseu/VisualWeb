# Engine.Paint

Immutable background/solid-border/glyph display lists, SkiaSharp CPU rasterization
and portable opaque BGRA presentation. Registers exact shaping font bytes
explicitly; no font fallback, GPU, resource loading or browser UI.
Outside list markers use bounded filled ellipses for discs, a single
stroke-ellipse command for hollow circles, and existing rectangle commands
for squares. Nested-list UA defaults and author styles use the same commands.
Circle strokes remain inside their bounds and
preserve the actual underlying center pixels.

See the [painting guide](../../../docs/painting.md) for scope, ownership, budgets
and offline evidence, the [project map](../../../docs/project-map.md) and
[standards workflow](../../../docs/standards.md).
