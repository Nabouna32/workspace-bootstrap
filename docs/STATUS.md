# Status

## Architecture

Workspace Bootstrap is a native C#/.NET 10 Windows application.

The product path is:

```text
Windows UI / CLI
      ↓
C# Engine
      ↓
Catalog → Resolver → Cache → Verification → Inventory → Provisioning → State/Recovery
```

## Current development state

- Native C#/.NET 10 architecture is established.
- WPF desktop and self-contained Windows x64 CLI share the Engine.
- Provisioning plans now consume live Windows inventory and expose Installed/Missing/Outdated states.
- Provisioning has durable state, a serialized worker lock and recoverable worker startup failures.
- Installer artifacts use isolated staging, SHA-256 verification and Authenticode validation.
- Cache metadata is revalidated before an artifact is trusted.
- Windows optimization state is package-local and persisted atomically.
- The desktop has an explicit WPF exception boundary with diagnostic logging.
- GitHub Actions is configured to execute validation on the project's self-hosted Windows runner.
- Windows releases are explicit via manual dispatch or `v*` tags.

## Validation cadence

### Every pull request

- NuGet restore with cache;
- Release compilation with warnings treated as errors;
- automated tests;
- formatting verification.

### Weekly / manual full validation

- Debug and Release compilation;
- tests;
- formatting;
- self-contained Windows publication;
- portable package smoke test.

### Release

- Release build and tests;
- self-contained Windows x64 package;
- package smoke test;
- SHA-256 checksum;
- GitHub Release for tagged builds.

### Security

CodeQL remains a separate scheduled/manual validation rather than part of the fast development loop.

## Next product focus

1. Complete the desktop information architecture and component/profile experience.
2. Strengthen installer cache lifecycle, retention and recovery UX.
3. Expand provisioning/job integration tests around cancellation, resume, reboot and failure recovery.
4. Add published-artifact startup/integration coverage beyond the CLI contract smoke test.
5. Continue dead-code and retired-script cleanup.
6. Improve inventory matching and version comparison for components whose installers expose richer version metadata.
