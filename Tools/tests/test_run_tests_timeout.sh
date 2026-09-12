#!/bin/bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"
probe_root="$(mktemp -d)"
trap 'rm -rf "$probe_root"' EXIT

assert_contains() {
    if ! grep -F -- "$2" "$1" >/dev/null; then
        echo "Missing expected timeout contract: $2" >&2
        cat "$1" >&2
        exit 1
    fi
}

env -u UNITY_TEST_TIMEOUT_SECONDS ./run_tests.sh --print-config > "$probe_root/default-config"
assert_contains "$probe_root/default-config" 'UNITY_TEST_TIMEOUT_SECONDS=285'
assert_contains "$probe_root/default-config" 'UNITY_TEST_PROCESS_TIMEOUT_SECONDS=300'
env -u UNITY_TEST_TIMEOUT_SECONDS ./run_tests.sh --dry-run core > "$probe_root/default-plan"
assert_contains "$probe_root/default-plan" 'timeout --kill-after=10 300 '
assert_contains "$probe_root/default-plan" '-codexTestTimeoutSeconds 285'

for budget in 1 900 2147483632; do
    UNITY_TEST_TIMEOUT_SECONDS="$budget" ./run_tests.sh --print-config > "$probe_root/valid-config"
    assert_contains "$probe_root/valid-config" "UNITY_TEST_TIMEOUT_SECONDS=$budget"
    assert_contains "$probe_root/valid-config" "UNITY_TEST_PROCESS_TIMEOUT_SECONDS=$((budget + 15))"
done
UNITY_TEST_TIMEOUT_SECONDS=900 ./run_tests.sh --dry-run core > "$probe_root/extended-plan"
assert_contains "$probe_root/extended-plan" 'timeout --kill-after=10 915 '
assert_contains "$probe_root/extended-plan" '-codexTestTimeoutSeconds 900'

for invalid in '' 0 -1 +1 01 ' 900' '900 ' '1.5' abc 2147483633 999999999999999999999; do
    if UNITY_TEST_TIMEOUT_SECONDS="$invalid" ./run_tests.sh --dry-run core > "$probe_root/invalid" 2>&1; then
        echo "Invalid timeout unexpectedly accepted: '$invalid'" >&2
        exit 1
    fi
    assert_contains "$probe_root/invalid" 'UNITY_TEST_TIMEOUT_SECONDS must be an integer'
done

echo 'run_tests.sh timeout default, opt-in, boundary, and invalid-input checks passed'
