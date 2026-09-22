# Roadmap

## Completed foundation

- Native C#/.NET 10 engine.
- Native WPF desktop shell.
- Self-contained Windows x64 CLI.
- Declarative Windows component/profile catalog.
- Official-source-first installer resolution.
- Persistent local installer cache at C:\DevCache.
- SHA-256 and Authenticode verification foundation.
- WinGet explicit fallback.
- Provisioning operation history and recovery foundation.
- Windows inventory and optimization foundations.
- Self-hosted Windows CI.

## Next engineering milestones

### 1. Provisioning reliability
- durable worker process;
- atomic operation claims;
- checkpointed resume without duplicate installs;
- reboot-aware recovery;
- postcondition verification per component.

### 2. Installer/cache hardening
- authoritative upstream digest support;
- stronger Authenticode publisher validation;
- immutable cache records;
- cache garbage-collection policy that preserves rollback versions;
- offline restore/export/import.

### 3. Component catalog
- complete official-source resolvers;
- authoritative version detection per component;
- dependency graph validation;
- schema validation;
- explicit fallback declarations.

### 4. Desktop UX
- profile/component selection;
- dry-run diff;
- live progress;
- operation detail and logs;
- recovery center;
- diagnostics;
- maintenance and optimization workflows;
- polished Windows 11 visual system.

### 5. Validation
- unit tests;
- cache integration tests;
- installer resolver tests with deterministic fixtures;
- Windows integration tests;
- self-contained startup smoke test;
- isolated fresh-Windows smoke environment.

## Explicit non-goals

- Linux/WSL provisioning.
- PowerShell or batch compatibility layers.
- Parallel provisioning implementations.
- Hidden destructive optimizations.
