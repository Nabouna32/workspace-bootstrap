# Configuration

Configuration is declarative and versioned.

## Bundle

A configuration bundle may contain:

- profile selection;
- component selection;
- supported version policies;
- maintenance preferences;
- optimization preferences;
- scheduler/resource preferences;
- language preferences;
- project/toolchain preferences.

Version selection is explicit per component. Supported policies include `latest`, `exact`, `minimum`, `range` and `channel` where declared.

An exact version is a desired-state constraint. If it is unavailable or incompatible, the operation reports a visible conflict rather than silently selecting another version.

## State

Persistent application state lives under `C:\Dev\WorkspaceBootstrap`. Installer artifacts live under `C:\DevCache`.

Secrets and registration tokens are never stored in configuration bundles.
