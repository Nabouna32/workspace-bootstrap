# Workspace Control — Security Model

Workspace Control performs powerful Windows administration, so dangerous operations must be constrained, explicit and auditable.

## Least privilege
The desktop normally runs unelevated. A small privileged component handles operations that require administrator rights.

The privileged boundary accepts structured commands, validates arguments, exposes only allow-listed capabilities, never executes arbitrary UI-provided shell text and returns structured results.

An explicitly elevated application session may be offered as a convenience. It does not replace least privilege.

## Downloads
Artifacts are staged, hashed, checked against authoritative digests when available, Authenticode-validated where applicable, atomically promoted and recorded with provenance.

## Registry and policies
Registry-backed settings are labelled according to evidence. Undocumented tweaks are never presented as official Windows policy.

## Irreversible actions
Irreversible operations clearly state consequences and cannot be hidden behind generic confirmation. Reversible mechanisms and backups are preferred where technically appropriate.

## Logs
Logging is configurable. Logs never contain passwords, tokens or API keys.

## Plugins
Plugins require metadata, compatibility and declared permissions. Untrusted plugins must not receive unrestricted administrator access.

## Cloud
Cloud is optional. Authentication material is never embedded in portable profiles or ordinary logs.