# Development browser shell

The approved implementation uses existing SDL portable windows/input/pixels
instead of a second UI toolkit. It supplies drawn address/tab chrome, bounded
session history, GET navigation, tab create/close/move and multiple windows.
**It is development tooling for trusted content,
not a secure or standards-complete production browser.**
Phase 9 supplies local rendering; phase 10a adds explicitly selected per-tab
worker processes. See the [process guide](renderer-processes.md) for launch,
wire limits, deadlines, crash/restart behavior and unfinished confinement.
Phase 10b adds [Linux confinement, now extended to ARM64](linux-confinement.md) and
[Windows confinement](windows-confinement.md). Recommended CLI `--multiprocess`
and legacy `--development-multiprocess` require confinement by default, before
display/content startup. `--require-sandbox` is a redundant explicit assertion.
Unsandboxed modes require trusted-content acknowledgement
`--allow-unsandboxed-development`, rejected with normal `--multiprocess` or
`--require-sandbox`. Unsupported OS versions/configurations fail closed, without fallback.
Low-level shell/renderer API defaults remain unchanged for library/test callers.
Required Linux confinement includes phase-10c hard per-worker cgroup resource
limits; unavailable user-manager/controller support also fails closed.

Each shell owns one bounded, thread-safe, memory-only
[HSTS policy store](networking.md#session-hsts), shared across tab-local
`GetPageSource` loaders in all modes and windows. HTTPS STS learning upgrades
future HTTP navigation and redirects before destination checks. Per-tab cookies
stay off; independent shells share no policies. Final upgraded URLs determine
committed document origins and transactional renderer rotation, without changing
an existing document on failed navigation. No persistence, preload, public-suffix
policy or general SOP/CSP enforcement is implied.

## Run it

For the recommended confined mode, see the [process launch command](renderer-processes.md#launch).
For local development with only the trusted offline welcome page, from the
repository root using an explicitly chosen trusted regular font:

```sh
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --development-single-process --allow-unsandboxed-development \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

The bundled [OFL font](../tests/Engine.Text.Tests/Data/README.md) is a pinned
development/test fixture, not automatic production font discovery. A supplied
font must contain the ASCII chrome glyphs and supported page text. There is no
fallback, synthetic bold/italic or nearest-weight matching.
The single supplied face is explicitly mapped to the generic serif/sans-serif/
monospace aliases; this is development caller configuration, not font metadata
matching or a guarantee that the supplied face has all those characteristics.

The explicit mode, development opt-out where applicable, and font path are mandatory. `--help`
describes options. `--url ABSOLUTE_URL` replaces the offline welcome page;
enter absolute `http:`, `https:`, local `file:` or HTML `data:` URLs. No search,
scheme guessing or implicit filesystem-path navigation is performed.

Linux auto-selects Wayland/X11 through the existing platform composition root.
`--backend x11`, `--backend wayland` or `--backend windows` requests an exact
backend; failures and fallback diagnostics are logged, never silently hidden.
The mode warning also remains drawn above the tab strip, identifying
NO SANDBOX, LINUX CONFINEMENT REQUIRED or WINDOWS APP CONTAINER REQUIRED.
The native window title retains the same warning alongside the active page title.

## Controls

| Control                                                                                                                                    | Behavior                                                                                                                                                                                                                                                                                                 |
| ------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Address bar / Ctrl+L                                                                                                                       | Focus address and select all                                                                                                                                                                                                                                                                             |
| Committed text / Ctrl+A                                                                                                                    | Insert text / select whole address                                                                                                                                                                                                                                                                       |
| Left/Right, Home/End, Backspace/Delete                                                                                                     | Move caret or edit Unicode scalar boundaries                                                                                                                                                                                                                                                             |
| Enter / Escape                                                                                                                             | Navigate / restore committed URL and leave address editing                                                                                                                                                                                                                                               |
| B / Alt+Left                                                                                                                               | Back                                                                                                                                                                                                                                                                                                     |
| F / Alt+Right                                                                                                                              | Forward                                                                                                                                                                                                                                                                                                  |
| R / F5 / Ctrl+R                                                                                                                            | Reload committed URL                                                                                                                                                                                                                                                                                     |
| +T / Ctrl+T                                                                                                                                | New tab with offline welcome page                                                                                                                                                                                                                                                                        |
| Tab label                                                                                                                                  | Activate that tab; middle-click closes it without activating it first                                                                                                                                                                                                                                    |
| Tab x / Ctrl+W                                                                                                                             | Close tab; closing the active tab selects the next tab in its former position, or the previous tab when closing the last tab; the window closes when its only tab is closed                                                                                                                              |
| Tab arrows / Ctrl+Tab / Ctrl+Shift+Tab / Ctrl+PageUp/PageDown                                                                              | Cycle next/previous tab                                                                                                                                                                                                                                                                                  |
| +W / Ctrl+N                                                                                                                                | New window with welcome tab                                                                                                                                                                                                                                                                              |
| M / Ctrl+M                                                                                                                                 | Move active tab to the first other window, or create an empty destination                                                                                                                                                                                                                                |
| Native window close                                                                                                                        | Close that window and only its tabs                                                                                                                                                                                                                                                                      |
| Pointer hover on visible anchor text                                                                                                       | Draw a shell-owned outline around its visible text rectangles without modifying the page raster                                                                                                                                                                                                          |
| Primary click on visible anchor text                                                                                                       | Navigate on release only if it remains on the same anchor; dragging at least four logical pixels instead selects coarse text fragments without navigating                                                                                                                                                |
| Middle click on visible anchor text                                                                                                        | Open its resolved href in a new active tab, regardless of the link target                                                                                                                                                                                                                                |
| Ctrl+primary click on visible anchor text                                                                                                  | Open its resolved href in a background tab, regardless of the link target                                                                                                                                                                                                                                |
| Tab / Shift+Tab                                                                                                                            | Traverse drawn chrome controls, then rendered textual anchors, forwards/backwards; focused offscreen anchors scroll into view                                                                                                                                                                            |
| Enter outside address editing                                                                                                              | Activate the focused chrome control or textual anchor; Shift+Enter opens a focused anchor in a new active tab; Ctrl+Enter opens it in a background tab                                                                                                                                                   |
| Arrow Up/Down, Page Up/Down, Home/End outside editable fields; Ctrl+Home/End outside address editing and form controls                     | Scroll the active page vertically                                                                                                                                                                                                                                                                        |
| Vertical page scrollbar                                                                                                                    | Click or drag the browser-owned thumb to scroll the active page                                                                                                                                                                                                                                          |
| Single-select control                                                                                                                      | Click, Enter, or Space to open; arrows/Home/End, Page Up/Down, and committed-text prefix search select enabled options; optgroup labels prefix flattened options and disabled groups disable their options; hovered popup rows are highlighted; wheel scrolls long lists; Escape/outside click dismisses |
| Tab / Shift+Tab with simple forms                                                                                                          | Visible enabled form controls join page traversal in tree order between anchor groups                                                                                                                                                                                                                    |
| Committed text, Left/Right, Backspace/Delete in a focused text/search/email/tel/url/password/date/time/month/week/number field or textarea | Edit the shell-owned field value (no page scrolling)                                                                                                                                                                                                                                                     |
| Home/End in a focused text/search/email/tel/url/password/date/time/month/week/number field                                                 | Move the caret to the start/end of the value                                                                                                                                                                                                                                                             |
| Home/End in a focused textarea                                                                                                             | Move the caret to the start/end of the current line                                                                                                                                                                                                                                                      |
| Up/Down in a focused textarea                                                                                                              | Move the caret between visible wrapped lines, preserving its preferred column                                                                                                                                                                                                                            |
| Space in a focused checkbox toggles it; Enter or Space on a focused submit control; primary click on a submit control                      | Submit the [simple GET form](#simple-get-forms); Enter in a textarea inserts a line                                                                                                                                                                                                                      |
| Enter or Space, or primary click on a focused/visible reset control                                                                        | Restore the owning form's initial values and checked states without navigation or submission validation                                                                                                                                                                                                  |
| Space on a focused inert button                                                                                                            | No action; inert buttons never submit or navigate                                                                                                                                                                                                                                                        |
| Primary left click on non-link page text                                                                                                   | Begin a coarse text selection and give the page keyboard focus                                                                                                                                                                                                                                           |
| Drag across visible non-link text                                                                                                          | Select whole shaped text fragments; selection is highlighted by browser-owned pixels                                                                                                                                                                                                                     |
| Shift+primary click with an existing selection; Shift+arrows and Shift+Home/End with page focus                                            | Extend the coarse fragment selection, including link text without activating it                                                                                                                                                                                                                          |
| Ctrl+A with page focus outside editable controls                                                                                           | Select all currently visible shaped text fragments                                                                                                                                                                                                                                                       |
| Ctrl+C with page text selected                                                                                                             | Copy selected fragment text to the system clipboard                                                                                                                                                                                                                                                      |
| Escape with page text selected                                                                                                             | Clear the browser-owned selection without changing the clipboard                                                                                                                                                                                                                                         |
| Ctrl+V while editing the address or a text/search/email/tel/url/password/date/time/month/week/number/textarea field                        | Paste clipboard text at the caret or over the current field selection                                                                                                                                                                                                                                    |

The visible tab strip follows the active tab when tabs exceed available slots.
Page text hit geometry is a bounded v9 data-only snapshot of shaped fragments
(up to 32,768 entries and 1 MiB of JSON); no DOM nodes cross the renderer boundary.
Selection uses whole-fragment rectangles, with line breaks inserted on visible line
changes when copied. This is not character-level selection or browser text shaping.
Browser windows and tab IDs are distinct; tab identity/history/content survives
moving. Text input activation uses SDL committed text. Page selection is limited
to whole visible shaped fragments (not character offsets). A primary click on a link
navigates on release only if the pointer remains over the same anchor; dragging
at least four logical pixels from link text selects fragments instead. Drags beginning in ordinary text can also include anchor fragments.
Shift+primary click extends an existing selection to a visible text fragment,
including text inside a link without navigating. Shift+Left/Right moves the active
selection endpoint by one paint-order fragment; Shift+Up/Down chooses the nearest
fragment on the adjacent visual line; Shift+Home/End selects to the current visual
line boundary. These shortcuts are inactive while a form control owns focus. Selection is cleared on successful
document, scroll or resize publications and on other page clicks before a new
interaction begins.
Ctrl+C copies selected fragment text. Text/search fields support Ctrl+A
select-all and Ctrl+V paste; address editing supports Ctrl+V over its existing
Ctrl+L selection. IME preedit and richer accessibility remain deferred.
Keyboard traversal starts with enabled, drawn chrome targets in drawing order:
tab arrows, visible tab labels/close buttons, navigation/window controls, then
the address editor, followed by rendered textual anchors in first-fragment paint
order. Ctrl+L still selects the address; Tab leaves editing for the first rendered
anchor, while Shift+Tab returns to the preceding chrome control. At the first
page anchor Shift+Tab returns to the address (or last drawn chrome control);
at the last anchor Tab wraps to the first chrome control. Disabled/undrawn controls
are skipped. Focusing an offscreen text anchor scrolls it into view; empty and
non-text anchors remain unsupported. With no rendered text links, traversal wraps
among chrome controls. Ctrl+Tab/Ctrl+Shift+Tab and Ctrl+PageUp/PageDown retain tab switching,
and editing, navigation and scrolling shortcuts retain their existing meanings.

Page keyboard ownership and selected anchor are per-tab and survive switching
or moving tabs. Clicking chrome gives it keyboard ownership; a primary link click
navigates on pointer release, while visible link hover gets a separate browser-owned
outline. A subtle shell-owned blue-gray border outlines **all**
visible rectangles of the selected anchor, including wrapped/nested text; one
anchor is one stop even if it has many fragments, while identical URLs on different
anchors remain separate stops. Without page focus or an active text selection,
page pixels are copied unchanged; the renderer raster is never modified by either
overlay. Successful document commits and resize repaints clear the selected anchor
because groups are frame-local; retained scroll repaints preserve target ordering
and focus. Failed navigation keeps the old
page and focus; closing a tab discards its focus. Enter performs only ordinary
current-tab link navigation, with existing unsupported-scheme errors; it does not
dispatch DOM keyboard/mouse events, run JavaScript URLs or synthesize native focus.
Chrome labels are bounded ASCII displays (unsupported characters become `?`);
the underlying URL and page title are not modified by that display conversion.
Long labels/statuses are clipped; full navigation errors are also written to
stderr. The native window title is bounded to 120 Unicode scalars.

## Basic vertical scrolling

Wheel amounts use positive Y for scrolling down (48 CSS pixels per line, capped
at 100 lines per event). Wheel input targets only the active tab, is suppressed
while editing the address, and is ignored over chrome when the last logical
pointer position is known. Wheel events themselves carry no pointer coordinates.
Page Up/Down move one viewport; Home/End select the document top/bottom outside
address editing, and Ctrl+Home/End do the same outside address editing and all
focused form controls. Horizontal scrolling, painted scrollbars and general DOM input/default
actions are not implemented.

Each tab's controller content owns its scroll position, including across tab
moves. Positions clamp to the rendered root document border-box bottom minus
the current CSS viewport height. No-overflow pages stay at zero. Resize retains
the document and preserves/clamps the offset; only successful new-document
publication resets it. Failed navigation keeps the previous frame and position.
Scrolling is inactive before a document commits and while navigation is loading.
Failed scroll repaints report an error and retain the last published frame/offset.

Both local and process renderers repaint the retained parsed DOM (including
script mutations), with no fetch, parse or script reexecution. Scroll translation
is applied only during rasterization under the same viewport clip; CSS viewport
width/height, layout coordinates and opaque BGRA frame dimensions are unchanged.
Root extent and vertical paint coordinates are limited to 10,000,000 CSS pixels;
this is document scrolling, not general CSS overflow or nested scrolling.
Newer scroll/resize requests cancel older publications. In-flight retained
process exchanges drain under their existing deadline to keep the DOM/channel
alive; stale frames cannot publish. New-document candidates still commit
transactionally, and viewport changes during navigation repaint that candidate
before publication without rerunning scripts.

## Textual link activation

Both rendering modes collect visible rectangles from shaped text fragments,
using the nearest HTML `a` ancestor with `href`, after optional script mutations.
Each rectangle uses the run's width and line's height, clipped to the CSS viewport
after the clamped scroll translation. Retained resize/scroll recomputes targets
without rerunning scripts. Each anchor groups all its visible rectangles, in
first-fragment paint order; the last matching group wins hit-testing. Empty anchors
and non-link text have no targets.

The renderer resolves destinations with Core.Url against the final `LoadedPage.Url`
before returning data-only rectangles and absolute URLs; it never navigates or
fetches. Primary left-button presses in the page region subtract the 120-logical-
pixel chrome height and activate on release only when the pointer remains over
the same visible anchor. Releasing outside it or over another anchor cancels.
Middle-button presses open only visible anchor targets in a
new active tab; they do not activate form controls. Both actions use the same
browser-owned destination checks and navigation transaction. Ctrl+primary click
opens the link in a background tab without activating page controls or chrome.
SDL window-local logical coordinates already match CSS pixels: pixel density
scales the raster, not pointer coordinates. Chrome,
other buttons, releases and misses do not navigate. A frame whose viewport/scale
does not match the current window (for example, during asynchronous resize)
cannot activate links.

Activation uses the current tab's ordinary `Navigate` transaction and shared
session HSTS loader. Cross-document page-initiated destinations are limited
to HTTP(S): `file:` and `data:` links fail visibly before loading; enter those
URLs explicitly in the address bar. Same-document fragment links are handled
locally, including fragments in opaque `data:` documents. HTTPS and opaque `data:` documents also block HTTP link destinations
and HTTP redirect hops unless session HSTS upgrades them before transport. Other
schemes, including `javascript:`, fail visibly. URL/count/
metadata limits and malformed hrefs fail visibly during rendering. Failed
navigation keeps the old frame, link targets, scroll position, origin and history.
Relative and fragment URL components are preserved. Same-document fragment
links update the URL-only history entry and scroll the retained document to a
rendered element with the matching decoded `id`, or to rendered text inside an
`a[name]` legacy anchor when no ID matches; an empty fragment scrolls to the top.
ID matches take precedence over named anchors. This does not fetch, reparse or
rerun scripts. Unknown IDs and empty/unrendered named anchors do not scroll.
Back/Forward between fragment entries for the
retained document updates the URL and restores fragment scrolling without a load;
cross-document traversal still reloads.
The hyperlink `target="_blank"` keyword opens a new active tab in the source
window for pointer or focused-Enter activation; source-document scheme and HTTPS
downgrade checks still apply to the navigation. `_self`, `_parent`, and `_top`
resolve to the current tab because nested browsing contexts are absent. The first
`<base target>` supplies the fallback for links without their own target, for
these same keywords. Named browsing contexts are rejected visibly rather than
silently navigated in the current tab. The first `<base href>` resolves hyperlink
URLs; an invalid, `data:`, or `javascript:` base URL falls back to the final
response URL. Form actions and browser-discovered linked stylesheets use the same
first-base resolution and fallback. `tabindex`, DOM focus APIs, downloads, image/area
links, CSS link decoration, page mouse/keyboard events and JavaScript default-action
cancellation remain deferred. Keyboard traversal can reveal text-bearing rendered
anchors; it does not add hit-testing for empty or non-text anchors. This is a bounded subset of HTML
[following hyperlinks](https://html.spec.whatwg.org/multipage/links.html#following-hyperlinks)
(cached standard ID `html`), not a full DOM event/default-action implementation.

## Simple GET forms

This is a deliberately narrow subset of HTML
[form submission](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm)
(cached standard ID `html`); general forms remain unsupported. The renderer
reports data-only form/control snapshots (IPC v36); the browser owns all values,
focus, carets and submission. Supported controls are `input` text/search/email/tel/url/password/date/time/month/week/number/range, checkbox, radio, textarea,
hidden, submit and reset, single-select `select`, plus `button` submit/reset and inert `button type=button`, plus visible `input type=button` as shell-owned inert buttons.
Text fields use generic editing and draw applicable placeholders in muted chrome only while the shell-owned value is empty; input placeholders strip line breaks and never become submitted values. Password values are shell-masked while editing and are never drawn as plaintext; tel has no telephone-specific keyboard or validation;
email supports one valid address or, with `multiple`, a comma-separated list of
valid addresses; list tokens are ASCII-whitespace-trimmed before validation and
GET submission. URL requires a valid absolute URL on submission; date values use
strict `YYYY-MM-DD` Gregorian syntax
for years 0001–9999 and are edited as text (there is no calendar picker); invalid
initial date values become empty, required dates must be nonempty, and nonempty
values are checked before submission. Valid date `min` and `max` bounds are enforced
before submission; valid numeric date `step` values enforce a bounded day grid, while `step=any`
disables it. Date `pattern`, length, `list`,
`multiple` and `dirname` attributes are unsupported and mark the form with a visible
error. Time values use strict HTML `HH:mm`, `HH:mm:ss`, or
`HH:mm:ss.fraction` syntax (one to three fractional digits, no leap seconds or
zone); invalid initial values become empty and nonempty values are validated
before submission. Valid time `min` and `max` bounds are enforced before submission;
invalid bounds are ignored. Valid numeric time `step` values enforce a bounded
millisecond grid; `step=any` disables it. Time `pattern`, length, `list`,
`multiple` and `dirname` are unsupported and mark the form with a visible error.
Time fields are edited as text, without a clock picker. Month values use a
positive year with at least four ASCII digits and a two-digit month from `01`
through `12`; invalid initial values become empty and nonempty values are
validated before submission. Valid month `min` and `max` bounds are enforced
before submission; invalid bounds are ignored. Month values are edited as text without a month
picker. Month `step`, `pattern`, length, `list`, `multiple` and
`dirname` are unsupported and mark the form with a visible error. Week values use
strict `YYYY-Www` ISO week syntax; positive years need at least four ASCII digits,
and week 53 is accepted only in 53-week week-years. Values are edited as text
without a week picker; invalid initial values become empty and nonempty values
are validated before submission. Valid week `min` and `max` bounds are enforced
before submission; invalid bounds are ignored. Week `step`, `pattern`, length,
`list`, `multiple` and `dirname` are unsupported and mark the form with a visible
error. Number accepts finite HTML floating-point values with optional
min/max and step-grid validation. `step=any` disables step checks, while
absent or invalid step values use the default step of 1. The step base is a valid
`min`, otherwise the initial value when valid, otherwise zero. Step arithmetic uses
bounded binary64 precision and fails closed when the value-to-step ratio exceeds its
reliable range. Range controls default to 0–100 with step 1, sanitize initial values to their
range/grid, and use click/drag plus arrow-key adjustment with Home/End for endpoints. Checkboxes and radios are shell-drawn; checkboxes toggle
on click or Space, while radios select on click or Space. Unchecked checkboxes and radios are omitted from submission, checked controls submit
their value (default `on`), and required checkboxes must be checked while required
radio groups need one selected member. Required single-selects need a nonempty enabled selection.
Single-select controls carry bounded direct options, choose the selected or first enabled option by default,
skip disabled options during Up/Down and Home/End selection, and submit only an enabled selected option.
Committed text performs a case-insensitive, 32-scalar-bounded prefix search; repeated single-character input
cycles through matching options, and the search prefix expires after one second. A primary click, Enter or Space
opens a browser-owned popup with up to 12 visible rows; arrow/Home/End and Page Up/Down keys continue to select
enabled options (page keys move by the visible row count and skip disabled options), wheel input scrolls longer
option lists, clicking a disabled row leaves the popup open without changing selection, and Escape or an outside click dismisses the popup.
Optgroup labels are prefixed to their flattened options, and disabled groups disable their options.
Listboxes and multiple selection remain unsupported. Radios sharing a form and nonempty name are
mutually exclusive; Space selects the focused member and arrow keys move/select
within its enabled group. Input/button/select controls default to 160 by 20 CSS pixels. Textareas
default to 20 columns by 2 rows (160 by 40 pixels); `cols` and `rows` are bounded
to 128 and 64. Author CSS may size the controls within the layout limits; author
`display:none` still hides them. Textarea values are drawn by the shell overlay
with soft wrapping measured using the shell font and the available control width.
The visible line window follows the caret across explicit and wrapped lines;
pointer clicks place the caret on the clicked measured visual line. Up/Down and
Home/End operate on those visual lines, and wheel input over a textarea scrolls
its line window independently of the page. Form-control inline
boxes wrap atomically with text; nonzero control margins and general inline-block/
replaced-element layout remain unsupported.

Fields, single-select controls and submit/reset/input buttons are drawn by a shell-owned overlay (white field or
gray button, gray border, bounded ASCII text with `|` caret when focused),
including button text; the renderer raster is untouched outside control
rectangles. These defaults are bounded layout accommodations, not complete UA
widget sizing or styling. Tab/Shift+Tab visit visible, enabled controls merged in
tree order with anchor groups; primary-clicking a visible text/search/email/tel/url/password/date/time/month/week/number field or textarea
focuses it without starting page text selection; clicking within a supported
single-line text field places its caret at the nearest shell-font glyph boundary
(the shell font is used for the overlay, not the page font). Clicking a single-select
opens its bounded shell popup, with wheel scrolling and pointer selection of enabled
options. Escape or an outside click closes the popup. Range controls can be adjusted by
pointer click/drag. Focusing an editable
control enables SDL committed text input, disabled again when focus leaves.
Textareas store LF internally: Enter inserts a line break, and pasted CR/LF
sequences normalize to LF; other control characters are rejected. Ctrl+A selects
the entire value and Ctrl+V pastes at the caret or over that selection. Left/Right caret
movement is linear; Up/Down moves between measured visual textarea lines
while preserving the preferred column. Wrapping uses the shell's configured font,
not page font styles, and unsupported non-ASCII text is displayed as `?`. Wheel
scrolling is bounded to the available visual lines. Values are capped at 8192 UTF-16 code
units and truncated to `maxlength` without splitting surrogate pairs. A nonempty
value edited below `minlength` blocks submission; an untouched initial value does
not trigger too-short validation.

Enter in a focused reset control restores each control owned by that form to its
renderer-reported initial `Value` and `Checked` state, clearing shell edits,
dirty/check overrides and textarea view state while preserving focus. It does
not navigate or run submission validation. Reset activation is limited to the
focused reset control (Enter) or primary pointer click; it is not a default
submitter for implicit Enter in a text field.

Enter in a focused text/search/email/tel/url/password/date/time/month/week/number field submits through the form's first submit
button (a disabled default button does nothing) or, without one, only when the
form has at most one text/search/email/tel/url/password/date/time/month/week/number field (implicit submission). Enter in a
textarea inserts LF and never triggers implicit submission. Form state is tab-local
and survives tab switches and same-document scroll/resize repaints whose control
metadata is unchanged; new document commits reset it, failed navigations keep it.

Submission includes named, non-disabled text/search/email/tel/url/password/date/time/month/week/number/range/checkbox/radio/select/textarea/hidden controls
plus the named activating submitter, in tree order. `_charset_` hidden fields
submit `UTF-8`; names/values normalize newlines to CRLF. Encoding is UTF-8
`application/x-www-form-urlencoded` (alphanumerics and `*-._` kept, space as `+`,
everything else `%XX`, lone surrogates as U+FFFD), replacing the action's query
and keeping its fragment. The result uses the ordinary current/new-tab navigation
transaction (HSTS, redirects, origin rotation, history). Page-initiated `file:`
and `data:` actions are rejected by the navigation broker. The query is capped
at 8192 characters and the resulting URL at the existing URL limit.

HTTP(S) forms in HTTP(S) documents may submit only to the document's same origin;
cross-origin actions fail before form values are serialized or a request is
started, and redirects are checked against that original document origin before
the redirected request is sent. HTTPS-to-HTTP actions for the same host and
effective port are allowed to reach the existing HSTS/downgrade policy, so HSTS
may upgrade them and an unupgraded downgrade remains blocked. Opaque `data:`
documents cannot submit to HTTP(S) destinations. Custom `IPageSource`
implementations must explicitly support HTTPS downgrade protection for secure
page links and fixed-origin redirects for constrained form navigation; the
compatibility defaults fail visibly instead of silently using an unrestricted
source. These are stricter browser-broker rules for this form subset and link
handling, not general same-origin enforcement, CORS, or request authorization.

A nonempty form `action` resolves against the first `<base href>`; a missing or
empty action defaults to the final document URL. A selected submit button's
`formaction` overrides the form action and resolves against the same base URL;
an empty override uses the final document URL. Invalid or unsupported overrides
fail visibly only when that submitter is activated. An invalid, `data:`, or
`javascript:` base URL falls back to that document URL. The resulting destination still
passes the browser's same-origin, local-file, opaque-origin, HSTS and downgrade
checks before any form values are serialized. `_self`, `_parent`, and `_top`
(including the first `<base target>` fallback) use the current tab; `_blank`
opens a new active tab in the source window. The selected button or input submitter's
`formtarget` overrides the form target, including an empty override selecting the current tab.
Named and other non-keyword contexts remain visibly rejected. Target errors are separate
from other form errors: an invalid submitter affects only its own activation, and a
supported submitter override can replace an unsupported form/base target. Target checks
precede validation and value serialization; destination policy checks precede new-tab
creation. New-tab form preflight uses the source's shared HSTS store before checking
downgrade/fixed-origin policy, and the destination loader rechecks initial/redirect
policy without rebinding the document origin. Custom `IPageSource` implementations
inherit conservative initial checks through `ValidateNavigationTarget`; sources that
upgrade HTTP must explicitly provide equivalent HSTS-aware preflight.

Supported controls with a `form` attribute use the first element in tree order
with the exact matching `id` when that element is a `<form>`; otherwise they
have no owner and do not fall back to an ancestor. Controls without `form` use
their nearest ancestor form. Associated controls are submitted in document
tree order, including controls before or outside the form element. A fieldset's `form=` owner does not transfer its descendant controls; each control keeps its own owner. Descendants
of disabled fieldsets are disabled except controls inside each fieldset's first
`legend` child; the inherited state prevents focus, validation and successful
submission, and nested fieldsets apply independently.
Form-associated `<output>` elements are not submittable controls; their rendered
content is omitted from browser form-control metadata and GET entries. `<input
type="button">` and `<button type="button">` are likewise non-submittable inert
controls: they are reported with kind `inert`, use their `value` or text content as
their label respectively, are focusable by pointer and Tab, and are painted by the
shell overlay. Activation never submits or navigates, and these controls do not
contribute GET entries or implicit-submission button counts. They have no scripting
or `onclick`; the input variant has no default label. `type=image`/`file`/`color`
remain unsupported.

Visible failures, without navigation and keeping the committed page: a form with
`method` post/dialog, unsupported `enctype`, unsupported named or non-keyword target
(including inherited `<base target>` or the selected `formtarget`), non-UTF-8 `accept-charset`, unsupported `form=` ownership on object, submitter overrides other than `formaction`, `formtarget` and `formnovalidate`, unsupported input types, invalid email/address-list, URL or number values, and reversed numeric bounds,
textarea `dirname`, listbox/multiple select semantics, optgroups and unsupported select content,
`object` controls,
`datalist` controls, invalid/unsupported action schemes,
unsupported document encodings, HTTPS form downgrades not upgraded by HSTS,
required empty fields, edited too-short/overlong values, pattern mismatches,
unsupported pattern syntax, or query/URL limit overflow. Form-level `novalidate` and a selected submit button's `formnovalidate` skip supported constraint checks; address/scheme and origin protections still apply. `formaction` is supported, while the other submitter overrides remain unsupported.
There is no POST, constraint-validation UI, `submit`/`input`/`change`/`keydown`
events, `requestSubmit`, script submission, autofill, IME preedit, or clipboard selection range support.

## Navigation and page policy

Page-initiated link and form navigation to `file:` and `data:` URLs is blocked;
explicit address-bar navigation remains available. This prevents page content
from choosing privileged local-file loading or constructing navigable `data:`
documents through the broker. Local `file:`
documents also cannot initiate HTTP(S) link or form navigation, preventing an
untrusted local page from sending form values to a remote endpoint. Forms in
opaque `data:` documents also block HTTP submissions (subject to a prior HSTS
upgrade), and block HTTPS form submissions as well because their origin is
opaque. HTTP(S) documents cannot submit forms cross-origin; a same-host HTTPS
document's HTTP action can proceed only through the existing HSTS/downgrade
check. Users can still enter an explicit URL in the address bar. These are narrow
guards, not filesystem sandboxing: locally selected file documents remain
privileged/trusted content, and there is no general origin/CORS/CSP or local-file
isolation policy yet.

BrowserController owns independent per-tab IPageSource/IPageRenderer instances.
GetPageSource uses a separate ResourceLoader per tab, with **4 MiB responses**,
existing redirect/deadline limits and cookies **off**. Fetch is asynchronous;
completion is observed in the main-thread pump. In single-process mode,
DOM/style/layout, HarfBuzz and Skia remain synchronous on the UI thread and can
pause responsiveness. In multiprocess mode those operations run on each
worker's main thread; the browser pumps asynchronous replies. SDL and chrome
stay on the browser UI thread in both modes.

Only `text/html` is accepted, except that explicitly selected local files
without MIME metadata are treated as HTML. There is **no MIME sniffing**.
The same tab loader also fetches supported linked stylesheets before publication
(see the stylesheet policy below); a stylesheet failure fails the navigation.
Decoding uses `HtmlEncodingSniffer`: a BOM first (even when the Content-Type
charset is unknown), then a valid MIME charset, then the HTML prescan of the
first 1024 bytes (`<meta charset>`, `http-equiv` pragma, XML declaration), then
explicit UTF-8 fallback. The diagnostics name the chosen source, declaration
offset, confidence and ignored labels. Unlike the spec, an unknown Content-Type
charset without a BOM still fails visibly instead of falling through to the
prescan. There is no statistical/locale sniffing or reparse when the tree
builder sees a later declaration. Unsupported MIME fails visibly. HTTP error-status HTML
can render; status codes and transport diagnostics remain visible.

`LoadedPage.Origin` is associated with the **final response URL**, after redirects.
Tuple identity comes from that URL; every new opaque document gets a distinct
identity even when the same `BrowserUrl` is reused for reload or another tab.
The retained document and ordinary metadata record copies preserve identity.
URL and origin are get-only to prevent `with { Url = ... }` from leaving a stale
origin; a changed URL requires a new document.

`BrowserTab.Origin` is the browser-owned **committed origin**: read-only to
callers, null until the first successful publication, and set by
`BrowserController` only in the same UI-pump step that publishes the
document's page, history entry and retained document, after rendering and
`IPageRenderer.CommitDocument` succeed. A pending, stale or superseded load or
render, a load/render failure or a rejected commit leaves the previous origin
(or null). Retained resize and tab moves keep the same instance; reload or a
new data/file document publishes a new opaque identity; tuple navigations
update to the final URL's origin. A renderer crash keeps the committed origin
until a fresh load succeeds. The origin is never derived from the worker. This
is identity only: it does not authorize requests, and same-origin/CORS/CSP,
inherited/sandbox origin selection, site isolation and production navigation
policy remain unimplemented; explicitly selected development navigation is
unchanged.

**Origin-isolated navigation renderers.** `BrowserController` accepts an
optional `isolateOrigins` policy (`IsolatesOrigins`); low-level/test controllers
default to one renderer per tab. `DevelopmentShell` always enables it when a
renderer path is set, so every multiprocess CLI mode (confined or explicitly
unsandboxed) rotates renderers with no opt-out. After loading, the authoritative
final `LoadedPage.Origin` is compared with the committed document origin via
`SecurityOrigin.IsSameOrigin` (never serialized `null` or the requested URL):

- the first document uses the tab's pristine renderer; once any content has
  been sent to it (even a canceled or failed render) it is no longer pristine;
- same-origin tuple navigations, reloads and traversals reuse the committed
  renderer; every new opaque document (data:, about:blank, file:, reload of
  those) and every cross-origin document gets a new factory renderer before
  any content is sent;
- the candidate is owned by the pending operation. The old renderer, document,
  title, origin, history and pixels stay published until the candidate's
  `RenderAsync`, `CommitDocument` and the history commit all succeed; then the
  candidate is promoted and the previous renderer disposed in the same pump step;
- factory, render, commit or history failures, cancellation, stale/superseded
  completions and tab/window/controller close dispose the candidate without
  touching the committed renderer. Retained resize always uses the committed
  renderer and never rotates or reruns scripts. Only the committed renderer's
  `TakeFailure` is polled, so crash recovery restarts that renderer.

This is strict per-origin top-level navigation rotation only: there is no site
computation, cross-site frame isolation, inherited origin selection, browser-wide
same-origin/CORS/CSP policy or production request authorization.

The shared VisualWeb.PageRendering StaticPageRenderer parses once and collects connected embedded `style` blocks
and supported linked stylesheets in document (cascade) order, taking style child text. Inline attributes also participate.
Named stylesheet sets, non-CSS type values, media other than absent/`all`/a single
`screen` media type, media-query conditions, and CSS imports fail explicitly rather
than silently producing a partially styled page.

Bounded linked stylesheets are brokered by the browser, never by renderers.
`GetPageSource` parses the decoded document with the renderer's exact parser
options (bounded managed parsing in the browser process, for discovery only),
then fetches each supported classic `link rel=stylesheet` (tokens ASCII
case-insensitive; `href` resolved with Core.Url against the document base URL,
using the final response URL as fallback)
through the same tab-local `ResourceLoader`, sharing the shell session HSTS store,
before the document is published. Requests keep cookies disabled and the bad-port
and HSTS checks. HTTP(S) stylesheets must remain same-origin with the final
document URL across every redirect; cross-origin requests and redirect targets
fail before that request is sent. This intentionally rejects cross-origin CSS
loads that ordinary HTML permits. HTTPS documents also block HTTP stylesheet
URLs and HTTPS-to-HTTP redirects before the downgraded request is sent. This is
limited to linked stylesheets, not a general mixed-content policy. HTTP(S)
documents can load same-origin HTTP(S) and data URLs; `file:` and opaque `data:`
documents can load only data URLs. This prevents local/opaque page content from
using the privileged browser broker to access additional local files or trigger
network requests. This is a narrow stylesheet policy, not general request authorization. Links
without a nonempty `href` create no sheet, as in HTML.
Alternate/titled/disabled links, `media` other than absent/`all`, `type` other than
absent/empty/`text/css`, `crossorigin`, `integrity`, `referrerpolicy`, the obsolete
`charset` attribute and unsupported schemes fail navigation. The first `<base href>`
resolves relative stylesheet URLs; invalid, `data:`, and `javascript:` base values
fall back to the final response URL. Cross-origin destinations remain blocked before
request, even when introduced by a base URL. Each response
must be 2xx `text/css` (no sniffing or quirks-mode fallback), at most 1 MiB of body bytes and 256 Ki decoded characters.
Decoding follows the CSS fallback encoding order (BOM, Content-Type charset,
exact `@charset "…";` prefix with UTF-16 mapped to UTF-8, document encoding).
A valid leading `@charset` marker is consumed as an encoding signature before
CSS parsing; unknown labels fail instead of falling through. At most 32 unique URLs, with
a 1 MiB UTF-8 JSON budget for the whole collection, are published on
`LoadedPage.Stylesheets` as data only (request URL and decoded text; no headers,
redirect URL, DOM or capability). Any stylesheet failure fails the whole navigation,
so the committed document, frame, history and origin are retained and no partial
document is published. Cross-origin candidates receive only this data, never
fetch authority. Renderers look up each link's resolved URL and fail
visibly when a link added or changed by an enabled inline script was not provided.
`@import`, `url()` resources, fonts, images, CORS/SOP and general mixed-content policy
for other subresources remain unimplemented; the narrow HTTPS stylesheet downgrade
guard does not provide those policies. Caching remains unimplemented; unsupported CSS
still yields diagnostics that fail layout.
Scripts are inert by default; the explicit
`--enable-inline-scripts` phase-11d option executes a bounded inline classic batch
after parsing and before stylesheet collection. See the
[scripting guide](scripting.md) for exact classification, limits and deviations.
Its phase-11e bindings also allow bounded attributes and node creation/mutation;
new inline styles and tree edits participate in the post-script paint pass,
but inserted script elements never execute.
Phase 11f enables finite native Promise/queueMicrotask checkpoints between
classic scripts and before paint, sharing task deadlines and DOM/queue budgets.
Callback failure rejects navigation; retained resize never reruns jobs.
There is no persistent script host or HTML event loop.
Phase 11g also enables bounded synchronous synthetic Event/EventTarget dispatch
and Node/document capture/target/bubble listeners. Caught listener failures
still reject navigation. No automatic input/load events or `on*` handlers run.
Phase 11h runs finite loading/interactive/DOMContentLoaded/complete readiness
and readystatechange events before paint, with native checkpoints between
stages and shared navigation budgets. Listener failures preserve the published
page; retained resize never repeats lifecycle. This is a post-parse
approximation, not HTML scheduling or Window/load support.
Phase 11i exposes bounded querySelector/querySelectorAll and Element
matches/closest. Static list wrappers retain node identity; query-driven
lifecycle style/title changes participate in paint and retained resize.
Scoped `:scope` queries and root-scoped `:scope` stylesheet rules feed the same
lifecycle paint/retained resize path; selector failures stay transactional.
Filtered `:nth-child`/`:nth-last-child` queries and stylesheet rules also feed
that path in local and process rendering, with strict-list failures preserving
the committed document and exact retained pixels.
Phase 11j adds className/live classList; lifecycle token mutations feed CSS
queries and class selectors before paint, with transactional page failure.
Phase 11k adds bounded live tree inspection/navigation for lifecycle callbacks;
failed inspection cannot publish a candidate or displace retained DOM.
Phase 11l adds reflected IDs and attribute toggles that feed lookup/selectors
before painting, retaining the same transactional navigation behavior.
Phase 11m allows bounded in-place style/title CharacterData edits during
lifecycle; text identity and retained DOM resize remain unchanged.
Phase 11n allows bounded style/title Text splitting during lifecycle;
retained resize uses both resulting nodes without script reexecution.
Phase 11o allows bounded lifecycle Node normalization with atomic subtree
preflight, surviving Text identity and detached suffix data preservation.
Styles/title are recomputed before paint; retained resize never renormalizes.
Phase 11p lets lifecycle scripts compare live DOM structure via bounded
isEqualNode, without changing publication, retained DOM or script scheduling.
Phase 11q exposes readonly Node/Element metadata for lifecycle scripts,
preserving bounded string outputs, owner identity and transactional resize.
Phase 11r allows ordered attribute snapshots/presence checks in lifecycle
scripts, without changing stylesheet discovery or retained resize policy.
Phase 11s allows comment/instruction creation and target inspection in lifecycle
scripts. They are inert non-Text barriers, excluded from stylesheet/title text;
no pseudoattribute reactions, loading or dynamic script scheduling is added.
Phase 11t allows lifecycle self-removal of style/title nodes. Discovery and
paint use the final live tree; failed candidates retain the committed DOM,
and resize still recomputes without reexecuting scripts.
Phase 11u lets lifecycle scripts inspect document.doctype and its readonly
name/public/system identifiers. Existing parser legacy-mode rejection,
no identifier fetching and transactional/retained rendering remain unchanged.
Phase 11v allows lifecycle scripts to deep-clone style elements and insert
them into the live tree; detached clones have no style effect until insertion.
Cloned script elements remain inert and the finite initial script snapshot is
unchanged.
Engine.Content's new RenderParsed entry point keeps this collection policy
outside the engine pipeline and avoids parsing twice. GET/MIME/decoding remains
browser-owned in both modes.
Browser GET navigation inherits Engine.Net Fetch bad-port blocking: HTTP(S)
addresses or redirects to a blocked port (for example `:6000`) fail with a
visible tab error and keep the committed page, history and origin. There is no
bypass flag; see the [networking guide](networking.md#fetch-bad-port-blocking).

Existing HTML/CSS/text/layout/paint subsets remain enforced. **The shell
does not override author CSS to make unsupported pages appear successful.**
Adjacent block-sibling and eligible first/last block-child vertical margins
collapse. The welcome page also overrides bold/italic UA defaults for the
configured regular face. Unsupported executable script features in opt-in mode, layout algorithms, CSS diagnostics and
paint features remain errors. See [HTML](html-dom.md), [CSS](css.md),
[text/layout](text-layout.md) and [painting](painting.md).

Successful load **and rendering** commit history, final redirected URL, title
and current frame atomically. Failure preserves the old document/frame and
history index; the address retains the attempted URL, with a red error status
and stderr diagnostic. Reload replaces the current entry rather than appending. A
new successful navigation discards the forward branch. Back/Forward between
entries created by local fragment navigation retains the current document and
restores the target fragment scroll without loading; traversal to another document
refetches/rerenders it at that entry's saved, extent-clamped scroll position. There
is no bfcache, iframe history, History API or session persistence.

New loads cancel previous loads/renders for that tab. Generation checks prevent late
stale results from replacing newer content; closing a tab cancels its loads
and disposes its loader and local font owner or worker process. Minimized/too-short windows defer
completed page rendering until a usable viewport exists. Static-mode resizing rerenders
the retained decoded HTML; opt-in scripting retains the committed mutated DOM
and never reruns scripts on resize. Neither refetches. Worker loss requires
explicit reload for scripted pages; unsupported resize output
is reported and stale-size frames are not presented.
Oversize windows receive an explicit title/stderr framebuffer-limit error;
they pause presentation/loading completion until resized smaller without
terminating other windows.

Chrome is rendered separately from page styles and composed with the current
page frame below its 120-logical-unit header. Physical sizes match each native
surface exactly, including fractional pixel density: a division/multiplication
round-trip is rounded down by one double ULP only when needed to prevent
`ceil` creating a spurious extra pixel. Pointer targets use native logical
coordinates, not physical framebuffer coordinates.

## Limits and trust boundary

| Shell limit                                             |   Default |
| ------------------------------------------------------- | --------: |
| Windows                                                 |         8 |
| Total tabs                                              |        32 |
| URL-only history entries per tab                        |       128 |
| Typed address UTF-16 characters                         |      8192 |
| Pending loads, including canceled ones not yet finished |        64 |
| Frame pixels per page/window                            | 4,194,304 |
| Response bytes per tab load                             |     4 MiB |

History evicts the oldest entry at its configured count limit. Other limits
fail explicitly. Engine stage budgets still apply separately; these are not
a single process-wide memory bound. Only the current decoded HTML and frame
are retained per tab; parsed DOM is retained in the renderer for scrolling/resize,
with at most one unpublished candidate alongside the committed document.
Render-stage objects are not shared between tabs or retained as a history cache. Font owners and loaders are
tab-local, while immutable configured font file paths and the chrome renderer
are shell-level resources.

Pages support bounded textual anchor hit-testing and primary-click navigation,
but no general DOM input, controls or selection.
Basic vertical document scrolling is available; general/nested CSS overflow is
not. Bounded classic linked stylesheets load as described above; there are no
downloads, storage, images or media.
Post-parse inline scripting remains explicit opt-in, not a full event loop. Native page crashes
can terminate the entire shell in single-process mode; multiprocess mode
contains worker failures to their tab. Browser-native chrome/platform crashes
remain shell failures. Production origin/CORS/CSP/confinement policy remains
**unfinished phase 10/future work**, not inferred from process separation.

## Validation

```sh
dotnet test tests/VisualWeb.Browser.Tests/VisualWeb.Browser.Tests.csproj
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --development-single-process --allow-unsandboxed-development --font tests/Engine.Text.Tests/Data/NotoSans.ttf \
  --smoke --backend dummy
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --development-single-process --allow-unsandboxed-development --font tests/Engine.Text.Tests/Data/NotoSans.ttf \
  --smoke --backend x11
```

These explicit unsandboxed opt-outs are only for trusted offline development
fixtures; `--smoke` itself never bypasses confinement. To exercise the required
profile, use normal `--multiprocess` with `--renderer` as in the platform guides.
Smoke checks use hidden native windows and offline HTML data URLs. They
verify actual SDL surface pixels, keyboard shortcut routing/address focus,
tab/window lifecycle, moved identities and back/forward history. `--backend
dummy` and `--skip-text-input` are restricted to smoke mode; the latter is
an explicit exclusion for compositors lacking text-input support, not a
claim that keyboard/IME works. `--url` is disallowed in smoke mode to preserve
offline deterministic input.

The [browser suite](../tests/VisualWeb.Browser.Tests/) exercises exact history/
limits, failed and stale navigation, close/move lifecycle, MIME/encoding and
stylesheets, actual native CPU frames, physical density composition and
address/button events using a fake window system. Ordinary tests do not
initialize displays or contact remote hosts.

Official references use the independently refreshed [cache](standards.md):
`html` style-block/session-history sections, `url`, `encoding`, `mime-sniffing`,
`fetch`, CSS documents and SDL video/input/surface APIs. No additional live
document fetch occurs during tests.

### Phase-9 validation outcome

Validated on Linux x64: all **36 projects** build and all **11,616 tests** pass,
without failures or skipped cases. Totals: Browser 42, Content 5, Paint 24,
Text 17, Layout 39, CSS 203, HTML 7,202, DOM 17, Core 3,815, Networking 210,
Platform 19 and SpecCache 23. First-party formatting and editor diagnostics
are clean; independently, all **43 cached references** remain fresh.

Actual SDL dummy and X11 hidden-window smoke checks pass with **10 presented
frames each**, including reading the composed blue page pixel from the native
SDL surface. Wayland shell keyboard/IME, Windows/arm64 native execution and
real high-DPI desktop behavior still require suitable target environments;
fractional-density frame composition is covered by deterministic tests, not
claimed as desktop certification.

Phase-10a process-separation results and current totals are recorded in the
[renderer process guide](renderer-processes.md#phase-10a-validation-outcome).
