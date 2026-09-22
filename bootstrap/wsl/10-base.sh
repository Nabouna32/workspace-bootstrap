#!/usr/bin/env bash
set -Eeuo pipefail

sudo apt-get update
if ! command -v gh >/dev/null 2>&1; then
  sudo install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | sudo tee /etc/apt/keyrings/githubcli-archive-keyring.gpg >/dev/null
  sudo chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg
  sudo tee /etc/apt/sources.list.d/github-cli.sources >/dev/null <<'EOF'
Types: deb
URIs: https://cli.github.com/packages
Suites: stable
Components: main
Architectures: amd64
Signed-By: /etc/apt/keyrings/githubcli-archive-keyring.gpg
EOF
  sudo apt-get update
fi

# WSL is intentionally Linux-first. Keep the base small and avoid duplicating
# Windows-owned Android/Flutter/Java toolchains here.
sudo apt-get install -y   ca-certificates curl wget unzip zip xz-utils jq git git-lfs   build-essential clang cmake ninja-build pkg-config   python3 python3-pip python3-venv shellcheck gh

git lfs install

echo "WSL Linux base packages installed."
