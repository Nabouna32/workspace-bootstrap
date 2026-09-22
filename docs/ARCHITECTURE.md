# Development Environment Architecture

GitHub repository `NabounaLab/dev-environment` is the source of truth.

## Windows 11 Pro

- Windows-native Flutter builds
- MSVC / Windows SDK / MSBuild
- Windows smoke tests
- WSL lifecycle

## WSL2 Ubuntu

- Node.js / npm / pnpm / Yarn
- Flutter Android
- Android SDK/emulator
- Docker Engine / Compose / BuildKit
- Playwright browsers
- Git / GitHub CLI
- self-hosted GitHub Actions runner

## CI

```
Git push -> GitHub -> self-hosted runner -> quality -> integration
         -> Android -> Windows -> Playwright E2E -> security -> artifacts
```

Vercel remains useful for production deployment, but daily development validation should run locally/self-hosted.

## Principles

- reproducible
- idempotent
- root-cause fixes
- stable toolchains
- explicit secrets handling
- branches/PRs
- verification after installation
