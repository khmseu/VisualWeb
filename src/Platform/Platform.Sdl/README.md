# Platform.Sdl

Shared SDL3 implementation of window/input/pixel contracts, plus the shared
.NET process launcher and file-font catalog used by OS composition roots.
This additional project keeps SDL marshalling and lifecycle logic out of the
portable contracts and avoids duplicating it between Linux and Windows.

Uses matching SDL3-CS and SDL3-CS.Native 3.4.2 packages, whose native assets
include Linux/Windows x64 and arm64. Native OS libraries/displays are still
required. No Skia/HarfBuzz/V8 dependencies are added at this phase.

Only one window system owns SDL video/events per process; windows and event
handlers run on its creating UI thread. Disposal releases owned windows and
SDL video, and restores the previous video-driver hint. Failed construction
releases ownership. Thread violations and native errors are explicit.

Surface presentation copies opaque BGRA32 bytes synchronously with explicit
stride, converts to the current SDL window surface, and does not retain caller
memory or expose native handles. Frame dimensions must match physical window
dimensions; query them again after resize/scale events.

Implemented input: close, expose, physical resize, display scale, focus,
pointer movement/buttons/wheel, key transitions and committed UTF-8 text.
IME composition/preedit, clipboard, touch and accessibility are future work.
Text-input activation can fail when the compositor lacks its required protocol;
this is reported rather than silently disabling input.

See [native validation](../../../tools/PlatformSmoke/README.md) and
the [platform guide](../../../docs/platform.md).
