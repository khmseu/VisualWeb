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

- Security-origin identity is partially implemented: immutable tuple/opaque URL origins and `LoadedPage` origins from the final response URL, with fresh opaque identity per new document and stable identity for retained repaint/metadata copies. Tabs expose a browser-owned committed origin published atomically with successfully rendered documents. Inherited/sandbox origin selection remains missing. Engine.Net has an opt-in same-origin restricted GET mode (`LoadSameOriginAsync`) that checks the initial URL and every redirect against a fixed HTTP(S) request origin. Nothing calls it from script or the browser, and it is not production-safe policy. Both Engine.Net entry points (and therefore browser navigation) block Fetch bad ports for HTTP(S) URLs and every redirect hop. Bounded session HSTS is implemented: one memory-only DNS-host policy store per shell session, shared across tabs/windows in all modes, authenticated HTTPS first-header learning and initial/redirect upgrades before destination checks; standalone loaders opt in explicitly. No HSTS persistence/preload/public-suffix policy, browser-wide same-origin enforcement, CORS, CSP, general mixed-content policy, or storage partitioning is implemented. HTTPS documents do block HTTP linked stylesheets, including redirects to HTTP.
- Top-level navigation renderer swaps are implemented: every multiprocess shell mode (confined or explicitly unsandboxed) rotates to a fresh renderer for cross-origin final origins and every new opaque document, transactionally after render/commit/history success, reusing it for same-origin navigation. Low-level controllers keep the per-tab default. There are no cross-site frames or frame isolation, no site computation, no inherited/sandbox origin selection, and no SOP/CORS/CSP or production request authorization.
- The CLI secure-by-default renderer milestone is delivered: recommended `--multiprocess` and legacy `--development-multiprocess` require supported confinement before SDL/content/worker startup, without fallback. Unsandboxed execution requires explicit `--allow-unsandboxed-development` trusted-content acknowledgement in a development mode (mandatory for single-process), rejected with normal mode or `--require-sandbox`. `--require-sandbox` remains a redundant compatible assertion; low-level library/test renderer defaults are unchanged. This does not deliver production request authorization or complete web security.
- Linux ARM64 confinement is explicitly unsupported and fails closed.
- The browser/network broker remains privileged; production request authorization is unfinished. Page-initiated `file:` links and forms are now rejected, while explicit address-bar file navigation remains available; local-document confinement is still absent. Linked stylesheet discovery rejects `file:` destinations for non-file documents, and HTTPS documents block HTTP linked stylesheets and downgrade redirects.

Evidence: `networking trust boundary`, `isolation architecture`, and `Linux architecture guard`.

### 2. Basic interaction with pages

The shell supports bounded textual link activation and vertical document scrolling;
broader page interaction is still missing:

