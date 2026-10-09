# VisualWeb.PageRendering

Shared app-layer static-page models and rendering policy for the browser's
local development mode and renderer executable. Owns explicit native font
configuration, embedded and linked stylesheet discovery and offline Engine.Content calls.
`LoadedPage.Stylesheets` is a bounded immutable data collection (request URL and
decoded CSS) validated by the shared IPC contract. `DiscoverStylesheets` gives the
browser the supported classic `link rel=stylesheet` URLs (resolved against the first
`<base href>`, with the final document URL as fallback) from the same parser options;
`CollectStyles` interleaves embedded and
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
identity. `_blank` opens a new tab; `_self`, `_parent`, and `_top` use the current
tab, including the first `<base target>` fallback. Unsupported named contexts
fail visibly. `BrowserPage.TextTargets` is a separate bounded list of visible shaped
text fragments with clipped rectangles for browser-owned selection; it exposes no
DOM identity. The shared IPC contract caps 4096 anchors, 64 rectangles per anchor,
8192 characters per URL and 1 MiB serialized metadata locally and in workers.

`BrowserPage.Forms`/`FormControls` are data-only snapshots of the simple forms
subset collected after optional script mutations: `form` action/error, and
`input` text/search/email/tel/url/password/date/time/month/week/number/range/checkbox/radio/hidden/submit/reset, `textarea`, single-select `select`, plus `button` submit/reset/plain-button
controls. Supported controls use finite inline-block layout: input/button fallback
dimensions are 160 by 20 CSS pixels; textarea defaults to 20 columns by 2 rows
with bounded cols/rows and exposes its text content as its initial value; select
controls carry bounded direct-option metadata and use 160 by 20 CSS-pixel fallback geometry. Controls
expose name, initial value, label, disabled/readonly/required/pattern/minlength/maxlength, email `multiple` state, bounded applicable placeholder text, and finite number/range bounds and step metadata, merged
traversal position before a link group and clipped visible border box.
Unsupported semantics (POST/dialog, multipart/text-plain,
non-UTF-8 `accept-charset`, unsupported `form=`
owners on output/object, submitter overrides other than `formaction`, `formtarget` and `formnovalidate`, other input types, textarea pattern/dirname,
listbox/multiple/optgroup select semantics, date/time/month/week constraints (`min`, `max`, `step`, `pattern`, length, `list`, `multiple`, `dirname`), output/object and datalist) become a per-form `Error`, rejected visibly only
when that form is submitted. Supported controls resolve `form=` to the first
exact matching ID when it belongs to a form; unresolved/non-form IDs have no
owner and never fall back to an ancestor. Controls without `form=` use the
nearest ancestor form. A fieldset may itself carry `form=` without assigning its children to that form; script-visible fieldset ownership remains unavailable. Descendants of a disabled fieldset inherit disabled state
except descendants of its first `legend` element child; nested fieldsets apply
independently. Hyperlinks and form actions resolve against the first valid `<base href>`;
forms without an action attribute or with an empty value default to the final
document URL. Invalid/data/javascript base
URLs fall back to the document URL. Form targets `_self`, `_parent`, and `_top`
(including first `<base target>` fallback) use the current tab; `_blank` selects a
new active tab. Selected button/input `formtarget` overrides the form target;
an empty override selects the current tab. Named/non-keyword targets become
separate bounded target errors; an invalid submitter affects only its own
activation, and a valid override can replace an invalid form/base target.
IPC v35 carries form-level `novalidate` state alongside current/new-tab flags and diagnostics, never raw context names.
Browser-discovered linked stylesheets also resolve against the
first `<base href>`; invalid, `data:`, or `javascript:` values fall back to the
document URL, and cross-origin requests remain blocked by the browser broker.
Forms may carry a `novalidate` flag, and selected submit buttons may carry a resolved `formaction` override or `formnovalidate` flag. Invalid override URLs become submitter-specific errors. These metadata let the browser select the action or skip supported constraint validation for that submission; they do not bypass navigation or origin policy. The renderer never submits or navigates.

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
