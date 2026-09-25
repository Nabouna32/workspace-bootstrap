# Workspace Control — Decisions

This file records durable decisions and their context. It is not a development journal.

## Baseline decisions

### Product identity

Workspace Control is a permanent Windows 11 control center. The original bootstrapper concept is historical context, not the current product model.

### User model

**My Workspace** is the primary user-facing desired-state concept. Users choose what they want managed. Predefined profiles/templates are optional starting points, not the primary product model.

### Workspace semantics

A Workspace is a **partial desired state**. Only elements explicitly defined by the user create desired-state requirements. Undefined elements are outside the Workspace's management scope and must not generate inferred mutations.

The product must distinguish:

- defined desired state;
- undefined desired state;
- unknown observed state.

An unknown observed state is not permission to guess or mutate.

### Workspace capture and reuse

A Workspace may be created from an explicit capture of the current machine. Capture must be controllable so that the product does not automatically turn the complete machine inventory into a giant desired-state configuration.

Workspaces are intended to be reusable artifacts. They may be saved, duplicated, exported, imported and applied to compatible machines.

This supports both ongoing maintenance and reconstruction after formatting or a fresh Windows installation.

### Templates

Templates are optional starting configurations from which a user can create a Workspace. The resulting Workspace is independent and user-owned.

The product should use **Workspace** as the primary domain concept and **template** for optional starting points. The term **profile** must not be allowed to silently become a competing primary model.

### Personal and organizational use

The same Workspace/desired-state model must be usable for a personal machine and extensible to future multi-machine or organizational deployment.

Enterprise/fleet management is not required for the local product, but the architecture must not make it impossible. Future organizational capabilities should reuse the local desired-state and execution model rather than introduce a separate Windows configuration engine.

The exact future concepts for organizations—such as groups, policies, inheritance, targeting and centralized authorization—remain intentionally undecided.

### Safety

Normal interactive operation does not mutate silently. Important changes are planned, reviewed and explicitly confirmed before execution, followed by verification.

Future explicitly authorized automated or organizational execution may use a separate policy/authorization model and does not imply interactive confirmation for every individual operation.

### Architecture

Desktop and CLI share application/domain contracts. Infrastructure owns Windows/provider integration. WinGet is a provider, not the architecture.

### Portability

Application-owned mutable state is portable and explicit. Hidden AppData persistence is forbidden unless a future explicit decision changes the product contract.

### Technology

The supported desktop stack is C#/.NET 10 with WinUI 3 / Windows App SDK on Windows 11 x64.

### Local-first

The local product does not require an account or cloud service. Future cloud/fleet capabilities must reuse the local desired-state/execution model rather than introduce a second Windows engine.

### Implementation realignment

The current codebase is an implementation snapshot, not the authority for product direction. Where implementation has drifted from the brainstorming and validated product intent, the project will realign the implementation with that intent. Existing code, architecture or abstractions may be substantially rewritten or removed when that is the cleanest way to restore coherence. Preserving existing implementation is not itself a product constraint.

The distinction is explicit:

**product intent → validated decisions → architecture → implementation**

not:

**existing implementation → retroactive product decision**.

### Historical installation direction

The brainstorming includes cache and offline capabilities as a meaningful product direction. No later decision should demote that direction merely because the current implementation is online-first. Its exact scope and sequencing remain to be determined during product/architecture realignment.

## Decision discipline

A new decision may refine or change the product direction, but implementation details do not become decisions merely because they exist in code.

When historical context cannot be verified, record the uncertainty instead of reconstructing a false history.
