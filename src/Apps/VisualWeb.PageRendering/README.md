# VisualWeb.PageRendering

Shared app-layer static-page models and rendering policy for the browser's
local development mode and renderer executable. Owns explicit native font
configuration, embedded and linked stylesheet discovery and offline Engine.Content calls.
`LoadedPage.Stylesheets` is a bounded immutable data collection (request URL and
decoded CSS) validated by the shared IPC contract. `DiscoverStylesheets` gives the
browser the supported classic `link rel=stylesheet` URLs (resolved against the final
document URL) from the same parser options; `CollectStyles` interleaves embedded and
provided linked sources in document order and throws for unsupported link semantics
or links (for example, script-changed ones) whose text was not provided.
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
`BrowserPage.LinkTargets` is a nonpositional read-only list, empty by default.
Rendering gathers nearest-anchor ancestry from shaped text fragments in paint
order, using run widths and line heights, then translates/clips to visible CSS
viewport space. URLs resolve against the final source URL before returning
data-only IPC-contract targets. Each anchor is one target containing all its
visible rectangles; groups follow first-fragment order, without exposing element
identity. `BrowserPage.TextTargets` is a separate bounded list of visible shaped
text fragments with clipped rectangles for browser-owned selection; it exposes no
DOM identity. The shared v21 contract caps 4096 anchors, 64 rectangles per anchor,
8192 characters per URL and 1 MiB serialized metadata locally and in workers.

`BrowserPage.Forms`/`FormControls` are data-only snapshots of the simple forms
subset collected after optional script mutations: `form` action/error, and
`input` text/search/email/tel/url/password/date/time/number/range/checkbox/radio/hidden/submit/reset, `textarea`, single-select `select`, plus `button` submit/reset/plain-button
controls. Supported controls use finite inline-block layout: input/button fallback
dimensions are 160 by 20 CSS pixels; textarea defaults to 20 columns by 2 rows
with bounded cols/rows and exposes its text content as its initial value; select
controls carry bounded direct-option metadata and use 160 by 20 CSS-pixel fallback geometry. Controls
expose name, initial value, label, disabled/readonly/required/pattern/minlength/maxlength and finite number/range bounds and step metadata, merged
traversal position before a link group and clipped visible border box.
Unsupported semantics (POST/dialog, multipart/text-plain, non-self targets,
`novalidate`, non-UTF-8 `accept-charset`, `<base>`, `form=` owners, submitter
overrides, other input types, textarea pattern/dirname/wrap=hard,
listbox/multiple/optgroup select semantics, date/time constraints (`min`, `max`, `step`, `pattern`, length, `list`, `multiple`, `dirname`), output/object, datalist and disabled fieldsets) become a per-form `Error`, rejected visibly only
when that form is submitted. The renderer never submits or navigates.

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

No networking (linked stylesheet text is supplied by the browser), window backend or browser chrome dependencies. Unsupported page
features fail explicitly. See the [shell guide](../../../docs/browser-shell.md)
and [process guide](../../../docs/renderer-processes.md), plus the
[scripting guide](../../../docs/scripting.md) for exact policy and budgets.
