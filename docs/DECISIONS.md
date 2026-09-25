# Workspace Control — Decisions

This file records durable decisions and their context. It is not a development journal.

## Baseline decisions

### Product identity

Workspace Control is a permanent Windows 11 control center. The original bootstrapper concept is historical context, not the current product model.

### User model

**My Workspace** is the primary user-facing desired-state concept. Users choose what they want managed. Predefined profiles are optional editable templates.

### Safety

Normal interactive operation does not mutate silently. Important changes are planned, reviewed and explicitly confirmed before execution, followed by verification.

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
