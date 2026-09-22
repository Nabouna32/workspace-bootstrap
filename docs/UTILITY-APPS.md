# Utility applications

Workspace Bootstrap manages Windows workstation applications and development tooling through declarative component manifests.

## Source policy

Official vendor installers are preferred. WinGet is used only where a component explicitly declares it as a fallback.

## Application state

The Engine records detected versions and evidence so the desktop UI can distinguish installed, missing, outdated and unknown states.

All installation and removal operations remain idempotent where technically possible and destructive operations require explicit confirmation.
