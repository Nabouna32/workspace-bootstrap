# Toolchain version policy

The repository defines compatibility policy, not arbitrary frozen versions.

## Rules

1. Prefer the latest stable release supported by the project.
2. Use LTS releases for foundational runtimes when an LTS line exists.
3. Pin an exact version only when reproducibility or compatibility requires it.
4. Preview/nightly releases require an explicit reason and must not silently become the default.
5. Compatibility constraints belong in the repository, close to the affected stack.
6. Installation scripts must be idempotent: rerunning them should converge on the desired state.
7. Verification must report the effective installed versions rather than assuming them.

## Current baseline policy

| Tool | Policy |
|---|---|
| Windows | Windows 11 Pro, current supported stable |
| PowerShell | Latest supported LTS |
| Git for Windows | Latest stable |
| GitHub CLI | Latest stable |
| VS Code | Latest stable |
| MSVC / Build Tools | Latest stable supported by the Windows/Flutter toolchain |
| WSL | Latest stable Microsoft Store/runtime release |
| Ubuntu | Current Ubuntu LTS supported by WSL and our toolchain |
| Node.js | LTS compatible with the application stack |
| Flutter | Latest stable compatible with the application stack |
| Java | LTS compatible with Flutter/Android |
| Android SDK | Stable API required by the application stack |
| Docker Engine | Latest stable |
| Playwright | Latest stable compatible with the application stack |

## Why we do not freeze everything

Freezing every tool version would make a fresh workstation reproducible only for a short period and would turn routine security and maintenance updates into manual migrations.

Instead, the environment records compatibility constraints and intentional pins. When a tool must be pinned, the reason and upgrade path should be documented.

## Intentional pins

Exact versions currently required by known application compatibility should be recorded here as they are established.

At the time of this bootstrap, no application-specific exact version pin is declared here beyond the compatibility constraints already documented in the project repositories.
