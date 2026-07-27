#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUN_TESTS_LIBRARY_ONLY=1
source "$ROOT_DIR/run_tests.sh"
VISUAL_GUARD_TEST_HOOKS_ENABLED=1
VISUAL_GUARD_TEST_RETURN_AFTER_FINALIZE=1

TEST_MONOTONIC_MS=100000
visual_guard_monotonic_ms() {
    printf '%s\n' "$TEST_MONOTONIC_MS"
}
visual_guard_sleep_ms() {
    TEST_MONOTONIC_MS=$((TEST_MONOTONIC_MS + $1))
}

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

assert_interruption_status() {
    local actual="$1"
    local label="$2"

    if [ "$actual" != "130" ] && [ "$actual" != "143" ]; then
        echo "ERROR: $label"
        echo "  expected: 130 or 143"
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
    local command_ready_marker="$scenario_root/command-ready"
    local first_observed_marker="$scenario_root/first-observed"
    local second_observed_marker="$scenario_root/second-observed"
    local cleanup_waiting_marker="$scenario_root/cleanup-waiting"
    local cleanup_release_marker="$scenario_root/cleanup-release"
    local command_release_marker="$scenario_root/command-release"
    local runner_status=0
    local runner_shell_pid="$BASHPID"
    local cleanup_signal_sequence=""
    local cleanup_signal_phase="before_restore"
    local finalization_signal_sequence=""
    local finalization_hook_phase=""
    local force_process_cleanup_failure=0
    local finalize_via_exit=0

    PROJECT_PATH_WSL="$scenario_root/project"
    PROJECT_PATH_WIN="$PROJECT_PATH_WSL"
    capture_guarded_paths() {
        printf '%s\n' "guarded.asset"
    }
    visual_guard_iter_unity_process_records() {
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
    elif [ "$scenario" = "sigint" ] || [ "$scenario" = "sigterm" ]; then
        (
            wait_for_file_content "$guarded_file" "mutated"
            kill "-$signal_name" "$runner_shell_pid"
        ) &
        visual_guard_run_command bash -c \
            'printf "%s\n" "$$" > "$2"; printf "mutated\n" > "$1"; exec sleep 300' \
            _ "$guarded_file" "$child_marker"
    elif [[ "$scenario" == sequential-int-term* ]] ||
         [[ "$scenario" == sequential-term-int* ]]; then
        local first_signal="INT"
        local second_signal="TERM"
        if [[ "$scenario" == sequential-term-int* ]]; then
            first_signal="TERM"
            second_signal="INT"
        fi
        visual_guard_signal_observed_test_hook() {
            local observed_name="$1"
            local first_observation="$3"

            if [ "$first_observation" -eq 1 ]; then
                printf '%s\n' "$observed_name" > "$first_observed_marker"
            else
                printf '%s\n' "$observed_name" > "$second_observed_marker"
            fi
        }
        visual_guard_cleanup_test_hook() {
            local phase="$1"

            if [ "$phase" != "before_restore" ]; then
                return 0
            fi
            printf 'waiting\n' > "$cleanup_waiting_marker"
            while [ ! -f "$cleanup_release_marker" ]; do
                sleep 0.05
            done
        }
        (
            wait_for_file_content "$command_ready_marker" "ready"
            kill "-$first_signal" "$runner_shell_pid"
            wait_for_file_content "$first_observed_marker" "$first_signal"
            wait_for_file_content "$cleanup_waiting_marker" "waiting"
            kill "-$second_signal" "$runner_shell_pid"
            wait_for_file_content "$second_observed_marker" "$second_signal"
            printf 'release\n' > "$cleanup_release_marker"
        ) &
        visual_guard_run_command bash -c \
            'printf "ready\n" > "$3"; printf "%s\n" "$$" > "$2"; printf "mutated\n" > "$1"; exec sleep 300' \
            _ "$guarded_file" "$child_marker" "$command_ready_marker"
    elif [[ "$scenario" == co-pending-int-term* ]] ||
         [[ "$scenario" == co-pending-term-int* ]]; then
        local first_sent_signal="INT"
        local second_sent_signal="TERM"
        if [[ "$scenario" == co-pending-term-int* ]]; then
            first_sent_signal="TERM"
            second_sent_signal="INT"
        fi
        (
            wait_for_file_content "$command_ready_marker" "ready"
            kill "-$first_sent_signal" "$runner_shell_pid"
            kill "-$second_sent_signal" "$runner_shell_pid"
            printf 'release\n' > "$command_release_marker"
        ) &
        printf 'mutated\n' > "$guarded_file"
        bash -c \
            'printf "ready\n" > "$1"; while [ ! -f "$2" ]; do sleep 0.05; done' \
            _ "$command_ready_marker" "$command_release_marker"
        observe_capture_assets_before_restore \
            "$baseline_root" \
            "$mutation_evidence" || true
        visual_guard_mark_observation_complete "PASS"
        visual_guard_finish 0
    else
        case "$scenario" in
            cleanup-int)
                cleanup_signal_sequence="INT"
                ;;
            cleanup-term)
                cleanup_signal_sequence="TERM"
                ;;
            cleanup-int-term)
                cleanup_signal_sequence="INT TERM"
                ;;
            cleanup-term-int)
                cleanup_signal_sequence="TERM INT"
                ;;
            failure-cleanup-int)
                cleanup_signal_sequence="INT"
                runner_status=37
                ;;
            failure-cleanup-term)
                cleanup_signal_sequence="TERM"
                runner_status=37
                ;;
            cleanup-failure-int)
                cleanup_signal_sequence="INT"
                force_process_cleanup_failure=1
                ;;
            cleanup-failure-term)
                cleanup_signal_sequence="TERM"
                force_process_cleanup_failure=1
                ;;
            completed-race-int)
                cleanup_signal_sequence="INT"
                cleanup_signal_phase="after_restore_before_completed"
                ;;
            before-cleanup-completed-int)
                finalization_signal_sequence="INT"
                finalization_hook_phase="before_cleanup_completed"
                ;;
            after-cleanup-completed-term)
                finalization_signal_sequence="TERM"
                finalization_hook_phase="after_cleanup_completed"
                ;;
            before-finalizing-int)
                finalization_signal_sequence="INT"
                finalization_hook_phase="before_finalizing"
                ;;
            after-finalizing-term)
                finalization_signal_sequence="TERM"
                finalization_hook_phase="after_finalizing"
                ;;
            before-snapshot-int)
                finalization_signal_sequence="INT"
                finalization_hook_phase="before_final_status_snapshot"
                ;;
            before-snapshot-term)
                finalization_signal_sequence="TERM"
                finalization_hook_phase="before_final_status_snapshot"
                ;;
            after-snapshot-int)
                finalization_signal_sequence="INT"
                finalization_hook_phase="after_final_status_snapshot"
                ;;
            after-snapshot-term)
                finalization_signal_sequence="TERM"
                finalization_hook_phase="after_final_status_snapshot"
                ;;
            exit-after-snapshot-int)
                finalization_signal_sequence="INT"
                finalization_hook_phase="after_final_status_snapshot"
                finalize_via_exit=1
                ;;
            exit-after-snapshot-term)
                finalization_signal_sequence="TERM"
                finalization_hook_phase="after_final_status_snapshot"
                finalize_via_exit=1
                ;;
            failure-after-snapshot-int)
                finalization_signal_sequence="INT"
                finalization_hook_phase="after_final_status_snapshot"
                runner_status=37
                ;;
            cleanup-failure-after-snapshot-term)
                finalization_signal_sequence="TERM"
                finalization_hook_phase="after_final_status_snapshot"
                force_process_cleanup_failure=1
                ;;
            before-final-exit-int)
                finalization_signal_sequence="INT"
                finalization_hook_phase="before_final_exit"
                ;;
            before-final-exit-term)
                finalization_signal_sequence="TERM"
                finalization_hook_phase="before_final_exit"
                ;;
            *)
                echo "ERROR: unsupported scenario: $scenario"
                return 1
                ;;
        esac
        visual_guard_cleanup_test_hook() {
            local phase="$1"
            local cleanup_signal

            if [ "$phase" != "$cleanup_signal_phase" ]; then
                return 0
            fi
            for cleanup_signal in $cleanup_signal_sequence; do
                kill "-$cleanup_signal" "$BASHPID"
            done
        }
        visual_guard_finalization_test_hook() {
            local phase="$1"
            local finalization_signal

            if [ "$phase" != "$finalization_hook_phase" ]; then
                return 0
            fi
            for finalization_signal in $finalization_signal_sequence; do
                kill "-$finalization_signal" "$BASHPID"
            done
        }
        if [ "$force_process_cleanup_failure" -eq 1 ]; then
            visual_guard_stop_active_child() {
                VISUAL_GUARD_CLEANUP_PROCESS_RESULT="FAILED_TEST_PROCESS_CLEANUP"
                return 1
            }
        fi
        printf 'mutated\n' > "$guarded_file"
        observe_capture_assets_before_restore \
            "$baseline_root" \
            "$mutation_evidence" || true
        if [ "$runner_status" -eq 0 ]; then
            visual_guard_mark_observation_complete "PASS"
        else
            visual_guard_mark_observation_complete "FAIL"
        fi
        if [ "$finalize_via_exit" -eq 1 ]; then
            exit "$runner_status"
        fi
        visual_guard_finish "$runner_status"
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
    local first_observed_signal
    local first_observed_status
    local actual_observed_int
    local actual_observed_term
    local actual_observed_mask
    local actual_co_pending
    local expected_first_observed_signal="$signal_name"
    local expected_termination_signal="$signal_name"
    local expected_interrupted=false
    local expected_observed_int=false
    local expected_observed_term=false
    local expected_observed_mask=none
    local expected_co_pending=false

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

    if [ "$expected_status" = "130|143" ]; then
        assert_interruption_status "$runner_status" "$lane $scenario status"
    else
        assert_equal "$expected_status" "$runner_status" "$lane $scenario status"
    fi
    first_observed_signal="$(
        sed -n 's/^first_observed_signal=//p' "$lifecycle_evidence"
    )"
    first_observed_status="$(
        sed -n 's/^first_observed_status=//p' "$lifecycle_evidence"
    )"
    actual_observed_int="$(sed -n 's/^observed_int=//p' "$lifecycle_evidence")"
    actual_observed_term="$(sed -n 's/^observed_term=//p' "$lifecycle_evidence")"
    actual_observed_mask="$(sed -n 's/^observed_signal_mask=//p' "$lifecycle_evidence")"
    actual_co_pending="$(sed -n 's/^co_pending_detected=//p' "$lifecycle_evidence")"
    if [ -n "$signal_name" ]; then
        expected_interrupted=true
        expected_co_pending=unknown
    else
        expected_first_observed_signal=none
        expected_termination_signal=""
    fi
    case "$scenario" in
        sequential-*)
            expected_observed_int=true
            expected_observed_term=true
            expected_observed_mask="INT|TERM"
            expected_co_pending=unknown
            ;;
        co-pending-*)
            case "$actual_observed_mask" in
                INT)
                    assert_equal "INT" "$first_observed_signal" "$lane $scenario single dispatched signal"
                    assert_equal "true" "$actual_observed_int" "$lane $scenario observed INT"
                    assert_equal "false" "$actual_observed_term" "$lane $scenario unobserved TERM"
                    assert_equal "unknown" "$actual_co_pending" "$lane $scenario co-pending inference"
                    ;;
                TERM)
                    assert_equal "TERM" "$first_observed_signal" "$lane $scenario single dispatched signal"
                    assert_equal "false" "$actual_observed_int" "$lane $scenario unobserved INT"
                    assert_equal "true" "$actual_observed_term" "$lane $scenario observed TERM"
                    assert_equal "unknown" "$actual_co_pending" "$lane $scenario co-pending inference"
                    ;;
                "INT|TERM")
                    assert_equal "true" "$actual_observed_int" "$lane $scenario observed INT"
                    assert_equal "true" "$actual_observed_term" "$lane $scenario observed TERM"
                    assert_equal "unknown" "$actual_co_pending" "$lane $scenario co-pending inference"
                    ;;
                *)
                    echo "ERROR: $lane $scenario invalid observed signal mask: $actual_observed_mask"
                    exit 1
                    ;;
            esac
            expected_observed_int="$actual_observed_int"
            expected_observed_term="$actual_observed_term"
            expected_observed_mask="$actual_observed_mask"
            expected_co_pending="$actual_co_pending"
            ;;
        *)
            if [ "$signal_name" = "INT" ]; then
                expected_observed_int=true
                expected_observed_mask=INT
            elif [ "$signal_name" = "TERM" ]; then
                expected_observed_term=true
                expected_observed_mask=TERM
            fi
            ;;
    esac
    if [[ "$scenario" == co-pending-* ]]; then
        expected_first_observed_signal="$first_observed_signal"
        expected_termination_signal="$first_observed_signal"
        assert_interruption_status "$first_observed_status" "$lane $scenario first observed status"
        if [ "$first_observed_signal" = "INT" ]; then
            assert_equal "130" "$first_observed_status" "$lane $scenario INT mapping"
        elif [ "$first_observed_signal" = "TERM" ]; then
            assert_equal "143" "$first_observed_status" "$lane $scenario TERM mapping"
        else
            echo "ERROR: $lane $scenario invalid first observed signal: $first_observed_signal"
            exit 1
        fi
    elif [ -n "$signal_name" ]; then
        assert_equal "$expected_status" "$first_observed_status" "$lane $scenario first observed status"
    else
        assert_equal "0" "$first_observed_status" "$lane $scenario no interruption status"
    fi
    assert_equal "baseline-$lane" "$(cat "$guarded_file")" "$lane $scenario restore"
    assert_equal "$expected_interrupted" "$(sed -n 's/^interrupted=//p' "$lifecycle_evidence")" "$lane $scenario interrupted"
    assert_equal "PASS" "$(sed -n 's/^restore_result=//p' "$lifecycle_evidence")" "$lane $scenario restore result"
    assert_equal "$runner_status" "$(sed -n 's/^final_exit_status=//p' "$lifecycle_evidence")" "$lane $scenario final status"
    assert_equal "1" "$(sed -n 's/^cleanup_trap_installed=//p' "$lifecycle_evidence")" "$lane $scenario trap"
    assert_equal "1" "$(sed -n 's/^cleanup_started=//p' "$lifecycle_evidence")" "$lane $scenario cleanup started"
    assert_equal "1" "$(sed -n 's/^cleanup_completed=//p' "$lifecycle_evidence")" "$lane $scenario cleanup completed"
    assert_equal "1" "$(sed -n 's/^cleanup_effective_count=//p' "$lifecycle_evidence")" "$lane $scenario cleanup count"
    assert_equal "1" "$(sed -n 's/^process_cleanup_effective_count=//p' "$lifecycle_evidence")" "$lane $scenario process cleanup count"
    assert_equal "1" "$(sed -n 's/^mutation_observation_effective_count=//p' "$lifecycle_evidence")" "$lane $scenario mutation observation count"
    assert_equal "1" "$(sed -n 's/^asset_restore_effective_count=//p' "$lifecycle_evidence")" "$lane $scenario asset restore count"
    assert_equal "FIRST_OBSERVED_SEQUENTIAL_CO_PENDING_UNSPECIFIED" "$(
        sed -n 's/^signal_order_contract=//p' "$lifecycle_evidence"
    )" "$lane $scenario signal order contract"
    assert_equal "$expected_first_observed_signal" "$first_observed_signal" "$lane $scenario first observed signal"
    assert_equal "$expected_termination_signal" "$(sed -n 's/^termination_signal=//p' "$lifecycle_evidence")" "$lane $scenario signal"
    assert_equal "$expected_observed_int" "$(sed -n 's/^observed_int=//p' "$lifecycle_evidence")" "$lane $scenario observed INT"
    assert_equal "$expected_observed_term" "$(sed -n 's/^observed_term=//p' "$lifecycle_evidence")" "$lane $scenario observed TERM"
    assert_equal "$expected_observed_mask" "$(sed -n 's/^observed_signal_mask=//p' "$lifecycle_evidence")" "$lane $scenario observed mask"
    assert_equal "$expected_co_pending" "$(sed -n 's/^co_pending_detected=//p' "$lifecycle_evidence")" "$lane $scenario co-pending detection"
    assert_equal "$first_observed_status" "$(sed -n 's/^final_interruption_status=//p' "$lifecycle_evidence")" "$lane $scenario interruption status"
    assert_equal "$runner_status" "$(
        sed -n 's/^final_exit_status=//p' "$lifecycle_evidence"
    )" "$lane $scenario lifecycle status"

    if [ -n "$signal_name" ]; then
        assert_equal "INTERRUPTED" "$(sed -n 's/^lane_verdict=//p' "$lifecycle_evidence")" "$lane $scenario verdict"
        if [ "$scenario" = "sigint" ] || [ "$scenario" = "sigterm" ]; then
            assert_equal "INTERRUPTED_BEFORE_COMPLETE_CLASSIFICATION" "$(
                sed -n 's/^observation_order=//p' "$mutation_evidence"
            )" "$lane $scenario interrupted observation"
        fi
    fi

    if [ -f "$child_marker" ]; then
        child_pid="$(cat "$child_marker")"
        if kill -0 "$child_pid" 2>/dev/null; then
            echo "ERROR: $lane $scenario child remains: $child_pid"
            exit 1
        fi
    fi
    echo "$lane $scenario: PASS (status=$runner_status, first_observed=$first_observed_signal, observed_mask=$actual_observed_mask)"
}