- Textual `<a href>` links support primary left-click and Tab/Shift+Tab/Enter
  navigation in shell, local and actual renderer-process paths. Enabled drawn
  chrome controls precede visible links in traversal; Ctrl+L retains address
  editing. Focus is per-tab, with shell-only outlines across all rectangles of
  one anchor and no page raster changes without focus. IPC v7 groups visible
  rectangles per anchor (4096 anchors, 64 rectangles each, 1 MiB metadata),
  without element IDs/DOM/native handles. Successful document commits/repaints
  clear the frame-local anchor focus; failed navigation preserves it. General
  hit-testing, DOM focus/events and offscreen-link traversal remain missing.
  See [shell controls](../docs/browser-shell.md#controls).
- A bounded simple-forms subset submits same-tab GET urlencoded queries from
  shell-edited `input` text/search/hidden/submit and `button` submit controls
  via IPC v8 data-only metadata and the ordinary
  navigation transaction; supported controls use finite inline-block layout with
  bounded fallback dimensions and unsupported form semantics fail visibly.
  General forms remain missing: POST/multipart, other input types, textarea/select,
  constraint validation UI, form events/scripted submission, `form=` owners,
  intrinsic widget sizing, control margins, autofill and non-UTF-8 submission
  encodings. See
  [simple GET forms](../docs/browser-shell.md#simple-get-forms).
- A bounded browser-owned selection supports mouse drag across visible non-link
  shaped text fragments and Ctrl+C to the system clipboard. Selection is fragment-
  level (not character-level); a primary press beginning on an anchor retains
  immediate navigation, while a drag begun in ordinary text can include anchor
  fragments. Selection resets on successful document/scroll/resize publication.
  Basic vertical document scrolling is implemented via wheel and Page
  Up/Down/Home/End, retaining DOM/scripts across
  local/process repaints.
  A browser-owned vertical scrollbar now supports click-to-position and drag scrolling.
  Horizontal/nested scrolling and general CSS overflow remain missing.
- No automatic page mouse/keyboard events or general default actions beyond
  ordinary current-tab textual link navigation.
- No downloads, persistent browser sessions, or complete history behavior. History currently stores URLs and reloads documents; same-document navigation, script History API, and bfcache are absent.

This is a substantial gap even for a modest usable browser.

Evidence: `page limitations`, `input dispatch`, and `history implementation`.

### 3. Loading complete web pages

Besides the main HTML resource, only bounded classic linked stylesheets are loaded:
the browser (never the renderer) fetches supported `link rel=stylesheet` sheets
(media absent/`all`, type absent/`text/css`, no title/alternate/disabled/CORS/integrity)
with the tab loader and shared session HSTS store, cookies off, resolved against the
final response URL, as at most 32 sheets of 256 Ki characters within a 1 MiB IPC
budget. Any stylesheet failure fails the navigation. Missing pieces include:

- CSS `@import`, `url()` resources, external scripts, images, fonts, media, and frame resources;
  media-conditional/alternate/titled sheets, `<base href>` resolution, CORS/SOP and general mixed-content policy for subresources.
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

- Tables, templates, general forms (only the simple form-pointer subset exists), select/options, SVG/MathML, and other specialized parsing algorithms.
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
- IME composition/preedit, clipboard support, and accessibility integration.
- Storage APIs, service workers, canvas, audio/video, WebGL/WebGPU, and the wider browser API surface.

Evidence: `text/layout exclusions`, `platform input limitations`, and `standards-growth roadmap`.

### 8. Distribution and comprehensive validation

Linux and Windows implementation and native CI configurations exist; Windows support should not be described as merely a placeholder.

Still outstanding:

- Real desktop validation for Wayland keyboard/IME and high-DPI behavior.
- Broader conformance coverage: current tests exercise selected WPT/html5lib fixtures, not the full web platform.
- Routine Linux confinement/resource-exhaustion coverage: the Linux workflow explicitly filters out several such integration tests.
- Production packaging, installation/update workflows, and deployment hardening. Current confinement requires trusted framework-dependent deployments; Linux self-contained/single-file deployment is unsupported.

Evidence: `platform validation`, `HTML conformance coverage`, `Linux CI`, `Windows CI`, and `deployment prerequisites`.

## Documentation also needs reconciliation

Some older passages incorrectly say JavaScript/V8 or page scripting is still absent, despite the implemented opt-in scripting pipeline. Examples appear in the `shell limitations` and `Windows validation notes`. These can obscure the actual remaining work.

## Recommended priority

1. **Security model and mandatory supported isolation** before untrusted browsing.
2. **Links, page input, and subresource loading** for basic usability. Basic
   bounded textual links, vertical document scrolling, bounded classic linked
   stylesheets and simple same-tab GET forms are implemented; broader page input, other subresources and overflow remain.
3. **HTML recovery, ordinary layout, fonts, and common CSS** for static-site compatibility.
4. **Persistent scripting/event-loop integration and browser APIs** for interactive sites.
5. Broader standards, accessibility, conformance coverage, and release engineering.

*The original audit was read-only and did not independently verify builds/tests
or CI. The interaction and loading entries above were subsequently updated for the
implemented bounded textual-link, vertical-scrolling, linked-stylesheet and
simple GET form subsets.*
