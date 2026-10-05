# PlatformSmoke

Explicit native integration check, not a browser UI and not part of offline
unit tests. Requires native SDL and the selected OS display/backend:

```sh
dotnet run --project tools/PlatformSmoke -- dummy
dotnet run --project tools/PlatformSmoke -- x11
dotnet run --project tools/PlatformSmoke -- wayland
dotnet run --project tools/PlatformSmoke -- windows
```

With no backend, uses the OS composition root's automatic selection.
Checks two real SDL windows, BGRA pixel values read back from the SDL surface,
window-specific close/pointer events, independent disposal, thread affinity,
font enumeration and teardown/reinitialization. Errors go to stderr with a
nonzero exit code. It briefly creates small windows and exits; no long-running
server or renderer is left behind.

Some headless compositors lack Wayland text-input protocols. To validate the
remaining native features explicitly without claiming text-input support:

```sh
dotnet run --project tools/PlatformSmoke -- wayland --skip-text-input
```

The tool prints a prominent skipped-check diagnostic. Do not use this option
to claim full keyboard/IME validation. Native Windows and arm64 checks must
run on those targets; a dummy driver pass is not desktop portability proof.
The browser's [Windows confinement smoke](../../docs/windows-confinement.md)
additionally exercises an AppContainer renderer and exact page pixels.
