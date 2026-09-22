# Workspace Control — Agent Instructions

## Product identity
- Product: **Workspace Control**.
- Repository: `workspace-bootstrap` until a deliberate repository rename.
- Target: **Windows 11 x64**.
- This is a permanent Windows control center, not a one-time bootstrapper.
- Core domains: software, Windows configuration, diagnostics, cleanup, optimization, drivers, WSL, profiles, cache/offline workflows and future fleet configuration.
- Local operation must remain useful without an account or cloud service.

## Non-negotiable product principles
1. User control first: normal interactive mode never mutates silently.
2. Explain before changing: show what, why, scope, risk, reversibility and restart impact.
3. Prefer root-cause fixes over workarounds, suppression or hacks.
4. Local-first: core local workflows work offline where their providers/data permit.
5. Capability-driven: desktop, CLI and future API/cloud consume the same capabilities.
6. Provider-neutral: WinGet is a provider, never the architecture.
7. Evidence-based inventory: unknown software is still inventory; never guess.
8. Risk-aware mutation: reversible, risky and irreversible actions are explicitly classified.
9. Least privilege by default; a user may explicitly launch an elevated session as a convenience.
10. Automation is explicit and auditable.
11. Windows 11 is the only supported target unless product direction explicitly changes.
12. Open-source local client first; future paid services remain optional.

## Target technology
- C# / .NET 10.
- WinUI 3 / Windows App SDK for the desktop.
- .NET CLI sharing the same application/domain engine.
- No hybrid PowerShell architecture. PowerShell may be invoked as an external Windows mechanism when appropriate, but business logic remains C#.
- C++/Rust only for a demonstrated native requirement that .NET cannot satisfy cleanly.

## Target architecture
```text
Workspace Control
├── Desktop (WinUI 3)
├── CLI
├── Core / Domain
├── Application / Use cases
├── Engine
├── Windows integration
├── Software providers
├── Drivers
├── WSL
├── Optimization / Policies
├── Diagnostics
├── Profiles / Desired state
├── Cache / Offline
├── Security / Privileged operations
└── Plugins
```

Presentation, application orchestration, domain rules and OS/provider adapters stay separated.

## UX rules
- Modern Windows 11 Fluent-inspired experience.
- System / Light / Dark themes.
- Semantic colors: green success, blue information/primary action, amber warning, orange high impact, red error/danger, purple advanced/automation.
- Never use color alone to communicate state.
- Simple, Advanced and Expert are presentation levels over the same engine.
- Technical details are available through progressive disclosure.
- Destructive or irreversible actions require contextual confirmation.
- Interactive installers are first-class: launch vendor UI, wait, rescan and verify postconditions.

## Mutation contract
Every mutation should have a stable operation id, capability, target, evidence, desired state, plan, risk, reversibility, confirmation requirement, elevation requirement, progress, logs and result/recovery state.

## Profiles and future fleet
Profiles are declarative desired-state documents. They may contain applications, Windows policies/settings, registry-backed settings, drivers, WSL, optimizations and machine conditions. The model must support future machine groups, per-machine overrides and cloud synchronization without a second execution engine.

## Security
- Verify downloaded artifacts with SHA-256 and Authenticode where applicable.
- Never store secrets in ordinary profiles or logs.
- Keep privileged execution narrow, structured and auditable.
- Never present an undocumented registry tweak as an official Windows policy.

## Testing and CI
- Unit, provider-contract, Windows integration, provisioning, cache/offline, CLI, published-artifact, UI/accessibility and security tests.
- Fast validation on PRs; heavy Windows/E2E suites on main, scheduled/manual workflows and releases as appropriate.
- GitHub-hosted Windows runners only. **Self-hosted runners are not part of this project.**
- Never claim CI green without checking the exact commit.

## Git workflow
- GitHub is the source of truth.
- Use feature branches and PRs.
- The tech lead may merge a coherent PR autonomously once exact required CI is green and the change is safe.

## Execution-first
When the user says `continue`, `vas-y`, `feu vert`, `carte blanche` or equivalent, execute repository work autonomously. Ask only when a decision materially affects product direction, safety, data loss, legal/security boundaries or cannot safely be inferred.