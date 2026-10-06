# Tests

Use one xUnit v3 test project per implemented source project:
`tests/<source-project>.Tests/<source-project>.Tests.csproj`.
The [cache tests](Tools.SpecCache.Tests/) are the phase-1 example.
The [shared platform suite](Platform.Tests/) tests portable contracts, backend
selection, process services and OS paths without initializing native displays.
It groups both OS backends to avoid duplicating contract/fake tests.
The [core suite](Core.Tests/) groups URL, encoding and MIME utilities with
[pinned official fixtures](Core.Tests/Data/README.md) and offline regression tests.
The [networking suite](Engine.Net.Tests/) uses fake transports and
[pinned Fetch data URL fixtures](Engine.Net.Tests/Data/README.md). Its integration
tests bind only owned, ephemeral loopback ports and never contact remote hosts.
The [DOM suite](Engine.Dom.Tests/) checks tree mutation invariants and the
[HTML suite](Engine.Html.Tests/) runs [pinned html5lib data](Engine.Html.Tests/Data/README.md)
and static parser/limit regressions. No HTML test invokes scripts or fetches resources.
The [CSS suite](Engine.Css.Tests/) tests syntax, selectors, cascade/computed
styles, exact limits and all [pinned WPT An+B cases](Engine.Css.Tests/Data/README.md).
CSS tests neither run JavaScript nor fetch/import resources.
The [text suite](Engine.Text.Tests/) uses a [pinned OFL font](Engine.Text.Tests/Data/README.md)
for native HarfBuzz metrics, ligatures, clustering and ownership/GC tests.
The [layout suite](Engine.Layout.Tests/) combines exact deterministic geometry
with native text integration. No text/layout test initializes a display.
The [paint suite](Engine.Paint.Tests/) checks independent exact pixels,
commands, native glyph rendering, font resource ownership and portable fake
presentation. The [content suite](Engine.Content.Tests/) checks the offline
HTML/CSS-to-frame pipeline. Both link the existing font fixture and use CPU
Skia without displays, scripts or resource fetching.
The [browser suite](VisualWeb.Browser.Tests/) checks window/tab/history models,
transactional loading, MIME/encoding and embedded styles, address/shortcut
events and native CPU chrome/page composition with fake window surfaces.
It also checks asynchronous render publication and launches real per-tab
workers for exact pixels, PID reuse, crash containment/reload, deadlines and
cancellation. Its build copies the actual renderer and private
[Renderer.TestPeer](Renderer.TestPeer/) fault executable with their runtime
files; the latter simulates malformed replies and hangs, never remote traffic.
Phase-11d cases cover opt-in inline source classification/preflight and exact
budgets, title/style mutations reaching pixels, fresh navigation contexts,
committed DOM retention on resize/failed candidates, transactional script errors,
lost-worker reload requirements and actual confined V8 deadline recovery.
The [IPC suite](Ipc.Tests/) verifies exact wire shapes, fragmentation, byte
budgets, strict JSON, opacity, truncation and cancellation, plus v3 document/
publication/script policy fields and rejection of previous protocol versions.
The [scripting suite](Engine.Scripting.Tests/) runs native V8 primitive/type
copying, intrinsic-tampering, isolate separation, source/result/stack/buffer
limits, thread ownership and deadline/cancellation tests.
Classic-script tests additionally verify global lexical/strict/TDZ semantics,
bounded batch snapshots/order/failures and whole-batch invalidation.
Live-binding tests cover title/text/ID semantics, immediate native updates,
detached identity/adoption, exclusive document leases, private-callback
invisibility, intrinsic tampering and exact handle/call/traversal/text budgets.
Phase-11e tests cover shared Node identities, detached fragments/move semantics,
required arguments/receiver brands/conversion errors, native adoption rejection,
atomic hierarchy/attribute/tree failures and exact creation/attribute/subtree
budgets. Browser cases verify inserted nodes/inline attributes/styles reach
exact pixels in local and worker modes, while new script nodes remain inert
and removed initial scripts retain their preflight snapshot.
Phase-11f tests verify native Promise/queueMicrotask FIFO ordering, per-script
checkpoints, exact pending/recursive task limits, callback arguments/returns,
captured-intrinsic tampering, failure latching and native callback interruption.
Local/worker pages verify checkpoint title/style pixels, retained resize and
failed-task preservation/recovery.
Phase-11g tests cover branded synthetic Event/EventTarget, DOM propagation paths,
mutation/removal/addition/once ordering, passive/cancellation controls, callback
object getters, exact listener/invocation/path/depth limits, task failure latching,
intrinsic tampering, adoption and cancellation. Local/process page fixtures
verify event/listener-microtask mutations reach pixels and failed candidates
preserve committed DOM.
Phase-11h tests verify finite readiness ordering and event flags, readonly state,
private dispatch/trust despite tampering, checkpoints before subsequent stages,
one-shot/prevalidation/cancellation semantics and shared invocation/microtask
quotas/deadlines. Local/process pages verify lifecycle mutations reach paint
and failures preserve committed DOM while fresh navigation recovers.
Phase-11i tests cover tree-order/scope/deduplication, static NodeList indices/
callbacks/iteration, live identity after mutation, detached/adopted nodes,
syntax/unsupported/receiver failures, intrinsic tampering and exact result/
traversal/selector bounds. CSS tests verify shared matching/candidate budgets;
local/process lifecycle query fixtures verify pixels and transactional failure.
The explicit
[V8Smoke](../tools/V8Smoke/) probe additionally measures monitored heap failure
and native startup under unchanged Linux or Windows renderer confinement.
Native SDL browser smoke is explicit and separate from ordinary tests.
Linux x64 browser integration also runs required-confinement workers, so it
requires bubblewrap/libseccomp, unprivileged namespaces and a systemd user manager
with delegated cgroup v2 memory/pids/CPU controllers. Other targets assert
unsupported fail-closed behavior. The explicit
[LinuxSandboxSmoke](../tools/LinuxSandboxSmoke/) separately measures actual
filesystem/network/process/namespace denials; it is not a conformance-data fetch.
Its `--resources` mode additionally verifies kernel-accounted native OOM, task
denial and CPU throttling within bounded owned scopes. Platform tests reject
wrong/max kernel settings and unavailable manager launch without fallback.
Phase-10d browser tests also drive those same resource probes over real confined
IPC: native OOM failure/restart and transactional history, task-denial/channel
reuse, CPU-pressure cancellation/deadlines, queued cancellation and tab-close
cleanup with unaffected actual-renderer tabs. The peer alone has a trusted
OOM-evidence supervisor; production renderers have no fault-injection hooks.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
    <ProjectReference Include="../../src/Area/Project/Project.csproj" />
  </ItemGroup>
