# VisualWeb.PageRendering

Shared app-layer static-page models and rendering policy for the browser's
local development mode and renderer executable. Owns explicit native font
configuration, embedded stylesheet discovery and offline Engine.Content calls.
Phase 11d adds explicit `executeInlineScripts: true` post-parse inline classics
with live DOM bindings before style/layout/paint. Default rendering remains
script-free. Publication pins the successful DOM for resize; at most one
unpublished candidate is retained alongside it. Navigation/reload uses a fresh
host; lost DOM requires explicit reload rather than repeating execution.
Fonts remain creating-thread-affine; asynchronous process orchestration belongs
to the browser client, not this synchronous native policy.

No networking, window backend or browser chrome dependencies. Unsupported page
features fail explicitly. See the [shell guide](../../../docs/browser-shell.md)
and [process guide](../../../docs/renderer-processes.md), plus the
[scripting guide](../../../docs/scripting.md) for exact policy and budgets.
