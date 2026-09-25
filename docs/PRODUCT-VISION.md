# Workspace Control — Product Vision

## Purpose

Workspace Control is a permanent Windows 11 control center. It evolved from the original bootstrapper idea into a local-first product for understanding, configuring and maintaining Windows machines over time.

The product should be **simple in surface and powerful underneath**: everyday users should not need to understand Windows internals, while advanced users must retain access to the evidence and technical detail behind an operation.

Workspace Control is intended to work for an individual machine first, while keeping the desired-state model general enough to support multiple machines and, eventually, organizational deployment and fleet management.

## Product model

The central user concept is **My Workspace**: a user-owned desired state describing the parts of a Windows environment that the user explicitly wants Workspace Control to manage.

A Workspace is a **partial desired state**. It does not have to describe the whole machine. An element that is **undefined** in a Workspace has no desired-state requirement and therefore should not be changed merely because the observed machine differs from it.

A Workspace can cover applications, Windows configuration and policies, drivers, WSL, optimizations, maintenance rules and other supported capabilities. It can be created from scratch, based on an optional template, captured from an observed machine state, imported, exported, duplicated and reused.

A captured Workspace is not necessarily a complete inventory snapshot. The user should be able to decide which observed domains or elements become desired-state requirements.

A Workspace can therefore serve several related purposes:

- maintain an existing machine according to explicit choices;
- preserve a known-good environment;
- rebuild a machine after formatting or a fresh Windows installation;
- reproduce an environment on another machine;
- provide a basis for future multi-machine and organizational deployment.

Optional predefined **templates** are starting points for creating a Workspace. They are not the primary product model and do not replace user-owned Workspaces.

The core interaction is:

**observe → choose or capture desired state → compare → review → explicitly confirm → apply → verify**

## Desired-state semantics

Workspace Control must distinguish the user's intent from the machine's observed state.

For a desired-state element:

- **Defined** means the user has explicitly expressed a desired state.
- **Undefined** means the user has not expressed a requirement; no action should be inferred from that absence.
- **Unknown** means the actual machine state cannot currently be established with sufficient confidence; Workspace Control must not silently guess or mutate based on missing evidence.

The product must not treat:

- undefined as absent;
- absent as unwanted;
- unknown as healthy;
- detected as managed.

These distinctions are fundamental to safe, predictable behavior.

## Principles

- Local operation remains useful without an account or cloud service.
- Interactive operations do not silently mutate the system.
- Detection can be broad; mutation is conservative.
- Unknown state remains visible rather than being guessed away.
- Every important change should be explainable, risk-aware and verified.
- A Workspace expresses explicit intent; undefined areas remain outside its management scope.
- Providers are implementation mechanisms, not product policy.
- WinGet is a provider, not the product architecture.
- The same local desired-state model should support personal use and future multi-machine management.
- Cloud synchronization and fleet management are future extensions, not prerequisites for the local product.
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
- future multi-machine, cloud and fleet capabilities.

This list describes product direction, not a promise that every domain belongs in the first public release.

## Boundaries

Workspace Control is not intended to become:

- a silent system modification engine;
- a generic registry cleaner;
- a WinGet-only frontend;
- a PowerShell wrapper presented as an architecture;
- a cloud-dependent product;
- an opaque automation tool.

Future automated or organizational deployment may apply explicitly defined policies or Workspaces without interactive confirmation on every individual machine, subject to a separate authorization and policy model. This does not change the default safety model for normal interactive use.

## Technology direction

The supported desktop target is Windows 11 x64, using C#/.NET and WinUI 3 / Windows App SDK. The CLI shares the same application/domain contracts.

The product is designed local-first so that a local Workspace, an imported Workspace or a future cloud-managed desired state can feed the same local desired-state and execution model rather than creating separate Windows execution architectures.
