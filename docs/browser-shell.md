# Development browser shell

The approved implementation uses existing SDL portable windows/input/pixels
instead of a second UI toolkit. It supplies drawn address/tab chrome, bounded
session history, GET navigation, tab create/close/move and multiple windows.
**It is development tooling for trusted content,
not a secure or standards-complete production browser.**
Phase 9 supplies local rendering; phase 10a adds explicitly selected per-tab
worker processes. See the [process guide](renderer-processes.md) for launch,
wire limits, deadlines, crash/restart behavior and unfinished confinement.
Phase 10b adds [Linux x64 confinement](linux-confinement.md) and
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

| Control | Behavior |
| --- | --- |
| Address bar / Ctrl+L | Focus address and select all |
| Committed text / Ctrl+A | Insert text / select whole address |
| Left/Right, Home/End, Backspace/Delete | Move caret or edit Unicode scalar boundaries |
| Enter / Escape | Navigate / restore committed URL and leave address editing |
| B / Alt+Left | Back |
| F / Alt+Right | Forward |
| R / F5 / Ctrl+R | Reload committed URL |
| +T / Ctrl+T | New tab with offline welcome page |
| Tab label | Activate that tab |
| Tab x / Ctrl+W | Close tab; last tab closes its window |
| Tab arrows / Ctrl+Tab / Ctrl+Shift+Tab | Cycle next/previous tab |
| +W / Ctrl+N | New window with welcome tab |
| M / Ctrl+M | Move active tab to the first other window, or create an empty destination |
| Native window close | Close that window and only its tabs |
| Primary left click on visible anchor text | Navigate the current tab to its resolved href |

The visible tab strip follows the active tab when tabs exceed available slots.
Browser windows and tab IDs are distinct; tab identity/history/content survives
moving. Text input activation uses SDL committed text; clipboard, selection
ranges, IME preedit and richer editing/accessibility remain deferred.
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
address editing. Horizontal scrolling, painted scrollbars and general DOM input/default
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
without rerunning scripts. Targets follow text paint order; the last matching
rectangle wins. Empty anchors and non-link text have no targets.

The renderer resolves destinations with Core.Url against the final `LoadedPage.Url`
before returning data-only rectangles and absolute URLs; it never navigates or
fetches. Primary left-button presses in the page region subtract the 120-logical-
pixel chrome height. SDL window-local logical coordinates already match CSS
pixels: pixel density scales the raster, not pointer coordinates. Chrome,
other buttons, releases and misses do not navigate. A frame whose viewport/scale
does not match the current window (for example, during asynchronous resize)
cannot activate links.

Activation uses the current tab's ordinary `Navigate` transaction and shared
session HSTS loader. Only `http:`, `https:`, `file:` and `data:` are supported;
other schemes, including `javascript:`, fail visibly before loading. URL/count/
metadata limits and malformed hrefs fail visibly during rendering. Failed
navigation keeps the old frame, link targets, scroll position, origin and history.
Relative and fragment URL components are preserved, but fragment activation
currently performs a full navigation, not same-document scrolling.
`target`, downloads, `<base>` semantics, image/area links, link decoration,
keyboard activation, page mouse events and JavaScript default-action cancellation
remain deferred. This is a bounded subset of HTML
[following hyperlinks](https://html.spec.whatwg.org/multipage/links.html#following-hyperlinks)
(cached standard ID `html`), not a full DOM event/default-action implementation.

## Navigation and page policy

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
computation, cross-site frame isolation, inherited origin selection,
same-origin/CORS/CSP policy or production request authorization.

The shared VisualWeb.PageRendering StaticPageRenderer parses once and collects connected embedded `style` blocks
and supported linked stylesheets in document (cascade) order, taking style child text. Inline attributes also participate.
Named stylesheet sets, non-CSS type values, media other than absent/`all`
and CSS imports fail explicitly rather than silently producing a partially styled page.

Bounded linked stylesheets are brokered by the browser, never by renderers.
`GetPageSource` parses the decoded document with the renderer's exact parser
options (bounded managed parsing in the browser process, for discovery only),
then fetches each supported classic `link rel=stylesheet` (tokens ASCII
case-insensitive; `href` resolved with Core.Url against the final response URL)
through the same tab-local `ResourceLoader`, sharing the shell session HSTS store,
before the document is published. Requests keep cookies disabled and the bad-port
and HSTS checks; only HTTP(S) and data URLs are allowed, plus file URLs from file
documents. Links without a nonempty `href` create no sheet, as in HTML.
Alternate/titled/disabled links, `media` other than absent/`all`, `type` other than
absent/empty/`text/css`, `crossorigin`, `integrity`, `referrerpolicy`, the obsolete
`charset` attribute, `<base href>` and other schemes fail navigation. Each response
must be 2xx `text/css` (a MIME-less file only from a file document; no sniffing
or quirks-mode fallback), at most 1 MiB of body bytes and 256 Ki decoded characters.
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
`@import`, `url()` resources, fonts, images, CORS/SOP/mixed-content policy and
caching remain unimplemented; unsupported CSS still yields diagnostics that fail layout.
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
Pages should set non-root vertical margins to zero while collapse is deferred;
the welcome page also overrides bold/italic UA defaults for the configured
regular face. Unsupported executable script features in opt-in mode, layout algorithms, CSS diagnostics and
paint features remain errors. See [HTML](html-dom.md), [CSS](css.md),
[text/layout](text-layout.md) and [painting](painting.md).

Successful load **and rendering** commit history, final redirected URL, title
and current frame atomically. Failure preserves the old document/frame and
history index; the address retains the attempted URL, with a red error status
and stderr diagnostic. Traversal refetches/rerenders URL-only history; reload
replaces the current entry rather than appending. A new successful navigation
discards the forward branch. There is no bfcache, same-document fragment
navigation, iframe history, History API or session persistence.

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

| Shell limit | Default |
| --- | ---: |
| Windows | 8 |
| Total tabs | 32 |
| URL-only history entries per tab | 128 |
| Typed address UTF-16 characters | 8192 |
| Pending loads, including canceled ones not yet finished | 64 |
| Frame pixels per page/window | 4,194,304 |
| Response bytes per tab load | 4 MiB |

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
