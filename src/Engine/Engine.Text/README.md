# Engine.Text

Phase-7 renderer-local font loading, native HarfBuzz Latin/LTR shaping and
horizontal metrics. FontSet uses explicit family/face registration and the
portable IFontCatalog; no OS backend dependency or silent font fallback.
Fonts are owned, disposable and creating-thread-affine; runs are managed snapshots.

Official document IDs: `css-fonts`, `css-text`, `css-inline`, `harfbuzz-shaping`,
`harfbuzz-font`. HarfBuzzSharp/Linux/Win32 assets are pinned together.
See the [standards workflow](../../../docs/standards.md).
Read the [text/layout guide](../../../docs/text-layout.md) for loading,
registration, native ownership, supported text, limits and target validation.
