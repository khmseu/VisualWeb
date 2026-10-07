# Ipc.Contracts

Version-8 data-only hello/render/frame/error messages, confirmed sandbox
profile handshake and exact
viewport/text/frame budgets. No DOM, V8, native handles or broker capabilities.
Each private channel serves one tab with monotonically increasing request IDs.
Render requests carry nonempty document identities, publication acknowledgement,
retained-document repaint intent and explicit inline script policy. These are
data-only values; older protocol versions fail closed.
Requests include bounded finite nonnegative CSS-pixel `ScrollY`; frames include
bounded finite `ScrollHeight`. Scroll fields are restricted to their message kinds.
Frame replies also carry the exact CSS viewport/scale and required `LinkTargets`
(empty when absent from the page). Up to 4096 visible positive rectangles fit
entirely inside that viewport; absolute serialized URLs have at most 8192
characters each, and the full link JSON array has a 1 MiB UTF-8 budget.
Targets cannot appear on requests, errors or handshakes. Missing, duplicate or
unknown target fields fail closed. Scheme policy belongs to browser activation.
Frames also require `Forms` (at most 256) and `FormControls` (at most 1024)
arrays with a 1 MiB combined UTF-8 JSON budget, 8192-character strings, valid
form indices, supported kinds, nondecreasing `BeforeLink` positions and
viewport-contained rectangles (never for hidden inputs). A form either has an
absolute http/https/file/data action or an empty action with a nonempty error.

These are internal contracts, not a web standard. See the
[process guide](../../../docs/renderer-processes.md).
Render requests require a `Stylesheets` array (rejected on other kinds): at most
32 unique absolute serialized URLs (8192 characters each) with decoded CSS text of
at most 256 Ki characters each and a 1 MiB UTF-8 JSON budget for the whole array.
These are browser-fetched data; the renderer never receives fetch capability.
