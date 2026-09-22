# Windows clean-install runbook

## Before formatting

1. Push all repository work and verify the exact branch/commit is on GitHub.
2. Export `C:\DevCache` to a non-system disk if you want offline reinstallation after formatting.
3. Back up personal files, SSH keys and any application data outside the repository.
4. Export any WSL data separately if it is still needed; WSL is not managed by Workspace Bootstrap.

Never commit secrets or private backup data to this repository.

## After Windows 11 Pro installation

1. Fully update Windows.
2. Install the current supported .NET 10 SDK/runtime as required for development. The published Workspace Bootstrap CLI is self-contained.
3. Install or verify WinGet, because it is used as an explicit fallback for selected components.
4. Clone `Nabouna32/workspace-bootstrap`.
5. Build or obtain the self-contained Windows x64 CLI.
6. Start the Workspace Bootstrap desktop application.
7. Select the desired profile.
8. Review the dry-run plan.
9. Restore or attach the exported `C:\DevCache` when offline installation is required.
10. Run provisioning and monitor the operation history.
11. Restart Windows when an operation explicitly requires it, then use the application's recovery/resume flow.
12. Run diagnostics and verify the installed-state inventory.
13. Reinstall application repositories and run their own project-specific validation.

The repository is the reconstruction plan; the local cache is the recovery asset.
