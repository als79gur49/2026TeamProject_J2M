#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

UNITY_PATH="${UNITY_PATH:-/mnt/c/Users/user/Desktop/6000.3.11f1/Editor/Unity.exe}"
DOTNET_PATH="${DOTNET_PATH:-/mnt/c/Program Files/dotnet/dotnet.exe}"
PROJECT_PATH_WIN="${PROJECT_PATH_WIN:-C:\Users\user\2026TeamProject_J2M}"
PROJECT_PATH_WSL="$SCRIPT_DIR"

RESULT_DIR="$PROJECT_PATH_WSL/TestResults"
METRICS_DIR="$RESULT_DIR/.metrics"
mkdir -p "$RESULT_DIR" "$METRICS_DIR"

STRATIFICATION_MANIFEST_PATH_WSL="$PROJECT_PATH_WSL/Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/GameplayTestStratificationManifest.json"
STRATIFICATION_CHECKER_PATH="$PROJECT_PATH_WSL/Tools/check_gameplay_test_stratification.py"

DOTNET_CORE_LOG="$RESULT_DIR/wsl-dotnet-core.log"
DOTNET_FULL_LOG="$RESULT_DIR/wsl-dotnet-full.log"
DOTNET_INTEGRATION_SIMULATION_LOG="$RESULT_DIR/wsl-dotnet-integration-simulation.log"
DOTNET_INTEGRATION_REPLAY_LOG="$RESULT_DIR/wsl-dotnet-integration-replay.log"
DOTNET_INTEGRATION_FUZZ_LOG="$RESULT_DIR/wsl-dotnet-integration-fuzz.log"

UNITY_CORE_EDITMODE_LOG="$RESULT_DIR/wsl-unity-core-editmode.log"
UNITY_CORE_EDITMODE_XML="$RESULT_DIR/wsl-unity-core-editmode.xml"
UNITY_CORE_PLAYMODE_LOG="$RESULT_DIR/wsl-unity-core-playmode.log"
UNITY_CORE_PLAYMODE_XML="$RESULT_DIR/wsl-unity-core-playmode.xml"

UNITY_FULL_EDITMODE_LOG="$RESULT_DIR/wsl-unity-full-editmode.log"
UNITY_FULL_EDITMODE_XML="$RESULT_DIR/wsl-unity-full-editmode.xml"
UNITY_FULL_PLAYMODE_LOG="$RESULT_DIR/wsl-unity-full-playmode.log"
UNITY_FULL_PLAYMODE_XML="$RESULT_DIR/wsl-unity-full-playmode.xml"

UNITY_INTEGRATION_SIMULATION_EDITMODE_LOG="$RESULT_DIR/wsl-unity-integration-simulation-editmode.log"
UNITY_INTEGRATION_SIMULATION_EDITMODE_XML="$RESULT_DIR/wsl-unity-integration-simulation-editmode.xml"
UNITY_INTEGRATION_REPLAY_EDITMODE_LOG="$RESULT_DIR/wsl-unity-integration-replay-editmode.log"
UNITY_INTEGRATION_REPLAY_EDITMODE_XML="$RESULT_DIR/wsl-unity-integration-replay-editmode.xml"
UNITY_INTEGRATION_FUZZ_EDITMODE_LOG="$RESULT_DIR/wsl-unity-integration-fuzz-editmode.log"
UNITY_INTEGRATION_FUZZ_EDITMODE_XML="$RESULT_DIR/wsl-unity-integration-fuzz-editmode.xml"

PLAYMODE_CORE_CAP=0

require_file() {
    local path="$1"
    local label="$2"

    if [ ! -f "$path" ]; then
        echo "Missing $label: $path"
        exit 1
    fi
}

require_command() {
    local command_name="$1"

    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Missing required command: $command_name"
        exit 1
    fi
}

validate_xml() {
    local xml_path="$1"
    local total_tests

    if [ ! -f "$xml_path" ] || [ ! -s "$xml_path" ]; then
        echo "Missing or empty XML: $xml_path"
        return 1
    fi

    grep -q "<test-run" "$xml_path" || {
        echo "Invalid XML (no test-run node)"
        return 1
    }

    grep -q 'total="' "$xml_path" || {
        echo "Invalid XML (no test count)"
        return 1
    }

    total_tests="$(grep -o 'total="[0-9]*"' "$xml_path" | grep -o '[0-9]*' | head -n 1)"
    if [ -z "$total_tests" ] || [ "$total_tests" -eq 0 ]; then
        echo "ERROR: No tests executed (total=0)"
        return 1
    fi
}

