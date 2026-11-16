# Platform.Windows.Sandbox

Windows x64/arm64 per-renderer confinement uses an AppContainer token with no
capabilities and a kill-on-close Job Object with 512 MiB per-process and total
commit bounds, a one-process limit and a one-CPU hard cap. The launcher stages
only the trusted renderer, its active .NET runtime and the explicitly selected
regular font and any explicitly configured bold/italic/bold-italic files into
a private read-only AppContainer-readable directory. No font-source directory
access is granted. Writes to
renderer temp and profile storage are denied. The child receives only private
stdin/stdout/stderr pipes and a minimal environment. Startup fails closed if
required APIs or resource controls are unavailable.

Optional smoke diagnostics time staging and disposal phases. With diagnostics
enabled, the existing copy traversal aggregates successfully copied app/runtime
file counts and bytes, partitions app files by non-RID/active-RID/other-RID,
and reports `.pdb` files as an overlapping subset. Runtime totals include
host/fxr, the selected shared runtime and `dotnet.exe`; other RID assets are
not presumed unused. Reparse-point skips and copy inclusion remain unchanged.
No extra length metadata is read when diagnostics are disabled. Exports contain
only aggregate counts, never paths or file contents.

Windows Job Objects have no thread-count or per-job swap limit; commit limits
do not disable pagefile use. This profile does not claim quotas for unrelated
Windows storage or a general filesystem sandbox.

This is a development renderer boundary, not a production browser guarantee.
The profile confirmation is produced by the trusted worker binary; it is not
remote attestation. The browser remains privileged and unconfined.

See the [Windows confinement guide](../../../docs/windows-confinement.md).
