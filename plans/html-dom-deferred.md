# HTML/DOM Deferred Features

This is the revisit-ready backlog for HTML/DOM work postponed by the
[prioritized support roadmap](whatwg-html-dom-completion-roadmap.md). These are
not permanent exclusions. Reclassify an item when a common-site requirement
needs it, a dependency becomes available, or implementation cost falls.

The initial list was checked against the current subsystem guides, standards
inventory, `plans/Missing.md`, implementation markers, and focused tests. It is
not an exhaustive audit; Phase 1 may still refine each item's exact algorithm
scope. HTML/DOM estimates remain rough heuristics, not conformance scores.

| ID      | Deferred area                                                    | Spec reference                                                                                                    | Current boundary                                                                                                                                                                                                                                                                              | Why postponed                                                                                                                        | Revisit when                                                                                                                 |
| ------- | ---------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| HDF-001 | Shadow DOM, slotting, and custom-element reactions               | WHATWG DOM `dom`, Shadow trees; WHATWG HTML `html`, Custom elements.                                              | Native shadow trees, slot assignment, custom-element registries/reactions, and script bindings are not implemented.                                                                                                                                                                           | Requires coordinated tree, lifecycle, event, styling, and script-host semantics beyond ordinary light-DOM pages.                     | A supported site needs Web Components, or the tree/lifecycle foundations make a bounded implementation practical.            |
| HDF-002 | Advanced DOM mutation and inspection APIs                        | WHATWG DOM `dom`, Mutation observers, Ranges, Traversal.                                                          | Mutation observers, ranges, iterators/walkers, and related live-update behavior are incomplete or absent.                                                                                                                                                                                     | More complex lifetime, identity, mutation ordering, and budget semantics; only add when representative common use requires them.     | A prioritized editor/framework case needs the API, or implementation can reuse existing checked tree algorithms at low cost. |
| HDF-003 | Full Web IDL and broad interface surface                         | Web IDL `webidl`; WHATWG HTML `html`; WHATWG DOM `dom`.                                                           | Only selected script facades and conversion rules exist; generated bindings and the complete HTML/DOM interface set are absent.                                                                                                                                                               | Broad binding generation is a substantial cross-cutting project and would expose features outside the prioritized target.            | The common API inventory stabilizes and shared binding infrastructure can reduce the cost of multiple needed interfaces.     |
| HDF-004 | Frame browsing contexts and plugins                              | WHATWG HTML `html`, iframe/embedded-content and navigation algorithms.                                            | Static frame/frameset nodes may parse, but frame loading, nested browsing contexts, plugin behavior, and associated lifecycle are not implemented.                                                                                                                                            | Requires navigation, resource, process, and isolation policy beyond building the parent document tree.                               | A concrete supported-site requirement and reviewed frame/process security model are available.                               |
| HDF-005 | Full SVG/MathML behavior and rendering                           | WHATWG HTML `html`, foreign-content parsing; SVG/MathML source IDs must be added to the manifest if promoted.     | Basic SVG foreign-content parsing is ranked for Phase 2; complete SVG/MathML DOM, styling, layout, and rendering remain outside the common parser increment.                                                                                                                                  | Depends on additional standards algorithms and layout/paint behavior, not just HTML tree construction.                               | A representative workload needs the behavior and the required engine/rendering work is separately scoped.                    |
| HDF-006 | Canvas and advanced graphics                                     | WHATWG HTML `html`, Canvas; related graphics APIs require separate inventory if promoted.                         | Canvas APIs, rasterization semantics, WebGL/WebGPU, and their script bindings are not part of the current HTML/DOM subset.                                                                                                                                                                    | Requires a new graphics/API surface and platform/resource considerations.                                                            | A common supported application requires it and a bounded graphics milestone is approved.                                     |
| HDF-007 | Audio, video, and broad media behavior                           | WHATWG HTML `html`, Media elements.                                                                               | Media playback, codecs, controls, streaming, and related resource lifecycle are not implemented.                                                                                                                                                                                              | Requires media backends, resource scheduling, platform integration, and additional security/resource limits.                         | A media requirement and supported backend policy are established.                                                            |
| HDF-008 | Full resource loading, Fetch/CSP, and cross-origin policy        | WHATWG Fetch `fetch`; CSP `csp3`; HTML resource-fetching algorithms `html`.                                       | Existing loading is a bounded privileged subset; full Fetch, CSP, caching, and broad subresource scheduling are not implemented.                                                                                                                                                              | Security-sensitive cross-cutting behavior must not be approximated or bypass current broker/confinement rules.                       | A separate reviewed networking/security plan supplies the required policy and tests.                                         |
| HDF-009 | Workers, service workers, and storage                            | WHATWG HTML `html`, Workers; service-worker/storage spec IDs must be added to the manifest if promoted.           | Worker execution, service-worker lifecycle, and browser storage APIs are not implemented.                                                                                                                                                                                                     | Requires separate execution contexts, persistence/lifecycle, process/resource policies, and additional standards.                    | A specific common-site need is approved with isolation and storage requirements.                                             |
| HDF-010 | Full module graph and uncommon script scheduling                 | WHATWG HTML `html`, Script processing and modules; Fetch `fetch` for resource loading.                            | Import-free inline modules are bounded; external module graphs, imports/import maps, dynamic import, and broader scheduling are incomplete.                                                                                                                                                   | Requires a reviewed resource loader, URL/CORS policy, scheduling, and failure semantics.                                             | The common-script phase demonstrates the required broker policy and the module features rank above competing work.           |
| HDF-011 | Remaining specialized HTML tree construction                     | WHATWG HTML `html`, Parsing and the relevant element algorithm.                                                   | The tree builder rejects SVG/MathML until Phase 2 foreign-content work; some table starts still reach an unsupported foster-parent path, and adoption recovery can fail when removing the active form pointer. Fragment contexts `svg`, `math`, `frame`, and `frameset` are also unsupported. | Lower expected use than ordinary document, form, and interaction behavior; individual low-cost parser fixes may be promoted earlier. | A regression or representative page shows a compatibility failure, or the fix is a safe low-cost addition.                   |
| HDF-012 | Platform accessibility tree and assistive-technology integration | Related platform accessibility work, outside the WHATWG HTML/DOM target; add the applicable spec IDs if promoted. | The engine does not provide a complete platform accessibility tree or mappings.                                                                                                                                                                                                               | Cross-platform accessibility semantics and native integration are a separate project, not implied by DOM nodes or ARIA attributes.   | A separately scoped accessibility effort defines platform APIs, semantics, and target validation.                            |

