# Workspace Control — UX

## Experience principles

Workspace Control should feel like a modern Windows 11 control center, not a collection of technical utilities.

The primary rule is:

> **Human first, technical detail second.**

The interface should expose the user's goal and the consequence of an action before exposing implementation details.

## My Workspace

My Workspace is the primary desired-state experience.

The user should be able to:

1. observe the current PC;
2. choose what they want managed;
3. see what differs from the current state;
4. review impact, risk and reversibility;
5. explicitly confirm;
6. follow execution;
7. see final verification.

A predefined profile may be offered as a starting template, but the user owns and edits the resulting Workspace.

## Presentation levels

The same underlying capability should support:

- **Simple** — essential information and safe actions;
- **Advanced** — additional control and context;
- **Expert** — technical evidence, methods and detailed state.

Changing presentation level must not silently change the safety model.

## Visual language

The interface should be Fluent-inspired and support:

- System, Light and Dark themes;
- semantic status colors;
- clear hierarchy and whitespace;
- progressive disclosure;
- responsive layouts;
- consistent interaction patterns.

Semantic colors communicate meaning such as success, information, warning, high impact and error. Color is never the sole carrier of state.

## States

Unknown, unavailable, blocked, warning, error, healthy and in-progress states must remain distinct and understandable.

A missing capability must not be represented as a generic failure or as an apparently successful empty result.

## Accessibility

Keyboard navigation, focus, screen-reader names, contrast, text scaling and reduced-motion preferences are part of the normal UX quality bar.

English and French are supported from the beginning. User-facing strings belong in the localization system rather than hard-coded UI markup.

## Errors and operations

Errors should progress from:

**understandable problem → useful action → technical detail**

Long-running operations should expose progress, current activity, remaining work and relevant errors. Logs and technical details remain available without becoming the primary message.

Destructive or irreversible operations require contextual confirmation.
