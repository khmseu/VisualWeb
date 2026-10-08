# VisualWeb.Renderer

Per-tab static rendering executable. Requires `--development-unsandboxed`
and a trusted `--font` path; intended only for private browser-launched stdio IPC.

Owns native fonts and HTML/CSS/layout/paint through VisualWeb.PageRendering
and Engine.Content. No networking/window/chrome dependency; stdout is protocol
only and diagnostics go to stderr. Native work stays on its creating main thread.
IPC v5 carries bounded CSS-pixel scroll offsets/extents, exact frame CSS geometry
and visible textual-anchor rectangles with absolute URLs, and optionally enables
post-parse inline classic scripts before painting, with fresh navigation hosts
and retained committed DOM for scroll/resize in both script-free and scripted modes. Script policy
is fixed per channel; script errors are explicit page failures. No DOM/V8 objects
cross IPC and no external script is fetched. IPC v6 render requests carry the
browser-fetched linked stylesheet texts; the worker never fetches and fails a
page whose (possibly script-changed) link was not provided. IPC v8 introduced
simple form/control metadata; IPC v10 adds the generic editable `tel` kind; IPC v11 adds bounded
multiline textarea metadata, IPC v12 adds minlength, IPC v13 adds bounded pattern metadata, IPC v14 adds editable URL controls, v15 adds single-address email controls, v16 adds checkbox state, and v17 adds radio controls; the worker never edits or submits controls.
The worker never submits forms.
Normal user permissions remain in development-unsandboxed mode. The trusted
`--linux-sandbox-bootstrap` reexecutes into required Linux confinement before
receiving any page. Direct `--linux-sandbox-worker` requires verified status/
mount configuration. See the [confinement guide](../../../docs/linux-confinement.md).
Multiprocess shells start a new worker for each cross-origin or new opaque
top-level document and keep it for same-origin navigation. Future cross-site
frames require stronger site isolation than these per-tab, per-origin workers.

See the [process guide](../../../docs/renderer-processes.md) and
[architecture](../../../docs/architecture/overview.md).
