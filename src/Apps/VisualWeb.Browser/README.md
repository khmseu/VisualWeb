# VisualWeb.Browser

Runnable development-only single-process browser executable. Draws SDL
address/tab chrome, navigates GET HTML into the static engine, and manages
history, tabs and multiple windows.

Owns windows, tabs, browser chrome, privileged resource brokers and renderer
supervision in the future production architecture. Currently composes the
platform backend with tab-local loaders/native fonts, but no renderer processes
or OS sandbox: native crashes are not contained.

Requires explicit `--development-single-process` acknowledgement and trusted
`--font` path. See the [shell guide](../../../docs/browser-shell.md) for run
commands, controls, limits and validation, and the
[architecture](../../../docs/architecture/overview.md).
