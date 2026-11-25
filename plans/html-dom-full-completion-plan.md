# HTML/DOM Full Completion Plan

## Plan: HTML/DOM and Adjacent Browser Support

Extend the previously completed bounded roadmap to cover all categories in the
[deferred register](html-dom-deferred.md), including adjacent browser and
platform systems. The user approved this scope and the ten-phase ordering on
2026-11-25. The order is a rough estimate of implementation ease, not measured
effort; prerequisites take precedence, and later design decisions are not
preapproved by this roadmap.

The register is not exhaustive. Finishing its entries is not by itself proof
of HTML/DOM conformance or of a complete browser. Phase 10 audits a pinned
standards baseline and records further gaps and evidence. The earlier
[common-support roadmap](whatwg-html-dom-completion-roadmap.md) remains the
record of its completed, bounded scope.

### Execution rules

- Within each phase, deliver small, independently testable increments in the
  stated order. For each increment: write focused failing tests, run them,
  implement the smallest correct change, and rerun them and affected suites.
  Do not leave a red/green cycle spanning phases.
- Before standards-dependent implementation, use the local spec cache and
  refresh missing or stale sources according to [standards guidance](../docs/standards.md);
  add manifest entries when new standards are directly used. Tests stay offline.
- Preserve explicit unsupported failures, resource and cancellation limits,
  script opt-in, renderer ownership, confinement, and existing trust boundaries.
  Do not weaken security policy to unblock a phase.
- Update the relevant guides, support inventory and deferred register with
  exact implemented scope and test evidence. Preserve pinned fixture families.
- Build the solution, run touched subsystem suites and verify formatting for
  code changes; before proposing a commit also run the solution test suite.
  Run applicable native smoke checks and record target-platform evidence and
  gaps; do not infer Windows or arm64 results from Linux x64.
- These are workstreams, not single commits. Before large cross-cutting changes,
  specify and review their ownership, scheduling, security and failure model.

### Phases

1. **Phase 1: Remaining parser recovery** — HDF-011; lower estimated effort.
   - **Objective:** Remove remaining non-foreign-content tree-construction
     failures one algorithm at a time, retaining correct recovery and limits.
   - **Status:** In progress. Table-text foster insertion into a later open
     template and into the preceding open-stack element after a parser callback
     detaches the open table have been implemented. Fostered text under a
     document parent is discarded. In-body table-structure starts inside
     selects and select fragments are ignored with a parse error; unmatched
     structure ends use generic in-body recovery there. Early document modes
     now process table-structure starts and ends through their existing
     mode-specific rules, including head-noscript recovery with scripting
     disabled. After-body and after-after-body table tokens reenter in-body
     processing. In frameset, after-frameset and after-after-frameset modes,
     table and structure tokens, and `svg`/`math` starts, are diagnosed and
     ignored without constructing foreign content. Column-group fragments also
     ignore `svg`/`math` starts when the current node is not a `colgroup`.
     In-body select and input starts respect scope boundaries and the select
     fragment context, retaining the separate in-table hidden-input rule.
     Select ends use generic in-body recovery with special-element barriers and
     implied end tags.
     In-body option starts use scoped implied-end recovery and formatting
     reconstruction, including fragments and fostered selects.
     Other unsupported paths remain.
   - **Files:** [Engine.Html](../src/Engine/Engine.Html/),
     [Engine.Dom](../src/Engine/Engine.Dom/),
     [HTML tests](../tests/Engine.Html.Tests/) and
     [DOM tests](../tests/Engine.Dom.Tests/).
   - **Tests:** Table foster-parent starts and insertion locations, adoption
     involving the active form pointer, fragment contexts, exact limits and
     cancellation; applicable whole pinned fixture families.
   - **Steps:** Inventory the remaining explicit failures and relevant fixture
     cases; implement each supported HTML recovery path with the shared TDD
     cycle. Keep foreign-content and frame contexts for their later phases.

