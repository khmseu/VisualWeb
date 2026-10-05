# Platform layer (phase 2)

## Composition

Choose LinuxPlatformServices or WindowsPlatformServices at the application's
composition root. Both implement IPlatformServices, exposing app paths,
TimeProvider.System, fonts, processes and OpenWindows. OS constructors reject
the wrong host OS. They do not create profile directories merely by construction.

The shared [Platform.Sdl](../src/Platform/Platform.Sdl/) implementation avoids
duplicated SDL interop. [Platform.Abstractions](../src/Platform/Platform.Abstractions/)
has no native packages. OS projects reference the shared implementation;
engine projects must reference only portable contracts.

SDL video/event operations must run on the process UI/main thread. Construct
the window system there, then create/pump/present/dispose on that same thread;
do not continue a UI loop on an arbitrary async continuation. Exactly one
window system owns video/events per process, but it may own multiple windows.

## Display selection and failures

Linux auto selection tries Wayland when WAYLAND_DISPLAY is nonempty, followed
by X11 when DISPLAY is nonempty. Actual SDL initialization determines usability.
Failed candidates are retained in InitializationFailures; the composition root
must show/log these diagnostics if fallback occurs. All candidates failing
throws an error containing their individual reasons.

OpenWindows overrides take precedence over VISUALWEB_VIDEO_BACKEND.
Linux accepts `x11` or `wayland`; Windows accepts `windows`. Explicit overrides
never fall back to another backend. No advertised display is an explicit error.
SDL's `dummy` driver is used only for explicit development validation, not as a
successful-looking replacement for an unavailable desktop.

Native assets come from SDL3-CS.Native 3.4.2, matched to SDL3-CS 3.4.2.
Linux still needs the usual X11/Wayland system libraries. Wayland window
decorations and text input depend on compositor capabilities; missing text
input support is an explicit PlatformException. A successful headless surface
test does not certify real IME/keyboard behavior or a compositor's decorations.

## Units, frames and events

LogicalSize is SDL window coordinate size. PixelSize is the physical framebuffer
size. Pointer coordinates are logical; PixelDensity is the physical/logical
ratio, while DisplayScale describes preferred UI scaling.

Present accepts opaque BGRA32, an explicit byte stride, and dimensions matching
PixelSize. It checks bounds/overflow, copies synchronously, converts into the
current SDL surface, and updates the window. Resize invalidates the previous
frame dimensions. Native surface pointers never escape the portable contract.

Events are routed by window ID and copied to managed values. Close requests
must be accepted by the owner via Dispose. Disposing one window does not close
others. TextEntered holds copied UTF-8 committed text. Preedit/IME composition,
touch, clipboard, accessibility and richer keyboard semantics remain future work.

## Fonts, paths and processes

Linux uses the XDG absolute-path rule with conventional home defaults. Windows
uses roaming/local app directories. FileFontCatalog enumerates .ttf/.otf/.ttc
files in conventional system/user directories without following symlink
directories; inaccessible directories fail visibly. This is not a Fontconfig
query, CSS family matcher or shaping engine.

SystemProcessLauncher uses ProcessStartInfo.ArgumentList and UseShellExecute=false.
Arguments are never concatenated into shell commands. It supports environment
overrides/removals and a working directory. Waiting can be cancelled without
killing the child. Termination is explicit; handle disposal alone leaves it
running. Requested confinement fails before launch: there is **no OS sandbox**.
The [phase-10a browser client](renderer-processes.md) separately owns and
terminates per-tab workers through private pipes; process separation is not
confinement. Renderers must not load hostile content under a claim of sandboxing.

## Validation

```sh
dotnet test tests/Platform.Tests/Platform.Tests.csproj
dotnet run --project tools/PlatformSmoke -- dummy
dotnet run --project tools/PlatformSmoke -- x11
dotnet run --project tools/PlatformSmoke -- wayland
dotnet run --project tools/PlatformSmoke -- windows
```

Managed tests stay display-independent. Platform.Tests is one shared contract/
selector/service suite, rather than duplicated per-backend suites. Native smoke
is an explicit tool, separate from ordinary tests.

Validated here on Linux x64: dummy and X11 (including text-input activation),
headless Weston 13 Wayland (surfaces/events/lifecycle, **text-input check explicitly
excluded** because that compositor lacks zwp_text_input_v3), automatic Wayland
preference and real failure-to-X11 fallback. The temporary compositor was not
installed system-wide. Windows, arm64, real Wayland keyboard/IME, and high-DPI
desktop behavior still need their target environments.

Official SDL, XDG and .NET/OS references are recorded in the
[standards registry](../specs/manifest.json) and locally cached separately from tests.
