#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

UNITY_PATH="${UNITY_PATH:-/mnt/c/Users/user/Desktop/6000.3.11f1/Editor/Unity.exe}"
DOTNET_PATH="${DOTNET_PATH:-/mnt/c/Program Files/dotnet/dotnet.exe}"
PROJECT_PATH_WSL="$SCRIPT_DIR"
PROJECT_PATH_WIN="${PROJECT_PATH_WIN:-}"
DRY_RUN=0

RESULT_DIR="$PROJECT_PATH_WSL/TestResults"
METRICS_DIR="$RESULT_DIR/.metrics"

STRATIFICATION_CHECKER_PATH="$PROJECT_PATH_WSL/Tools/check_gameplay_test_stratification.py"
SEMANTIC_QUERY_CHECKER_PATH="$PROJECT_PATH_WSL/Tools/check_gameplay_semantic_query_migration.py"
ACTION_PLAN_CORRELATION_CHECKER_PATH="$PROJECT_PATH_WSL/Tools/check_gameplay_action_plan_correlation_migration.py"

DOTNET_CORE_LOG="$RESULT_DIR/wsl-dotnet-core.log"
DOTNET_UI_LOG="$RESULT_DIR/wsl-dotnet-ui.log"
DOTNET_FULL_LOG="$RESULT_DIR/wsl-dotnet-full.log"
DOTNET_INTEGRATION_SIMULATION_LOG="$RESULT_DIR/wsl-dotnet-integration-simulation.log"
DOTNET_INTEGRATION_REPLAY_LOG="$RESULT_DIR/wsl-dotnet-integration-replay.log"
DOTNET_INTEGRATION_FUZZ_LOG="$RESULT_DIR/wsl-dotnet-integration-fuzz.log"

UNITY_CORE_EDITMODE_LOG="$RESULT_DIR/wsl-unity-core-editmode.log"
UNITY_CORE_EDITMODE_XML="$RESULT_DIR/wsl-unity-core-editmode.xml"
UNITY_CORE_PLAYMODE_LOG="$RESULT_DIR/wsl-unity-core-playmode.log"
UNITY_CORE_PLAYMODE_XML="$RESULT_DIR/wsl-unity-core-playmode.xml"

UNITY_UI_EDITMODE_LOG="$RESULT_DIR/wsl-unity-ui-editmode.log"
UNITY_UI_EDITMODE_XML="$RESULT_DIR/wsl-unity-ui-editmode.xml"

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

require_file() {
    local path="$1"
    local label="$2"

    if [ ! -f "$path" ]; then
        echo "Missing $label: $path"
        exit 1
    fi
}

require_dir() {
    local path="$1"
    local label="$2"

    if [ ! -d "$path" ]; then
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

ensure_result_dirs() {
    mkdir -p "$RESULT_DIR" "$METRICS_DIR"
}

find_solution_file() {
    local solution_candidates
    local solution_count=0
    local solution_file=""
    local solution

    solution_candidates="$(find "$PROJECT_PATH_WSL" -maxdepth 1 -type f -name '*.sln' -printf '%f\n' | sort)"
    while IFS= read -r solution; do
        if [ -z "$solution" ]; then
            continue
        fi
        solution_count=$((solution_count + 1))
        solution_file="$solution"
    done <<EOF
$solution_candidates
EOF

    if [ "$solution_count" -ne 1 ]; then
        echo "ERROR: Expected exactly one .sln in current worktree, found $solution_count."
        echo "  PROJECT_PATH_WSL: $PROJECT_PATH_WSL"
        printf '  Solution candidates:'
        if [ "$solution_count" -eq 0 ]; then
            printf ' <none>'
        else
            while IFS= read -r solution; do
                if [ -n "$solution" ]; then
                    printf ' %s' "$solution"
                fi
            done <<EOF
$solution_candidates
EOF
        fi
        printf '\n'
        exit 1
    fi

    printf '%s\n' "$solution_file"
}

normalize_windows_path_for_compare() {
    local path="$1"

    path="$(printf '%s' "$path" | tr -d '\r' | sed 's|/|\\|g')"
    while [ "${#path}" -gt 3 ]; do
        case "$path" in
            *\\) path="${path%\\}" ;;
            *) break ;;
        esac
    done
    printf '%s' "$path" | tr '[:upper:]' '[:lower:]'
}

