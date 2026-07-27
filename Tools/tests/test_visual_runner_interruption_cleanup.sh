#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUN_TESTS_LIBRARY_ONLY=1
source "$ROOT_DIR/run_tests.sh"

assert_equal() {
    local expected="$1"
    local actual="$2"
    local label="$3"

    if [ "$expected" != "$actual" ]; then
        echo "ERROR: $label"
        echo "  expected: $expected"
        echo "  actual:   $actual"
        exit 1
    fi
}

wait_for_file_content() {
    local path="$1"
    local expected="$2"
    local attempt

    for attempt in $(seq 1 100); do
        if [ -f "$path" ] && [ "$(cat "$path")" = "$expected" ]; then
            return 0
        fi
        sleep 0.05
    done
    echo "ERROR: Timed out waiting for $path to contain $expected"
    return 1
}

run_scenario_child() {
    local lane="$1"
    local scenario="$2"
    local signal_name="$3"
    local scenario_root="$4"
    local guarded_file="$scenario_root/project/guarded.asset"
    local baseline_root="$scenario_root/baseline"
    local mutation_evidence="$scenario_root/mutation.log"
    local lifecycle_evidence="$scenario_root/lifecycle.log"
    local child_marker="$scenario_root/child.pid"
    local runner_status=0
    local runner_shell_pid="$BASHPID"

    PROJECT_PATH_WSL="$scenario_root/project"
    capture_guarded_paths() {
        printf '%s\n' "guarded.asset"
    }
    terminate_current_project_unity_processes() {
        return 0
    }
    find_current_project_unity_processes() {
        return 0
    }

    visual_guard_begin \
        "$baseline_root" \
        "$mutation_evidence" \
        "$lifecycle_evidence" \
        "$lane"

    if [ "$scenario" = "normal" ]; then
        visual_guard_run_command bash -c 'exit 0'
        observe_capture_assets_before_restore \
            "$baseline_root" \
            "$mutation_evidence"
        visual_guard_mark_observation_complete "PASS"
        visual_guard_finish 0
    elif [ "$scenario" = "failure" ]; then
        visual_guard_run_command bash -c \
            'printf "mutated\n" > "$1"; exit 37' \
            _ "$guarded_file" || runner_status=$?
        observe_capture_assets_before_restore \
            "$baseline_root" \
            "$mutation_evidence" || true
        visual_guard_mark_observation_complete "FAIL"
        visual_guard_finish "$runner_status"
    else
        (
            wait_for_file_content "$guarded_file" "mutated"
            kill "-$signal_name" "$runner_shell_pid"
        ) &
        visual_guard_run_command bash -c \
            'printf "%s\n" "$$" > "$2"; printf "mutated\n" > "$1"; exec sleep 300' \
            _ "$guarded_file" "$child_marker"
    fi
}

if [ "${1:-}" = "--scenario-child" ]; then
    run_scenario_child "$2" "$3" "$4" "$5"
    exit $?
fi

TEST_ROOT="$(mktemp -d)"
trap 'rm -rf "$TEST_ROOT"' EXIT

run_guard_scenario() {
    local lane="$1"
    local scenario="$2"
    local signal_name="${3:-}"
    local expected_status="$4"
    local scenario_root="$TEST_ROOT/$lane-$scenario"
    local guarded_file="$scenario_root/project/guarded.asset"
    local baseline_root="$scenario_root/baseline"
    local mutation_evidence="$scenario_root/mutation.log"
    local lifecycle_evidence="$scenario_root/lifecycle.log"
    local child_marker="$scenario_root/child.pid"
    local runner_status=0
    local child_pid

    mkdir -p "$scenario_root/project" "$baseline_root"
    printf 'baseline-%s\n' "$lane" > "$guarded_file"
    cp "$guarded_file" "$baseline_root/guarded.asset"

    if bash "$0" \
        --scenario-child \
        "$lane" \
        "$scenario" \
        "$signal_name" \
        "$scenario_root"; then
        runner_status=0
    else
        runner_status=$?
    fi

    assert_equal "$expected_status" "$runner_status" "$lane $scenario status"
    assert_equal "baseline-$lane" "$(cat "$guarded_file")" "$lane $scenario restore"
    assert_equal "$(
        if [ -n "$signal_name" ]; then printf true; else printf false; fi
    )" "$(sed -n 's/^interrupted=//p' "$lifecycle_evidence")" "$lane $scenario interrupted"
    assert_equal "PASS" "$(sed -n 's/^restore_result=//p' "$lifecycle_evidence")" "$lane $scenario restore result"
    assert_equal "$expected_status" "$(sed -n 's/^final_exit_status=//p' "$lifecycle_evidence")" "$lane $scenario final status"
    assert_equal "1" "$(sed -n 's/^cleanup_trap_installed=//p' "$lifecycle_evidence")" "$lane $scenario trap"
    assert_equal "1" "$(sed -n 's/^cleanup_started=//p' "$lifecycle_evidence")" "$lane $scenario cleanup started"
    assert_equal "1" "$(sed -n 's/^cleanup_completed=//p' "$lifecycle_evidence")" "$lane $scenario cleanup completed"
    assert_equal "$signal_name" "$(sed -n 's/^termination_signal=//p' "$lifecycle_evidence")" "$lane $scenario signal"

    if [ -n "$signal_name" ]; then
        assert_equal "INTERRUPTED" "$(sed -n 's/^lane_verdict=//p' "$lifecycle_evidence")" "$lane $scenario verdict"
        assert_equal "INTERRUPTED_BEFORE_COMPLETE_CLASSIFICATION" "$(
            sed -n 's/^observation_order=//p' "$mutation_evidence"
        )" "$lane $scenario interrupted observation"
    fi

    if [ -f "$child_marker" ]; then
        child_pid="$(cat "$child_marker")"
        if kill -0 "$child_pid" 2>/dev/null; then
            echo "ERROR: $lane $scenario child remains: $child_pid"
            exit 1
        fi
    fi
    echo "$lane $scenario: PASS"
}

