# Workspace Control — Product Vision

## Purpose

Workspace Control is a permanent Windows 11 control center. It evolved from the original bootstrapper idea into a local-first product for understanding, configuring and maintaining a Windows machine over time.

The product should be **simple in surface and powerful underneath**: everyday users should not need to understand Windows internals, while advanced users must retain access to the evidence and technical detail behind an operation.

## Product model

The central user concept is **My Workspace**: a user-owned desired state describing what they want Workspace Control to manage.

A Workspace may eventually cover applications, Windows configuration, drivers, WSL, optimizations and conditions. Predefined profiles, if offered, are optional editable templates and never the mandatory primary experience.

The core interaction is:

**observe → choose desired state → compare → review → explicitly confirm → apply → verify**

## Principles

- Local operation remains useful without an account or cloud service.
- Interactive operations do not silently mutate the system.
- Detection can be broad; mutation is conservative.
- Unknown state remains visible rather than being guessed away.
- Every important change should be explainable, risk-aware and verified.
- Providers are implementation mechanisms, not product policy.
- WinGet is a provider, not the product architecture.
- Cloud and fleet management are future extensions, not prerequisites for the local product.
- Accessibility, localization, security and maintainability are product quality requirements.

## Scope

Long-term product domains include:

- applications and software;
- Windows administration and policies;
- diagnostics;
- cleanup;
- optimization;
- drivers;
- WSL;
- Workspaces / desired state;
- cache and artifact management;
- providers and controlled extensions;
- future cloud/fleet capabilities.

This list describes product direction, not a promise that every domain belongs in the first public release.

## Boundaries

Workspace Control is not intended to become:

- a silent system modification engine;
- a generic registry cleaner;
- a WinGet-only frontend;
- a PowerShell wrapper presented as an architecture;
- a cloud-dependent product;
- an opaque automation tool.

## Technology direction

The supported desktop target is Windows 11 x64, using C#/.NET and WinUI 3 / Windows App SDK. The CLI shares the same application/domain contracts.

The product is designed local-first so that future cloud synchronization can feed the same local desired-state engine rather than creating a second Windows execution architecture.