</Project>
```

Versions belong in the root central package file. Add each new test project to
`VisualWeb.slnx`. Pass `TestContext.Current.CancellationToken` to asynchronous
operations and inject clocks/HTTP handlers rather than depending on real time
or network services.

```sh
dotnet test tests/Tools.SpecCache.Tests/Tools.SpecCache.Tests.csproj
dotnet test tests/Platform.Tests/Platform.Tests.csproj
dotnet test tests/Core.Tests/Core.Tests.csproj
dotnet test tests/Engine.Net.Tests/Engine.Net.Tests.csproj
dotnet test tests/Engine.Dom.Tests/Engine.Dom.Tests.csproj
dotnet test tests/Engine.Html.Tests/Engine.Html.Tests.csproj
dotnet test tests/Engine.Css.Tests/Engine.Css.Tests.csproj
dotnet test tests/Engine.Text.Tests/Engine.Text.Tests.csproj
dotnet test tests/Engine.Layout.Tests/Engine.Layout.Tests.csproj
dotnet test tests/Engine.Paint.Tests/Engine.Paint.Tests.csproj
dotnet test tests/Engine.Content.Tests/Engine.Content.Tests.csproj
dotnet test tests/VisualWeb.Browser.Tests/VisualWeb.Browser.Tests.csproj
dotnet test tests/Ipc.Tests/Ipc.Tests.csproj
dotnet build VisualWeb.slnx
dotnet test VisualWeb.slnx --no-build
```

Building the solution is the all-project build smoke check, not a recursive
unit test that starts another build. Empty source projects do not have dummy
test suites; add meaningful tests as their implementations are introduced.

Tests do not read/download live standards. Future WPT/html5lib fixtures must
be versioned or otherwise reproducibly provisioned with upstream license and
provenance. Standards documentation has its own [cache workflow](../docs/standards.md).
