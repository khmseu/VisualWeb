# Bounded HTML Template Support Phase 1 Completion Report

## Phase 1 Complete: DOM template contents

Implemented native HTML template elements with stable fragment-backed contents, inert template-contents owner-document behavior, and adoption/deep-clone handling. Nested templates reuse the correct inert owner document; cloning accounts for template fragments and descendants in its bounded preflight. HTML parser construction remains explicitly unsupported.

**Files created/changed:**

- `src/Engine/Engine.Dom/AssemblyInfo.cs`
- `src/Engine/Engine.Dom/DomCloning.cs`
- `src/Engine/Engine.Dom/DomDocument.cs`
- `src/Engine/Engine.Dom/DomElement.cs`
- `src/Engine/Engine.Dom/DomNode.cs`
- `src/Engine/Engine.Dom/DomTemplateElement.cs`
- `src/Engine/Engine.Dom/README.md`
- `tests/Engine.Dom.Tests/CloningTests.cs`
- `tests/Engine.Dom.Tests/TemplateTests.cs`
- `docs/html-dom.md`
- `docs/standards.md`
- `plans/Missing.md`
- `Kontext/Wissensstand.md`
- `Kontext/Kontextprotokoll.md`
- `plans/whatwg-html-dom-completion-plan.md`

**Functions created/changed:**

- `DomDocument.CreateElement` creates `DomTemplateElement` for `template`.
- `DomDocument.TemplateContentsOwnerDocument` reuses the inert owner document, including when nested templates are created there.
- `DomNode.AdoptSubtree` traverses and rehomes template contents.
- `DomCloning` clones template content separately and includes fragments in preflight limits.
- `DomTemplateElement.Content` exposes the stable native content fragment.

**Tests created/changed:**

- Native template content identity and owner-document behavior.
- Nested template owner-document reuse and adoption.
- Shallow/deep clone behavior and bounded clone-preflight coverage.

**Review Status:** APPROVED after fixing nested template owner-document reuse.

**Validation:**

- Engine.Dom.Tests: 63 passed.
- Engine.Html.Tests: 7,279 passed.
- `dotnet build VisualWeb.slnx`: passed, no warnings or errors.
- `dotnet test VisualWeb.slnx --no-build`: passed, 17,272 successful and 2 skipped.
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party`: passed.
- `git diff --check`: passed.

**Git Commit Message:**

feat(dom): add native template content model

Represent template contents as stable fragments with inert owner-document semantics, including nested templates during adoption and deep cloning.

- Include template fragments and descendants in clone-budget preflight
- Add focused DOM tests and document the unsupported parser boundary

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
