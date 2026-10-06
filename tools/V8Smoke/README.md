# V8Smoke

Explicit native ClearScript/V8 host and confinement probe. It does not render a
page, fetch resources or execute untrusted external scripts.

```sh
dotnet run --project tools/V8Smoke -- --local
dotnet run --project tools/V8Smoke -- \
  --require-sandbox --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

Required confinement uses the existing Linux x64 or Windows AppContainer/Job
Object launcher, with no weaker fallback. The worker verifies that profile
before creating V8. Unsupported targets/refused policy/native initialization
fail visibly. The outer 30-second deadline cleans up only the owned scope/tree
or job. The supplied trusted font is a launcher prerequisite, not loaded by V8.

Measures private isolates, precise primitives, no CLR/browser globals, copied
result and external-buffer limits, monitored heap invalidation, infinite-script
deadlines and fresh-isolate recovery.
Classic-script probes additionally verify persistent lexical bindings, isolate
separation and whole-batch deadline interruption.
Optional microtask probes verify native Promise/queueMicrotask FIFO ordering,
per-source checkpoints and deadline interruption inside native callbacks,
under the same confinement profile.
Synthetic event probes verify DOM capture/target/bubble ordering, cancelation
and listener-enqueued microtask title mutation under unchanged confinement.
Finite document lifecycle probes additionally verify loading/interactive/
DOMContentLoaded/complete order, trusted event flags and checkpoint title mutation.
Bounded query probes verify selector results, static-list identity and Element
matches/closest locally and under the unchanged renderer confinement profiles.
Live class token probes verify className reflection, same-object classList,
ordered mutation, readonly indexed reads/iteration and class-selector matching.
Tree inspection probes verify containment, root/identity, parentElement and
element-only child/sibling navigation under the same confinement profiles.
ID reflection/attribute-toggle probes verify live selector identity and
presence/removal under unchanged confinement.
Optional live DOM probes verify native title/text/attribute mutations, branded
element/text/fragment creation/insertion/removal and private callback
invisibility; no production browser page-script execution is enabled.
Linux arm64 confinement remains unsupported; native Windows/arm64 evidence
requires those hosts. See the
[scripting guide](../../docs/scripting.md) for guarantees and limitations.
