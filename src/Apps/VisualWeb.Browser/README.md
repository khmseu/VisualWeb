# VisualWeb.Browser

Runnable development-only browser executable. Draws SDL
address/tab chrome, navigates GET HTML into the static engine, and manages
history, tabs and multiple windows.

Owns windows, tabs, browser chrome, privileged resource brokers and renderer
supervision. Composes the platform backend with tab-local GET loaders and
either local rendering or one worker process per tab and committed origin.
Multiprocess mode always rotates the worker transactionally for cross-origin or
new opaque top-level documents (no site/frame isolation). Worker
crashes are contained in multiprocess mode. The CLI requires
[Linux x64 confinement](../../../docs/linux-confinement.md) or
[Windows confinement](../../../docs/windows-confinement.md) by default;
unsupported targets/configurations fail closed before display/content startup.
Neither mode is production-safe browsing.

Every shell session owns one bounded in-memory HSTS store shared across all
tab-local GET loaders/windows, in local and multiprocess modes. Independent
shells are isolated; cookies remain disabled. Standalone `GetPageSource` accepts
explicit store injection. Final upgraded URLs feed document origins and
transactional swaps. See [session HSTS](../../../docs/networking.md#session-hsts)
(official source ID `rfc6797`); persistence/preload/public-suffix policy is deferred.

Requires exactly one mode and a trusted `--font` path. Recommended
`--multiprocess` and legacy `--development-multiprocess` require `--renderer`
and supported confinement. `--require-sandbox` is a redundant assertion.
Unsandboxed development requires explicit trusted-content acknowledgement
`--allow-unsandboxed-development`, including for `--development-single-process`.
It conflicts with normal `--multiprocess` and `--require-sandbox`; single-process
also rejects sandbox/renderer options. No fallback or smoke exemption exists.
Public `BrowserLaunchOptions.Parse` performs native preflight. Low-level
renderer/shell API defaults remain unchanged for library/test callers.
`--enable-inline-scripts` explicitly enables the finite post-parse inline subset
in either mode; scripts stay inert by default. See the
[process guide](../../../docs/renderer-processes.md) and
[shell guide](../../../docs/browser-shell.md) for run
commands, controls, limits and validation, and the
[architecture](../../../docs/architecture/overview.md).
