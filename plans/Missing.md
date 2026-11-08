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

HTML user-agent phrasing defaults now apply `font-size:larger` to `big`,
`font-size:smaller` to `small`, italic style to `dfn`, and the monospace family
to `tt`. Relative sizes reuse the existing parent-relative computation and
ordinary cascade. Shaping still requires explicitly registered matching faces,
including italic for `dfn`; there is no fallback or synthesis. Other HTML
phrasing rules and cross-style unbroken-word layout remain unsupported. See
the [phrasing font scope](../docs/css.md#cascade-and-computed-values).
Its 27 new CSS, layout, native-content and local/process browser cases pass;
the solution build, all 15,780 solution tests, solution formatting, local and
confined V8 smokes, and Linux confinement smoke also pass locally.

The HTML UA stylesheet now colors `:link` text `#0000EE`, so otherwise
unstyled links on simple pages are visually distinguishable. The existing
history-independent matcher treats every `a`/`area` with an `href` as
unvisited, even when navigation to its value is blocked. Normal cascade and
inheritance apply; disabling the UA sheet removes the default. Visited/active
styling and cursor behavior remain unsupported. The UA link underline is
described with the bounded text-decoration support below. See the
[link color scope](../docs/css.md#cascade-and-computed-values).
Its 26 new CSS, native-content and local/process browser cases pass, including
exact shared limits and scripted attribute recomputation. The solution build,
all 15,806 solution tests, solution formatting and local V8 smoke pass.

HTML `<font color>` now supplies a bounded presentational color hint using the
full HTML legacy color algorithm, including non-CSS malformed values, rather
than CSS color parsing. Normal cascade, inheritance and recomputation apply;
aggregate raw attribute processing has a configurable limit. `font face`
remains unsupported. See [CSS hint scope](../docs/css.md#cascade-and-computed-values).
All 1,053 CSS and 78 Content tests, focused local/process browser cases,
solution build, formatting and local V8 smoke pass. The serial full run passed
16,009 cases but failed the unchanged
`UpgradeInsecureRequestsUpgradesPageLinkAndRedirectBeforeTransport` test while
authorization remained pending after all expected requests. The unmodified
test passed on base `257f0b1`; with a test-only 150 ms delay before its first
upgraded request, the same base test intermittently failed from duplicate
requests or a resource deadline. Those failures do not reproduce the exact
pending-authorization symptom or prove its cause. Three current focused
repetitions passed, and the temporary baseline worktree was removed.

HTML `<font size>` now supplies a separate bounded presentational hint using
the HTML legacy size algorithm and existing CSS absolute-size mapping.
Signed sizes are relative to 3 rather than the parent; parsing, cascade,
inheritance and recomputation are covered through native rendering and
local/process retained resize. An independent configurable aggregate raw UTF-16
budget bounds size attribute work. The solution build, all 16,043 solution
tests, formatting and local V8 smoke pass. Long inputs and pre-cancelled
computations are tested, but cancellation specifically during a scan has not
been deterministically tested. At the end of that increment `font face` and
broader font support remained open; that increment did not complete the priority.

HTML `<font face>` now supplies a bounded presentational `font-family` hint
parsed with the existing CSS family grammar. Valid family lists follow the
normal author-hint cascade, inheritance and recomputation; the shaper checks
listed names in order against explicitly registered faces, without implicit
fallback, downloads or synthesis. A separate aggregate raw UTF-16 budget counts
invalid as well as valid attributes. CSS tests (1,129) and Content tests (91)
pass, including native pixel comparison, retained resize, exact UTF-16 units,
and explicit failure when no listed face is registered. The full solution
suite (16,120 tests), solution build (zero warnings/errors) and formatting
verification also pass.

Canonical table layout in [TableLayout.cs](../src/Engine/Engine.Layout/TableLayout.cs)
supports bounded horizontal `colspan` and bounded `rowspan`. Column spans contribute intrinsic minimum/maximum constraints that
are shared equally across covered columns in increasing span order; arrangement
uses the combined track widths and internal spacing. Positive row spans are
capped at 65,534; `rowspan=0` covers the remaining rows of the explicit row
group, and spans cannot escape that group. Occupied slots shift later cells to
the first free contiguous run, with collision checks during grid reservation.
Any spanning-cell height deficit, including internal row spacing, is shared
equally across covered rows. Table, row-group and row backgrounds paint before
cell backgrounds so later rows cannot cover spanning cells.
`LayoutOptions.MaxTableColumns` (default 100,000) rejects oversized grids
before allocation. Repeated track work shares
`LayoutOptions.MaxTableColumnOperations` (default 10,000,000); row-span grid
and height work has the separate
[`LayoutOptions.MaxTableRowSpanOperations`](../src/Engine/Engine.Layout/StaticLayout.cs)
limit (default 1,000,000). Attribute scanning retains the shared text and
cancellation budgets. Focused layout tests cover geometry, row-group clipping,
HTML parsing and limits; paint tests verify overlapping row/cell background
order. Remaining table gaps include anonymous/arbitrary tables and other listed
exclusions.
The preceding `colspan` increment remains recorded in commit `3e568b1`:
layout (807), Content (92), all 16,130 solution tests, solution build and
format verification passed; Linux, Windows confinement and CodeQL CI passed
(runs 41342219697, 41342219768 and 41342218952).

Pass 4 implements positive `rowspan` values and `rowspan=0` through the end of
the current explicit row group, with collision-checked slot placement and
even row-height deficit distribution including internal spacing. The independent
`MaxTableRowSpanOperations` limit bounds grid and height work. Focused layout
(821) and paint (69) suites, the zero-warning solution build, all 16,163
solution tests and solution formatting verification pass locally. Commit
`a79f119` is pushed. Linux x64/arm64, Windows x64 and CodeQL CI passed; the
first Windows arm64 run hit a duplicate-request failure in the unrelated
`UpgradeInsecureRequestsUpgradesPageLinkAndRedirectBeforeTransport` browser
test, and the isolated arm64 rerun passed (run 41367865554).

Pass 5 implements one first-element-child HTML caption above a canonical
table. `display:table-caption` is accepted and supplied by the UA stylesheet;
the wrapper matches the table border-edge width, and caption content does not
affect intrinsic columns. Caption and table backgrounds/borders use separate
geometry. `caption-side`, bottom captions and other documented table
exclusions remained unsupported at the end of Pass 5. CSS (1,139), layout (823)
and paint (70) focused suites pass locally. The zero-warning solution build,
all 16,168 solution tests and formatting verification pass. Commit `b0d0441`
is pushed; Linux development, Windows confinement and Push on main workflows
pass (runs [41387361158](https://github.com/khmseu/VisualWeb/actions/runs/41387361158),
[41387361276](https://github.com/khmseu/VisualWeb/actions/runs/41387361276)
and [41387360887](https://github.com/khmseu/VisualWeb/actions/runs/41387360887)).

Pass 6 adds canonical column/group displays and bounded HTML `span` expansion
for `colgroup`/`col`. Nonnegative CSS widths computed to pixels on a column
constrain that track's minimum and maximum; a group width raises the minimum
across its tracks, shared evenly. Percentage widths and backgrounds on either
element remained unsupported at that pass. CSS (1,140), Layout (834), the
zero-warning solution build and
format verification pass locally. The complete solution test run passes
(15,786 tests). Commit [`c677ceb`](https://github.com/khmseu/VisualWeb/commit/c677ceb95714b3acfb8491dfb027274ef3998ec9)
is pushed; Linux development, Windows confinement and Push on main CI pass
(runs [41403577075](https://github.com/khmseu/VisualWeb/actions/runs/41403577075),
[41403577187](https://github.com/khmseu/VisualWeb/actions/runs/41403577187) and
[41403576947](https://github.com/khmseu/VisualWeb/actions/runs/41403576947)).

Pass 7 adds bounded column-group and column `background-color` layers. Layout
stores one complete cell rectangle for each background on the cell's originating
group/column, so spans crossing track/group boundaries retain CSS 2.2 coverage.
The painter orders these layers below row-group/row/cell backgrounds; alpha
composites through the existing raster path, and the layer geometry shares table
operation and paint-command limits. Percentage widths and other table gaps
remain unsupported. Layout (833) and Paint (71), the zero-warning solution
build, all 16,180 solution tests and solution formatting verification pass
locally. Commit `879982f` is pushed; Linux x64/arm64, Windows x64/arm64 and
Push on main CI pass (runs [42340535912](https://github.com/khmseu/VisualWeb/actions/runs/42340535912),
[42340535879](https://github.com/khmseu/VisualWeb/actions/runs/42340535879) and
[42340535497](https://github.com/khmseu/VisualWeb/actions/runs/42340535497)).

Pass 10 adds row-group and row `background-color` support, painted in CSS 2.2
table-layer order beneath cells. Layout (807), Content (93), the
zero-warning solution build, all 16,131 solution tests and solution formatting
verification pass locally; column backgrounds were unsupported at that earlier
point and are implemented by Pass 7 above.

The HTML UA stylesheet now applies the ordinary `hidden` rendering rule through
the normal cascade. An override may make a hidden element visible, but
descendants cannot escape an ancestor whose computed display remains `none`.
Hidden controls remain eligible for submission under the normal successful-control
rules, and hidden scripts/styles remain active. Button labels omit computed
`display:none` descendants; a rendered select with such descendants fails
explicitly because popup visibility is not yet separate from option selection
and submission. The first matching DOM ID, or named anchor when no ID matches,
blocks later duplicates from becoming fragment scroll targets even if hidden.
Visible `hidden=until-found`, its reveal/`beforematch` behavior, and specialized
`<embed hidden>` layout remain unsupported. See the
[hidden rendering scope](../docs/css.md#cascade-and-computed-values)
and [layout boundaries](../docs/text-layout.md#layout-entry-point).
Widget and ancestor fragment targets use rendered geometry; protocol limits
apply to emitted identities, while ancestor traversal is separately bounded and
cancellable. The solution build, all 15,907 solution tests, format verification
and local V8 smoke pass.

The HTML UA stylesheet gives `listing`, `xmp` and `plaintext` the existing
`pre` block, 1em vertical margin, monospace and preserved-whitespace defaults.
Their distinct parser modes remain unchanged: `listing` parses ordinary markup,
while apparent markup inside `xmp` and `plaintext` remains literal. Focused
CSS, HTML, layout, native-content and local/process browser tests pass;
the solution build, all 15,943 solution tests, format verification and local
V8 smoke also pass. Broader whitespace algorithms remain unsupported.

HTML `search` now receives the standard UA `display:block` default through
the normal cascade. The existing parser closes an open paragraph at its start,
and ordinary block and form descendants use the existing layout path instead
of being rejected as block descendants of an inline. Focused CSS, layout,
native-content and local/process browser tests pass, including process-backed
GET submission and exact matched-selector limits. The solution build, all 15,953
solution tests, format verification and local V8 smoke also pass. This adds no
search widget or new form, accessibility or layout algorithm.

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
policy or exposing history. See the
[static selector scope](../docs/css.md#static-selectors). Its 28 regressions,
the solution build and all 14,789 solution tests pass locally; local V8 smoke
passes. Formatting reports only the three findings reproduced on base `ad0bca6`.

The static `:link` pseudo-class now matches the same HTML hyperlinks, treating
all links as unvisited without consulting browsing history. This
privacy-preserving policy follows Selectors Level 4; `:visited` remains an
explicit unsupported selector, including in mixed selector lists. CSS cascade,
live DOM queries and retained page rendering share the existing matcher.
The increment adds 30 regression cases and removes two obsolete unsupported-selector cases.
The solution build, local V8 smoke, all 816 CSS, 352 Scripting, 30 Content,
1,212 Browser and 14,972 solution tests pass locally. Formatting reports only
the three findings reproduced on base `ad0bca6`; changed C# files pass separately.

The screen-only CSS engine now evaluates nested `@media` groups with an empty
or comma-separated `all`/`screen`/`print` list. Active groups retain their
cascade positions; print-only contents are skipped. Unsupported conditions and
active nested imports remain diagnostics, and nested imports are never fetched.
This does not implement feature queries, device-dependent evaluation or general
conditional rules. See [screen media-type rules](../docs/css.md#screen-media-type-rules).
Its 36 new regressions, solution build and all 14,825 solution tests pass locally.
Formatting reports only the three findings reproduced on base `ad0bca6`.

The finite CSS cascade accepts `none`, `underline` and `line-through` for the
`text-decoration` shorthand and `text-decoration-line`, including the existing
`initial`, `inherit`, `unset` and `revert` CSS-wide keywords. The UA `:link`
rule supplies both its unvisited blue color and an underline; `ins` and `u`
also receive the HTML underline default, while `del`, `s` and `strike` receive
the HTML line-through default. Paint uses each decorating ancestor's color and
exact registered OpenType metrics for underlines and line-throughs; descendant
`none` does not cancel an ancestor decoration, and missing line-through metrics
fail explicitly. Overline, other decoration
style/color/thickness values, atomic-inline decoration and skip-ink remain
unsupported.

Bounded solid inline `background-color` now creates one merged rectangular
fragment per text-backed non-replaced inline element and line. Fragments use
laid-out text/space advances, paint before their line's text with ancestor
layers first, and are limited by `LayoutOptions.MaxInlineBackgroundOperations`
(default 1,000,000). Inline margins/padding/borders and backgrounds on or around
atomic widgets or line breaks remain explicit failures; image/gradient
backgrounds, clipping, radii and zero-advance ink remain unsupported. The HTML
`mark` UA default integrates this path with yellow background and black text;
author overrides remain in the normal cascade. See the [layout](../docs/text-layout.md#ordinary-flow),
[CSS](../docs/css.md) and [painting](../docs/painting.md) guides.
Validation: the full solution build passed with zero warnings, all 16,154
solution tests passed, and `dotnet format VisualWeb.slnx --verify-no-changes
--no-restore --exclude third_party` passed.
Commit `7bd6e99` is pushed. Linux x64/arm64, Windows x64/arm64 and CodeQL
checks passed ([Linux](https://github.com/khmseu/VisualWeb/actions/runs/41359886765),
[Windows](https://github.com/khmseu/VisualWeb/actions/runs/41359886868),
[CodeQL](https://github.com/khmseu/VisualWeb/actions/runs/41359886768)).

The earlier underline-only parser increment added 36 regressions and its
extension added CSS default/cascade, native underline-pixel and descendant-
propagation coverage. CSS (1,102), Paint (65) and Content (88) tests passed;
the solution build and formatting verification passed. The changed-tree full
suite passed 16,077 tests on retry and the base suite at `3d00c0f` passed
16,072. An earlier local full run and first Windows x64 CI run timed out in the
unchanged `UpgradeInsecureRequestsUpgradesPageLinkAndRedirectBeforeTransport`
test; both the local full-suite retry and Windows x64 CI rerun passed, but no
root cause was established. Commit `c6f13b1` was pushed and Linux, Windows
x64/arm64 and CodeQL CI passed.

Pass 1 adds CSS line-through cascade/defaults, paint-command/metric, native
pixel/scroll and retained content-pixel coverage. Focused CSS (1,134), Paint
(67) and Content (96) suites pass. The solution build has zero warnings, all
16,141 solution tests pass, and formatting verification passes. Commit
`f7a83d5` is pushed; Linux, Windows x64/arm64 and CodeQL CI pass (runs
41350401406, 41350401385 and 41350401525).

Pass 2 adds the HTML UA underline defaults for `ins` and `u` through the
existing cascade and paint path. CSS (1,136) and Content (98), the zero-warning
solution build, all 16,145 solution tests and solution formatting verification
pass locally. Commit, push and CI status remain to be verified.

The current engine implements finite static block/inline rendering. Missing essentials include:

- Flexbox, grid, broader tables, floats and positioning; margin collapse is implemented
  for adjacent block siblings and eligible parent/first-or-last block-child edges.
- Conditional/feature-based media queries, custom properties, calculations, modern selectors, and broader CSS properties. (`:scope`, `:any-link`, `:link` and filtered `:nth-child(An+B of S)`/`:nth-last-child(An+B of S)` are supported for stylesheets and DOM queries; `:has`, visited/dynamic state, namespaces and pseudo-elements remain missing.)
- Images/replaced elements, general controls and broader list
  styling, decorations, nested scrolling, and general overflow layout. Root
  horizontal document scrolling is browser-owned and does not implement CSS overflow.
- Default `ul` and `menu` unordered lists now render with outside markers, including
  nested and empty items. UA items use `display:list-item`; inherited
  `list-style-type:disc/circle/square/decimal/decimal-leading-zero/none`, the alphabetic and Roman
  styles `lower-alpha`/`upper-alpha`/`lower-roman`/`upper-roman` and the
  `lower-latin`/`upper-latin` aliases, outside positioning and no marker image
  are supported. Graphical markers use bounded geometry and current-color
  ellipse or rectangle painting, not synthetic DOM text. Ordered lists use separately shaped
  decimal (including zero-padded), ASCII alphabetic or Roman markers with `start`, `reversed` and `li value`;
  alphabetic ordinals wrap bijectively and fall back to decimal at zero or below.
  Roman styles use decimal fallback outside 1..3999, not extended overlines.
  Zero-padded decimal counts a negative sign toward its two-character minimum.
  Nested lists have
  independent numbering, hidden/non-list-items do not count, and marker
  suppression preserves numbering. CSS square and hollow circle markers reuse
  the same bounded outside position and current color as discs, using a filled
  rectangle or a single clipped, inset outline command respectively. The
  HTML UA marker defaults now use circles with one list ancestor and squares
  with two or more; ordered lists remain decimal, author styles still override
  UA rules. `menu` has `ul`-equivalent block/padding and bounded list layout;
  `dir` participates only in CSS marker rules.
  HTML UA list margins are 1em above and below outer lists and zero on
  descendants of `dir`/`dl`/`menu`/`ol`/`ul`; vertical writing and
  `dir` list layout remain unsupported.
  Numbering attributes accept HTML integer
  syntax within signed 32-bit limits and fail visibly beyond that range.
  Legacy `type` hints support ASCII-case-insensitive disc/circle/square/none
  on `ul`/`li` and exact `1`, `a`, `A`, `i` and `I` on `ol`/`li`, using the author presentational
  hint origin and shared CSS assignment budget. `menu` attributes `type`,
  `start` and `reversed` do not affect list rendering. Unsupported or malformed
  types on the supported hint elements, other marker styles, inside positioning
  and arbitrary list-item elements remain explicit failures. See the
  [list layout scope](../docs/text-layout.md#default-unordered-lists) and
  [paint guide](../docs/painting.md#paint-order-and-supported-output).
  Local validation: the decimal increment adds 19 regressions covering CSS,
  exact numbering/geometry/budgets, native marker pixels, offline content and
  local/process retained repainting; the solution build and all 14,253
  solution tests pass. Solution formatting still reports
  only the three existing findings reproduced on base commit `81bb414`.
  The earlier finite `list-style` shorthand increment supported
  disc/square/decimal/none, outside positioning, image none, resets and CSS-wide
  keywords; invalid or unsupported components diagnose the complete declaration
  atomically instead of partially applying values. Its initial 20 CSS/native-content
  regressions, solution build and all 14,273 solution tests passed locally;
  formatting retained only the same three documented baseline findings.
  The square-marker increment adds 23 CSS, layout, paint, content and browser
  regressions. The solution build and all 14,911 solution tests pass locally;
  formatting reports only the three findings reproduced on base `ad0bca6`.
  The circle increment extends the same shorthand and covers CSS cascade,
  geometry and limits, transparent center and single-composite pixels, large
  scroll offsets, offline content, and retained local/process browser repainting.
  The solution build and all 15,025 solution tests pass locally, with 53 net new
  cases. Solution formatting reports only the three findings reproduced on
  base commit `b47a5a0`; other changed C# files pass separately.
  The subsequent nested-UA increment changes the earlier all-disc nested
  default. The solution build and all 15,045 solution tests pass locally;
  formatting reports only those same three baseline findings, while all
  C# files changed in this increment pass a targeted format check.
  The nested-UA margin increment adds 20 CSS/layout/content/browser
  regression cases. The solution build and all 15,065 solution tests pass;
  formatting reports only the same three baseline findings, and changed C#
  files pass a targeted format check.
  The list-type presentational hint increment adds 32 net CSS, layout,
  native-content and local/process browser regression cases. Its solution
  build, V8 smoke and all 15,097 solution tests pass locally. The solution
  formatter reports only the three findings reproduced on base `b47a5a0`,
  including two in unchanged lines of `StaticLayout.cs`; the other changed
  C# files pass a separate format check.
  The alphabetic-marker increment adds 54 net regression cases across CSS,
  layout, paint, native content and local/process browser rendering. Its
  solution build, local V8 smoke and all 15,151 solution tests pass. The
  solution formatter reports only the three findings reproduced on base
  `b47a5a0`, including two unchanged lines of `StaticLayout.cs`; the other
  changed C# files pass a separate format check.
  The Roman-marker increment adds 61 net regression cases across CSS, layout,
  paint, native content and local/process browser rendering, including all
  3,999 values in both Roman styles against an independent reference. The
  solution build, local V8 smoke and all 15,212 solution tests pass; solution
  formatting reports only the same three baseline findings, with all other
  changed C# files passing a targeted format check.
  The zero-padded decimal increment adds 35 net regressions covering the entire
  -10..10 boundary, cascade, layout, budgets, native pixels and retained browser
  rendering. The solution build, local V8 smoke and all 15,247 solution tests
  pass; solution formatting reports only the same three baseline findings, with
  all other changed C# files passing a targeted format check.
  The `menu` list-layout increment adds 13 net CSS, layout, paint, native-pixel
  and browser regressions. The solution build, local V8 smoke and all 15,260
  solution tests pass; the solution formatter reports only the same three
  baseline findings, with other changed C# files passing a targeted check.
- Transforms, gradients, rounded borders, stacking contexts, and advanced compositing.

Nonwrapping `white-space:pre/nowrap` now accepts supported Latin/common
punctuation (including ordinary URL and hyphenated text) without soft wrapping;
spaces/forced breaks and horizontal overflow remain mode-appropriate.
`normal`/`pre-line` support a leading ASCII hyphen before a digit and internal
hyphens between ASCII letters/digits in one text node. Internal hyphens break
only before a letter, not before a digit; `-10` and `2026-10-31` stay
unbroken. A solidus between two ASCII letters or two ASCII digits likewise
stays unbroken, including `and/or`, `1/2` and `24/7`. These cases retain
source identity and share bounded break-search work and table sizing.
The ASCII infix separators `.`, `,`, `:` and `;` between ASCII letters/digits
in one text node likewise stay unbroken, allowing `example.com`, `1,000.25`
and `12:30` without changing space/hyphen breaks or intrinsic sizing.
Internal `@`, `_` and `+` between ASCII letters/digits in one text node also
stay unbroken, allowing `first.last+tag@example.com` and
`user_name@example.com` through the same bounded layout and retained rendering.
Other solidus forms, unsupported internal infix contexts (including adjacent
separators or non-ASCII neighbors), unsupported neighbors of those email
joiners, broader punctuation/Unicode-break algorithms, cross-node word shaping,
unsupported scripts/bidi/controls and missing-font handling remain unsupported.
Supported inline form controls now provide soft-wrap opportunities on both
sides in wrapping modes, including beside U+00A0; `pre`/`nowrap` remain
nonwrapping. Direct control adjacency to U+035C..U+0362 glue in wrapping
modes fails explicitly until line-break backtracking is implemented.
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
The ASCII infix-separator increment adds 48 layout, native-content and
local/process browser regression cases. Its solution build, local V8 smoke and
all 15,308 solution tests pass; the solution formatter reports only the three
baseline findings, with changed test files passing a targeted check. Earlier
full runs each hit an unchanged V8 or browser deadline test; both passed
individually and the subsequent serialized full run passed.
The email-joiner increment adds 74 net layout, native-content and local/process
browser cases, including exact table widths, work limits, cancellation and
retained link selection. The solution build, all 15,403 solution tests and
solution formatting verification pass locally, as does the local V8 smoke.
General email, URL and Unicode line breaking remain unsupported.
The bounded numeric and atomic-control increment adds 350 net layout,
native-content and local/process browser cases. It covers negative numbers,
dates, fractions, mixed hyphen opportunities, intrinsic widths, whitespace,
explicit glue rejection and retained selection. The solution build, all 15,753
solution tests, solution formatting and local V8 smoke pass.

HTML `align` presentational hints on `p` and `h1`–`h6` now map exact
ASCII-case-insensitive left/right/center/justify values to the existing
`text-align` cascade and layout paths. Invalid values and other elements are
ignored. Focused CSS, layout, native-content and local/process browser tests
cover cascade, assignment limits, mutation, wrapped geometry and retained
resize; no broader alignment behavior is added. See
[HTML rendering hints](https://html.spec.whatwg.org/multipage/rendering.html#tables-2)
(local ID `html`).
Focused validation: CSS align filter 21 passed; layout alignment/justification
filter 29 passed; native-content alignment/justification filter 5 passed;
local/process browser alignment/justification filter 6 passed. The CSS project
regressions also passed (1,033 cases), and formatting verification of the
modified C# files passed. An initial complete run passed 15,986 cases and
failed one unchanged shell form-navigation test. The same assertion failed on
base commit `087954f` with a test-only 150ms external-transport delay, and
passed with that delay when Return was sent only for pending confirmations;
the exact cause of the original failure remains unproven. A complete rerun
passed 15,986 cases and failed the unchanged
`UpgradeInsecureRequestsUpgradesPageLinkAndRedirectBeforeTransport` shell test
with a resource deadline. That same test and deadline were reproduced on
baseline `37dc296` with a test-only 100ms transport delay; the precise cause
of either uninstrumented full-run failure is not established. Both tests
passed three subsequent focused repetitions.

The previously rejected `text-align:justify` now supports bounded English
inter-word justification on soft-wrapped lines. Collapsed ASCII spaces absorb
equal shares of positive slack; final and forced-break lines remain
start-aligned, and source/selection/link geometry follows the expanded glyph
advances. A no-break space on a line needing expansion and a missing shaped
space glyph fail explicitly. Other justification methods, `text-align-last`,
`text-justify`, broader script handling and Unicode word separators remain
unsupported. See [ordinary text flow](../docs/text-layout.md#ordinary-flow).
This increment adds 21 net regression cases. The solution build, local V8
smoke and all 15,329 solution tests pass, including 338 Layout, 42 Content and
1,236 Browser cases. The solution formatter reports the same three baseline
findings on unchanged lines; all changed test files pass a targeted check.

Basic canonical HTML tables now support automatic intrinsic column sizing,
separate borders, one or two spacing lengths (one for both axes, or horizontal
then vertical), the HTML `cellspacing` and nonzero `table[width]` hints,
top-aligned text/block cells, and table/cell/row/group/column background colors
and solid borders through ordinary paint flow. Table widths act as minima;
header/footer groups are ordered around body groups.
Bounded horizontal `colspan` distributes spanned intrinsic minimum/maximum
constraints equally across tracks; the total column grid is capped by
`LayoutOptions.MaxTableColumns` before allocation and repeated track work by
`LayoutOptions.MaxTableColumnOperations`. Positive `rowspan` and `rowspan=0`,
one caption, and bounded column/group tracks and widths are supported as
documented. Anonymous/arbitrary CSS tables, collapsed borders, fixed layout,
nested tables/widgets, percentages inside cells and other legacy presentation
attributes remain unsupported. See the
[exact supported scope and limits](../docs/text-layout.md#basic-automatic-tables).
The table increment adds 32 regressions covering CSS, intrinsic/actual geometry,
exact work limits and cancellation, native pixels, and local/process
scroll/link/retained resize behavior. Build, touched suites and all 14,340
solution tests pass locally. Formatting reports only the three established
baseline findings, not new table findings.

Pass 10 accepts row-group and row `background-color` on canonical tables.
Existing parent-before-child paint traversal paints these layers below cells,
so transparent cells reveal row colors and row spacing reveals the group color.
Margins, padding, borders and row/group sizing remain unsupported. Column
backgrounds were unsupported at that earlier point and are implemented by
Pass 7 above. Layout (807), Content (93), the zero-warning solution build, all
16,131 solution tests and solution formatting verification pass locally.

**A particularly restrictive current limitation:** parent-edge margin collapsing is limited to eligible first/last block-child edges without intervening content, borders or padding (bottom propagation additionally requires auto height and zero min-height). Some CSS values parse successfully but remain unsupported downstream in layout or paint.

HTML `div[align]` now supplies the bounded `text-align` presentational hints
`left`, `right`, `center`, `middle` and `justify`, with ASCII-case-insensitive
matching and `middle` mapped to `center` through the existing cascade. Legacy
HTML `center` is block-level by default and supplies the same bounded
`text-align:center` presentational hint. `table[align=center]` supplies
horizontal auto-margin hints, centering supported explicit-width tables.
HTML `figure` now receives 40px left/right user-agent margins in the supported
horizontal LTR subset; its captions remain ordinary block flow. HTML
`address` now defaults to italic through the UA sheet and still requires an
explicitly registered italic face for rendering.
The HTML table-cell `align` attribute now maps `left`, `right`, `center`,
`middle` and `justify` on `td`/`th` to `text-align`, with ASCII-case-insensitive
matching and `middle` mapped to `center`; `absmiddle` also maps to `center` on
these six table elements. The same values on `tr`, `thead`, `tbody` and `tfoot`
now map to inherited `text-align`. Legacy over-constrained descendant-margin
alignment, table left/right floats and other alignment targets remain
unsupported. CSS `vertical-align:baseline`, `middle` and `bottom` now position
canonical table-cell contents against their final row height, including the
complete height and internal spacing of row-spanning cells; `top` remains the
UA default. Pass 8 adds explicit CSS-parser and exact single-/multi-row-span
geometry regressions. CSS (1,142), Layout (839), Content (99) and Paint (71)
tests pass; the solution build has zero warnings/errors, all 16,188 solution
tests pass, and solution format verification passes. Commit `8edd05b` is pushed;
Linux development, Windows confinement (x64/arm64) and CodeQL CI pass (runs
42357015761, 42357015749 and 42357015436). On the CI-recording commit, the
unchanged Windows x64 navigation test intermittently duplicated requests on
the first run; the failed-job-only rerun passed, so Windows run 42357742176 is
green. The Pass 8 increment adds 14 CSS cascade cases, 13 layout
acceptance/rejection cases and one retained-resize pixel regression. CSS
(1,116), Layout (798), Content (89), the solution build (zero warnings/errors),
all 16,105 solution tests and format verification pass.

Pass 9 adds HTML `td`/`th` `valign=top`, `middle` and `bottom` as
ASCII-case-insensitive author presentational hints, using the existing
single-row and row-span alignment behavior. At that time, baseline, invalid
cell values and row/group `valign` remained unsupported and failed explicitly
at layout. CSS
(1,149), Layout (852), the zero-warning solution build, all 16,208 solution
tests and formatting verification pass locally. Commit `ce09478` is pushed and
Linux x64/arm64, Windows x64/arm64 and CodeQL CI pass (runs 42362747599,
42362747677 and 42362747202).

Pass 10 adds CSS and HTML `td`/`th` baseline alignment, including row-spanning
cells. A cell's first in-flow line baseline (or its content bottom if empty)
aligns with the maximum baseline offset among baseline-aligned cells starting
in the row. For baseline row-spans, any added height uses the existing even
deficit distribution; CSS 2.2 does not define this row-height policy. UA cells
remain top-aligned, and row/group `valign` remains unsupported.
CSS (1,152), Layout (853) and Content (100) tests pass. The solution build
reports zero warnings/errors; all 16,213 solution tests and formatting
verification pass. The retained-content regression verifies baseline `valign`
against equivalent CSS across resize. Commit `348ad31` is pushed; Linux x64/arm64,
Windows x64/arm64 and CodeQL CI pass (runs 42373689351, 42373689357 and
42373689317).

The caption-placement increment supports CSS 2.2 `caption-side:top/bottom`,
including inherited bottom positioning for the existing first-child caption.
Bottom captions follow the table box and contribute to wrapper height without
changing intrinsic column sizing. Multiple, later-child and non-HTML captions
remain unsupported. CSS tests (1,152), Layout tests (854), the zero-warning
solution build, all 16,214 solution tests, solution format verification and
`git diff --check` pass locally. Commit `ef3247f` is pushed; Linux, Windows
confinement and CodeQL CI pass (runs
[42379320965](https://github.com/khmseu/VisualWeb/actions/runs/42379320965),
[42379321055](https://github.com/khmseu/VisualWeb/actions/runs/42379321055) and
[42379320829](https://github.com/khmseu/VisualWeb/actions/runs/42379320829)).

Pass 2 supports CSS 2.2 percentage widths on canonical `col` and `colgroup`
elements. Definite-width tables resolve against the table width; automatic
tables use a bounded deterministic solve against their final width. A
column-group deficit is shared evenly across its tracks. Constraints without
a finite solution fail explicitly. Layout tests (858), the zero-warning
solution build, all 16,218 solution tests and format verification pass locally.
Commit `dee2a86` is
pushed; Linux, Windows confinement and CodeQL CI pass (runs
[42381800740](https://github.com/khmseu/VisualWeb/actions/runs/42381800740),
[42381800852](https://github.com/khmseu/VisualWeb/actions/runs/42381800852) and
[42381800232](https://github.com/khmseu/VisualWeb/actions/runs/42381800232)).

Pass 3 extends inherited CSS 2.2 `border-spacing` to accept one or two
nonnegative lengths. One length applies to both axes; two values specify
horizontal then vertical spacing. Relative `em`/`rem` values compute to
absolute pixels, and layout uses the horizontal value for column geometry and
the vertical value for row and rowspan geometry. CSS tests (1,155), Layout
tests (859), the zero-warning solution build, all 16,222 solution tests,
format verification and `git diff --check` pass locally. Commit `9102c49` is
pushed; Linux (Run 42384229690), Windows confinement (Run 42384229722) and
CodeQL (Run 42384229517) pass.

Pass 4 supports the HTML `table[cellspacing]` presentational hint: values that
parse as non-negative integers set equal horizontal and vertical pixel spacing.
Malformed values add no hint, and normal author CSS overrides the hint. CSS
tests (1,161), Layout tests (861), the zero-warning solution build, all 16,230
solution tests, format verification and `git diff --check` pass locally.
Commit `146952f` is pushed; Linux (Run 42388005024), Windows confinement
(Run 42388005016) and CodeQL (Run 42388004385) pass.

Pass 5 extends percentage `col`/`colgroup` constraints to auto-width tables.
The bounded solver resolves them against the final table width, charges
repeated work to the shared table-column operation limit, and rejects
constraints without a finite solution. Regression coverage includes final
width resolution, overlapping group/column constraints, unsatisfiable
percentages and the operation limit. Layout (867), the zero-warning solution
build, all 16,236 solution tests, solution format verification and
`git diff --check` pass locally. Commit `de132ad` is pushed; Linux (Run
42396138538), Windows confinement (Run 42396138531, x64/arm64) and CodeQL
(Run 42396138216) pass.

Pass 6 supports the HTML `table[width]` presentational hint through the
standard nonzero dimension parser. Positive lengths map to pixels and
percentages resolve against the containing block; zero and invalid values add
no hint, while values outside the finite numeric range fail explicitly.
CSS and layout regressions cover parsing, cascade precedence, attribute/work
limits, actual table widths and continued rejection of width attributes on
other table parts. CSS (1,171), Layout (870), the zero-warning solution build,
all 16,249 solution tests, format verification and `git diff --check` pass
locally. The HTML cache was refreshed before implementation. Commit and CI
`fb73991` is pushed; Linux development (Run 42401385159), Windows confinement
x64/arm64 (Run 42401385143) and CodeQL (Run 42401384901) pass. Pass 6 is
complete.

Pass 7 extends the HTML table `align` presentational hint to `tr`, `thead`,
`tbody` and `tfoot`, accepting ASCII-case-insensitive `left`, `right`,
`center`, `middle` and `justify`; the inherited text alignment reaches cells.
Legacy alignment of over-constrained descendant margins remains unsupported,
and invalid row/group values still fail explicitly at layout. CSS (1,174),
Layout (891), the zero-warning solution build, all 16,273 solution tests,
format verification and `git diff --check` pass locally. Commit `ae7f1ae` is
pushed; Linux (Run 42474057086), Windows confinement x64/arm64 (Run 42474057143) and Push on main/CodeQL (Run 42474056897) pass. Pass 7 is
complete.

Pass 8 adds the local HTML rendering rule's `align=absmiddle` alias for
`text-align:center` on `td`, `th`, `tr`, `thead`, `tbody` and `tfoot`. CSS
cascade coverage, layout acceptance and exact centered-cell geometry verify
the behavior; other elements and unsupported table-alignment behaviors remain
unchanged. CSS (1,177), Layout (898), the zero-warning solution build, all
16,283 solution tests, solution format verification and `git diff --check`
pass locally. Commit `25e1ab4` is pushed; Linux development
([Run 42478735102](https://github.com/khmseu/VisualWeb/actions/runs/42478735102)),
Windows confinement x64/arm64
([Run 42478735101](https://github.com/khmseu/VisualWeb/actions/runs/42478735101))
and Push on main/CodeQL
([Run 42478735123](https://github.com/khmseu/VisualWeb/actions/runs/42478735123))
all pass. Pass 8 is complete; two increments remain in this ten-pass request.

Pass 9 supports HTML `valign=baseline/top/middle/bottom` on `tr`, `thead`,
`tbody` and `tfoot` as defaults for contained cells. The nearest row wins over
its row group, while cell CSS and cell `valign` hints override; this is a
bounded HTML compatibility rule, not general CSS inheritance. Invalid
row/group values and unsupported targets continue to fail explicitly.

CSS (1,183), Layout (908), the zero-warning solution build, all 16,299
solution tests, format verification and `git diff --check` pass locally.
Pass 9 is complete locally; one increment remains in this ten-pass request.
Commit `dd42bec` is pushed. Linux development
([Run 42485850844](https://github.com/khmseu/VisualWeb/actions/runs/42485850844)),
Windows confinement x64/arm64
([Run 42485850871](https://github.com/khmseu/VisualWeb/actions/runs/42485850871))
and Push on main/CodeQL
([Run 42485850925](https://github.com/khmseu/VisualWeb/actions/runs/42485850925))
pass.

Pass 10 selects bounded HTML `body[bgcolor]` support for simple-page
rendering. It reuses the legacy color parser and author presentational-hint
cascade, with an independent aggregate raw-attribute budget. CSS and offline
pixel regressions pass. CSS (1,191), Content (101), all 16,308 solution
tests, the zero-warning solution build, format verification and
`git diff --check` pass locally. Commit `49ab268` is pushed; Linux
development (Run 42490116966), Windows confinement x64/arm64 (Run 42490116900)
and Push-on-main/CodeQL (Run 42490116932) pass. Pass 10 and the requested
ten-increment run are complete.

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

Support for WebDriver and/or WebDriver BiDi could be very helpful for debugging
the browser. Consider it as a cross-cutting development aid alongside the
priorities below; the choice of protocol and its scope remain open. See the
[AI standards references](../docs/AI-standards.md) for related specifications.

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
   engineering** as coverage expands beyond simple websites. WAI-ARIA support
   and WebMCP are lower-priority targets.

Preserve existing confinement, request restrictions, explicit development
opt-ins and fail-closed behavior throughout all priorities. Add the security
checks needed by each new resource or scripting capability as part of that
increment; this ordering does not authorize weakening safeguards or imply that
simple websites are safe to browse untrusted. Validate each increment with
focused regressions and representative offline simple-site fixtures.

_Priority order updated on 2026-10-28 to favor simple-site usability first;
debugging and later targets clarified on 2026-10-31. These are work-order
changes, not new implementation or validation claims._

*Status cross-checked against the implementation and subsystem guides on
2026-10-19. Native Linux x64/ARM64 and Windows x64/ARM64 CI passed for commit
`bc22600` ([Linux](https://github.com/khmseu/VisualWeb/actions/runs/39194869543),
[Windows](https://github.com/khmseu/VisualWeb/actions/runs/39194869712)). These checks are not full conformance, security, CI-matrix or
real-desktop validation.
