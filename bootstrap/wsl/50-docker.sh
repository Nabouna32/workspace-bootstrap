#!/usr/bin/env bash
set -Eeuo pipefail

if ! command -v docker >/dev/null 2>&1; then
  # shellcheck disable=SC1091
. /etc/os-release

  if [[ "${ID:-}" != "ubuntu" ]]; then
    echo "Docker bootstrap currently supports official Ubuntu releases only." >&2
    exit 1
  fi

  sudo apt-get update
  sudo apt-get install -y ca-certificates curl

  sudo install -m 0755 -d /etc/apt/keyrings
  sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  sudo chmod a+r /etc/apt/keyrings/docker.asc

  sudo tee /etc/apt/sources.list.d/docker.sources >/dev/null <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: ${UBUNTU_CODENAME:-$VERSION_CODENAME}
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF

  sudo apt-get update
  sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi

sudo systemctl enable --now docker

if ! id -nG | tr ' ' '
' | grep -qx docker; then
  sudo usermod -aG docker "$USER"
  echo "Docker group membership added for $USER. WSL restart is required."
  exit 11
fi

echo "Docker:"
docker --version
docker compose version
docker buildx version
