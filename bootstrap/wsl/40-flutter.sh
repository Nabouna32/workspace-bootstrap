#!/usr/bin/env bash
set -Eeuo pipefail
FLUTTER_ROOT="$HOME/dev-tools/flutter"
RELEASES_URL="https://storage.googleapis.com/flutter_infra_release/releases/releases_linux.json"
mkdir -p "$HOME/dev-tools"

if [[ ! -x "$FLUTTER_ROOT/bin/flutter" ]]; then
  manifest="$(curl -fsSL "$RELEASES_URL")"
  current_hash="$(printf '%s\n' "$manifest" | jq -r '.current_release.stable')"
  release="$(printf '%s\n' "$manifest" | jq -r --arg hash "$current_hash" '.releases[] | select(.channel == "stable" and .hash == $hash) | @base64' | head -n 1)"
  [[ -n "$release" ]] || { echo "Unable to resolve Flutter stable." >&2; exit 1; }
  version="$(printf '%s' "$release" | base64 -d | jq -r '.version')"
  archive="$(printf '%s' "$release" | base64 -d | jq -r '.archive')"
  sha256="$(printf '%s' "$release" | base64 -d | jq -r '.sha256')"
  base_url="$(printf '%s\n' "$manifest" | jq -r '.base_url')"
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' EXIT
  curl -fsSL "$base_url/$archive" -o "$tmp/flutter.tar.xz"
  actual="$(sha256sum "$tmp/flutter.tar.xz" | awk '{print $1}')"
  [[ "$actual" == "$sha256" ]] || { echo "Flutter SHA-256 mismatch." >&2; exit 1; }
  staging_root="$(mktemp -d "$HOME/dev-tools/.flutter-staging.XXXXXX")"
  trap 'rm -rf "$staging_root" "$tmp"' EXIT
  tar -xJf "$tmp/flutter.tar.xz" --strip-components=1 -C "$staging_root"
  [[ -x "$staging_root/bin/flutter" ]] || { echo "Flutter archive extraction is incomplete." >&2; exit 1; }
  if [[ -e "$FLUTTER_ROOT" ]]; then
    echo "Flutter target exists but is incomplete; refusing to replace it automatically: $FLUTTER_ROOT" >&2
    exit 1
  fi
  mv "$staging_root" "$FLUTTER_ROOT"
  trap 'rm -rf "$tmp"' EXIT
  echo "Flutter $version installed."
fi

grep -Fq "# dev-environment: flutter" "$HOME/.bashrc" 2>/dev/null || {
  # shellcheck disable=SC2016
printf '\n# dev-environment: flutter\nexport PATH="%s/bin:$PATH"\n' "$FLUTTER_ROOT" >> "$HOME/.bashrc"
}
export PATH="$FLUTTER_ROOT/bin:$PATH"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
export ANDROID_SDK_ROOT="$ANDROID_HOME"
export PATH="$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$PATH"

flutter config --no-analytics
flutter config --enable-linux-desktop
flutter config --enable-android
flutter precache --android --linux
flutter --version
flutter devices
