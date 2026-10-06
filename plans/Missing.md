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
| Isolation between tabs | Partially delivered: multiprocess mode has one renderer per tab, bounded IPC, deadlines, and tab-local crash/restart handling. Optional OS confinement exists. **Process separation is not complete web security.** See the `process architecture`. |
| Architecture prepared for standards growth | Present: subsystem boundaries, official-spec references/cache, and targeted conformance fixtures. This is preparation, not comprehensive standards implementation. |

## What is still missing

### 1. Production-safe browsing and stronger isolation

This is the most important gap before accepting arbitrary web content.

- Security-origin identity is partially implemented: immutable tuple/opaque URL origins and `LoadedPage` origins from the final response URL, with fresh opaque identity per new document and stable identity for retained repaint/metadata copies. Tabs expose a browser-owned committed origin published atomically with successfully rendered documents. Inherited/sandbox origin selection remains missing. Engine.Net has an opt-in same-origin restricted GET mode (`LoadSameOriginAsync`) that checks the initial URL and every redirect against a fixed HTTP(S) request origin. Nothing calls it from script or the browser, and it is not production-safe policy. Both Engine.Net entry points (and therefore browser navigation) block Fetch bad ports for HTTP(S) URLs and every redirect hop. No browser-wide same-origin enforcement, CORS, CSP, mixed-content policy, HSTS, or storage partitioning is implemented.
- No cross-site frame isolation or navigation process swaps; isolation is currently per tab.
- Renderer confinement is **opt-in**, not the normal launch policy.
- Linux ARM64 confinement is explicitly unsupported and fails closed.
- The browser/network broker remains privileged; production request authorization is unfinished.

Evidence: `networking trust boundary`, `isolation architecture`, and `Linux architecture guard`.

### 2. Basic interaction with pages

The shell can navigate through its address bar, but rendered pages are not yet interactive:

- No clickable links, page hit-testing, input focus, or form controls.
- No text selection or page scrolling; overflow is clipped.
- No automatic page mouse/keyboard events or their default actions.
- No downloads, persistent browser sessions, or complete history behavior. History currently stores URLs and reloads documents; same-document navigation, script History API, and bfcache are absent.

This is a substantial gap even for a modest usable browser.

Evidence: `page limitations`, `input dispatch`, and `history implementation`.

### 3. Loading complete web pages

Only the main HTML resource is loaded. Missing pieces include:

- Linked stylesheets, CSS imports, external scripts, images, fonts, media, and frame resources.
- Resource discovery, scheduling, and lifecycle integration.
- General HTTP methods/request bodies, streaming responses, HTTP caching, and authentication challenges.
- Full cookie policy and persistence. The browser currently disables cookies; the networking library’s optional cookie subset rejects Domain, SameSite, and Partitioned attributes.
- MIME sniffing. The byte-level HTML charset prescan (`<meta charset>`, pragma, XML declaration; first 1024 bytes) now feeds navigation decoding, but statistical/locale encoding detection, container inheritance and the parser's reparse on a later conflicting declaration are absent.

Linked stylesheets and external scripts are explicitly rejected rather than merely ignored.

Evidence: `stylesheet collection`, `script classification`, `HTML loading policy`, and `networking scope`.

### 4. General HTML and DOM compatibility

The tokenizer is considerably more complete than the tree builder. Remaining parser work includes:

- Tables, templates, forms, select/options, SVG/MathML, and other specialized parsing algorithms.
- Fragment parsing.
- Adoption-agency/active-formatting recovery for malformed markup.
- Broader legacy doctype handling.

The DOM also lacks full Web IDL bindings, specialized HTML element behavior, form state, custom-element reactions, and other browser APIs.

These are compatibility blockers: unsupported constructs can reject a page.

Evidence: `HTML tree-building scope`.

### 5. General CSS, layout, and painting

The current engine implements finite static block/inline rendering. Missing essentials include:

- Flexbox, grid, tables, floats, positioning, and margin collapse.
- Media queries, custom properties, calculations, modern selectors, and broader CSS properties.
- Images/replaced elements, controls, list markers, decorations, scrolling, and overflow layout.
- Transforms, gradients, rounded borders, stacking contexts, and advanced compositing.

**A particularly restrictive current limitation:** non-root vertical margins must be zero. Ordinary default paragraph/body styling therefore requires overrides. Some CSS values parse successfully but remain unsupported downstream in layout or paint.

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
2. **Scrolling, links, page input, and subresource loading** for basic usability.
3. **HTML recovery, ordinary layout, fonts, and common CSS** for static-site compatibility.
4. **Persistent scripting/event-loop integration and browser APIs** for interactive sites.
5. Broader standards, accessibility, conformance coverage, and release engineering.

*This was a read-only repository audit. No files were changed, and builds/tests or CI results were not rerun or independently verified.*