for lane in ObjectiveHud Typography; do
    run_guard_scenario "$lane" normal "" 0
    run_guard_scenario "$lane" failure "" 37
    run_guard_scenario "$lane" sigint INT 130
    run_guard_scenario "$lane" sigterm TERM 143
    run_guard_scenario "$lane" cleanup-int INT 130
    run_guard_scenario "$lane" cleanup-term TERM 143
done

run_guard_scenario ObjectiveHud failure-cleanup-int INT 130
run_guard_scenario ObjectiveHud failure-cleanup-term TERM 143
run_guard_scenario ObjectiveHud cleanup-failure-int INT 130
run_guard_scenario ObjectiveHud cleanup-failure-term TERM 143
run_guard_scenario ObjectiveHud completed-race-int INT 130
run_guard_scenario ObjectiveHud before-cleanup-completed-int INT 130
run_guard_scenario ObjectiveHud after-cleanup-completed-term TERM 143
run_guard_scenario ObjectiveHud before-finalizing-int INT 130
run_guard_scenario ObjectiveHud after-finalizing-term TERM 143
run_guard_scenario ObjectiveHud before-snapshot-int INT 130
run_guard_scenario ObjectiveHud before-snapshot-term TERM 143
run_guard_scenario ObjectiveHud after-snapshot-int INT 130
run_guard_scenario ObjectiveHud after-snapshot-term TERM 143
run_guard_scenario ObjectiveHud exit-after-snapshot-int INT 130
run_guard_scenario ObjectiveHud exit-after-snapshot-term TERM 143
run_guard_scenario ObjectiveHud failure-after-snapshot-int INT 130
run_guard_scenario ObjectiveHud cleanup-failure-after-snapshot-term TERM 143
run_guard_scenario ObjectiveHud before-final-exit-int INT 130
run_guard_scenario ObjectiveHud before-final-exit-term TERM 143
run_guard_scenario ObjectiveHud sequential-int-term INT 130
run_guard_scenario ObjectiveHud sequential-term-int TERM 143
for iteration in 1 2 3; do
    run_guard_scenario \
        ObjectiveHud \
        "co-pending-int-term-$iteration" \
        CO_PENDING \
        "130|143"
    run_guard_scenario \
        ObjectiveHud \
        "co-pending-term-int-$iteration" \
        CO_PENDING \
        "130|143"