for lane in ObjectiveHud Typography; do
    run_guard_scenario "$lane" normal "" 0
    run_guard_scenario "$lane" failure "" 37
    run_guard_scenario "$lane" sigint INT 130
    run_guard_scenario "$lane" sigterm TERM 143
done

partial_root="$TEST_ROOT/partial"
mkdir -p "$partial_root/project"
printf 'user-bytes\n' > "$partial_root/project/guarded.asset"
PROJECT_PATH_WSL="$partial_root/project"
capture_guarded_paths() {
    printf '%s\n' "guarded.asset"
}
visual_guard_reset_state
visual_guard_cleanup 0
visual_guard_cleanup 0
assert_equal "user-bytes" "$(cat "$partial_root/project/guarded.asset")" "partial baseline no-op"
assert_equal "SKIPPED_BASELINE_NOT_READY" "$VISUAL_GUARD_RESTORE_RESULT" "partial baseline result"

idempotent_root="$TEST_ROOT/idempotent"
mkdir -p "$idempotent_root/project" "$idempotent_root/baseline"
printf 'baseline\n' > "$idempotent_root/project/guarded.asset"
cp "$idempotent_root/project/guarded.asset" "$idempotent_root/baseline/guarded.asset"
PROJECT_PATH_WSL="$idempotent_root/project"
visual_guard_begin \
    "$idempotent_root/baseline" \
    "$idempotent_root/mutation.log" \
    "$idempotent_root/lifecycle.log" \
    "Idempotent"
printf 'mutated\n' > "$idempotent_root/project/guarded.asset"
visual_guard_cleanup 0
visual_guard_cleanup 0
visual_guard_finish 0
assert_equal "baseline" "$(cat "$idempotent_root/project/guarded.asset")" "double cleanup restore"
echo "partial baseline and double cleanup: PASS"

cleanup_failure_root="$TEST_ROOT/cleanup-failure"
mkdir -p "$cleanup_failure_root/project" "$cleanup_failure_root/baseline"
printf 'baseline\n' > "$cleanup_failure_root/project/guarded.asset"
cp "$cleanup_failure_root/project/guarded.asset" "$cleanup_failure_root/baseline/guarded.asset"
cleanup_failure_status=0
if (
    PROJECT_PATH_WSL="$cleanup_failure_root/project"
    capture_guarded_paths() {
        printf '%s\n' "guarded.asset"
    }
    terminate_current_project_unity_processes() {
        return 0
    }
    find_current_project_unity_processes() {
        return 0
    }
    restore_capture_assets_from_baseline() {
        return 29
    }
    visual_guard_begin \
        "$cleanup_failure_root/baseline" \
        "$cleanup_failure_root/mutation-zero.log" \
        "$cleanup_failure_root/lifecycle-zero.log" \
        "CleanupFailureZero"
    visual_guard_finish 0
); then
    echo "ERROR: cleanup failure replaced a failure with success"
    exit 1
else
    cleanup_failure_status=$?
fi
assert_equal "1" "$cleanup_failure_status" "zero status becomes cleanup failure"

if (
    PROJECT_PATH_WSL="$cleanup_failure_root/project"
    capture_guarded_paths() {
        printf '%s\n' "guarded.asset"
    }
    terminate_current_project_unity_processes() {
        return 0
    }
    find_current_project_unity_processes() {
        return 0
    }
    restore_capture_assets_from_baseline() {
        return 29
    }
    visual_guard_begin \
        "$cleanup_failure_root/baseline" \
        "$cleanup_failure_root/mutation-nonzero.log" \
        "$cleanup_failure_root/lifecycle-nonzero.log" \
        "CleanupFailureNonzero"
    visual_guard_finish 37
); then
    echo "ERROR: original nonzero status was replaced with success"
    exit 1
else
    cleanup_failure_status=$?
fi
assert_equal "37" "$cleanup_failure_status" "nonzero status survives cleanup failure"
echo "cleanup failure status preservation: PASS"

echo "visual runner interruption cleanup checks passed"
