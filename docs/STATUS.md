# Status

## Architecture

The project is in the native C#/.NET 10 cutover.

The historical PowerShell, batch and Linux provisioning implementations are retired. The product path is:

```text
Windows UI / CLI
      ↓
C# Engine
      ↓
Catalog → Resolver → Cache → Verification → Provisioning → State/Recovery
```

## Current branch

`refactor/native-csharp-cutover`

This branch consolidates the product identity, native Engine, desktop project, installer/cache semantics, provisioning worker, repository policy tests and GitHub-hosted CI.

## Validation policy

The exact PR head must pass:

- repository policy tests;
- Debug and Release builds;
- unit/integration tests;
- formatting validation;
- self-contained CLI and desktop publication;
- CodeQL;
- dependency review on pull requests.

No local machine or self-hosted runner is required for CI.
