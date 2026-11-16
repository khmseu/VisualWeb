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

The Windows confinement workflow builds the portable renderer as a comparison
baseline and publishes a framework-dependent RID-specific candidate for the
native runner's `win-x64` or `win-arm64` RID. After validation, that candidate
is selected as the `Renderer` fixture for every Windows browser test, including
the five CPU-cancellation repeats; the selection happens before the first
browser-test invocation. The native publish includes its apphost, so the suite
continues to exercise both DLL launches and its native apphost test. The
candidate is also used by the ordinary native Windows static and scripted
multiprocess smokes and manual comparison; normal CLI and library deployment
choices are unchanged, and the launcher still receives the renderer DLL and
copies its complete trusted deployment. Candidate outputs live under the
ignored project-local `src/Apps/VisualWeb.Renderer/obj/windows-rid-publish/<rid>/`
directory.
RID build outputs are separately isolated under `obj/windows-rid-build/` so
publishing cannot add native assets to the portable `bin/Release/net10.0`
deployment.

The workflow uses the SDK's framework-dependent publish with `--runtime <rid>
--self-contained false -p:UseAppHost=true`. For a manual native comparison,
set `$rid` to `win-x64` or `win-arm64`, then publish and validate with the same
commands:

```powershell
$rid = "win-x64" # Or win-arm64 on a native ARM64 Windows runner.
$buildRoot = Join-Path $PWD "src/Apps/VisualWeb.Renderer/obj/windows-rid-build/"
dotnet publish src/Apps/VisualWeb.Renderer/VisualWeb.Renderer.csproj `
  --configuration Release --runtime $rid --self-contained false `
  -p:UseAppHost=true "-p:BaseOutputPath=$buildRoot" `
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

After the portable baseline and fast tests, native CI publishes and validates
the RID candidate, then rebuilds the browser-test project with
`RendererFixtureSource` set to the validated publish directory before any
browser test runs. Fixture selection defaults to the portable renderer for
ordinary local builds. Selection cleans only the test output's owned
`Renderer/` directory before copying the exact selected deployment; it does not
merge portable assets into the RID fixture or alter the separate `Peer/`
fixture. A source missing the renderer assembly, or overlapping the fixture
output, fails the build rather than falling back; CI validates the complete
publish before selection. Native CI then runs the browser tests and the two ordinary
multiprocess smokes. Static smoke uses `--multiprocess`; scripted smoke retains
the legacy `--development-multiprocess` assertion, `--require-sandbox`, and
`--enable-inline-scripts`. Both use the native RID renderer DLL, registered
font, Windows backend and existing AppContainer/Job Object policy. The portable
renderer is not run as an automatic duplicate; `smoke-timing` and
`smoke-inventory v=1` records remain in the job logs. Cross-publishing and
structural checks on Linux do not execute Windows code. Candidate execution and
confinement still require native Windows x64 and arm64 CI evidence.

### Optional native wall-time comparison

The last workflow step is an opt-in comparison. The Windows confinement workflow
exposes a typed boolean `compare_renderer_variants` input through `workflow_call`,
defaulting to `false`; its ordinary `workflow_dispatch` has no inputs.
The dedicated manual-only Windows startup comparison workflow calls that same
workflow with the input set to `true`, reusing the complete existing
jobs/matrix/build/tests/publish sequence without duplication. The comparison gate
requires both the original caller event `workflow_dispatch` and the true boolean;
the reusable workflow's event context follows its caller. Push, pull-request,
and ordinary Windows confinement manual runs do not execute the additional
comparisons. Explicitly opt in with:

```sh
gh workflow run windows-startup-comparison.yml
```

This is the retained portable-renderer control and repeated timing path;
ordinary Windows CI uses RID for both smoke scenarios and its browser-test
renderer fixture. The comparison does not enable explicit CPU deadline rows.
Those remain a separate manual request in the
[test guide](../tests/README.md#opt-in-cpu-deadline-tests).

Add `--ref <branch>` when dispatching a branch other than the repository's
default branch. For each native architecture the step runs exactly eight
additional browser CLI invocations: static smoke portable then RID-specific,
static smoke RID-specific then portable, scripted smoke portable then
RID-specific, and scripted smoke RID-specific then portable. Every invocation
uses the Windows backend, the registered Noto Sans font, `--multiprocess`,
`--require-sandbox`, and the same smoke configuration; only the renderer DLL
changes within a scenario/order pair. Scripted runs additionally use
`--enable-inline-scripts`. The step has a 15-minute wall timeout and stops at
the first nonzero CLI exit or any `smoke-diagnostic-error` row, including when
the CLI itself returned zero. Full CLI smoke timing, inventory, and error output
is retained in the logs.

Each invocation is bracketed by invariant-culture records with the fixed
`smoke-compare v=1` schema:

```text
smoke-compare v=1 event=start rid=win-x64 scenario=static order=portable-to-rid renderer=portable wall-ms=na exit=na
smoke-compare v=1 event=end rid=win-x64 scenario=static order=portable-to-rid renderer=portable wall-ms=1234.567 exit=0
```

Fields identify the native RID, static/scripted scenario, direction, renderer
variant, CLI wall milliseconds, and exit status; start records use `na` until
the invocation completes. `wall-ms` wraps the actual CLI invocation and is a
separate wall-clock observation, not a sum or subtraction of nested
`smoke-timing` phases. The preceding ordinary RID smokes and any restored
build/runtime caches are uncontrolled. The fixed order is not a cold-cache
experiment, statistical sample, or causal estimate of deployment/startup
benefit. These records support a bounded native comparison only; native Windows
x64 and arm64 runs are still required and Linux mocks or structural checks are
not Windows evidence.

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
The enclosing `windows.copy-runtime` interval is preserved and contains three
fixed component aggregates (`hostfxr`, `shared-runtime`, `dotnet`), each with
component, `.copy`, and `.metadata` records. `.copy` sums actual `File.Copy`
calls. `.metadata` sums the named directory creation, enumeration, attribute,
and optional file-length operations. Those operation sums are disjoint, but
exclude path construction, inventory bookkeeping and other traversal or
instrumentation overhead; component total minus copy time is therefore not
metadata time. The component records also overlap their enclosing runtime-copy
record. A failed component reports attempted work and preserves the existing
copy order; later components are not attempted.
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

The smoke opt-in also requests bounded worker startup durations over stderr.
The worker verifies its AppContainer/Job profile and constructs its owned
HarfBuzz/Skia font resources before Hello; page-script execution occurs later.
Separate host Hello receive/validation and worker preflight/verification/
renderer-construction/Hello-write phases help locate delays without calling
all Hello wait time IPC latency. Pre-managed runtime startup is not measured,
and durations from the worker and host cannot be subtracted as a precise gap.
This does not change standard-stream handles, IPC framing, process creation,
confinement verification or resource limits. See the
[startup diagnostic schema](browser-shell.md#validation).

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
