# Workspace Control — Documentation

## Product

- [Product experience](PRODUCT-EXPERIENCE.md) — canonical user-facing product model, Workspace UX, application catalogue and interaction contract.

- [Product vision](PRODUCT-VISION.md) — mission, audience, boundaries and long-term direction.
- [Roadmap](ROADMAP.md) — implementation phases and quality bar.
- [Status](STATUS.md) — current migration state and confirmed decisions.

## Architecture

- [Architecture](ARCHITECTURE.md) — layers, capabilities, providers, desired state and privilege boundaries.
- [UX design](UX-DESIGN.md) — navigation, presentation levels, themes, semantic colors, accessibility and localization.
- [Profiles](PROFILES.md) — desired-state model, conditions and future heterogeneous-machine support.
- [Software management](SOFTWARE.md) — discovery, providers, interactive installers, updates and cleanup.
- [Inventory](INVENTORY.md) — observations, evidence, ownership and desired-state diff.
- [Provisioning and operations](PROVISIONING.md) — operation lifecycle, confirmation, recovery and offline execution.

## Windows administration

- [Prerequisites](PREREQUISITES.md) — machine capability detection.
- [Security](SECURITY.md) — elevation, downloads, registry/policy safety, plugins and secrets.
- [Configuration](CONFIGURATION.md) — product settings versus desired-state profiles.
- [Logging](LOGGING.md) — configurable diagnostics and operation logs.
- [Reinstall and recovery](REINSTALL.md) — preserving profiles/cache across Windows reinstallation.
- [Version policy](VERSION-POLICY.md) — per-application and provider version semantics.

## Engineering

- [CI and local builds](CI-LOCAL-BUILD.md) — validation strategy and GitHub-hosted runners.
- [Git workflow](GIT-WORKFLOW.md) — branches, PRs and integration rules.

## Documentation rule

This directory describes the **Workspace Control** product, not the historical bootstrapper. New documentation must use the current product model. Superseded architecture, job-system and utility-app documents are intentionally removed rather than maintained in parallel.