done

visual_guard_iter_unity_process_records() {
    return 0
}

partial_root="$TEST_ROOT/partial"
mkdir -p "$partial_root/project"
printf 'user-bytes\n' > "$partial_root/project/guarded.asset"
PROJECT_PATH_WSL="$partial_root/project"
PROJECT_PATH_WIN="$PROJECT_PATH_WSL"
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
idempotent_status=0
if (
    PROJECT_PATH_WSL="$idempotent_root/project"
    PROJECT_PATH_WIN="$PROJECT_PATH_WSL"
    capture_guarded_paths() {
        printf '%s\n' "guarded.asset"
    }
    visual_guard_iter_unity_process_records() {
        return 0
    }
    visual_guard_begin \
        "$idempotent_root/baseline" \
        "$idempotent_root/mutation.log" \
        "$idempotent_root/lifecycle.log" \
        "Idempotent"
    printf 'mutated\n' > "$idempotent_root/project/guarded.asset"
    observe_capture_assets_before_restore \
        "$idempotent_root/baseline" \
        "$idempotent_root/mutation.log" || true
    visual_guard_cleanup 37
    visual_guard_cleanup 37
    visual_guard_finish 37
); then
    idempotent_status=0
else
    idempotent_status=$?
fi
assert_equal "baseline" "$(cat "$idempotent_root/project/guarded.asset")" "double cleanup restore"
assert_equal "1" "$(sed -n 's/^cleanup_effective_count=//p' "$idempotent_root/lifecycle.log")" "double cleanup effective count"
assert_equal "1" "$(sed -n 's/^process_cleanup_effective_count=//p' "$idempotent_root/lifecycle.log")" "double cleanup process count"
assert_equal "1" "$(sed -n 's/^asset_restore_effective_count=//p' "$idempotent_root/lifecycle.log")" "double cleanup restore count"
assert_equal "37" "$idempotent_status" "double cleanup original status"
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

