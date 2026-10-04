# Platform.Linux

Phase-1 placeholder for Linux OS dependencies. Must support both X11 and
Wayland through SDL3, with tested initialization/fallback and explicit override.
Future process confinement uses Linux-specific facilities.

Implement Platform.Abstractions; keep Linux code here, not in engine libraries.
See the [architecture](../../../docs/architecture/overview.md). No native
backend or sandbox is implemented yet.
