# Prerequisites

Workspace Bootstrap targets supported Windows 11 x64 systems.

The Engine can inspect:

- operating-system version and build;
- processor architecture;
- available storage and memory;
- administrator capability;
- required files and commands;
- Windows components;
- Visual Studio / Build Tools capabilities where a component requires them;
- WinGet availability when a component declares it as fallback.

Component-specific prerequisites are declared in their manifests and evaluated before execution.

A failed prerequisite produces a visible diagnostic and does not silently substitute an incompatible version or source.
