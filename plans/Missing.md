# What is missing

## Overall assessment

**The architectural foundation is largely delivered, but VisualWeb is still a development browser for a narrow HTML/CSS subset—not yet a general-purpose browser or safe for untrusted websites.**

The biggest remaining gaps are page interaction, real-world standards compatibility, and production-grade security/isolation.

### What already satisfies the original plan

| Original requirement | Current state |
| --- | --- |
| C# monorepo with separate browser subsystems | Implemented: platform, core standards utilities, networking, HTML/DOM, CSS, text/layout, painting, scripting, IPC, and applications. See the `project map`. |
| Encapsulated OS dependencies | Implemented through platform abstractions and Linux/Windows backends using SDL. See the `platform guide`. |
| Tabs and multiple windows | Implemented, including tab creation, closing, switching, and moving between windows. See the `browser shell guide`. |
| Isolation between tabs | Partially delivered: multiprocess mode has one renderer per tab and committed origin (rotated on cross-origin/opaque navigation), bounded IPC, deadlines, and tab-local crash/restart handling. Browser CLI multiprocess modes require supported OS confinement by default. **Process separation is not complete web security.** See the `process architecture`. |
| Architecture prepared for standards growth | Present: subsystem boundaries, official-spec references/cache, and targeted conformance fixtures. This is preparation, not comprehensive standards implementation. |

## What is still missing

### 1. Production-safe browsing and stronger isolation

This is the most important gap before accepting arbitrary web content.

- Security-origin identity is partially implemented: immutable tuple/opaque URL origins and `LoadedPage` origins from the final response URL, with fresh opaque identity per new document and stable identity for retained repaint/metadata copies. Tabs expose a browser-owned committed origin published atomically with successfully rendered documents. Inherited/sandbox origin selection remains missing. Engine.Net has a same-origin restricted GET mode (`LoadSameOriginAsync`) that checks the initial URL and every redirect against a fixed HTTP(S) request origin; the browser now applies it to HTTP(S) linked stylesheets using the final document origin. This intentionally stricter-than-HTML policy is not production-safe request authorization. Top-level navigation remains unrestricted. Both Engine.Net entry points (and therefore browser navigation) block Fetch bad ports for HTTP(S) URLs and every redirect hop. Bounded session HSTS is implemented: one memory-only DNS-host policy store per shell session, shared across tabs/windows in all modes, authenticated HTTPS first-header learning and initial/redirect upgrades before destination checks; standalone loaders opt in explicitly. No HSTS persistence/preload/public-suffix policy, browser-wide same-origin enforcement, CORS, CSP, general mixed-content policy, or storage partitioning is implemented. HTTPS documents also block HTTP linked stylesheets and downgrade redirects.
- Top-level navigation renderer swaps are implemented: every multiprocess shell mode (confined or explicitly unsandboxed) rotates to a fresh renderer for cross-origin final origins and every new opaque document, transactionally after render/commit/history success, reusing it for same-origin navigation. Low-level controllers keep the per-tab default. There are no cross-site frames or frame isolation, no site computation, no inherited/sandbox origin selection, and no SOP/CORS/CSP or production request authorization.
- The CLI secure-by-default renderer milestone is delivered: recommended `--multiprocess` and legacy `--development-multiprocess` require supported confinement before SDL/content/worker startup, without fallback. Unsandboxed execution requires explicit `--allow-unsandboxed-development` trusted-content acknowledgement in a development mode (mandatory for single-process), rejected with normal mode or `--require-sandbox`. `--require-sandbox` remains a redundant compatible assertion; low-level library/test renderer defaults are unchanged. This does not deliver production request authorization or complete web security.
- Native Linux ARM64 confinement is implemented with the same fail-closed namespace/mount/seccomp/cgroup/resource guarantees as x64, and native ARM64 CI confinement/resource smokes are configured. Successful native ARM64 smoke/test evidence has not yet been observed here; cross-generated BPF policy tests and x64 validation do not close that target-evidence gap.
- The browser/network broker remains privileged; production request authorization is unfinished. Page-initiated `file:`/`data:` links and forms and linked file stylesheets are rejected, while explicit address-bar navigation remains available. Local file documents also cannot initiate HTTP(S) link/form navigation; HTTPS page-initiated links/forms block cleartext requests and downgrade redirects unless HSTS upgrades the destination before transport. Opaque `data:` links block cleartext HTTP navigation and opaque documents cannot submit forms to any HTTP(S) destination. HTTP(S) document forms are restricted to same-origin actions and redirect hops, except same-host/effective-port HTTPS-to-HTTP actions which proceed through existing HSTS/downgrade policy. Cross-origin form redirect targets fail before their requests are sent, preventing forwarded query data; custom `IPageSource` implementations that cannot enforce fixed-origin redirects for forms or HTTPS downgrade protection for secure page links fail closed. Local file and opaque `data:` documents cannot trigger linked HTTP(S) stylesheet requests (`data:` stylesheets remain allowed); HTTP(S) documents can load only same-origin HTTP(S) stylesheets, with cross-origin redirects rejected, plus `data:` sheets. These are narrow restrictions, not local-document confinement or general request authorization. Linked stylesheet discovery rejects `file:` destinations, and HTTPS documents block HTTP linked stylesheets and downgrade redirects.

