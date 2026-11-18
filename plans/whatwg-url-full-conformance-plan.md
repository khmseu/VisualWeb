## Plan: Completing WHATWG URL Conformance

Extend the completed bounded implementation to cover the remaining WHATWG URL behavior, including the URL-specific Web IDL surface and URL-required environment hooks. Use offline, pinned conformance data and test-first fixes; a general Web IDL generator and unrelated HTML/File API implementation remain out of scope.

**Phases**

1. **Phase 1: Correct URL interface reflection**
   - **Objective:** Match URL and URLSearchParams operation descriptors and function lengths to the URL/Web IDL definitions without weakening private bridge protections.
   - **Files/Functions to Modify/Create:** `src/Engine/Engine.Scripting/UrlBindings.cs`; `tests/Engine.Scripting.Tests/UrlBindingTests.cs`.
   - **Tests to Write:** Prototype operation descriptor attributes; constructor and operation `length`; optional-argument behavior; preserved brand and private bridge defenses.
   - **Steps:**
     1. Add focused reflection tests that fail against the current descriptors and arities.
     2. Implement the required Web IDL attributes and argument lengths while retaining hardened private bridge state.
     3. Run the URL binding test class and all Engine.Scripting tests.
2. **Phase 2: Implement Web IDL iterator objects**
   - **Objective:** Replace generator-based URLSearchParams iterators with the required iterator-object behavior for `keys()`, `values()`, `entries()`, and default iteration.
   - **Files/Functions to Modify/Create:** `src/Engine/Engine.Scripting/UrlBindings.cs`; `tests/Engine.Scripting.Tests/UrlBindingTests.cs`.
   - **Tests to Write:** Iterator prototype/tag and `next()` result shape; key/value/pair ordering; default iterator behavior; supported mutation during iteration.
   - **Steps:**
     1. Add tests for observable iterator-object shape and iteration semantics.
     2. Implement URL-specific Web IDL iterator behavior without exposing CLR iterator objects.
     3. Run focused URL binding tests and all Engine.Scripting tests.
3. **Phase 3: Complete Core URL and URLSearchParams behavior**
   - **Objective:** Establish offline coverage for the applicable URL Standard algorithms and close verified gaps in component setters, origins, parsing/serialization, and form-urlencoded processing.
   - **Files/Functions to Modify/Create:** `src/Core/Core.Url/BrowserUrl.cs`, `MutableBrowserUrl.cs`, `SecurityOrigin.cs`, `UrlSearchParams.cs`; relevant vendored parser files; `tests/Core.Tests/` fixtures and URL tests.
   - **Tests to Write:** Pinned official WPT cases for uncovered URLSearchParams and URL algorithms; Core regressions for every confirmed mismatch, including mutable component updates and the applicable legacy query-encoding behavior.
   - **Steps:**
     1. Inventory existing offline fixture families and identify untested applicable requirements.
     2. Pin missing official vectors with provenance, expected counts, and source hashes; add failing focused regressions for confirmed deviations.
     3. Implement the smallest fixes supported by those tests, preserving the existing Unicode 17 IDNA baseline and vendored-parser license/provenance.
     4. Run all URL, IDNA, URLSearchParams, and Core tests.
4. **Phase 4: Add URL-required environment behavior**
   - **Objective:** Implement URL Standard behavior that depends on environment state, including blob URL entry origins and URL rendering-for-display rules, with narrow adapters rather than general HTML or File API implementations.
   - **Files/Functions to Modify/Create:** URL origin/environment abstractions in `src/Core/Core.Url/` and only directly required integration points in the existing browser/scripting host; focused Core and scripting tests.
   - **Tests to Write:** Injected blob URL entry-origin and fallback cases; opaque/file/tuple origin cases; URL display serialization and security-sensitive rendering cases; environment/base handling where required by the URL interface.
   - **Steps:**
     1. Add deterministic tests using injected environment data, with no network or global mutable state.
     2. Implement narrow URL-specific adapters and required display behavior.
     3. Verify that unrelated HTML/File APIs, page-script defaults, and network policy remain unchanged.
     4. Run the affected Core, Scripting, and integration tests.
5. **Phase 5: Verify and document URL conformance**
   - **Objective:** Re-run the complete applicable pinned offline evidence and update support documentation to state the demonstrated scope and any remaining exclusions accurately.
   - **Files/Functions to Modify/Create:** `docs/standards.md`, `docs/core.md`, `docs/scripting.md`, `src/Core/Core.Url/README.md`, `src/Engine/Engine.Scripting/README.md`, fixture provenance documentation, and the completion report.
   - **Tests to Write:** Add regressions only for deviations found during final conformance runs; preserve all existing fixture families.
   - **Steps:**
     1. Run all relevant URL, IDNA, URLSearchParams, and scripting conformance tests offline.
     2. Run the solution build, full solution tests, and format verification; address failures caused by this work.
     3. Update the support inventory only to claims demonstrated by the tests and implementation.

**Open Questions**

1. None blocking. Phase 4 is limited to URL Standard-required environment/display behavior; general Web IDL, general HTML integration, and File API implementation are excluded.
