# Workspace Control — Product Experience Contract

This document is the canonical product/UX contract for the user-facing Workspace Control experience.

It exists to prevent implementation work in one subsystem from silently redefining the product. Engine, provider, provisioning, CI, or infrastructure changes may evolve implementation details, but they must not replace this experience contract unless the product direction is intentionally changed and documented.

## Product promise

Workspace Control is a permanent Windows 11 control center.

It is not a bootstrap wizard and it is not primarily a collection of predefined setup scripts. The user opens it to understand the current PC, decide what they want to manage, make deliberate changes, and keep the machine in a known state over time.

The product should make difficult Windows administration understandable without hiding technical depth.

## The core user model: My Workspace

The primary user concept is **My Workspace**.

A Workspace is the user's own desired configuration for this PC or a class of PCs. The user builds it by choosing what should be present, enabled, configured, optimized or otherwise managed.

The user is not required to start from a predefined profile.

The technical engine represents a Workspace as a versioned declarative **ProfileManifest**. "Profile" remains valid in domain, file-format and API terminology; **Workspace** is the preferred user-facing term.

### First-run principle

First launch should begin from the machine that actually exists:

1. Detect and explain the current machine state.
2. Show what Workspace Control can observe and manage.
3. Let the user choose the areas and items they want to manage.
4. Let the user check/uncheck desired applications, Windows settings, optimizations and other supported capabilities.
5. Save those choices as a Workspace.
6. Compare the Workspace with the current machine.
7. Review changes, risk and impact.
8. Confirm explicitly.
9. Apply the persisted plan.
10. Verify the resulting state.

The application must not force a predefined setup profile onto the user.

## Predefined profiles and templates

Predefined profiles are not the primary UX.

If the product ships curated configurations, they are **optional templates** that help users start quickly. They must never replace the Workspace builder or imply that one configuration is correct for everyone.

A template may be imported, previewed and edited before becoming the user's Workspace.

The distinction is:

- **Template** — optional starting point supplied by the product or another source.
- **Workspace** — user-owned desired state.
- **ProfileManifest** — technical serialized representation of desired state.

## Primary navigation

The main navigation should expose the product domains directly:

- Home
- Applications
- Updates
- Windows
- Optimizations
- Drivers
- WSL
- Diagnostics
- Cleanup
- My Workspace
- Settings

Advanced/diagnostic surfaces may additionally expose inventory, providers, policies, registry evidence, services, operations, logs and plugin diagnostics.

The exact navigation can evolve as capabilities mature, but implementation work must preserve the principle that the user navigates by **what they want to accomplish**, not by internal engine concepts.

## Home

Home is the control-center dashboard.

It should answer, at a glance:

- Is the PC healthy?
- What needs attention?
- What changed?
- What updates are available?
- What applications are detected?
- What recommendations are available?
- What operations are running or require attention?
- What can I do next?

Typical cards include:

- System health
- Updates
- Applications
- Windows configuration
- Optimizations
- Diagnostics/issues
- Workspace status
- Recent operations
- Quick actions

Cards must lead to real detail and actions. Home is not a decorative dashboard.

## Applications

Applications are a first-class product surface, not merely an inventory table.

The experience should provide:

- searchable application catalogue;
- categories such as Browsers, Development, Media, Gaming, Productivity, Utilities, Communication, Microsoft and System;
- filters such as All, Installed, Updates and Available;
- application cards/list rows with name, publisher, icon where available, installed version, available version and state;
- install, update and uninstall actions where supported;
- multi-selection for safe batch operations;
- application details including source/provider, provenance, evidence, version policy and capabilities;
- clear explanation when an action is unavailable;
- discovery beyond the small curated catalogue.

The curated catalogue is a source of richer metadata and provider preferences, not a hard limit on what Workspace Control can see.

An application may be:

- installed and managed;
- installed and observed;
- available for installation;
- updateable;
- partially known;
- unknown;
- action unavailable.

Unknown is an evidence state, not an error.

## Unknown, unavailable and blocked states

The UI must never collapse uncertainty into a vague or alarming status.

Use distinct concepts:

- **Unknown** — evidence is incomplete or the product cannot determine the state reliably.
- **Unavailable** — the requested action is not currently supported or cannot be performed with available providers.
- **Blocked** — a safety, prerequisite, condition or precondition prevents the operation.
- **Error** — an operation or observation failed unexpectedly.
- **Healthy/Ready** — the required evidence and preconditions are satisfied.

Every non-obvious state should explain:

- what is known;
- what is not known;
- why the state exists;
- what the user can do next;
- technical evidence through progressive disclosure.

Never silently invent a state to make the UI look complete.

## Windows

Windows management should present supported settings and policies as understandable controls.

For each setting, show where appropriate:

- current value;
- desired value;
- explanation;
- impact;
- restart/sign-out requirement;
- reversibility;
- evidence/source;
- advanced technical details.

Registry-backed settings must be presented as documented product capabilities, not as arbitrary registry hacks.

## Optimizations

Optimizations are recommendations with explicit impact and reversibility.

Each optimization should communicate:

