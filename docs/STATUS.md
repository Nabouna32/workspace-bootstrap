# Environment Status

Last updated: 2026-09-22

## Latest architecture decisions

### 2026-09-22 — Public CI and local-build strategy

- Personal development repositories are intended to move to the personal GitHub account and become public where appropriate.
- Public repositories use standard GitHub-hosted runners. GitHub currently documents those runners as free and unlimited for public repositories.
- Full useful validation should be restored where appropriate: analysis, unit/integration tests, E2E, Android builds/tests and Windows builds/tests should not be artificially reduced merely to save hosted-runner minutes.
- The personal PC remains the local development and build environment; hosted CI is authoritative for repository validation.
- Windows is the primary local development/build environment. WSL is complementary and mainly Linux-first.
- Local builds remain important because downloading a large APK/AAB/EXE artifact for every iteration consumes the user's limited weekday bandwidth. CI validates remotely; local builds provide fast, bandwidth-efficient iteration when desired.
- Ordinary validation should avoid unnecessary artifacts. Releases/manual build workflows may publish artifacts when the user actually needs the binaries.


- Product identity is **Dev Environment** with canonical technical identity **DevEnvironment**.
- `%LOCALAPPDATA%\\DevEnvironment` is the target per-user data root; historical names require deliberate migration.
- C#/.NET is the strategic application and execution language. C++/Rust are isolated options only for concrete native requirements.
- PowerShell is now explicitly treated as a transitional compatibility/execution backend, not the long-term application core.
- PR #35 (persistent execution context) merged after exact-head Repository validation passed. Merge commit: `223215d51c670fd440e5f0c569ad47806fe8f707`.
- PR #36 (neutral product identity and C# execution direction) merged after exact-head Repository validation passed. Merge commit: `19141fc7a14e69b975966339e32a59397a121a92`.

## Product architecture

| Area | State |
|---|---|
| Windows 11 + native toolchain | ✅ |
| WSL2 + systemd | ✅ |
| Windows local build toolchain | ✅ |
| WSL Linux-first tooling | ✅ minimal bootstrap |
| Android / Flutter local tooling | Windows-owned |
| Node / Docker / Playwright in WSL | optional Linux-project components |
| CI execution | GitHub-hosted standard runners |
| Online-first provisioning | ✅ — official vendor source first, local installer cache retained |
| Update manager | ✅ |
| Interactive optimization engine | ✅ Safe / Advanced / Aggressive |
| Optimization plan preview | ✅ — état réel vérifié |
| Guarded rollback | ✅ — drift conservé |
| Aggressive AppX debloat | 🔧 first implementation |
| Interactive Maintenance Center | ✅ |
| WSL-owned tool health checks | ✅ |
| Development Extended profile | ✅ component catalog |
| Minecraft/modding support | 🗺️ roadmap |
| Tech-lead product direction | ✅ documented |
| Persistent job architecture | 🏗️ foundation in progress |
| SQLite job store | 🏗️ first implementation |
| Capability-aware scheduler | 🏗️ reconciliation foundation |

## Important boundaries

- Windows may provision Android SDK/emulator for local builds/tests.
- Public repository CI is GitHub-hosted.
- Local build toolchains exist to avoid repeatedly downloading large CI artifacts.
- The local installer cache under C:\\DevCache is an installation/reinstallation asset, not a CI cache. Exported caches can be restored on another drive and used with explicit cache-only provisioning.
- WSL checks only tools that are actually WSL-owned/installed.

## Product direction rule

The user defines product goals, constraints and important personal preferences. The assistant is expected to act as technical lead: it may proactively recommend better architecture, tooling, security, UX and developer-workflow choices when they materially improve the product.

Routine, reversible technical decisions may be made autonomously. Changes that materially affect budget, irreversible behavior, major scope or user-facing product direction must be surfaced with their trade-offs.

## Current PR / CI state

PR #21 (fix: protect optimization drift and rollback) has been merged after the exact head completed Repository validation successfully. Merge commit: `fb8f44fece8151e7dc421ef5b9ec365f8fda5acd`.

PR #22 (feat: introduce native desktop application shell) was integrated before the structured desktop contract work.

PR #23 (feat: establish structured desktop engine contract) has now been merged after the exact head `1ec63b72638279eef0a0223db5b7e8f618404cc2` passed Repository validation, including the native WPF build. Merge commit: `c8da8a52fb23afc62629a8993114691a80802439`.

PR #24 (refactor: move desktop shell to MVVM) has now been merged after the exact head passed Repository validation, including the native WPF build. Merge commit: `ef324bdaece6880d093dd8c80faec547adf6d580`.

PR #25 (feat: expose structured provisioning plans) has now been merged after the exact head passed Repository validation, including the native WPF build. Merge commit: `edb604249de4236549ded891ddab3a933e987744`.

PR #27 (`feat/desktop-provisioning-resume`) has now been merged after the exact head passed Repository validation, including the native WPF build. Merge commit: `be388e57cc436a2cba8fccb1a9b66c0828fb0d1b`.

PR #29 (`feat: add provisioning operation history`) has now been merged after the exact head `322012fb85373ac0150ecf23cad621b485fbfbd0` passed Repository validation, including the native WPF build. Merge commit: `4af0deef1134e063670ea126bcff52325344c3c7`.

The desktop provisioning center now exposes the last 20 operation snapshots. The next step is an operation detail view and safe access to the underlying run logs.

The desktop provisioning operation now persists a resume index, detects an abandoned worker, discovers recoverable operations when the desktop starts, and can explicitly resume after failure or reboot.

## Current state after V2 foundation

