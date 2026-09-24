# Workspace Control — Agent Instructions

## Product identity
- Product: **Workspace Control**.
- Repository: `workspace-control`.
- Target: **Windows 11 x64**.
- This is a permanent Windows control center, not a one-time bootstrapper.
- Core domains: software, Windows configuration, diagnostics, cleanup, optimization, drivers, WSL, profiles/workspaces, cache/offline workflows and future fleet configuration.
- Local operation must remain useful without an account or cloud service.
- The canonical user-facing product/UX contract is `docs/PRODUCT-EXPERIENCE.md`.

## Product model — do not regress
- **Workspace** is the primary user-facing desired-state concept.
- **ProfileManifest** is the technical serialized/domain representation of a Workspace.
- The user creates a Workspace by choosing and checking/unchecking the capabilities they want managed. A predefined profile must never be required.
- Product-supplied profiles, when present, are optional templates that can be previewed and edited; they are not the primary UX.
- First-run experience observes the real machine before asking the user to decide what to manage.
- Applications are a first-class catalogue and management surface. A small curated catalogue is not a product limit.
- Unknown, unavailable, blocked and error states must remain distinct and explained.
- The user-facing flow is: observe machine → choose desired state → compare → review → explicitly confirm → apply the persisted plan → verify.
- Engine or infrastructure work must not silently replace this product model with an implementation-centric workflow.

## Non-negotiable product principles
1. User control first: normal interactive mode never mutates silently.
2. Explain before changing: show what, why, scope, risk, reversibility and restart impact.
3. Prefer root-cause fixes over workarounds, suppression or hacks.
4. Online-first installation: reuse a verified local installer when it already matches the requested current version; do not design a cache-only/offline provisioning mode.
5. Capability-driven: desktop, CLI and future API/cloud consume the same capabilities.
6. Provider-neutral: WinGet is a provider, never the architecture.
7. Evidence-based inventory: unknown software is still inventory; never guess.
8. Risk-aware mutation: reversible, risky and irreversible actions are explicitly classified.
9. Least privilege by default; a user may explicitly launch an elevated session as a convenience.
10. Automation is explicit and auditable.
11. Provisioning plans are immutable after confirmation; Apply executes the persisted plan and never silently replans.
12. Windows 11 is the only supported target unless product direction explicitly changes.
13. Open-source local client first; future paid services remain optional.

## Documentation is a product contract

Markdown documentation is not disposable implementation commentary. Product and UX documents preserve decisions made through product analysis, brainstorming and design work.

### Authority hierarchy
When documents disagree, resolve them in this order:

1. `docs/PRODUCT-EXPERIENCE.md` — canonical user-facing product and UX contract.
2. `docs/PRODUCT-VISION.md` — mission, scope, audience and product boundaries.
3. `docs/UX-DESIGN.md` — detailed design system and interaction rules.
4. `docs/PROFILES.md`, `docs/SOFTWARE.md`, `docs/INVENTORY.md`, `docs/PROVISIONING.md`, `docs/CONFIGURATION.md`, `docs/PORTABILITY.md` — domain and cross-cutting contracts.
5. `docs/ARCHITECTURE.md` — technical architecture implementing the product contracts.
6. `docs/DECISIONS.md` — reconstructed decision record and anti-drift ledger.
7. `docs/ROADMAP.md` and `docs/STATUS.md` — implementation state and sequencing.

### Anti-drift rules
- Never wholesale-replace product/UX Markdown with a summary derived from the latest engine work.
- A backend, infrastructure, provider, CI or build change may update technical details, but it must preserve existing product decisions unless the PR explicitly changes product direction.
- Before editing an existing Markdown file, read the current file and preserve unrelated decisions.
- Prefer surgical edits and additions over regeneration.
- If implementation reality conflicts with the product contract, document the implementation gap; do not silently redefine the product to match the gap.
- If a product decision genuinely changes, update the canonical product contract and every affected dependent document in the same coherent documentation change.
- Any PR that materially changes user experience, product scope, terminology, desired-state semantics or safety rules must include a documentation impact audit.
- Do not delete or weaken product decisions merely because the current UI does not implement them yet.
- Do not mark a capability complete merely because its engine foundation exists; distinguish foundation, user experience and end-to-end completion.
- Roadmap/status documents must not be used as a reason to overwrite canonical product/UX documents.
- Documentation changes must be checked for contradictions across all affected Markdown files before merge.

