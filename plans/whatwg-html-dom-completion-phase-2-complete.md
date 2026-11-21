# Bounded HTML Template Support Phase 2 Completion Report

## Phase 2 Complete: Bounded template tree construction

The HTML parser now constructs document templates in head/body using the bounded ordinary in-body subset. Content, including nested templates, is kept in separate inert owner-document fragments; parser mode resets restore the surrounding head/body behavior. Unsupported insertion contexts still fail explicitly.

**Files created/changed:**

- `src/Engine/Engine.Html/HtmlParser.cs`
- `tests/Engine.Html.Tests/TreeTests.cs`
- `src/Engine/Engine.Html/README.md`
- `src/Engine/Engine.Dom/README.md`
- `docs/html-dom.md`
- `docs/standards.md`
- `plans/Missing.md`
- `Kontext/Wissensstand.md`
- `Kontext/Kontextprotokoll.md`

**Functions created/changed:**

- `Builder.TemplateToken` handles bounded template insertion-mode rules.
- `Builder.StartTemplate` and `Builder.CloseTemplate` maintain template elements and the template insertion-mode stack.
- `Builder.ResetInsertionModeAppropriately` restores the current template mode or surrounding head/body mode.
- `Builder.InsertionParent` routes elements and miscellaneous nodes into template `Content`.
- `Builder.Insert` counts each implicit template content fragment toward `MaxNodes`.
- `Builder.CompleteScript` suppresses parser completion callbacks for scripts inside template contents.

**Tests created/changed:**

- Basic and nested template parsing, including nested templates after ordinary and void elements.
- Separate fragment ownership, document-tree exclusion, and head/body insertion-mode restoration.
- EOF handling, explicit unsupported table/select/foreign contexts, and fragment-node budget accounting.
- Inert template scripts and valid start tags that do not consume parse-error budget.
- Comment and processing-instruction placement and owner-document identity.

**Review Status:** APPROVED after revisions; the reviewer’s suggested void-child comment/PI regression was added afterward.

**Validation:**

- Engine.Html.Tests: 7,294 passed.
- `dotnet build VisualWeb.slnx`: passed, no warnings or errors.
- `dotnet test VisualWeb.slnx --no-build`: passed, 17,287 successful and 2 skipped.
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party`: passed.
- `git diff --check`: passed.

**Git Commit Message:**

feat(html): parse bounded template contents

Construct document templates in head/body using the supported in-body subset while preserving separate inert content fragments and parser-mode restoration.

- Preserve nested templates, fragment owner documents, and parser budgets
- Keep template scripts inert and reject unsupported insertion contexts

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