assert_path_match() {
    local expected="$1"
    local label="$2"
    shift 2
    local candidate

    candidate="$(extract_project_path_from_argv "$@")" || {
        echo "ERROR: $label did not parse -projectPath"
        exit 1
    }
    if ! visual_project_paths_match "$candidate" "$expected"; then
        echo "ERROR: $label exact path mismatch"
        echo "  expected: $expected"
        echo "  candidate: $candidate"
        exit 1
    fi
}

assert_path_no_match() {
    local expected="$1"
    local label="$2"
    shift 2
    local candidate

    candidate="$(extract_project_path_from_argv "$@" || true)"
    if [ -n "$candidate" ] && visual_project_paths_match "$candidate" "$expected"; then
        echo "ERROR: $label unexpectedly matched"
        echo "  expected: $expected"
        echo "  candidate: $candidate"
        exit 1
    fi
}

path_root="$TEST_ROOT/path parser"
mkdir -p "$path_root/game" "$path_root/game-copy" "$path_root/game/sub"
assert_path_match "$path_root/game" "exact path" \
    Unity.exe -projectPath "$path_root/game"
assert_path_no_match "$path_root/game" "copy suffix" \
    Unity.exe -projectPath "$path_root/game-copy"
assert_path_no_match "$path_root/game" "subdirectory" \
    Unity.exe -projectPath "$path_root/game/sub"
