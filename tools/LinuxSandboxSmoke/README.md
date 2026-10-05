# LinuxSandboxSmoke

Explicit Linux x64 OS-confinement probe using the same launcher and filter as
the renderer. Requires bubblewrap, libseccomp and unprivileged namespaces.
No remote hosts or native display. Creates only a unique owned host canary,
checks its denial inside the sandbox and deletes it afterwards.

```sh
dotnet run --project tools/LinuxSandboxSmoke -- \
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

Requires hidden host file/environment/processes, read-only mounts, denied
socket/process/namespace/privilege syscalls and usable private tmpfs. Failure
is nonzero; there is no success fallback. A 30-second parent deadline
terminates only the owned child tree. The font is trusted configuration.

See the [confinement guide](../../docs/linux-confinement.md) for exact limits
and unfinished guarantees. These probes do not certify production security.