Evidence: `networking trust boundary`, `isolation architecture`, and `Linux confinement architecture policy and evidence boundary`.

### 2. Basic interaction with pages

The shell supports bounded textual link activation and vertical document scrolling;
broader page interaction is still missing:

- Textual `<a href>` links support primary left-click and Tab/Shift+Tab/Enter
  navigation in shell, local and actual renderer-process paths; middle-click opens
  the visible link in a new active tab and Ctrl+primary-click opens it in a
  background tab, regardless of its target. Shift+Enter opens the focused link
  in a new active tab; Ctrl+Enter opens it in a background tab. Enabled drawn
  chrome controls precede visible links in traversal; Ctrl+L retains address
  editing. Focus is per-tab, with shell-only outlines across all rectangles of
  one anchor and no page raster changes without focus. IPC v28 introduced grouped
  visible rectangles plus bounded `_blank` target metadata (4096 anchors, 64
  rectangles each, 1 MiB metadata), plus bounded fragment-ID offsets for
  same-document scrolling, without element IDs/DOM/native handles. Fragment
  links update URL-only history and repaint the retained document without a
  resource load; only rendered element IDs and empty-fragment top scrolling are
  supported. `_blank` opens an active tab in the source window through the
  existing broker policy; `_self`, `_parent`, and `_top` use the current tab,
  and the first `<base target>` supplies fallback for links. Named browsing
  contexts remain unsupported and fail visibly. Hyperlink URLs resolve against
  the first `<base href>` (invalid/data/javascript bases fall back to the final
  response URL); form actions and linked stylesheets use the same resolution.
  Successful document commits/repaints clear the frame-local anchor focus;
  failed navigation preserves it. General hit-testing, DOM
  focus/events and offscreen-link traversal remain missing.
  See [shell controls](../docs/browser-shell.md#controls).
- A bounded simple-forms subset supports shell-owned `input`
  text/search/email/tel/url/password/date/time/month/week/number/range/checkbox/radio/hidden/submit/reset,
  `textarea`, single-select `select`, `button` submit/reset controls, and inert `input type=button`/`button type=button` controls; supported successful controls
  submit current/new-tab GET urlencoded queries via IPC v36 data-only metadata and the ordinary
  navigation transaction; supported controls use finite inline-block layout with
  bounded fallback dimensions and unsupported form semantics fail visibly.
  Reset controls are shell-owned: pointer activation or Enter on the focused
  reset control restores all controls owned by its form to their initial values
  and checked states, clears their edited/dirty/check state, preserves focus,
  and does not navigate or run submission validation. Text-field implicit Enter
  continues to target only a submitter and never activates reset controls.
  Selects expose bounded direct options, default selection, disabled-option
  skipping and Up/Down/Home/End/PageUp/PageDown keyboard selection. Page keys move
  by the visible popup row count and skip disabled options. Bounded committed-text prefix
  search selects matching enabled options and cycles repeated single-character
  input. Enter/Space or a primary click opens a browser-owned popup; it supports
  pointer option selection, hover highlighting and wheel scrolling through longer
  lists, with Escape/outside-click dismissal. Text fields
  and textareas display bounded placeholders
  without changing submitted values. Email `multiple` supports sanitized
  comma-separated address lists, while select multiple selection, listboxes and
  optgroups remain unsupported.
  Date inputs use strict `YYYY-MM-DD` Gregorian values for years 0001–9999,
  sanitize invalid initial values to empty, and validate nonempty values and
  `required` before submission; no calendar picker or date constraints are
  implemented (`min`, `max`, `step`, `pattern`, length, `list`, `multiple` and
  `dirname` are rejected visibly). Time values use strict bounded HTML time
  syntax with generic text editing and validation; no clock picker or time
  constraints are implemented (the same constraint attributes are rejected). Month values use positive years of at least four digits and months 01–12, with generic text editing and validation; no month picker or constraints are implemented (the same constraint attributes are rejected). Week values enforce valid ISO week-years (including the 52/53-week boundary) and use generic text editing; no week picker or constraints are implemented (the same constraint attributes are rejected). Number inputs support finite floating-point syntax, optional finite min/max
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
  tab; `_self`, `_parent`, and `_top` retain current-tab behavior. A selected
  button/input submitter's `formtarget` overrides that fallback. Invalid/named
  overrides affect only that submitter and fail before value serialization or
  request/tab startup. Named/other non-keyword contexts remain missing and visibly
  rejected. None bypass destination or origin restrictions; new-tab destination
  checks run before tab creation, with shared HSTS applied before downgrade checks.
  Visible text/search/email/tel/url/password/date/time/month/week/number/checkbox/radio/textarea/select controls support pointer focus without starting page text
  selection, Ctrl+A select-all/edit replacement, and Ctrl+V clipboard paste. Single-select
  popup option selection is browser-owned, bounded to 12 displayed rows, and wheel-scrollable.
  Clipboard support remains bounded to selection copy and paste into focused
  text/search/email/tel/url/password/date/time/month/week/number/checkbox/radio/textarea fields or the selected address bar. General forms remain
  missing: POST/multipart, other input types beyond text/search/email/tel/url/password/date/time/month/week/number/range/checkbox/radio/reset,
  broader textarea behavior (`dirname` remains unsupported),
  full constraint-validation UI and semantics, submitter overrides other than
  `formaction`/`formtarget`/`formnovalidate`, form events/scripted submission, unsupported `form=` ownership on object, intrinsic widget sizing, control margins, autofill and non-UTF-8 submission
  encodings. See
  [simple GET forms](../docs/browser-shell.md#simple-get-forms).
- A bounded browser-owned selection supports mouse drag across visible non-link
  shaped text fragments and Ctrl+C to the system clipboard. Selection is fragment-
  level (not character-level); a primary press beginning on an anchor retains
  immediate navigation, while a drag begun in ordinary text can include anchor
  fragments. Selection resets on successful document/scroll/resize publication.
  Basic vertical document scrolling is implemented via wheel, Arrow Up/Down,
  Page Up/Down and Home/End outside editable fields, retaining DOM/scripts across
  local/process repaints.
  A browser-owned vertical scrollbar now supports click-to-position and drag scrolling.
  Horizontal/nested scrolling and general CSS overflow remain missing.
- No automatic page mouse/keyboard events or general default actions beyond
  ordinary current-tab textual link navigation.
- No downloads, persistent browser sessions, or complete history behavior. History stores URLs plus internal same-document grouping: Back/Forward between user-created fragment entries retains the active document and restores fragment scrolling without a load; cross-document traversal reloads. Script History API and bfcache are absent.

This is a substantial gap even for a modest usable browser.

Evidence: `page limitations`, `input dispatch`, and `history implementation`.

### 3. Loading complete web pages

Besides the main HTML resource, only bounded classic linked stylesheets are loaded:
the browser (never the renderer) fetches supported `link rel=stylesheet` sheets
(media absent/`all`, type absent/`text/css`, no title/alternate/disabled/CORS/integrity)
with the tab loader and shared session HSTS store, cookies off, resolved against the
first `<base href>` (falling back to the final response URL), as at most 32 sheets of 256 Ki characters within a 1 MiB IPC
budget. HTTP(S) sheets are restricted to the final document origin across redirects,
stricter than ordinary HTML stylesheet loading and not CORS. Any stylesheet failure
fails the navigation. Missing pieces include:

- CSS `@import`, `url()` resources, external scripts, images, fonts, media, and frame resources;
  media-conditional/alternate/titled sheets, full CORS/SOP and
  general mixed-content policy for subresources.
- General resource discovery, scheduling, and lifecycle integration (stylesheets are
  discovered from the pre-script parse only; script-added or changed links fail visibly,
  and there are no load/error events or incremental rendering).
- General HTTP methods/request bodies, streaming responses, HTTP caching, and authentication challenges.
- Full cookie policy and persistence. The browser currently disables cookies; the networking library’s optional cookie subset rejects Domain, SameSite, and Partitioned attributes.
- MIME sniffing. The byte-level HTML charset prescan (`<meta charset>`, pragma, XML declaration; first 1024 bytes) now feeds navigation decoding, but statistical/locale encoding detection, container inheritance and the parser's reparse on a later conflicting declaration are absent.

Unsupported linked-stylesheet semantics and external scripts are explicitly rejected rather than merely ignored.

Evidence: `stylesheet collection`, `script classification`, `HTML loading policy`, and `networking scope`.

### 4. General HTML and DOM compatibility

The tokenizer is considerably more complete than the tree builder. Remaining parser work includes:

- Tables, templates, general forms (only the simple form-pointer subset exists), optgroup and unsupported select contents, SVG/MathML, and other specialized parsing algorithms.
- Fragment parsing.
- Adoption-agency/active-formatting recovery for malformed markup.
- Broader legacy doctype handling.

The DOM also lacks full Web IDL bindings, specialized HTML element behavior, form state, custom-element reactions, and other browser APIs.

These are compatibility blockers: unsupported constructs can reject a page.

Evidence: `HTML tree-building scope`.

### 5. General CSS, layout, and painting

The current engine implements finite static block/inline rendering. Missing essentials include:

- Flexbox, grid, tables, floats and positioning; margin collapse is implemented
  for adjacent block siblings and eligible parent/first-or-last block-child edges.
- Media queries, custom properties, calculations, modern selectors, and broader CSS properties. (`:scope` and filtered `:nth-child(An+B of S)`/`:nth-last-child(An+B of S)` are supported for stylesheets and DOM queries; `:has`, dynamic state, namespaces and pseudo-elements remain missing.)
- Images/replaced elements, controls, list markers, decorations, nested/horizontal scrolling, and general overflow layout.
- Transforms, gradients, rounded borders, stacking contexts, and advanced compositing.

**A particularly restrictive current limitation:** parent-edge margin collapsing is limited to eligible first/last block-child edges without intervening content, borders or padding (bottom propagation additionally requires auto height and zero min-height). Some CSS values parse successfully but remain unsupported downstream in layout or paint.

Evidence: `CSS scope`, `layout restrictions`, and `painting limitations`.

### 6. A persistent browser JavaScript environment

V8 and substantial bounded DOM bindings **are implemented**. JavaScript is not wholly missing.

However:

- Page scripting is disabled by default.
- Enabled scripts are post-parse inline classics only.
- There is no persistent HTML event loop or script host after initial execution.
- Modules, external scripts, timers, workers, script-visible networking/storage, and full Window/Web IDL behavior remain deferred.
- Synthetic events and finite lifecycle/microtask processing exist, but not normal ongoing page interaction.
- WebAssembly is explicitly disabled in the current host.

Evidence: `scripting scope` and `short-lived page script host`.

### 7. Internationalization, accessibility, and broader web APIs

Remaining areas include:

- RTL/bidi, complex-script shaping, Unicode line breaking, font fallback, and automatic font matching.
- IME composition/preedit and accessibility integration. Clipboard support is
  bounded to Ctrl+C copying browser-owned selection and Ctrl+V pasting into
  focused text/search/email/tel/url/password/date/time/month/week/number/checkbox/radio/textarea fields or the selected address bar; character-level page
  selection and clipboard selection ranges remain missing.
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

## Documentation reconciliation still needed

The opt-in page-script pipeline exists, so documentation should distinguish
"disabled by default / unavailable in ordinary modes" from "not implemented."
The current Windows confinement note still says page scripting is deferred;
update it to describe the existing development opt-in and its limitations. The
phase-by-phase statements in the browser foundation plan describe earlier
milestones and should be read as historical status, not current capability.

## Recommended priority

1. **Security model and mandatory supported isolation** before untrusted browsing.
2. **Links, page input, and subresource loading** for basic usability. Basic
   bounded textual links, vertical document scrolling, bounded classic linked
   stylesheets and simple current/new-tab GET forms are implemented; broader page input, other subresources and overflow remain.
3. **HTML recovery, ordinary layout, fonts, and common CSS** for static-site compatibility.
4. **Persistent scripting/event-loop integration and browser APIs** for interactive sites.
5. Broader standards, accessibility, conformance coverage, and release engineering.

*Status cross-checked against the current implementation on 2026-10-09. Placeholder text is drawn by the shell only for empty fields, input line breaks are stripped, textarea line breaks are preserved, and placeholder text never becomes a submitted value. Clipboard editing includes field Ctrl+A/Ctrl+V and address-bar
Ctrl+V; telephone inputs use the generic text-field path without telephone-specific
keyboard or validation semantics; email inputs use generic text editing and
bounded validation for one address or sanitized `multiple` comma-separated address
lists; checkbox state is shell-owned, keyboard/pointer toggled and submitted
only when checked; radio state is mutually exclusive by form/name, selected by
pointer, Space, and arrow-key navigation, and required validation applies to the group; bounded editable textareas submit newline-normalized
GET values and use shell-font-measured soft wrapping with visual-line-aware caret
movement; `minlength` is checked for edited nonempty values and bounded text
field patterns (bounded non-backtracking subset), email address/list validation, URL/date/time/month/week input validation and required-checkbox and radio-group state plus numeric syntax/range/step constraints are enforced on submission; password controls are shell-masked and use bounded text constraints; range controls
are sanitized to finite bounds and the step grid and remain shell-owned; reset
controls restore initial values/checks and clear shell-owned edits without
navigation or validation; page-initiated `file:`/`data:` link and form
navigations are blocked while explicit address-bar navigation remains available;
local file documents cannot initiate HTTP(S) link/form navigation, HTTPS links
and forms block cleartext paths unless HSTS upgrades them, opaque `data:` forms
cannot target HTTP(S), and HTTP(S) forms are restricted to same-origin actions
and redirect hops (with the same-host HTTPS downgrade path delegated to HSTS
policy). Cross-origin redirects are rejected before the target request. These
are narrow broker restrictions, not production authorization. For bounded
form placeholders, the browser suite passes (659 tests), IPC passes (147), the
solution builds with zero warnings/errors, and format verification passes. A prior full-solution test invocation also reported
`Engine.Scripting.Tests.CloningTests.CancellationDuringCloningInvalidatesTheHostWithoutChangingSource`
(expected `InvalidOperationException`, none thrown). These checks are not full
conformance, security, CI-matrix or real-desktop validation.