The repository's CI is GitHub-hosted and independent of the local workstation.

The next thematic change is the provisioning dry-run on branch `feat/provisioning-dry-run`. It adds a read-only plan mode so a new Windows installation can be inspected before any component installer is executed.

Do not describe CI as green for any branch until the exact branch head has completed the required GitHub-hosted workflows successfully. PR #32 is currently open and its latest head has not yet produced a workflow run, so it must not be merged on the basis of static inspection alone.

## Conversation handoff

For a new ChatGPT conversation, saying reprends le projet is sufficient. The assistant should read AGENTS.md, this status, the roadmap and the relevant architecture documents, then inspect the live PR/CI state before continuing. In the same conversation, continue means continue from the current stopping point rather than restarting the project audit.

## Job orchestration milestone

The architecture now explicitly defines a persistent SQLite-backed job queue. Jobs depend on capabilities rather than historical job results, survive application close/crash/reboot, and can remain blocked until a prerequisite is verified. The current foundation includes persistent job definitions, capability snapshots, reboot/resource-aware reconciliation, resource-policy persistence, resource-lock filtering and explicit version-selection semantics. PowerShell remains the system execution engine; C# owns queue persistence and scheduling. The implementation is being introduced incrementally so the existing provisioning resume path remains safe during migration.

## Next runtime validation on the real PC

1. Run the new main menu and verify stable numbering/navigation.
2. Check all profiles and component states.
3. Open the optimization plan preview, then validate Safe/Advanced/Aggressive in a controlled session.
4. Validate rollback after a reversible optimization and verify that manual changes are not overwritten.
5. Validate Maintenance Center actions individually.
6. Validate the simplified WSL Linux tooling health screen.
7. Validate local Windows Android/Flutter builds when those toolchains are selected.
8. Validate optional Linux Node/Docker/Playwright components only when selected.
9. Verify that public GitHub-hosted workflows cover the important build/test contracts without depending on the PC being online.

## Real-PC validation finding — 7-Zip

The first real-PC Base profile run installed 7-Zip successfully through WinGet, but the post-install check incorrectly reported `MISSING` because it required the `7z` command to be discoverable through the current process PATH. The installer result was successful; the verification model was wrong for this package. PR #12 (`fix/7zip-postinstall-check`) is merged into `main`: generic `file-any` checks now expand Windows environment variables, and the 7-Zip component verifies the installed `7z.exe` under `%ProgramFiles%` or `%ProgramFiles(x86)%` instead of requiring PATH exposure.

The real-PC run also confirmed that the earlier installer-output pipeline bug is fixed: the Base profile completed all nine steps without the previous `$result.status` failure.

## Latest engineering change — read-only optimization baseline

The optimization center now includes a read-only baseline of Windows build, CPU, memory, uptime, active power plan, volumes and startup entries. It deliberately inspects startup items without disabling or deleting anything, providing a factual starting point before applying an optimization plan.

## Latest engineering change — optimization drift safety

The optimization engine now verifies the live registry/power-plan state instead of trusting persisted state alone. The preview distinguishes `DÉJÀ APPLIQUÉ`, `DÉJÀ CONFORME`, `MODIFIÉ DEPUIS L'APPLICATION` and `À APPLIQUER`. Applying an optimization never overwrites a setting that was manually changed after a previous application. Rollback keeps any drifted entry in persistent state instead of silently discarding it.

## Known next engineering targets

- complete the persistent job execution loop: atomic claims/locks, executor lifecycle, capability verification and safe retry;
- finish the desktop application around structured engine contracts rather than raw console text;
- expose provisioning profiles and dry-run state through the desktop contract;
- execute provisioning through a resumable operation state rather than blocking the UI;
- complete the MVVM shell boundary so the WPF window remains presentation-only;
- graphical provisioning with profiles, dry-run, progress, reboot/resume, operation history and clear failure states;
- structured diagnostics with component ownership and severity;
- richer optimization UX with catalog details, drift state and guarded rollback;
- strict manifest schema validation;
- Minecraft/modding toolchain support;
- project profiles for future Flutter/Next.js/Node and other repositories.



## Product direction — portable bootstrap

The long-term product model includes a portable configuration layer: export/import a versioned desired environment as JSON, preview the machine-specific delta, then generate persistent independent jobs. Local JSON comes before account/cloud synchronization. Secrets are excluded from ordinary bundles and require dedicated secure handling.


## Current execution-core direction

- PR #38 established `IProvisioningBackend` with the existing PowerShell engine behind a compatibility implementation.
- PR #39 introduces the persistent provisioning job executor and uses the durable `ExternalOperationId` to reconnect/resume external work after restart.
- The PR #39 CI initially failed only because `JobStore` declared the new state-writer interface without implementing it; the root-cause fix is on the branch and must be validated at exact head before merge.
- Next architectural target: validated executor registry + explicit durable execution/progress/recovery semantics.



### 2026-09-22 — Controlled Windows installer cache

- PR #43 introduced the durable local installer cache and replaced 7-Zip/ShareX/Everything according to the selected workstation profile.
- PR #44 refines the cache to prefer official vendor sources, verify publisher signatures for EXE/MSI installers, retain immutable versioned entries, and use WinGet only as an explicit fallback.
- Before formatting Windows, export C:\\DevCache to a non-formatted disk or external drive with -ExportCacheTo.


## Native .NET migration

PR #44 (official vendor installer sources) is merged into `main`. The PowerShell provisioning implementation is now being replaced by the native C#/.NET 10 engine on PR #46. The migration is intentionally one-way: after native parity and fresh-Windows validation, the remaining PowerShell and batch product paths will be removed.
