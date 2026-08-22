#!/bin/bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUN_TESTS_LIBRARY_ONLY=1 source "$REPO_ROOT/run_tests.sh"

TEST_ROOT="$(mktemp -d)"
trap 'rm -rf -- "$TEST_ROOT"' EXIT

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

assert_contains() {
    local expected="$1"
    local path="$2"
    local label="$3"

    if ! grep -F -- "$expected" "$path" >/dev/null; then
        echo "ERROR: $label"
        echo "  missing: $expected"
        echo "  path:    $path"
        exit 1
    fi
}

prepare_scenario() {
    local name="$1"

    SCENARIO_ROOT="$TEST_ROOT/$name"
    PROJECT_PATH_WSL="$SCENARIO_ROOT/project"
    SCENARIO_ASSET_2000="$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET"
    SCENARIO_ASSET_2019="$PROJECT_PATH_WSL/$CLIMATE_2019_SDF_ASSET"
    SCENARIO_ASSET="$SCENARIO_ASSET_2000"
    SCENARIO_LOG="$SCENARIO_ROOT/$name.log"
    SCENARIO_EVIDENCE="$SCENARIO_ROOT/${name}-sdf-integrity.log"
    SCENARIO_2019_EVIDENCE="$SCENARIO_ROOT/${name}-climate-2019-sdf-integrity.log"
    mkdir -p "$(dirname "$SCENARIO_ASSET_2000")"
    cp --preserve=mode,timestamps -- \
        "$REPO_ROOT/$CLIMATE_SDF_ASSET" \
        "$SCENARIO_ASSET_2000"
    cp --preserve=mode,timestamps -- \
        "$REPO_ROOT/$CLIMATE_2019_SDF_ASSET" \
        "$SCENARIO_ASSET_2019"
    git -C "$PROJECT_PATH_WSL" init -q
    git -C "$PROJECT_PATH_WSL" add "$CLIMATE_SDF_ASSET" "$CLIMATE_2019_SDF_ASSET"
    git -C "$PROJECT_PATH_WSL" \
        -c user.name=runner-fixture \
        -c user.email=runner-fixture@example.invalid \
        commit -q -m baseline
}

mutate_to_expected_import_drift() {
    perl -pi -e '
        s/^([ \t]*(?:m_MipmapLimitGroupName|m_PlatformBlob|path|referencedFontAssetGUID|referencedTextAssetGUID|m_SourceFontFilePath|Name|m_LockedProperties):)\r?\n$/$1 \n/;
        s/^([ \t]*- _ScaleRatioA:) 1\r?\n$/$1 0.9\n/;
        s/^([ \t]*- _ScaleRatioC:) 1\r?\n$/$1 0.73125\n/;
    ' "$SCENARIO_ASSET"
}

no_mutation() {
    :
}

unexpected_mutation() {
    printf '# unexpected user-visible mutation\n' >> "$SCENARIO_ASSET"
}

mark_invoked() {
    touch "$SCENARIO_ROOT/invoked"
}

prepare_scenario no-mutation
run_with_climate_integrity_guard \
    no-mutation \
    "$SCENARIO_LOG" \
    no_mutation
assert_equal \
    "$CLIMATE_COMMITTED_SDF_SHA256" \
    "$(sha256sum "$SCENARIO_ASSET" | awk '{print $1}')" \
    "no-mutation final hash"
assert_contains "Classification=NO_MUTATION" "$SCENARIO_EVIDENCE" "no-mutation classification"
assert_contains "RestoreAttempted=NO" "$SCENARIO_EVIDENCE" "no-mutation restore policy"
assert_contains "Classification=NO_MUTATION" "$SCENARIO_2019_EVIDENCE" "Climate 2019 no-mutation classification"
assert_equal \
    "$CLIMATE_2019_COMMITTED_SDF_SHA256" \
    "$(sha256sum "$SCENARIO_ASSET_2019" | awk '{print $1}')" \
    "Climate 2019 no-mutation final hash"

prepare_scenario expected-drift
run_with_climate_integrity_guard \
    expected-drift \
    "$SCENARIO_LOG" \
    mutate_to_expected_import_drift
assert_equal \
    "$CLIMATE_COMMITTED_SDF_SHA256" \
    "$(sha256sum "$SCENARIO_ASSET" | awk '{print $1}')" \
    "expected-drift final hash"
assert_contains \
    "Imported=8c776e06dff6e330538814c3535e563a55add13fdf25b1c89e34e520547dc386" \
    "$SCENARIO_EVIDENCE" \
    "expected-drift imported hash"
assert_contains "Classification=EXPECTED_IMPORT_DERIVED_DRIFT" "$SCENARIO_EVIDENCE" "expected-drift classification"
assert_contains "RestoreSucceeded=YES" "$SCENARIO_EVIDENCE" "expected-drift restore"
assert_contains "FinalMutationDetected=0" "$SCENARIO_EVIDENCE" "expected-drift final state"

prepare_scenario expected-drift-2019
SCENARIO_ASSET="$SCENARIO_ASSET_2019"
run_with_climate_integrity_guard \
    expected-drift-2019 \
    "$SCENARIO_LOG" \
    mutate_to_expected_import_drift
assert_equal \
    "$CLIMATE_2019_COMMITTED_SDF_SHA256" \
    "$(sha256sum "$SCENARIO_ASSET_2019" | awk '{print $1}')" \
    "Climate 2019 expected-drift final hash"
