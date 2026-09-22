# Reinstall and recovery

Workspace Control is designed to remain useful after Windows maintenance or a fresh installation.

Important user-controlled data includes profiles, exported configuration, retained cache artifacts and selected operation history.

## Before reinstalling Windows
Export or copy the profiles and cache artifacts you want to preserve.

## After reinstalling
1. Install/restore Workspace Control.
2. Import profiles/configuration.
3. Restore/import cache when desired.
4. Run inventory.
5. Review the desired-state diff.
6. Confirm the desired operations.
7. Apply and verify.

The final packaging design must not assume that mutable application state can safely live inside a protected Program Files directory.