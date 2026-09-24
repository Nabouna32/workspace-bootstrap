# Reinstall and recovery

Workspace Control is designed to remain useful after Windows maintenance or a fresh installation.

Important user-controlled data includes Workspaces, profiles, exported configuration, retained cache artifacts and selected operation history. These application-owned data live under the portable application root rather than a hidden AppData location.

## Before reinstalling Windows
Back up or copy the portable Workspace Control directory if you want to preserve its local Workspaces, configuration, cache and state. Export profiles when you want a portable document independent of the application directory.

## After reinstalling
1. Install/restore Workspace Control.
2. Import profiles/configuration.
3. Restore/import cache when desired.
4. Run inventory.
5. Review the desired-state diff.
6. Confirm the desired operations.
7. Apply and verify.

The final packaging design must not assume that mutable application state can safely live inside a protected Program Files directory.