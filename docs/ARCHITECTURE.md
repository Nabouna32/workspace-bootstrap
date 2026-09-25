# Workspace Control — Architecture

## Architectural goal

The architecture supports a local Windows control center whose user-facing model is Workspace / desired state rather than a provisioning-only engine.

The primary dependency direction is:

**Desktop / CLI → Application → Domain**

with **Infrastructure** implementing application-facing adapters and integrating Windows and providers.

## Layers

### Domain

Contains stable concepts that can be tested without a live Windows machine:

- applications and inventory;
- capabilities;
- desired state;
- Workspaces / WorkspaceManifest;
- plans;
- operations;
- risk and reversibility;
- provenance and evidence;
- diagnostics.

### Application

Owns use-case orchestration and coordinates:

**observed state + desired state → diff → plan → confirmation → execution → verification**

It must not contain WinUI concerns or concrete Windows integration details.

### Infrastructure

Implements Windows and external mechanisms such as:

- Registry;
- Windows APIs;
- services and scheduled tasks;
- Event Log / WMI / CIM;
- WSL;
- package and installer providers;
- cache and artifact handling.

Providers are replaceable adapters. They provide evidence and execution mechanisms but do not own product policy.

### Desktop

WinUI 3 / Windows App SDK provides presentation, navigation, localization, accessibility and user interaction. Windows mutation logic does not belong in the presentation layer.

### CLI

The CLI consumes the same application/domain contracts and does not maintain a second implementation of product behavior.

## Desired-state mutation

Important mutations use a persisted operation lifecycle:

**Observed → Desired → Plan → awaiting confirmation → queued → running → post-condition verification → completed**

If observed state diverges from the persisted plan before an uncompleted mutation, the operation becomes stale rather than silently replanning.

Plans are read-only after confirmation. Apply executes the persisted plan.

## Security boundary

The normal application remains unelevated where possible. Privileged operations use a narrow, structured and auditable boundary with validated inputs.

Downloaded artifacts are verified before use. User-controlled, network and provider data are treated as untrusted.

## Storage

Workspace Control is a self-contained, unpackaged portable application.

Application-owned mutable data lives under an explicit portable application root. The application must not silently use AppData, ApplicationData.Current or Registry-backed application configuration/state.

Windows system stores remain valid when they are the target being observed or managed.

## Extensibility

Plugins are a future controlled extension boundary. Compatibility, metadata, permissions and trust must be explicit; privileged access is never implicit.

## Technology

- Windows 11 x64
- C#
- .NET 10
- WinUI 3 / Windows App SDK

The architecture should remain as simple as the product permits. Future cloud/fleet functionality should feed the same local engine rather than duplicate Windows mutation logic.
