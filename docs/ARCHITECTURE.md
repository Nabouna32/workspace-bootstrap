# Workspace Control — Architecture

## Target shape
```text
WinUI 3 Desktop ─┐
CLI ─────────────┼──> Application / Domain Engine
Future API ──────┤              │
Future Cloud ────┘              ├── Software providers
                                ├── Windows administration
                                ├── Drivers
                                ├── WSL
                                ├── Optimization / Policies
                                ├── Diagnostics
                                ├── Profiles / Desired state
                                ├── Cache / Offline
                                ├── Security
                                └── Plugins
```

## Layers
### Presentation
WinUI 3 handles navigation, input, accessibility, localization and presentation. It never owns Windows mutation logic.

### Application
Use cases coordinate inventory, planning, confirmation, operation lifecycle, profiles, cache and diagnostics.

### Domain
Defines application, inventory, capability, desired state, plan, operation, risk, provenance, provider, profile, diagnostic and cache concepts. Domain rules remain testable without a live machine.

### Infrastructure / Windows adapters
Adapters communicate with Registry, Windows APIs, services, scheduled tasks, Windows Update, Event Log, WMI/CIM, WSL, package providers and installers. They return structured evidence/results.

## Capabilities
Stable capabilities include `DetectApplication`, `InstallApplication`, `UpdateApplication`, `RemoveApplication`, `InspectResiduals`, `ObserveRegistrySetting`, `ApplyRegistrySetting`, `InspectDriver`, `UpdateDriver`, `InspectWindowsPolicy`, `ApplyWindowsPolicy`, `ApplyOptimization`, `RevertOptimization`, `RunDiagnostic`, `InstallWsl`, `ConfigureWsl`, `ApplyProfile`, `ManageCache` and related operations.

Each capability declares inputs, preconditions, risk, elevation requirement, preview, execution, postconditions and rollback metadata where available.

## Desired state
```text
Desired state + observed evidence
          ↓
        Desired-state Diff
          ↓
         Plan
          ↓
   User confirmation
          ↓
      Operation
          ↓
    Postcondition
          ↓
     Final verification
        ↓
     Final state
```

## Providers
Providers are replaceable adapters. Examples include official vendor installers, MSI/EXE, Store/package sources, WinGet, portable artifacts and future plugins. Providers expose evidence and provenance; they do not own product policy.

## Provisioning operation lifecycle

A provisioning mutation is never implied by computing a plan. Registry mutations use the same persisted operation lifecycle as application provisioning: capture snapshot → write → post-condition verification → final verification, with reverse-order rollback on failure. The lifecycle is persisted as:

Observed State → Desired State → Plan → awaiting-confirmation → queued → running → post-condition verification → completed

ProvisioningOperation stores the exact ProvisioningPlan that the user confirmed. Apply executes that persisted plan rather than recomputing a replacement plan. Before each uncompleted mutation, current inventory is compared with the corresponding planned precondition. A divergence makes the operation stale and non-resumable until a new plan is created and explicitly confirmed. Recovery/resume reuses the same persisted plan and repeats the precondition check before any mutation.

## Privilege boundary
The normal UI runs unelevated. A small privileged component accepts a narrow structured command contract and validates all arguments. An explicitly elevated application session may be offered as a convenience but is not the security foundation.

## Cache
Installer artifacts are staged and verified before use. A verified artifact is reused when it already matches the requested current version; there is no cache-only/offline provisioning mode. Failed downloads never invalidate valid artifacts.

## Plugins
Plugins are planned as a controlled extension boundary for providers, diagnostics, optimizations and Windows/WSL capabilities. Metadata, compatibility and permissions are required; unrestricted privileged access is never implicit.

## Future cloud
Cloud desired state resolves into the same local engine model. There is no second implementation of Windows mutation logic.

## Technology
C#/.NET 10, WinUI 3/Windows App SDK, Windows 11 x64 and a .NET CLI. WPF is retired; WinUI 3 is the only desktop presentation technology.