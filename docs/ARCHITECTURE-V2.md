# Workspace Bootstrap Architecture

## Product boundary

Workspace Bootstrap is a Windows 11 workstation provisioning and recovery application.

## Native application architecture

```text
                    Workspace Bootstrap
                           |
              +------------+------------+
              |                         |
        WPF Desktop                 CLI
              |                         |
              +------------+------------+
                           |
                    .NET 10 Engine
                           |
       +-------------------+-------------------+
       |                   |                   |
  Configuration        Provisioning       Windows APIs
       |                   |                   |
  Components/Profiles  Installer/Cache   Inventory/Diagnostics
                           |
                    Official sources
                           |
                    WinGet fallback
```

There is one implementation of provisioning. The UI and CLI are clients of the Engine.

## State and cache

The package contains its mutable state:

- application state: `state`
- logs: `logs`
- installer cache: `cache`
- cache staging: `cache/staging`
- cache metadata: `cache/metadata`

Downloads are written to unique staging files. Verification occurs before promotion into the versioned cache. A failed staging operation must not invalidate an existing verified artifact.

## Installer source policy

1. Use an explicitly declared official vendor source.
2. Resolve the component-specific current stable version.
3. Download into unique staging.
4. Verify the artifact.
5. Atomically promote it into the cache.
6. Reuse a matching verified artifact on subsequent installs.
7. Retain older verified artifacts for rollback/reinstallation.
8. Use WinGet only when the component explicitly declares it as a fallback.

## Verification

- SHA-256 is calculated for every cached artifact.
- When an upstream digest is published, it must be checked against the downloaded digest.
- Signed EXE/MSI artifacts require Authenticode validation.
- Invalid or incomplete staging files are discarded.
- Cache metadata is treated as untrusted until the referenced artifact hash is revalidated.

## Execution safety

Provisioning is idempotent and observable. Destructive operations require explicit confirmation. Reboot-required operations and failures remain recoverable.

## CI architecture

GitHub Actions validates the repository on GitHub-hosted Windows runners.
