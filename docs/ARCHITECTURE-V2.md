# Workspace Bootstrap Architecture

## Product boundary

Workspace Bootstrap is a Windows 11 workstation provisioning and recovery application. Linux/WSL bootstrap is not part of the product.

## Native application architecture

```
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
  Configuration        Provisioning       Windows services
       |                   |                   |
  Components/Profiles  Installer/Cache   Inventory/Diagnostics
                           |
                    Official sources
                           |
                    WinGet fallback
```

There is one implementation of provisioning. The UI and CLI are clients of the engine.

## State and cache

- Application state: `C:\Dev\WorkspaceBootstrap\state`
- Logs: `C:\Dev\WorkspaceBootstrap\logs`
- Installer cache: `C:\DevCache`
- Cache staging: `C:\DevCache\staging`
- Cache metadata: `C:\DevCache\metadata`

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

Provisioning is idempotent and observable. Destructive operations require explicit confirmation. Reboot-required operations and failures must remain recoverable rather than being silently retried or duplicated.

## CI architecture

All GitHub Actions jobs run on the project's GitHub-hosted Windows runner. Repository validation must never depend on GitHub-hosted operating systems.
