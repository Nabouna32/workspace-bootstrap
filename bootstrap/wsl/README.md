# WSL bootstrap

WSL owns the complete Android/Flutter Android toolchain and the Linux-side web/CI stack.

The Windows launcher starts `bootstrap.sh`, which is interactive and persists choices under `~/.config/dev-environment/wsl-config.env`.

Android SDK, Platform-Tools, Emulator, API 36, Build-Tools and NDK are **WSL-only**. Windows does not install Android Studio, Android SDK, Android Emulator or Android Java/Gradle tooling.

KVM permissions are repaired at the source. The bootstrap adds the user to the existing `kvm` group and the Windows orchestrator restarts WSL automatically when the new group membership requires a new session. It never makes `/dev/kvm` world-writable.

Windows retains only native Windows tooling such as Visual Studio Build Tools + its Windows SDK components for Flutter Windows.

When enabled, the WSL bootstrap downloads and configures the Linux GitHub Actions runner as a systemd service. GitHub CLI authentication remains user-owned; the runner registration token is generated just-in-time and never stored in the repository.