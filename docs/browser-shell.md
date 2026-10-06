# Development browser shell

The approved implementation uses existing SDL portable windows/input/pixels
instead of a second UI toolkit. It supplies drawn address/tab chrome, bounded
session history, GET navigation, tab create/close/move and multiple windows.
**It is development tooling for trusted content,
not a secure or standards-complete production browser.**
Phase 9 supplies local rendering; phase 10a adds explicitly selected per-tab
worker processes. See the [process guide](renderer-processes.md) for launch,
wire limits, deadlines, crash/restart behavior and unfinished confinement.
Phase 10b adds opt-in [Linux x64 confinement](linux-confinement.md) and
[Windows confinement](windows-confinement.md); without `--require-sandbox`,
both modes remain unsandboxed. Unsupported OS versions/configurations fail closed.
Required Linux confinement includes phase-10c hard per-worker cgroup resource
limits; unavailable user-manager/controller support also fails closed.

## Run it

From the repository root, using an explicitly chosen trusted regular font:

```sh
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --development-single-process \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

The bundled [OFL font](../tests/Engine.Text.Tests/Data/README.md) is a pinned
development/test fixture, not automatic production font discovery. A supplied
font must contain the ASCII chrome glyphs and supported page text. There is no
fallback, synthetic bold/italic or nearest-weight matching.
The single supplied face is explicitly mapped to the generic serif/sans-serif/
monospace aliases; this is development caller configuration, not font metadata
matching or a guarantee that the supplied face has all those characteristics.

The explicit mode acknowledgement and font path are mandatory. `--help`
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

The visible tab strip follows the active tab when tabs exceed available slots.
Browser windows and tab IDs are distinct; tab identity/history/content survives
moving. Text input activation uses SDL committed text; clipboard, selection
ranges, IME preedit and richer editing/accessibility remain deferred.
Chrome labels are bounded ASCII displays (unsupported characters become `?`);
the underlying URL and page title are not modified by that display conversion.
Long labels/statuses are clipped; full navigation errors are also written to
stderr. The native window title is bounded to 120 Unicode scalars.

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
Decoding uses BOM precedence, then a valid MIME charset, otherwise explicit
UTF-8 fallback. The status identifies the encoding and missing HTML charset
prescan; `<meta charset>` is not a decoding-policy implementation. Unknown
charset labels and unsupported MIME fail visibly. HTTP error-status HTML
can render; status codes and transport diagnostics remain visible.

The shared VisualWeb.PageRendering StaticPageRenderer parses once and collects connected embedded `style` blocks
in document order, taking their child text. Inline attributes also participate.
Named stylesheet sets, non-CSS type values, media other than empty/`all`,
linked stylesheets and CSS imports fail explicitly rather than silently
producing a partially styled page. No subresource is fetched. Scripts are inert by default; the explicit
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
Engine.Content's new RenderParsed entry point keeps this collection policy
outside the engine pipeline and avoids parsing twice. GET/MIME/decoding remains
browser-owned in both modes.

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
are retained per tab; render-stage DOM/style/layout objects are not shared
between tabs or retained as a history cache. Font owners and loaders are
tab-local, while immutable configured font file paths and the chrome renderer
are shell-level resources.

Pages have no input/hit-testing, clickable links, controls, selection or
scrolling; overflow is clipped to the viewport. There are no downloads, storage,
automatic linked CSS, images/media or JavaScript/V8 yet. Native page crashes
can terminate the entire shell in single-process mode; multiprocess mode
contains worker failures to their tab. Browser-native chrome/platform crashes
remain shell failures. Production origin/CORS/CSP/confinement policy remains
**unfinished phase 10/future work**, not inferred from process separation.

## Validation

```sh
dotnet test tests/VisualWeb.Browser.Tests/VisualWeb.Browser.Tests.csproj
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --development-single-process --font tests/Engine.Text.Tests/Data/NotoSans.ttf \
  --smoke --backend dummy
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --development-single-process --font tests/Engine.Text.Tests/Data/NotoSans.ttf \
  --smoke --backend x11
```

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