assert_contains \
    "Classification=EXPECTED_IMPORT_DERIVED_DRIFT" \
    "$SCENARIO_2019_EVIDENCE" \
    "Climate 2019 expected-drift classification"
assert_contains "FinalMutationDetected=0" "$SCENARIO_2019_EVIDENCE" "Climate 2019 expected-drift final state"

prepare_scenario unexpected-mutation
if run_with_climate_integrity_guard \
    unexpected-mutation \
    "$SCENARIO_LOG" \
    unexpected_mutation; then
    echo "ERROR: Unexpected SDF mutation should fail."
    exit 1
fi
assert_contains "# unexpected user-visible mutation" "$SCENARIO_ASSET" "unexpected mutation preservation"
assert_contains "Classification=UNEXPECTED_SOURCE_MUTATION" "$SCENARIO_EVIDENCE" "unexpected classification"
assert_contains "RestoreAttempted=NO" "$SCENARIO_EVIDENCE" "unexpected restore prohibition"

prepare_scenario pre-existing-modification
printf '# pre-existing user modification\n' >> "$SCENARIO_ASSET"
pre_existing_hash="$(sha256sum "$SCENARIO_ASSET" | awk '{print $1}')"
if run_with_climate_integrity_guard \
    pre-existing-modification \
    "$SCENARIO_LOG" \
    mark_invoked; then
    echo "ERROR: Pre-existing SDF mutation should fail before invocation."
    exit 1
fi
assert_equal \
    "$pre_existing_hash" \
    "$(sha256sum "$SCENARIO_ASSET" | awk '{print $1}')" \
    "pre-existing modification preservation"
if [ -e "$SCENARIO_ROOT/invoked" ]; then
    echo "ERROR: Pre-existing modification did not stop the guarded invocation."
    exit 1
fi
assert_contains "Classification=PRE_EXISTING_SOURCE_MODIFICATION" "$SCENARIO_EVIDENCE" "pre-existing classification"
assert_contains "Imported=NOT_RUN" "$SCENARIO_EVIDENCE" "pre-existing invocation protection"

prepare_scenario pre-existing-modification-2019
printf '# pre-existing Climate 2019 user modification\n' >> "$SCENARIO_ASSET_2019"
pre_existing_2019_hash="$(sha256sum "$SCENARIO_ASSET_2019" | awk '{print $1}')"
if run_with_climate_integrity_guard \
    pre-existing-modification-2019 \
    "$SCENARIO_LOG" \
    mark_invoked; then
    echo "ERROR: Pre-existing Climate 2019 SDF mutation should fail before invocation."
    exit 1
fi
assert_equal \
    "$pre_existing_2019_hash" \
    "$(sha256sum "$SCENARIO_ASSET_2019" | awk '{print $1}')" \
    "Climate 2019 pre-existing modification preservation"
if [ -e "$SCENARIO_ROOT/invoked" ]; then
    echo "ERROR: Pre-existing Climate 2019 modification did not stop the guarded invocation."
    exit 1
fi
assert_contains \
    "Classification=PRE_EXISTING_SOURCE_MODIFICATION" \
    "$SCENARIO_2019_EVIDENCE" \
    "Climate 2019 pre-existing classification"
assert_contains "Imported=NOT_RUN" "$SCENARIO_2019_EVIDENCE" "Climate 2019 invocation protection"

prepare_scenario pre-existing-staged-modification
printf '# staged user modification\n' >> "$SCENARIO_ASSET_2000"
git -C "$PROJECT_PATH_WSL" add "$CLIMATE_SDF_ASSET"
if run_with_climate_integrity_guard \
    pre-existing-staged-modification \
    "$SCENARIO_LOG" \
    mark_invoked; then
    echo "ERROR: Pre-existing staged SDF mutation should fail before invocation."
    exit 1
fi
assert_contains "Classification=PRE_EXISTING_SOURCE_MODIFICATION" "$SCENARIO_EVIDENCE" "staged modification classification"
assert_contains "Imported=NOT_RUN" "$SCENARIO_EVIDENCE" "staged modification invocation protection"

prepare_scenario restore-failure
if (
    restore_climate_integrity_snapshot() {
        return 73
    }
    run_with_climate_integrity_guard \
        restore-failure \
        "$SCENARIO_LOG" \
        mutate_to_expected_import_drift
); then
    echo "ERROR: Restore verification failure should fail the guard."
    exit 1
fi
assert_equal \
    "8c776e06dff6e330538814c3535e563a55add13fdf25b1c89e34e520547dc386" \
    "$(sha256sum "$SCENARIO_ASSET" | awk '{print $1}')" \
    "restore-failure preserved imported state"
assert_contains "Classification=EXPECTED_IMPORT_DERIVED_DRIFT" "$SCENARIO_EVIDENCE" "restore-failure classification"
assert_contains "RestoreSucceeded=NO" "$SCENARIO_EVIDENCE" "restore-failure evidence"
assert_contains "FinalMutationDetected=1" "$SCENARIO_EVIDENCE" "restore-failure final state"

assert_contains \
    "run_with_climate_integrity_guard" \
    "$REPO_ROOT/run_tests.sh" \
    "common Unity wrapper ownership"

echo "Unity invocation SDF integrity guard fixtures passed"
