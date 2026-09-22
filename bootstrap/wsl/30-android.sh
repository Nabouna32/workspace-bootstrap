#!/usr/bin/env bash
set -Eeuo pipefail
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
export ANDROID_SDK_ROOT="$ANDROID_HOME"
export PATH="$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$HOME/.local/bin:$PATH"
mkdir -p "$ANDROID_HOME"
printf '%s\n%s\n' '--no-metrics' "--sdk=$ANDROID_HOME" > "$HOME/.androidrc"

if ! command -v android >/dev/null 2>&1; then
  sudo install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://dl.google.com/linux/linux_signing_key.pub | sudo tee /etc/apt/keyrings/google.asc >/dev/null
  sudo chmod a+r /etc/apt/keyrings/google.asc
  sudo tee /etc/apt/sources.list.d/android-cli.list >/dev/null <<'EOF'
Types: deb
URIs: http://dl.google.com/android/cli/latest/debian/
Suites: stable
Components: main
Architectures: amd64
Signed-By: /etc/apt/keyrings/google.asc
EOF
  sudo apt-get update
  sudo apt-get install -y android-cli
fi

android --sdk="$ANDROID_HOME" sdk install platform-tools
android --sdk="$ANDROID_HOME" sdk install emulator
android --sdk="$ANDROID_HOME" sdk install "platforms/android-36"
android --sdk="$ANDROID_HOME" sdk install "build-tools/36.1.0"
android --sdk="$ANDROID_HOME" sdk install "ndk/29.0.14206865"
android --sdk="$ANDROID_HOME" sdk install "system-images/android-36/google_apis/x86_64"

grep -Fq "# dev-environment: android" "$HOME/.bashrc" 2>/dev/null || {
  # shellcheck disable=SC2016
printf '\n# dev-environment: android\nexport ANDROID_HOME="%s"\nexport ANDROID_SDK_ROOT="%s"\nexport PATH="$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$PATH"\n' "$ANDROID_HOME" "$ANDROID_SDK_ROOT" >> "$HOME/.bashrc"
}

if ! android --sdk="$ANDROID_HOME" emulator list 2>/dev/null | grep -q "medium_phone"; then
  android --sdk="$ANDROID_HOME" emulator create --profile=medium_phone
fi

adb version
emulator -version | head -n 1
android --version
