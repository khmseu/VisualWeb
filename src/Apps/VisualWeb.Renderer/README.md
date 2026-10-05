# VisualWeb.Renderer

Per-tab static rendering executable. Requires `--development-unsandboxed`
and a trusted `--font` path; intended only for private browser-launched stdio IPC.

Owns native fonts and HTML/CSS/layout/paint through VisualWeb.PageRendering
and Engine.Content. No networking/window/chrome dependency; stdout is protocol
only and diagnostics go to stderr. Native work stays on its creating main thread.
Normal user OS permissions remain: there is **no sandbox**.
Future cross-site frames require stronger site isolation than per-tab processes.

See the [process guide](../../../docs/renderer-processes.md) and
[architecture](../../../docs/architecture/overview.md).
