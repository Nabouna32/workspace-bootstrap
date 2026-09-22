# Workspace Control desktop

The desktop client is being migrated from legacy WPF to **WinUI 3 / Windows App SDK** on .NET 10.

## UX
Modern Windows 11 Fluent-inspired interface, System/Light/Dark themes, semantic colors, Simple/Advanced/Expert presentation levels, progressive disclosure, accessibility and clear operation recovery.

## Architecture
The desktop is presentation only. It consumes the same application/domain capabilities as the CLI and future API. It must not implement registry, installer, inventory or provisioning logic.

## Migration rule
Do not add new product capabilities to the legacy WPF shell. New UI work targets WinUI 3.