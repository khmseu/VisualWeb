# Platform.Linux

Linux composition of SDL3 windows, file-based font discovery, .NET process
launching and XDG app paths. Supports X11 and Wayland: prefer advertised
Wayland, then try advertised X11 if initialization fails. Explicit overrides
do not silently fall back. Previous failed attempts are exposed by the SDL
window system's InitializationFailures.

Override with OpenWindows("x11"/"wayland") or VISUALWEB_VIDEO_BACKEND.
No display is an explicit failure, not an implicit headless mode. Dummy mode
is available only directly through shared SDL services for validation.

Fonts are enumerated from conventional system/user directories; custom
Fontconfig configuration and CSS family matching are not implemented.
Native process confinement remains deferred; RequireSandbox is rejected.
See the [platform guide](../../../docs/platform.md).
