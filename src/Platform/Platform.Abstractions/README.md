# Platform.Abstractions

Phase-1 placeholder for OS-neutral windows, input, presentation surfaces,
font discovery, clocks and process/sandbox capabilities. Keep native handles
out of public portable APIs.

Backends implement these contracts; engine projects may depend on them, never
on concrete backends. Cite official platform/API documentation as contracts
are added. See the [architecture](../../../docs/architecture/overview.md).
