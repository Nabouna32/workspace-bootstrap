#!/usr/bin/env bash
set -Eeuo pipefail
command -v node >/dev/null || { echo "Node.js must be installed first." >&2; exit 1; }
npm install --global @playwright/test@latest @playwright/cli@latest
playwright --version
playwright-cli --help >/dev/null
playwright install --with-deps