assert_path_no_match "$path_root/game" "log argument is not ownership" \
    Unity.exe -logFile "$path_root/game/log" -projectPath "$path_root/game-copy"
assert_path_no_match "$path_root/game" "missing projectPath" \
    Unity.exe -logFile "$path_root/game/log"
assert_path_match "$path_root/game" "quoted whitespace token" \
    Unity.exe -projectPath "$path_root/game"
assert_path_match "C:\\Repo\\Game" "Windows and WSL equivalence" \
    Unity.exe -PROJECTPATH "/mnt/c/Repo/Game"
echo "exact projectPath parser checks: PASS"

process_root="$TEST_ROOT/process-ownership"
mkdir -p "$process_root/project" "$process_root/baseline"
printf 'baseline\n' > "$process_root/project/guarded.asset"
cp "$process_root/project/guarded.asset" "$process_root/baseline/guarded.asset"
process_inventory="$process_root/inventory.tsv"
process_killed="$process_root/killed.log"
expected_project="$process_root/project"
similar_project="$process_root/project-copy"
sub_project="$process_root/project/sub"

write_process_record() {
    local source="$1"
    local pid="$2"
    local project_path="$3"

    printf '%s\t%s\t1\tstart-%s\t%s\n' \
        "$source" \
        "$pid" \
        "$pid" \
        "$(printf '%s' "$project_path" | base64 -w0)"
}

visual_guard_iter_unity_process_records() {
    local source
    local pid
    local ppid
    local start_identity
    local encoded

    while IFS=$'\t' read -r source pid ppid start_identity encoded; do
        if [ -s "$process_killed" ] &&
           grep -Fx -- "$source:$pid" "$process_killed" >/dev/null; then
            continue
        fi
        printf '%s\t%s\t%s\t%s\t%s\n' \
            "$source" "$pid" "$ppid" "$start_identity" "$encoded"
    done < "$process_inventory"
}

visual_guard_terminate_candidate() {
    printf '%s:%s\n' "$1" "$2" >> "$process_killed"
}

write_process_record wsl 100 "$expected_project" > "$process_inventory"
PROJECT_PATH_WSL="$process_root/project"
PROJECT_PATH_WIN="$expected_project"
capture_guarded_paths() {
    printf '%s\n' "guarded.asset"
}
visual_guard_begin \
    "$process_root/baseline" \
    "$process_root/mutation.log" \
    "$process_root/lifecycle.log" \
    "ProcessOwnership"
{
    write_process_record wsl 100 "$expected_project"
    write_process_record wsl 101 "$expected_project"
    write_process_record wsl 102 "$similar_project"
    write_process_record wsl 103 "$sub_project"
    write_process_record wsl 104 ""
} > "$process_inventory"
visual_guard_finish 0

assert_equal "wsl:101" "$(cat "$process_killed")" "only new exact-path process killed"
assert_equal "1" "$VISUAL_GUARD_FALLBACK_USED" "fallback used for orphan exact candidate"
assert_equal "true" "$(sed -n 's/^fallback_scan_performed=//p' "$process_root/lifecycle.log")" "fallback scan evidence"
assert_equal "0" "$(sed -n 's/^final_survivor_count=//p' "$process_root/lifecycle.log")" "fallback final survivors"
assert_equal "1" "$(
    sed -n '/candidate_pid=100/,/termination_result=/p' "$process_root/lifecycle.log" |
        sed -n 's/^preexisting=//p' |
        sort -u
)" "same-path preexisting protected"
assert_equal "0" "$(
    sed -n '/candidate_pid=102/,/termination_result=/p' "$process_root/lifecycle.log" |
        sed -n 's/^match=//p' |
        sort -u
)" "similar path rejected"
assert_equal "0" "$(
    sed -n '/candidate_pid=103/,/termination_result=/p' "$process_root/lifecycle.log" |
        sed -n 's/^match=//p' |
        sort -u
)" "subproject rejected"
assert_equal "0" "$(
    sed -n '/candidate_pid=104/,/termination_result=/p' "$process_root/lifecycle.log" |
        sed -n 's/^match=//p' |
        sort -u
)" "missing projectPath rejected"
echo "fallback exact ownership and preexisting protection: PASS"

primary_root="$TEST_ROOT/primary-ownership"
mkdir -p "$primary_root/project" "$primary_root/baseline"
printf 'baseline\n' > "$primary_root/project/guarded.asset"
cp "$primary_root/project/guarded.asset" "$primary_root/baseline/guarded.asset"
: > "$process_inventory"
: > "$process_killed"
PROJECT_PATH_WSL="$primary_root/project"
PROJECT_PATH_WIN="$PROJECT_PATH_WSL"
visual_guard_begin \
    "$primary_root/baseline" \
    "$primary_root/mutation.log" \
    "$primary_root/lifecycle.log" \
    "PrimaryOwnership"
