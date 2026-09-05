#!/bin/bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUN_TESTS_LIBRARY_ONLY=1 source "$REPO_ROOT/run_tests.sh"

mkdir -p /mnt/d/Repositories /mnt/d/J2M/evidence
TEST_ROOT="$(mktemp -d /mnt/d/Repositories/j2m-font-guard-fixtures.XXXXXX)"
EVIDENCE_ROOT="$(mktemp -d /mnt/d/J2M/evidence/font-guard-fixtures.XXXXXX)"
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
    SCENARIO_ASSET_MEDIUM="$PROJECT_PATH_WSL/$KBO_MEDIUM_SDF_ASSET"
    SCENARIO_ASSET_LIGHT="$PROJECT_PATH_WSL/$KBO_LIGHT_SDF_ASSET"
    SCENARIO_ASSET="$SCENARIO_ASSET_MEDIUM"
    SCENARIO_LOG="$EVIDENCE_ROOT/$name.log"
    SCENARIO_EVIDENCE="$EVIDENCE_ROOT/${name}-sdf-integrity.log"
    SCENARIO_LIGHT_EVIDENCE="$EVIDENCE_ROOT/${name}-kbo-light-sdf-integrity.log"
    mkdir -p "$(dirname "$SCENARIO_ASSET_MEDIUM")"
    cp --preserve=mode,timestamps -- \
        "$REPO_ROOT/$KBO_MEDIUM_SDF_ASSET" \
        "$SCENARIO_ASSET_MEDIUM"
    cp --preserve=mode,timestamps -- \
        "$REPO_ROOT/$KBO_LIGHT_SDF_ASSET" \
        "$SCENARIO_ASSET_LIGHT"
    git -C "$PROJECT_PATH_WSL" init -q
    git -C "$PROJECT_PATH_WSL" add "$KBO_MEDIUM_SDF_ASSET" "$KBO_LIGHT_SDF_ASSET"
    git -C "$PROJECT_PATH_WSL" \
        -c user.name=runner-fixture \
        -c user.email=runner-fixture@example.invalid \
        commit -q -m baseline
}