run_dotnet_build() {
    local log_path="$1"
    shift

    if ! "$DOTNET_PATH" build "$@" >> "$log_path" 2>&1; then
        echo "DOTNET BUILD FAILED"
        tail -n 50 "$log_path"
        exit 1
    fi
}

run_governance_check() {
    local exit_code

    echo "Running stratification governance check..."
    if python3 "$STRATIFICATION_CHECKER_PATH" --root "$PROJECT_PATH_WSL"; then
        :
    else
        exit_code=$?
        echo "Stratification governance check failed"
        exit "$exit_code"
    fi

    PLAYMODE_CORE_CAP="$(python3 - "$PROJECT_PATH_WSL" <<'PY'
import sys
from pathlib import Path

root = Path(sys.argv[1])
sys.path.insert(0, str(root / "Tools"))
import gameplay_test_stratification_lib as lib

paths = lib.get_repo_paths(root)
tests = lib.discover_tests(root, paths["test_root"])
overrides = lib.load_overrides(paths["override_path"])
manifest = lib.build_manifest(tests, overrides, root)
mode = lib.determine_governance_mode()
summary, _, _ = lib.build_governance_summary(root, tests, manifest, mode)
print(summary.playmode_core_cap)
PY
)"
}

summarize_stage_result() {
    local stage_key="$1"
    local xml_path="$2"
    local stage_exit_code="$3"
    local metrics_path="$METRICS_DIR/${stage_key}.json"

    python3 - "$PROJECT_PATH_WSL" "$xml_path" "$metrics_path" "$stage_key" "$stage_exit_code" <<'PY'
import sys
from pathlib import Path

root = Path(sys.argv[1])
xml_path = Path(sys.argv[2])
metrics_path = Path(sys.argv[3])
stage_key = sys.argv[4]
stage_exit_code = int(sys.argv[5])

sys.path.insert(0, str(root / "Tools"))
import gameplay_test_stratification_lib as lib

current = lib.parse_test_result_xml(xml_path)
history = lib.read_stage_metrics(metrics_path)

print(
    f"Stage metrics [{stage_key}]: "
    f"total={current['total']} "
    f"failed={current['failed']} "
    f"failure_ratio={current['failure_ratio']:.3f} "
    f"duration_seconds={current['duration_seconds']:.3f}"
)

for warning in lib.evaluate_reliability_metrics(current, history):
    print(f"WARNING: {stage_key} {warning}")

failed_cases = current.get("failed_cases") or []
if failed_cases:
    print(f"---- FAILED TESTS [{stage_key}] ----")
    for failed_case in failed_cases[:20]:
        print(failed_case.get("full_name", "<unknown>"))
        message = (failed_case.get("message") or "").strip()
        if message:
            print(f"  Reason: {message}")

lib.update_stage_metrics(metrics_path, current, record_success=(stage_exit_code == 0))
PY
}

run_unity_stage() {
    local selection="$1"
    local stage_key="$2"
    local stage_label="$3"
    local platform="$4"
    local log_path="$5"
    local xml_path="$6"
    local execute_method="$7"
    local -a command
    local log_path_win
    local xml_path_win
    local manifest_path_win
    local exit_code

    rm -f "$xml_path"
    log_path_win="$(wslpath -w "$log_path")"
    xml_path_win="$(wslpath -w "$xml_path")"
    manifest_path_win="$(wslpath -w "$STRATIFICATION_MANIFEST_PATH_WSL")"

    command=(
        timeout --kill-after=10 300
        "$UNITY_PATH"
        -batchmode
        -nographics
        -projectPath "$PROJECT_PATH_WIN"
        -logFile "$log_path_win"
        -executeMethod "$execute_method"
        -codexSelection "$selection"
        -codexResultPath "$xml_path_win"
        -codexManifestPath "$manifest_path_win"
    )

    if [ "$selection" = "core" ] && [ "$platform" = "PlayMode" ] && [ "$PLAYMODE_CORE_CAP" -gt 0 ]; then
        command+=(-codexPlayModeCoreCap "$PLAYMODE_CORE_CAP")
        if [ "${ALLOW_PLAYMODE_CORE_OVERFLOW:-0}" = "1" ]; then
            command+=(-codexAllowPlayModeCoreOverflow 1)
        fi
    fi

    echo "Running Unity $stage_label..."

    if "${command[@]}"; then
        exit_code=0
    else
        exit_code=$?
    fi

    if [ "$exit_code" -eq 124 ] || [ "$exit_code" -eq 137 ]; then
        echo "Unity execution timed out (possible hang)"
        return "$exit_code"
    fi

    if ! validate_xml "$xml_path"; then
        if [ "$exit_code" -ne 0 ]; then
            return "$exit_code"
        fi
        return 1
    fi

    if ! summarize_stage_result "$stage_key" "$xml_path" "$exit_code"; then
        return 1
    fi
    return "$exit_code"
}

