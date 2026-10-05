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
Linux arm64 confinement remains unsupported; native Windows/arm64 evidence
requires those hosts. See the
[scripting guide](../../docs/scripting.md) for guarantees and limitations.
