# Workspace Control — UX

## Experience principles

Workspace Control should feel like a modern Windows 11 control center, not a collection of technical utilities.

The primary rule is:

> **Human first, technical detail second.**

The interface should expose the user's goal and the consequence of an action before exposing implementation details.

The Workspace experience should not feel like a large checklist. Controls such as checkboxes may be appropriate for individual settings, but the primary mental model is **current state → desired state → differences → review → action**.

## My Workspace

My Workspace is the primary desired-state experience.

A Workspace represents a **partial desired state**: the user explicitly chooses which parts of the machine they want managed. Everything else remains outside the Workspace's management scope.

The user should be able to:

1. observe the current PC;
2. create a Workspace from scratch, from an optional template, from an existing Workspace, or from a controlled capture of the current machine;
3. choose what they want managed;
4. see what differs from the current state;
5. review impact, risk and reversibility;
6. explicitly confirm;
7. follow execution;
8. see final verification.

Creating a Workspace from the current machine must be an explicit capture workflow. The product should not automatically turn every detected item into a desired-state requirement.

### Workspace lifecycle

A Workspace can be:

- created;
- edited;
- duplicated;
- captured from observed state;
- imported;
- exported;
- reused on the same or another compatible machine.

This makes a Workspace useful both for long-term maintenance and for rebuilding an environment after formatting or a fresh Windows installation.

### Templates

Templates are optional starting points used to create Workspaces.

Once a template is used, the resulting Workspace belongs to the user and can be modified independently. Templates must not be presented as a replacement for user-owned Workspaces.

### Desired-state presentation

The UI must make the following distinction understandable:

**Defined** — the user has expressed a requirement.

**Undefined** — the user has not expressed a requirement. This does not mean missing, unwanted or disabled, and must not produce an inferred action.

**Unknown** — the product cannot establish the actual state reliably enough to evaluate the requirement.

Observed state and desired state should be presented separately before their differences are presented.

## Presentation levels

The same underlying capability should support:

- **Simple** — essential information and safe actions;
- **Advanced** — additional control and context;
- **Expert** — technical evidence, methods and detailed state.

Changing presentation level must not silently change the safety model.

## Visual language

The interface should be Fluent-inspired and feel native to Windows 11 while having a distinct Workspace Control visual identity.

It should support:

- System, Light and Dark themes;
- semantic status colors;
- clear hierarchy and whitespace;
- progressive disclosure;
- responsive layouts;
- consistent interaction patterns;
- friendly visual feedback without decorative noise.

Semantic colors communicate meaning such as:

- success / healthy;
- information / neutral;
- warning / attention;
- high impact / elevated risk;
- error / failure.

Color is never the sole carrier of state; icons, text, labels and accessible semantics must communicate the same meaning.

## Navigation and information architecture

The application should be organized around user goals and product capabilities rather than implementation details.

The home experience should act as a control center: it should make the current machine state, Workspace state and meaningful differences visible without turning the application into a dashboard of unrelated counters.

Workspace domains may include applications, Windows configuration, privacy, performance, maintenance, drivers, WSL and other supported capabilities. These domains can be introduced progressively rather than exposing every technical area at once.

## Accessibility

Keyboard navigation, focus, screen-reader names, contrast, text scaling and reduced-motion preferences are part of the normal UX quality bar.

English and French are supported from the beginning. User-facing strings belong in the localization system rather than hard-coded UI markup.

## Errors and operations

Errors should progress from:

**understandable problem → useful action → technical detail**

Long-running operations should expose progress, current activity, remaining work and relevant errors. Logs and technical details remain available without becoming the primary message.

Destructive or irreversible operations require contextual confirmation.

For important Workspace changes, the primary action should be to **review the proposed changes**, not to immediately apply them.

The review should explain what will change, why it is changing, impact, risk, reversibility, prerequisites and the expected post-condition before confirmation.

## Multi-machine future

The local UX must not prevent a future multi-machine experience.

The same Workspace model may eventually be used for:

- a personal machine;
- several machines owned by one user;
- groups of organizational machines;
- centrally managed deployment policies.

The exact fleet UX, authorization model and policy inheritance are future design work and are not fixed by this document.
