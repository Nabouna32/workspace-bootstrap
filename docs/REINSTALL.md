# Reinstallation and recovery

Workspace Bootstrap is portable. The application package contains the executable, bundled manifests and local application data needed to resume normal operation.

## Before reinstalling Windows

1. Copy the complete Workspace Bootstrap application directory if you want to preserve its local state.
2. Export `C:\DevCache` if you want to preserve the verified installer cache.
3. Keep any external configuration you explicitly want to restore.

## After reinstalling Windows

1. Restore the application directory.
2. Restore `C:\DevCache` when available.
3. Launch the application.
4. Run diagnostics.
5. Preview the provisioning plan.
6. Apply the plan.

The Engine verifies cached artifacts before reuse. Invalid artifacts are ignored; a valid older version remains available when present.
