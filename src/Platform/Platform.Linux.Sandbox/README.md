# Platform.Linux.Sandbox

Dedicated Linux x64 renderer confinement bootstrap: required bubblewrap
namespaces, explicit read-only mounts, environment clearing, libseccomp default-
deny syscall policy, inherited resource limits and required cgroup v2 memory/
swap/task/CPU accounting in unique transient systemd user scopes. No window backend or
engine dependency. Entry/verification failure is explicit; never fall back
to an unconfined worker. Windows/arm64 fail closed.

The trusted bootstrap reexecutes before receiving page content. The worker
profile confirms configuration, not hostile-code attestation. The generic
SystemProcessLauncher still rejects sandbox requests because it lacks this
renderer-specific mount/runtime contract.
LinuxRendererResources verifies the actual kernel settings before the worker
handshake and stops only its generated unit on shutdown. Missing controller/
user-manager support fails closed. No global systemd/kernel changes are made.

See the [Linux confinement guide](../../../docs/linux-confinement.md),
[policy tests](../../../tests/Platform.Tests/LinuxSandboxTests.cs) and explicit
[native denial probe](../../../tools/LinuxSandboxSmoke/).
