# VisualWeb.Browser

Runnable development-only browser executable. Draws SDL
address/tab chrome, navigates GET HTML into the static engine, and manages
history, tabs and multiple windows.
Visible textual HTML anchors and anchors whose measurable rendered block geometry
comes from the anchor itself or rendered descendants activate through the current tab's GET navigation on primary left click or
Tab/Shift+Tab/Enter, open in a new active tab on middle click, and open in a
background tab on Ctrl+primary click, using renderer-owned clipped CSS rectangle
groups. Empty inline anchors and non-text anchors without a measurable block box
remain unsupported. On opted-in pages, primary and middle-pointer activation
first dispatch trusted `click` and `auxclick` Events respectively; focused-link
Enter/Shift+Enter/Ctrl+Enter and Ctrl+primary activation dispatch trusted `click`
events. `preventDefault()` cancels the corresponding navigation or tab-opening
default. These are plain Events, not PointerEvents, and do not synthesize DOM
keyboard or modifier events. Keyboard traversal visits chrome before
supported links and visible enabled form controls, honoring the bounded
`tabindex` ordering documented in the controls guide;
visible link hover and per-tab page focus use shell-owned outlines without changing the renderer raster.
Page text selection uses bounded visible HarfBuzz clusters, with individual
stops for ordinary supported LTR characters and no estimated positions inside
multi-character ligatures or combining clusters. SDL-reported double-clicks on
non-link text select the same-line Unicode word containing those clusters;
triple-clicks select the visible visual line.
Visible explicit or implicit `<label>` text can focus or activate its associated
supported visible form control; nested links remain independently clickable and
no DOM form events are generated. The address bar and supported text-like form
fields, including textareas, support scalar-safe Shift+Arrow/Home/End range selection. Textarea vertical
selection follows measured visual lines; selected text is highlighted and
Ctrl+C copies it, while typing, paste, Backspace and Delete replace the range.
The `_blank` target keyword opens an active tab in the source window. Same-document
fragment links and their Back/Forward traversal update URL-only history and scroll
to bounded rendered element IDs or text-bearing legacy `<a name>` anchors without a new load. Other ASCII
target names are scoped to the current window and reuse a matching named tab or
create one when absent. DOM input events remain unsupported. A narrow forms subset adds shell-edited text/search/email/tel/url/password/date/time/datetime-local/month/week/number fields (password values are masked in browser chrome), checkboxes, radio groups,
single-select controls with bounded options and flattened optgroup labels, Up/Down/Home/End selection, and
click/Enter/Space popup activation, and multiline textareas with Tab focus,
committed-text entry and Enter/click submission
of current/new-tab GET `application/x-www-form-urlencoded` queries through the ordinary
navigation transaction. Supported text/search/email/tel/url/password inputs and textarea also
submit bounded nonempty `dirname` companions with value `ltr`, immediately after
each successful named value. Disabled/unnamed controls emit neither entry;
read-only controls submit both, and reset preserves the companion name.
Names use the existing newline normalization, UTF-8 serializer and submission
limits. Unsupported kinds, empty names and RTL/auto/bdi/bdo scopes fail visibly;
no bidi or DOM events are implemented. Multiple selects use browser-owned popups and an independent enabled-option
cursor: unmodified Up/Down/Home/End/PageUp/PageDown move without changing selection; Space/Enter opens,
Space toggles while keeping an open popup, and Enter/Escape dismiss without reverting toggles.
Selection is submitted in option order and reset restores initial selections/cursor. Type-ahead moves
the independent cursor without changing selection; Ctrl+A selects all enabled options without moving it.
Shift+Up/Down replaces the selection with the enabled range anchored at the cursor where Shift selection
began; Shift+Home/End extends to the first/last enabled option with the same anchor. Releasing Shift or
unmodified cursor movement resets the anchor. Other Shift/range selection remains unsupported; no DOM input events
are generated. Single- and multiple-select `size` 2..12 listboxes keep a row-bounded, shell-owned first visible option
for wheel scrolling (no page scroll over the listbox), offset pointer targets and keyboard auto-visibility.
Multiple listboxes never open a popup: clicks toggle enabled rows, keys move an outlined cursor, Space toggles it
and Enter is consumed; multiple selects without an explicit `size` 2..12 keep the popup.
See the controls guide for exact bounds and page-key behavior.
Textareas have bounded rows/columns, soft wrapping and
independent line-window scrolling; `wrap=hard` inserts bounded line breaks only
when submitting and requires explicit supported `cols`; number fields validate finite values, optional min/max bounds and supported
step grids; email fields support sanitized comma-separated lists when `multiple`
is present, and supported text fields draw bounded placeholders without submitting them; date fields accept strict ISO Gregorian dates for years 0001–9999 as
text without a calendar picker; time fields validate bounded HTML time strings as text without a clock picker; datetime-local fields combine bounded Gregorian dates (0001–9999) and times without a timezone, normalize initial/submitted values, and enforce required/min/max and bounded millisecond step grids (default 60 seconds), without a picker; month fields validate positive-year `YYYY-MM` values without a month picker; week fields validate ISO week-years without a picker; range fields render as shell-owned sliders adjusted by keyboard or pointer. Reset controls restore their form's initial values and checked states without navigation; forms support `novalidate`, disabled fieldsets disable descendant controls except controls within the first `legend`, and submit buttons support bounded `formaction`, `formtarget` and `formnovalidate` overrides. Form/link targets use first `<base target>` fallback; `_blank` opens a new active tab while `_self`, `_parent`, and `_top` use the current tab. Other ASCII names scope to the current window and reuse/create named tabs. A selected invalid non-ASCII or whitespace-bearing `formtarget` fails before serialization without poisoning other submitters. New-tab form policy checks run before tab creation and preserve shared HSTS, fixed-origin and downgrade restrictions. POST and other general controls remain unsupported. See [controls](../../../docs/browser-shell.md#controls).
Response `form-action` CSP source lists are also checked before bounded GET
form values are serialized; this does not restrict links or address-bar
navigation and does not use `default-src` as a fallback.
Every HTTP top-level navigation request and every HTTPS top-level navigation
request sends `Upgrade-Insecure-Requests: 1`, including redirect hops. HTTPS
requests signal on all hosts without consulting a preload list; subresources do
not carry the header. It does not itself upgrade a URL.

