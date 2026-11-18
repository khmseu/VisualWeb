## Phase 4 Complete: Mutable URL and live searchParams

Added a bounded mutable Core URL model with stable `URLSearchParams` identity and bidirectional query synchronization. `BrowserUrl` remains immutable; replacing `Href` or `Search` refreshes the same params instance, and params updates replace the URL snapshot while retaining other components.

**Files created/changed:**

- `src/Core/Core.Url/MutableBrowserUrl.cs`
- `src/Core/Core.Url/UrlSearchParams.cs`
- `tests/Core.Tests/MutableBrowserUrlTests.cs`
- `docs/core.md`
- `src/Core/Core.Url/README.md`

**Functions created/changed:**

- `MutableBrowserUrl` exposes mutable `Href` and `Search`, an immutable `Snapshot`, and one stable linked `SearchParams` instance.
- `UrlSearchParams` supports internal mutation notification and in-place parameter refresh for URL replacement.

**Tests created/changed:**

- Stable `SearchParams` identity and URL-to-params/params-to-URL synchronization.
- Preservation of non-query URL components and unchanged state after invalid `Href`.
- Empty-query semantics: `Search = "?"` retains an empty query until a parameter update; sorting empty params updates the URL and clears the query delimiter.
- Single-entry `sort()` update normalizes `?a` to `?a=`.

**Validation:**

- Focused `MutableBrowserUrlTests` — passed; the follow-up single-entry regression passed.
- `dotnet test tests/Core.Tests/Core.Tests.csproj` — passed, 4,223 tests.
- `dotnet build VisualWeb.slnx` — passed, 0 warnings and 0 errors.
- `dotnet test VisualWeb.slnx --no-build` — passed, 17,170 tests; two browser process tests were skipped.
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party` — passed.
- `git diff --check` — passed.

**Review Status:** APPROVED. The first review's `sort()` concern was resolved against the cached URL Standard's sort and update algorithms; no code change to `Sort()` was required.

**Git Commit Message:**

feat(core-url): add live searchParams linkage

Add a mutable Core URL model with one stable URLSearchParams instance and bidirectional query synchronization.
Preserve immutable BrowserUrl snapshots, non-query components, and empty-query update behavior.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