initialize_project_paths() {
    if [ -z "$PROJECT_PATH_WIN" ]; then
        PROJECT_PATH_WIN="$(wslpath -w "$PROJECT_PATH_WSL")"
    fi
}

validate_project_paths() {
    local expected_project_path_win
    local expected_compare
    local actual_compare

    expected_project_path_win="$(wslpath -w "$PROJECT_PATH_WSL")"
    expected_compare="$(normalize_windows_path_for_compare "$expected_project_path_win")"
    actual_compare="$(normalize_windows_path_for_compare "$PROJECT_PATH_WIN")"

    if [ "$actual_compare" != "$expected_compare" ]; then
        echo "ERROR: PROJECT_PATH_WIN does not match current worktree."
        echo "  PROJECT_PATH_WSL:          $PROJECT_PATH_WSL"
        echo "  Expected PROJECT_PATH_WIN: $expected_project_path_win"
        echo "  Actual PROJECT_PATH_WIN:   $PROJECT_PATH_WIN"
        exit 1
    fi

    require_dir "$PROJECT_PATH_WSL/Assets" "Unity Assets directory"
    require_dir "$PROJECT_PATH_WSL/ProjectSettings" "Unity ProjectSettings directory"
    require_file "$PROJECT_PATH_WSL/ProjectSettings/ProjectVersion.txt" "Unity ProjectVersion.txt"
}

print_environment_summary() {
    echo "Test environment:"
    echo "  PROJECT_PATH_WSL: $PROJECT_PATH_WSL"
    echo "  PROJECT_PATH_WIN: $PROJECT_PATH_WIN"
    echo "  UNITY_PATH:       $UNITY_PATH"
    echo "  DOTNET_PATH:      $DOTNET_PATH"
    echo "  RESULT_DIR:       $RESULT_DIR"
}

print_config() {
    echo "PROJECT_PATH_WSL=$PROJECT_PATH_WSL"
    echo "PROJECT_PATH_WIN=$PROJECT_PATH_WIN"
    echo "UNITY_PATH=$UNITY_PATH"
    echo "DOTNET_PATH=$DOTNET_PATH"
    echo "RESULT_DIR=$RESULT_DIR"
}

print_shell_command() {
    printf '  '
    printf '%q ' "$@"
    printf '\n'
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

    if [ "$DRY_RUN" -eq 1 ]; then
        echo "Would run Windows dotnet build:"
        print_shell_command "$DOTNET_PATH" build "$@"
        return 0
    fi

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
}

run_semantic_query_migration_check() {
    local exit_code

    echo "Running semantic query migration governance check..."
    if python3 "$SEMANTIC_QUERY_CHECKER_PATH" --root "$PROJECT_PATH_WSL"; then
        :
    else
        exit_code=$?
        echo "Semantic query migration governance check failed"
        exit "$exit_code"
    fi
}

run_action_plan_correlation_check() {
    local exit_code

    echo "Running ActionPlanId correlation governance check..."
    if python3 "$ACTION_PLAN_CORRELATION_CHECKER_PATH" --root "$PROJECT_PATH_WSL"; then
        :
    else
        exit_code=$?
        echo "ActionPlanId correlation governance check failed"
        exit "$exit_code"
    fi
}