run_dotnet_core() {
    : > "$DOTNET_CORE_LOG"
    echo "Running Windows dotnet core build..."
    run_dotnet_build "$DOTNET_CORE_LOG" Game.Feature.Gameplay.Tests.csproj -c Debug
    run_dotnet_build "$DOTNET_CORE_LOG" Game.Feature.Gameplay.PlayModeTests.csproj -c Debug
}

run_dotnet_full() {
    : > "$DOTNET_FULL_LOG"
    echo "Running Windows dotnet full build..."
    run_dotnet_build "$DOTNET_FULL_LOG" 2026TeamProject_J2M.sln -c Debug
}

run_dotnet_integration() {
    local log_path="$1"

    : > "$log_path"
    echo "Running Windows dotnet integration build..."
    run_dotnet_build "$log_path" Game.Feature.Gameplay.Tests.csproj -c Debug
}

run_unity_core() {
    run_unity_stage "core" "core-editmode" "core (EditMode)" "EditMode" "$UNITY_CORE_EDITMODE_LOG" "$UNITY_CORE_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode"
    run_unity_stage "core" "core-playmode" "core (PlayMode)" "PlayMode" "$UNITY_CORE_PLAYMODE_LOG" "$UNITY_CORE_PLAYMODE_XML" "TestRunnerCliBootstrap.RunPlayMode"
}

run_unity_full() {
    run_unity_stage "full" "full-editmode" "full (EditMode)" "EditMode" "$UNITY_FULL_EDITMODE_LOG" "$UNITY_FULL_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode"
    run_unity_stage "full" "full-playmode" "full (PlayMode)" "PlayMode" "$UNITY_FULL_PLAYMODE_LOG" "$UNITY_FULL_PLAYMODE_XML" "TestRunnerCliBootstrap.RunPlayMode"
}

run_unity_integration_simulation() {
    run_unity_stage "integration-simulation" "integration-simulation-editmode" "integration-simulation (EditMode)" "EditMode" "$UNITY_INTEGRATION_SIMULATION_EDITMODE_LOG" "$UNITY_INTEGRATION_SIMULATION_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode"
}

run_unity_integration_replay() {
    run_unity_stage "integration-replay" "integration-replay-editmode" "integration-replay (EditMode)" "EditMode" "$UNITY_INTEGRATION_REPLAY_EDITMODE_LOG" "$UNITY_INTEGRATION_REPLAY_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode"
}

run_unity_integration_fuzz() {
    run_unity_stage "integration-fuzz" "integration-fuzz-editmode" "integration-fuzz (EditMode)" "EditMode" "$UNITY_INTEGRATION_FUZZ_EDITMODE_LOG" "$UNITY_INTEGRATION_FUZZ_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode"
}

main() {
    local mode="${1:-}"

    require_command timeout
    require_command wslpath
    require_command python3
    require_file "$DOTNET_PATH" "dotnet executable"
    require_file "$UNITY_PATH" "Unity executable"
    require_file "$STRATIFICATION_CHECKER_PATH" "stratification governance checker"
    require_file "$STRATIFICATION_MANIFEST_PATH_WSL" "stratification manifest"

    run_governance_check

    case "$mode" in
        core)
            run_dotnet_core
            run_unity_core
            ;;
        full)
            run_dotnet_full
            run_unity_full
            ;;
        --integration-simulation)
            run_dotnet_integration "$DOTNET_INTEGRATION_SIMULATION_LOG"
            run_unity_integration_simulation
            ;;
        --integration-replay)
            run_dotnet_integration "$DOTNET_INTEGRATION_REPLAY_LOG"
            run_unity_integration_replay
            ;;
        --integration-fuzz)
            run_dotnet_integration "$DOTNET_INTEGRATION_FUZZ_LOG"
            run_unity_integration_fuzz
            ;;
        *)
            echo "Usage: ./run_tests.sh [core|full|--integration-simulation|--integration-replay|--integration-fuzz]"
            exit 1
            ;;
    esac

    echo "ALL TESTS PASSED"
}

main "$@"
