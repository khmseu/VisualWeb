# WHATWG URL Support Phase 2 Completion Report

## Phase 2 Complete: URL parser and serialization gaps

Corrected a verified relative-resolution bug: `DomUrl.TryCreate(input, baseUrl)` selected the parser for the string base URL using the relative input. An ASCII relative reference could therefore cause supplementary Unicode characters in the base path to be percent-encoded as separate UTF-16 surrogate halves instead of UTF-8. The fix selects the base parser from `baseUrl`; regression cases cover both an empty reference and `"child"`.

**Files created/changed:**

- `third_party/Dubzer.WhatwgUrl/DomUrl.cs`
- `third_party/Dubzer.WhatwgUrl/README.md`
- `tests/Core.Tests/UrlTests.cs`

**Functions created/changed:**

- `DomUrl.TryCreate(string input, string baseUrl)` now selects the parser using the base URL when parsing that URL.

**Tests created/changed:**

- Added regressions for empty and child relative references against a supplementary-Unicode base path.
- Existing pinned fixture tests cover 896 URL vectors, 87 `toascii` cases, and 2,670 Unicode 17 IDNA cases; all applicable cases passed.

**Validation:**

- Regression test failed before the fix, as expected.
- `dotnet test tests/Core.Tests/Core.Tests.csproj --no-restore --filter FullyQualifiedName~UrlTests` — passed, 3,794 tests.
- `dotnet test tests/Core.Tests/Core.Tests.csproj` — passed, 4,195 tests.
- `dotnet build VisualWeb.slnx` — passed, 0 warnings and 0 errors.
- `dotnet test VisualWeb.slnx --no-build` — passed; two browser process tests were skipped.
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party` — passed.
- `git diff --check` — passed.

**Review Status:** APPROVED. No blocking findings. The reviewer suggested, as an optional addition, a complementary Unicode-input/ASCII-base case.

**Git Commit Message:**

fix(core-url): select parser from URL base

Choose the parser for a string base URL from the base itself rather than the relative input. This preserves supplementary Unicode characters as UTF-8 when resolving ASCII relative references.
Add regressions for empty and child references. Existing pinned URL, toascii, and Unicode 17 IDNA vectors remain green.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