summarize_stage_result() {
    local stage_key="$1"
    local xml_path="$2"
    local stage_exit_code="$3"
    local metrics_path="$METRICS_DIR/${stage_key}.json"

    python3 - "$xml_path" "$metrics_path" "$stage_key" "$stage_exit_code" <<'PY'
import json
import math
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

xml_path = Path(sys.argv[1])
metrics_path = Path(sys.argv[2])
stage_key = sys.argv[3]
stage_exit_code = int(sys.argv[4])


def extract_failure_message(node):
    message_node = node.find("./failure/message")
    if message_node is None or message_node.text is None:
        return ""
    return message_node.text.strip().splitlines()[0].strip()


def parse_test_result_xml(path):
    tree = ET.parse(path)
    root = tree.getroot()

    total = int(root.attrib.get("total", "0") or 0)
    failed = int(root.attrib.get("failed", "0") or 0)
    duration_text = root.attrib.get("duration", root.attrib.get("time", "0")) or "0"
    try:
        duration_seconds = float(duration_text)
    except ValueError:
        duration_seconds = 0.0

    failed_cases = []
    for node in root.findall(".//test-case[@result='Failed']"):
        failed_cases.append(
            {
                "full_name": node.attrib.get("fullname") or node.attrib.get("name") or "<unknown>",
                "message": extract_failure_message(node),
            }
        )

    return {
        "total": total,
        "failed": failed,
        "failure_ratio": (failed / total) if total else 0.0,
        "duration_seconds": duration_seconds,
        "failed_cases": failed_cases,
    }


def read_stage_metrics(path):
    if not path.exists():
        return []
    payload = json.loads(path.read_text(encoding="utf-8"))
    return payload.get("runs", [])


def evaluate_reliability_metrics(current, history):
    successful_history = [entry for entry in history[-3:] if entry.get("total", 0) > 0]
    if len(successful_history) < 3:
        return []

    average_total = sum(int(entry.get("total", 0)) for entry in successful_history) / len(successful_history)
    average_duration = sum(float(entry.get("duration_seconds", 0.0)) for entry in successful_history) / len(successful_history)
    current_total = int(current.get("total", 0))
    current_duration = float(current.get("duration_seconds", 0.0))

    warnings = []
    total_drop_threshold = max(2, math.ceil(average_total * 0.05))
    if current_total < average_total - total_drop_threshold:
        warnings.append(f"test count dropped from moving average {average_total:.1f} to {current_total}")

    if average_duration > 0 and current_duration < average_duration * 0.60:
        warnings.append(
            f"duration dropped from moving average {average_duration:.3f}s to {current_duration:.3f}s"
        )

    return warnings


def write_json(path, payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def update_stage_metrics(path, current, record_success):
    if not record_success:
        return

    history = read_stage_metrics(path)
    history.append(
        {
            "timestampUtc": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
            "total": int(current.get("total", 0)),
            "failed": int(current.get("failed", 0)),
            "failure_ratio": float(current.get("failure_ratio", 0.0)),
            "duration_seconds": float(current.get("duration_seconds", 0.0)),
        }
    )
    write_json(path, {"schemaVersion": 1, "runs": history[-3:]})


current = parse_test_result_xml(xml_path)
history = read_stage_metrics(metrics_path)

print(
    f"Stage metrics [{stage_key}]: "
    f"total={current['total']} "
    f"failed={current['failed']} "
    f"failure_ratio={current['failure_ratio']:.3f} "
    f"duration_seconds={current['duration_seconds']:.3f}"
)

for warning in evaluate_reliability_metrics(current, history):
    print(f"WARNING: {stage_key} {warning}")

failed_cases = current.get("failed_cases") or []
if failed_cases:
    print(f"---- FAILED TESTS [{stage_key}] ----")
    for failed_case in failed_cases[:20]:
        print(failed_case.get("full_name", "<unknown>"))
        message = (failed_case.get("message") or "").strip()
        if message:
            print(f"  Reason: {message}")

update_stage_metrics(metrics_path, current, record_success=(stage_exit_code == 0))
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
    local log_path_win
    local xml_path_win
    local exit_code

    log_path_win="$(wslpath -w "$log_path")"
    xml_path_win="$(wslpath -w "$xml_path")"

    if [ "$DRY_RUN" -eq 1 ]; then
        echo "Would run Unity $stage_label:"
        print_shell_command \
            timeout --kill-after=10 300 \
            "$UNITY_PATH" \
            -batchmode \
            -nographics \
            -projectPath "$PROJECT_PATH_WIN" \
            -logFile "$log_path_win" \
            -executeMethod "$execute_method" \
            -codexSelection "$selection" \
            -codexResultPath "$xml_path_win"
        return 0
    fi

    rm -f "$xml_path"
    echo "Running Unity $stage_label..."

    if timeout --kill-after=10 300 \
        "$UNITY_PATH" \
        -batchmode \
        -nographics \
        -projectPath "$PROJECT_PATH_WIN" \
        -logFile "$log_path_win" \
        -executeMethod "$execute_method" \
        -codexSelection "$selection" \
        -codexResultPath "$xml_path_win"
    then
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
    if [ "$DRY_RUN" -eq 0 ]; then
        : > "$DOTNET_CORE_LOG"
    fi
    echo "Running Windows dotnet core build..."
    run_dotnet_build "$DOTNET_CORE_LOG" Game.Feature.Gameplay.Tests.csproj -c Debug
    run_dotnet_build "$DOTNET_CORE_LOG" Game.Feature.Gameplay.PlayModeTests.csproj -c Debug
}

run_dotnet_ui() {
    if [ "$DRY_RUN" -eq 0 ]; then
        : > "$DOTNET_UI_LOG"
    fi
    echo "Running Windows dotnet UI build..."
    run_dotnet_build "$DOTNET_UI_LOG" Game.Feature.UI.Tests.csproj -c Debug
}

run_dotnet_full() {
    local solution_file

    if [ "$DRY_RUN" -eq 0 ]; then
        : > "$DOTNET_FULL_LOG"
    fi
    solution_file="$(find_solution_file)"
    echo "Running Windows dotnet full build..."
    run_dotnet_build "$DOTNET_FULL_LOG" "$solution_file" -c Debug
}

run_dotnet_integration() {
    local log_path="$1"

    if [ "$DRY_RUN" -eq 0 ]; then
        : > "$log_path"
    fi
    echo "Running Windows dotnet integration build..."
    run_dotnet_build "$log_path" Game.Feature.Gameplay.Tests.csproj -c Debug
}

run_unity_core() {
    run_unity_stage "core" "core-editmode" "core (EditMode)" "EditMode" "$UNITY_CORE_EDITMODE_LOG" "$UNITY_CORE_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode"
    run_unity_stage "core" "core-playmode" "core (PlayMode)" "PlayMode" "$UNITY_CORE_PLAYMODE_LOG" "$UNITY_CORE_PLAYMODE_XML" "TestRunnerCliBootstrap.RunPlayMode"
}

run_unity_ui() {
    run_unity_stage "ui" "ui-editmode" "ui (EditMode)" "EditMode" "$UNITY_UI_EDITMODE_LOG" "$UNITY_UI_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode"
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

    require_command wslpath
    initialize_project_paths
    validate_project_paths

    if [ "$mode" = "--print-config" ]; then
        print_config
        return 0
    fi

    if [ "$mode" = "--dry-run" ]; then
        DRY_RUN=1
        shift
        mode="${1:-}"
    fi

    print_environment_summary

    if [ "$DRY_RUN" -eq 0 ]; then
        require_command timeout
        require_command python3
        require_file "$DOTNET_PATH" "dotnet executable"
        require_file "$UNITY_PATH" "Unity executable"
        require_file "$STRATIFICATION_CHECKER_PATH" "stratification governance checker"
        require_file "$SEMANTIC_QUERY_CHECKER_PATH" "semantic query governance checker"
        require_file "$ACTION_PLAN_CORRELATION_CHECKER_PATH" "ActionPlanId correlation governance checker"
        ensure_result_dirs

        run_governance_check
        run_semantic_query_migration_check
        run_action_plan_correlation_check
    else
        echo "Dry run: governance checks, dotnet builds, and Unity stages will not execute."
    fi

    case "$mode" in
        core)
            run_dotnet_core
            run_unity_core
            ;;
        ui)
            run_dotnet_ui
            run_unity_ui
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
            echo "Usage: ./run_tests.sh [--print-config|--dry-run <lane>|core|ui|full|--integration-simulation|--integration-replay|--integration-fuzz]"
            exit 1
            ;;
    esac

    if [ "$DRY_RUN" -eq 0 ]; then
        echo "ALL TESTS PASSED"
    fi
}

main "$@"
