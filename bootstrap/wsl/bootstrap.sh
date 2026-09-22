#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
STATE_DIR="$HOME/.config/dev-environment"
STATE_FILE="$STATE_DIR/wsl-config.env"
MARKER="$STATE_DIR/bootstrap-complete"
mkdir -p "$STATE_DIR"
rm -f "$MARKER"

if [[ "$(id -u)" -eq 0 ]]; then echo "Run the WSL bootstrap as the normal Ubuntu user, not root." >&2; exit 1; fi

WSL_CONF_SOURCE="$(cd "$ROOT/../.." && pwd)/config/wsl/wsl.conf"
if [[ -f "$WSL_CONF_SOURCE" ]]; then
  if [[ ! -f /etc/wsl.conf ]] || ! cmp -s "$WSL_CONF_SOURCE" /etc/wsl.conf; then
    sudo install -m 0644 "$WSL_CONF_SOURCE" /etc/wsl.conf
    echo "WSL configuration updated from repository. WSL restart is required."
    exit 11
  fi
fi

echo ""
echo "╔════════════════════════════════════════════════════╗"
echo "║   🐧 Dev Environment — Linux tools for WSL       ║"
echo "╚════════════════════════════════════════════════════╝"
echo ""
echo "Windows is the primary development environment."
echo "WSL installs only Linux-first tooling."

ask_yes_no() {
  local prompt="$1" default="$2" answer
  while true; do
    if [[ "$default" == "yes" ]]; then read -r -p "$prompt [O/n] " answer || true; answer="${answer:-o}"
    else read -r -p "$prompt [o/N] " answer || true; answer="${answer:-n}"; fi
    case "${answer,,}" in o|oui|y|yes) echo "yes"; return ;; n|non|no) echo "no"; return ;; *) echo "Répondez par O ou N." ;; esac
  done
}

if [[ ! -f "$STATE_FILE" ]]; then
  node_enabled="$(ask_yes_no "Installer Node.js + pnpm + Yarn pour les projets Linux ?" "no")"
  docker_enabled="$(ask_yes_no "Installer Docker Engine + Compose dans WSL ?" "no")"
  playwright_enabled="$(ask_yes_no "Installer Playwright + navigateurs dans WSL ?" "no")"
  cat > "$STATE_FILE" <<EOF
node_enabled="$node_enabled"
docker_enabled="$docker_enabled"
playwright_enabled="$playwright_enabled"
EOF
else
  echo "Configuration précédente détectée :"
  grep -E '^(node_enabled|docker_enabled|playwright_enabled)=' "$STATE_FILE" || true
  for key in node_enabled docker_enabled playwright_enabled; do
    if ! grep -qE "^\${key}=(yes|no)$|^\${key}=\"(yes|no)\"$" "$STATE_FILE"; then
      echo "Invalid or incomplete WSL state: missing/invalid $key" >&2
      rm -f "$STATE_FILE"
      echo "The interactive WSL configuration will be recreated on the next run."
      exit 1
    fi
  done
  # shellcheck disable=SC1090
  source "$STATE_FILE"
fi

run_step() { local label="$1" script="$2"; echo ""; echo "▶ $label"; bash "$ROOT/$script"; }
run_step "Prérequis WSL" "00-preflight.sh"
run_step "Base Linux" "10-base.sh"
[[ "$node_enabled" == "yes" ]] && run_step "Node.js + pnpm + Yarn" "20-node.sh"
[[ "$docker_enabled" == "yes" ]] && run_step "Docker Engine + Compose + Buildx" "50-docker.sh"
[[ "$playwright_enabled" == "yes" ]] && run_step "Playwright + navigateurs" "55-playwright.sh"
run_step "Validation finale WSL" "90-verify.sh"
touch "$MARKER"
echo "✅ Configuration WSL terminée."
