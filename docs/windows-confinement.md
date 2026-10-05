# Windows renderer confinement

The optional Windows x64/arm64 renderer profile uses a capability-free
AppContainer token and a per-renderer Job Object. It is a development
confinement layer, not production-safe hostile-content browsing or completion
of browser isolation. The browser remains privileged and unconfined. Renderer
process separation does not provide origin/site isolation.

## Requirements and use

Requires Windows 10 version 1709 or later and the framework-dependent .NET 10
renderer deployment. Run the existing multiprocess browser command with
`--require-sandbox`; the browser refuses unsupported OS versions, apphost-only
renderer launch, and every failed setup without an unsandboxed retry.

The browser launches the trusted framework-dependent renderer DLL. The
renderer, its deployment directory, active runtime and selected font are
copied into a unique staging directory. The AppContainer SID receives
read/execute access to that staged tree and write access to its temporary
directory. Windows also provides the AppContainer's private profile storage.
The host process supplies only private standard streams and a minimal runtime
environment. No network, broad host filesystem, or other capabilities are
added. Staging and AppContainer profile resources are removed when the owned
worker is disposed.

## Process and resource limits

| Control | Required value |
| --- | --- |
| AppContainer capabilities | None |
| Job Object active process limit | 1 |
| Job Object per-process memory | 512 MiB |
| Job Object CPU hard cap | 100% of one logical processor |
| Job Object lifetime | Kill all assigned processes when the owned job handle closes |
| Managed heap | 256 MiB |
| Configured processor count | 2 |
| IPC startup/render deadline | Existing 30 seconds |

The Job Object is assigned at process creation, before the suspended worker is
resumed. The startup handshake requires `windows-appcontainer-job-v1`; the
worker checks its token and Job Object membership before opening its font or
receiving content. This profile string is trusted-binary configuration
confirmation, not remote attestation.

The temporary directory and AppContainer profile storage are not subject to a
kernel disk quota. The AppContainer still relies on the Windows kernel and its normal system DLL
loading policy. This implementation does not claim a native-code exploit
boundary, browser-wide resource quota, persistence protection, site isolation,
or production supply-chain policy.

## Validation

```sh
dotnet test tests/Platform.Tests/Platform.Tests.csproj
dotnet test tests/VisualWeb.Browser.Tests/VisualWeb.Browser.Tests.csproj
dotnet run --project tools/PlatformSmoke -- windows
dotnet run --project src/Apps/VisualWeb.Browser -- \
  --development-multiprocess \
  --renderer src/Apps/VisualWeb.Renderer/bin/Debug/net10.0/VisualWeb.Renderer.dll \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf --require-sandbox --smoke --backend windows
```

The browser suite exercises confined worker launch and exact frame pixels on
Windows. The native smoke command separately validates actual SDL windows,
events, pixels, fonts and lifecycle. These native checks must run on Windows
x64 and arm64; Linux builds and policy tests do not certify them.
