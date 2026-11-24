# Common HTML/DOM Support Roadmap

## Plan: Common HTML/DOM Support

Prioritize WHATWG HTML and DOM behavior that ordinary websites commonly rely
on, then include nearby features when they reuse existing implementation paths
at low additional cost. Defer lower-frequency or architecture-heavy features
in the linked [HTML/DOM deferred register](html-dom-deferred.md), with enough
context to resume them later. This is a practical support roadmap, not a claim
of complete WHATWG HTML or DOM conformance.

The existing [bounded template plan](whatwg-html-dom-completion-plan.md) is
complete and remains unchanged. This roadmap builds on it.

### Prioritization and completion rules

- **Common:** Needed to parse ordinary pages, operate on their DOM, use common
  forms and links, or run the page behavior those sites rely on.
- **Low-cost:** Can be added beside a selected feature by reusing existing
  parser, DOM, or scripting machinery, without introducing a new platform
  dependency, trust boundary, or broad architectural subsystem.
- **Deferred:** Lower-frequency, high-cost, or dependent on another substantial
  subsystem. Record its spec location, current support, reason, prerequisites,
  and a concrete revisit condition in `html-dom-deferred.md`.
- Base ranking on the current guides, `plans/Missing.md`, existing tests, and
  representative offline page cases. This is a reasoned priority estimate, not
  production telemetry or a standards-conformance percentage.
- For each behavior change, add focused failing tests first, implement the
  smallest correct increment, then run the affected subsystem tests. Keep
  unsupported behavior explicit; do not silently approximate it.
- Preserve existing resource, script opt-in, cancellation, renderer ownership,
  and confinement rules. Any change to these policies needs its own explicit
  decision and tests.

### Initial priority inventory

The ordering below is the Phase 1 starting point, based on repository evidence.
It is a user-approved workstream priority, not an empirical ranking of browser
market share; Phase 1 may adjust it when the inventory exposes better evidence.

| Priority | Feature area                                           | Current evidence and gap                                                                                                                                                                                                                                                                                                                                                                                                                                                             | Planned phase     |
| -------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------- |
| Common 1 | Ordinary document parsing and recovery                 | `Engine.Html` supports a large static subset and pinned tokenizer/tree fixtures, but the tree corpus covers only selected families; Phase 2 adds in-body recovery for table-structure starts and unmatched ends, while broader table recovery remains limited and SVG/MathML require namespace-aware DOM/parser support deferred under HDF-005. See `TreeTests.OfficialStaticTreeFixture`, `UnsupportedTreeAlgorithmsFailExplicitly`, and `UnsupportedTableRecoveryFailsExplicitly`. | Phase 2           |
| Common 2 | Fragment parsing and context-correct markup operations | `ParseFragment` supports bounded body, table, select, template, and text contexts with explicit unsupported cases. Bounded script-facing `innerHTML` parsing and serialization now exist, but cover only supported contexts and algorithms; conformance remains incomplete. See `FragmentTests`, `InnerHtmlTests`, `HtmlFragmentSerializerTests`, and `docs/html-dom.md`.                                                                                                            | Phases 2-3        |
| Common 3 | DOM queries and tree/attribute mutation used by pages  | The script facade already includes selectors, mutation, class tokens, metadata, CharacterData, cloning, and static NodeList snapshots. The gap is broader common markup/collection behavior, not a missing DOM foundation. See `QueryTests`, `DomMutationTests`, `ClassTokenTests`, and `TreeInspectionTests`.                                                                                                                                                                       | Phase 3           |
| Common 4 | Forms and browser-visible interactions                 | Browser-shell forms, focus, selection, and link activation have substantial bounded coverage. Phase 4 adds opted-in trusted plain clicks for user-activated `type=button` controls. Submit/reset event defaults and live script-visible control state remain deferred under HDF-013. See `FormControlTests`, `FormTests`, `ShellTests`, `EventTests`, `ClickDispatchTests`, and `ButtonClickEventTests`.                                                                             | Phase 4 / HDF-013 |
| Common 5 | Script scheduling and document lifecycle               | Explicit opt-in parser-ordered classics, readiness, microtasks, events, and import-free inline modules exist. Broader async/defer scheduling and module/resource loading remain missing. See `ClassicScriptTests`, `DocumentLifecycleTests`, `MicrotaskTests`, and `ModuleExecutionTests`.                                                                                                                                                                                           | Phase 5           |
| Low-cost | Adjacent parser recovery and simple interface behavior | Promote small corrections that reuse existing insertion-mode, native primitive, or binding paths; assess individually rather than broadening the API surface speculatively. Existing HTML/DOM focused tests provide regression patterns.                                                                                                                                                                                                                                             | Phases 2-6        |
| Deferred | New subsystems or advanced APIs                        | Shadow trees, full Web IDL, frame browsing contexts, media/graphics, broad resource policy, workers/storage, and platform accessibility require substantial new behavior or dependencies. The deferred register records current evidence and revisit conditions.                                                                                                                                                                                                                     | Deferred register |

