#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

EXPECTED_PROJECT_PATH_WIN="$(wslpath -w "$ROOT_DIR")"
printf -v EXPECTED_PROJECT_PATH_WIN_SHELL '%q' "$EXPECTED_PROJECT_PATH_WIN"
EXPECTED_SOLUTION="$(find "$ROOT_DIR" -maxdepth 1 -type f -name '*.sln' -printf '%f\n' | sort)"
PRINT_CONFIG_OUTPUT="$(mktemp)"
MISMATCH_OUTPUT="$(mktemp)"
CORE_DRY_RUN_OUTPUT="$(mktemp)"
FULL_DRY_RUN_OUTPUT="$(mktemp)"
HEAD_CLIMATE_CONTRACT_OUTPUT="$(mktemp)"
CANDIDATE_CLIMATE_CONTRACT_OUTPUT="$(mktemp)"
trap 'rm -f "$PRINT_CONFIG_OUTPUT" "$MISMATCH_OUTPUT" "$CORE_DRY_RUN_OUTPUT" "$FULL_DRY_RUN_OUTPUT" "$HEAD_CLIMATE_CONTRACT_OUTPUT" "$CANDIDATE_CLIMATE_CONTRACT_OUTPUT"' EXIT

assert_contains() {
    local path="$1"
    local expected="$2"

    if ! grep -F -- "$expected" "$path" >/dev/null; then
        echo "Expected output to contain: $expected"
        echo "---- output ----"
        cat "$path"
        exit 1
    fi
}

assert_not_contains() {
    local path="$1"
    local unexpected="$2"

    if grep -F -- "$unexpected" "$path" >/dev/null; then
        echo "Expected output not to contain: $unexpected"
        echo "---- output ----"
        cat "$path"
        exit 1
    fi
}

bash -n run_tests.sh

sed -n '/^verify_climate_committed_source_integrity() {/,/^}/p' run_tests.sh > "$HEAD_CLIMATE_CONTRACT_OUTPUT"
assert_contains "$HEAD_CLIMATE_CONTRACT_OUTPUT" 'head_climate_sdf_sha256="$('
assert_contains "$HEAD_CLIMATE_CONTRACT_OUTPUT" 'git_head_runner_constant CLIMATE_COMMITTED_SDF_SHA256'

sed -n '/^verify_climate_worktree_source_integrity() {/,/^}/p' run_tests.sh > "$CANDIDATE_CLIMATE_CONTRACT_OUTPUT"
assert_contains "$CANDIDATE_CLIMATE_CONTRACT_OUTPUT" 'candidate_sdf_hash="$('
assert_contains "$CANDIDATE_CLIMATE_CONTRACT_OUTPUT" 'if [ "$candidate_sdf_hash" != "$CLIMATE_COMMITTED_SDF_SHA256" ]; then'

./run_tests.sh --print-config > "$PRINT_CONFIG_OUTPUT"
assert_contains "$PRINT_CONFIG_OUTPUT" "PROJECT_PATH_WSL=$ROOT_DIR"
assert_contains "$PRINT_CONFIG_OUTPUT" "PROJECT_PATH_WIN=$EXPECTED_PROJECT_PATH_WIN"
assert_contains "$PRINT_CONFIG_OUTPUT" "RESULT_DIR=$ROOT_DIR/TestResults"
assert_not_contains "$PRINT_CONFIG_OUTPUT" "ALL TESTS PASSED"

if PROJECT_PATH_WIN='C:\wrong\path' ./run_tests.sh --print-config > "$MISMATCH_OUTPUT" 2>&1; then
    echo "Expected wrong PROJECT_PATH_WIN override to fail"
    cat "$MISMATCH_OUTPUT"
    exit 1
fi
assert_contains "$MISMATCH_OUTPUT" "ERROR: PROJECT_PATH_WIN does not match current worktree."
assert_contains "$MISMATCH_OUTPUT" "PROJECT_PATH_WSL"
assert_contains "$MISMATCH_OUTPUT" "Expected PROJECT_PATH_WIN"
assert_contains "$MISMATCH_OUTPUT" "Actual PROJECT_PATH_WIN"

./run_tests.sh --dry-run core > "$CORE_DRY_RUN_OUTPUT"
assert_contains "$CORE_DRY_RUN_OUTPUT" "-projectPath $EXPECTED_PROJECT_PATH_WIN_SHELL"
assert_contains "$CORE_DRY_RUN_OUTPUT" "Would run Windows dotnet build:"
assert_contains "$CORE_DRY_RUN_OUTPUT" "Would run Unity core (EditMode):"
assert_not_contains "$CORE_DRY_RUN_OUTPUT" "ALL TESTS PASSED"

if [ -n "$EXPECTED_SOLUTION" ]; then
    ./run_tests.sh --dry-run full > "$FULL_DRY_RUN_OUTPUT"
    assert_contains "$FULL_DRY_RUN_OUTPUT" "Would run Windows dotnet build:"
    assert_contains "$FULL_DRY_RUN_OUTPUT" "$EXPECTED_SOLUTION"
    assert_contains "$FULL_DRY_RUN_OUTPUT" "-projectPath $EXPECTED_PROJECT_PATH_WIN_SHELL"
    assert_not_contains "$FULL_DRY_RUN_OUTPUT" "ALL TESTS PASSED"
fi

echo "run_tests.sh worktree path checks passed"