- what changes;
- why it may help;
- expected impact;
- risk level;
- reversibility;
- prerequisites;
- restart requirements;
- current state.

High-impact optimizations require stronger confirmation.

## Updates

Updates provide a dedicated view over provider-backed update evidence.

Users should be able to understand:

- what is outdated;
- current version;
- available version;
- source/provider;
- why an update is proposed;
- whether restart is required;
- whether the update can be performed automatically or interactively.

Batch update actions must still produce an explicit reviewable plan.

## My Workspace

My Workspace is the user-facing desired-state editor.

It should let users build and modify their desired configuration through clear sections and checkable controls, for example:

### Applications
Examples may include:

- Visual Studio Code
- Git
- GitHub CLI
- Visual Studio
- Firefox
- Chrome
- VLC
- 7-Zip

### Windows
Examples may include:

- Dark/light appearance preferences where supported;
- file-extension visibility;
- Developer Mode where supported;
- Windows Update preferences;
- Widgets or other documented Windows settings.

### Performance
Examples may include:

- startup applications;
- visual effects;
- power mode;
- other documented, reversible optimizations.

### Development
Examples may include:

- Git;
- GitHub CLI;
- .NET SDK;
- Python;
- Node.js;
- Rust.

These are examples of the desired product model, not a fixed catalogue.

The user can:

- select/unselect items;
- inspect details;
- see current state;
- see whether the item is already satisfied;
- save the Workspace;
- rename it;
- duplicate it;
- export/import it;
- compare it with the current PC;
- review and apply changes.

## Desired-state review

The review flow is:

**My Workspace → Observe current PC → Compare → Review changes → Confirm → Apply → Verify**

The compare stage is read-only.

The review must distinguish at least:

- already satisfied;
- will be installed/enabled/configured;
- will be updated;
- will be removed/disabled;
- blocked;
- unknown/unverified.

Each proposed change should expose risk, reversibility and restart impact where relevant.

The persisted provisioning plan is the safety boundary. Apply executes the exact confirmed plan and never silently replans.

## Presentation levels

Simple, Advanced and Expert are presentation levels over the same product engine.

### Simple

Prioritize:

- health;
- updates;
- recommended actions;
- clear controls;
- human-readable explanations.

### Advanced

Expose:

- inventory;
- provider/source;
- desired-state details;
- profile/workspace details;
- operation details;
- risk and reversibility metadata.

### Expert

Expose:

- provider evidence;
- registry/policy paths;
- raw diagnostics;
- operation contracts;
- detailed logs;
- CLI/API-oriented information.

Changing presentation level must not change the underlying safety contract.

## Visual language

The application should feel like a modern Windows 11 control center rather than a utility dump.

Use:

- clear hierarchy;
- readable typography;
- deliberate spacing;
- cards and grouped surfaces where they improve scanning;
- consistent icons;
- clear selection states;
- meaningful empty states;
- visible focus;
- semantic status indicators;
- restrained use of transparency and opacity.

Do not use low-opacity text as a substitute for hierarchy. Text must remain legible at normal Windows scaling and high-contrast/accessibility settings.

## Semantic colors

The product semantic palette is:

| Meaning | Role |
|---|---|
| Green | success, healthy, compliant |
| Blue | information, primary action |
| Amber | warning, attention |
| Orange | high impact, elevated risk |
| Red | error, danger, destructive |
| Purple | advanced, automation |

Color is never the only state signal. Pair it with text, icons, labels, shape or other accessible affordances.

## Themes

Support:

- System;
- Light;
- Dark.

Theme is presentation only and never changes operation semantics.

## Interaction and safety

Normal interactive mode never mutates the machine silently.

Before mutation, the user must be able to understand:

- what will change;
- why;
- scope;
- risk;
- reversibility;
- restart/sign-out impact;
- provider/source where relevant.

Destructive or irreversible actions require contextual confirmation.

Long-running operations show:

- current step;
- progress where measurable;
- affected item;
- elapsed time;
- cancellation state;
- details;
- recovery state.

Errors must be visible and actionable.

## Accessibility

The experience must support:

- keyboard navigation;
- visible focus;
- screen-reader names;
- sufficient contrast;
- scalable text;
- reduced-motion consideration;
- accessible error and progress announcements.

Visual polish is not complete if the experience becomes difficult to use at 125%, 150% or larger Windows text scales.

## Localization

English and French are required from the beginning.

User-facing strings belong in localization resources. Product/domain contracts remain language-neutral.

New features must add English and French resources together and must not introduce hard-coded user-facing strings.

## Product/engine boundary

The product experience is intentionally independent from the internal implementation.

The engine may evolve:

- provider implementations;
- inventory aggregation;
- persistence;
- provisioning internals;
- Windows adapters;
- dependency injection;
- performance;
- CI/build infrastructure.

Those changes must preserve the user-facing model unless a deliberate product decision changes it.

## Change control

A product-direction change requires:

1. update this contract first or in the same coherent change;
2. update affected product/UX/domain documentation;
3. explain the reason and scope in the PR;
4. update implementation only after the contract is coherent;
5. audit all dependent Markdown documents for contradictions.

An implementation PR must never replace this document with a narrower engine-oriented description.

