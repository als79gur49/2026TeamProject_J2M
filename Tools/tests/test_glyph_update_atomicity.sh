#!/bin/bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [ "${1:-}" = "--child" ]; then
    scenario="$2"
    scenario_root="$3"
    RUN_TESTS_LIBRARY_ONLY=1
    source "$ROOT_DIR/run_tests.sh"
    VISUAL_GUARD_TEST_HOOKS_ENABLED=1
    VISUAL_GUARD_TEST_RETURN_AFTER_FINALIZE=1
    PROJECT_PATH_WSL="$scenario_root/project"
    PROJECT_PATH_WIN="$PROJECT_PATH_WSL"
    TEST_MONOTONIC_MS=0
    visual_guard_monotonic_ms() {
        printf '%s\n' "$TEST_MONOTONIC_MS"
    }
    visual_guard_sleep_ms() {
        TEST_MONOTONIC_MS=$((TEST_MONOTONIC_MS + $1))
    }
    capture_guarded_paths() {
        printf '%s\n' "KBOMedium.asset" "KBOLight.asset"
    }
    visual_guard_iter_unity_process_records() {
        return 0
    }

    visual_guard_begin \
        "$scenario_root/baseline" \
        "$scenario_root/mutation.log" \
        "$scenario_root/lifecycle.log" \
        "GlyphAtomicityFixture"

    case "$scenario" in
        command-failure)
            command_status=0
            if visual_guard_run_command bash -c 'exit 37'; then
                command_status=0
            else
                command_status=$?
            fi
            visual_guard_mark_observation_complete "FAIL"
            visual_guard_finish "$command_status"
            ;;
        after-kbo-medium-failure)
            command_status=0
            if visual_guard_run_command bash -c \
                'printf "kbo-medium-mutated\n" > "$1"; exit 41' \
                _ "$PROJECT_PATH_WSL/KBOMedium.asset"; then
                command_status=0
            else
                command_status=$?
            fi
            visual_guard_mark_observation_complete "FAIL"
            visual_guard_finish "$command_status"
            ;;
        int|term)
            runner_pid="$BASHPID"
            signal_name="INT"
            if [ "$scenario" = "term" ]; then
                signal_name="TERM"
            fi
            (
                while [ "$(cat "$PROJECT_PATH_WSL/KBOMedium.asset")" != "kbo-medium-mutated" ]; do
                    sleep 0.02
                done
                kill "-$signal_name" "$runner_pid"
            ) &
            visual_guard_run_command bash -c \
                'printf "kbo-medium-mutated\n" > "$1"; printf "kbo-light-mutated\n" > "$2"; exec sleep 300' \
                _ \
                "$PROJECT_PATH_WSL/KBOMedium.asset" \
                "$PROJECT_PATH_WSL/KBOLight.asset"
            ;;
        success)
            visual_guard_run_command bash -c \
                'printf "kbo-medium-updated\n" > "$1"; printf "kbo-light-updated\n" > "$2"' \
                _ \
                "$PROJECT_PATH_WSL/KBOMedium.asset" \
                "$PROJECT_PATH_WSL/KBOLight.asset"
            visual_guard_cleanup_process_once
            VISUAL_GUARD_RESTORE_ON_SUCCESS=0
            visual_guard_mark_observation_complete "PASS"
            visual_guard_finish 0
            ;;
        *)
            echo "ERROR: Unknown glyph atomicity fixture scenario: $scenario"
            exit 2
            ;;
    esac
    exit $?
fi

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

TEST_ROOT="$(mktemp -d)"
trap 'rm -rf "$TEST_ROOT"' EXIT

run_scenario() {
    local scenario="$1"
    local expected_status="$2"
    local expected_kbo_medium="$3"
    local expected_kbo_light="$4"
    local scenario_root="$TEST_ROOT/$scenario"
    local status=0

    mkdir -p "$scenario_root/project" "$scenario_root/baseline"
    printf 'kbo-medium-baseline\n' > "$scenario_root/project/KBOMedium.asset"
    printf 'kbo-light-baseline\n' > "$scenario_root/project/KBOLight.asset"
    cp "$scenario_root/project/KBOMedium.asset" "$scenario_root/baseline/KBOMedium.asset"
    cp "$scenario_root/project/KBOLight.asset" "$scenario_root/baseline/KBOLight.asset"

    if bash "$0" --child "$scenario" "$scenario_root"; then
        status=0
    else
        status=$?
    fi

    assert_equal "$expected_status" "$status" "$scenario exit status"
    assert_equal "$expected_kbo_medium" "$(cat "$scenario_root/project/KBOMedium.asset")" "$scenario KBO Medium state"
    assert_equal "$expected_kbo_light" "$(cat "$scenario_root/project/KBOLight.asset")" "$scenario KBO Light state"
    assert_equal "1" "$(sed -n 's/^cleanup_effective_count=//p' "$scenario_root/lifecycle.log")" "$scenario cleanup one-shot"
    assert_equal "1" "$(sed -n 's/^process_cleanup_effective_count=//p' "$scenario_root/lifecycle.log")" "$scenario process cleanup one-shot"
    assert_equal "0" "$(sed -n 's/^final_survivor_count=//p' "$scenario_root/lifecycle.log")" "$scenario survivor count"
}

run_scenario command-failure 37 kbo-medium-baseline kbo-light-baseline
run_scenario after-kbo-medium-failure 41 kbo-medium-baseline kbo-light-baseline
run_scenario int 130 kbo-medium-baseline kbo-light-baseline
run_scenario term 143 kbo-medium-baseline kbo-light-baseline
run_scenario success 0 kbo-medium-updated kbo-light-updated

echo "glyph update atomicity fixtures passed"
