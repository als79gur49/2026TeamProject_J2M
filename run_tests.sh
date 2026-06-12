#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

UNITY_PATH="${UNITY_PATH:-/mnt/c/Users/user/Desktop/6000.3.11f1/Editor/Unity.exe}"
DOTNET_PATH="${DOTNET_PATH:-/mnt/c/Program Files/dotnet/dotnet.exe}"
PROJECT_PATH_WSL="$SCRIPT_DIR"
PROJECT_PATH_WIN="${PROJECT_PATH_WIN:-}"
DRY_RUN=0
TEST_FILTER=""
FILTERED_TOTAL=0

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
UNITY_CORE_FEATURE_GATE_EDITMODE_LOG="$RESULT_DIR/wsl-unity-core-feature-gate-editmode.log"
UNITY_CORE_FEATURE_GATE_EDITMODE_XML="$RESULT_DIR/wsl-unity-core-feature-gate-editmode.xml"
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

print_usage() {
    echo "Usage: ./run_tests.sh [--print-config|--dry-run <lane>|core|core-feature-gate|ui|full|--integration-simulation|--integration-replay|--integration-fuzz] [--filter <test-filter>|--test-filter <test-filter>]"
}

print_shell_command() {
    printf '  '
    printf '%q ' "$@"
    printf '\n'
}

find_current_project_unity_processes() {
    ps -eo pid,ppid,stat,etime,args |
        grep -F "$PROJECT_PATH_WIN" |
        grep -Ei 'Unity(\.exe|Editor)|/Unity\.exe' |
        grep -v '[g]rep' || true

    find_current_project_windows_unity_processes
}