setsid sleep 300 &
primary_pid=$!
VISUAL_GUARD_ACTIVE_CHILD_PID="$primary_pid"
VISUAL_GUARD_ACTIVE_CHILD_PGID="$primary_pid"
VISUAL_GUARD_OWNED_CHILD_PID="$primary_pid"
VISUAL_GUARD_OWNED_CHILD_PGID="$primary_pid"
visual_guard_finish 0
if kill -0 "$primary_pid" 2>/dev/null; then
    echo "ERROR: primary owned child remains: $primary_pid"
    exit 1
fi
assert_equal "0" "$VISUAL_GUARD_FALLBACK_USED" "primary-only cleanup has no fallback candidate"
assert_equal "1" "$VISUAL_GUARD_FALLBACK_SCAN_PERFORMED" "valid primary metadata still runs fallback"
assert_equal "33" "$VISUAL_GUARD_FALLBACK_SCAN_PASSES" "primary cleanup observes full grace and quiet period"
assert_equal "1" "$VISUAL_GUARD_STARTUP_GRACE_COMPLETED" "primary cleanup grace completed"
assert_equal "6000" "$UNITY_DETACHED_STARTUP_GRACE_MS" "primary cleanup configured grace"
assert_equal "6400" "$VISUAL_GUARD_STARTUP_GRACE_ELAPSED_MS" "primary cleanup grace plus quiet elapsed"
assert_equal "1" "$VISUAL_GUARD_QUIET_PERIOD_COMPLETED" "primary cleanup quiet completed"
assert_equal "0" "$VISUAL_GUARD_FINAL_SURVIVOR_COUNT" "primary cleanup final survivors"
assert_equal "" "$(cat "$process_killed")" "primary termination does not use candidate killer"
echo "primary PID/PGID ownership: PASS"

run_detached_cleanup_scenario() (
    local name="$1"
    local primary_mode="$2"
    local candidate_mode="$3"
    local signal_timing="$4"
    local signal_name="$5"
    local original_status="$6"
    local expected_status="$7"
    local expected_survivors="$8"
    local scenario_root="$TEST_ROOT/detached-$name"
    local inventory="$scenario_root/inventory.tsv"
    local killed="$scenario_root/killed.log"
    local exact_project="$scenario_root/project"
    local similar_project="$scenario_root/project-copy"
    local primary_pid=""
    local scenario_status=0

    mkdir -p "$scenario_root/project" "$scenario_root/baseline"
    printf 'baseline\n' > "$scenario_root/project/guarded.asset"
    cp "$scenario_root/project/guarded.asset" "$scenario_root/baseline/guarded.asset"
    : > "$inventory"
    : > "$killed"

    PROJECT_PATH_WSL="$exact_project"
    PROJECT_PATH_WIN="$exact_project"
    UNITY_DETACHED_STARTUP_GRACE_MS=600
    UNITY_DETACHED_POLL_INTERVAL_MS=200
    UNITY_DETACHED_QUIET_PERIOD_MS=400
    UNITY_DETACHED_HARD_TIMEOUT_MS=1400
    capture_guarded_paths() {
        printf '%s\n' "guarded.asset"
    }
    visual_guard_iter_unity_process_records() {
        local source
        local pid
        local ppid
        local start_identity
        local encoded

        while IFS=$'\t' read -r source pid ppid start_identity encoded; do
            if [ -s "$killed" ] &&
               grep -Fx -- "$source:$pid" "$killed" >/dev/null; then
                continue
            fi
            printf '%s\t%s\t%s\t%s\t%s\n' \
                "$source" "$pid" "$ppid" "$start_identity" "$encoded"
        done < "$inventory"
    }
    visual_guard_terminate_candidate() {
        local source="$1"
        local pid="$2"

        if [ "$candidate_mode" = "refuses" ] && [ "$pid" = "201" ]; then
            return 1
        fi
        if [ "$candidate_mode" = "race" ] && [ "$pid" = "201" ]; then
            : > "$inventory"
            return 0
        fi
        printf '%s:%s\n' "$source" "$pid" >> "$killed"
        return 0
    }

    if [ "$candidate_mode" = "preexisting" ]; then
        write_process_record wsl 201 "$exact_project" > "$inventory"
    fi
    visual_guard_begin \
        "$scenario_root/baseline" \
        "$scenario_root/mutation.log" \
        "$scenario_root/lifecycle.log" \
        "DetachedSmoke"

    case "$candidate_mode" in
        exact|race|refuses)
            write_process_record wsl 201 "$exact_project" > "$inventory"
            ;;
        late)
            : > "$inventory"
            ;;
        similar)
            write_process_record wsl 201 "$similar_project" > "$inventory"
            ;;
        preexisting)
            ;;
        no-projectPath)
            write_process_record wsl 201 "" > "$inventory"
            ;;
        *)
            echo "ERROR: unsupported detached candidate mode: $candidate_mode"
            exit 1
            ;;
    esac

    visual_guard_fallback_scan_test_hook() {
        local pass="$1"
        local phase="$2"

        if [ "$candidate_mode" = "late" ] &&
           [ "$pass" -eq 1 ] &&
           [ "$phase" = "after_scan" ]; then
            write_process_record wsl 201 "$exact_project" > "$inventory"
        fi
        if [ "$signal_timing" = "during" ] &&
           [ "$pass" -eq 1 ] &&
           [ "$phase" = "before_scan" ]; then
            kill "-$signal_name" "$BASHPID"
        fi
        return 0
    }

    if [ "$primary_mode" = "live" ]; then
        setsid sleep 300 &
        primary_pid=$!
        VISUAL_GUARD_ACTIVE_CHILD_PID="$primary_pid"
        VISUAL_GUARD_ACTIVE_CHILD_PGID="$primary_pid"
        VISUAL_GUARD_OWNED_CHILD_PID="$primary_pid"
        VISUAL_GUARD_OWNED_CHILD_PGID="$primary_pid"
    fi
    if [ "$signal_timing" = "before" ]; then
        if [ "$signal_name" = "INT" ]; then
            visual_guard_record_signal INT 130
        else
            visual_guard_record_signal TERM 143
        fi
    fi

    observe_capture_assets_before_restore \
        "$scenario_root/baseline" \
        "$scenario_root/mutation.log"
    visual_guard_mark_observation_complete "PASS"
    if visual_guard_finish "$original_status"; then
        scenario_status=0
    else
        scenario_status=$?
    fi

    assert_equal "$expected_status" "$scenario_status" "$name final status"
    assert_equal "baseline" "$(cat "$scenario_root/project/guarded.asset")" "$name restore"
    assert_equal "1" "$VISUAL_GUARD_FALLBACK_SCAN_PERFORMED" "$name fallback performed"
    assert_equal "$expected_survivors" "$VISUAL_GUARD_FINAL_SURVIVOR_COUNT" "$name survivors"
    assert_equal "1" "$VISUAL_GUARD_CLEANUP_EFFECTIVE_COUNT" "$name cleanup count"
    if [ "$primary_mode" = "live" ]; then
        if kill -0 "$primary_pid" 2>/dev/null; then
            echo "ERROR: $name primary remains: $primary_pid"
            exit 1
        fi
        assert_equal "1" "$VISUAL_GUARD_PRIMARY_WAS_LIVE" "$name primary was live"
        assert_equal "TERMINATED" "$VISUAL_GUARD_PRIMARY_TERMINATION_RESULT" "$name primary result"
    fi
    case "$candidate_mode" in
        exact|late)
            assert_equal "wsl:201" "$(sort -u "$killed")" "$name detached termination"
            ;;
        race)
            assert_equal "" "$(cat "$killed")" "$name already-exited candidate no-op"
            ;;
        refuses)
            assert_equal "" "$(cat "$killed")" "$name refusing candidate survives"
            assert_equal "true" "$(sed -n 's/^fallback_timeout=//p' "$scenario_root/lifecycle.log")" "$name timeout"
            assert_equal "FAILED_RUNNER_OWNED_PROCESS_REMAINS" "$VISUAL_GUARD_CLEANUP_PROCESS_RESULT" "$name cleanup result"
            assert_equal "FAILED_RUNNER_OWNED_PROCESS_REMAINS" "$VISUAL_GUARD_LANE_VERDICT" "$name lane verdict"
            ;;
        similar|preexisting|no-projectPath)
            assert_equal "" "$(cat "$killed")" "$name protected candidate"
            ;;
    esac
    echo "detached cleanup $name: PASS"
)

