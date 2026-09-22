# Status

## Architecture

Workspace Bootstrap is a native C#/.NET 10 Windows application.

The product path is:

```text
Windows UI / CLI
      ↓
C# Engine
      ↓
Catalog → Resolver → Cache → Verification → Provisioning → State/Recovery
```

## Current branch

`refactor/native-csharp-cutover`

## Validation

The repository validates:

- Debug and Release builds;
- automated tests;
- formatting;
- self-contained Windows publication;
- CodeQL security analysis.

The published desktop package contains the desktop executable, CLI and bundled application assets.
