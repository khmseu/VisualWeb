# What is missing

## Overall assessment

**The architectural foundation is largely delivered, but VisualWeb is still a development browser for a narrow HTML/CSS subset—not yet a general-purpose browser or safe for untrusted websites.**

The biggest remaining gaps are page interaction, real-world standards compatibility, and production-grade security/isolation.

### What already satisfies the original plan

| Original requirement                         | Current state                                                                                                                                                                                                                                                                                                                                                    |
| -------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| C# monorepo with separate browser subsystems | Implemented: platform, core standards utilities, networking, HTML/DOM, CSS, text/layout, painting, scripting, IPC, and applications. See the `project map`.                                                                                                                                                                                                      |
| Encapsulated OS dependencies                 | Implemented through platform abstractions and Linux/Windows backends using SDL. See the `platform guide`.                                                                                                                                                                                                                                                        |
| Tabs and multiple windows                    | Implemented, including tab creation, closing, switching, and moving between windows. See the `browser shell guide`.                                                                                                                                                                                                                                              |
| Isolation between tabs                       | Partially delivered: multiprocess mode has one renderer per tab and committed origin (rotated on cross-origin/opaque navigation), bounded IPC, deadlines, and tab-local crash/restart handling. Browser CLI multiprocess modes require supported OS confinement by default. **Process separation is not complete web security.** See the `process architecture`. |
| Architecture prepared for standards growth   | Present: subsystem boundaries, official-spec references/cache, and targeted conformance fixtures. This is preparation, not comprehensive standards implementation.                                                                                                                                                                                               |

## What is still missing

### 1. Production-safe browsing and stronger isolation

This is the most important gap before accepting arbitrary web content.

**Origin-selection update (2026-10-23):** Page-initiated `about:blank` now
inherits its initiating committed origin, while browser-UI blank loads stay
opaque; history retains the selected origin across reload and traversal. This
narrows the inherited-origin gaps below only for this supported top-level case.
Sandboxed documents and other inherited-origin contexts remain unsupported.

- Security-origin identity is partially implemented: immutable tuple/opaque URL origins and `LoadedPage` origins from the final response URL, with fresh opaque identity per new document and stable identity for retained repaint/metadata copies. Tabs expose a browser-owned committed origin published atomically with successfully rendered documents. Page-initiated top-level `about:blank` now inherits its initiating committed origin; browser-UI blank loads remain opaque, and history preserves the selected origin across reload/traversal. Other inherited-origin and sandbox selection remain missing. Engine.Net has a same-origin restricted GET mode (`LoadSameOriginAsync`) that checks the initial URL and every redirect against a fixed HTTP(S) request origin; the browser applies it to same-origin HTTP(S) linked and imported stylesheets. Cross-origin HTTP(S) linked/imported stylesheets use a bounded credentialless CORS subset: send the document `Origin`, require one final `Access-Control-Allow-Origin` value matching that origin or `*`, and reject redirects that leave the effective initial stylesheet origin. This deliberately restrictive subset is not full Fetch CORS or production-safe request authorization. Top-level navigation remains without general request authorization; the development shell confirms cross-origin page-initiated requests and typed HTTP(S) address-bar requests that differ from the committed document. When no document is committed, every typed HTTP(S) address-bar submission is confirmed before transport. Both Engine.Net entry points (and therefore browser navigation) block Fetch bad ports for HTTP(S) URLs and every redirect hop. Bounded session HSTS is implemented: one memory-only DNS-host policy store per shell session, shared across tabs/windows in all modes, authenticated HTTPS first-header learning and initial/redirect upgrades before destination checks; valid STS fields from public suffixes are rejected with diagnostics using the pinned ICANN+PRIVATE list (including its prevailing-default treatment of single-label hosts such as `localhost`), while standalone loaders opt in explicitly. The final-response CSP subset applies `upgrade-insecure-requests` URL rewriting to browser-brokered linked/imported stylesheet URLs and redirects; shell navigation URL rewriting remains limited to its documented page-initiated cases. Separately, HTTP and HTTPS main-document navigation hops send `Upgrade-Insecure-Requests: 1`; subresources do not. HTTPS navigation always sends the signal without a host-specific preload list. This request signal is not URL rewriting or authorization. It also enforces `form-action` for browser-brokered GET form submissions before query serialization, without `default-src` fallback or affecting links/address-bar navigation. No HSTS persistence/preload, browser-wide same-origin enforcement, full Fetch CORS, general CSP enforcement, general mixed-content policy, or storage partitioning is implemented. HTTPS documents also block HTTP linked and imported stylesheets and downgrade redirects unless an applicable UIR directive first upgrades them.
- Navigation has a development-shell confirmation increment: cross-origin page-initiated link/form destinations and typed HTTP(S) address-bar destinations wait before transport; when no document is committed, every typed HTTP(S) address-bar destination is confirmed because no source origin exists. Automatically started shell HTTP(S) loads, including launch URLs opened through `OpenWindow`, use the same confirmation before their first request, and every redirect hop that changes origin relative to the preceding request prompts separately, including a return to a previously seen origin. The prompt reveals only the destination origin. During page-initiated requests the address bar retains its prior value; address-bar submissions retain the typed destination. The built-in new-tab welcome page is a `data:` URL; other non-HTTP(S) shell navigation and direct low-level `BrowserController.Navigate` calls remain unchanged. This is explicit user confirmation, not production request authorization.
- The non-HTTP(S) exception in the preceding shell summary is refined by the top-level `file:` and `data:` confirmation increments below. The built-in `data:` welcome page and other non-HTTP(S) shell navigation remain unchanged.
- Top-level navigation renderer swaps are implemented: every multiprocess shell mode (confined or explicitly unsandboxed) rotates to a fresh renderer for cross-origin final origins and every new opaque document, transactionally after render/commit/history success, reusing it for same-origin navigation. Page-initiated `about:blank` inherits the initiating origin and can reuse its renderer. Low-level controllers keep the per-tab default. There are no cross-site frames or frame isolation, no site computation, no sandbox or other inherited-origin selection, and no SOP/CORS/CSP or production request authorization.

  The CORS limitation here means general request authorization: the new
  credentialless CORS subset applies only to linked/imported stylesheets and
  does not provide general SOP, CORS or CSP enforcement.

