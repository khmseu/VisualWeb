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
copied into a unique staging directory. Explicit optional bold/italic/bold-italic
font files are copied individually beside the staged regular font; their host
directories are not granted access. The AppContainer SID receives
read/execute access to that staged tree. Its temporary directory is also
read-only, and the profile storage ACL replaces the container SID's default
access with read/execute only. The host process supplies only private standard
streams and a minimal runtime environment. No network, broad host filesystem, or
other capabilities are added. Staging and AppContainer profile resources are removed
when the owned worker is disposed. Profile storage lookup converts the binary
AppContainer SID to a Unicode SID string before calling
`GetAppContainerFolderPath`; the SID string and returned path are released with
`LocalFree` and `CoTaskMemFree`, respectively. Native launch failures include the
Win32 error code and system message so CI diagnostics retain the cause of refusal. The
minimal environment includes `LOCALAPPDATA` pointing only to that write-denied
AppContainer profile storage, required for Windows AppContainer environment
redirection; it never inherits the host user's local app-data path.

The inherited handle list contains only the inheritable child pipe endpoints:
stdin's read end and stdout/stderr's write ends. The parent retains the opposite,
non-inheritable endpoints. Reversing the stdout/stderr endpoints supplies
non-inheritable handles to `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, violating its
launch contract and causing `CreateProcessW` to reject the launch with error 87.

## Process and resource limits

| Control                                        | Required value                                               |
| ---------------------------------------------- | ------------------------------------------------------------ |
| AppContainer capabilities                      | None                                                         |
| Job Object active process limit                | 1                                                            |
| Job Object per-process commit limit            | 512 MiB                                                      |
| Job Object total commit limit                  | 512 MiB                                                      |
| Job Object CPU hard cap                        | 100% of one logical processor                                |
| Job Object lifetime                            | Kill all assigned processes when the owned job handle closes |
| Renderer writes to staged temp/profile storage | Denied                                                       |
| Managed heap                                   | 256 MiB                                                      |
| Configured processor count                     | 2                                                            |
| IPC startup/render deadline                    | Existing 30 seconds                                          |

The Job Object is assigned at process creation, before the suspended worker is
resumed. Security capabilities, the Job Object list and the inherited handle
list use owned unmanaged storage retained until process creation completes;
process attribute identifiers match the native pointer-sized ABI. The startup handshake requires `windows-appcontainer-job-v1`; the
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

## RID-specific staging experiment

The Windows confinement workflow compares the existing portable renderer with a
framework-dependent RID-specific publish candidate for the native runner's
`win-x64` or `win-arm64` RID. This is a staging-size experiment only: normal CLI
and library deployment choices are unchanged, and the launcher still receives
the renderer DLL and copies its complete trusted deployment. Candidate outputs
live under the ignored project-local
`src/Apps/VisualWeb.Renderer/obj/windows-rid-publish/<rid>/` directory;
RID build outputs are separately isolated under `obj/windows-rid-build/` so
publishing cannot add native assets to the portable `bin/Release/net10.0`
deployment.

The workflow uses the SDK's framework-dependent publish with `--runtime <rid>
--self-contained false -p:UseAppHost=false`. For a manual native comparison,
set `$rid` to `win-x64` or `win-arm64`, then publish and validate with the same
commands:

```powershell
$rid = "win-x64" # Or win-arm64 on a native ARM64 Windows runner.
$buildRoot = Join-Path $PWD "src/Apps/VisualWeb.Renderer/obj/windows-rid-build/"
dotnet publish src/Apps/VisualWeb.Renderer/VisualWeb.Renderer.csproj `
  --configuration Release --runtime $rid --self-contained false `
  -p:UseAppHost=false "-p:BaseOutputPath=$buildRoot" `
  --output "src/Apps/VisualWeb.Renderer/obj/windows-rid-publish/$rid"
pwsh -NoProfile -File scripts/Validate-WindowsRendererPublish.ps1 `
  -RuntimeIdentifier $rid `
  -PublishDirectory "src/Apps/VisualWeb.Renderer/obj/windows-rid-publish/$rid" `
  -PortableDirectory src/Apps/VisualWeb.Renderer/bin/Release/net10.0 `
  -RequireReduction
dotnet run --project src/Apps/VisualWeb.Browser --configuration Release --no-build -- `
  --multiprocess `
  --renderer "src/Apps/VisualWeb.Renderer/obj/windows-rid-publish/$rid/VisualWeb.Renderer.dll" `
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf --smoke --backend windows
dotnet run --project src/Apps/VisualWeb.Browser --configuration Release --no-build -- `
  --multiprocess `
  --renderer "src/Apps/VisualWeb.Renderer/obj/windows-rid-publish/$rid/VisualWeb.Renderer.dll" `
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf --require-sandbox `
  --enable-inline-scripts --smoke --backend windows
