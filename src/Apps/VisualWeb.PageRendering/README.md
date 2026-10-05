# VisualWeb.PageRendering

Shared app-layer static-page models and rendering policy for the browser's
local development mode and renderer executable. Owns explicit native font
configuration, embedded stylesheet discovery and offline Engine.Content calls.
Fonts remain creating-thread-affine; asynchronous process orchestration belongs
to the browser client, not this synchronous native policy.

No networking, window backend or browser chrome dependencies. Unsupported page
features fail explicitly. See the [shell guide](../../../docs/browser-shell.md)
and [process guide](../../../docs/renderer-processes.md).
