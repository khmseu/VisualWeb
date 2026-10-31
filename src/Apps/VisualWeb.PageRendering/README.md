# VisualWeb.PageRendering

Shared app-layer static-page models and rendering policy for the browser's
local development mode and renderer executable. Owns explicit native font
configuration, embedded and linked stylesheet discovery and offline Engine.Content calls.
`LoadedPage.Stylesheets` is a bounded immutable data collection (request URL,
final response base URL and decoded CSS) validated by the shared IPC contract.
`DiscoverStylesheets` gives the
browser the supported classic `link rel=stylesheet` URLs (resolved against the first
`<base href>`, with the final document URL as fallback) from the same parser options;
leading `@import` rules are discovered for embedded and linked sources and
`CollectStyles` expands them at their cascade positions in document order.
Optional media lists containing exact `all`, `screen`, and `print` media types are
supported for imports and stylesheet/link media attributes; lists that omit `all`
and `screen` are ignored in this screen-only renderer. Other
unsupported media queries, conditions, layers and late imports fail explicitly.
Nested CSS `@media` groups use the same screen media types and preserve cascade
order; an empty group prelude also matches. Inactive print contents are not
evaluated, and nested `@import` rules are never fetched; active nested imports
produce a CSS diagnostic.
The renderer throws for
unsupported link semantics or sources (for example, script-changed ones) whose text
was not provided.
Phase 11d adds explicit `executeInlineScripts: true` post-parse inline classics
with live DOM bindings before style/layout/paint. Default rendering remains
script-free. Publication pins every successful parsed DOM for scroll/resize; at most one
unpublished candidate is retained alongside it. Navigation/reload uses a fresh
host; lost DOM requires explicit reload rather than repeating execution.
Opted-in documents keep their renderer-local host/listeners for bounded trusted
`click`, `auxclick`, `focus` and `blur` events (at most 1024 combined events per
document). Browser focus events target existing rendered links and visible
enabled supported controls; they are plain Events with non-bubbling,
non-cancelable, composed flags, not `FocusEvent` instances. Stable opaque
per-document target IDs are data-only and are not DOM identifiers. Failures
invalidate the host, and replacement/disposal releases it. Link events remain
plain Events without PointerEvent metadata.
Fonts remain creating-thread-affine; asynchronous process orchestration belongs
to the browser client, not this synchronous native policy.
`StaticPageRenderer` accepts optional trusted bold/italic/bold-italic paths
alongside the required regular path. It registers 400 normal, 700 normal,
400 italic and 700 italic against the existing explicit generic aliases,
using the same exact bytes for native shaping and painting. Files are loaded
eagerly, owned and released on the creating thread; constructor failure releases
already loaded faces. Missing descriptors still fail; no metadata discovery,
font synthesis, fallback, downloads or broader weight matching is added.

`PageViewport.ScrollY` and `ScrollX` are optional init-only CSS-pixel offsets;
its three-value constructor/deconstruction are unchanged. `BrowserPage.ScrollHeight`
and `ScrollWidth` report bounded root extents (at least the corresponding viewport
dimension). Scroll changes raster translation only, not layout viewport geometry
or frame size. Horizontal width is measured from supported layout border boxes,
lines, text fragments and inline widgets; negative-left-origin overflow and nested
horizontal overflow are not represented.
`PageViewport.ScrollOffsets` carries bounded per-container vertical offsets for
the supported nested overflow subset. Rendering resolves definite-height block
containers with computed `overflow-x:hidden` and `overflow-y:auto`, applies
their clip/translation stacks during paint, and reports viewport-projected
`BrowserPage.ScrollContainers` metadata (`index`, `parentIndex`, `clip`,
`offset`, `maxOffset`) for browser-owned wheel consumption and edge chaining.
This is not general CSS overflow behavior or DOM scroll-event delivery.
`BrowserPage.LinkTargets` is a nonpositional read-only list, empty by default.
Rendering gathers nearest-anchor ancestry from shaped text fragments in paint
order, using run widths and line heights, and also reports non-empty measurable
rendered block geometry for anchors without shaped text, including geometry
contributed by rendered descendants. Text rectangles and block boxes are
translated/clipped to visible CSS viewport space. URLs resolve against the final
source URL before returning data-only IPC-contract targets. Each anchor is one
target containing its visible rectangles, bounded document-space X/Y offsets and
parsed `tabindex` value; groups follow renderer target order, without exposing element identity. Offscreen
anchors have an empty rectangle list until scrolled into view. Empty inline
anchors and non-text anchors without a non-empty rendered block box remain
unsupported. IPC v37 adds the vertical offset without exposing renderer DOM identity;
IPC v47 adds the horizontal offset and bounded root scroll width. IPC v48 adds
unique bounded focus-target IDs to rendered links and eligible controls and
strictly scoped focus/blur requests; no DOM handles cross the channel.
`_blank` opens a new tab; `_self`,
`_parent`, and `_top` use the current tab, including the first `<base target>`
fallback. Other ASCII names are preserved as current-window browsing-context
targets. `BrowserPage.TextTargets` is a separate bounded list of visible shaped
clusters with clipped rectangles for browser-owned selection; it exposes no
DOM identity. The shared IPC contract caps 4096 anchors, 64 rectangles per anchor,
8192 characters per URL and 1 MiB serialized metadata locally and in workers.

