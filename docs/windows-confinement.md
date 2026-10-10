# Windows renderer confinement

The Windows x64/arm64 renderer profile uses a capability-free
AppContainer token and a per-renderer Job Object. It is a development
confinement layer, not production-safe hostile-content browsing or completion
of browser isolation. The browser remains privileged and unconfined. Renderer
process separation does not provide origin/site isolation.

## Requirements and use

Requires Windows 10 version 1709 or later and the framework-dependent .NET 10
renderer deployment. Recommended CLI `--multiprocess` and legacy
`--development-multiprocess` require this profile by default;
`--require-sandbox` is a compatible redundant assertion. Public parsing invokes
`WindowsRendererSandbox.RequireSupport` before SDL/content/process startup.
The browser refuses unsupported OS versions, apphost-only
renderer launch, and every failed setup without an unsandboxed retry.
Unsandboxed execution requires explicit trusted-content
`--allow-unsandboxed-development` in a development mode, rejected with normal
mode or `--require-sandbox`. Low-level renderer/library defaults are unchanged.

The browser launches the trusted framework-dependent renderer DLL. The
renderer, its deployment directory, active runtime and selected font are
copied into a unique staging directory. The AppContainer SID receives
read/execute access to that staged tree. Its temporary directory is also
read-only, and the AppContainer profile directory denies writes to the
container SID. The host process supplies only private standard streams and a
minimal runtime environment. No network, broad host filesystem, or other
capabilities are added. Staging and AppContainer profile resources are removed
when the owned worker is disposed.

## Process and resource limits

| Control | Required value |
| --- | --- |
| AppContainer capabilities | None |
| Job Object active process limit | 1 |
| Job Object per-process commit limit | 512 MiB |
| Job Object total commit limit | 512 MiB |
| Job Object CPU hard cap | 100% of one logical processor |
| Job Object lifetime | Kill all assigned processes when the owned job handle closes |
| Renderer writes to staged temp/profile storage | Denied |
| Managed heap | 256 MiB |
| Configured processor count | 2 |
| IPC startup/render deadline | Existing 30 seconds |

The Job Object is assigned at process creation, before the suspended worker is
resumed. The startup handshake requires `windows-appcontainer-job-v1`; the
worker checks its token and Job Object membership before opening its font or
receiving content. It queries and verifies the active Job Object's process,
per-process and total commit limits, and CPU hard cap. This profile string is
trusted-binary configuration confirmation, not remote attestation.

Windows Job Objects have no thread/task-count limit or per-job swap limit.
The 512 MiB commit limits bound process/job committed memory (including
pagefile-backed commit), but do not prohibit pagefile use or provide a
Linux-equivalent `memory.swap.max`. Rather than claim a per-directory disk
quota, this profile removes renderer write access to its only explicitly
configured temporary and AppContainer profile storage. Other Windows kernel,
filesystem and system-DLL behavior is not a native-code exploit boundary. This
implementation does not claim a browser-wide resource quota, persistence
protection, site isolation, or production supply-chain policy.

## Validation

```sh
dotnet test tests/Platform.Tests/Platform.Tests.csproj
dotnet test tests/VisualWeb.Browser.Tests/VisualWeb.Browser.Tests.csproj
dotnet run --project tools/WindowsSandboxSmoke -- `
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
dotnet run --project tools/PlatformSmoke -- windows
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --multiprocess \
  --renderer src/Apps/VisualWeb.Renderer/bin/Debug/net10.0/VisualWeb.Renderer.dll \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf --smoke --backend windows
```

The browser suite exercises confined worker launch, native Job Object memory
pressure recovery, CPU-pressure cancellation/deadlines and unaffected renderers
on Windows. `WindowsSandboxSmoke` probes actual host-file/environment,
process-creation, loopback-network and storage-write denials. The native platform
smoke separately validates actual SDL windows, events, pixels, fonts and
lifecycle. On native Windows x64 and arm64 runners, the Windows confinement
workflow also runs the platform smoke and browser smoke in local, unsandboxed
multiprocess, and required-sandbox multiprocess modes. Together with the worker
denial probe, these exercise native window presentation and the full confined
browser launch path on both architectures. They do not certify high-DPI desktop
behavior or production isolation; Linux builds and policy tests do not certify
Windows targets.

The phase-11a [V8 host probe](scripting.md) is also scheduled in native Windows
x64/arm64 CI under this same profile; Linux host validation does not certify
ClearScript compatibility with AppContainer. Page scripts remain disabled by
default; `--enable-inline-scripts` opts into bounded post-parse inline classic
execution. External scripts and a persistent browser event loop remain deferred.
