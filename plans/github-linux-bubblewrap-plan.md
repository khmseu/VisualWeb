# Plan: GitHub Linux Bubblewrap Setup

Add a GitHub-hosted Linux development workflow that installs bubblewrap and verifies the required executable before running the solution build and focused tests. Keep confinement smoke tests out of this workflow because GitHub-hosted runner support for the required systemd user manager and delegated cgroup controllers is not established.

## Phases

1. **Phase 1: Add Linux development workflow**
    - **Objective:** Ensure Linux GitHub Actions installs bubblewrap and checks the documented `/usr/bin/bwrap` path and minimum version.
    - **Files/Functions to Modify/Create:** `.github/workflows/linux-development.yml`
    - **Tests to Write:** Workflow package preflight step checks executable path and version before build/test steps.
    - **Steps:** Add an Ubuntu GitHub Actions workflow for push, pull request, and manual dispatch; install bubblewrap; validate the executable path/version; set up .NET 10; build and run focused Linux-compatible tests.
2. **Phase 2: Validate workflow behavior**
    - **Objective:** Confirm the workflow configuration and its relevant .NET build/test commands are valid in the local environment where feasible.
    - **Files/Functions to Modify/Create:** No additional files unless validation exposes a workflow defect.
    - **Tests to Write:** None; run focused configuration/build/test checks.
    - **Steps:** Validate YAML and shell syntax; execute the targeted build and test suites where available; report environment-dependent checks that cannot be reproduced locally.
3. **Phase 3: Review and close out**
    - **Objective:** Review the workflow scope and document completion.
    - **Files/Functions to Modify/Create:** `plans/github-linux-bubblewrap-phase-1-complete.md`, `plans/github-linux-bubblewrap-complete.md`
    - **Tests to Write:** None.
    - **Steps:** Review dependency installation and version checks; summarize files, test results and any hosted-runner limitations.

## Open Questions

1. None; this targets GitHub Actions, not Codespaces or local Linux setup.
