## Plan: Bounded HTML Template Support

Extend the finite HTML and DOM subsets together, beginning with the
HTMLTemplateElement content model and a deliberately bounded parser subset.
This plan does not claim complete WHATWG HTML or DOM conformance and does not
add script-facing `innerHTML` or template bindings.

**Phases**

1. **Phase 1: DOM template contents**
   - **Objective:** Represent native HTML template elements with stable,
     fragment-backed contents, the inert template-contents owner document,
     and specified adoption/deep-clone behavior.
   - **Files/Functions to Modify/Create:** `src/Engine/Engine.Dom/DomDocument.cs`,
     `DomElement.cs`, `DomNode.cs`, `DomCloning.cs`; add a template-element
     type as needed; `tests/Engine.Dom.Tests/CloningTests.cs` and focused DOM
     tests; `src/Engine/Engine.Dom/README.md`, `docs/html-dom.md`,
     `docs/standards.md`, and `plans/Missing.md`.
   - **Tests to Write:** `CreateElement_template_has_separate_stable_content`,
     `Adopt_template_rehomes_content_and_nested_templates`,
     `ShallowClone_template_has_empty_distinct_content`,
     `DeepClone_template_copies_content_without_element_children`,
     and bounded clone-preflight coverage for template contents.
   - **Steps:**
     1. Add failing tests for the template content fragment, owner-document
        identity, adoption and clone behavior.
     2. Implement the native template representation and the smallest
        adoption/clone changes that satisfy the HTML/DOM algorithms.
     3. Run focused DOM tests and the phase validation suite.
     4. Document that the HTML parser still rejects template markup until
        Phase 2.

2. **Phase 2: Bounded template tree construction**
   - **Objective:** Parse a documented subset of ordinary and nested
     `<template>` content into template fragments; explicitly reject contexts
     whose template insertion-mode algorithms are not implemented.
   - **Files/Functions to Modify/Create:** `src/Engine/Engine.Html/HtmlParser.cs`,
     HTML parser tests under `tests/Engine.Html.Tests/`, and the HTML/DOM guide
     and project README.
   - **Tests to Write:** `Template_parses_basic_in_body_content`,
     `Nested_templates_keep_separate_content_fragments`,
     `Template_content_is_not_a_document_child_or_descendant`, and
     `Unsupported_template_contexts_fail_explicitly`.
   - **Steps:**
     1. Add failing positive and explicit-rejection tests, preserving all
        existing html5lib fixture families and parser limits.
     2. Implement only the selected template insertion-mode cases; unsupported
        modes must continue to throw rather than return an approximate tree.
     3. Run the focused HTML suite and phase validation suite.
     4. Update HTML/DOM documentation and the tracker with the exact remaining
        parser limitations.

3. **Phase 3: Cross-subsystem inertness and limits**
   - **Objective:** Verify template contents remain outside ordinary document
     traversal/rendering and parser-script scheduling, and that bounded
     operations include template subtrees where required.
   - **Files/Functions to Modify/Create:** Relevant `VisualWeb.PageRendering`
     implementation/tests only if the new parser behavior exposes a regression;
     `Engine.Dom` and `Engine.Html` focused tests; `docs/html-dom.md`,
     subsystem READMEs, `docs/standards.md`, and `plans/Missing.md`.
   - **Tests to Write:** A focused retained-page regression proving template
     contents are inert and do not schedule scripts, plus exact template
     subtree limit/cancellation tests for any affected bounded operation.
   - **Steps:**
     1. Add failing end-to-end tests for any newly reachable traversal,
        rendering, script-scheduling, budget or cancellation edge.
     2. Make only the required corrections without adding template script
        execution, a general HTML event loop or script-facing template APIs.
     3. Run all affected subsystem suites, the solution build and tests, and
        format verification.
     4. Update the standards inventory without changing rough percentages
        unless new measurable coverage evidence justifies a recalculation.

**Open Questions**

1. This bounded template increment does not define the complete HTML/DOM
   completion endpoint; subsequent increments must be selected and scoped
   separately against the updated tracker.
