# Workspace Control — UX and Design System

## Goal
Make difficult Windows administration feel simple, calm, modern and trustworthy while keeping technical depth available.

## Navigation
- Home
- Applications
- Updates
- Windows
- Optimizations
- Drivers
- WSL
- Diagnostics
- Cleanup
- Profiles
- Cache / Offline
- Settings

Advanced surfaces can expose inventory, providers, policies, registry evidence, services, operations, logs and plugin diagnostics.

## Presentation levels
### Simple
Health, updates, recommendations and safe actions.

### Advanced
Inventory, providers, configuration details, profiles and operation details.

### Expert
Provider evidence, raw policy/registry details, operation contracts, diagnostics and CLI/API-oriented information.

These are views over the same capabilities, not separate products.

## Themes
Support System, Light and Dark. Theme affects presentation only.

## Semantic colors
| Meaning | Role |
|---|---|
| Green | success / healthy / compliant |
| Blue | information / primary action |
| Amber | warning / attention |
| Orange | high impact / elevated risk |
| Red | error / danger / destructive |
| Purple | advanced / automation |

Never use color alone to communicate state.

## Progressive disclosure
Every mutation starts with: what, why, scope, risk, reversibility and restart impact. Technical implementation, registry/policy paths, provider details and logs are one step deeper.

## Operations
Long operations show current step, progress where measurable, affected item, elapsed time, cancellation, details and recovery status. The UI must never leave the user unsure whether work is still running.

## Accessibility
Keyboard navigation, visible focus, screen-reader names, sufficient contrast, scalable text, reduced-motion consideration and accessible errors are required.

## Localization
English is the default. French is supported from the beginning. Additional languages must be addable without domain-code changes.

WinUI user-facing strings are stored in qualified `Strings/<language>/Resources.resw` files. XAML uses `x:Uid` for static UI text and accessibility names; view-model status and progress messages use the same resource set through the desktop localization service. Domain and application contracts remain language-neutral.

The provisioning surface must localize:
- page headings and actions;
- operation status and progress messages;
- recovery/stale-plan guidance;
- error feedback labels;
- accessibility names for lists, actions, status and progress controls.

Locale changes must not change provisioning behavior or operation state.