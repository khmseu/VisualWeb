# Standards and local reference documents

The authoritative source registry is [specs/manifest.json](../specs/manifest.json).
It records IDs, official URLs, local filenames, fetch/check timestamps and HTTP
validators. The local HTML files live under `specs/cache/`; this generated
directory is ignored by git to avoid large document churn. The registry is
tracked. Each developer/agent checkout bootstraps its own local cache once.

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

| IDs | Used by |
| --- | --- |
| `url`, `encoding`, `mime-sniffing` | Core URL, Encoding and MIME (Core.Encoding also cites `html` for the charset prescan) |
| `unicode-idna`, `unicode-bidi` | URL internationalized domains and Unicode bidi defaults |
| `fetch` | Networking and resource policies |
| `http-cookies`, `dotnet-cookie-container`, `dotnet-http-handler` | Basic session cookies and HTTP transport, not full browser cookie policy |
| `html`, `dom` | HTML parser, DOM, navigation, event loop |
| `css-syntax`, `selectors`, `css-cascade`, `css-values` | CSS parsing and computed styles |
| `css-color`, `css-sizing`, `css-backgrounds` | Initial computed colors, dimensions and physical borders |
| `css2-visual`, `css2-sizing` | Initial block/inline formatting and geometry constraints |
| `harfbuzz-shaping`, `harfbuzz-font` | Native shaping and font APIs |
| `css2-paint`, `skia-canvas` | Normal-flow display order and native CPU canvas API |
| `css-box`, `css-display`, `css-inline`, `css-text`, `css-fonts` | Layout, shaping and painting |
| `ecmascript`, `webidl` | V8 host, future script/DOM bindings |
| `clearscript-v8`, `clearscript-v8-constraints` | Pinned native host API, interruption, monitored heap/stack and ArrayBuffer limits |
| `sdl-video`, `sdl-init`, `sdl-create-window`, `sdl-window-surface`, `sdl-events`, `sdl-text-input`, `sdl-linux` | Native windows, presentation and input |
| `xdg-directories`, `dotnet-process`, `dotnet-process-start`, `windows-fonts`, `fontconfig` | OS paths, process launch/lifecycle and font sources |
| `dotnet-process-pipes`, `dotnet-stream-read` | Private renderer pipes, continuous diagnostic draining and exact framed reads |
| `dotnet-host-environment` | Runtime host/root selection and deployment configuration |
| `bubblewrap`, `linux-seccomp`, `libseccomp`, `linux-resource-limits` | Linux renderer namespace/mount isolation, exact syscall filter and resource limits |
| `linux-cgroup-v2`, `systemd-run`, `systemd-resource-control` | Hard per-worker memory/swap/task/CPU accounting and transient scope lifecycle |
| `windows-appcontainer`, `windows-process-attributes`, `windows-job-objects` | Windows renderer token isolation, startup attributes and process limits |

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
