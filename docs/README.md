# Documentation index

- [Architecture and process boundaries](architecture/overview.md)
- [Project ownership and dependency map](project-map.md)
- [Platform APIs, native backends and validation](platform.md)
- [Core URL, encoding and MIME APIs and conformance scope](core.md)
- [Resource loading, redirects, cookies and security boundary](networking.md)
- [HTML tokenization, static tree building and renderer-local DOM](html-dom.md)
- [Static CSS syntax, selectors, cascade and computed styles](css.md)
- [Native fonts/shaping and static LTR block/inline layout](text-layout.md)
- [Display lists, CPU painting and offline HTML-to-frame rendering](painting.md)
- [Development browser shell, navigation, tabs and multiple windows](browser-shell.md)
- [Per-tab renderer processes, IPC, deadlines and unfinished confinement](renderer-processes.md)
- [Linux x64 renderer confinement, exact guarantees and denial probes](linux-confinement.md)
- [Windows AppContainer and Job Object renderer confinement](windows-confinement.md)
- [Standards sources, cache and citation convention](standards.md)
- [Foundation decision record](adr/0001-browser-foundation.md)
- [Approved phased roadmap](../plans/browser-foundation-plan.md)
- [Spec cache tool](../tools/SpecCache/README.md)
- [Tests and test-project convention](../tests/README.md)
- [AI agent entry point](../AGENTS.md)

Phases 1-9 provide scaffolding, tooling, platform services, core standards
utilities, loading, static HTML/DOM/CSS, text/layout, CPU painting and a
development-only browser shell. Phase 10a adds per-tab process separation;
Phase 10b adds opt-in Linux x64 confinement, strengthened by phase-10c hard
per-worker resource accounting; Windows adds an AppContainer/Job Object
development profile with native x64/arm64 validation. Production isolation
and broader standards remain later work.
