#!/usr/bin/env bash
set -Eeuo pipefail

fail=0
check() {
  local file="$1" pattern="$2" description="$3"
  if grep -Eq "$pattern" "$file"; then
    echo "OK  $description"
  else
    echo "FAIL $description" >&2
    fail=1
  fi
}

# Completion is transactional: an old marker is invalidated before a new run
# and recreated only after final verification succeeds.
check bootstrap/wsl/bootstrap.sh 'rm -f "\$MARKER"' 'WSL bootstrap invalidates a stale completion marker'
if awk '/run_step "Validation finale WSL"/ { verify=NR } /touch "\$MARKER"/ { marker=NR } END { exit !(verify > 0 && marker > verify) }' bootstrap/wsl/bootstrap.sh; then
  echo "OK  completion marker follows final verification"
else
  echo "FAIL completion marker does not follow final verification" >&2
  fail=1
fi
check bootstrap/wsl/bootstrap.sh 'run_step "Validation finale WSL" "90-verify.sh"' 'WSL bootstrap performs final verification'
check bootstrap/wsl/bootstrap.sh 'touch "\$MARKER"' 'WSL bootstrap writes completion marker after verification'

# Resume configuration must be persisted before component execution and validated
# when an existing state file is reused.
check bootstrap/wsl/bootstrap.sh 'STATE_FILE=.*wsl-config\.env' 'WSL bootstrap has persistent configuration state'
check bootstrap/wsl/bootstrap.sh 'Invalid or incomplete WSL state' 'WSL bootstrap rejects invalid persisted state'
check bootstrap/wsl/bootstrap.sh 'rm -f "\$STATE_FILE"' 'WSL bootstrap discards invalid persisted state'

exit "$fail"
