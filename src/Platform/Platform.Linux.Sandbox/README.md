# Platform.Linux.Sandbox

Dedicated native Linux x64/ARM64 renderer confinement bootstrap: required bubblewrap
namespaces, explicit read-only mounts, environment clearing, libseccomp default-
deny syscall policy, inherited resource limits and required cgroup v2 memory/
swap/task/CPU accounting in unique transient systemd user scopes. No window backend or
engine dependency. Entry/verification failure is explicit; never fall back
to an unconfined worker. Other OS/architecture combinations fail closed.
Both supported ABIs use the same namespace/mount/resource policy. The x64
syscall rules are unchanged; ARM64 excludes only explicitly listed legacy
entry points absent from its ABI. Unresolved required rules fail filter export.

The trusted bootstrap reexecutes before receiving page content. The worker
profile confirms configuration, not hostile-code attestation. The generic
SystemProcessLauncher still rejects sandbox requests because it lacks this
renderer-specific mount/runtime contract.
LinuxRendererResources verifies the actual kernel settings before the worker
handshake and stops only its generated unit on shutdown. Missing controller/
user-manager support fails closed. No global systemd/kernel changes are made.

Cross-generated x64/ARM64 BPF policy tests are not native ARM64 validation.
The Linux workflow requires native hosted runners and runs denial/resource
smokes and confined browser tests on both architectures. Native ARM64 results
have not been observed here; runtime/JIT/native-library and kernel enforcement
evidence remains required before calling ARM64 support fully validated.

See the [Linux confinement guide](../../../docs/linux-confinement.md),
[policy tests](../../../tests/Platform.Tests/LinuxSandboxTests.cs) and explicit
[native denial probe](../../../tools/LinuxSandboxSmoke/).