2. **Phase 2: Parent-context markup** — HDF-015; lower estimated effort.
   - **Objective:** Add `outerHTML` and `insertAdjacentHTML()` with correct
     parent/context selection, fragment parsing and checked multi-node mutation.
   - **Files:** Engine.Html, Engine.Dom,
     [Engine.Scripting](../src/Engine/Engine.Scripting/), their test projects,
     and the HTML/DOM and scripting guides.
   - **Tests:** Detached, document and fragment parents; all insertion
     positions; conversion/receiver errors, context-sensitive serialization,
     inert inserted scripts, limits, cancellation and atomic failure.
   - **Steps:** Test and add shared checked insertion/replacement primitives;
     expose each API separately with the shared TDD cycle. Preserve the
     existing `innerHTML` behavior and explicitly reject unsupported contexts.

3. **Phase 3: Live DOM inspection and mutation** — HDF-002; medium to high.
   - **Objective:** Implement traversal interfaces first, then ranges and
     mutation observers, including their live mutation semantics.
   - **Files:** Engine.Dom, Engine.Scripting, their test projects and guides.
   - **Tests:** Traversal filtering and mutation; live-range boundary repair;
     observer ordering, microtask delivery and disconnection; wrapper identity,
     ownership, cancellation and exact work/queue limits.
   - **Steps:** Define lifetimes and shared budgets; implement and test
     NodeIterator/TreeWalker, Range and MutationObserver in separate increments.
     Establish their integration with all relevant mutation paths before
     claiming each interface is complete.

4. **Phase 4: Reveal and form interaction** — HDF-014 and HDF-013; high.
   - **Objective:** Add hidden-until-found ancestor reveal and `beforematch`,
     then consistent script-visible form state and activation events/defaults.
   - **Files:** [Browser](../src/Apps/VisualWeb.Browser/),
     [PageRendering](../src/Apps/VisualWeb.PageRendering/), Engine.Scripting,
     [Engine.Layout](../src/Engine/Engine.Layout/),
     [IPC](../src/Ipc/), and relevant test projects.
   - **Tests:** Reveal ordering, cancellation, fragment/find triggers and
     retained repaint; edited values and control identity across mutations;
     click/submit/reset/input/change ordering, prevention, stale or removed
     controls, and local/process parity.
   - **Steps:** Define a bounded reveal trigger and event/render sequence; add
     stable control identities and value reconciliation; implement event and
     default actions incrementally. Never use stale control snapshots.

5. **Phase 5: Namespaces and foreign content** — HDF-005; high.
   - **Objective:** Support namespace-preserving DOM nodes and attributes,
     HTML foreign-content parsing, and separately scoped SVG/MathML styling,
     layout and rendering.
   - **Files:** Engine.Dom, Engine.Html,
     [Engine.Css](../src/Engine/Engine.Css/), Engine.Layout,
     [Engine.Paint](../src/Engine/Engine.Paint/) and their tests/guides.
   - **Tests:** Namespace identity, attribute/name adjustment, integration
     points and breakout, fragment contexts and complete pinned tree families;
     separate rendering and unsupported-feature tests.
   - **Steps:** Design and test namespace-aware DOM storage first; implement
     parser algorithms and bindings; then scope and implement rendering without
     treating successful parsing as rendering support.

6. **Phase 6: Web IDL and web components** — HDF-003 and HDF-001; high.
   - **Objective:** Establish reusable Web IDL binding machinery and complete
     the planned shadow-tree, slotting and custom-element lifecycle work.
   - **Files:** Engine.Scripting, Engine.Dom, Engine.Css, their test projects
     and scripting/HTML/DOM guides.
   - **Tests:** Conversion and exception ordering, prototype/brand/identity
     behavior, shadow ownership, slot assignment, event retargeting, style
     scoping and custom-element reaction ordering.
   - **Steps:** Inventory interfaces and specify binding generation or shared
     handwritten infrastructure; migrate/add interfaces incrementally. Then
     implement shadow roots, slots and reactions with integrated tree, event and
     style tests. No CLR DOM objects cross the script boundary.

