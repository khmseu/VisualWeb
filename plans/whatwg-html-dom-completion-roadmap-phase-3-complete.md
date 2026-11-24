# Common HTML/DOM Support Phase 3 Completion Report

## Phase 3 Complete: Common DOM and Markup Operations

Implemented bounded, script-facing `Element.innerHTML` for the supported
HTML-only subset. The getter serializes with traversal/output limits; the
setter uses context-sensitive fragment parsing and validates replacement
before changing existing children. Template content, cancellation, ownership,
conversion ordering, explicit unsupported cases, and inert inserted scripts
are covered.

**Files created/changed:**

- `src/Engine/Engine.Dom/DomNode.cs`
- `src/Engine/Engine.Html/HtmlFragmentSerializer.cs`
- `src/Engine/Engine.Html/README.md`
- `src/Engine/Engine.Scripting/DomBindings.cs`
- `src/Engine/Engine.Scripting/Engine.Scripting.csproj`
- `tests/Engine.Dom.Tests/ReplaceAllTests.cs`
- `tests/Engine.Html.Tests/HtmlFragmentSerializerTests.cs`
- `tests/Engine.Scripting.Tests/InnerHtmlTests.cs`
- `docs/html-dom.md`
- `docs/scripting.md`
- `docs/standards.md`
- `plans/Missing.md`
- `plans/whatwg-html-dom-completion-roadmap.md`
- `Kontext/Wissensstand.md`
- `Kontext/Kontextprotokoll.md`

**Functions created/changed:**

- `HtmlFragmentSerializer.SerializeChildren`
- Internal `DomNode.ReplaceAll`
- Script-facing `Element.innerHTML` getter/setter binding

**Tests created/changed:**

- Atomic DOM child replacement and invalid replacement cases
- HTML fragment serialization, escaping, templates, void/raw-text elements,
  output and traversal limits, cancellation, and unsupported nodes
- Processing-instruction exact output, including the `?>` terminator and
  required separator for empty data
- `innerHTML` context behavior, conversion order, template ownership, failure
  atomicity, and inert inserted scripts

**Review Status:** APPROVED, including a final read-only review of the
processing-instruction terminator correction.

**Validation:**

- `dotnet build VisualWeb.slnx` — passed, 0 warnings and 0 errors
- `dotnet test tests/Engine.Dom.Tests/Engine.Dom.Tests.csproj` — 78 passed
- `dotnet test tests/Engine.Html.Tests/Engine.Html.Tests.csproj` — 7,717 passed
- `dotnet test tests/Engine.Scripting.Tests/Engine.Scripting.Tests.csproj` — 404 passed
- `dotnet test VisualWeb.slnx --no-build` — passed on final rerun
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party` — passed
- `git diff --check` — passed

The first full-suite run, which overlapped formatting verification, had two
timeouts in unrelated URL-binding and browser link-click tests. Each passed
when rerun alone, and the subsequent full solution test run passed.

**Git Commit Message:**

feat(scripting): add bounded Element.innerHTML

Add context-sensitive fragment parsing and bounded HTML serialization to the
script-facing Element.innerHTML binding. Preflight replacement before mutating
existing children, preserve template content handling, and keep inserted scripts
inert.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
