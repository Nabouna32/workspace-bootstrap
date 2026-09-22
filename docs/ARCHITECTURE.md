# Architecture

Workspace Bootstrap is a Windows 11 workstation provisioning and recovery application.

## Runtime

- C# / .NET 10 is the application technology.
- The shared Engine owns domain logic, Windows integration, provisioning, installer resolution, cache, verification, diagnostics, maintenance, optimization and operation recovery.
- The desktop application is a native Windows presentation layer over the Engine.
- The CLI is a self-contained Windows x64 entry point over the same Engine.

## Boundaries

```
Desktop / CLI
     │
     ▼
WorkspaceBootstrap.Engine
     ├── Configuration
     ├── Catalog / Profiles
     ├── Inventory / Diagnostics
     ├── Installer resolution
     ├── Cache / Verification
     ├── Provisioning / Recovery
     └── Windows integration
```

External vendor installers, WinGet, Windows APIs, Registry and filesystem access are infrastructure boundaries. Their use is orchestrated by C#.

## Portable package

The application resolves bundled configuration and mutable state relative to `AppContext.BaseDirectory`. A published package can therefore be copied as a unit without requiring a per-user application-data directory or a fixed development path.

## CI

GitHub Actions validates, tests, publishes and security-scans the repository on GitHub-hosted Windows runners.
