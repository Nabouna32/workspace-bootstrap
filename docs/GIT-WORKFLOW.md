# Git workflow

GitHub is the source of truth.

## Branches
Use `feat/<area>-<name>`, `fix/<area>-<name>`, `refactor/<area>-<name>`, `docs/<area>-<name>` or `chore/<area>-<name>`.

## PRs
PRs provide isolation, CI, reviewable diffs and rollback points. The tech lead may merge a coherent PR autonomously once exact required CI is green and the change is safe.

## Rules
- Never merge failing required CI.
- Never claim CI is green from an older commit.
- Delete merged branches when practical.
- Do not revive abandoned self-hosted runner work.
- Do not preserve dead architecture merely to minimize a diff.

Important architecture, security or irreversible-behavior changes must update the documentation contract.