#!/usr/bin/env bash
set -Eeuo pipefail
echo "=== WSL preflight ==="
id -un
uname -a
# shellcheck disable=SC1091
. /etc/os-release
echo "$PRETTY_NAME"
if [[ "$(id -u)" -eq 0 ]]; then
  echo "Do not run the bootstrap as root."
  exit 1
fi