## Context reconstruction and decision audit — non-negotiable

Conversation context is not the source of truth for project decisions.

When a task depends on an earlier product or architecture discussion:
1. inspect the current `AGENTS.md` and applicable Markdown contracts;
2. search merged PRs, commit history and repository documentation for the decision;
3. distinguish verified repository evidence from assumptions or unavailable conversation history;
4. if a decision cannot be verified, do not invent it; record the uncertainty in the decision audit and use the safest interpretation consistent with the existing product contract;
5. when an implementation exposes a contradiction with an established decision, correct the implementation and update the relevant documentation in the same coherent unit of work.

Important decisions must be durable in repository documentation. A future agent must not need to remember a private conversation to avoid regressing the product.

Use `docs/DECISIONS.md` for reconstructed decision history and `docs/PORTABILITY.md` for the portable-storage invariant.
## Portability and application-owned storage — non-negotiable

Workspace Control is a **portable, self-contained, unpackaged Windows application**.

Application-owned mutable data must stay under the explicit portable application root. The default root is `AppContext.BaseDirectory`; tests and controlled composition may inject an explicit root.

This applies to:
- user Workspaces;
- application configuration and preferences;
- installer cache and metadata;
- staging data;
- operation state and recovery journals;
- optimization state;
- application-owned logs and diagnostics;
- future application-owned mutable state.

**Never silently introduce** `%LOCALAPPDATA%`, `%APPDATA%`, `ApplicationData.Current`, another implicit per-user store, or Registry-backed application configuration/state.

Windows Registry/services/tasks/etc. remain valid when they are the **system being managed**, not as hidden Workspace Control storage. User-selected import/export destinations are explicit user choices and are not hidden persistence.

The application must not silently fall back to AppData when the portable root is not writable. A different deployment/storage model requires an explicit product/architecture decision and documentation impact audit.

Before changing persistence, read and obey `docs/PORTABILITY.md` and `docs/DECISIONS.md`. Treat this as a product/architecture invariant, not an implementation preference.
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
├── Infrastructure / Windows integration
├── Software providers
├── Drivers
├── WSL
├── Optimization / Policies
├── Diagnostics
├── Profiles / Desired state
├── Provisioning plan / confirmation lifecycle
├── Installer artifact reuse
├── Security / Privileged operations
└── Plugins
```

Presentation depends on Application contracts. Application owns use-case orchestration and depends only on Domain. Infrastructure implements Application-facing adapters and composes Windows/provider integrations. Domain remains independent of Windows and provider implementations.

## UX rules
- Modern Windows 11 Fluent-inspired experience.
- System / Light / Dark themes.
- Semantic colors: green success, blue information/primary action, amber warning, orange high impact, red error/danger, purple advanced/automation.
- Never use color alone to communicate state.
- Simple, Advanced and Expert are presentation levels over the same engine.
- Technical details are available through progressive disclosure.
- The primary desired-state UX is user-built **My Workspace**, not mandatory predefined profiles.
- Destructive or irreversible actions require contextual confirmation.
- Interactive installers are first-class: launch vendor UI, wait, rescan and verify postconditions.
- See `docs/PRODUCT-EXPERIENCE.md` for the complete UX contract.

## Mutation contract
Every mutation should have a stable operation id, capability, target, evidence, desired state, plan, risk, reversibility, confirmation requirement, elevation requirement, progress, logs and result/recovery state.

## Profiles and future fleet
Profiles are declarative desired-state documents. In the user-facing product they are presented as Workspaces; `ProfileManifest` remains the technical contract. They may contain applications, Windows policies/settings, registry-backed settings, drivers, WSL, optimizations and machine conditions. The model must support future machine groups, per-machine overrides and cloud synchronization without a second execution engine. A provisioning operation persists its exact plan, requires explicit confirmation, verifies preconditions before uncompleted mutations, and enters a terminal stale state when observed state diverges.

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
