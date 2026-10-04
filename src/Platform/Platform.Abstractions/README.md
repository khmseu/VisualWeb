# Platform.Abstractions

Portable contracts for windows, input, BGRA pixel presentation, font files,
app paths, TimeProvider clocks and child-process lifecycles. No native handles
are exposed. SDL and official OS/.NET documentation are cited in XML docs.

Backends implement these contracts; engine projects may depend on them, never
on concrete backends. Window operations and event dispatch run on the creating
UI thread. Input coordinates use logical window units; surface dimensions use
physical pixels. Pixel density converts between those spaces, while display
scale describes UI content scaling.

Close events are requests, not automatic destruction. A child-process handle's
disposal does not kill it; termination is explicit. RequireSandbox launches
are rejected until OS confinement exists.

See the [platform guide](../../../docs/platform.md).
