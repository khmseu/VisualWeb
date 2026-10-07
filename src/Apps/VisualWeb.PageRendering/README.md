# VisualWeb.PageRendering

Shared app-layer static-page models and rendering policy for the browser's
local development mode and renderer executable. Owns explicit native font
configuration, embedded stylesheet discovery and offline Engine.Content calls.
Phase 11d adds explicit `executeInlineScripts: true` post-parse inline classics
with live DOM bindings before style/layout/paint. Default rendering remains
script-free. Publication pins every successful parsed DOM for scroll/resize; at most one
unpublished candidate is retained alongside it. Navigation/reload uses a fresh
host; lost DOM requires explicit reload rather than repeating execution.
Fonts remain creating-thread-affine; asynchronous process orchestration belongs
to the browser client, not this synchronous native policy.

`PageViewport.ScrollY` is an optional init-only CSS-pixel offset; its three-value
constructor/deconstruction are unchanged. `BrowserPage.ScrollHeight` reports the
bounded root extent (at least the viewport height). Scroll changes raster
translation only, not layout viewport geometry or frame size.

`LoadedPage` owns an immutable final response URL and document `SecurityOrigin`.
Each new construction gets a fresh opaque origin (including reused data/file URL
instances); tuple origins use the URL's scheme/host/effective port. Metadata
record copies and retained repaint keep the same origin and document ID. The
four-argument constructor and deconstruction remain supported, but `Url` and
`Origin` are get-only: replace a URL by constructing a new document, not by `with`.
The browser publishes this origin as `BrowserTab.Origin` only with a committed
document. This is identity, not inherited or sandbox origin selection,
same-origin enforcement, CORS or CSP. IPC remains
unchanged; worker reconstruction has a local identity, not a transmitted browser
opaque principal.

No networking, window backend or browser chrome dependencies. Unsupported page
features fail explicitly. See the [shell guide](../../../docs/browser-shell.md)
and [process guide](../../../docs/renderer-processes.md), plus the
[scripting guide](../../../docs/scripting.md) for exact policy and budgets.