simulate_unity_serialization() {
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
run_with_kbo_font_integrity_guard \
    no-mutation \
    "$SCENARIO_LOG" \
    no_mutation
assert_equal \
    "$KBO_MEDIUM_COMMITTED_SDF_SHA256" \
    "$(sha256sum "$SCENARIO_ASSET" | awk '{print $1}')" \
    "no-mutation final hash"
assert_contains "Classification=NO_MUTATION" "$SCENARIO_EVIDENCE" "no-mutation classification"
assert_contains "RestoreAttempted=NO" "$SCENARIO_EVIDENCE" "no-mutation restore policy"
assert_contains "Classification=NO_MUTATION" "$SCENARIO_LIGHT_EVIDENCE" "KBO Light no-mutation classification"
assert_equal \
    "$KBO_LIGHT_COMMITTED_SDF_SHA256" \
    "$(sha256sum "$SCENARIO_ASSET_LIGHT" | awk '{print $1}')" \
    "KBO Light no-mutation final hash"

prepare_scenario serialization-stable
run_with_kbo_font_integrity_guard \
    serialization-stable \
    "$SCENARIO_LOG" \
    simulate_unity_serialization
assert_equal \
    "$KBO_MEDIUM_COMMITTED_SDF_SHA256" \
    "$(sha256sum "$SCENARIO_ASSET" | awk '{print $1}')" \
    "serialization-stable final hash"
assert_contains \
    "Imported=d8c3627e6092754441da7b34a59a70efc31b4ec2c77b4e8a941bdf7a8d06d2b6" \
    "$SCENARIO_EVIDENCE" \
    "serialization-stable imported hash"
assert_contains "Classification=NO_MUTATION" "$SCENARIO_EVIDENCE" "serialization-stable classification"
assert_contains "RestoreAttempted=NO" "$SCENARIO_EVIDENCE" "serialization-stable restore"
assert_contains "FinalMutationDetected=0" "$SCENARIO_EVIDENCE" "serialization-stable final state"

prepare_scenario serialization-stable-light
SCENARIO_ASSET="$SCENARIO_ASSET_LIGHT"
run_with_kbo_font_integrity_guard \
    serialization-stable-light \
    "$SCENARIO_LOG" \
    simulate_unity_serialization
assert_equal \
    "$KBO_LIGHT_COMMITTED_SDF_SHA256" \
    "$(sha256sum "$SCENARIO_ASSET_LIGHT" | awk '{print $1}')" \
    "KBO Light serialization-stable final hash"
assert_contains \
    "Classification=NO_MUTATION" \
    "$SCENARIO_LIGHT_EVIDENCE" \
    "KBO Light serialization-stable classification"
assert_contains "FinalMutationDetected=0" "$SCENARIO_LIGHT_EVIDENCE" "KBO Light serialization-stable final state"

prepare_scenario unexpected-mutation
if run_with_kbo_font_integrity_guard \
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
if run_with_kbo_font_integrity_guard \
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

prepare_scenario pre-existing-modification-light
printf '# pre-existing KBO Light user modification\n' >> "$SCENARIO_ASSET_LIGHT"
pre_existing_light_hash="$(sha256sum "$SCENARIO_ASSET_LIGHT" | awk '{print $1}')"
if run_with_kbo_font_integrity_guard \
    pre-existing-modification-light \
    "$SCENARIO_LOG" \
    mark_invoked; then
    echo "ERROR: Pre-existing KBO Light SDF mutation should fail before invocation."
    exit 1
fi
assert_equal \
    "$pre_existing_light_hash" \
    "$(sha256sum "$SCENARIO_ASSET_LIGHT" | awk '{print $1}')" \
    "KBO Light pre-existing modification preservation"
if [ -e "$SCENARIO_ROOT/invoked" ]; then
    echo "ERROR: Pre-existing KBO Light modification did not stop the guarded invocation."
    exit 1
fi
assert_contains \
    "Classification=PRE_EXISTING_SOURCE_MODIFICATION" \
    "$SCENARIO_LIGHT_EVIDENCE" \
    "KBO Light pre-existing classification"
assert_contains "Imported=NOT_RUN" "$SCENARIO_LIGHT_EVIDENCE" "KBO Light invocation protection"

prepare_scenario pre-existing-staged-modification
printf '# staged user modification\n' >> "$SCENARIO_ASSET_MEDIUM"
git -C "$PROJECT_PATH_WSL" add "$KBO_MEDIUM_SDF_ASSET"
if run_with_kbo_font_integrity_guard \
    pre-existing-staged-modification \
    "$SCENARIO_LOG" \
    mark_invoked; then
    echo "ERROR: Pre-existing staged SDF mutation should fail before invocation."
    exit 1
fi
assert_contains "Classification=PRE_EXISTING_SOURCE_MODIFICATION" "$SCENARIO_EVIDENCE" "staged modification classification"
assert_contains "Imported=NOT_RUN" "$SCENARIO_EVIDENCE" "staged modification invocation protection"

# Canonical ratios are derived already; any later ratio edit is unexpected.
prepare_scenario ratio-mutation
change_ratio() {
    sed -i 's/_ScaleRatioA: 0.9/_ScaleRatioA: 0.8/' "$SCENARIO_ASSET"
}
if run_with_kbo_font_integrity_guard ratio-mutation "$SCENARIO_LOG" change_ratio; then
    echo "ERROR: Noncanonical ratio mutation should fail."
    exit 1
fi
assert_contains "_ScaleRatioA: 0.8" "$SCENARIO_ASSET" "ratio mutation preservation"
assert_contains "Classification=UNEXPECTED_SOURCE_MUTATION" "$SCENARIO_EVIDENCE" "ratio mutation classification"
assert_contains "RestoreAttempted=NO" "$SCENARIO_EVIDENCE" "ratio mutation preservation policy"

prepare_scenario command-failure
fail_command() { return 73; }
command_status=0
run_with_kbo_font_integrity_guard command-failure "$SCENARIO_LOG" fail_command || command_status=$?
assert_equal 73 "$command_status" "Unity command failure propagation"
assert_contains "Classification=NO_MUTATION" "$SCENARIO_EVIDENCE" "failed command source preservation"

# Visual capture uses the same strict byte comparison.
assert_equal "0" "$(verify_kbo_font_working_transition "$SCENARIO_ASSET" "$SCENARIO_ASSET" >/dev/null; echo $?)" "identical transition"
printf '# unexpected serialization\n' > "$SCENARIO_ROOT/changed.asset"
if verify_kbo_font_working_transition "$SCENARIO_ASSET" "$SCENARIO_ROOT/changed.asset"; then
    echo "ERROR: Changed capture asset should fail transition validation."
    exit 1
fi

assert_contains \
    "run_with_kbo_font_integrity_guard" \
    "$REPO_ROOT/run_tests.sh" \
    "common Unity wrapper ownership"

echo "Unity invocation SDF integrity guard fixtures passed"