When the response also includes `upgrade-insecure-requests`, shell-managed GET
form actions and same-origin page-initiated HTTP links, including redirects,
are upgraded before applicable policy, authorization and transport checks;
explicit HTTP port 80 maps to HTTPS's default port 443. Cross-origin link
navigation is not upgraded (forms are the exception). Explicit address-bar
navigation does not inherit the page's upgrade policy.
The bounded response `base-uri` subset checks the first usable `<base href>`
without `default-src` fallback. A blocked base uses the final document URL for
browser stylesheet/import discovery and renderer link/form resolution; the
effective base snapshot crosses IPC as data only.
Supported CSP source lists include the bare `*` source (HTTP(S) plus the
protected document's scheme), HTTP(S) DNS hosts, and wildcard subdomain hosts
such as `https://*.example.test`; wildcard subdomain sources do not match the
apex. Host sources support omitted, numeric, or `*` port parts. A missing port
matches only the scheme's default; paths support exact matching and trailing-
slash directory prefixes over percent-decoded segments. Paths on IP-literal
sources remain unsupported.

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

The development shell asks before page-initiated links/forms or typed HTTP(S)
address-bar navigation reach another origin from the committed document. When
there is no committed document, each typed HTTP(S) address-bar destination is
confirmed before transport. Origin-changing redirects are confirmed hop by hop,
while same-origin hops do not prompt after the initial destination is approved.
The confirmation shows only the destination origin and blocks transport until
Allow/Cancel, Enter, or Escape. Automatically started shell HTTP(S) loads,
including launch URLs opened through `OpenWindow`, use the same initial
confirmation before transport. Origin-changing redirects still prompt
separately. The built-in new-tab welcome page is a `data:` URL and does not
prompt. Explicit top-level `data:` loads from the address bar,
`OpenWindow(data:)`, and cross-document Back/Forward/Reload history also
require confirmation; the prompt identifies a data URL without showing its
payload. The built-in welcome page remains exempt, including when revisited
from history. Top-level `file:` loads from the address bar, `OpenWindow(file:)`,
and file Back/Forward/Reload history actions also require confirmation before
reading the file; the prompt shows the canonical file URL path without query
or fragment. Same-document fragment traversal does not reload or prompt, and
page-initiated file links/forms remain blocked. Other non-HTTP(S) shell
navigation and direct `BrowserController.Navigate` calls remain unchanged.
This is not production request authorization or filesystem isolation.
HTTP(S) Back/Forward/Reload operations also prompt for cross-origin history
targets and origin-changing redirects. HTTPS documents block HTTP history
targets and downgrade redirects before transport; same-origin history targets
do not prompt. Direct `BrowserController.Back`, `Forward`, and `Reload` calls
keep their existing behavior; the shell uses confirmation-aware entry points.

Every shell session owns one bounded in-memory HSTS store shared across all
tab-local GET loaders/windows, in local and multiprocess modes. Independent
shells are isolated; cookies remain disabled. Standalone `GetPageSource` accepts
explicit store injection. Supported classic linked stylesheets and recursive
unconditional `@import` sources are fetched by each tab's `GetPageSource` with
that same loader/store (cookies off) before publication and sent to renderers
only as bounded data; renderers never fetch. Conditional/layer imports fail visibly.
Final upgraded URLs feed document origins and
transactional swaps. See [session HSTS](../../../docs/networking.md#session-hsts)
(official source ID `rfc6797`); persistence and preload policy remain deferred.

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
