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

record_unity_failure() {
    CALLS+=(unity)
    return 42
}

record_unity_without_projects() {
    CALLS+=(unity)
}

record_dotnet_failure() {
    CALLS+=(dotnet)
    return 43
}

record_full_dotnet() {
    CALLS+=("dotnet:$(find_generated_solution_file)")
}

record_full_dotnet_failure() {
    CALLS+=("dotnet:$(find_generated_solution_file)")
    return 43
}

record_unity_and_generate_slnx() {
    CALLS+=(unity)
    touch "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").slnx"
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
rm -f -- "$PROJECT_PATH_WSL/Game.Feature.Gameplay.Tests.csproj"
rm -f -- "$PROJECT_PATH_WSL/Game.Feature.Gameplay.PlayModeTests.csproj"
run_dotnet_and_unity_lane --integration-simulation record_dotnet record_unity_and_generate_projects
assert_calls "unity dotnet"

CALLS=()
run_dotnet_and_unity_lane --integration-replay record_dotnet record_unity_and_generate_projects
assert_calls "dotnet unity"

CALLS=()
rm -f -- "$PROJECT_PATH_WSL/Game.Feature.Gameplay.Tests.csproj"
rm -f -- "$PROJECT_PATH_WSL/Game.Feature.Gameplay.PlayModeTests.csproj"
if run_dotnet_and_unity_lane --integration-fuzz record_dotnet record_unity_failure; then
    echo "Expected Unity bootstrap failure to be preserved." >&2
    exit 1
else
    status=$?
fi
if [ "$status" -ne 42 ]; then
    echo "Expected Unity bootstrap exit 42 but observed $status." >&2
    exit 1
fi
assert_calls "unity"

CALLS=()
if run_dotnet_and_unity_lane --integration-simulation record_dotnet record_unity_without_projects; then
    echo "Expected missing generated project inputs to fail." >&2
    exit 1
fi
assert_calls "unity"

CALLS=()
record_unity_and_generate_projects
CALLS=()
if run_dotnet_and_unity_lane --integration-replay record_dotnet_failure record_unity_and_generate_projects; then
    echo "Expected dotnet failure to be preserved." >&2
    exit 1
else
    status=$?
fi
if [ "$status" -ne 43 ]; then
    echo "Expected dotnet exit 43 but observed $status." >&2
    exit 1
fi
assert_calls "dotnet"

CALLS=()
rm -f -- "$PROJECT_PATH_WSL/Game.Feature.Gameplay.Tests.csproj"
rm -f -- "$PROJECT_PATH_WSL/Game.Feature.Gameplay.PlayModeTests.csproj"
DRY_RUN=1
run_dotnet_and_unity_lane core record_dotnet record_unity_and_generate_projects
assert_calls "dotnet unity"

DRY_RUN=0
CALLS=()
touch "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").sln"
run_dotnet_and_unity_lane full record_full_dotnet record_unity_and_generate_slnx
assert_calls "dotnet:$(basename "$PROJECT_PATH_WSL").sln unity"

CALLS=()
rm -f -- "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").sln"
touch "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").slnx"
run_dotnet_and_unity_lane full record_full_dotnet record_unity_and_generate_slnx
assert_calls "dotnet:$(basename "$PROJECT_PATH_WSL").slnx unity"

CALLS=()
touch "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").sln"
run_dotnet_and_unity_lane full record_full_dotnet record_unity_and_generate_slnx
assert_calls "dotnet:$(basename "$PROJECT_PATH_WSL").sln unity"

CALLS=()
rm -f -- "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").sln" \
    "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").slnx"
run_dotnet_and_unity_lane full record_full_dotnet record_unity_and_generate_slnx
assert_calls "unity dotnet:$(basename "$PROJECT_PATH_WSL").slnx"

CALLS=()
rm -f -- "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").slnx"
touch "$PROJECT_PATH_WSL/unexpected-a.sln" "$PROJECT_PATH_WSL/unexpected-b.sln"
if run_dotnet_and_unity_lane full record_full_dotnet record_unity_and_generate_slnx; then
    echo "Expected ambiguous generated solution inputs to fail." >&2
    exit 1
fi
assert_calls ""

CALLS=()
rm -f -- "$PROJECT_PATH_WSL/unexpected-a.sln" "$PROJECT_PATH_WSL/unexpected-b.sln"
touch "$PROJECT_PATH_WSL/$(basename "$PROJECT_PATH_WSL").slnx"
if run_dotnet_and_unity_lane full record_full_dotnet_failure record_unity_and_generate_slnx; then
    echo "Expected full-lane consumer failure to be preserved." >&2
    exit 1
else
    status=$?
fi
if [ "$status" -ne 43 ]; then
    echo "Expected full consumer exit 43 but observed $status." >&2
    exit 1
fi
assert_calls "dotnet:$(basename "$PROJECT_PATH_WSL").slnx"

echo "run_tests cold-checkout ordering tests passed"
