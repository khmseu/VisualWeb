# Phase 1 Complete: Add Linux Development Workflow

Added a GitHub Actions Linux workflow that installs Bubblewrap, builds upstream v0.9.0 when the runner package is older, and verifies the executable before .NET build and test steps. The workflow runs on pushes, pull requests, and manual dispatches; browser cases requiring a systemd user manager and delegated cgroup controllers are excluded.

## Files created/changed

- `.github/workflows/linux-development.yml`
- `plans/github-linux-bubblewrap-plan.md`

## Functions created/changed

- None

## Tests/checks

- `dotnet build VisualWeb.slnx --configuration Release` passed.
- `Platform.Tests`: 24 passed.
- Filtered `VisualWeb.Browser.Tests`: 66 passed, 0 failed.
- Workflow YAML parsing, embedded shell syntax, and whitespace checks passed.
- Confinement smoke was not run; GitHub-hosted systemd/cgroup capability is not established.

## Review Status

APPROVED

## Git Commit Message

ci: install bubblewrap in Linux Actions

Install bubblewrap from apt and build upstream v0.9.0 when the runner package is below the sandbox requirement. Verify `/usr/bin/bwrap` before the .NET build and tests. Exclude browser cases that require systemd user scopes or delegated cgroup controllers.