- The CLI secure-by-default renderer milestone is delivered: recommended `--multiprocess` and legacy `--development-multiprocess` require supported confinement before SDL/content/worker startup, without fallback. Unsandboxed execution requires explicit `--allow-unsandboxed-development` trusted-content acknowledgement in a development mode (mandatory for single-process), rejected with normal mode or `--require-sandbox`. `--require-sandbox` remains a redundant compatible assertion; low-level library/test renderer defaults are unchanged. This does not deliver production request authorization or complete web security.
- Native Linux ARM64 confinement is implemented with the same fail-closed namespace/mount/seccomp/cgroup/resource guarantees as x64. The native Linux x64/ARM64 workflow passed on commit `bc22600`, including the full solution tests, confinement/resource-exhaustion smokes, V8 and browser checks ([run](https://github.com/khmseu/VisualWeb/actions/runs/39194869543)); this closes the previously recorded native ARM64 evidence gap. This does not certify desktop Wayland/IME, high-DPI behavior or production-safe browsing.
- A complete pinned Public Suffix List has a bounded immutable matcher for exact, wildcard, exception, IDNA and prevailing-default rules across ICANN and PRIVATE sections. HSTS now rejects authenticated STS fields from public suffixes with a response diagnostic; the list is not used for registrable-domain/site computation or broader request policy.
- The browser/network broker remains privileged; production request authorization is unfinished. Page-initiated `file:`/`data:` links and forms and linked file stylesheets are rejected, while explicit address-bar navigation remains available. Local file documents also cannot initiate HTTP(S) link/form navigation; HTTPS page-initiated links/forms block cleartext requests and downgrade redirects unless HSTS upgrades the destination before transport. Opaque `data:` links block cleartext HTTP navigation and opaque documents cannot submit forms to any HTTP(S) destination. Trusted shell activation now permits HTTP(S) links and HTTP(S) document forms to target cross-origin HTTP(S) destinations. Link navigation reauthorizes every HSTS-resolved redirect through the browser-owned loader; form navigation instead fixes the effective action origin across redirects so query data cannot be forwarded to another origin. HTTPS and opaque documents still reject unupgraded HTTP targets. Custom `IPageSource` implementations that cannot enforce per-hop link authorization, fixed-origin form redirects, or HTTPS downgrade protection fail closed. A bounded final-response CSP parser models intersecting `default-src`/`script-src`/`script-src-elem`/`style-src`/`style-src-elem` policies plus `style-src-attr` style-attribute policy with `style-src` then `default-src` fallback with `'none'`, `'self'`, exact HTTP(S) origins, the bare `*` source (HTTP(S) and the protected document's scheme), HTTP(S) DNS host sources, including wildcard subdomains (not the apex; omitted ports match only default ports, numeric and `*` ports are supported; DNS-host paths support exact and trailing-slash prefix matching on percent-decoded segments only for initial requests, as CSP3 ignores source paths after a redirect; IP-literal paths remain unsupported), `data:`/`http:`/`https:` resource scheme sources (including CSP3's HTTP-to-HTTPS scheme upgrade), strict nonce-source matching, and SHA-256/384/512 hash sources; matching nonces or hashes of exact UTF-8 inline element text or non-overridden `'unsafe-inline'` permit script/style elements, and non-overridden `'unsafe-inline'` permits style attributes; matching hashes also permit exact style-attribute text only with `'unsafe-hashes'` (no event-handler or `javascript:` authorization); inline style elements and linked/imported stylesheet destinations use `style-src-elem` with `style-src` then `default-src` fallback and are authorized before each request/redirect while preserving fixed-origin HTTP(S) stylesheet checks (a same-host request may be upgraded to HTTPS by UIR). `upgrade-insecure-requests` applies only to brokered linked/imported stylesheet URLs and redirects; `script-src-attr`, meta/report-only policies, navigation upgrades and broader directives remain unfinished. Local file and opaque `data:` documents cannot trigger linked/imported HTTP(S) stylesheet requests (`data:` stylesheets require an applicable CSP source list to allow `data:` explicitly); HTTP(S) documents can load only same-origin HTTP(S) linked/imported stylesheets, with cross-origin redirects rejected, plus CSP-authorized `data:` sheets. These are narrow restrictions, not local-document confinement or general request authorization; non-shell initiation and broader SOP/CORS/CSP remain unfinished. Linked stylesheet discovery and CSS `@import` parsing reject `file:` destinations, and HTTPS documents block HTTP linked/imported stylesheets and downgrade redirects unless a UIR directive upgrades the request first.
  In the shell-configured path, page-initiated cross-origin form redirects use these per-hop confirmations instead of silently following or applying the low-level fixed-action-origin form restriction; direct low-level controllers retain their existing restrictions unless given a policy.
  UIR's bounded shell integration upgrades HTTP GET form actions (including cross-origin actions) and same-origin page-initiated HTTP links, including redirect destinations, before applicable `form-action`, page-navigation authorization, downgrade and bad-port checks. Cross-origin link navigations are not upgraded. Explicit port 80 maps to HTTPS's default port 443; non-default ports and URL components are preserved. Form query values are sent only after the existing cross-origin confirmation. Explicit address-bar navigation does not inherit document-policy URL rewriting, but its HTTP(S) main-document hops carry the generic UIR header signal. Other resource types and non-shell controller paths remain unchanged. The cross-origin form/redirect confirmation regression now waits for the browser-owned prompt before accepting, avoiding repeated Enter events resubmitting a form while asynchronous redirect authorization is pending.

- The bounded response-header CSP `base-uri` subset checks the first usable `<base href>` without `default-src` fallback. A disallowed base resolves stylesheet/import discovery, renderer links and form actions against the final document URL. The effective URL crosses IPC v41 as data only; dynamic base mutations are unimplemented and lower-priority work.

Evidence: `networking trust boundary`, `isolation architecture`, and `Linux confinement architecture policy and evidence boundary`.

Shell history-navigation increment: HTTP(S) Back/Forward/Reload targets use
the development-shell authorizer. Cross-origin history destinations prompt
against the currently committed document, and origin-changing redirects prompt
relative to each preceding hop. HTTPS documents block HTTP history targets and
downgrade redirects before transport. This does not close the broader
production-authorization gap or change low-level controllers without an
authorizer.

Shell top-level file-navigation increment: explicit `file:` loads from the
address bar, `OpenWindow(file:)`, and cross-document file Back/Forward/Reload
confirm before file I/O and show the canonical file URL path without its query
or fragment. Page-initiated file links/forms remain blocked; same-document
fragment history does not reload or prompt. Direct low-level controller calls
remain unchanged. This does not isolate filesystem access.

Shell top-level data-navigation increment: explicit `data:` loads from the
address bar, `OpenWindow(data:)`, and cross-document Back/Forward/Reload
history now require confirmation before loading. The prompt identifies a data
URL without displaying its payload. The built-in welcome page remains exempt,
including in history; page-initiated data links/forms and direct low-level
controller behavior remain unchanged. This remains a DevelopmentShell policy,
not production request authorization.

### 2. Basic interaction with pages

The shell supports bounded textual link activation and horizontal/vertical
document scrolling, including Ctrl+Home/End outside editable controls;
broader page interaction is still missing:

- Textual `<a href>` links and empty/non-text anchors with non-empty measurable
  block geometry (from the anchor itself or rendered descendants) support primary left-click (release must remain over the same anchor)
  and Tab/Shift+Tab/Enter navigation in shell, local and actual
  renderer-process paths; middle-click opens
  the visible link in a new active tab and Ctrl+primary-click opens it in a
  background tab, regardless of its target. On opted-in pages, middle-click
  first dispatches trusted cancelable `auxclick`; cancellation suppresses the
  active-new-tab default. Shift+Enter opens the focused link
  in a new active tab; Ctrl+Enter opens it in a background tab. Enabled drawn
  chrome controls precede rendered links in traversal; focusing an offscreen
  anchor scrolls it into view. Ctrl+L retains address editing. Focus is per-tab,
  with shell-only outlines across visible rectangles and no page raster changes.
  IPC v28 introduced grouped visible rectangles plus bounded `_blank` target
  metadata (4096 anchors, 64 rectangles each, 1 MiB metadata); IPC v37 adds
  document-space offsets for offscreen anchors, without element IDs/DOM/native
  handles. v28 also added bounded fragment-ID offsets for
  same-document scrolling, without element IDs/DOM/native handles. Fragment
  links update same-document URL history and bounded per-entry scroll positions,
  then repaint the retained document without a resource load; rendered element IDs,
  text-bearing legacy `<a name>` targets,
  and empty-fragment top scrolling are supported. `_blank` opens an active tab in the source window through the
  existing broker policy; `_self`, `_parent`, and `_top` use the current tab,
  and the first `<base target>` supplies fallback for links. Other ASCII target
  names are scoped to the current window and reuse a matching named tab or create
  one when absent. Hyperlink URLs resolve against
  the first `<base href>` (invalid/data/javascript bases fall back to the final
  response URL); form actions and linked stylesheets use the same resolution.
  Successful document commits and resize repaints clear frame-local anchor focus;
  retained scroll repaints preserve the focused anchor. Failed navigation preserves it. Hovering visible link rectangles draws a
  shell-owned outline without changing renderer pixels. Empty inline anchors and
  non-text anchors without measurable rendered block geometry remain unsupported.
  Primary non-link/control/non-text page clicks now clear focused page targets.
  Sequential focus ordering honors valid signed-32-bit `tabindex` values on
  rendered links and visible enabled supported form controls: positive values
  precede zero/default values in ascending order, ties retain tree order, and
  negative values are skipped. Invalid or omitted values retain tree order;
  values outside the supported signed-32-bit range fail visibly, and other
  elements do not become focusable through `tabindex`.
  On explicitly opted-in pages, focused-link Enter, Shift+Enter and Ctrl+Enter
  also dispatch the retained trusted click event before their existing current,
  active-new-tab or background-new-tab default action; Ctrl+primary pointer
  activation dispatches it before opening the background tab. `preventDefault()`
  cancels those defaults. Opted-in middle-click also dispatches trusted bubbling,
  cancelable `auxclick` before its active-new-tab default; static mode and
  unmodified page keyboard events remain unchanged. These are plain Events, not
  PointerEvents with pointer metadata.
  General hit-testing, DOM focus/keyboard events and other page input remain missing.
  See [shell controls](../docs/browser-shell.md#controls).
- A bounded simple-forms subset supports shell-owned `input`
  text/search/email/tel/url/password/date/time/datetime-local/month/week/number/range/color/checkbox/radio/hidden/submit/reset,
  `textarea`, single-select and multiple `select`, `button` submit/reset controls, and inert `input type=button`/`button type=button` controls; supported successful controls
  submit current/new-tab GET urlencoded queries via IPC v39 data-only metadata and the ordinary
  navigation transaction; supported controls use finite inline-block layout with
  bounded fallback dimensions and unsupported form semantics fail visibly.
  Visible enabled supported controls also activate through visible text in an
  explicit `<label for>` or the first supported labelable descendant; nested
  interactive descendants remain independent and no DOM form events are sent.
  Label hit metadata is bounded to 64 rectangles per control and 4096 per frame.
  Reset controls are shell-owned: pointer activation or Enter on the focused
  reset control restores all controls owned by its form to their initial values
  and checked states, clears their edited/dirty/check state, preserves focus,
  and does not navigate or run submission validation. Text-field implicit Enter
  continues to target only a submitter and never activates reset controls.
  Single-selects expose bounded direct options, default selection, disabled-option
  skipping and Up/Down/Home/End/PageUp/PageDown keyboard selection. Page keys move
  by the visible popup row count and skip disabled options. Bounded committed-text prefix
  search selects matching enabled options and cycles repeated single-character
  input. Enter/Space or a primary click opens a browser-owned popup; it supports
  pointer option selection, hover highlighting and wheel scrolling through longer
  lists, with Escape/outside-click dismissal. Text fields
  and textareas display bounded placeholders
  without changing submitted values. Email `multiple` supports sanitized
  comma-separated address lists. Single-select controls with a bounded `size` from 2 through 12 render as
  browser-owned listboxes with pointer selection and existing keyboard selection. A shell-owned, row-bounded
  first visible option supports wheel scrolling over enabled listboxes (consumed at bounds, no page scroll), offset
  pointer mapping and keyboard auto-visibility; it survives retained repaints and resets with the form. Bounded nested
  vertical wheel scrolling is now supported for definite-height block containers styled with
  `overflow-x:hidden` and `overflow-y:auto`; general CSS overflow semantics and DOM scroll events remain unsupported.
  Multiple selects preserve bounded initially selected options and use the
  browser-owned popup for pointer toggles; selected rows are highlighted, enabled toggles retain the popup,
  and the closed control shows the selected count. Enabled selections submit in option order and reset restores
  their initial state. Multiple-select keyboard interaction now uses an independent enabled-option cursor:
  unmodified Up/Down/Home/End/PageUp/PageDown navigate without changing selections or wrapping; page keys use
  the visible popup row count (one when closed), skip disabled options, and leave the cursor unchanged if a
  full step exceeds the list. The cursor starts at the first enabled selected option, otherwise the first
  enabled option, is kept visible/highlighted, follows pointer toggles, and resets with the form.
  Space/Enter opens a closed popup; non-repeated Space toggles while retaining an open popup, Enter dismisses,
  and Escape/Tab/outside clicks dismiss without reverting toggles. Empty/all-disabled options are inert;
  retained repaints preserve cursor/selection. Multiple selects with an explicit `size` 2..12 render as
  browser-owned listboxes without a popup: selected rows are highlighted and the focused cursor outlined, the
  shared row-bounded first visible option supports wheel scrolling and offset pointer toggles of enabled options,
  cursor keys keep the cursor visible, non-repeated Space toggles it, Enter is consumed, and reset restores
  selection/cursor/offset. Absent/invalid/0/1 `size` keeps the popup (no HTML default of four rows); `size` above
  12 is a visible form error. Multiple-select committed-text prefix search moves the independent cursor without changing selections,
  skips disabled options, cycles repeated single-character matches, keeps popup/listbox rows visible and
  shares the single-select scalar/deadline bounds. Ctrl+A selects every enabled option without moving the
  independent cursor; empty/all-disabled lists remain unchanged. Shift+Up/Down extends/contracts the
  enabled-option range from the cursor where Shift selection began, replacing selections outside that range;
  releasing Shift or unmodified cursor movement resets the anchor. Shift+Home/End extends to the first/last
  enabled option with the same anchor. Other Shift/range selection, platform-native behavior and DOM
  keyboard/input/change events remain unsupported. Optgroups flatten
  into the option list, prefix group labels, and propagate disabled group state.
  Date inputs use strict `YYYY-MM-DD` Gregorian values for years 0001–9999,
  sanitize invalid initial values to empty, and validate nonempty values, `required`,
  and valid `min`/`max` bounds before submission; no calendar picker is implemented.
  Bounded date step grids are supported; `pattern`, length, `list`, `multiple` and
  `dirname` are rejected visibly. Time values use strict bounded HTML time
  syntax with generic text editing and validation; no clock picker is implemented,
  while bounded step grids are supported and remaining constraint attributes are rejected.
  Datetime-local combines bounded Gregorian dates (0001–9999) and millisecond-precision
  times without timezones, using generic shell editing, focus and pointer caret placement,
  implicit GET submission, required/min/max and bounded step validation (default 60 seconds).
  Initial/submitted values normalize to `T` and shortest time strings; invalid initial
  values become empty, invalid edits block validation, and reset restores normalized initial
  values. Readonly/disabled and validation bypass use existing form policy; bypassed invalid
  editor text submits empty. Pattern/length/list/multiple/dirname/placeholder semantics
  remain visibly rejected; pickers, localization, timezone UI, wider years/precision and
  DOM form APIs/events remain missing.
  Month values use positive years of at least four digits and months 01–12, with generic
  text editing and validation; no month picker is implemented, bounded step grids are
  supported, and remaining constraint attributes are rejected. Week values enforce valid
  ISO week-years (including the 52/53-week boundary), bounded continuous step grids,
  and generic text editing; no week picker is implemented and remaining constraint attributes
  are rejected. Number inputs support finite floating-point syntax, optional finite min/max
  bounds and step-grid validation; `step=any` disables the grid, absent/invalid
  steps default to 1, and reversed ranges are surfaced as form errors. The step
  base is a valid minimum, otherwise the valid initial value, otherwise zero.
  Grid calculations use bounded binary64 precision and fail closed beyond the
  reliable value-to-step ratio range. Range controls have bounded min/max/step
  metadata (defaults 0–100/step 1), initial range/grid sanitization, keyboard/pointer
  adjustment, bounded GET submission and shell-owned slider painting.
  Textareas support bounded rows/columns, multiline shell editing, measured soft
  wrapping with visual-line-aware caret movement and caret-driven/wheel-controlled
  independent line-window scrolling, plus newline-normalized GET entries. Form-level `novalidate` and a selected submit button's `formnovalidate` bypass supported constraint checks. Disabled fieldsets disable descendant controls except those within their first `legend` child; nested disabled states apply independently.
  `formaction` selects a submitter-specific base-resolved action; textarea
  `wrap=hard` applies bounded explicit-column line breaks only during submission.
  Focused submit and reset controls activate on Enter or Space; Space on a focused
  inert button has no submission/navigation effect.
  Form `target` and first `<base target>` fallback support `_blank` as a new active
  tab; `_self`, `_parent`, and `_top` retain current-tab behavior. Other ASCII
  names target current-window named tabs, reusing when present or creating when
  absent. A selected button/input submitter's `formtarget` overrides that fallback.
  Invalid non-ASCII or whitespace-bearing overrides affect only that submitter
  and fail before value serialization or request/tab startup. None bypass destination or origin restrictions; new-tab destination
  checks run before tab creation, with shared HSTS applied before downgrade checks.
  Visible text/search/email/tel/url/password/date/time/datetime-local/month/week/number/color/checkbox/radio/textarea/select controls support pointer focus without starting page text
  selection. Pointer clicks in single-line text fields and textareas place the
  caret at a shell-font glyph boundary; page-font hit-testing remains absent.
  Editable text-like fields, textareas and the address bar support scalar-safe
  Shift+Arrow/Home/End range selection, shell-owned highlighting, Ctrl+C copy,
  and replacement by committed text, paste, Backspace or Delete. Textarea
  vertical extension follows measured visual lines; Ctrl+A select-all remains
  supported. Single-select
  popup option selection is browser-owned, bounded to 12 displayed rows, and wheel-scrollable.
  Clipboard support remains bounded to selection copy and paste into focused
  editable text/search/email/tel/url/password/date/time/datetime-local/month/week/number/textarea
  fields or the selected address bar. General forms remain
  missing: POST/multipart, other input types beyond text/search/email/tel/url/password/date/time/datetime-local/month/week/number/range/color/checkbox/radio/reset,
  broader textarea behavior and bidi directionality,
  full constraint-validation UI and semantics, submitter overrides other than
  `formaction`/`formtarget`/`formnovalidate`, form events/scripted submission, unsupported `form=` ownership on object, intrinsic widget sizing, control margins, autofill and non-UTF-8 submission
  encodings. See
  [simple GET forms](../docs/browser-shell.md#simple-get-forms).
  Bounded `dirname` is implemented for text/search/email/tel/url/password inputs
  and textarea using the repository's LTR-only scope: a nonempty name of at most
  8192 UTF-16 code units crosses IPC v39 and emits an adjacent `ltr` entry after
  each successful named value. Disabled/unnamed controls emit neither entry;
  read-only values submit both and reset preserves names. Companion names use
  existing CRLF/UTF-8 serialization and form metadata/query limits. Other control
  kinds, empty names, RTL/auto on controls or ancestors and bdi/bdo ancestry fail
  visibly (including controls omitted from layout). Bidi, direction switching,
  DOM events and broader form behavior remain missing.
- A bounded browser-owned selection supports mouse drag across visible non-link
  HarfBuzz text clusters, Ctrl+A to select all visible clusters, Ctrl+C to copy,
  and Escape to clear the selection. SDL-reported double-clicks on visible
  non-link text select the same-line run of letters, numbers, combining marks
  and supported connector punctuation containing the hit cluster; whitespace
  and punctuation remain single-cluster selections. SDL-reported triple-clicks
  select the visible shaped clusters on the hit cluster's visual line, including
  whitespace and punctuation, without crossing lines. Each exact LTR shaping
  cluster is a selection unit, so ordinary supported characters are individually selectable while
  multi-character ligatures and combining clusters remain indivisible; no
  estimated intra-cluster caret positions are used. Primary clicks on anchors
  navigate on release, while a drag beginning over a visible link selects
  clusters instead of activating it. Drags beginning in ordinary text can include
  anchor clusters. Shift+primary click extends an existing cluster selection to a
  visible cluster, including link text without navigating; Shift+click misses keep
  the existing selection unchanged. Shift+Left/Right moves the active endpoint by
  one paint-order cluster, Shift+Up/Down moves to the nearest cluster on an
  adjacent visual line, and Shift+Home/End extends to that line's boundary.
  Selection resets on successful
  document/scroll/resize publication and on page clicks before new control/link
  interaction.
  Bounded root document scrolling is implemented on both axes. Vertical
  scrolling uses wheel, Arrow Up/Down, Page Up/Down, Home/End and Ctrl+Home/End;
  horizontal scrolling uses wheel X, Shift+wheel Y, Arrow Left/Right and its
  browser-owned scrollbar. Both retain DOM/scripts across local/process repaints,
  preserve/clamp per-tab offsets on resize, restore per-history-entry offsets and
  keep layout viewport/frame dimensions fixed. Root horizontal extent is the
  maximum supported measurable right edge, bounded at 10,000,000 CSS pixels;
  negative-left-origin scrolling and nested horizontal overflow are excluded.
  Browser-owned vertical and horizontal scrollbars support click-to-position and drag scrolling.
  Bounded nested vertical wheel scrolling with edge chaining is implemented for
  definite-height block containers styled with `overflow-x:hidden` and
  `overflow-y:auto`; unconsumed wheel delta chains to document scroll. General CSS
  overflow semantics, nested horizontal scrolling and DOM scroll events remain missing.
- The shell does not dispatch general pointer/keyboard input to pages or provide
  general DOM focus management. Pass 9 adds bounded trusted focus/blur
  notifications for changes among existing rendered links and visible enabled
  supported controls on opted-in pages, including pointer focus,
  Tab/Shift+Tab and page/chrome transitions. Blur precedes focus; OS-window
  focus and new focusable elements are excluded. These are plain composed Events
  with no `FocusEvent`, `relatedTarget` or `activeElement`, not general page input.
  It implements only the documented bounded link, form, and control
  interactions. For `--enable-inline-scripts` pages, primary-pointer, Ctrl+primary
  and focused-link keyboard activation dispatch trusted bubbling, cancelable plain
  `click` Events before browser-owned link activation. Middle-click dispatches a
  plain `auxclick` Event before its active-new-tab default. `preventDefault()`
  suppresses those defaults (dom: event dispatch; html: links created by a and area
  elements; pointerevents4: click/auxclick). They are not PointerEvents and expose
  no pointer metadata. Focus/blur share the per-document 1024 trusted-event bound
  with click/auxclick; IPC v48 carries unique bounded per-document target IDs,
  not DOM handles. Modifier/keyboard events, form/control defaults and all other
  browser-generated DOM event delivery remain unavailable to scripts.
- No downloads, persistent browser sessions, or complete history behavior. History
  stores URLs, per-entry two-axis scroll positions and internal same-document grouping:
  Back/Forward between user-created fragment entries retains the active document
  and restores fragment scrolling without a load; cross-document traversal reloads
  at the saved bounded horizontal and vertical scroll positions. Script History
  API and bfcache are absent.

This is a substantial gap even for a modest usable browser.

The development-shell guide's limits summary and scrolling overview now reflect
the implemented bounded link, form, selection, root two-axis scrolling and
nested vertical wheel-scrolling behavior above.
General DOM input events, `tabindex` focus for elements outside the supported
link/form targets, downloads, persistent sessions, complete history, nested
horizontal overflow and unsupported CSS overflow features remain missing; this
increment does not implement general overflow semantics.

Evidence: `page limitations`, `input dispatch`, and `history implementation`.

### 3. Loading complete web pages

Besides the main HTML resource, bounded classic linked stylesheets and CSS imports are loaded:
the browser (never the renderer) fetches supported `link rel=stylesheet` sheets
(media absent or an exact comma-separated list using only `all`/`screen`/`print`,
with lists excluding both `all` and `screen` ignored by the screen-only renderer;
type absent/`text/css`, no title/alternate/disabled/CORS/integrity)
with the tab loader and shared session HSTS store, cookies off, resolved against the
first `<base href>` (falling back to the final response URL). Recursive leading
`@import` rules using a string or `url()` are fetched from embedded and
linked sheets, with relative imports resolved against the containing sheet's final
response URL and expanded at their cascade positions. Optional comma-separated
import media lists containing exact `all`/`screen`/`print` media types are
supported; lists excluding both `all` and `screen` are ignored by the
screen-only renderer. Linked and imported sheets
share a 32-unique-URL cap, 256 Ki character/1 MiB body per sheet and 1 MiB IPC
budget. Same-origin HTTP(S) sheets are restricted to the final document origin
across redirects. Cross-origin HTTP(S) sheets use credentialless CORS: the
document Origin is sent and one final Access-Control-Allow-Origin must match it
or be `*`; redirects are restricted to the effective initial stylesheet origin.
This intentionally narrow policy is not full Fetch CORS. Any stylesheet failure
fails the navigation. Missing pieces include:

- Conditional/layer/late CSS imports, `url()` resources, external scripts (JavaScript
  referenced by a `<script src="...">` URL rather than inline script text), images,
  fonts, media, and frame resources; media queries beyond exact comma-separated
  `all`/`screen`/`print` type lists, alternate/titled linked sheets,
  full CORS/SOP and
  general mixed-content policy for subresources. CSP `upgrade-insecure-requests`
  upgrades brokered linked/imported stylesheet requests and redirects; shell
  navigation rewriting remains limited to documented same-origin links and GET
  forms. HTTP and HTTPS main-document navigation sends `Upgrade-Insecure-Requests: 1`
  on every request hop, without preload-host classification; subresources remain
  excluded. This request signal does not upgrade URLs.
- General resource discovery, scheduling, and lifecycle integration (stylesheets are
  discovered from the pre-script parse only; script-added or changed links fail visibly,
  and there are no load/error events or incremental rendering).
- General HTTP methods/request bodies, streaming responses, HTTP caching, and authentication challenges.
- Full cookie policy and persistence. The browser currently disables cookies; the networking library’s optional cookie subset rejects Domain, SameSite, and Partitioned attributes.
- MIME sniffing. The byte-level HTML charset prescan (`<meta charset>`, pragma, XML declaration; first 1024 bytes) now feeds navigation decoding, but statistical/locale encoding detection, container inheritance and the parser's reparse on a later conflicting declaration are absent.

Unsupported linked-stylesheet semantics are explicitly rejected rather than merely
ignored. When inline page scripting is enabled, classic scripts with a `src`
attribute (for example, `<script src="app.js"></script>`) are rejected; their
source is not fetched or executed.

Evidence: `stylesheet collection`, `script classification`, `HTML loading policy`, and `networking scope`.

### 4. General HTML and DOM compatibility

The tokenizer is considerably more complete than the tree builder. A bounded
static table subset now builds `colgroup`/`col`, table sections, rows and cells,
including implied `colgroup`/`tbody`/`tr` wrappers and scoped closure. Captions
use the existing in-body content subset and reprocess table tokens that close
them. Non-whitespace table-character runs, including adjacent whitespace, are
foster-parented before the nearest table; whitespace-only runs remain in place.
In-table `script`/`style` start tags use script-data/raw-text parsing and return
to their original table insertion mode; script content remains inert.
`type=hidden` inputs are inserted as void table children with a parse-error
diagnostic; other input types remain unsupported in table content.
In table mode, a nested table start closes the current in-scope table and
reprocesses the token; nested tables inside cells remain cell descendants and
restore cell insertion mode when closed. In-table form starts follow the
form-pointer exception and pop the inserted form from the open-element stack;
repeated starts are ignored while the pointer is set. In-table form end tags
use in-body handling. Duplicate `html` start tokens in table, section and row
modes merge missing root attributes without inserting a new element. Nested
tables inside captions and other tokens that require table foster parenting
remain explicitly unsupported; rendering supports only the
[basic automatic table subset](../docs/text-layout.md#basic-automatic-tables). Other remaining
parser work includes:

- Templates, general forms (only the simple form-pointer subset exists), unsupported select contents, SVG/MathML, and other specialized parsing algorithms.
- Fragment parsing.
- Adoption-agency/active-formatting recovery for malformed markup.

Legacy public/system doctypes now recover with `invalid-doctype` diagnostics
instead of rejecting the page. The complete HTML initial-mode identifier
tables select no-quirks/limited-quirks/quirks mode using ASCII comparisons,
preserve inert identifiers and never fetch DTDs. This removes a parser blocker,
not the remaining CSS/table/image rendering gaps or a claim of full quirks
layout support.
Validation: 35 parser classification/recovery cases plus a native content
fixture pass; one prior unsupported case is replaced by positive coverage.
The solution build and all 14,308 tests pass locally. Formatting reports only
the three previously documented baseline findings.

The DOM also lacks full Web IDL bindings, specialized HTML element behavior,
script-visible form state and APIs, custom-element reactions, and other browser APIs;
the browser-shell form state is a separate bounded shell-owned subset.

These are compatibility blockers: unsupported constructs can reject a page.

Evidence: `HTML tree-building scope`; focused tests cover the simple table subset,
including captions and fostered text; broader table conformance fixtures remain
outside the supported scope.

### 5. General CSS, layout, and painting

The browser now accepts optional trusted `--font-bold`, `--font-italic` and
`--font-bold-italic` paths alongside its required regular font. Local and worker
rendering use exact 700 normal, 400 italic and 700 italic faces for ordinary
headings, bold/emphasis and combinations. Workers load and retain their own
native resources; Linux mounts only the selected files read-only, and Windows
stages only those files. Missing faces/files still fail visibly. This does not
add automatic family selection, synthesis, nearest-weight matching, weight 900,
oblique, variable axes or downloaded fonts. See
[explicit font configuration](../docs/browser-shell.md#run-it).
Validation adds 51 regressions: pinned native faces and exact styled pixels,
missing/partial/duplicate configuration, strict worker startup, local/process
shell wiring, confined/scripted workers and retained resize. Build and all
14,408 solution tests pass, as do the Linux denial smoke and confined CLI
four-face smoke. Formatting retains only the three established baseline findings.

Standard named colors now cover all 148 opaque CSS Color 4 names, rather than
only the 16 basic colors plus orange/rebeccapurple. Common ordinary stylesheet
values such as lightgray, whitesmoke and darkslategray no longer reject a page.
Exact sRGB channels, gray/grey aliases and case/escape handling use the existing
bounded color parser across text, backgrounds and borders. This does not add
system colors, new functional color spaces, gradients or broad color management.
Its 155 regressions, solution build and all 14,563 solution tests pass locally;
formatting reports only the three established baseline findings.

Modern space-separated `rgb()` and `rgba()` now use the same bounded sRGB
output, including mixed number/percentage components, optional slash alpha,
and static `none` components. Legacy comma syntax remains supported.
Missing-component preservation and interpolation remain unsupported; see the
[CSS color scope](../docs/css.md#cascade-and-computed-values).
Its 35 regressions, solution build and all 14,711 solution tests pass locally;
formatting reports only the three findings reproduced on base `ad0bca6`.

The finite color parser now also accepts in-gamut `hsl()`/`hsla()` syntax,
including hue angles, legacy commas, modern components and alpha. Values
requiring out-of-gamut mapping remain diagnostics; other color spaces and
interpolation remain unsupported. See the [CSS color scope](../docs/css.md#cascade-and-computed-values).
Its 50 regressions, solution build and all 14,761 solution tests pass locally;
formatting reports only the three findings reproduced on base `ad0bca6`.

The finite `background` shorthand now accepts one supported color and optional
`none`, with transparent reset when the color is omitted. CSS-wide keywords,
currentColor, inline/source-order/important cascade and existing limits apply.
Images, multiple layers and positioning/sizing/repeat/attachment/box components
remain explicit whole-declaration diagnostics, not partial color application.
Its 31 regressions, solution build and all 14,594 solution tests pass locally;
formatting reports only the three established baseline findings.

The finite `font` shorthand now expands style/weight/size/line-height/family
atomically, with mandatory size/family, optional style/weight and slash
line-height, CSS-wide keywords and resets of omitted supported fields.
Existing units, relative sizes/weights and family lists are reused.
Non-normal variants/widths, oblique angles and system fonts remain diagnosed.
This does not add new font longhands, downloads, fallback or synthesized faces.
Its 52 regressions, solution build and all 14,646 solution tests pass locally;
formatting reports only the three established baseline findings.

CSS absolute lengths `in`, `cm`, `mm`, `Q`, `pt` and `pc` now use their fixed
CSS-pixel ratios in the existing length-valued properties and shorthands.
Viewport units and additional font-relative units remain unsupported.
See the [CSS length scope](../docs/css.md#cascade-and-computed-values).
Thirty new regressions, the solution build and all 14,676 solution tests pass
locally. Formatting reports only the three findings reproduced on base `ad0bca6`.

The static `:any-link` pseudo-class matches HTML `a` and `area` elements with
an `href` attribute in stylesheets and DOM queries. It follows current attribute
presence, including empty or non-navigable values, without changing navigation
policy or exposing history. `:link` and `:visited` remain unsupported. See the
[static selector scope](../docs/css.md#static-selectors). Its 28 regressions,
the solution build and all 14,789 solution tests pass locally; local V8 smoke
passes. Formatting reports only the three findings reproduced on base `ad0bca6`.

The screen-only CSS engine now evaluates nested `@media` groups with an empty
or comma-separated `all`/`screen`/`print` list. Active groups retain their
cascade positions; print-only contents are skipped. Unsupported conditions and
active nested imports remain diagnostics, and nested imports are never fetched.
This does not implement feature queries, device-dependent evaluation or general
conditional rules. See [screen media-type rules](../docs/css.md#screen-media-type-rules).
Its 36 new regressions, solution build and all 14,825 solution tests pass locally.
Formatting reports only the three findings reproduced on base `ad0bca6`.

The finite CSS cascade accepts `text-decoration:none` and
`text-decoration-line:none`, including the existing `initial`, `inherit`,
`unset` and `revert` CSS-wide keywords. This prevents a common declaration from
rejecting an otherwise renderable page, without adding or inhibiting any
visible decoration. Line drawing, decoration style/color/
thickness and default link underlines remain unsupported. See the
[no-decoration scope](../docs/css.md#cascade-and-computed-values).
Its 36 new regressions, solution build and all 14,888 solution tests pass locally.
Formatting reports only the three findings reproduced on base `ad0bca6`.

The current engine implements finite static block/inline rendering. Missing essentials include:

- Flexbox, grid, broader tables, floats and positioning; margin collapse is implemented
  for adjacent block siblings and eligible parent/first-or-last block-child edges.
- Conditional/feature-based media queries, custom properties, calculations, modern selectors, and broader CSS properties. (`:scope`, `:any-link` and filtered `:nth-child(An+B of S)`/`:nth-last-child(An+B of S)` are supported for stylesheets and DOM queries; `:has`, visited/dynamic state, namespaces and pseudo-elements remain missing.)
- Images/replaced elements, general controls and broader list
  styling, decorations, nested scrolling, and general overflow layout. Root
  horizontal document scrolling is browser-owned and does not implement CSS overflow.
- Default unordered lists now render with outside disc bullets, including
  nested and empty items. UA items use `display:list-item`; inherited
  `list-style-type:disc/square/decimal/none`, outside positioning and no marker
  image are supported. Graphical markers use bounded geometry and current-color
  ellipse or rectangle painting, not synthetic DOM text. Ordered lists use separately shaped
  decimal markers with `start`, `reversed` and `li value`; nested lists have
  independent numbering, hidden/non-list-items do not count, and marker
  suppression preserves numbering. CSS square markers reuse the same bounded
  outside position and current-color paint as discs, using a filled rectangle;
  nested UA defaults remain discs. Numbering attributes accept HTML integer
  syntax within signed 32-bit limits and fail visibly beyond that range.
  Legacy `type` attributes except `ol type=1`,
  other marker styles, inside positioning and
  arbitrary list-item elements remain unsupported. See the
  [list layout scope](../docs/text-layout.md#default-unordered-lists) and
  [paint guide](../docs/painting.md#paint-order-and-supported-output).
  Local validation: the decimal increment adds 19 regressions covering CSS,
  exact numbering/geometry/budgets, native marker pixels, offline content and
  local/process retained repainting; the solution build and all 14,253
  solution tests pass. Solution formatting still reports
  only the three existing findings reproduced on base commit `81bb414`.
  The finite `list-style` shorthand supports disc/square/decimal/none, outside
  positioning, image none, resets and CSS-wide keywords; invalid or unsupported
  components diagnose the complete declaration atomically instead of partially
  applying values. Its initial 20 CSS/native-content regressions, solution build
  and all 14,273 solution tests pass locally; formatting retains only the
  same three documented baseline findings.
  The square-marker increment adds 23 CSS, layout, paint, content and browser
  regressions. The solution build and all 14,911 solution tests pass locally;
  formatting reports only the three findings reproduced on base `ad0bca6`.
- Transforms, gradients, rounded borders, stacking contexts, and advanced compositing.

Nonwrapping `white-space:pre/nowrap` now accepts supported Latin/common
punctuation (including ordinary URL and hyphenated text) without soft wrapping;
spaces/forced breaks and horizontal overflow remain mode-appropriate.
`normal`/`pre-line` now also allow a soft break after an ASCII hyphen between
ASCII letters in one text node, with bounded break-search work and retained
source identity. A solidus between ASCII letters in one text node stays in
the same unbroken segment; this also works with supported hyphen breaks.
Other solidus forms, punctuation/Unicode-break algorithms, cross-node word
shaping, unsupported scripts/bidi/controls and missing-font handling remain
unsupported.
See [ordinary text flow](../docs/text-layout.md#ordinary-flow).
The earlier nonwrapping increment's 17 new regressions, solution build and all
14,357 solution tests pass; formatting retains only the three established
baseline findings.
The internal-hyphen increment adds 27 regressions covering deterministic
wrapping, work limits, intrinsic table sizing, native pixels, retained resize,
link geometry and visible text selection. The solution build, all 138 Layout,
26 Content, 1,200 Browser and 14,852 solution tests pass locally. Formatting
reports only the three findings reproduced on base `ad0bca6`.
The internal-solidus increment adds 33 regressions covering unbroken runs,
explicit exclusions, mixed hyphen work limits, table sizing, native pixels and
local/process retained link selection. The solution build, all 171 Layout,
29 Content, 1,206 Browser and 14,944 solution tests pass locally. Formatting
reports only the same three baseline findings; changed test files pass separately.

Basic canonical HTML tables now support automatic intrinsic column sizing,
separate borders, one spacing length, top-aligned text/block cells and
table/cell backgrounds through ordinary paint flow. Explicit table width and
cell heights act as minima; header/footer groups are ordered around body groups.
Spans, captions/columns, anonymous/arbitrary CSS tables, collapsed borders,
fixed layout, nested tables/widgets, percentages inside cells, row/group
backgrounds and legacy presentation attributes remain unsupported. See the
[exact supported scope and limits](../docs/text-layout.md#basic-automatic-tables).
The table increment adds 32 regressions covering CSS, intrinsic/actual geometry,
exact work limits and cancellation, native pixels, and local/process
scroll/link/retained resize behavior. Build, touched suites and all 14,340
solution tests pass locally. Formatting reports only the three established
baseline findings, not new table findings.

**A particularly restrictive current limitation:** parent-edge margin collapsing is limited to eligible first/last block-child edges without intervening content, borders or padding (bottom propagation additionally requires auto height and zero min-height). Some CSS values parse successfully but remain unsupported downstream in layout or paint.

Evidence: `CSS scope`, `layout restrictions`, and `painting limitations`.

### 6. A persistent browser JavaScript environment

V8 and substantial bounded DOM bindings **are implemented**. JavaScript is not wholly missing.

However:

- Page scripting is disabled by default.
- Enabled scripts are post-parse inline classics only.
- There is no persistent HTML event loop. Opted-in documents now retain their
  renderer-local host/listeners after initial execution, but only for bounded
  trusted link-event dispatch (1024 click/auxclick events combined per document; failures invalidate the
  host until reload); no timers, tasks or other callbacks run later.
- Modules, external scripts (`<script src="...">` resources), timers, workers,
  script-visible networking/storage, and full Window/Web IDL behavior are
  unimplemented and lower-priority work.
- Synthetic events, finite lifecycle/microtask processing and the narrow link-click
  dispatch exist, but not general ongoing page interaction.
- WebAssembly is explicitly disabled in the current host.
- Clone cancellation coverage distinguishes pre-canceled calls (which do not
  invalidate an unused host) from cancellation during a still-running script after
  a successful clone. The script continues running after cloning so timer delays
  cannot let the cancellation regression finish without observing cancellation.

Evidence: `scripting scope`, `short-lived page script host`, and clone-cancellation
regressions.

### 7. Internationalization, accessibility, and broader web APIs

Remaining areas include:

- RTL/bidi, complex-script shaping, Unicode line breaking, font fallback, and automatic font matching.
- IME composition/preedit and accessibility integration. Clipboard support is
  bounded to browser-owned page-text copying and pasting or range copying in
  editable text/search/email/tel/url/password/date/time/datetime-local/month/week/number/textarea
  fields and the address bar. Page-text caret stops inside multi-character
  shaping clusters remain unsupported.
- Storage APIs, service workers, canvas, audio/video, WebGL/WebGPU, and the wider browser API surface.

Evidence: `text/layout exclusions`, `platform input limitations`, and `standards-growth roadmap`.

### 8. Distribution and comprehensive validation

Linux and Windows implementation and native CI configurations exist; Windows support should not be described as merely a placeholder.

Still outstanding:

- Real desktop validation for Wayland keyboard/IME and high-DPI behavior.
- Broader conformance coverage: current tests exercise selected WPT/html5lib fixtures, not the full web platform.
- Routine Linux confinement/resource-exhaustion coverage: Linux CI runs confined-renderer pixel/crash-restart, rejected-handshake/no-fallback, per-tab resource-scope isolation/cleanup, task-limit recovery, bounded CPU-throttle cancellation/deadline, inline-script deadline recovery, isolated kernel-OOM renderer/controller recovery, and tab-close-under-CPU-pressure cleanup integration tests.
- Production packaging, installation/update workflows, and deployment hardening. Current confinement requires trusted framework-dependent deployments; Linux self-contained/single-file deployment is unsupported.

Evidence: `platform validation`, `HTML conformance coverage`, `Linux CI`, `Windows CI`, and `deployment prerequisites`.

## Recommended priority

Prioritize handling simple websites end to end before advanced web applications.
Start with mostly static, document-oriented pages: readable text, ordinary
styling, images, working links, scrolling and basic forms. Choose small,
testable increments that remove concrete blockers on representative pages;
do not require completion of an entire subsystem before improving another.

1. **HTML recovery and ordinary rendering** for simple static pages. Prioritize
   common markup, lists, basic tables, common CSS, ordinary block/inline layout,
   and font handling so pages parse and remain readable instead of failing on
   routine constructs.
2. **Essential page resources** so those pages render as intended. Build on
   existing linked/imported stylesheet loading with images, required resource
   discovery and sizing, and commonly encountered stylesheet features. Broader
   media, frames and advanced resource scheduling have lower priority.
3. **Reliable basic navigation and interaction.** Build on existing links,
   root scrolling, selection and simple GET forms; prioritize remaining
   scrolling/overflow, keyboard focus and form issues that prevent using simple
   sites. Avoid adding advanced input APIs ahead of basic usability.
4. **Small amounts of page JavaScript** once the static browsing path works.
   Prioritize external classic scripts (`<script src="...">`), the DOM/events
   needed by simple interactions, and bounded scheduling/lifecycle integration.
   Full application frameworks, modules, workers and the wider browser API
   surface have lower priority.
5. **Broader production security and isolation** beyond the existing protections,
   including general request authorization and wider SOP/CORS/CSP coverage,
   before claiming safe browsing of arbitrary untrusted sites. Completing this
   entire backlog is not a prerequisite for simple-site compatibility work.
6. **Broader standards, accessibility, advanced layout/media/APIs and release
   engineering** as coverage expands beyond simple websites.

Preserve existing confinement, request restrictions, explicit development
opt-ins and fail-closed behavior throughout all priorities. Add the security
checks needed by each new resource or scripting capability as part of that
increment; this ordering does not authorize weakening safeguards or imply that
simple websites are safe to browse untrusted. Validate each increment with
focused regressions and representative offline simple-site fixtures.

_Priority order updated on 2026-10-28 to favor simple-site usability first;
this is a work-order change, not a new implementation or validation claim._

*Status cross-checked against the implementation and subsystem guides on
2026-10-19. Native Linux x64/ARM64 and Windows x64/ARM64 CI passed for commit
`bc22600` ([Linux](https://github.com/khmseu/VisualWeb/actions/runs/39194869543),
[Windows](https://github.com/khmseu/VisualWeb/actions/runs/39194869712)). These checks are not full conformance, security, CI-matrix or
real-desktop validation.
