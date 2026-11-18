## Phase 1 Complete: URLSearchParams conformance baseline

Added pinned, offline WPT-derived vectors for form-urlencoded parsing, URLSearchParams stringification, and sequence construction, with fixture-driven tests and documented provenance. All selected vectors matched existing behavior, so no production implementation changes were needed.

**Files created/changed:**

- `tests/Core.Tests/Data/urlencoded-parser-wpt.json`
- `tests/Core.Tests/Data/urlsearchparams-stringifier-wpt.json`
- `tests/Core.Tests/Data/urlsearchparams-constructor-wpt.json`
- `tests/Core.Tests/Data/README.md`
- `tests/Core.Tests/UrlSearchParamsTests.cs`

**Functions created/changed:**

- Fixture-driven URL-encoded parser, stringifier, and sequence-constructor tests, with expected-count guards.

**Tests created/changed:**

- 35 pinned URL-encoded parser vectors
- 12 pinned URLSearchParams stringifier vectors
- 1 pinned sequence-constructor vector
- Focused URLSearchParams suite: 27 passed
- Core suite: 4,194 passed

**Validation:**

- `dotnet build VisualWeb.slnx` — passed, 0 warnings and 0 errors
- `dotnet test VisualWeb.slnx --no-build` — passed; no failures, two browser process tests were reported skipped
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party` — passed
- `git diff --check` — passed

**Review Status:** APPROVED; expected fixture counts and source blob hashes were added following review recommendations.

**Git Commit Message:**

test(core-url): add pinned URLSearchParams vectors

Pin WPT-derived parser, stringifier, and sequence-constructor vectors with
revision, source paths, blob hashes, and license provenance.
Add fixture-driven tests and expected-count guards while preserving existing
unit coverage. No production URL behavior changed.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
