# WindowsSandboxSmoke

Explicit native Windows x64/arm64 confinement probe. It launches this trusted
tool as a capability-free AppContainer worker with the production Job Object
limits and checks denial of a host canary, inherited environment, child-process
creation, loopback networking, and writes to staged application, temporary, and
AppContainer profile storage. Failure is nonzero; there is no fallback.

```powershell
dotnet run --project tools/WindowsSandboxSmoke -- `
  --font tests/Engine.Text.Tests/Data/NotoSans.ttf
```

The worker also verifies its actual Job Object process/memory/CPU settings
before probing. Windows does not expose a Job Object thread-count limit,
per-job pagefile/swap ceiling, or per-directory disk quota; this profile denies
renderer writes to disk-backed temporary and profile storage instead of claiming
those unsupported quotas. These probes validate this exact development profile,
not production security or all Windows policy boundaries.