### Phases

1. **Phase 1: Inventory, ranking, and deferred register**
   - **Objective:** Reconcile the current HTML/DOM support guides and tracker
     against implementation and tests. Rank common work, low-cost additions,
     and deferred gaps with traceable reasons.
   - **Files/Functions to Modify/Create:** `plans/Missing.md`,
     `plans/html-dom-deferred.md`, this roadmap, `docs/html-dom.md`,
     `docs/standards.md`, `src/Engine/Engine.Html/README.md`,
     `src/Engine/Engine.Dom/README.md`, and existing HTML/DOM test projects.
   - **Tests to Write:** No new behavior tests for this documentation-only
     inventory. Map existing tests and fixture families to each ranked item;
     record missing regression/conformance coverage for later phases.
   - **Steps:**
     1. Inventory the remaining requirements from the local `html` and `dom`
        sources, subsystem guides, project READMEs, and `plans/Missing.md`.
     2. Inspect existing implementations and tests so already-supported
        features are not replanned.
     3. Assign each gap a priority, effort rationale, owning subsystem,
        dependencies, and test evidence or test gap.
     4. Populate the deferred register and link it from the tracker.
     5. Keep standards coverage estimates unchanged unless a later measurable
        review justifies recalculation.

2. **Phase 2: Common document and fragment parsing**
   - **Objective:** Close parser gaps that cause ordinary documents or common
     malformed markup to reject or produce materially incorrect trees. Keep
     HTML parsing and the corresponding DOM tree/fragment behavior aligned.
   - **Files/Functions to Modify/Create:** `src/Engine/Engine.Html/HtmlParser.cs`,
     tokenizer code only where a selected parser case requires it,
     `src/Engine/Engine.Dom/`, `tests/Engine.Html.Tests/`,
     `tests/Engine.Dom.Tests/`, and their subsystem guides.
   - **Tests to Write:** Focused positive and recovery cases for selected
     ordinary document, list, table, form, template, and fragment gaps;
     applicable pinned html5lib fixture families; exact limits and cancellation
     cases for new traversals or allocations.
   - **Steps:**
     1. Select the highest-ranked parser gaps from the Phase 1 inventory and add
        focused failing tests without dropping or weakening existing fixture
        families.
     2. Implement the corresponding tree-construction and DOM behavior with
        the smallest correct changes.
     3. Reassess foreign-content parsing against the current DOM namespace
        model; if correct support requires a broader namespace subsystem,
        record it in HDF-005 rather than approximating the tree.
     4. Run Engine.Html and Engine.Dom tests, then update the guides and
        deferred statuses.

3. **Phase 3: Common DOM and markup operations**
   - **Objective:** Complete the DOM operations commonly used by application
     code and frameworks, including context-sensitive markup operations where
     their cost and security constraints are understood.
   - **Files/Functions to Modify/Create:** `src/Engine/Engine.Dom/`,
     `src/Engine/Engine.Html/` fragment/serialization paths,
     `src/Engine/Engine.Scripting/` bindings, and tests under
     `tests/Engine.Dom.Tests/`, `tests/Engine.Html.Tests/`, and
     `tests/Engine.Scripting.Tests/`.
   - **Tests to Write:** Receiver/ownership and Web IDL conversion-order cases;
     common query, attribute, mutation, and fragment cases; `innerHTML` and
     serialization context/error cases if selected; atomicity, identity,
     cancellation, and exact budget boundaries.
   - **Steps:**
     1. Add failing tests for the highest-ranked common operations missing from
        the native DOM and script facade.
     2. Implement native checked algorithms before exposing corresponding
        script APIs.
     3. Add commonly used markup parsing/serialization APIs, including
        `innerHTML`, only with context-correct parsing, safe failure behavior,
        and bounded allocation.
     4. Run affected DOM, HTML, and scripting suites; update guide and
        standards support descriptions.

