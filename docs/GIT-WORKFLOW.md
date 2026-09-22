# Git Workflow

## Why we use pull requests

GitHub is the source of truth and a PR is our safety boundary. A PR gives us CI, a reviewable diff, a rollback point and a clear unit of work without requiring a multi-developer process.

## Technical-lead integration rule

The user does not need to manually approve routine repository integration decisions. The assistant acts as tech lead for the project and may:
- choose between technically equivalent implementation approaches;
- recommend changes to architecture, tooling, UX and developer workflow;
- introduce a better low-risk direction when it materially improves maintainability or product quality;
- merge a coherent PR autonomously once the exact required CI is green and the branch is safe to integrate.

This does not authorize silently changing important product goals, budget constraints or irreversible user-facing behavior. Those remain explicit product decisions.

## Execution-first rule

When the user authorizes implementation, perform the repository work before giving the progress report. The report should explain what was actually changed, validated and merged; it should not replace execution with a future-tense plan when the repository tools are available.

## Normal workflow

```
main
  │
  ├── feat/<coherent-change>
  │       ├── implementation commits
  │       ├── local/runtime validation
  │       └── Draft PR
  │              ↓
  │           GitHub CI
  │              ↓
  │        review / correction
  │              ↓
  └────────── merge ──────────→ main
```

## Branch naming

- `feat/<area>-<short-name>` — capability
- `fix/<area>-<short-name>` — root-cause correction
- `refactor/<area>-<short-name>` — structural improvement
- `docs/<area>-<short-name>` — documentation
- `chore/<area>-<short-name>` — maintenance/tooling

## Commit policy

Commits describe coherent milestones. Do not artificially limit commit count, but do not mix unrelated changes in one commit.

## PR policy

- Open substantial work as Draft early.
- Keep CI on GitHub-hosted runners for this repository.
- Do not merge with failing or unverified required CI.
- Prefer one PR per coherent product change.
- Avoid PR-per-tiny-fix overhead.
- After merge, delete the feature branch and start from the new `main`.
- The assistant may merge autonomously when the exact PR head has passing required CI and the change is coherent and safe.

## Current V2 bootstrap

The repository follows the current architecture documented in the repository status and architecture documents. Keep changes focused on real dependencies, safety, maintainability and reviewability.

## CI and local-build rule

Public repositories use standard GitHub-hosted runners for CI. Do not make a PR depend on the personal PC being online.

Local builds remain supported and intentional: they are useful when the developer wants immediate feedback or wants to avoid repeatedly downloading large APK/AAB/EXE artifacts from GitHub. A local build is a convenience and diagnostic tool, not a substitute for CI.


## Release principle


A branch merge is not automatically a product release. Introduce tagging/releases once V2 reaches a stable real-PC validation point.
