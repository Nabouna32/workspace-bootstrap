# Configuration portability and machine bootstrap

## Purpose

The product is intended to rebuild a fresh Windows installation from a known desired environment. A future Configuration Profile describes desired state independently from one physical machine.

The first transport is local JSON export/import. An authenticated account may later synchronize encrypted configuration and non-secret preferences.

## Configuration bundle

A versioned configuration bundle may contain profile selection, components and toolchains, supported version policies, Windows/WSL preferences, maintenance and optimization preferences, scheduler/resource policies, language preferences and project/toolchain profiles.

Version selection is explicit per component. The user may choose `latest`, `exact`, `minimum`, `range`, or `channel` when the component supports those semantics. An exact version is a real desired-state constraint, not a hint: if it is unavailable or incompatible, the job must surface a visible conflict instead of silently installing another version.

Example:

```json
{
  "schemaVersion": 1,
  "profileId": "developer",
  "components": [
    { "id": "base" },
    { "id": "development-extended" },
    { "id": "java", "version": { "mode": "exact", "value": "21.0.8" } },
    { "id": "flutter", "version": { "mode": "minimum", "value": "3.47.2" } }
  ],
  "policies": {
    "network": { "mode": "limited", "maxDownloadBytesPerSecond": 10485760 }
  }
}
```

The exact schema will be introduced separately and must be schema-validated.

## Machine-specific state

The bundle expresses intent; it must not blindly copy machine-specific state. The importer re-detects hardware, installed software, capabilities, generated identifiers and other target-machine facts.

## Secrets and credentials

Plain-text passwords, access tokens, private keys and other credentials must not be exported into ordinary JSON. Export defaults to excluding secrets.

A future secure mechanism may use the Windows credential store, an encrypted secret container or an authenticated account-backed vault. A setting, a secret reference and a secret are distinct data classes.

## Import workflow

Import is preview-first:

1. validate schema and compatibility;
2. show requested configuration;
3. compare desired state with the target machine;
4. show conflicts and machine-specific differences;
5. generate independent persistent jobs;
6. execute through the scheduler;
7. verify capabilities after operations;
8. retain job/run logs and recovery state.

Import is intended to be idempotent: repeated import converges on the desired state instead of blindly reinstalling everything.

## Failure and recovery

An operating-system restore cannot be one atomic transaction. Each concrete operation is therefore a persistent job. A failed job affects its dependents, while unrelated work may continue. Crash/reboot recovery verifies actual machine state before resuming.

## Account synchronization

An authenticated account may eventually provide encrypted configuration backup, history, device registration and optional synchronization of non-secret preferences. The account is a synchronization layer, not the authority for machine capabilities.

Local JSON export/import comes first so the feature remains useful without a backend.

## Compatibility and auditability

Bundles carry schema version and optional compatibility/migration metadata. Unsafe or incompatible configuration must fail validation before jobs are enqueued.

Every import receives an operation ID so history can answer what was requested, what was already compliant, what changed, what failed and what remains pending.
