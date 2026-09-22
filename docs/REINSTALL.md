# Reinstallation and recovery

Workspace Bootstrap is designed to survive a clean Windows reinstall when the persistent cache is exported beforehand.

## Before reinstalling

1. Export `C:\DevCache` to a non-system drive.
2. Keep any user-owned configuration you explicitly want to restore.
3. Record the selected Workspace Bootstrap profiles.

## After reinstalling

1. Restore the cache to `C:\DevCache`.
2. Build or deploy the self-contained application.
3. Run diagnostics.
4. Import/select the desired profiles.
5. Preview the provisioning plan.
6. Apply the plan.

The Engine verifies cached artifacts before reuse. Invalid artifacts are ignored; a valid older version remains available when present.