run_detached_cleanup_scenario primary-live-exact live exact none "" 0 0 0
run_detached_cleanup_scenario primary-exited-exact gone exact none "" 0 0 0
run_detached_cleanup_scenario late-detached gone late none "" 0 0 0
run_detached_cleanup_scenario primary-live-similar live similar none "" 0 0 0
run_detached_cleanup_scenario primary-live-preexisting live preexisting none "" 0 0 0
run_detached_cleanup_scenario primary-live-no-projectPath live no-projectPath none "" 0 0 0
run_detached_cleanup_scenario inventory-exit-race gone race none "" 0 0 0
run_detached_cleanup_scenario refusing-detached gone refuses none "" 0 1 1
run_detached_cleanup_scenario int-before-cleanup live exact before INT 0 130 0
run_detached_cleanup_scenario term-before-cleanup live exact before TERM 0 143 0
run_detached_cleanup_scenario int-during-fallback gone exact during INT 0 130 0
run_detached_cleanup_scenario term-during-fallback gone exact during TERM 0 143 0
run_detached_cleanup_scenario original-failure-detached gone exact none "" 37 37 0
echo "detached process polling, protection, and status precedence: PASS"

run_timed_grace_scenario() (
    local name="$1"
    local spawn_times_csv="$2"
    local signal_at_ms="$3"
    local signal_name="$4"
    local original_status="$5"
    local refuses_termination="$6"
    local expected_status="$7"
    local expected_candidates="$8"
    local expected_killed="$9"
    local expected_survivors="${10}"
    local expected_hard_timeout="${11:-0}"
    local termination_delay_ms="${12:-0}"
    local scenario_root="$TEST_ROOT/timed-$name"
    local inventory_killed="$scenario_root/killed.log"
    local exact_project="$scenario_root/project"
    local signal_sent=0
    local scenario_status=0
    local elapsed
    local spawn_at
    local pid
    local index
    local actual_killed
    local -a spawn_times=()

    TEST_MONOTONIC_MS=0
    IFS=',' read -r -a spawn_times <<< "$spawn_times_csv"
    mkdir -p "$scenario_root/project" "$scenario_root/baseline"
    printf 'baseline\n' > "$scenario_root/project/guarded.asset"
    cp "$scenario_root/project/guarded.asset" "$scenario_root/baseline/guarded.asset"
    : > "$inventory_killed"

    PROJECT_PATH_WSL="$exact_project"
    PROJECT_PATH_WIN="$exact_project"
    UNITY_DETACHED_STARTUP_GRACE_MS=6000
    UNITY_DETACHED_POLL_INTERVAL_MS=200
    UNITY_DETACHED_QUIET_PERIOD_MS=400
    UNITY_DETACHED_HARD_TIMEOUT_MS=12000
    capture_guarded_paths() {
        printf '%s\n' "guarded.asset"
    }
    visual_guard_iter_unity_process_records() {
        elapsed="$TEST_MONOTONIC_MS"
        for index in "${!spawn_times[@]}"; do
            spawn_at="${spawn_times[$index]}"
            if [ -z "$spawn_at" ] || [ "$elapsed" -lt "$spawn_at" ]; then
                continue
            fi
            pid=$((700 + index))
            if grep -Fx -- "wsl:$pid" "$inventory_killed" >/dev/null; then
                continue
            fi
            write_process_record wsl "$pid" "$exact_project"
        done
    }
    visual_guard_terminate_candidate() {
        if [ "$refuses_termination" -eq 1 ]; then
            return 1
        fi
        printf '%s:%s\n' "$1" "$2" >> "$inventory_killed"
        TEST_MONOTONIC_MS=$((TEST_MONOTONIC_MS + termination_delay_ms))
    }
    visual_guard_fallback_scan_test_hook() {
        local phase="$2"

        if [ "$phase" != "before_scan" ] ||
           [ -z "$signal_at_ms" ] ||
           [ "$signal_sent" -eq 1 ] ||
           [ "$TEST_MONOTONIC_MS" -lt "$signal_at_ms" ]; then
            return 0
        fi
        signal_sent=1
        if [ "$signal_name" = "INT" ]; then
            visual_guard_record_signal INT 130
        else
            visual_guard_record_signal TERM 143
        fi
    }

    visual_guard_begin \
        "$scenario_root/baseline" \
        "$scenario_root/mutation.log" \
        "$scenario_root/lifecycle.log" \
        "TimedGrace"
    observe_capture_assets_before_restore \
        "$scenario_root/baseline" \
        "$scenario_root/mutation.log"
    visual_guard_mark_observation_complete "PASS"
    if visual_guard_finish "$original_status"; then
        scenario_status=0
    else
        scenario_status=$?
    fi

    actual_killed="$(
        awk 'NF' "$inventory_killed" |
            sort -u |
            wc -l |
            tr -d '[:space:]'
    )"
    assert_equal "$expected_status" "$scenario_status" "$name final status"
    assert_equal "$expected_candidates" "$VISUAL_GUARD_FALLBACK_CANDIDATE_COUNT" "$name candidates"
    assert_equal "$expected_killed" "$actual_killed" "$name killed"
    assert_equal "$expected_survivors" "$VISUAL_GUARD_FINAL_SURVIVOR_COUNT" "$name survivors"
    assert_equal "1" "$VISUAL_GUARD_STARTUP_GRACE_COMPLETED" "$name startup grace completed"
    assert_equal "1" "$VISUAL_GUARD_CLEANUP_EFFECTIVE_COUNT" "$name cleanup count"
    assert_equal "baseline" "$(cat "$scenario_root/project/guarded.asset")" "$name restore"
    if [ "$refuses_termination" -eq 1 ]; then
        assert_equal "1" "$VISUAL_GUARD_HARD_TIMEOUT_REACHED" "$name hard timeout"
        assert_equal "ELIGIBLE_SURVIVOR_AFTER_FINAL_INVENTORY" "$VISUAL_GUARD_FALLBACK_FAILURE_REASON" "$name survivor reason"
    elif [ "$expected_hard_timeout" -eq 1 ]; then
        assert_equal "1" "$VISUAL_GUARD_HARD_TIMEOUT_REACHED" "$name hard timeout"
        assert_equal "0" "$VISUAL_GUARD_QUIET_PERIOD_COMPLETED" "$name quiet incomplete"
        assert_equal "POST_GRACE_QUIET_PERIOD_NOT_COMPLETED" "$VISUAL_GUARD_FALLBACK_FAILURE_REASON" "$name quiet timeout reason"
    else
        assert_equal "1" "$VISUAL_GUARD_QUIET_PERIOD_COMPLETED" "$name quiet completed"
        assert_equal "0" "$VISUAL_GUARD_HARD_TIMEOUT_REACHED" "$name no hard timeout"
    fi
    echo "timed grace $name: PASS"
)

