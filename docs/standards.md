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
| `url`, `encoding`, `mime-sniffing` | Core URL, Encoding and MIME |
| `unicode-idna`, `unicode-bidi` | URL internationalized domains and Unicode bidi defaults |
| `fetch` | Networking and resource policies |
| `http-cookies`, `dotnet-cookie-container`, `dotnet-http-handler` | Basic session cookies and HTTP transport, not full browser cookie policy |
| `html`, `dom` | HTML parser, DOM, navigation, event loop |
| `css-syntax`, `selectors`, `css-cascade`, `css-values` | CSS parsing and computed styles |
| `css-box`, `css-display`, `css-inline`, `css-text`, `css-fonts` | Layout, shaping and painting |
| `ecmascript`, `webidl` | V8 host, future script/DOM bindings |
| `sdl-video`, `sdl-init`, `sdl-create-window`, `sdl-window-surface`, `sdl-events`, `sdl-text-input`, `sdl-linux` | Native windows, presentation and input |
| `xdg-directories`, `dotnet-process`, `dotnet-process-start`, `windows-fonts`, `fontconfig` | OS paths, process launch/lifecycle and font sources |

Add further official standards and native API references as their interfaces
are introduced. CSS drafts and living standards change: cite the exact section,
record the checked date and state the implemented subset. WPT/html5lib datasets
are test inputs managed separately, not substitutes for these documents.
Core tests and embedded encoding tables have explicit
[provenance and licensing](core.md#pinned-sources-and-licenses).
The approved IDNA baseline is stable Unicode 17, not Unicode 18 preview.
Networking has separate [pinned WPT data URL fixtures](../tests/Engine.Net.Tests/Data/README.md).

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