`BrowserPage.Forms`/`FormControls` are data-only snapshots of the simple forms
subset collected after optional script mutations: `form` action/error, and
`input` text/search/email/tel/url/password/date/time/month/week/number/range/color/checkbox/radio/hidden/submit/reset/button, `textarea`, single-select `select`, plus `button` submit/reset and inert `button type=button`
controls. Supported controls use finite inline-block layout: input/button fallback
dimensions are 160 by 20 CSS pixels; textarea defaults to 20 columns by 2 rows
with bounded cols/rows and exposes its text content as its initial value; select
controls carry bounded direct-option metadata and use 160 by 20 CSS-pixel fallback geometry. Controls
expose name, initial value, label, disabled/readonly/required/pattern/minlength/maxlength, email `multiple` state, bounded applicable placeholder text, finite number/range bounds and step metadata, parsed `tabindex`, merged
traversal position before a link group and clipped visible border box. IPC v47 carries link/control `tabindex` values and bounded root horizontal scroll
metadata as data-only fields; link targets include document-space X/Y anchor
offsets so browser focus scrolling can bring offscreen links into view. The browser
applies the bounded sequential-order subset documented in the shell guide. Bounded
data-only label-text rectangles associate explicit `for` labels or the first
supported labelable descendant with visible enabled controls; DOM identities do
not cross IPC.
Unsupported semantics (POST/dialog, multipart/text-plain,
non-UTF-8 `accept-charset`, unsupported `form=`
owners on object, submitter overrides other than `formaction`, `formtarget` and `formnovalidate`, other input types, textarea pattern,
select `size` above 12 (including `+`-prefixed sizes above 1) or a zero single-select size and unsupported select content, date/time/month/week constraints (`min`, `max`, `step`, `pattern`, length, `list`, `multiple`, `dirname`), object and datalist) become a per-form `Error`, rejected visibly only
when that form is submitted. Select metadata reports `SelectRows` as the leading non-negative integer of
`size` when it is 2..12 (single or `multiple`), matching static layout's 20px-per-row height; otherwise one row
(multiple selects without such a size stay compact popups). Supported controls resolve `form=` to the first
exact matching ID when it belongs to a form; unresolved/non-form IDs have no
owner and never fall back to an ancestor. Controls without `form=` use the
nearest ancestor form. `<output>` is form-associated but non-submittable and contributes no browser form snapshot. `<input type="button">` and `<button type="button">` are non-submittable and contribute no GET entry. Visible instances use bounded widget layout and are reported as inert `inert` controls, labelled by the input's `value` or the button's text content (empty when absent); activation never submits. A fieldset may itself carry `form=` without assigning its children to that form; script-visible fieldset ownership remains unavailable. Descendants of a disabled fieldset inherit disabled state
except descendants of its first `legend` element child; nested fieldsets apply
independently. Hyperlinks and form actions resolve against the first valid `<base href>`;
forms without an action attribute or with an empty value default to the final
document URL. Invalid/data/javascript base
URLs fall back to the document URL. Form targets `_self`, `_parent`, and `_top`
(including first `<base target>` fallback) use the current tab; `_blank` selects a
new active tab. Other ASCII names are preserved as current-window browsing
contexts. Selected button/input `formtarget` overrides the form target; an
empty override selects the current tab. Invalid non-ASCII or whitespace-bearing
targets become separate bounded target errors; an invalid submitter affects only
its own activation, and a valid override can replace an invalid form/base target.
IPC v39 carries form-level `novalidate` state alongside bounded target metadata and diagnostics.
Text/search/email/tel/url/password inputs and textarea may carry a nonempty
`Dirname` companion name, bounded to 8192 UTF-16 code units and the combined
1 MiB form metadata budget. The shell emits `ltr` immediately after a successful
control's value. Other kinds, empty names, RTL/auto directionality anywhere in
the control's ancestor chain, and bdi/bdo ancestry fail visibly rather than infer
directionality, including controls omitted from layout. No bidi or events are added.
Browser-discovered linked stylesheets also resolve against the
first `<base href>`; invalid, `data:`, or `javascript:` values fall back to the
document URL, and cross-origin requests remain blocked by the browser broker.
Forms may carry a `novalidate` flag, and selected submit buttons may carry a resolved `formaction` override or `formnovalidate` flag. Invalid override URLs become submitter-specific errors. These metadata let the browser select the action or skip supported constraint validation for that submission; they do not bypass navigation or origin policy. The renderer never submits or navigates.

`LoadedPage` owns an immutable final response URL and document `SecurityOrigin`.
Each new construction gets a fresh opaque origin (including reused data/file URL
instances); tuple origins use the URL's scheme/host/effective port. Metadata
record copies and retained repaint keep the same origin and document ID. The
optional `LoadedPage.BaseUrl` pins a browser-approved base URL across local and
process rendering when response `base-uri` policy is present; null preserves
renderer-side base discovery. Script changes to `<base href>` do not update this
bounded snapshot.
four-argument constructor and deconstruction remain supported, but `Url` and
`Origin` are get-only: replace a URL by constructing a new document, not by `with`.
The browser publishes this origin as `BrowserTab.Origin` only with a committed
document. The browser may construct a page-initiated `about:blank` with its
creator's committed origin; browser-UI blank pages remain fresh opaque origins.
Sandbox selection and other inherited-origin contexts remain unsupported. This
identity is not same-origin enforcement, CORS or CSP. IPC remains unchanged;
worker reconstruction has a local identity, not a transmitted browser opaque
principal.

No networking (linked stylesheet text is supplied by the browser), window backend or browser chrome dependencies. Unsupported page
features fail explicitly. See the [shell guide](../../../docs/browser-shell.md)
and [process guide](../../../docs/renderer-processes.md), plus the
[scripting guide](../../../docs/scripting.md) for exact policy and budgets.
