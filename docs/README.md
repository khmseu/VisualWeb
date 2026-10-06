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
- [ClearScript/V8 host foundation, limits and native confinement probes](scripting.md)
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
Phase 11a adds the bounded ClearScript/V8 host and native confinement probes,
without enabling page scripts or changing the static rendering pipeline.
Phase 11b adds persistent classic-script global lexical state and bounded
ordered batches; page execution and HTML script scheduling remain deferred.
Phase 11c adds minimal live title/ID/textContent DOM facades and binding probes,
without enabling browser page scripts.
Phase 11d adds explicit opt-in post-parse inline classic page execution,
transactional publication and retained scripted DOM repainting via IPC v3.
External scripts, HTML scheduling and event loops remain deferred.
Phase 11e adds bounded element attributes, element/text/fragment creation and
branded Node navigation/mutation while preserving the initial script snapshot.
Phase 11f adds opt-in finite native Promise/queueMicrotask checkpoints, bounded
task queues and shared deadlines before painting; persistent event loops remain
deferred. See the [scripting guide](scripting.md) for exact limits and deviations.
Phase 11g adds opt-in bounded synthetic Event/EventTarget and Node/document
capture/target/bubble dispatch, without automatic browser events or live hosts
after initial execution.
Phase 11h adds finite readonly document readiness and private readystatechange/
DOMContentLoaded dispatch/checkpoints under the same navigation task budgets,
without Window/load or persistent V8.
Phase 11i adds bounded DOM queries and static NodeList snapshots, sharing the
existing first-party CSS selector parser and native Node wrapper identities.
Phase 11j adds className and live bounded classList, including ordered token
mutations and iteration, over the existing private attribute bridge.