7. **Phase 7: Resource policy and script scheduling** — HDF-008 and HDF-010; very high.
   - **Objective:** Extend resource loading through reviewed Fetch/CSP and
     cross-origin policy, then support external async/defer and module graphs.
   - **Files:** [Engine.Net](../src/Engine/Engine.Net/), Browser,
     PageRendering, Engine.Scripting, IPC and corresponding tests/guides.
   - **Tests:** Redirect and origin policy, CSP and credentials, concurrent
     fetching, async completion order, deferred/module document order,
     imports, cancellation, deadlines and failed-navigation publication.
   - **Steps:** Agree a broker/security design and implement policy in bounded
     increments; introduce concurrent source requests and an event-loop/task
     model sufficient for scheduling; add classic and module loading without
     weakening current confinement or resource checks.

8. **Phase 8: Frames, workers and storage** — HDF-004 and HDF-009; very high.
   - **Objective:** Add nested browsing contexts, then isolated workers,
     service-worker lifecycle and storage with explicit persistence policy.
   - **Files:** Browser, PageRendering, Engine.Net, Engine.Scripting, IPC,
     [Platform](../src/Platform/) and relevant test projects.
   - **Tests:** Frame origin/isolation and navigation lifecycle, process
     failures and cleanup; worker isolation/cancellation; service-worker
     lifecycle and storage origin partitioning, quotas and persistence.
   - **Steps:** Review frame ownership and process/isolation design; implement
     frame loading and navigation using Phase 7 policy. Define worker execution
     and storage guarantees before adding dedicated workers, service workers
     and persistence in independently tested increments. Plugin behavior
     requires its own safety and support decision.

9. **Phase 9: Graphics and media** — HDF-006 and HDF-007; very high.
   - **Objective:** Add bounded Canvas APIs, followed by media playback and
     resource lifecycle; separately decide WebGL/WebGPU scope and guarantees.
   - **Files:** Engine.Paint, Engine.Scripting, Engine.Net, PageRendering,
     Platform backends, relevant tests and platform guides.
   - **Tests:** Pixel/serialization semantics, graphics resource limits and
     disposal, media loading and state transitions, cancellation, backend
     failure and native target behavior.
   - **Steps:** Specify canvas ownership and raster budgets, then implement
     and test bounded interfaces; choose supported media backends/codecs and
     security/resource policies before playback increments. Assess advanced
     graphics separately rather than implying it from Canvas support.

10. **Phase 10: Accessibility and completion audit** — HDF-012; very high.
    - **Objective:** Deliver platform accessibility integration and establish
      what remains before claiming conformance against a pinned baseline.
    - **Files:** Platform backends, Browser, PageRendering, engine semantic
      mapping, relevant test projects, [standards inventory](../docs/standards.md)
      and the deferred register.
    - **Tests:** Accessible tree/name/state mappings, updates and actions,
      process ownership and native platform integrations; offline conformance
      suites and regression cases for gaps found by the audit.
    - **Steps:** Specify platform APIs and semantic mappings; implement and
      validate supported targets incrementally. Pin relevant HTML/DOM and
      adjacent standards revisions, inventory normative requirements against
      implementation and test evidence, add uncovered work to the register,
      and address or explicitly report all remaining gaps. Full conformance
      must not be claimed without evidence.

### Open questions and assumptions

- Exact budgets, compatibility targets, and completion baseline require
  phase-specific decisions; the roadmap does not approve security changes.
- Order is approximate: shared event-loop, broker, namespace, native backend,
  or accessibility prerequisites may require independently approved
  reordering. Independent increments can be tackled earlier when truly easier.
- Broad standards and platform coverage may reveal additional work not listed
  in the original 15 entries; record and prioritize it rather than silently
  calling the browser complete.
- Native Windows, Linux x64/arm64 and target accessibility/media validation
  require suitable environments and separately recorded evidence.
