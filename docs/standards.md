# Standards sources, support status, and local reference documents

The authoritative source registry is [specs/manifest.json](../specs/manifest.json).
It records IDs, official URLs, local filenames, fetch/check timestamps and HTTP
validators. The local HTML files live under `specs/cache/`; this generated
directory is ignored by git to avoid large document churn. The registry is
tracked. Each developer/agent checkout bootstraps its own local cache once.
Implemented subsets and explicit omissions are recorded in the support inventory
below.

From the repository root:

```sh
dotnet run --project tools/SpecCache -- specs/manifest.json
```

Missing/empty files are fetched even if metadata is fresh. Otherwise files last
checked **more than 30 days ago** are revalidated using ETag/Last-Modified when
available. A 304 advances `checkedAt`, not `fetchedAt`; successful downloads
advance both. Fresh files cause no HTTP requests. Refresh is explicit: builds
and tests never download or refresh documents. Agents should run the refresh
as part of standards-dependent work, not every tool call.

Keep cache documents for offline work. Failed downloads retain existing copies,
leave failed entries stale and return a nonzero exit code; report stale evidence
instead of silently claiming current standards. `--force` revalidates every
entry (it still uses conditional requests where possible).

## Initial document ownership

| IDs                                                                                                             | Used by                                                                                                                                               |
| --------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `url`, `encoding`, `mime-sniffing`                                                                              | Core URL, Encoding and MIME (Core.Encoding also cites `html` for the charset prescan)                                                                 |
| `unicode-idna`, `unicode-bidi`                                                                                  | URL internationalized domains and Unicode bidi defaults                                                                                               |
| `fetch`                                                                                                         | Networking and resource policies                                                                                                                      |
| `rfc6797`                                                                                                       | Bounded, memory-only DNS-host HSTS learning/upgrades shared within a browser shell session                                                            |
| `http-cookies`, `dotnet-cookie-container`, `dotnet-http-handler`                                                | Basic session cookies and HTTP transport, not full browser cookie policy                                                                              |
| `html`, `dom`                                                                                                   | HTML parser, DOM, navigation, event loop, bounded `body[bgcolor]`/`body[text]`/`body[link]` color hints and legacy table-border rendering             |
| `pointerevents4`                                                                                                | Bounded trusted link `click`/`auxclick` event naming; pointer metadata remains unsupported                                                            |
| `css-syntax`, `selectors`, `css-cascade`, `css-values`, `css-overflow`                                          | CSS parsing, computed styles and bounded overflow properties                                                                                          |
| `css-color`, `css-sizing`, `css-backgrounds`                                                                    | Initial computed colors, dimensions and physical borders                                                                                              |
| `css-mediaqueries-5`, `css-conditional-3`                                                                       | Screen-only `all`/`screen`/`print` media-type lists for stylesheet imports and nested `@media` rules                                                  |
| `css-text-decoration`, `css-text-decoration-4`                                                                  | Bounded combinations of underline/overline/line-through, solid/double style, color/thickness, shorthand expansion and ancestor decoration propagation |
| `css2-visual`, `css2-sizing`                                                                                    | Initial block/inline formatting and geometry constraints                                                                                              |
| `css2-lists`                                                                                                    | Bounded outside disc/circle/square/decimal markers and inherited list-style longhands                                                                 |
| `css-counter-styles`                                                                                            | Predefined zero-padded decimal, ASCII alphabetic and Roman markers, ranges and decimal fallback                                                       |
| `css2-tables`                                                                                                   | Canonical HTML automatic intrinsic columns, separate borders, top-aligned cells, bounded table-layer backgrounds and legacy table-border rendering    |
| `unicode-linebreak`                                                                                             | Bounded manual U+00AD, ASCII-hyphen and U+2010-HYPHEN breaks plus infix-separator no-breaks; not general Unicode line breaking                        |
| `harfbuzz-shaping`, `harfbuzz-font`                                                                             | Native shaping and font APIs                                                                                                                          |
| `css2-paint`, `skia-canvas`                                                                                     | Normal-flow display order and native CPU canvas API                                                                                                   |
| `css-box`, `css-display`, `css-inline`, `css-text`, `css-fonts`, `css-overflow`                                 | Layout, shaping, painting and overflow clipping                                                                                                       |
| `css-overscroll`                                                                                                | Scroll chaining and author control over boundary default actions                                                                                      |
| `ecmascript`, `webidl`                                                                                          | V8 host, future script/DOM bindings                                                                                                                   |
| `clearscript-v8`, `clearscript-v8-constraints`                                                                  | Pinned native host API, interruption, monitored heap/stack and ArrayBuffer limits                                                                     |
| `sdl-video`, `sdl-init`, `sdl-create-window`, `sdl-window-surface`, `sdl-events`, `sdl-text-input`, `sdl-linux` | Native windows, presentation and input                                                                                                                |
| `xdg-directories`, `dotnet-process`, `dotnet-process-start`, `windows-fonts`, `fontconfig`                      | OS paths, process launch/lifecycle and font sources                                                                                                   |
| `dotnet-process-pipes`, `dotnet-stream-read`                                                                    | Private renderer pipes, continuous diagnostic draining and exact framed reads                                                                         |
| `dotnet-host-environment`                                                                                       | Runtime host/root selection and deployment configuration                                                                                              |
| `bubblewrap`, `linux-seccomp`, `libseccomp`, `linux-resource-limits`                                            | Linux renderer namespace/mount isolation, exact syscall filter and resource limits                                                                    |
| `linux-cgroup-v2`, `systemd-run`, `systemd-resource-control`                                                    | Hard per-worker memory/swap/task/CPU accounting and transient scope lifecycle                                                                         |
| `windows-appcontainer`, `windows-process-attributes`, `windows-job-objects`                                     | Windows renderer token isolation, startup attributes and process limits                                                                               |

