## Phase 5 Complete: Implement browser JavaScript URL interfaces

Added branded `URL` and `URLSearchParams` facades to the bounded scripting host, backed by Core URL state and live `searchParams` linkage. The bridge remains primitive-only, and the implementation preserves existing script policy and execution limits.

**Files created/changed:**

- `src/Engine/Engine.Scripting/UrlBindings.cs`
- `src/Engine/Engine.Scripting/V8ScriptHost.cs`
- `src/Engine/Engine.Scripting/Engine.Scripting.csproj`
- `tests/Engine.Scripting.Tests/UrlBindingTests.cs`
- `docs/scripting.md`
- `src/Engine/Engine.Scripting/README.md`

**Functions created/changed:**

- `UrlBindings` private primitive-only bridge and JavaScript bootstrap
- `V8ScriptHost` URL binding initialization, execution budgeting, and disposal
- JavaScript `URL`, `URLSearchParams`, `URL.parse()`, and `URL.canParse()`

**Tests created/changed:**

- URL constructor, static parse methods, mutable attributes, and live linkage
- Web IDL string/USVString conversion, errors, and primitive initializer conversion
- Record conversion of enumerable symbols and normalized duplicate keys
- Single-read iterable conversion and non-callable iterator errors
- Prototype-tampering resilience, iteration, callbacks, and resource limits

**Review Status:** APPROVED

**Validation:**

- 10 URL binding tests passed
- 372 Engine.Scripting tests passed
- `dotnet build VisualWeb.slnx` passed with zero warnings and errors
- `dotnet test VisualWeb.slnx --no-build` passed; two process-pressure tests were skipped
- `dotnet format VisualWeb.slnx --verify-no-changes --no-restore --exclude third_party` passed
- `git diff --check` passed
- `dotnet run --no-build --project tools/V8Smoke -- --local` passed

**Git Commit Message:**

```text
feat(engine.scripting): add URL browser interfaces

Expose bounded URL and URLSearchParams facades over Core URL with live query linkage.
Preserve interface-specific Web IDL conversion and private bridge resource limits.
Add scripting tests for constructors, mutations, iteration, records, and tampering.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```
