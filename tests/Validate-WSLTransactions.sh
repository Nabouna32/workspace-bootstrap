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

# Archive-based installs validate in temporary storage before finalization.
check bootstrap/wsl/20-node.sh 'staging_root=.*mktemp' 'Node uses a staging directory'
check bootstrap/wsl/20-node.sh 'trap .*tmp' 'Node cleans temporary material'
check bootstrap/wsl/20-node.sh 'sha256sum' 'Node verifies the downloaded archive'
check bootstrap/wsl/40-flutter.sh 'staging_root=.*mktemp' 'Flutter uses a staging directory'
check bootstrap/wsl/40-flutter.sh 'trap .*staging_root.*tmp' 'Flutter cleans staging and temporary material'
check bootstrap/wsl/40-flutter.sh 'sha256sum' 'Flutter verifies the downloaded archive'
# Never silently delete an existing final installation root.
if grep -Eq 'rm -rf "\$NODE_ROOT"|rm -rf "\$FLUTTER_ROOT"|rm -rf "\$RUNNER_ROOT"' bootstrap/wsl/20-node.sh bootstrap/wsl/40-flutter.sh bootstrap/wsl/70-github-runner.sh; then
  echo "FAIL an archive installer deletes an existing final installation root" >&2
  fail=1
else
  echo "OK  archive installers do not blindly delete final installation roots"
fi

exit "$fail"
