# Platform.Windows.Sandbox

Windows x64/arm64 per-renderer confinement using an AppContainer token with no
capabilities and a Job Object that limits each worker to 512 MiB and one
process, with kill-on-close ownership. The launcher stages only the trusted
renderer, its active .NET runtime and the explicitly selected font into a
private read-only AppContainer-readable directory; a private temporary
directory is writable. The child receives only private stdin/stdout/stderr
pipes and a minimal environment. Startup fails closed if required APIs or
resource controls are unavailable.

This is a development renderer boundary, not a production browser guarantee.
The profile confirmation is produced by the trusted worker binary; it is not
remote attestation. The browser remains privileged and unconfined.

See the [Windows confinement guide](../../../docs/windows-confinement.md).
