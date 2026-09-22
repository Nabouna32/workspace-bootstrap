#!/usr/bin/env bash
set -Eeuo pipefail

fail=0
check_cmd() { if command -v "$1" >/dev/null 2>&1; then echo "OK $1"; else echo "MISSING $1"; fail=1; fi; }
for cmd in git gh curl jq cmake ninja python3 shellcheck; do check_cmd "$cmd"; done

config_file="$HOME/.config/dev-environment/wsl-config.env"
read_state() { local key="$1" value; value="$(grep -E "^${key}=" "$config_file" 2>/dev/null | cut -d= -f2- | tr -d '"' | tail -n1 || true)"; [[ "$value" == "yes" || "$value" == "no" ]] || value="no"; printf "%s" "$value"; }
node_enabled="no"; docker_enabled="no"; playwright_enabled="no"
if [[ -f "$config_file" ]]; then
  node_enabled="$(read_state node_enabled)"
  docker_enabled="$(read_state docker_enabled)"
  playwright_enabled="$(read_state playwright_enabled)"
fi

if [[ "$node_enabled" == "yes" ]]; then check_cmd node; check_cmd npm; check_cmd pnpm; check_cmd yarn; fi

if [[ "$playwright_enabled" == "yes" ]]; then
  if command -v playwright >/dev/null 2>&1; then playwright install --list || fail=1; else echo "MISSING playwright CLI"; fail=1; fi
fi

if [[ "$docker_enabled" == "yes" ]]; then
  if command -v docker >/dev/null 2>&1; then docker version >/dev/null 2>&1 || fail=1; docker compose version >/dev/null 2>&1 || fail=1; else echo "MISSING docker"; fail=1; fi
fi

if [[ -f "$HOME/.config/dev-environment/bootstrap-complete" ]]; then echo "OK bootstrap marker"; else echo "MISSING bootstrap marker"; fail=1; fi
exit "$fail"