## Owners and prerequisites

| ID      | Likely owner                                                     | Main prerequisite                                                                                             |
| ------- | ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| HDF-001 | Engine.Dom, Engine.Scripting, Engine.Css                         | Shadow-tree identity/lifecycle, slot assignment, event/style integration, and bounded bindings.               |
| HDF-002 | Engine.Dom, Engine.Scripting                                     | Defined live-range/observer lifetimes, mutation ordering, cancellation, and shared work limits.               |
| HDF-003 | Engine.Scripting                                                 | A stable common-interface inventory and reusable Web IDL conversion/prototype infrastructure.                 |
| HDF-004 | VisualWeb.Browser, PageRendering, Engine.Net, IPC                | Reviewed frame navigation/resource policy, renderer ownership, and process/isolation design.                  |
| HDF-005 | Engine.Html, Engine.Dom, Engine.Css, Engine.Layout, Engine.Paint | Foreign namespace tree support plus separately pinned SVG/MathML sources and layout/paint scope.              |
| HDF-006 | Engine.Paint, Platform backends, Engine.Scripting                | Bounded graphics API, native ownership, raster/resource budgets, and platform validation.                     |
| HDF-007 | Engine.Net, Platform backends, PageRendering                     | Supported codecs/backends, resource policy, media lifecycle, and native resource limits.                      |
| HDF-008 | Engine.Net, VisualWeb.Browser, PageRendering                     | A separate reviewed Fetch/CSP/CORS policy and broker integration; no confinement or request-policy weakening. |
| HDF-009 | Engine.Scripting, VisualWeb.Browser, Platform backends           | Execution-context isolation, persistence policy, process/resource limits, and added standard sources.         |
| HDF-010 | PageRendering, Engine.Scripting, Engine.Net                      | Reviewed module resource loader, URL/CORS/failure semantics, and bounded scheduling.                          |
| HDF-011 | Engine.Html, Engine.Dom                                          | Phase 2 fixture-level selection of remaining table/recovery algorithms after common parser behavior.          |
| HDF-012 | Platform backends and a separate accessibility workstream        | Cross-platform semantics, native API choice, target validation, and applicable standards inventory.           |

