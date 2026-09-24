# Configuration

Product settings and desired-state profiles are separate concepts.

## Product settings
Theme, language, interface level, logging, local installer retention, notifications and provider preferences are local preferences. They never silently authorize dangerous system mutations.

## Workspaces and profiles
A Workspace is the user-facing desired-state object. `ProfileManifest` is its technical versioned representation. Workspaces contain applications, Windows policies/settings, drivers, WSL, optimizations and conditions.

Users build Workspaces by selecting and checking/unchecking what they want managed. Predefined profiles are optional templates, never a required first-run choice. Built-in templates remain packaged read-only content; user-owned Workspaces are persisted under the portable application root under `workspaces/` so the Workspace travels with the application copy and is not hidden in AppData.

## Portable configuration
Application-owned configuration, Workspaces, cache, state and logs use the portable application root. See `docs/PORTABILITY.md` for the complete storage contract. Profiles can also be imported/exported to an explicit user-selected path. Exports contain no secrets.

## Cloud-ready
The local engine treats a profile as data regardless of whether it originated locally, from an import or from future cloud synchronization.