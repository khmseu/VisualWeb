---
name: missing-priority-implementation
description: "Continue implementing the next highest-priority actionable item in plans/Missing.md. Use when asked to work through Missing.md, take the next browser-foundation priority, or repeat implementation passes with a tested commit for each."
argument-hint: "Optional number of sequential implementation passes"
---

# Missing Priority Implementation

Implement the next highest-priority actionable increment in `plans/Missing.md`,
validate it, update the tracker accurately, and commit that increment before
moving to another priority.

## Procedure

1. Read `docs/README.md`, `docs/architecture/overview.md`,
   `docs/project-map.md`, `plans/Missing.md`, the relevant subsystem guide and
   README, `plans/browser-foundation-plan.md`, and
   `docs/adr/0001-browser-foundation.md`. Check the working-tree status first;
   preserve unrelated or pre-existing changes.
2. Follow the priority order in `plans/Missing.md`. Select the next actionable
   work that is not already implemented. Treat broad headings as areas to
   break down, not as one-commit deliverables. Verify the current code and tests
   before choosing a concrete increment; do not infer completion from a title
   or stale tracker wording. If no actionable, unimplemented item exists, or
   every remaining item depends on an unresolved decision, report this with
   the evidence you checked and ask the user how to proceed instead of
   selecting speculative work.
3. Define a small, coherent change with observable behavior and focused tests.
   Respect the approved roadmap and subsystem constraints. If a material
   behavior or design choice is unresolved, ask the user before choosing.
4. For standards-dependent changes, read the relevant local files under
   `specs/cache/`. If a required document is missing, or its recorded fetch date
   in `specs/manifest.json` (or, if absent, the file's last-modified time) is more
   than 30 days old, run
   `dotnet run --project tools/SpecCache -- specs/manifest.json` before
   implementation. Report refresh failures and any stale source relied upon.
   Keep tests offline.
5. Implement the change, its focused tests, and directly related documentation.
   Update `plans/Missing.md` to describe exactly what changed and what remains;
   do not mark a broad priority complete when only an increment is done.
6. Run the smallest relevant test command, then any required build, format,
   or platform smoke validation warranted by the change. Fix regressions caused
   by the change. Do not suppress failures or claim unrun target validation.
7. Review the diff and status. Commit only the completed, related files with a
   Conventional Commit message. For non-trivial changes use a descriptive body,
   and include the repository's required
   `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
   trailer. Do not commit unrelated work. If a file you need to commit already
   contains unrelated pre-existing modifications, stage only your hunks
   (e.g. `git add -p`) or, if that is not feasible, stop and ask the user how to
   handle the overlapping changes before committing.
8. If the user requested multiple passes, repeat from step 1 against the
   updated tracker, making each pass a separately tested, separately committed
   increment. Do not repeat the same item or make speculative changes merely
   to reach a requested count. If safe, testable work is blocked, explain why
   and ask how to proceed rather than creating an empty or misleading commit.

## Repository guardrails

- Follow the owning subsystem's guide and existing patterns. Add xUnit v3 tests
  under the corresponding `tests/<project>.Tests/` project when code changes.
- Preserve explicit unsupported-feature failures and diagnostics; do not
  silently approximate behavior or imply standards conformance beyond evidence.
- Maintain trust boundaries, resource limits, cancellation, transactional state,
  offline test behavior, native ownership, and process/confinement guarantees
  documented for the affected subsystem.
- Link workspace files with their repository-relative paths in tracker and
  documentation updates.
