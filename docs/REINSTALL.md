# Clean Windows reinstall runbook

## Before formatting

Run the backup script to a non-system destination:

    .\bootstrap\windows\00-backup-before-reinstall.ps1 -BackupRoot "E:\dev-environment-backup" -ExportWsl

Check that it contains the SSH directory, Git configuration, WSL inventory, Windows package inventory and optional Ubuntu export.

Never upload that backup directory to GitHub.

Also verify that every local project with uncommitted work is pushed or separately backed up.

## After Windows 11 Pro 64-bit installation

1. Fully update Windows.
2. Verify WinGet.
3. Create `C:\dev` and clone this repository.
4. Open **PowerShell as Administrator**.
5. Run `.\bootstrap\windows\bootstrap.ps1`.
6. Restart Windows if WSL or Windows components request it.
7. Run `.\bootstrap\windows\90-verify.ps1`.
8. Recreate/configure WSL2 Ubuntu and run the WSL bootstrap.
9. Restore tracked WSL configuration.
10. Register the self-hosted runner with a fresh token.
11. Restore SSH keys outside the repository.
12. Validate GitHub SSH and `gh auth`.
13. Run all verification scripts.
14. Clone application repositories.
15. Run real Android and Windows builds.

The machine is disposable; the repository is the reconstruction plan.

See `docs/VERSION-POLICY.md` for the toolchain update policy.