```

The validation checks the actual publish files and RID deps target, the
framework-dependent runtime config, required Windows native payloads, absence
of incompatible native assets (only the active RID or generic `win`/`any`
fallbacks are allowed), and an observed total-file-byte
reduction against the portable output (no fixed size threshold). It verifies
the published HarfBuzz and Skia native PDBs by SHA-256 against the matching
restored Win32 native package symbols. Symbols are retained; trimming, AOT,
self-contained deployment and native payload filtering are not used. File-size
reduction is not a guarantee of staging or startup wall-clock savings.

After the existing portable controls and native tests, CI runs the same static
and inline-script multiprocess smokes against the RID candidate. The renderer
DLL launch, font, Windows backend, AppContainer/Job Object policy, and script
policy match their portable controls; `smoke-timing` and `smoke-inventory v=1`
records remain in the job logs for comparison. Cross-publishing and structural
checks on Linux do not execute Windows code. Candidate execution and confinement
still require native Windows x64 and arm64 CI evidence.

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

For native browser smoke timing, `windows.start` nests `windows.profile-storage`
(profile creation, storage and write restriction), `windows.acl`,
`windows.copy-app`, `windows.copy-runtime` (hostfxr, runtime and host),
`windows.copy-fonts`, `windows.temp-acl`, `windows.job` and
`windows.process` (pipes, process attributes, creation and resume). Failed
launches also emit `windows.start-cleanup` if cleanup is entered;
`windows.dispose` covers cleanup and contains
`windows.dispose-process-streams` (stream closure, Job Object termination,
process wait and process-handle disposal), `windows.dispose-profile` (AppContainer
profile removal), and `windows.dispose-staging` (staged-directory deletion).
The phases retain the existing order and are nested wall-clock measurements.
These host-side records use the same renderer lifetime ID as the browser's
`renderer.start`/`renderer.dispose`. They are wall clocks and overlap their
enclosing phases. The optional `HostTimingBuffer` passed to sandbox startup
is sealed, bounded and data-only; no caller callback or writer runs during
launch, successful ownership transfer or worker disposal. A full buffer latches
an export error without interrupting native cleanup or changing launch policy.
When enabled, one versioned `smoke-inventory` aggregate reports successfully
copied app and runtime file counts/bytes for each renderer. Runtime includes
host/fxr, the selected shared runtime and `dotnet.exe`; app assets are split
into non-RID, active Windows RID and other RID partitions, with `.pdb` files
reported as an overlapping subset. Other RID assets are not assumed unused.
The existing staging traversal supplies counts and skips reparse points; there
is no second scan, no path/content output, and no length metadata read when
diagnostics are disabled. The aggregate uses explicit `files,bytes` units and
does not change the existing timing record schema.
See the [schema](browser-shell.md#validation). Native Windows
CI is required to observe and validate these stages.

The browser suite exercises confined worker launch, native Job Object memory
pressure recovery, CPU-pressure cancellation/deadlines and unaffected renderers
on Windows, including a private stdin roundtrip to distinct stdout/stderr pipes
and checks that the parent endpoints are non-inheritable. Platform tests verify
the 64-bit native startup structure sizes and standard-handle offsets used on
x64 and arm64. The native Windows x64/arm64 workflow runs the normal solution test
suite and repeats CPU-pressure cancellation recovery five times without retrying
failures. Only the CPU deadline theory row is explicit: ordinary local and
push/PR runs skip it. Request it through the manual workflow input
`run_cpu_deadline_tests`, or the VSTest explicit-test command in the
[test guide](../tests/README.md#opt-in-cpu-deadline-tests). The manual workflow
runs the deadline row once per architecture, not in the five-repeat step.
Process integration tests use a nonparallel xUnit collection so unrelated
browser tests cannot compete with their cold worker staging/startup. CPU-pressure
recovery uses the production 30-second startup/render deadline, not a five-second
test override that can expire during ordinary cold ARM64 staging or replacement
startup before the fault is injected. The 500 ms cancellation remains unchanged;
the deadline case still requires termination of the unresponsive worker. Recovery checks actual
worker exit, replacement pixels and reuse, and the unaffected peer's identity.
`WindowsSandboxSmoke` probes actual host-file/environment,
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
default; `--enable-inline-scripts` opts into bounded parser-blocking inline and
browser-brokered external classic execution. Modules, broader HTML scheduling
and a persistent browser event loop remain deferred.
