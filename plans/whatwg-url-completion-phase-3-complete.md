# WHATWG URL Support Phase 3 Completion Report

## Phase 3 Complete: Core URL operations

Closed confirmed Core URL setter and origin gaps while preserving the immutable `BrowserUrl` snapshot contract. The corrections follow the cached URL Standard's scheme, host, port, and blob-origin algorithms; file origins remain opaque under the Core policy.

**Files created/changed:**

- `src/Core/Core.Url/BrowserUrl.cs`
- `src/Core/Core.Url/SecurityOrigin.cs`
- `src/Core/Core.Url/README.md`
- `tests/Core.Tests/UrlTests.cs`
- `tests/Core.Tests/SecurityOriginTests.cs`
- `docs/core.md`

**Functions created/changed:**

- `BrowserUrl.WithProtocol` allows valid file-to-special-scheme transitions for non-empty file hosts, preserving empty-host, credentials, and port restrictions.
- `BrowserUrl.WithPort` and `BrowserUrl.WithHost` share ASCII digit-prefix port parsing, including default-port normalization and out-of-range rejection.
- `BrowserUrl.WithHost` preserves file-host state-override behavior for drive-letter-like input.
- `BrowserUrl.WithSearch` and `BrowserUrl.WithHash` remove ASCII tabs/newlines in parser order while preserving delimiter semantics.
- `BrowserUrl.ComputeOrigin` applies the blob path-URL allowlist from the URL Standard: HTTP, HTTPS, and FILE. File origins remain opaque; unsupported tuple-like paths (FTP, WS, WSS) remain opaque.
- `SecurityOrigin` tuple comparison and serialization coverage was extended.

**Tests created/changed:**

- Protocol state override for hosted and empty-host file URLs.
- Port numeric-prefix, default-port, out-of-range, empty, and nonnumeric cases for port and host setters.
- File `WithHost("C:")` no-op behavior.
- Search/hash tab removal around query and fragment delimiters.
- Blob HTTP/HTTPS/FILE origin behavior and opaque FTP/WS/WSS cases; tuple and opaque-origin identity comparisons.

**Validation:**

- New regression tests failed before their respective fixes.
- Focused URL, URLSearchParams, and SecurityOrigin tests — passed, 3,886 tests.
- `dotnet test tests/Core.Tests/Core.Tests.csproj` — passed, 4,217 tests.
- `dotnet build VisualWeb.slnx` — passed, 0 warnings and 0 errors.
- `dotnet test VisualWeb.slnx --no-build` — passed, 17,164 tests; two browser process tests were skipped.
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party` — passed.
- `git diff --check` — passed.

**Review Status:** APPROVED after revisions. Final review found no blocking issues.

**Git Commit Message:**

fix(core-url): align URL setter state overrides

Correct Core URL protocol, host, port, search, hash, and blob-origin behavior to follow the URL Standard's state overrides and origin allowlist.
Add regressions for file-scheme transitions, numeric port prefixes, file hosts, blob origins, and delimiter cleanup while preserving immutable snapshots.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