find_current_project_windows_unity_processes() {
    if ! command -v powershell.exe >/dev/null 2>&1; then
        return 0
    fi

    powershell.exe -NoProfile -Command '
        & {
        param([string]$project)
        if ([string]::IsNullOrWhiteSpace($project)) { exit 0 }
        $project = $project.TrimEnd("\").ToLowerInvariant()
        $processes = Get-CimInstance Win32_Process
        $roots = $processes | Where-Object {
            $_.Name -eq "Unity.exe" -and
            $_.CommandLine -and
            $_.CommandLine.ToLowerInvariant().Contains($project)
        }
        $ids = @($roots | ForEach-Object { [int]$_.ProcessId })
        do {
            $added = $false
            foreach ($process in $processes) {
                if (($ids -contains [int]$process.ParentProcessId) -and -not ($ids -contains [int]$process.ProcessId)) {
                    $ids += [int]$process.ProcessId
                    $added = $true
                }
            }
        } while ($added)
        foreach ($process in $processes) {
            if ($ids -contains [int]$process.ProcessId) {
                "{0} {1} {2} {3}" -f $process.ProcessId, $process.ParentProcessId, $process.Name, $process.CommandLine
            }
        }
        }
    ' "$PROJECT_PATH_WIN" 2>/dev/null | tr -d '\r' || true
}

current_project_unity_pids() {
    ps -eo pid,ppid,stat,etime,args |
        grep -F "$PROJECT_PATH_WIN" |
        grep -Ei 'Unity(\.exe|Editor)|/Unity\.exe' |
        grep -v '[g]rep' |
        awk '{print $1}' || true
}

current_project_windows_unity_pids() {
    if ! command -v powershell.exe >/dev/null 2>&1; then
        return 0
    fi

    powershell.exe -NoProfile -Command '
        & {
        param([string]$project)
        if ([string]::IsNullOrWhiteSpace($project)) { exit 0 }
        $project = $project.TrimEnd("\").ToLowerInvariant()
        $processes = Get-CimInstance Win32_Process
        $roots = $processes | Where-Object {
            $_.Name -eq "Unity.exe" -and
            $_.CommandLine -and
            $_.CommandLine.ToLowerInvariant().Contains($project)
        }
        $ids = @($roots | ForEach-Object { [int]$_.ProcessId })
        do {
            $added = $false
            foreach ($process in $processes) {
                if (($ids -contains [int]$process.ParentProcessId) -and -not ($ids -contains [int]$process.ProcessId)) {
                    $ids += [int]$process.ProcessId
                    $added = $true
                }
            }
        } while ($added)
        $ids | Sort-Object -Descending
        }
    ' "$PROJECT_PATH_WIN" 2>/dev/null | tr -d '\r' || true
}

current_project_lock_holders() {
    local lock_path

    if ! command -v lsof >/dev/null 2>&1; then
        return 0
    fi

    for lock_path in \
        "$PROJECT_PATH_WSL/Temp/UnityLockfile" \
        "$PROJECT_PATH_WSL/Library/UnityLockfile" \
        "$PROJECT_PATH_WSL/Library/ArtifactDB-lock" \
        "$PROJECT_PATH_WSL/Library/SourceAssetDB-lock"
    do
        if [ -e "$lock_path" ]; then
            lsof "$lock_path" 2>/dev/null | awk 'NR > 1 { print }' || true
        fi
    done
}

write_project_lock_status() {
    local output_path="$1"
    local lock_path

    {
        for lock_path in \
            "$PROJECT_PATH_WSL/Temp/UnityLockfile" \
            "$PROJECT_PATH_WSL/Library/UnityLockfile" \
            "$PROJECT_PATH_WSL/Library/ArtifactDB-lock" \
            "$PROJECT_PATH_WSL/Library/SourceAssetDB-lock"
        do
            if [ -e "$lock_path" ]; then
                echo "$lock_path: exists"
                if command -v lsof >/dev/null 2>&1; then
                    lsof "$lock_path" || true
                else
                    echo "lsof unavailable"
                fi
            else
                echo "$lock_path: missing"
            fi
        done
    } > "$output_path"
}

ensure_no_current_project_unity_process() {
    local process_snapshot

    process_snapshot="$(find_current_project_unity_processes)"
    if [ -n "$process_snapshot" ]; then
        echo "ERROR: Current-project Unity process is already running. Refusing to start another Unity stage."
        echo "$process_snapshot"
        return 1
    fi
}

ensure_no_current_project_unity_lock() {
    local lock_holders

    lock_holders="$(current_project_lock_holders)"
    if [ -n "$lock_holders" ]; then
        echo "ERROR: Current-project Unity lock is held. Refusing to start another Unity stage."
        echo "$lock_holders"
        return 1
    fi
}

terminate_current_project_unity_processes() {
    local pids
    local windows_pids

    pids="$(current_project_unity_pids | sort -rn | tr '\n' ' ')"
    if [ -n "$pids" ]; then
        # shellcheck disable=SC2086
        kill -TERM $pids 2>/dev/null || true
        sleep 2
        pids="$(current_project_unity_pids | sort -rn | tr '\n' ' ')"
        if [ -n "$pids" ]; then
            # shellcheck disable=SC2086
            kill -KILL $pids 2>/dev/null || true
        fi
    fi

    windows_pids="$(current_project_windows_unity_pids | tr '\n' ' ')"
    if [ -n "$windows_pids" ] && command -v powershell.exe >/dev/null 2>&1; then
        powershell.exe -NoProfile -Command '
            & {
            param([string]$pidList)
            $pids = $pidList -split "\s+" |
                Where-Object { $_ } |
                ForEach-Object { [int]$_ }
            foreach ($pidValue in $pids) {
                Stop-Process -Id $pidValue -Force -ErrorAction SilentlyContinue
            }
            }
        ' "$windows_pids" >/dev/null 2>&1 || true
        sleep 2
    fi
}

remove_stale_current_project_unity_lockfiles() {
    local lock_holders

    if [ -n "$(find_current_project_unity_processes)" ]; then
        return 0
    fi

    lock_holders="$(current_project_lock_holders)"
    if [ -n "$lock_holders" ]; then
        return 0
    fi

    rm -f \
        "$PROJECT_PATH_WSL/Temp/UnityLockfile" \
        "$PROJECT_PATH_WSL/Library/UnityLockfile" || true
}

capture_unity_timeout_artifacts() {
    local stage_key="$1"
    local log_path="$2"
    local xml_path="$3"
    local exit_code="$4"
    local before_processes="$5"
    local artifact_dir

    artifact_dir="$RESULT_DIR/timeout-artifacts/${stage_key}-$(date +%Y%m%d-%H%M%S)"
    mkdir -p "$artifact_dir"
    printf '%s\n' "$before_processes" > "$artifact_dir/process-before.txt"
    find_current_project_unity_processes > "$artifact_dir/process-timeout-before-cleanup.txt"
    write_project_lock_status "$artifact_dir/lock-before-cleanup.txt"
    {
        echo "stage_key=$stage_key"
        echo "test_filter=$TEST_FILTER"
        echo "exit_code=$exit_code"
        echo "log_path=$log_path"
        echo "xml_path=$xml_path"
        if [ -f "$xml_path" ]; then
            echo "xml_present=yes"
        else
            echo "xml_present=no"
        fi
    } > "$artifact_dir/summary.txt"
    if [ -f "$log_path" ]; then
        tail -n 300 "$log_path" > "$artifact_dir/unity-log-tail.txt"
    else
        echo "Unity log missing: $log_path" > "$artifact_dir/unity-log-tail.txt"
    fi

    terminate_current_project_unity_processes
    remove_stale_current_project_unity_lockfiles
    find_current_project_unity_processes > "$artifact_dir/process-after-cleanup.txt"
    write_project_lock_status "$artifact_dir/lock-after-cleanup.txt"
    echo "Unity timeout artifacts: $artifact_dir"
}

format_plan_value() {
    local value="$1"
    if [ -z "$value" ]; then
        printf '<none>'
    else
        printf '%s' "$value"
    fi
}

print_unity_selection_plan() {
    local stage_label="$1"
    local platform="$2"
    local selection="$3"
    local assemblies="$4"
    local categories="$5"

    echo "Unity selection plan [$stage_label]:"
    echo "  mode:       $platform"
    echo "  selection:  $selection"
    echo "  assemblies: $(format_plan_value "$assemblies")"
    echo "  categories: $(format_plan_value "$categories")"
    echo "  filter:     $(format_plan_value "$TEST_FILTER")"
}

validate_xml() {
    local xml_path="$1"
    local allow_empty="${2:-0}"
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
    if [ -z "$total_tests" ]; then
        echo "ERROR: Missing test count"
        return 1
    fi

    if [ "$total_tests" -eq 0 ] && [ "$allow_empty" -ne 1 ]; then
        echo "ERROR: No tests executed (total=0)"
        return 1
    fi
}

get_xml_total() {
    local xml_path="$1"
    grep -o 'total="[0-9]*"' "$xml_path" | grep -o '[0-9]*' | head -n 1
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
    local selected_assemblies="${8:-}"
    local selected_categories="${9:-}"
    local log_path_win
    local xml_path_win
    local exit_code
    local allow_empty=0
    local total_tests
    local process_before
    local -a unity_command

    log_path_win="$(wslpath -w "$log_path")"
    xml_path_win="$(wslpath -w "$xml_path")"
    if [ -n "$TEST_FILTER" ]; then
        allow_empty=1
    fi

    unity_command=(
        timeout --kill-after=10 300
        "$UNITY_PATH"
        -batchmode
        -nographics
        -projectPath "$PROJECT_PATH_WIN"
        -logFile "$log_path_win"
        -executeMethod "$execute_method"
        -codexSelection "$selection"
        -codexResultPath "$xml_path_win"
    )

    if [ -n "$TEST_FILTER" ]; then
        unity_command+=(-codexTestFilter "$TEST_FILTER")
    fi

    if [ "$DRY_RUN" -eq 1 ]; then
        print_unity_selection_plan "$stage_label" "$platform" "$selection" "$selected_assemblies" "$selected_categories"
        echo "Would run Unity $stage_label:"
        print_shell_command "${unity_command[@]}"
        return 0
    fi

    ensure_no_current_project_unity_process
    ensure_no_current_project_unity_lock
    process_before="$(find_current_project_unity_processes)"
    rm -f "$xml_path"
    echo "Running Unity $stage_label..."

    if "${unity_command[@]}"
    then
        exit_code=0
    else
        exit_code=$?
    fi

    if [ "$exit_code" -eq 124 ] || [ "$exit_code" -eq 137 ]; then
        echo "Unity execution timed out (possible hang)"
        capture_unity_timeout_artifacts "$stage_key" "$log_path" "$xml_path" "$exit_code" "$process_before"
        return "$exit_code"
    fi

    if ! validate_xml "$xml_path" "$allow_empty"; then
        if [ "$exit_code" -ne 0 ]; then
            return "$exit_code"
        fi
        return 1
    fi

    if [ -n "$TEST_FILTER" ]; then
        total_tests="$(get_xml_total "$xml_path")"
        FILTERED_TOTAL=$((FILTERED_TOTAL + total_tests))
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
    run_unity_stage "core" "core-editmode" "core (EditMode)" "EditMode" "$UNITY_CORE_EDITMODE_LOG" "$UNITY_CORE_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode" "Game.Core.Tests" ""
    run_unity_stage "core" "core-playmode" "core (PlayMode)" "PlayMode" "$UNITY_CORE_PLAYMODE_LOG" "$UNITY_CORE_PLAYMODE_XML" "TestRunnerCliBootstrap.RunPlayMode" "Game.Feature.Gameplay.PlayModeTests" "Core"
}

run_unity_core_feature_gate() {
    run_unity_stage "core-feature-gate" "core-feature-gate-editmode" "core-feature-gate (EditMode)" "EditMode" "$UNITY_CORE_FEATURE_GATE_EDITMODE_LOG" "$UNITY_CORE_FEATURE_GATE_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode" "Game.Feature.Gameplay.Tests" "Core"
}

run_unity_ui() {
    run_unity_stage "ui" "ui-editmode" "ui (EditMode)" "EditMode" "$UNITY_UI_EDITMODE_LOG" "$UNITY_UI_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode" "Game.Feature.UI.Tests" ""
}

run_unity_full() {
    run_unity_stage "full" "full-editmode" "full (EditMode)" "EditMode" "$UNITY_FULL_EDITMODE_LOG" "$UNITY_FULL_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode" "" ""
    run_unity_stage "full" "full-playmode" "full (PlayMode)" "PlayMode" "$UNITY_FULL_PLAYMODE_LOG" "$UNITY_FULL_PLAYMODE_XML" "TestRunnerCliBootstrap.RunPlayMode" "" ""
}

run_unity_integration_simulation() {
    run_unity_stage "integration-simulation" "integration-simulation-editmode" "integration-simulation (EditMode)" "EditMode" "$UNITY_INTEGRATION_SIMULATION_EDITMODE_LOG" "$UNITY_INTEGRATION_SIMULATION_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode" "Game.Integration.Simulation.Tests" ""
}

run_unity_integration_replay() {
    run_unity_stage "integration-replay" "integration-replay-editmode" "integration-replay (EditMode)" "EditMode" "$UNITY_INTEGRATION_REPLAY_EDITMODE_LOG" "$UNITY_INTEGRATION_REPLAY_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode" "Game.Integration.Replay.Tests" ""
}

run_unity_integration_fuzz() {
    run_unity_stage "integration-fuzz" "integration-fuzz-editmode" "integration-fuzz (EditMode)" "EditMode" "$UNITY_INTEGRATION_FUZZ_EDITMODE_LOG" "$UNITY_INTEGRATION_FUZZ_EDITMODE_XML" "TestRunnerCliBootstrap.RunEditMode" "Game.Integration.Fuzz.Tests" ""
}

parse_arguments() {
    if [ "${1:-}" = "--print-config" ]; then
        if [ "$#" -ne 1 ]; then
            print_usage
            exit 1
        fi
        RUN_MODE="--print-config"
        return
    fi

    if [ "${1:-}" = "--dry-run" ]; then
        DRY_RUN=1
        shift
    fi

    RUN_MODE="${1:-}"
    if [ -z "$RUN_MODE" ]; then
        print_usage
        exit 1
    fi
    shift

    while [ "$#" -gt 0 ]; do
        case "$1" in
            --filter|--test-filter)
                local filter_arg="$1"
                shift
                if [ "$#" -eq 0 ] || [ -z "${1:-}" ]; then
                    echo "ERROR: $filter_arg requires a non-empty test filter."
                    print_usage
                    exit 1
                fi
                TEST_FILTER="$1"
                shift
                ;;
            --list-tests)
                echo "ERROR: --list-tests is not supported by this wrapper. Use --dry-run <lane> to inspect the Unity selection plan."
                print_usage
                exit 1
                ;;
            --verbose)
                echo "ERROR: --verbose is not supported. Dry-run and failing Unity stages already print the selected assemblies, categories, and filter."
                print_usage
                exit 1
                ;;
            --repeat)
                echo "ERROR: --repeat is not supported by this wrapper. Re-run the same explicit lane command when repeat evidence is needed."
                print_usage
                exit 1
                ;;
            *)
                echo "ERROR: Unsupported argument: $1"
                print_usage
                exit 1
                ;;
        esac
    done
}

require_filtered_tests_if_needed() {
    if [ -n "$TEST_FILTER" ] && [ "$DRY_RUN" -eq 0 ] && [ "$FILTERED_TOTAL" -eq 0 ]; then
        echo "ERROR: Test filter matched no tests in lane '$RUN_MODE': $TEST_FILTER"
        exit 1
    fi
}

main() {
    local mode

    parse_arguments "$@"
    mode="$RUN_MODE"

    require_command wslpath
    initialize_project_paths
    validate_project_paths

    if [ "$mode" = "--print-config" ]; then
        print_config
        return 0
    fi

    print_environment_summary
    if [ -n "$TEST_FILTER" ]; then
        echo "Test filter: $TEST_FILTER"
    fi

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
        core-feature-gate)
            run_dotnet_core
            run_unity_core_feature_gate
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
            print_usage
            exit 1
            ;;
    esac

    require_filtered_tests_if_needed

    if [ "$DRY_RUN" -eq 0 ]; then
        echo "ALL TESTS PASSED"
    fi
}

main "$@"
