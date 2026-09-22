#!/usr/bin/env bash
set -Eeuo pipefail
NODE_MAJOR="${NODE_MAJOR:-24}"
NODE_ROOT="$HOME/.local/node-$NODE_MAJOR"
mkdir -p "$HOME/.local"

if [[ ! -x "$NODE_ROOT/bin/node" ]]; then
  index="$(curl -fsSL https://nodejs.org/dist/index.json)"
  version="$(printf '%s\n' "$index" | jq -r --arg major "v$NODE_MAJOR." '[.[] | select(.version | startswith($major)) | select(.lts != false)] | .[0].version')"
  [[ -n "$version" && "$version" != "null" ]] || { echo "Unable to resolve Node.js LTS." >&2; exit 1; }
  archive="node-$version-linux-x64.tar.xz"
  base="https://nodejs.org/dist/$version"
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' EXIT
  curl -fsSL "$base/$archive" -o "$tmp/$archive"
  curl -fsSL "$base/SHASUMS256.txt" -o "$tmp/SHASUMS256.txt"
  expected="$(awk -v f="$archive" '$2 == f {print $1}' "$tmp/SHASUMS256.txt")"
  actual="$(sha256sum "$tmp/$archive" | awk '{print $1}')"
  [[ "$actual" == "$expected" ]] || { echo "Node.js SHA-256 mismatch." >&2; exit 1; }
  staging_root="$(mktemp -d "$HOME/.local/node-$NODE_MAJOR-staging.XXXXXX")"
  cleanup_staging() {
    rm -rf "$staging_root"
  }
  trap 'cleanup_staging; rm -rf "$tmp"' EXIT
  tar -xJf "$tmp/$archive" --strip-components=1 -C "$staging_root"
  [[ -x "$staging_root/bin/node" ]] || { echo "Node.js archive extraction is incomplete." >&2; exit 1; }
  if [[ -e "$NODE_ROOT" ]]; then
    echo "Node.js target exists but is incomplete; refusing to replace it automatically: $NODE_ROOT" >&2
    exit 1
  fi
  mv "$staging_root" "$NODE_ROOT"
  trap 'rm -rf "$tmp"' EXIT
fi

grep -Fq "# dev-environment: node" "$HOME/.bashrc" 2>/dev/null || {
  # shellcheck disable=SC2016
printf '\n# dev-environment: node\nexport PATH="%s/bin:$PATH"\n' "$NODE_ROOT" >> "$HOME/.bashrc"
}
export PATH="$NODE_ROOT/bin:$PATH"
node --version
npm --version
npm install --global pnpm@latest yarn@latest
pnpm --version
yarn --version