Add further official standards and native API references as their interfaces
are introduced. CSS drafts and living standards change: cite the exact section,
record the checked date and state the implemented subset. WPT/html5lib datasets
are test inputs managed separately, not substitutes for these documents.
Core tests and embedded encoding tables have explicit
[provenance and licensing](core.md#pinned-sources-and-licenses).
The approved IDNA baseline is stable Unicode 17, not Unicode 18 preview.
Networking has separate [pinned WPT data URL fixtures](../tests/Engine.Net.Tests/Data/README.md).
HTML uses pinned [html5lib token/tree fixtures](../tests/Engine.Html.Tests/Data/README.md)
and [official named-reference data](../src/Engine/Engine.Html/Data/README.md).
Data is independent of the monthly document cache. The current cached HTML
Standard includes processing-instruction tokenization; the fixture guide records
the compatibility translation for three obsolete tokenizer error names.
CSS has separate [pinned WPT An+B data](../tests/Engine.Css.Tests/Data/README.md);
the [CSS guide](css.md) identifies finite property/value support and the
current Selectors Level 4 whitespace rule for `:empty`.
Text/layout uses a separate [pinned OFL Noto Sans font](../tests/Engine.Text.Tests/Data/README.md)
for offline native shaping evidence, not a standards-document download.

## Interface citation convention

Use XML docs linking the official source and anchor, plus the registry ID:

```csharp
/// <summary>Parses a URL using the supported WHATWG algorithm subset.</summary>
/// <remarks>
/// Spec: url; <see href="https://url.spec.whatwg.org/#concept-basic-url-parser">basic URL parser</see>.
/// </remarks>
```

Read the corresponding section in `specs/cache/url.html` first. OS/internal
interfaces need relevant SDL/.NET/OS API documentation when appropriate,
not invented web-standard citations. The tool keeps source HTML with its
copyright/license notices intact; cached standards remain subject to their
publishers' terms and are not project-authored documentation.

## Support status

This inventory records the current support status of web-platform standards
and related specifications used by VisualWeb. It is a scope guide, not a
conformance declaration or a substitute for the subsystem documentation and
tests. A standards citation, cached source, or passing fixture family does not
by itself establish conformance.

### Status meanings

- **Partial** — a documented subset is implemented; the linked guide describes
  its behavior and important omissions.
- **Not implemented** — the source is referenced for future work or a related
  concept, but the specified feature is not implemented.
- **Full** — reserved for a standard whose complete applicable requirements
  have been demonstrated. VisualWeb currently makes no full-conformance claim
  for any web standard in this inventory.

The scope is the web standards and Unicode/RFC sources in the
[standards registry](../specs/manifest.json) that define implemented or
planned web-facing behavior. SDL, .NET, OS, HarfBuzz, Skia and ClearScript
documents are implementation/API references, not web-platform conformance
claims, and are not listed as supported standards here.

### Implemented subsets

| Standard or source ID                                                                                       | Status  | Current scope                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| ----------------------------------------------------------------------------------------------------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| WHATWG URL (`url`)                                                                                          | Partial | URL parsing, resolution, serialization and URL-origin values through an immutable facade and vendored parser. APIs such as `URLSearchParams`, mutable URL objects, and complete URL-standard conformance are not claimed. See [Core](core.md#url).                                                                                                                                                                                                                                                                                                                                                                    |
| Unicode IDNA Compatibility Processing (`unicode-idna`) and Unicode Bidirectional Algorithm (`unicode-bidi`) | Partial | URL domain processing uses the approved stable Unicode 17 IDNA baseline and bounded bidi validation. This is not a general Unicode or bidi implementation. See [Core](core.md#url).                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| WHATWG Encoding (`encoding`)                                                                                | Partial | Encoding labels and whole-buffer decoding, BOM handling, and bounded HTML charset prescan/sniffing are implemented. Incremental decoding, encoders, and JavaScript `TextDecoder` are deferred. See [Core](core.md#encoding).                                                                                                                                                                                                                                                                                                                                                                                          |
| WHATWG Fetch (`fetch`)                                                                                      | Partial | A privileged GET-only loader implements selected URL, redirect, body-limit and bad-port behavior, with separate opt-in same-origin and credentialless CORS subsets. It is not script-visible Fetch or browser-wide request policy. See [Networking](networking.md).                                                                                                                                                                                                                                                                                                                                                   |
| RFC 6265 HTTP State Management (`http-cookies`)                                                             | Partial | Basic bounded session cookies are opt-in and loader-owned; this is not full browser cookie policy or persistent storage. See [Networking](networking.md#basic-cookies-not-full-browser-cookie-policy).                                                                                                                                                                                                                                                                                                                                                                                                                |
| RFC 6797 HSTS (`rfc6797`)                                                                                   | Partial | Explicitly injected, bounded, memory-only session learning and upgrades for authenticated HTTPS DNS hosts. Preload, persistence, and broader HSTS policy are absent. See [Networking](networking.md#session-hsts).                                                                                                                                                                                                                                                                                                                                                                                                    |
| Upgrade Insecure Requests (`upgrade-insecure-requests`)                                                     | Partial | The browser adds the bounded main-document `Upgrade-Insecure-Requests: 1` signal; broader policy and URL-upgrade behavior are not implemented. See [Networking](networking.md#main-document-navigation-request-signal).                                                                                                                                                                                                                                                                                                                                                                                               |
| WHATWG HTML (`html`)                                                                                        | Partial | First-party tokenization and a bounded static document-tree subset; HTML charset sniffing is separately bounded. Layout honors `<wbr>` and the `&shy;` character reference as bounded optional line-break opportunities. CSS supports bounded `body[bgcolor]`, `body[text]` and `body[link]` legacy color hints, `<hr>` defaults, `width`/`align`, `color`/`noshade` solid-border hints, and the conditional `size` hints. Full tree construction, fragment parsing, scheduling and broad element behavior are not implemented. See [HTML and DOM](html-dom.md), [CSS](css.md) and [Text and layout](text-layout.md). |
| WHATWG DOM (`dom`)                                                                                          | Partial | Renderer-local nodes, checked mutations, attributes, and selected bounded script-facing operations. Full DOM, shadow DOM, mutation observers, ranges and complete Web IDL behavior are not implemented. See [HTML and DOM](html-dom.md) and [Scripting](scripting.md).                                                                                                                                                                                                                                                                                                                                                |
| Pointer Events Level 4 (`pointerevents4`)                                                                   | Partial | A bounded set of trusted link `click`/`auxclick` event naming and dispatch hooks; pointer metadata and the PointerEvent interface are unsupported. See [Browser shell](browser-shell.md) and [Scripting](scripting.md).                                                                                                                                                                                                                                                                                                                                                                                               |
| ECMAScript (`ecmascript`)                                                                                   | Partial | A private V8 host runs bounded classic-script batches and optional inline classics; browser globals, modules, timers, workers, and a persistent HTML event loop are absent. See [Scripting](scripting.md).                                                                                                                                                                                                                                                                                                                                                                                                            |
| Web IDL (`webidl`)                                                                                          | Partial | Selected manually implemented DOM facade semantics only. General/generated Web IDL bindings and complete prototype/constructor behavior are deferred. See [Scripting](scripting.md).                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| UAX #14 Line Breaking (`unicode-linebreak`)                                                                 | Partial | Static text layout supports manual U+00AD breaks, a narrow ASCII/U+2010 hyphen subset and infix-separator no-breaks, not general Unicode line breaking. See [Text and layout](text-layout.md).                                                                                                                                                                                                                                                                                                                                                                                                                        |

### CSS standards

VisualWeb has a first-party static CSS implementation with a finite property,
selector, layout and paint subset. Unsupported syntax/features produce
diagnostics or explicit failures rather than a claim of general browser CSS
support. See the [CSS](css.md), [Text and layout](text-layout.md), and
[Painting](painting.md) guides for exact details.

| Standards / source IDs                                                                                                           | Status          | Current scope                                                                                                                                                                                                                                                                                                                                                                                                                 |
| -------------------------------------------------------------------------------------------------------------------------------- | --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CSS Syntax Level 3 (`css-syntax`)                                                                                                | Partial         | Tokenization, stylesheet/declaration parsing and bounded recovery; not CSSOM or complete at-rule semantics.                                                                                                                                                                                                                                                                                                                   |
| Selectors Level 4 (`selectors`)                                                                                                  | Partial         | Static selectors and selected functional/pseudo-class selectors; dynamic states, pseudo-elements, `:has`, namespaces and other listed unsupported features remain unsupported.                                                                                                                                                                                                                                                |
| CSS Cascade and Inheritance Level 5 (`css-cascade`)                                                                              | Partial         | Static source-ordered cascade, inheritance and a finite computed-value set; not the complete cascade or CSSOM.                                                                                                                                                                                                                                                                                                                |
| CSS Values and Units Level 4 (`css-values`)                                                                                      | Partial         | A finite set of supported value forms and units; unsupported values are diagnosed.                                                                                                                                                                                                                                                                                                                                            |
| CSS Conditional Rules Level 3 and Media Queries Level 5 (`css-conditional-3`, `css-mediaqueries-5`)                              | Partial         | Comma-separated `all`, `screen` and `print` media-type lists for bounded `@media` and browser `@import` handling; conditions and other media types are unsupported.                                                                                                                                                                                                                                                           |
| CSS Color Level 4 (`css-color`)                                                                                                  | Partial         | The finite color syntax and computed colors documented by the CSS guide.                                                                                                                                                                                                                                                                                                                                                      |
| CSS Sizing Level 3 and CSS 2.2 sizing (`css-sizing`, `css2-sizing`)                                                              | Partial         | Supported dimensions and initial block/inline geometry constraints only.                                                                                                                                                                                                                                                                                                                                                      |
| CSS Backgrounds and Borders Level 3 (`css-backgrounds`)                                                                          | Partial         | Selected solid backgrounds and physical solid/inset/outset/dotted/dashed/double/groove/ridge borders, plus bounded canvas/body background propagation; images, gradients, radii and broader effects are unsupported.                                                                                                                                                                                                          |
| CSS 2.2 visual formatting, CSS Box, Display, Inline and Text (`css2-visual`, `css-box`, `css-display`, `css-inline`, `css-text`) | Partial         | Finite horizontal LTR block/inline layout, first-line indentation, final-line alignment, ASCII text-transform, bounded word-spacing, pre-wrap and break-spaces (including preserved-space wrapping around supported atomic form controls), `<wbr>`, manual U+00AD and bounded U+2010 hyphen opportunities, whitespace and selected line breaking; advanced layout, bidi, writing modes and full overflow behavior are absent. |
| CSS Fonts Level 4 (`css-fonts`)                                                                                                  | Partial         | Explicitly registered faces and a bounded family/size/weight/style selection model; no fallback, synthetic styles, font download or general font-feature support.                                                                                                                                                                                                                                                             |
| CSS Text Decoration Level 3 (`css-text-decoration`)                                                                              | Partial         | Bounded distinct combinations of underline/overline/line-through with solid/double style, computed color and shorthand components; registered-font metrics and originating-element propagation; blink, other styles/behavior unsupported.                                                                                                                                                                                     |
| CSS Text Decoration Level 4 (`css-text-decoration-4`)                                                                            | Partial         | Bounded non-inherited thickness values (auto/from-font, named widths, nonnegative supported lengths/percentages) for the existing three line types; other Level 4 behavior is unsupported.                                                                                                                                                                                                                                    |
| CSS 2.2 lists and CSS Counter Styles Level 3 (`css2-lists`, `css-counter-styles`)                                                | Partial         | Bounded disc/circle/square, decimal, ASCII alphabetic and Roman list markers and related properties.                                                                                                                                                                                                                                                                                                                          |
| CSS 2.2 Tables (`css2-tables`)                                                                                                   | Partial         | Static table construction/layout for documented captions, columns, groups, rows, cells and bounded legacy table-border rendering; this is not general table layout.                                                                                                                                                                                                                                                           |
| CSS 2.2 painting order (`css2-paint`)                                                                                            | Partial         | Normal-flow backgrounds, selected borders, markers and shaped text in a CPU raster pipeline; stacking contexts, transforms and broad painting behavior are unsupported.                                                                                                                                                                                                                                                       |
| CSS Overflow Level 3 (`css-overflow`)                                                                                            | Partial         | Only the finite properties/values documented by the CSS guide; general overflow clipping and scrolling are not implemented.                                                                                                                                                                                                                                                                                                   |
| CSS Overscroll Behavior Level 1 (`css-overscroll`)                                                                               | Not implemented | Scroll chaining and author control over boundary actions are not implemented.                                                                                                                                                                                                                                                                                                                                                 |

### Referenced but not implemented

| Standard or source ID                    | Current relationship                                                                                                                        |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| WHATWG MIME Sniffing (`mime-sniffing`)   | MIME type parsing/serialization exists, but context-dependent byte sniffing and MIME classification are deferred. See [Core](core.md#mime). |
| Content Security Policy Level 3 (`csp3`) | A redirect-count signal can support caller-owned policy checks; the loader does not parse or enforce CSP. See [Networking](networking.md).  |

The Public Suffix List (`public-suffix-list`) is pinned data used by the bounded
networking matcher and HSTS policy, not a web-platform standard whose full
behavior VisualWeb claims to implement. Likewise, passing WPT, html5lib, or
other focused tests is evidence for only the tested cases and documented
subset.

### Keeping this inventory current

When a change adds, removes, or materially changes standards-defined behavior:

1. Update the relevant status and scope row here in the same change.
2. Keep the owning subsystem guide and standards registry consistent; add a
   registry entry when a newly referenced source is introduced.
3. Use **Partial** unless the complete applicable standard has been verified;
   describe important omissions explicitly. Do not infer support from a
   citation, project name, API shape, or test count.

For source refresh and cache workflow, see the instructions at the start of
this document; for interface citation rules, see the
[Interface citation convention](#interface-citation-convention).
