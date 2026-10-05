# LinuxSandboxSmoke

Explicit Linux x64 OS-confinement probe using the same launcher and filter as
the renderer. Requires bubblewrap, libseccomp, unprivileged namespaces and a
systemd user manager with delegated cgroup v2 memory/pids/CPU controllers.
No remote hosts or native display. Creates only a unique owned host canary,
checks its denial inside the sandbox and deletes it afterwards.

```sh
dotnet run --project tools/LinuxSandboxSmoke -- \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
dotnet run --project tools/LinuxSandboxSmoke -- \
  --resources --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

Requires hidden host file/environment/processes, read-only mounts, denied
socket/process/namespace/privilege syscalls and usable private tmpfs. Failure
is nonzero; there is no success fallback. A 30-second parent deadline
terminates only the owned scope/child tree. The font is trusted configuration.
Resource mode uses bounded scopes to measure thread denial (`pids.events`),
CPU throttling (`cpu.stat`) and native resident-allocation OOM
(`memory.events oom_kill`), not merely managed allocation failure or property
names. A trusted supervisor survives to read the kernel OOM counter.
The internal ResourceExhaustion helper is source-linked into the private
Renderer.TestPeer fixture so browser lifecycle tests exercise these same probes
without introducing test hooks or dependencies into the production renderer.

See the [confinement guide](../../docs/linux-confinement.md) for exact limits
and unfinished guarantees. These probes do not certify production security.
