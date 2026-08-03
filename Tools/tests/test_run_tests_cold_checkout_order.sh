#!/bin/bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUN_TESTS_LIBRARY_ONLY=1 source "$REPO_ROOT/run_tests.sh"

TEST_ROOT="$(mktemp -d)"
trap 'rm -rf -- "$TEST_ROOT"' EXIT

PROJECT_PATH_WSL="$TEST_ROOT"
DRY_RUN=0
CALLS=()

record_dotnet() {
    CALLS+=(dotnet)
}

record_unity_and_generate_projects() {
    CALLS+=(unity)
    touch "$PROJECT_PATH_WSL/Game.Feature.Gameplay.Tests.csproj"
    touch "$PROJECT_PATH_WSL/Game.Feature.Gameplay.PlayModeTests.csproj"
}

assert_calls() {
    local expected="$1"
    local actual="${CALLS[*]}"

    if [ "$actual" != "$expected" ]; then
        echo "Expected call order '$expected' but observed '$actual'." >&2
        exit 1
    fi
}

run_dotnet_and_unity_lane core record_dotnet record_unity_and_generate_projects
assert_calls "unity dotnet"

CALLS=()
run_dotnet_and_unity_lane core record_dotnet record_unity_and_generate_projects
assert_calls "dotnet unity"

CALLS=()
rm -f -- "$PROJECT_PATH_WSL/Game.Feature.Gameplay.Tests.csproj"
rm -f -- "$PROJECT_PATH_WSL/Game.Feature.Gameplay.PlayModeTests.csproj"
DRY_RUN=1
run_dotnet_and_unity_lane core record_dotnet record_unity_and_generate_projects
assert_calls "dotnet unity"

echo "run_tests cold-checkout ordering tests passed"
