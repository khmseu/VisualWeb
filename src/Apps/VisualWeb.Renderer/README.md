# VisualWeb.Renderer

Per-tab static rendering executable. Requires `--development-unsandboxed`
and a trusted `--font` path; intended only for private browser-launched stdio IPC.

Owns native fonts and HTML/CSS/layout/paint through VisualWeb.PageRendering
and Engine.Content. No networking/window/chrome dependency; stdout is protocol
only and diagnostics go to stderr. Native work stays on its creating main thread.
Normal user permissions remain in development-unsandboxed mode. The trusted
`--linux-sandbox-bootstrap` reexecutes into required Linux confinement before
receiving any page. Direct `--linux-sandbox-worker` requires verified status/
mount configuration. See the [confinement guide](../../../docs/linux-confinement.md).
Future cross-site frames require stronger site isolation than per-tab processes.

See the [process guide](../../../docs/renderer-processes.md) and
[architecture](../../../docs/architecture/overview.md).
