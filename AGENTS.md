# Workspace Control — Agent Instructions

## Project identity

- Product: **Workspace Control**.
- Target: **Windows 11 x64**.
- Permanent Windows control center; not a one-time bootstrapper.
- Primary user concept: **My Workspace**.
- Technical desired-state representation: **ProfileManifest**.
- Local operation must remain useful without an account or cloud service.

## Source of truth

Use each source according to its role. Product intent has priority over the current implementation:

1. `BRAINSTORMING-RAW.md` — immutable historical brainstorming archive.
2. `BRAINSTORMING.md` — structured synthesis of the original product direction.
3. `docs/PRODUCT-VISION.md` — validated product purpose, scope and boundaries.
4. `docs/UX.md` — validated UX principles and interaction rules.
5. `docs/ARCHITECTURE.md` — validated technical direction and boundaries.
6. `docs/DECISIONS.md` — durable decisions that refine or constrain the direction.
7. `docs/PORTABILITY.md` — storage and distribution constraints.
8. `docs/ROADMAP.md` — current directional sequencing.
9. `docs/STATUS.md` — implementation snapshot only; never product authority.
10. Code and Git — actual implementation and history only; existing code does not override the intended product direction.
11. Conversation — temporary context.

When sources disagree, identify the divergence. Do not rewrite product intent merely to preserve existing code.

## Product invariants

- Users build a Workspace by choosing what they want managed.
- Predefined profiles are optional editable templates, never a mandatory primary UX.
- First-run experience observes the real machine before asking what to manage.
- Normal interactive mode never mutates silently.
- Important mutations follow **observe → desired state → diff/plan → review → explicit confirmation → apply → verify**.
- Unknown, unavailable, blocked and error states remain distinct; never guess missing evidence.
- WinGet is a provider, not the product architecture.
- Desktop, CLI and future surfaces share the same application/domain contracts.
- Future cloud/fleet functionality must reuse the local execution model.
- The original brainstorming direction remains the reference when deciding how to correct implementation drift.

## Storage invariant

Workspace Control is a self-contained, unpackaged portable application.

Application-owned mutable data stays under the explicit portable application root.

Do not introduce hidden persistence through AppData, `ApplicationData.Current`, Registry-backed application configuration/state or another implicit per-user location unless an explicit product/architecture decision changes this contract.

## Architecture

Target dependency direction:

**Desktop / CLI → Application → Domain**

Infrastructure implements application-facing adapters and Windows/provider integration.

Presentation must not contain Windows mutation logic. Domain must remain independent of concrete Windows/provider implementations.

The architecture is a means to realize the product direction, not a constraint that protects existing implementation.

## Safety

Treat user, provider, network and downloaded data as untrusted.

- Use least privilege.
- Keep privileged execution narrow and auditable.
- Verify downloaded artifacts before use.
- Never store secrets in ordinary profiles or logs.
- Do not present undocumented registry changes as official Windows policy.
- Persisted confirmed plans must not be silently recomputed.
- If preconditions diverge before an unfinished mutation, make the operation stale rather than silently applying a new plan.

## Technology

- C# / .NET 10.
- WinUI 3 / Windows App SDK.
- .NET CLI.
- PowerShell may be used as an external Windows mechanism when appropriate; business logic remains in C#.
- Prefer simple, maintainable solutions over unnecessary abstraction.
- Existing implementation may be replaced when that is the cleaner path to the intended product.

## UX

- Windows 11 Fluent-inspired UI.
- System / Light / Dark themes.
- Semantic status colors, never color alone.
- Simple / Advanced / Expert presentation levels.
- Progressive disclosure of technical details.
- English and French localization.
- Keyboard, screen-reader, contrast, text scaling and reduced-motion accessibility are part of the normal quality bar.

## Working rules

- Read the applicable canonical documents before changing product, architecture, UX or persistence behavior.
- Treat the current codebase as an implementation baseline to audit, not as a product contract.
- When code conflicts with product intent, prefer correcting or replacing the code unless a genuinely new product decision is required.
- Rebuild or delete existing code when incremental changes would preserve the wrong direction.
- Keep durable decisions in repository documentation.
- Do not turn brainstorming ideas into commitments without an explicit decision.
- Never claim CI is green without checking the exact commit.
- Use feature branches and PRs; keep commits coherent and atomic.
