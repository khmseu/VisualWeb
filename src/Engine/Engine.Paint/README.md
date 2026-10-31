# Engine.Paint

Immutable background/solid-border/glyph display lists, SkiaSharp CPU rasterization
and portable opaque BGRA presentation. Registers exact shaping font bytes
explicitly; no font fallback, GPU, resource loading or browser UI.
Outside list markers use bounded ellipse commands for discs and existing
rectangle commands for author-selected squares.

See the [painting guide](../../../docs/painting.md) for scope, ownership, budgets
and offline evidence, the [project map](../../../docs/project-map.md) and
[standards workflow](../../../docs/standards.md).