## Phase 1 audit evidence

- **HDF-001:** The [HTML/DOM guide](../docs/html-dom.md#dom-boundary-and-invariants)
  defers shadow DOM and most special-element behavior.
  [DomBindings.cs](../src/Engine/Engine.Scripting/DomBindings.cs) explicitly
  defers custom-element options; the existing DOM and scripting test projects
  contain no shadow-tree or custom-element suite.
- **HDF-002:** [DomNode.cs](../src/Engine/Engine.Dom/DomNode.cs),
  [DomCharacterData.cs](../src/Engine/Engine.Dom/DomCharacterData.cs), and
  normalization document observer/live-range reactions as deferred. Existing
  CharacterData, normalization, and tree-inspection tests cover adjacent
  primitives, not MutationObserver or Range APIs.
- **HDF-003:** The [scripting guide](../docs/scripting.md) describes bounded
  bindings and defers general Web IDL. Existing scripting tests cover selected
  interfaces and conversion behavior, not generated interface-wide bindings.
- **HDF-004:** The HTML guide and `Engine.Html` README state that frames do not
  create browsing contexts or load resources. [TreeTests.cs](../tests/Engine.Html.Tests/TreeTests.cs)
  cover static frameset parsing, not nested navigation or frame lifecycle.
- **HDF-005:** [HtmlParser.cs](../src/Engine/Engine.Html/HtmlParser.cs)
  explicitly rejects `svg` and `math`; tree and fragment tests verify explicit
  unsupported failures. Basic HTML foreign-content parsing is ranked for
  Phase 2; complete SVG/MathML trees and rendering are not covered by the
  pinned HTML tree fixture families.
- **HDF-006/HDF-007:** Canvas and media are listed among later browser
  capabilities in the [approved foundation roadmap](browser-foundation-plan.md)
  and have no corresponding implemented engine project/test suite.
- **HDF-008:** The [networking guide](../docs/networking.md) and
  `plans/Missing.md` describe the loader as a bounded privileged subset, not
  full Fetch/CSP. Existing network/browser tests exercise selected policies
  only.
- **HDF-009:** The [project map](../docs/project-map.md) and approved foundation
  roadmap defer workers and storage; no worker/storage engine or test suite is
  present.
- **HDF-010:** [ModuleExecutionTests.cs](../tests/Engine.Scripting.Tests/ModuleExecutionTests.cs)
  verifies imports fail explicitly without a resource loader; current module
  evaluation is bounded to import-free inline sources.
- **HDF-011:** `HtmlParser` retains explicit unsupported paths for remaining
  tree-construction algorithms: the table-mode default foster-parent path
  throws for unhandled content, adoption can fail when it would remove the
  active form pointer, and some foster insertion locations are unsupported.
  Fragment contexts `svg`, `math`, `frame`, and `frameset` are explicitly
  rejected. [TreeTests.cs](../tests/Engine.Html.Tests/TreeTests.cs) includes
  `UnsupportedTableRecoveryFailsExplicitly` and
  `UnsupportedTreeAlgorithmsFailExplicitly`; Phase 2 must still map each
  common parser failure to the pinned fixture family or a focused test.
- **HDF-012:** Platform accessibility integration is outside the HTML/DOM
  implementation boundary in the [architecture guide](../docs/architecture/overview.md);
  it remains related work, not a claim about a missing DOM node algorithm.

## Register maintenance

For each entry, keep the current support evidence and the exact missing behavior
specific. When work starts, record its roadmap phase and dependencies. When it
is implemented, remove or mark the item complete and link the tests/docs that
establish the result. If it is intentionally dropped, preserve that decision
and rationale rather than deleting the history.