4. **Phase 4: Common HTML elements, forms, and interactions**
   - **Objective:** Support common HTML element behavior needed by page
     navigation, form entry, and ordinary scripted interactions.
   - **Bounded increment (complete):** Opted-in enabled `type=button` pointer and
     Enter/Space activations dispatch a trusted plain click and repaint retained
     DOM; no form default is added. `ButtonClickEventTests`, `ShellTests`, and
     IPC protocol tests cover local/process routing and validation. Phase 4
     closes at this bounded priority; broader script-observable form activation
     and control-state behavior remains deferred under HDF-013.
   - **Files/Functions to Modify/Create:** Relevant `Engine.Dom`,
     `Engine.Html`, `Engine.Scripting`, and browser-shell integration under
     `src/Apps/VisualWeb.Browser/` and `src/Apps/VisualWeb.PageRendering/`;
     focused tests in the corresponding test projects.
   - **Tests to Write:** Form-owner and control-state cases; common input,
     select, textarea, button, label, link, and submission cases; event and
     browser integration regressions for selected user actions.
   - **Steps:**
     1. Add failing native DOM/parser tests for common element algorithms and
        attribute/state interactions.
     2. Implement native HTML/DOM behavior and expose only the required
        bounded script-facing operations.
     3. Integrate selected actions with the browser without sharing mutable DOM
        state across renderer or IPC boundaries.
     4. Run Engine.Dom, Engine.Html, Engine.Scripting, and Browser tests
        affected by the selected work; update support guides.

5. **Phase 5: Common scripting and document lifecycle**
   - **Objective:** Extend the existing opt-in script path only for scheduling
     and lifecycle behavior ranked as necessary for common sites.
   - **Files/Functions to Modify/Create:** `src/Apps/VisualWeb.PageRendering/`,
     `src/Engine/Engine.Scripting/`, brokered loading only where required,
     related IPC contracts only if data-only policy changes are unavoidable,
     and the relevant scripting/browser test projects.
   - **Tests to Write:** Ordered classic-script scheduling, readiness and
     event sequencing, cancellation/deadline, transactional navigation, and
     script-failure regressions. Add module/resource cases only when the
     required loading policy is included in the Phase 1 ranking.
   - **Steps:**
     1. Add failing lifecycle and scheduling tests for the selected common
        cases, preserving the current execution opt-in.
     2. Implement finite, cancellable stages under the existing shared
        budgets and transactional publication model.
     3. Add resource-dependent script behavior only behind an explicit,
        reviewed broker policy; move features requiring broader Fetch/CSP
        architecture to the deferred register otherwise.
     4. Run scripting and browser tests, including failure and stale-load
        cases; update the scripting and HTML/DOM guides.

6. **Phase 6: Low-cost adjacent features**
   - **Objective:** Add remaining nearby features from the inventory when they
     reuse implemented mechanisms and do not widen trust or platform
     boundaries.
   - **Files/Functions to Modify/Create:** Only the subsystem files and tests
     associated with each selected register item.
   - **Tests to Write:** A focused regression for each selected behavior;
     exact limits, diagnostics, and conversion order where applicable.
   - **Steps:**
     1. Reassess effort and dependencies for low-cost candidates after Phases
        2-5.
     2. For each accepted candidate, write a failing focused test first and
        implement only that feature.
     3. Move completed items out of the deferred register and update the
        owning subsystem documentation.
     4. Leave candidates that prove more expensive in the register with an
        updated reason and prerequisite.

7. **Phase 7: Integrated compatibility and support accounting**
   - **Objective:** Verify the prioritized target end to end and leave a
     reliable record of both supported and postponed behavior.
   - **Files/Functions to Modify/Create:** Test projects for touched
     subsystems; `docs/html-dom.md`, `docs/standards.md`, subsystem READMEs,
     `plans/Missing.md`, and `plans/html-dom-deferred.md`.
   - **Tests to Write:** Representative offline pages combining parsing,
     DOM operations, forms, and selected scripts; regression cases for every
     corrected failure; applicable pinned html5lib/WPT tests. Conformance
     tests remain offline.
   - **Steps:**
     1. Run targeted subsystem suites and add regressions for any newly found
        supported-scope failures.
     2. Run the solution build, full solution tests, and format verification
        required by repository guidance.
     3. Reconcile test results with the prioritized inventory; do not infer
        complete standard conformance from test counts or rough percentages.
     4. Update supported/deferred status, rationale, prerequisites, and
        revisit triggers. Keep the postponed register linked from `Missing.md`.

### Initial success criteria

- The prioritized common set is explicitly listed and has focused regression
  coverage in its owning test projects.
- Low-cost additions have a recorded reuse/effort rationale.
- Every postponed gap has a stable ID, spec location, current status, reason,
  dependencies, and a revisit condition in `html-dom-deferred.md`.
- Unsupported behavior remains visible and fails according to subsystem
  policy; no feature is represented as implemented without evidence.
- The result is described as prioritized practical support, not full WHATWG
  conformance.

### Assumptions

1. “Common” means typical website/browser requirements, based on repository
   evidence and representative cases rather than production telemetry.
2. Existing security, script opt-in, process, cancellation, and resource
   policies remain in force unless separately reviewed and approved.
3. Full rendering for foreign content and large adjacent standards are not
   implicit requirements of common HTML parsing.
