# Platform.Windows

Windows composition of SDL3 windows, system/per-user font directories,
.NET process launching and roaming/local application paths. Selects SDL's
windows driver; unsupported overrides and non-Windows construction fail.

Windows 10+, x64 and arm64 remain targets requiring native validation.
The SDL package contains Windows x64/arm64 assets. AppContainer/Job Objects
are not implemented; RequireSandbox is rejected before launch.

See the [platform guide](../../../docs/platform.md).
