# Logging and diagnostics

Each run is isolated under bootstrap/windows/logs/<run-id>/.

- summary.json records ordered steps, status, duration and exit code.
- Each component gets its own complete transcript.
- A failed component stops the pipeline immediately. Later components are not executed.
- Installers must remain idempotent so a rerun safely rechecks and repairs the failed step.
- Paths, usernames, versions and normal environment details remain visible for debugging.
- Sanitized diagnostics redact tokens, bearer credentials, API keys, passwords and similar secrets.
- Sanitized diagnostics are bounded to 5 MiB total and 1 MiB per file by default.
- Cleanup never deletes diagnostic logs.