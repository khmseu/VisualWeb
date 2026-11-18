## Plan: Completing WHATWG URL Support

Extend VisualWeb's current bounded URL support toward the WHATWG URL Standard with an evidence-led Core implementation and browser-facing `URL`/`URLSearchParams` behavior, including live linkage. The scope includes the Web IDL behavior needed by these interfaces, not a general Web IDL implementation. Preserve the existing immutable `BrowserUrl` contract and keep standards tests offline.

**Phases**

1. **Phase 1: URLSearchParams conformance baseline**
   - **Objective:** Add pinned offline conformance coverage for form-urlencoded parsing/serialization and the URLSearchParams operations that are not already established by existing tests; fix deviations found by the coverage.
   - **Files/Functions to Modify/Create:** `tests/Core.Tests/Data/`, `tests/Core.Tests/Data/README.md`, `tests/Core.Tests/UrlSearchParamsTests.cs`, and only directly implicated `src/Core/Core.Url/UrlSearchParams.cs` code.
   - **Tests to Write:** URLSearchParams parser and serializer fixture tests; constructor, UTF-8 replacement, tuple ordering, and stringification edge cases.
   - **Steps:** Inventory existing cases and identify specific uncovered behavior; add focused tests that fail before fixes; implement only deviations demonstrated by the tests; run the focused tests and `tests/Core.Tests/Core.Tests.csproj`.
2. **Phase 2: URL parser and serialization gaps**
   - **Objective:** Audit and correct remaining URL parsing, host, relative-resolution, and serialization deviations using pinned URL and domain vectors.
   - **Files/Functions to Modify/Create:** `third_party/Dubzer.WhatwgUrl/` parser files as required, `third_party/Dubzer.WhatwgUrl/README.md`, and `tests/Core.Tests/UrlTests.cs` plus pinned fixtures as required.
   - **Tests to Write:** Focused regressions for each uncovered parser state/host case; complete applicable pinned `urltestdata.json`, `toascii.json`, and Unicode 17 IDNA corpus tests.
   - **Steps:** Add a failing regression for each verified gap; make the smallest parser correction while preserving existing local patches and license notices; run the full relevant fixture families and Core tests.
3. **Phase 3: Complete Core URL operations**
   - **Objective:** Verify and close gaps in URL parsing/resolution, component getters and snapshot setters, origin computation, and their component-preservation/error semantics without changing `BrowserUrl` immutability.
   - **Files/Functions to Modify/Create:** `src/Core/Core.Url/BrowserUrl.cs`, `SecurityOrigin.cs`, `UrlSearchParams.cs`, `tests/Core.Tests/UrlTests.cs`, and `tests/Core.Tests/UrlSearchParamsTests.cs`.
   - **Tests to Write:** Spec-derived setter and origin cases, including state-override edge cases, opaque URLs, Unicode/USVString inputs, and search/hash/query delimiter preservation.
   - **Steps:** Add targeted failing cases for confirmed omissions; implement the URL Standard algorithms through existing Core abstractions; run focused and full Core tests.
4. **Phase 4: Add mutable URL and live searchParams linkage**
   - **Objective:** Provide browser-facing mutable URL state whose `searchParams` object has stable identity and synchronizes changes in both directions, while retaining the existing immutable Core facade.
   - **Files/Functions to Modify/Create:** The smallest suitable API in `src/Core/Core.Url/` and focused tests in `tests/Core.Tests/`; update the Core URL guide if public semantics change.
   - **Tests to Write:** Same-object identity, URL-to-params updates, params-to-URL updates, empty-query behavior, and preservation of non-query components.
   - **Steps:** Add failing synchronization/identity tests; implement the mutable model without weakening `BrowserUrl` snapshot behavior; run focused and full Core tests.
5. **Phase 5: Implement browser JavaScript URL interfaces**
   - **Objective:** Expose `URL` and `URLSearchParams` to the scripting host with interface-specific constructor, static method, conversion, exception, attribute, and iteration behavior.
   - **Files/Functions to Modify/Create:** `src/Engine/Engine.Scripting/`, `tests/Engine.Scripting.Tests/`, and the scripting guide; follow the existing primitive-only host bridge and ownership/resource limits.
   - **Tests to Write:** JavaScript constructor and `canParse`/`parse` cases, mutable attribute behavior, `searchParams` identity/linkage, Web IDL string conversion and errors, and iterable/forEach/stringifier behavior.
   - **Steps:** Add failing scripting tests for each interface contract; implement only URL-specific bindings without introducing general generated Web IDL or exposing CLR objects; run focused and full scripting tests.
6. **Phase 6: Final conformance evidence and support documentation**
   - **Objective:** Re-run all pinned URL evidence, state precise implemented behavior and remaining exclusions, and update standards/subsystem documentation consistently.
   - **Files/Functions to Modify/Create:** `docs/core.md`, `docs/standards.md`, `src/Core/Core.Url/README.md`, `src/Engine/Engine.Scripting/README.md` or its owning guide, and fixture provenance documentation.
   - **Tests to Write:** Any regression tests needed for findings from the final offline conformance run; preserve the complete existing fixture families.
   - **Steps:** Run Core and scripting suites and the solution build/test/format checks; resolve failures attributable to this work; update the support inventory only to claims established by the evidence.

**Open Questions**

1. None for the approved scope: the user approved Core URL completion plus browser-facing `URL`/`URLSearchParams` behavior and interface-specific Web IDL semantics, excluding general Web IDL.
