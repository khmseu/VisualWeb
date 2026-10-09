# VisualWeb.Browser

Runnable development-only browser executable. Draws SDL
address/tab chrome, navigates GET HTML into the static engine, and manages
history, tabs and multiple windows.
Visible textual HTML anchors activate through the current tab's GET navigation
on primary left click or Tab/Shift+Tab/Enter, and open in a new active tab on
middle click, using renderer-owned clipped CSS rectangle groups. Keyboard
traversal visits chrome before visible anchors;
per-tab page focus is outlined by the shell without changing the renderer raster.
The `_blank` target keyword opens an active tab in the source window. Same-document
fragment links and their Back/Forward traversal update URL-only history and scroll
to bounded rendered element IDs without a new load; named targets
and DOM input events remain unsupported. A narrow forms subset adds shell-edited text/search/email/tel/url/password/date/time/month/week/number fields (password values are masked in browser chrome), checkboxes, radio groups,
single-select controls with bounded options, Up/Down/Home/End selection, and
click/Enter/Space popup activation, and multiline textareas with Tab focus,
committed-text entry and Enter/click submission
of current/new-tab GET `application/x-www-form-urlencoded` queries through the ordinary
navigation transaction. Textareas have bounded rows/columns, soft wrapping and
independent line-window scrolling; `wrap=hard` inserts bounded line breaks only
when submitting and requires explicit supported `cols`; number fields validate finite values, optional min/max bounds and supported
step grids; email fields support sanitized comma-separated lists when `multiple`
is present, and supported text fields draw bounded placeholders without submitting them; date fields accept strict ISO Gregorian dates for years 0001–9999 as
text without a calendar picker; time fields validate bounded HTML time strings as text without a clock picker; month fields validate positive-year `YYYY-MM` values without a month picker; week fields validate ISO week-years without a picker; range fields render as shell-owned sliders adjusted by keyboard or pointer. Reset controls restore their form's initial values and checked states without navigation; forms support `novalidate`, disabled fieldsets disable descendant controls except controls within the first `legend`, and submit buttons support bounded `formaction`, `formtarget` and `formnovalidate` overrides. Form/link targets use first `<base target>` fallback; `_blank` opens a new active tab while `_self`, `_parent`, and `_top` use the current tab. Named contexts fail visibly; a selected invalid `formtarget` fails before serialization without poisoning other submitters. New-tab form policy checks run before tab creation and preserve shared HSTS, fixed-origin and downgrade restrictions. POST and other general controls remain unsupported. See [controls](../../../docs/browser-shell.md#controls).

Owns windows, tabs, browser chrome, privileged resource brokers and renderer
supervision. Composes the platform backend with tab-local GET loaders and
either local rendering or one worker process per tab and committed origin.
Multiprocess mode always rotates the worker transactionally for cross-origin or
new opaque top-level documents (no site/frame isolation). Worker
crashes are contained in multiprocess mode. The CLI requires
[Linux x64/ARM64 confinement](../../../docs/linux-confinement.md) or
[Windows confinement](../../../docs/windows-confinement.md) by default;
unsupported targets/configurations fail closed before display/content startup.
Neither mode is production-safe browsing.

Every shell session owns one bounded in-memory HSTS store shared across all
tab-local GET loaders/windows, in local and multiprocess modes. Independent
shells are isolated; cookies remain disabled. Standalone `GetPageSource` accepts
explicit store injection. Supported classic linked stylesheets are fetched by
each tab's `GetPageSource` with that same loader/store (cookies off) before
publication and sent to renderers only as bounded data; renderers never fetch.
Final upgraded URLs feed document origins and
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