run_timed_grace_scenario no-candidate "" "" "" 0 0 0 0 0 0
run_timed_grace_scenario old-early-exit-800ms "800" "" "" 0 0 0 1 1 0
run_timed_grace_scenario mid-grace-3000ms "3000" "" "" 0 0 0 1 1 0
run_timed_grace_scenario grace-minus-poll "5800" "" "" 0 0 0 1 1 0
run_timed_grace_scenario grace-deadline-boundary "6000" "" "" 0 0 0 1 1 0
run_timed_grace_scenario after-three-empty-scans "800" "" "" 0 0 0 1 1 0
run_timed_grace_scenario repeated-detached "800,3000,5800" "" "" 0 0 0 3 3 0
run_timed_grace_scenario post-grace-quiet-candidate "6200" "" "" 0 0 0 1 1 0
run_timed_grace_scenario refuses-termination "3000" "" "" 0 1 1 1 0 1
run_timed_grace_scenario quiet-timeout-no-survivor "6000" "" "" 0 0 1 1 1 0 1 6000
run_timed_grace_scenario int-early-grace "800" "200" INT 0 0 130 1 1 0
run_timed_grace_scenario term-mid-grace "3000" "2000" TERM 0 0 143 1 1 0
run_timed_grace_scenario int-near-grace-end "5800" "5600" INT 0 0 130 1 1 0
run_timed_grace_scenario original-failure-delayed "2000" "" "" 37 0 37 1 1 0
run_timed_grace_scenario refuses-with-int "3000" "2000" INT 0 1 130 1 0 1
echo "time-based startup grace, quiet reset, hard timeout, and signal matrix: PASS"

echo "visual runner interruption cleanup checks passed"
