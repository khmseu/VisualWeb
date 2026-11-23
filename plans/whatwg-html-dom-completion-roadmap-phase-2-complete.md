# Common HTML/DOM Support Phase 2 Completion Report

## Phase 2 Complete: Common document and fragment parsing

Ordinary in-body document parsing and body-context fragments now recover from
table-structure start tags by reporting a parse error and ignoring the tag;
unmatched table-structure end tags use the existing generic in-body recovery.
Valid table construction and explicit failures outside the supported in-body
context remain unchanged. Foreign-content parsing was moved to HDF-005 after
inspection showed that correct support requires namespace-aware DOM storage and
additional parser algorithms, not a low-cost parser-only change.

**Files created/changed:**

- `src/Engine/Engine.Html/HtmlParser.cs`
- `tests/Engine.Html.Tests/TreeTests.cs`
- `tests/Engine.Html.Tests/FragmentTests.cs`
- `docs/html-dom.md`
- `src/Engine/Engine.Html/README.md`
- `docs/standards.md`
- `plans/whatwg-html-dom-completion-roadmap.md`
- `plans/html-dom-deferred.md`
- `Kontext/Wissensstand.md`
- `Kontext/Kontextprotokoll.md`
- `plans/whatwg-html-dom-completion-roadmap-phase-2-complete.md`

**Functions created/changed:**

- `HtmlParser.Builder.Process`
- `HtmlParser.Builder.BodyToken`

**Tests created/changed:**

- `InBodyTableStructureStartsAreIgnoredWithoutDisruptingContent`
- `InBodyUnmatchedTableStructureEndsAreIgnoredWithoutDisruptingContent`
- `TableStructureTagsInOtherUnsupportedContextsStillFail`
- `BodyContextIgnoresTableStructureStartsAndKeepsFollowingContent`
- `BodyContextIgnoresUnmatchedTableStructureEndsAndKeepsFollowingContent`
- `SelectContextStillRejectsTableStructureEndTags`

**Review Status:** APPROVED after updating HDF-011 to consistently defer
SVG/MathML namespace support under HDF-005.

**Validation:**

- `dotnet build VisualWeb.slnx`: passed, 0 warnings and 0 errors.
- `dotnet test tests/Engine.Html.Tests/Engine.Html.Tests.csproj`: passed,
  7,713 tests.
- `dotnet test VisualWeb.slnx --no-build`: passed; 1,396 browser tests passed
  and 2 platform-specific tests were skipped.
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party`:
  passed.
- `git diff --check`: passed.

**Git Commit Message:**

```text
fix(html): recover stray table structure tags

Apply in-body recovery to table-structure starts and unmatched ends in
documents and body-context fragments while preserving valid table parsing and
explicit failures in unsupported modes.

Document the bounded behavior and defer namespace-aware SVG/MathML parsing to
HDF-005 rather than producing HTML-namespace-only trees.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```
