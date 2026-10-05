# VisualWeb.Browser

Runnable development-only browser executable. Draws SDL
address/tab chrome, navigates GET HTML into the static engine, and manages
history, tabs and multiple windows.

Owns windows, tabs, browser chrome, privileged resource brokers and renderer
supervision. Composes the platform backend with tab-local GET loaders and
either local rendering or one unsandboxed worker process per tab. Worker
crashes are contained in multiprocess mode; neither mode provides OS confinement.

Requires explicit `--development-single-process` or `--development-multiprocess`
acknowledgement and trusted `--font` path. Multiprocess mode also requires
`--renderer`; sandbox-required launches fail. See the
[process guide](../../../docs/renderer-processes.md) and
[shell guide](../../../docs/browser-shell.md) for run
commands, controls, limits and validation, and the
[architecture](../../../docs/architecture/overview.md).
