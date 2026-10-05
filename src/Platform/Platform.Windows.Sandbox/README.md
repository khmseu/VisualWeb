# Platform.Windows.Sandbox

Windows x64/arm64 per-renderer confinement uses an AppContainer token with no
capabilities and a kill-on-close Job Object with 512 MiB per-process and total
commit bounds, a one-process limit and a one-CPU hard cap. The launcher stages
only the trusted renderer, its active .NET runtime and the explicitly selected
font into a private read-only AppContainer-readable directory. Writes to
renderer temp and profile storage are denied. The child receives only private
stdin/stdout/stderr pipes and a minimal environment. Startup fails closed if
required APIs or resource controls are unavailable.

Windows Job Objects have no thread-count or per-job swap limit; commit limits
do not disable pagefile use. This profile does not claim quotas for unrelated
Windows storage or a general filesystem sandbox.

This is a development renderer boundary, not a production browser guarantee.
The profile confirmation is produced by the trusted worker binary; it is not
remote attestation. The browser remains privileged and unconfined.

See the [Windows confinement guide](../../../docs/windows-confinement.md).
