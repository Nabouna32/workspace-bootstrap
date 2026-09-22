# Logging and diagnostics

Logging is a configurable product subsystem.

## User control
Users can configure persistent logging level, retention, diagnostic detail and export behavior.

## Operation logs
Each long-running operation has an id, ordered steps, timestamps, result, provider/source, diagnostics and error information.

## Privacy
Logs must not contain passwords, access tokens, API keys or authentication cookies. Exported diagnostics should minimize sensitive machine details where possible.

## UX
Normal UI shows understandable summaries. Technical details expose provider, mechanism, raw error, relevant Windows evidence and exportable logs.

## Failure
Failures remain visible. Never suppress exceptions merely to keep the interface green.