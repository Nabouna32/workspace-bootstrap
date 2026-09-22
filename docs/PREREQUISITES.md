# Prerequisites and dependency resolution

Provisioning is dependency-aware and fail-fast.

## Two levels

### Host prerequisites

Before provisioning, the engine verifies:

- elevated administrator session;
- Windows 11 build 22000 or newer;
- WinGet.

If WinGet is missing, the engine attempts to re-register Microsoft App Installer before stopping.

Host prerequisites that cannot be repaired automatically are reported as a hard block. The engine never continues into an installer with an invalid host baseline.

### Component dependencies

Each component declares dependencies.

Example:

- github-cli depends on git;
- windows-sdk depends on visual-studio.

The resolver expands dependencies and produces a topological execution plan. A dependency is installed and verified before its dependent component is started.

If a dependency fails, is blocked, or requires a reboot, downstream components do not run.

## Component prerequisites

Components can additionally declare lightweight prerequisites such as:

- administrator privileges;
- Windows build;
- required command;
- another component in the current plan;
- required file.

These are checks, not hidden installation side effects.

## Compliance checks

Components may declare checks used by -Mode check.

Current Windows checks cover command presence, required files, WSL Ubuntu presence, and Visual Studio component/compiler compliance.

The intended statuses are:

- OK
- MISSING
- OUTDATED
- BLOCKED
- FAILED
- REBOOT_REQUIRED

check never installs anything. install and repair perform the declared provisioning steps after prerequisites pass.

## Why this is explicit

A missing prerequisite must be visible in the run log. We do not silently swallow it, skip an installer, or continue into unrelated components and bury the root cause in later errors.
