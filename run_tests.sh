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

VISUAL_GUARD_BASELINE_ROOT=""
VISUAL_GUARD_MUTATION_EVIDENCE=""
VISUAL_GUARD_LIFECYCLE_EVIDENCE=""
VISUAL_GUARD_LANE=""
VISUAL_GUARD_BASELINE_READY=0
VISUAL_GUARD_TRAP_INSTALLED=0
VISUAL_GUARD_INTERRUPTED=0
VISUAL_GUARD_TERMINATION_SIGNAL=""
VISUAL_GUARD_SIGNAL_STATUS=0
VISUAL_GUARD_OBSERVATION_COMPLETED=0
VISUAL_GUARD_LANE_VERDICT="NOT_STARTED"
VISUAL_GUARD_CLEANUP_STARTED=0
VISUAL_GUARD_CLEANUP_COMPLETED=0
VISUAL_GUARD_CLEANUP_STATUS=0
VISUAL_GUARD_RESTORE_RESULT="NOT_STARTED"
VISUAL_GUARD_ACTIVE_CHILD_PID=""
VISUAL_GUARD_EXITING=0

TYPOGRAPHY_VISUAL_OUTPUT_ROOT="$PROJECT_PATH_WSL/TestLogs/TypographyVisualQA"
TYPOGRAPHY_VISUAL_OUTPUT_DIR=""
TYPOGRAPHY_VISUAL_UNITY_LOG=""
TYPOGRAPHY_VISUAL_MANIFEST=""
TYPOGRAPHY_VISUAL_WIDTH=1920
TYPOGRAPHY_VISUAL_HEIGHT=1080
TYPOGRAPHY_VISUAL_EXECUTE_METHOD="Game.Feature.UI.Composition.Editor.TypographyPreviewScreenshotMenu.CaptureRequiredPreviewScreenshotSliceFromCommandLine"
TYPOGRAPHY_VISUAL_RECONSTRUCT_METHOD="Game.Feature.UI.Composition.Editor.TypographyPreviewScreenshotMenu.ReconstructCanonicalManifestFromCommandLine"
TYPOGRAPHY_VISUAL_NANUM_ASSET="Assets/_Shared/UI/Fonts/NanumGothic SDF.asset"
NANUM_SOURCE_TTF_ASSET="Assets/_Shared/UI/Fonts/NanumGothic.ttf"
NANUM_SOURCE_TTF_META="$NANUM_SOURCE_TTF_ASSET.meta"
NANUM_SDF_ASSET="Assets/_Shared/UI/Fonts/NanumGothic SDF.asset"
NANUM_SDF_META="$NANUM_SDF_ASSET.meta"
NANUM_SYNTHETIC_BOLD_ASSET="Assets/_Features/UI/UI_Composition/Authoring/Typography/NanumGothic SDF SyntheticBold.mat"
NANUM_SYNTHETIC_BOLD_META="$NANUM_SYNTHETIC_BOLD_ASSET.meta"
NANUM_SOURCE_TTF_GUID="9efe96b63470e314280dc43c0aa565db"
NANUM_SDF_GUID="4662feb1d501d1f479b757a82e304069"
NANUM_SYNTHETIC_BOLD_GUID="2a2e67f1c1d143dc9f2d4af986ba7f21"
OBJECTIVE_HUD_VISUAL_OUTPUT_ROOT="$PROJECT_PATH_WSL/TestLogs/ObjectiveHudVisualQA"
OBJECTIVE_HUD_VISUAL_WIDTH=1920
OBJECTIVE_HUD_VISUAL_HEIGHT=1080
OBJECTIVE_HUD_VISUAL_EXECUTE_METHOD="Game.Feature.UI.Tests.ObjectiveHudVisualEvidenceUtility.CaptureFromCommandLine"
OBJECTIVE_HUD_VISUAL_CLIMATE_ASSET="Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset"
CLIMATE_SOURCE_TTF_ASSET="Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000.ttf"
CLIMATE_SOURCE_TTF_META="$CLIMATE_SOURCE_TTF_ASSET.meta"
CLIMATE_SDF_ASSET="$OBJECTIVE_HUD_VISUAL_CLIMATE_ASSET"
CLIMATE_SDF_META="$CLIMATE_SDF_ASSET.meta"
CLIMATE_COMMITTED_SDF_SHA256="c22ee5c03ebbe4f55322cf75b80acb7891173a5580ea56ef7b2f72c50f8431d5"
CLIMATE_SOURCE_TTF_SHA256="aa0e58ef1dd54ae760c29bdd0ce28d6b710c2d5910e88efadf5e23416b01d0f1"
CLIMATE_SOURCE_TTF_GUID="5360535d0de75234ca21822297323672"
CLIMATE_SDF_GUID="40d61154fd6576b4d85c2d78460b16ad"
CLIMATE_MATERIAL_LOCAL_ID="1352911973252649374"
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

git_head_blob_sha256() {
    local path="$1"

    git show "HEAD:$path" | sha256sum | awk '{print $1}'
}

git_head_runner_constant() {
    local name="$1"
    local value

    value="$(
        git show HEAD:run_tests.sh |
            sed -n "s/^${name}=\"\\([^\"]*\\)\"$/\\1/p"
    )"
    if [ -z "$value" ]; then
        echo "ERROR: Git HEAD run_tests.sh constant is missing: $name" >&2
        return 1
    fi
    printf '%s\n' "$value"
}

require_git_head_blob_text() {
    local path="$1"
    local expected="$2"
    local label="$3"

    if ! git show "HEAD:$path" | grep -F -- "$expected" >/dev/null; then
        echo "ERROR: Git HEAD $label mismatch: $path"
        return 1
    fi
}

verify_climate_committed_source_integrity() {
    local committed_sdf_hash
    local committed_ttf_hash
    local head_climate_sdf_sha256
    local head_climate_ttf_sha256
    local head_climate_ttf_guid
    local head_climate_sdf_guid
    local head_climate_material_local_id
    local head_nanum_ttf_guid
    local head_nanum_sdf_guid
    local head_nanum_material_guid
    local retained_path
    local -a retained_nanum_paths=(
        "Assets/_Shared/UI/Fonts/NanumGothic.ttf"
        "Assets/_Shared/UI/Fonts/NanumGothic.ttf.meta"
        "Assets/_Shared/UI/Fonts/NanumGothic SDF.asset"
        "Assets/_Shared/UI/Fonts/NanumGothic SDF.asset.meta"
        "Assets/_Features/UI/UI_Composition/Authoring/Typography/NanumGothic SDF SyntheticBold.mat"
        "Assets/_Features/UI/UI_Composition/Authoring/Typography/NanumGothic SDF SyntheticBold.mat.meta"
    )

    head_climate_sdf_sha256="$(git_head_runner_constant CLIMATE_COMMITTED_SDF_SHA256)"
    head_climate_ttf_sha256="$(git_head_runner_constant CLIMATE_SOURCE_TTF_SHA256)"
    head_climate_ttf_guid="$(git_head_runner_constant CLIMATE_SOURCE_TTF_GUID)"
    head_climate_sdf_guid="$(git_head_runner_constant CLIMATE_SDF_GUID)"
    head_climate_material_local_id="$(
        git_head_runner_constant CLIMATE_MATERIAL_LOCAL_ID
    )"
    head_nanum_ttf_guid="$(git_head_runner_constant NANUM_SOURCE_TTF_GUID)"
    head_nanum_sdf_guid="$(git_head_runner_constant NANUM_SDF_GUID)"
    head_nanum_material_guid="$(
        git_head_runner_constant NANUM_SYNTHETIC_BOLD_GUID
    )"

    committed_sdf_hash="$(git_head_blob_sha256 "$CLIMATE_SDF_ASSET")"
    committed_ttf_hash="$(git_head_blob_sha256 "$CLIMATE_SOURCE_TTF_ASSET")"
    if [ "$committed_sdf_hash" != "$head_climate_sdf_sha256" ]; then
        echo "ERROR: Climate committed SDF Git blob mismatch."
        echo "  expected: $head_climate_sdf_sha256"
        echo "  actual:   $committed_sdf_hash"
        return 1
    fi
    if [ "$committed_ttf_hash" != "$head_climate_ttf_sha256" ]; then
        echo "ERROR: Climate committed source TTF Git blob mismatch."
        echo "  expected: $head_climate_ttf_sha256"
        echo "  actual:   $committed_ttf_hash"
        return 1
    fi

    require_git_head_blob_text \
        "$CLIMATE_SOURCE_TTF_META" \
        "guid: $head_climate_ttf_guid" \
        "source TTF GUID"
    require_git_head_blob_text \
        "$CLIMATE_SDF_META" \
        "guid: $head_climate_sdf_guid" \
        "SDF GUID"
    require_git_head_blob_text \
        "$CLIMATE_SDF_ASSET" \
        "--- !u!21 &$head_climate_material_local_id" \
        "material localID"

    for retained_path in "${retained_nanum_paths[@]}"; do
        if ! git cat-file -e "HEAD:$retained_path"; then
            echo "ERROR: Required retained Nanum asset is absent from Git HEAD: $retained_path"
            return 1
        fi
    done
    require_git_head_blob_text \
        "$NANUM_SOURCE_TTF_META" \
        "guid: $head_nanum_ttf_guid" \
        "Nanum source TTF GUID"
    require_git_head_blob_text \
        "$NANUM_SDF_META" \
        "guid: $head_nanum_sdf_guid" \
        "Nanum SDF GUID"
    require_git_head_blob_text \
        "$NANUM_SYNTHETIC_BOLD_META" \
        "guid: $head_nanum_material_guid" \
        "Nanum synthetic-bold material GUID"
    require_git_head_blob_text \
        "$NANUM_SDF_ASSET" \
        "m_SourceFontFileGUID: $head_nanum_ttf_guid" \
        "Nanum SDF source TTF reference"
    require_git_head_blob_text \
        "$NANUM_SYNTHETIC_BOLD_ASSET" \
        "guid: $head_nanum_sdf_guid" \
        "Nanum synthetic-bold atlas reference"

    echo "Climate committed source integrity: PASS (historical HEAD audit)"
    echo "  SDF Git blob SHA-256: $committed_sdf_hash"
    echo "  TTF GUID:             $head_climate_ttf_guid"
    echo "  SDF GUID:             $head_climate_sdf_guid"
    echo "  Material localID:     $head_climate_material_local_id"
    echo "  Nanum body/meta:      6/6 committed"
    echo "  Nanum TTF GUID:       $head_nanum_ttf_guid"
    echo "  Nanum SDF GUID:       $head_nanum_sdf_guid"
    echo "  Nanum material GUID:  $head_nanum_material_guid"
}

require_worktree_file_text() {
    local path="$1"
    local expected="$2"
    local label="$3"

    if ! grep -F -- "$expected" "$PROJECT_PATH_WSL/$path" >/dev/null; then
        echo "ERROR: Candidate worktree $label mismatch: $path"
        return 1
    fi
}

verify_climate_worktree_source_integrity() {
    local candidate_sdf_hash
    local candidate_ttf_hash
    local retained_path
    local -a retained_nanum_paths=(
        "$NANUM_SOURCE_TTF_ASSET"
        "$NANUM_SOURCE_TTF_META"
        "$NANUM_SDF_ASSET"
        "$NANUM_SDF_META"
        "$NANUM_SYNTHETIC_BOLD_ASSET"
        "$NANUM_SYNTHETIC_BOLD_META"
    )

    candidate_sdf_hash="$(
        sha256sum "$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET" | awk '{print $1}'
    )"
    candidate_ttf_hash="$(
        sha256sum "$PROJECT_PATH_WSL/$CLIMATE_SOURCE_TTF_ASSET" | awk '{print $1}'
    )"
    if [ "$candidate_sdf_hash" != "$CLIMATE_COMMITTED_SDF_SHA256" ]; then
        echo "ERROR: Candidate worktree Climate SDF mismatch."
        echo "  expected: $CLIMATE_COMMITTED_SDF_SHA256"
        echo "  actual:   $candidate_sdf_hash"
        return 1
    fi
    if [ "$candidate_ttf_hash" != "$CLIMATE_SOURCE_TTF_SHA256" ]; then
        echo "ERROR: Candidate worktree Climate source TTF mismatch."
        echo "  expected: $CLIMATE_SOURCE_TTF_SHA256"
        echo "  actual:   $candidate_ttf_hash"
        return 1
    fi

    require_worktree_file_text \
        "$CLIMATE_SOURCE_TTF_META" \
        "guid: $CLIMATE_SOURCE_TTF_GUID" \
        "source TTF GUID"
    require_worktree_file_text \
        "$CLIMATE_SDF_META" \
        "guid: $CLIMATE_SDF_GUID" \
        "SDF GUID"
    require_worktree_file_text \
        "$CLIMATE_SDF_ASSET" \
        "--- !u!21 &$CLIMATE_MATERIAL_LOCAL_ID" \
        "material localID"

    for retained_path in "${retained_nanum_paths[@]}"; do
        if [ ! -f "$PROJECT_PATH_WSL/$retained_path" ]; then
            echo "ERROR: Required retained Nanum candidate is absent: $retained_path"
            return 1
        fi
    done
    require_worktree_file_text \
        "$NANUM_SOURCE_TTF_META" \
        "guid: $NANUM_SOURCE_TTF_GUID" \
        "Nanum source TTF GUID"
    require_worktree_file_text \
        "$NANUM_SDF_META" \
        "guid: $NANUM_SDF_GUID" \
        "Nanum SDF GUID"
    require_worktree_file_text \
        "$NANUM_SYNTHETIC_BOLD_META" \
        "guid: $NANUM_SYNTHETIC_BOLD_GUID" \
        "Nanum synthetic-bold material GUID"
    require_worktree_file_text \
        "$NANUM_SDF_ASSET" \
        "m_SourceFontFileGUID: $NANUM_SOURCE_TTF_GUID" \
        "Nanum SDF source TTF reference"
    require_worktree_file_text \
        "$NANUM_SYNTHETIC_BOLD_ASSET" \
        "guid: $NANUM_SDF_GUID" \
        "Nanum synthetic-bold atlas reference"

    echo "Climate/Nanum candidate worktree integrity: PASS"
    echo "  Climate SDF SHA-256: $candidate_sdf_hash"
    echo "  Nanum body/meta:     6/6 present"
}

climate_working_sha256() {
    sha256sum "$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET" | awk '{print $1}'
}

diagnose_climate_file_state() {
    local label="$1"
    local candidate="$2"
    local hash

    hash="$(sha256sum "$candidate" | awk '{print $1}')"
    echo "Climate working-state diagnostic [$label]:"
    echo "  SHA-256: $hash"
    python3 - "$candidate" "$CLIMATE_SDF_ASSET" <<'PY'
import subprocess
import sys
from pathlib import Path

candidate_path = Path(sys.argv[1])
asset_path = sys.argv[2]
before = subprocess.run(
    ["git", "show", f"HEAD:{asset_path}"],
    check=True,
    stdout=subprocess.PIPE,
).stdout.decode("utf-8").replace("\r\n", "\n").split("\n")
after = candidate_path.read_text(encoding="utf-8").replace("\r\n", "\n").split("\n")
if before == after:
    print("  Classification: COMMITTED_SOURCE_SHAPE")
    raise SystemExit(0)
if len(before) != len(after):
    raise SystemExit("ERROR: Climate mutation changed serialized line count.")

allowed = {
    ("- _ScaleRatioA: 1", "- _ScaleRatioA: 0.9"): "_ScaleRatioA:1->0.9",
    ("- _ScaleRatioC: 1", "- _ScaleRatioC: 0.73125"): "_ScaleRatioC:1->0.73125",
}
required_whitespace_properties = {
    "m_MipmapLimitGroupName:",
    "m_PlatformBlob:",
    "path:",
    "referencedFontAssetGUID:",
    "referencedTextAssetGUID:",
    "m_SourceFontFilePath:",
    "Name:",
    "m_LockedProperties:",
}
changes = []
whitespace_changes = set()
for old, new in zip(before, after):
    if old == new:
        continue
    if (
        old.strip() == new.strip() and
        old.strip() in required_whitespace_properties and
        new == old + " "
    ):
        whitespace_changes.add(old.strip())
        changes.append(f"serialization-whitespace:{old.strip()}")
        continue
    key = (old.strip(), new.strip())
    if key not in allowed:
        print(f"  Observed properties: {','.join(changes)}", file=sys.stderr)
        raise SystemExit(
            "ERROR: Climate mutation is outside the exact importer-derived property allowlist: "
            f"{key[0]} -> {key[1]}"
        )
    changes.append(allowed[key])
if (
    whitespace_changes != required_whitespace_properties or
    (
        ("_ScaleRatioA:1->0.9" in changes) !=
        ("_ScaleRatioC:1->0.73125" in changes)
    )
):
    missing_whitespace = sorted(required_whitespace_properties - whitespace_changes)
    print(f"  Observed properties: {','.join(changes)}", file=sys.stderr)
    print(f"  Missing whitespace properties: {','.join(missing_whitespace)}", file=sys.stderr)
    raise SystemExit(
        "ERROR: Climate mutation did not match the complete exact property allowlist."
    )
print("  Classification: EXPECTED_IMPORT_DERIVED_DRIFT")
print(f"  Derived properties: {','.join(changes)}")
PY
}

verify_climate_working_transition() {
    local before_snapshot="$1"
    local after_path="$2"
    local before_hash
    local after_hash
    local failed=0

    before_hash="$(sha256sum "$before_snapshot" | awk '{print $1}')"
    after_hash="$(sha256sum "$after_path" | awk '{print $1}')"
    diagnose_climate_file_state "pre-import" "$before_snapshot" || failed=1
    diagnose_climate_file_state "post-import" "$after_path" || failed=1
    if [ "$failed" -ne 0 ]; then
        echo "  Import transition: UNEXPECTED_ASSET_MUTATION"
        return 1
    fi
    if [ "$before_hash" = "$after_hash" ]; then
        echo "  Import transition: NO_DRIFT"
    else
        echo "  Import transition: EXACT_PROPERTY_CLASSIFIED_DRIFT"
    fi
}

ensure_result_dirs() {
    mkdir -p "$RESULT_DIR" "$METRICS_DIR"
}

count_generated_test_scenes() {
    find "$PROJECT_PATH_WSL/Assets" -maxdepth 1 -type f \( -name 'InitTestScene*.unity' -o -name 'InitTestScene*.unity.meta' \) | wc -l
}

cleanup_generated_test_scenes() {
    local residue_count

    residue_count="$(count_generated_test_scenes | tr -d '[:space:]')"
    if [ "$residue_count" -eq 0 ]; then
        return 0
    fi

    echo "Removing stale generated Unity test scenes: $residue_count"
    find "$PROJECT_PATH_WSL/Assets" -maxdepth 1 -type f \( -name 'InitTestScene*.unity' -o -name 'InitTestScene*.unity.meta' \) -delete
}

assert_no_generated_test_scenes() {
    local residue_count

    residue_count="$(count_generated_test_scenes | tr -d '[:space:]')"
    if [ "$residue_count" -eq 0 ]; then
        return 0
    fi

    echo "ERROR: Unity test run left generated InitTestScene artifacts in Assets/: $residue_count"
    find "$PROJECT_PATH_WSL/Assets" -maxdepth 1 -type f \( -name 'InitTestScene*.unity' -o -name 'InitTestScene*.unity.meta' \) -printf '  %P\n' | sort | head -n 40
    return 1
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
    echo "Usage: ./run_tests.sh [--print-config|--dry-run <lane>|core|core-feature-gate|ui|typography-visual|typography-hud-visual|full|--integration-simulation|--integration-replay|--integration-fuzz] [--filter <test-filter>|--test-filter <test-filter>]"
}

print_shell_command() {
    printf '  '
    printf '%q ' "$@"
    printf '\n'
}

prepare_typography_visual_paths() {
    local timestamp

    timestamp="$(date +%Y%m%d-%H%M%S)"
    TYPOGRAPHY_VISUAL_OUTPUT_DIR="$TYPOGRAPHY_VISUAL_OUTPUT_ROOT/CommandLine-$timestamp"
    TYPOGRAPHY_VISUAL_UNITY_LOG="$TYPOGRAPHY_VISUAL_OUTPUT_DIR/manifest-unity.log"
    TYPOGRAPHY_VISUAL_MANIFEST="$TYPOGRAPHY_VISUAL_OUTPUT_DIR/capture.log"
}

capture_guarded_paths() {
    printf '%s\n' \
        "$CLIMATE_SDF_ASSET" \
        "$NANUM_SDF_ASSET" \
        "Assets/TextMesh Pro/Resources/TMP Settings.asset" \
        "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset" \
        "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab" \
        "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab" \
        "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab" \
        "Assets/Synty/InterfaceSciFiSoldierHUD/Prefabs/_CommonComponents/Label_SciFiSoldier_SemiBold.prefab"
}

prepare_capture_asset_baseline() {
    local baseline_root="$1"
    local asset_path
    local -a guarded_paths

    mapfile -t guarded_paths < <(capture_guarded_paths)

    mkdir -p "$baseline_root"
    for asset_path in "${guarded_paths[@]}"; do
        mkdir -p "$baseline_root/$(dirname "$asset_path")"
        cp "$PROJECT_PATH_WSL/$asset_path" "$baseline_root/$asset_path"
    done
}

observe_capture_assets_before_restore() {
    local baseline_root="$1"
    local evidence_path="$2"
    local asset_path
    local before_hash
    local after_hash
    local transition_output
    local transition_exit
    local index
    local mutation_exit=0
    local -a guarded_paths
    local -a before_hashes
    local -a after_hashes
    local -a mutation_detected
    local -a changed_properties
    local -a classifications
    local -a allowed
    local -a lane_verdicts

    mapfile -t guarded_paths < <(capture_guarded_paths)
    for index in "${!guarded_paths[@]}"; do
        asset_path="${guarded_paths[$index]}"
        before_hash="$(
            sha256sum "$baseline_root/$asset_path" | awk '{print $1}'
        )"
        if [ -f "$PROJECT_PATH_WSL/$asset_path" ]; then
            after_hash="$(
                sha256sum "$PROJECT_PATH_WSL/$asset_path" | awk '{print $1}'
            )"
        else
            after_hash="MISSING"
        fi
        before_hashes[$index]="$before_hash"
        after_hashes[$index]="$after_hash"
        mutation_detected[$index]=0
        changed_properties[$index]=""
        classifications[$index]="NO_MUTATION"
        allowed[$index]=1
        lane_verdicts[$index]="PASS"
        if [ "$before_hash" = "$after_hash" ]; then
            continue
        fi

        mutation_detected[$index]=1
        if [ "$asset_path" = "$CLIMATE_SDF_ASSET" ]; then
            transition_exit=0
            transition_output="$(
                verify_climate_working_transition \
                    "$baseline_root/$asset_path" \
                    "$PROJECT_PATH_WSL/$asset_path" 2>&1
            )" || transition_exit=$?
            printf '%s\n' "$transition_output"
            if [ "$transition_exit" -eq 0 ]; then
                changed_properties[$index]="$(
                    printf '%s\n' "$transition_output" |
                        sed -n 's/^  Derived properties: //p'
                )"
                classifications[$index]="EXPECTED_IMPORT_DERIVED_DRIFT"
                continue
            fi
        fi

        changed_properties[$index]="UNCLASSIFIED_BYTE_DELTA"
        classifications[$index]="UNEXPECTED_ASSET_MUTATION"
        allowed[$index]=0
        lane_verdicts[$index]="FAIL"
        mutation_exit=1
    done

    {
        echo "schema_version=1"
        echo "observation_order=ALL_GUARDED_PATHS_BEFORE_ANY_RESTORE"
        echo "guarded_path_count=${#guarded_paths[@]}"
        echo "lane_verdict_before_restore=$(
            if [ "$mutation_exit" -eq 0 ]; then
                printf PASS
            else
                printf FAIL
            fi
        )"
        for index in "${!guarded_paths[@]}"; do
            echo
            echo "[asset-mutation/$index]"
            echo "path=${guarded_paths[$index]}"
            echo "before_hash=${before_hashes[$index]}"
            echo "after_capture_hash=${after_hashes[$index]}"
            echo "mutation_detected=${mutation_detected[$index]}"
            echo "changed_properties=${changed_properties[$index]}"
            echo "classification=${classifications[$index]}"
            echo "allowed=${allowed[$index]}"
            echo "lane_verdict_before_restore=${lane_verdicts[$index]}"
        done
    } > "$evidence_path"

    return "$mutation_exit"
}

restore_capture_assets_from_baseline() {
    local baseline_root="$1"
    local evidence_path="$2"
    local asset_path
    local before_hash
    local restored_hash
    local restored
    local restore_exit=0
    local -a guarded_paths

    mapfile -t guarded_paths < <(capture_guarded_paths)
    for asset_path in "${guarded_paths[@]}"; do
        cp "$baseline_root/$asset_path" "$PROJECT_PATH_WSL/$asset_path"
    done
    for asset_path in "${guarded_paths[@]}"; do
        before_hash="$(
            sha256sum "$baseline_root/$asset_path" | awk '{print $1}'
        )"
        restored_hash="$(
            sha256sum "$PROJECT_PATH_WSL/$asset_path" | awk '{print $1}'
        )"
        restored=1
        if [ "$restored_hash" != "$before_hash" ]; then
            restored=0
            restore_exit=1
        fi
        {
            echo
            echo "[asset-restore/$asset_path]"
            echo "restored=$restored"
            echo "restored_hash=$restored_hash"
            echo "expected_hash=$before_hash"
        } >> "$evidence_path"
    done

    if [ "$restore_exit" -ne 0 ]; then
        echo "ERROR: Runner capture baseline restore failed."
    fi
    return "$restore_exit"
}

visual_guard_reset_state() {
    VISUAL_GUARD_BASELINE_ROOT=""
    VISUAL_GUARD_MUTATION_EVIDENCE=""
    VISUAL_GUARD_LIFECYCLE_EVIDENCE=""
    VISUAL_GUARD_LANE=""
    VISUAL_GUARD_BASELINE_READY=0
    VISUAL_GUARD_TRAP_INSTALLED=0
    VISUAL_GUARD_INTERRUPTED=0
    VISUAL_GUARD_TERMINATION_SIGNAL=""
    VISUAL_GUARD_SIGNAL_STATUS=0
    VISUAL_GUARD_OBSERVATION_COMPLETED=0
    VISUAL_GUARD_LANE_VERDICT="NOT_STARTED"
    VISUAL_GUARD_CLEANUP_STARTED=0
    VISUAL_GUARD_CLEANUP_COMPLETED=0
    VISUAL_GUARD_CLEANUP_STATUS=0
    VISUAL_GUARD_RESTORE_RESULT="NOT_STARTED"
    VISUAL_GUARD_ACTIVE_CHILD_PID=""
    VISUAL_GUARD_EXITING=0
}

visual_guard_write_lifecycle_evidence() {
    local final_exit_status="$1"

    if [ -z "$VISUAL_GUARD_LIFECYCLE_EVIDENCE" ]; then
        return 0
    fi

    {
        echo "schema_version=1"
        echo "lane=$VISUAL_GUARD_LANE"
        echo "cleanup_trap_installed=$VISUAL_GUARD_TRAP_INSTALLED"
        echo "interrupted=$(
            if [ "$VISUAL_GUARD_INTERRUPTED" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "termination_signal=$VISUAL_GUARD_TERMINATION_SIGNAL"
        echo "signal_exit_status=$VISUAL_GUARD_SIGNAL_STATUS"
        echo "observation_order=ALL_GUARDED_PATHS_BEFORE_ANY_RESTORE"
        echo "mutation_observation_completed=$VISUAL_GUARD_OBSERVATION_COMPLETED"
        echo "lane_verdict=$VISUAL_GUARD_LANE_VERDICT"
        echo "cleanup_started=$VISUAL_GUARD_CLEANUP_STARTED"
        echo "cleanup_completed=$VISUAL_GUARD_CLEANUP_COMPLETED"
        echo "restore_result=$VISUAL_GUARD_RESTORE_RESULT"
        echo "final_exit_status=$final_exit_status"
    } > "$VISUAL_GUARD_LIFECYCLE_EVIDENCE"
}

visual_guard_prepare_interrupted_mutation_evidence() {
    if [ -z "$VISUAL_GUARD_MUTATION_EVIDENCE" ] ||
       [ -s "$VISUAL_GUARD_MUTATION_EVIDENCE" ]; then
        return 0
    fi

    {
        echo "schema_version=1"
        echo "observation_order=INTERRUPTED_BEFORE_COMPLETE_CLASSIFICATION"
        echo "lane_verdict_before_restore=INTERRUPTED"
        echo "termination_signal=$VISUAL_GUARD_TERMINATION_SIGNAL"
        echo "signal_exit_status=$VISUAL_GUARD_SIGNAL_STATUS"
    } > "$VISUAL_GUARD_MUTATION_EVIDENCE"
}

visual_guard_stop_active_child() {
    local child_pid="$VISUAL_GUARD_ACTIVE_CHILD_PID"
    local attempt

    if [ -n "$child_pid" ] && kill -0 "$child_pid" 2>/dev/null; then
        kill -TERM -- "-$child_pid" 2>/dev/null ||
            kill -TERM "$child_pid" 2>/dev/null ||
            true
        for attempt in $(seq 1 50); do
            if ! kill -0 "$child_pid" 2>/dev/null; then
                break
            fi
            sleep 0.1
        done
        if kill -0 "$child_pid" 2>/dev/null; then
            kill -KILL -- "-$child_pid" 2>/dev/null ||
                kill -KILL "$child_pid" 2>/dev/null ||
                true
        fi
    fi
    if [ -n "$child_pid" ]; then
        wait "$child_pid" 2>/dev/null || true
    fi
    VISUAL_GUARD_ACTIVE_CHILD_PID=""

    if declare -F terminate_current_project_unity_processes >/dev/null; then
        terminate_current_project_unity_processes
    fi
    if declare -F find_current_project_unity_processes >/dev/null &&
       [ -n "$(find_current_project_unity_processes)" ]; then
        echo "ERROR: Visual guard left a current-project Unity child running."
        return 1
    fi
}

visual_guard_cleanup() {
    local original_status="${1:-0}"
    local cleanup_status=0

    if [ "$VISUAL_GUARD_CLEANUP_STARTED" -eq 1 ]; then
        return "$VISUAL_GUARD_CLEANUP_STATUS"
    fi

    VISUAL_GUARD_CLEANUP_STARTED=1
    trap '' INT TERM

    if ! visual_guard_stop_active_child; then
        cleanup_status=1
    fi

    if [ "$VISUAL_GUARD_BASELINE_READY" -eq 1 ]; then
        visual_guard_prepare_interrupted_mutation_evidence
        if restore_capture_assets_from_baseline \
            "$VISUAL_GUARD_BASELINE_ROOT" \
            "$VISUAL_GUARD_MUTATION_EVIDENCE"; then
            VISUAL_GUARD_RESTORE_RESULT="PASS"
        else
            VISUAL_GUARD_RESTORE_RESULT="FAIL"
            cleanup_status=1
        fi
    else
        VISUAL_GUARD_RESTORE_RESULT="SKIPPED_BASELINE_NOT_READY"
    fi

    VISUAL_GUARD_CLEANUP_STATUS="$cleanup_status"
    VISUAL_GUARD_CLEANUP_COMPLETED=1
    visual_guard_write_lifecycle_evidence "$original_status"
    if [ "$VISUAL_GUARD_TRAP_INSTALLED" -eq 1 ] &&
       [ "$VISUAL_GUARD_EXITING" -eq 0 ]; then
        trap 'visual_guard_handle_signal INT 130' INT
        trap 'visual_guard_handle_signal TERM 143' TERM
    fi
    return "$cleanup_status"
}

visual_guard_handle_signal() {
    VISUAL_GUARD_INTERRUPTED=1
    VISUAL_GUARD_TERMINATION_SIGNAL="$1"
    VISUAL_GUARD_SIGNAL_STATUS="$2"
    VISUAL_GUARD_LANE_VERDICT="INTERRUPTED"
    exit "$2"
}

visual_guard_handle_exit() {
    local original_status="$1"
    local cleanup_status=0
    local final_status="$original_status"

    VISUAL_GUARD_EXITING=1
    trap - EXIT INT TERM
    visual_guard_cleanup "$original_status" || cleanup_status=$?
    if [ "$original_status" -eq 0 ] && [ "$cleanup_status" -ne 0 ]; then
        final_status="$cleanup_status"
    fi
    if [ "$cleanup_status" -ne 0 ]; then
        echo "ERROR: Visual guard cleanup failed (original status: $original_status, cleanup status: $cleanup_status)."
    fi
    visual_guard_write_lifecycle_evidence "$final_status"
    exit "$final_status"
}

visual_guard_begin() {
    local baseline_root="$1"
    local mutation_evidence="$2"
    local lifecycle_evidence="$3"
    local lane="$4"
    local asset_path
    local -a guarded_paths

    visual_guard_reset_state
    VISUAL_GUARD_BASELINE_ROOT="$baseline_root"
    VISUAL_GUARD_MUTATION_EVIDENCE="$mutation_evidence"
    VISUAL_GUARD_LIFECYCLE_EVIDENCE="$lifecycle_evidence"
    VISUAL_GUARD_LANE="$lane"

    mapfile -t guarded_paths < <(capture_guarded_paths)
    for asset_path in "${guarded_paths[@]}"; do
        if [ ! -f "$baseline_root/$asset_path" ]; then
            echo "ERROR: Visual guard baseline is incomplete: $asset_path"
            return 1
        fi
    done

    VISUAL_GUARD_BASELINE_READY=1
    trap 'visual_guard_handle_exit $?' EXIT
    trap 'visual_guard_handle_signal INT 130' INT
    trap 'visual_guard_handle_signal TERM 143' TERM
    VISUAL_GUARD_TRAP_INSTALLED=1
    visual_guard_write_lifecycle_evidence 0
}

visual_guard_run_command() {
    local command_status

    setsid "$@" &
    VISUAL_GUARD_ACTIVE_CHILD_PID=$!
    if wait "$VISUAL_GUARD_ACTIVE_CHILD_PID"; then
        command_status=0
    else
        command_status=$?
    fi
    VISUAL_GUARD_ACTIVE_CHILD_PID=""
    return "$command_status"
}

visual_guard_mark_observation_complete() {
    VISUAL_GUARD_OBSERVATION_COMPLETED=1
    VISUAL_GUARD_LANE_VERDICT="$1"
}

visual_guard_finish() {
    local original_status="$1"
    local cleanup_status=0
    local final_status="$original_status"

    visual_guard_cleanup "$original_status" || cleanup_status=$?
    if [ "$original_status" -eq 0 ] && [ "$cleanup_status" -ne 0 ]; then
        final_status="$cleanup_status"
    fi
    if [ "$cleanup_status" -ne 0 ]; then
        echo "ERROR: Visual guard cleanup failed (original status: $original_status, cleanup status: $cleanup_status)."
    fi

    trap - EXIT INT TERM
    visual_guard_write_lifecycle_evidence "$final_status"
    return "$final_status"
}

print_typography_visual_plan() {
    local output_dir_win
    local locale
    local target
    local slice
    local slice_name
    local slice_log
    local slice_log_win
    local baseline_root_win
    local -a unity_command
    local -a capture_slices=(
        "en-US|"
        "ko-KR|Settings"
        "ko-KR|Pause"
        "ko-KR|MainMenu"
    )

    output_dir_win="$(wslpath -w "$TYPOGRAPHY_VISUAL_OUTPUT_DIR")"
    baseline_root_win="$(wslpath -w "$TYPOGRAPHY_VISUAL_OUTPUT_DIR/pre-capture-assets")"

    echo "Typography visual evidence plan:"
    echo "  PROJECT_PATH_WSL: $PROJECT_PATH_WSL"
    echo "  PROJECT_PATH_WIN: $PROJECT_PATH_WIN"
    echo "  UNITY_PATH:       $UNITY_PATH"
    echo "  execute method:   $TYPOGRAPHY_VISUAL_EXECUTE_METHOD"
    echo "  output directory: $TYPOGRAPHY_VISUAL_OUTPUT_DIR"
    echo "  resolution:       ${TYPOGRAPHY_VISUAL_WIDTH}x${TYPOGRAPHY_VISUAL_HEIGHT}"
    echo "  raw Unity logs:   $TYPOGRAPHY_VISUAL_OUTPUT_DIR/capture-<slice>.log"
    echo "  manifest log:     $TYPOGRAPHY_VISUAL_UNITY_LOG"
    echo "  manifest:         $TYPOGRAPHY_VISUAL_MANIFEST"
    echo "  revision gate:    tracked repository files and Unity inputs must match Git HEAD"
    echo "Would run isolated Unity typography visual evidence slices:"
    for slice in "${capture_slices[@]}"; do
        locale="${slice%%|*}"
        target="${slice#*|}"
        slice_name="$locale"
        if [ -n "$target" ]; then
            slice_name="${locale}-${target}"
        fi
        slice_log="$TYPOGRAPHY_VISUAL_OUTPUT_DIR/capture-${slice_name}.log"
        slice_log_win="$(wslpath -w "$slice_log")"
        unity_command=(
            timeout --kill-after=10 600
            "$UNITY_PATH"
            -batchmode
            -quit
            -projectPath "$PROJECT_PATH_WIN"
            -logFile "$slice_log_win"
            -executeMethod "$TYPOGRAPHY_VISUAL_EXECUTE_METHOD"
            -typographyScreenshotOutput "$output_dir_win"
            -typographyScreenshotWidth "$TYPOGRAPHY_VISUAL_WIDTH"
            -typographyScreenshotHeight "$TYPOGRAPHY_VISUAL_HEIGHT"
            -typographyScreenshotLocale "$locale"
            -captureAssetBaselineRoot "$baseline_root_win"
        )
        if [ -n "$target" ]; then
            unity_command+=( -typographyScreenshotTarget "$target" )
        fi
        print_shell_command "${unity_command[@]}"
    done
    echo "Would reconstruct the canonical manifest:"
    unity_command=(
        timeout --kill-after=10 600
        "$UNITY_PATH"
        -batchmode
        -quit
        -projectPath "$PROJECT_PATH_WIN"
        -logFile "$(wslpath -w "$TYPOGRAPHY_VISUAL_UNITY_LOG")"
        -executeMethod "$TYPOGRAPHY_VISUAL_RECONSTRUCT_METHOD"
        -typographyScreenshotOutput "$output_dir_win"
    )
    print_shell_command "${unity_command[@]}"
}

verify_typography_visual_revision_gate() {
    local failed=0
    local -a untracked_unity_inputs

    if ! git diff --quiet; then
        echo "ERROR: Canonical typography evidence is blocked by unstaged tracked changes:"
        git status --short --untracked-files=no
        failed=1
    fi

    if ! git diff --cached --quiet; then
        echo "ERROR: Canonical typography evidence is blocked by staged tracked changes:"
        git diff --cached --name-only | sed 's/^/  /'
        failed=1
    fi

    mapfile -t untracked_unity_inputs < <(
        git ls-files --others --exclude-standard -- Assets Packages ProjectSettings
    )
    if [ "${#untracked_unity_inputs[@]}" -ne 0 ]; then
        echo "ERROR: Canonical typography evidence is blocked by untracked Unity inputs:"
        printf '  %s\n' "${untracked_unity_inputs[@]}"
        failed=1
    fi

    if [ "$failed" -ne 0 ]; then
        echo "The manifest records Git HEAD only, so dirty rendered inputs cannot produce canonical evidence."
        return 1
    fi

    echo "Canonical revision gate: PASS ($(git rev-parse HEAD))"
}

typography_visual_nanum_hash() {
    git hash-object "$PROJECT_PATH_WSL/$TYPOGRAPHY_VISUAL_NANUM_ASSET"
}

typography_visual_nanum_diff_sha256() {
    git diff -- "$TYPOGRAPHY_VISUAL_NANUM_ASSET" | sha256sum | awk '{print $1}'
}

verify_typography_visual_manifest() {
    local expected_head="$1"
    local expected_output_directory="${TYPOGRAPHY_VISUAL_OUTPUT_DIR#"$PROJECT_PATH_WSL/"}"

    python3 - \
        "$TYPOGRAPHY_VISUAL_OUTPUT_DIR" \
        "$TYPOGRAPHY_VISUAL_MANIFEST" \
        "$expected_head" \
        "$TYPOGRAPHY_VISUAL_RECONSTRUCT_METHOD" \
        "$TYPOGRAPHY_VISUAL_WIDTH" \
        "$TYPOGRAPHY_VISUAL_HEIGHT" \
        "$expected_output_directory" <<'PY'
import hashlib
import re
import sys
from pathlib import Path

output_dir = Path(sys.argv[1]).resolve()
manifest_path = Path(sys.argv[2]).resolve()
expected_head = sys.argv[3]
expected_command = sys.argv[4]
expected_width = sys.argv[5]
expected_height = sys.argv[6]
expected_output_directory = sys.argv[7]


def fail(message):
    raise SystemExit(f"ERROR: typography visual evidence verification failed: {message}")


if not manifest_path.is_file() or manifest_path.stat().st_size <= 0:
    fail(f"missing or empty manifest: {manifest_path}")

root = {}
entries = {}
current = root
for line_number, raw_line in enumerate(manifest_path.read_text(encoding="utf-8-sig").splitlines(), 1):
    line = raw_line.strip()
    if not line:
        continue
    if line.startswith("[") and line.endswith("]"):
        section = line[1:-1]
        if section in entries:
            fail(f"duplicate manifest section [{section}]")
        current = entries.setdefault(section, {})
        continue
    if "=" not in line:
        fail(f"malformed manifest line {line_number}: {raw_line}")
    key, value = line.split("=", 1)
    if key in current:
        fail(f"duplicate manifest field '{key}' on line {line_number}")
    current[key] = value

required_root = {
    "schema_version": "1",
    "git_head": expected_head,
    "capture_command": expected_command,
    "capture_mode": "RECONSTRUCTED_FROM_SPLIT_LOGS",
    "output_directory": expected_output_directory,
    "width": expected_width,
    "height": expected_height,
    "resolution": f"{expected_width}x{expected_height}",
    "overall_result": "PASS",
    "theme_validation": "PASS",
    "prefab_validation": "PASS",
    "guarded_asset_dirty_check": "PASS",
    "asset_mutation_observed_before_restore": "PASS",
    "unexpected_asset_mutation_count": "0",
}
for key, expected in required_root.items():
    actual = root.get(key)
    if actual != expected:
        fail(f"root {key} expected '{expected}', got '{actual}'")

expected_entries = {
    "Settings/en-US": ("Settings_en-US.png", "22", "38"),
    "Settings/ko-KR": ("Settings_ko-KR.png", "22", "38"),
    "Pause/en-US": ("Pause_en-US.png", "6", None),
    "Pause/ko-KR": ("Pause_ko-KR.png", "6", None),
    "MainMenu/en-US": ("MainMenu_en-US.png", "3", None),
    "MainMenu/ko-KR": ("MainMenu_ko-KR.png", "3", None),
}
if set(entries) != set(expected_entries):
    fail(
        "manifest sections differ from six-entry closure; "
        f"expected={sorted(expected_entries)}, actual={sorted(entries)}"
    )

expected_png_paths = set()
for section, (expected_file, localized_count, typography_count) in expected_entries.items():
    entry = entries[section]
    checks = {
        "file": expected_file,
        "width": expected_width,
        "height": expected_height,
        "dimensions": f"{expected_width}x{expected_height}",
        "localized_expected": localized_count,
        "localized_applied": localized_count,
        "capture_result": "PASS",
    }
    if typography_count is not None:
        checks["typography_bindings"] = typography_count
    for key, expected in checks.items():
        actual = entry.get(key)
        if actual != expected:
            fail(f"[{section}] {key} expected '{expected}', got '{actual}'")

    file_value = entry["file"]
    if Path(file_value).name != file_value:
        fail(f"[{section}] file must be a basename, got '{file_value}'")
    png_path = output_dir / file_value
    expected_png_paths.add(png_path.resolve())
    if not png_path.is_file() or png_path.stat().st_size <= 0:
        fail(f"[{section}] PNG missing or empty: {png_path}")

    try:
        recorded_size = int(entry.get("file_size_bytes", ""))
    except ValueError:
        fail(f"[{section}] invalid file_size_bytes '{entry.get('file_size_bytes')}'")
    actual_size = png_path.stat().st_size
    if recorded_size != actual_size:
        fail(f"[{section}] size mismatch: manifest={recorded_size}, actual={actual_size}")

    recorded_sha = entry.get("sha256", "")
    if not re.fullmatch(r"[0-9a-f]{64}", recorded_sha):
        fail(f"[{section}] invalid SHA-256 '{recorded_sha}'")
    actual_sha = hashlib.sha256(png_path.read_bytes()).hexdigest()
    if recorded_sha != actual_sha:
        fail(f"[{section}] SHA-256 mismatch: manifest={recorded_sha}, actual={actual_sha}")

actual_png_paths = {path.resolve() for path in output_dir.glob("*.png")}
if actual_png_paths != expected_png_paths:
    fail(
        "output PNG set differs from the required six files; "
        f"expected={sorted(path.name for path in expected_png_paths)}, "
        f"actual={sorted(path.name for path in actual_png_paths)}"
    )

print("Typography visual manifest verification: PASS")
print(f"  manifest: {manifest_path}")
print("  entries: 6")
print("  Settings en-US: typography_bindings=38 localized=22/22 capture_result=PASS")
print("  Settings ko-KR: typography_bindings=38 localized=22/22 capture_result=PASS")
print("  PNG size/SHA-256: verified for all six captures")
PY
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
    cleanup_generated_test_scenes
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
        if ! assert_no_generated_test_scenes; then
            cleanup_generated_test_scenes
            assert_no_generated_test_scenes || true
        fi
        return "$exit_code"
    fi

    if ! assert_no_generated_test_scenes; then
        cleanup_generated_test_scenes
        if ! assert_no_generated_test_scenes; then
            if [ "$exit_code" -ne 0 ]; then
                return "$exit_code"
            fi
            return 1
        fi
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
    local climate_before_snapshot
    local climate_before_hash
    local climate_restored_hash
    local unity_status=0

    climate_before_snapshot="$(mktemp)"
    cp "$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET" "$climate_before_snapshot"
    climate_before_hash="$(sha256sum "$climate_before_snapshot" | awk '{print $1}')"
    run_unity_stage \
        "ui" \
        "ui-editmode" \
        "ui (EditMode)" \
        "EditMode" \
        "$UNITY_UI_EDITMODE_LOG" \
        "$UNITY_UI_EDITMODE_XML" \
        "TestRunnerCliBootstrap.RunEditMode" \
        "Game.Feature.UI.Tests" \
        "" || unity_status=$?
    if [ "$unity_status" -eq 0 ]; then
        verify_climate_working_transition \
            "$climate_before_snapshot" \
            "$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET" || unity_status=$?
    fi
    cp "$climate_before_snapshot" "$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET"
    climate_restored_hash="$(
        sha256sum "$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET" | awk '{print $1}'
    )"
    if [ "$climate_restored_hash" != "$climate_before_hash" ]; then
        echo "ERROR: Climate working asset was not restored after UI validation."
        unity_status=1
    fi
    rm -f -- "$climate_before_snapshot"
    return "$unity_status"
}

run_typography_visual() {
    local output_dir_win
    local baseline_root
    local baseline_root_win
    local unity_log_win
    local slice_log
    local slice_log_win
    local locale
    local target
    local slice
    local slice_name
    local current_unity_log
    local expected_head
    local runner_mutation_evidence
    local runner_lifecycle_evidence
    local climate_hash_before
    local climate_hash_after
    local climate_restored_hash
    local nanum_hash_before
    local nanum_hash_after
    local nanum_diff_before
    local nanum_diff_after
    local unity_exit=0
    local residue_exit=0
    local nanum_exit=0
    local climate_exit=0
    local capture_guard_exit=0
    local restore_exit=0
    local process_before
    local -a unity_command
    local -a capture_slices=(
        "en-US|"
        "ko-KR|Settings"
        "ko-KR|Pause"
        "ko-KR|MainMenu"
    )

    prepare_typography_visual_paths
    if [ "$DRY_RUN" -eq 1 ]; then
        print_typography_visual_plan
        return 0
    fi

    verify_typography_visual_revision_gate
    ensure_no_current_project_unity_process
    ensure_no_current_project_unity_lock

    mkdir -p "$TYPOGRAPHY_VISUAL_OUTPUT_ROOT"
    if ! mkdir "$TYPOGRAPHY_VISUAL_OUTPUT_DIR"; then
        echo "ERROR: Typography visual output directory already exists; refusing to overwrite:"
        echo "  $TYPOGRAPHY_VISUAL_OUTPUT_DIR"
        return 1
    fi

    expected_head="$(git rev-parse HEAD)"
    nanum_hash_before="$(typography_visual_nanum_hash)"
    nanum_diff_before="$(typography_visual_nanum_diff_sha256)"
    output_dir_win="$(wslpath -w "$TYPOGRAPHY_VISUAL_OUTPUT_DIR")"
    baseline_root="$TYPOGRAPHY_VISUAL_OUTPUT_DIR/pre-capture-assets"
    baseline_root_win="$(wslpath -w "$baseline_root")"
    runner_mutation_evidence="$TYPOGRAPHY_VISUAL_OUTPUT_DIR/runner-asset-mutation.log"
    runner_lifecycle_evidence="$TYPOGRAPHY_VISUAL_OUTPUT_DIR/runner-cleanup-lifecycle.log"
    prepare_capture_asset_baseline "$baseline_root"
    visual_guard_begin \
        "$baseline_root" \
        "$runner_mutation_evidence" \
        "$runner_lifecycle_evidence" \
        "Typography"
    climate_hash_before="$(
        sha256sum "$baseline_root/$CLIMATE_SDF_ASSET" | awk '{print $1}'
    )"

    cleanup_generated_test_scenes
    process_before="$(find_current_project_unity_processes)"
    echo "Running isolated Unity typography visual evidence slices..."
    echo "  output directory: $TYPOGRAPHY_VISUAL_OUTPUT_DIR"
    echo "  raw Unity logs:   $TYPOGRAPHY_VISUAL_OUTPUT_DIR/capture-<slice>.log"
    echo "  manifest log:     $TYPOGRAPHY_VISUAL_UNITY_LOG"
    echo "  manifest:         $TYPOGRAPHY_VISUAL_MANIFEST"
    for slice in "${capture_slices[@]}"; do
        locale="${slice%%|*}"
        target="${slice#*|}"
        slice_name="$locale"
        if [ -n "$target" ]; then
            slice_name="${locale}-${target}"
        fi
        slice_log="$TYPOGRAPHY_VISUAL_OUTPUT_DIR/capture-${slice_name}.log"
        slice_log_win="$(wslpath -w "$slice_log")"
        current_unity_log="$slice_log"
        unity_command=(
            timeout --kill-after=10 600
            "$UNITY_PATH"
            -batchmode
            -quit
            -projectPath "$PROJECT_PATH_WIN"
            -logFile "$slice_log_win"
            -executeMethod "$TYPOGRAPHY_VISUAL_EXECUTE_METHOD"
            -typographyScreenshotOutput "$output_dir_win"
            -typographyScreenshotWidth "$TYPOGRAPHY_VISUAL_WIDTH"
            -typographyScreenshotHeight "$TYPOGRAPHY_VISUAL_HEIGHT"
            -typographyScreenshotLocale "$locale"
            -captureAssetBaselineRoot "$baseline_root_win"
        )
        if [ -n "$target" ]; then
            unity_command+=( -typographyScreenshotTarget "$target" )
        fi
        echo "  capture slice: $slice_name"
        if visual_guard_run_command "${unity_command[@]}"; then
            unity_exit=0
        else
            unity_exit=$?
            break
        fi
    done

    if [ "$unity_exit" -eq 0 ]; then
        unity_log_win="$(wslpath -w "$TYPOGRAPHY_VISUAL_UNITY_LOG")"
        current_unity_log="$TYPOGRAPHY_VISUAL_UNITY_LOG"
        unity_command=(
            timeout --kill-after=10 600
            "$UNITY_PATH"
            -batchmode
            -quit
            -projectPath "$PROJECT_PATH_WIN"
            -logFile "$unity_log_win"
            -executeMethod "$TYPOGRAPHY_VISUAL_RECONSTRUCT_METHOD"
            -typographyScreenshotOutput "$output_dir_win"
        )
        echo "Reconstructing canonical typography manifest..."
        if visual_guard_run_command "${unity_command[@]}"; then
            unity_exit=0
        else
            unity_exit=$?
        fi
    fi

    if [ "$unity_exit" -eq 124 ] || [ "$unity_exit" -eq 137 ]; then
        echo "Unity typography visual capture timed out (possible hang)."
        capture_unity_timeout_artifacts \
            "typography-visual" \
            "$current_unity_log" \
            "$TYPOGRAPHY_VISUAL_MANIFEST" \
            "$unity_exit" \
            "$process_before" || true
    fi

    nanum_hash_after="$(typography_visual_nanum_hash)"
    nanum_diff_after="$(typography_visual_nanum_diff_sha256)"
    climate_hash_after="$(climate_working_sha256)"
    if ! verify_climate_working_transition \
        "$baseline_root/$CLIMATE_SDF_ASSET" \
        "$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET"; then
        climate_exit=1
    fi
    if ! observe_capture_assets_before_restore \
        "$baseline_root" \
        "$runner_mutation_evidence"; then
        capture_guard_exit=1
    fi
    if [ "$capture_guard_exit" -eq 0 ]; then
        visual_guard_mark_observation_complete "PASS"
    else
        visual_guard_mark_observation_complete "FAIL"
    fi
    if ! visual_guard_cleanup "$unity_exit"; then
        restore_exit=1
    fi
    climate_restored_hash="$(climate_working_sha256)"
    if [ "$climate_restored_hash" != "$climate_hash_before" ]; then
        echo "ERROR: Climate asset was not restored after typography capture."
        climate_exit=1
    fi
    echo "Typography Climate capture transition:"
    echo "  before:   $climate_hash_before"
    echo "  observed: $climate_hash_after"
    echo "  restored: $climate_restored_hash"
    if [ "$nanum_hash_after" != "$nanum_hash_before" ]; then
        echo "ERROR: Nanum asset content hash changed during typography capture."
        echo "  before: $nanum_hash_before"
        echo "  after:  $nanum_hash_after"
        nanum_exit=1
    fi
    if [ "$nanum_diff_after" != "$nanum_diff_before" ]; then
        echo "ERROR: Nanum asset diff SHA-256 changed during typography capture."
        echo "  before: $nanum_diff_before"
        echo "  after:  $nanum_diff_after"
        nanum_exit=1
    fi

    if ! assert_no_generated_test_scenes; then
        cleanup_generated_test_scenes
        assert_no_generated_test_scenes || true
        residue_exit=1
    fi

    if [ "$unity_exit" -ne 0 ]; then
        echo "ERROR: Unity typography visual capture failed with exit code $unity_exit."
        echo "Diagnostics were preserved in: $TYPOGRAPHY_VISUAL_OUTPUT_DIR"
        visual_guard_finish "$unity_exit" || return $?
        return 0
    fi
    if [ "$nanum_exit" -ne 0 ] ||
       [ "$climate_exit" -ne 0 ] ||
       [ "$capture_guard_exit" -ne 0 ] ||
       [ "$restore_exit" -ne 0 ] ||
       [ "$residue_exit" -ne 0 ]; then
        echo "ERROR: Typography visual safety checks failed after Unity capture."
        echo "Diagnostics were preserved in: $TYPOGRAPHY_VISUAL_OUTPUT_DIR"
        visual_guard_finish 1 || return $?
        return 0
    fi

    if ! verify_typography_visual_manifest "$expected_head"; then
        echo "Diagnostics were preserved in: $TYPOGRAPHY_VISUAL_OUTPUT_DIR"
        visual_guard_finish 1 || return $?
        return 0
    fi
    echo "Typography visual evidence capture: PASS"
    echo "  output directory: $TYPOGRAPHY_VISUAL_OUTPUT_DIR"
    echo "  manifest:         $TYPOGRAPHY_VISUAL_MANIFEST"
    echo "  runner mutation:  $runner_mutation_evidence"
    echo "  recorded revision: $expected_head"
    echo "  Nanum hash/diff: preserved"
    visual_guard_finish 0
}

run_objective_hud_visual() {
    local timestamp
    local output_dir
    local output_dir_win
    local baseline_root
    local baseline_root_win
    local unity_log
    local unity_log_win
    local test_results
    local test_results_win
    local manifest
    local expected_head
    local runner_mutation_evidence
    local runner_lifecycle_evidence
    local climate_hash_before
    local climate_hash_after
    local climate_restored_hash
    local process_before
    local unity_exit=0
    local capture_guard_exit=0
    local restore_exit=0
    local -a unity_command

    timestamp="$(date +%Y%m%d-%H%M%S)"
    output_dir="$OBJECTIVE_HUD_VISUAL_OUTPUT_ROOT/CommandLine-$timestamp"
    unity_log="$output_dir/objective-hud-unity.log"
    test_results="$output_dir/objective-hud-playmode.xml"
    manifest="$output_dir/objective-hud-capture.log"
    runner_mutation_evidence="$output_dir/runner-asset-mutation.log"
    runner_lifecycle_evidence="$output_dir/runner-cleanup-lifecycle.log"
    output_dir_win="$(wslpath -w "$output_dir")"
    baseline_root="$output_dir/pre-capture-assets"
    baseline_root_win="$(wslpath -w "$baseline_root")"
    unity_log_win="$(wslpath -w "$unity_log")"
    test_results_win="$(wslpath -w "$test_results")"
    unity_command=(
        timeout --kill-after=10 600
        "$UNITY_PATH"
        -projectPath "$PROJECT_PATH_WIN"
        -logFile "$unity_log_win"
        -runTests
        -testPlatform PlayMode
        -testFilter "Game.Feature.Gameplay.Tests.PlayMode.ObjectiveHudVisualEvidencePlayModeTests.CaptureScreenSpaceOverlayEvidenceAfterSettledFrames"
        -testResults "$test_results_win"
        -objectiveHudVisualOutput "$output_dir_win"
        -objectiveHudVisualWidth "$OBJECTIVE_HUD_VISUAL_WIDTH"
        -objectiveHudVisualHeight "$OBJECTIVE_HUD_VISUAL_HEIGHT"
        -captureAssetBaselineRoot "$baseline_root_win"
    )

    if [ "$DRY_RUN" -eq 1 ]; then
        echo "Objective HUD visual evidence plan:"
        echo "  output directory: $output_dir"
        echo "  resolution: ${OBJECTIVE_HUD_VISUAL_WIDTH}x${OBJECTIVE_HUD_VISUAL_HEIGHT}"
        echo "  revision gate: tracked repository files and Unity inputs must match Git HEAD"
        print_shell_command "${unity_command[@]}"
        return 0
    fi

    verify_typography_visual_revision_gate
    ensure_no_current_project_unity_process
    ensure_no_current_project_unity_lock
    mkdir -p "$OBJECTIVE_HUD_VISUAL_OUTPUT_ROOT"
    mkdir "$output_dir"
    prepare_capture_asset_baseline "$baseline_root"
    visual_guard_begin \
        "$baseline_root" \
        "$runner_mutation_evidence" \
        "$runner_lifecycle_evidence" \
        "ObjectiveHud"

    expected_head="$(git rev-parse HEAD)"
    climate_hash_before="$(sha256sum "$OBJECTIVE_HUD_VISUAL_CLIMATE_ASSET" | awk '{print $1}')"
    process_before="$(find_current_project_unity_processes)"
    echo "Running Objective HUD production-composition visual evidence..."
    echo "  output directory: $output_dir"
    echo "  manifest: $manifest"
    if visual_guard_run_command "${unity_command[@]}"; then
        unity_exit=0
    else
        unity_exit=$?
    fi

    if [ "$unity_exit" -eq 124 ] || [ "$unity_exit" -eq 137 ]; then
        capture_unity_timeout_artifacts \
            "typography-hud-visual" \
            "$unity_log" \
            "$manifest" \
            "$unity_exit" \
            "$process_before" || true
    fi

    climate_hash_after="$(sha256sum "$OBJECTIVE_HUD_VISUAL_CLIMATE_ASSET" | awk '{print $1}')"
    if ! observe_capture_assets_before_restore \
        "$baseline_root" \
        "$runner_mutation_evidence"; then
        capture_guard_exit=1
    fi
    if [ "$capture_guard_exit" -eq 0 ]; then
        visual_guard_mark_observation_complete "PASS"
    else
        visual_guard_mark_observation_complete "FAIL"
    fi
    if ! visual_guard_cleanup "$unity_exit"; then
        restore_exit=1
    fi
    climate_restored_hash="$(
        sha256sum "$OBJECTIVE_HUD_VISUAL_CLIMATE_ASSET" | awk '{print $1}'
    )"
    echo "Objective HUD runner Climate transition:"
    echo "  before:   $climate_hash_before"
    echo "  observed: $climate_hash_after"
    echo "  restored: $climate_restored_hash"

    if [ "$unity_exit" -ne 0 ]; then
        echo "ERROR: Objective HUD visual capture failed with exit code $unity_exit."
        echo "Diagnostics were preserved in: $output_dir"
        visual_guard_finish "$unity_exit" || return $?
        return 0
    fi
    if [ "$capture_guard_exit" -ne 0 ] || [ "$restore_exit" -ne 0 ]; then
        echo "ERROR: Objective HUD runner asset safety checks failed."
        echo "  mutation evidence: $runner_mutation_evidence"
        visual_guard_finish 1 || return $?
        return 0
    fi
    if ! assert_no_generated_test_scenes; then
        cleanup_generated_test_scenes
        visual_guard_finish 1 || return $?
        return 0
    fi

    python3 - "$output_dir" "$manifest" "$expected_head" <<'PY'
import hashlib
import re
import struct
import sys
from pathlib import Path

output_dir = Path(sys.argv[1]).resolve()
manifest_path = Path(sys.argv[2]).resolve()
expected_head = sys.argv[3]

if not manifest_path.is_file():
    raise SystemExit(f"ERROR: Objective HUD manifest missing: {manifest_path}")

text = manifest_path.read_text(encoding="utf-8")
root = {}
sections = {}
current = root
for raw_line in text.splitlines():
    line = raw_line.strip()
    if not line or line.startswith("#"):
        continue
    if line.startswith("[") and line.endswith("]"):
        name = line[1:-1]
        if name in sections:
            raise SystemExit(f"ERROR: duplicate manifest section: {name}")
        current = sections[name] = {}
        continue
    if "=" not in line:
        raise SystemExit(f"ERROR: malformed manifest line: {line}")
    key, value = line.split("=", 1)
    current[key] = value

required_sections = {
    "Idle/en-US",
    "Idle/ko-KR",
    "MaxStack/en-US",
    "MaxStack/ko-KR",
}
capture_sections = {
    name: entry
    for name, entry in sections.items()
    if not name.startswith("asset-mutation/")
}
mutation_sections = {
    name: entry
    for name, entry in sections.items()
    if name.startswith("asset-mutation/")
}
if root.get("git_head") != expected_head:
    raise SystemExit("ERROR: Objective HUD manifest git_head mismatch")
if root.get("schema_version") != "2":
    raise SystemExit("ERROR: Objective HUD manifest schema_version mismatch")
resolution_match = re.fullmatch(r"([1-9][0-9]*)x([1-9][0-9]*)", root.get("resolution", ""))
if not resolution_match:
    raise SystemExit("ERROR: Objective HUD manifest resolution is invalid")
expected_width, expected_height = map(int, resolution_match.groups())
if root.get("overall_result") != "PASS" or root.get("errors") != "0":
    raise SystemExit("ERROR: Objective HUD manifest did not record a clean PASS")
if root.get("canonical_status") != "CANDIDATE_PENDING_INDEPENDENT_AUDIT":
    raise SystemExit("ERROR: Objective HUD canonical audit status mismatch")
if root.get("capture_mode") != "SCREEN_SPACE_OVERLAY_PRODUCTION_CONTROLLER_END_OF_FRAME":
    raise SystemExit("ERROR: Objective HUD capture mode is not the production overlay path")
if root.get("capture_environment") != "NON_BATCH_GAME_VIEW":
    raise SystemExit("ERROR: Objective HUD capture environment cannot render EndOfFrame")
if root.get("root_cause") != "FIXTURE_STATE_MISMATCH":
    raise SystemExit("ERROR: Objective HUD old evidence root-cause classification mismatch")
if root.get("graphic_completeness") != "PASS":
    raise SystemExit("ERROR: Objective HUD graphic completeness failed")
if root.get("cross_locale_non_text_parity") != "PASS":
    raise SystemExit("ERROR: Objective HUD cross-locale non-text parity failed")
if root.get("unexpected_pixel_delta") != "0":
    raise SystemExit("ERROR: Objective HUD unexpected non-text pixel delta is nonzero")
if root.get("asset_mutation_observed_before_restore") != "PASS":
    raise SystemExit("ERROR: capture mutation was not observed before restore")
if root.get("unexpected_asset_mutation_count") != "0":
    raise SystemExit("ERROR: capture recorded unexpected asset mutation")
if root.get("capture_count") != "4" or set(capture_sections) != required_sections:
    raise SystemExit("ERROR: Objective HUD manifest capture set mismatch")
if not mutation_sections:
    raise SystemExit("ERROR: Objective HUD manifest has no guarded asset evidence")

for name, entry in capture_sections.items():
    if entry.get("capture_result") != "PASS":
        raise SystemExit(f"ERROR: {name} capture_result is not PASS")
    if entry.get("decode") != "PASS" or entry.get("layout") != "PASS":
        raise SystemExit(f"ERROR: {name} decode/layout validation failed")
    if entry.get("glyph_coverage") != "PASS":
        raise SystemExit(f"ERROR: {name} glyph coverage failed")
    if entry.get("graphic_completeness") != "PASS":
        raise SystemExit(f"ERROR: {name} graphic completeness failed")
    for lifecycle in (
        "localization_ready",
        "presenter_render_complete",
        "canvas_rebuild_complete",
    ):
        if entry.get(lifecycle) != "PASS":
            raise SystemExit(f"ERROR: {name} lifecycle field {lifecycle} failed")
    if entry.get("objective_settle_strategy") != "PRODUCTION_UNSCALED_TIME":
        raise SystemExit(f"ERROR: {name} did not use production objective lifecycle")
    if entry.get("objective_settle_seconds") != "2.000":
        raise SystemExit(f"ERROR: {name} objective settle duration mismatch")
    try:
        end_of_frame_count = int(entry.get("end_of_frame_count", ""))
        capture_frame_index = int(entry.get("capture_frame_index", ""))
    except ValueError:
        raise SystemExit(f"ERROR: {name} frame lifecycle values are invalid")
    if end_of_frame_count < 1 or capture_frame_index < end_of_frame_count:
        raise SystemExit(f"ERROR: {name} EndOfFrame lifecycle was not observed")
    if entry.get("hud_root_active_in_hierarchy") != "1":
        raise SystemExit(f"ERROR: {name} HUD root is inactive")
    try:
        root_alpha = float(entry.get("hud_root_canvas_group_alpha", ""))
    except ValueError:
        raise SystemExit(f"ERROR: {name} HUD root CanvasGroup alpha is invalid")
    if root_alpha <= 0:
        raise SystemExit(f"ERROR: {name} HUD root CanvasGroup alpha is zero")
    for identity_hash in ("semantic_snapshot_hash", "hierarchy_hash"):
        if not re.fullmatch(r"[0-9a-f]{64}", entry.get(identity_hash, "")):
            raise SystemExit(f"ERROR: {name} {identity_hash} is invalid")
    try:
        graphic_count = int(entry.get("required_graphic_count", ""))
    except ValueError:
        raise SystemExit(f"ERROR: {name} required_graphic_count is invalid")
    if graphic_count <= 0:
        raise SystemExit(f"ERROR: {name} has no required graphics")
    categories = []
    for index in range(graphic_count):
        prefix = f"graphic_{index:03d}_"
        path = entry.get(prefix + "path", "")
        category = entry.get(prefix + "category", "")
        identity = entry.get(prefix + "identity", "")
        if not path or not category or not identity:
            raise SystemExit(f"ERROR: {name} graphic {index} identity is incomplete")
        if entry.get(prefix + "active_in_hierarchy") != "1":
            raise SystemExit(f"ERROR: {name} graphic {path} is inactive")
        if entry.get(prefix + "graphic_enabled") != "1":
            raise SystemExit(f"ERROR: {name} graphic {path} is disabled")
        if entry.get(prefix + "visible_pixel_area") != "POSITIVE":
            raise SystemExit(f"ERROR: {name} graphic {path} has no visible area")
        try:
            alpha = float(entry.get(prefix + "alpha_occupancy", ""))
        except ValueError:
            raise SystemExit(f"ERROR: {name} graphic {path} alpha is invalid")
        if alpha <= 0:
            raise SystemExit(f"ERROR: {name} graphic {path} alpha is zero")
        categories.append(category)
    expected_rows = 2 if name.startswith("Idle/") else 3
    if categories.count("ObjectiveHud") < expected_rows + 1:
        raise SystemExit(f"ERROR: {name} ObjectiveHud graphics are incomplete")
    if "ChancePanel" not in categories or "SurfaceBelt" not in categories:
        raise SystemExit(f"ERROR: {name} global HUD graphics are incomplete")
    paths = [
        entry[f"graphic_{index:03d}_path"]
        for index in range(graphic_count)
    ]
    required_fragments = {
        "objective panel background": (
            "HUD_SciFiSoldier_Objectives_02",
            "SPR_Background",
        ),
        "objective left decoration": (
            "HUD_SciFiSoldier_Objectives_02",
            "SPR_Flag",
        ),
        "chance panel background": ("ChancePanel", "Background"),
        "surface belt background": ("SurfaceBeltIndicatorRoot", "Background"),
    }
    for label, fragments in required_fragments.items():
        if not any(all(fragment in path for fragment in fragments) for path in paths):
            raise SystemExit(f"ERROR: {name} required {label} path is missing")
    checkbox_count = sum(
        "Objective_Item_Runtime_" in path and
        "SPR_Item_Inactive" in path
        for path in paths
    )
    if checkbox_count < expected_rows:
        raise SystemExit(f"ERROR: {name} checkbox/icon graphics are incomplete")
    png = output_dir / entry["file"]
    data = png.read_bytes()
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"ERROR: {png.name} is not a PNG")
    width, height = struct.unpack(">II", data[16:24])
    if (width, height) != (expected_width, expected_height):
        raise SystemExit(f"ERROR: {png.name} dimensions {width}x{height}")
    if hashlib.sha256(data).hexdigest() != entry.get("sha256"):
        raise SystemExit(f"ERROR: {png.name} SHA-256 mismatch")

for name, entry in mutation_sections.items():
    required = (
        "path",
        "before_hash",
        "after_capture_hash",
        "mutation_detected",
        "changed_properties",
        "classification",
        "allowed",
        "lane_verdict_before_restore",
        "restored",
        "restored_hash",
    )
    if any(field not in entry for field in required):
        raise SystemExit(f"ERROR: {name} mutation evidence is incomplete")
    if not re.fullmatch(r"[0-9a-f]{64}", entry["before_hash"]):
        raise SystemExit(f"ERROR: {name} before_hash is invalid")
    if not re.fullmatch(r"[0-9a-f]{64}", entry["after_capture_hash"]):
        raise SystemExit(f"ERROR: {name} after_capture_hash is invalid")
    if entry["allowed"] != "1" or entry["lane_verdict_before_restore"] != "PASS":
        raise SystemExit(f"ERROR: {name} failed before restore")
    if entry["restored"] != "1" or entry["restored_hash"] != entry["before_hash"]:
        raise SystemExit(f"ERROR: {name} restore evidence is invalid")
    if entry["mutation_detected"] == "1":
        if entry["classification"] != "EXPECTED_IMPORT_DERIVED_DRIFT":
            raise SystemExit(f"ERROR: {name} mutation classification is not allowlisted")
        required_whitespace_properties = {
            "serialization-whitespace:m_MipmapLimitGroupName:",
            "serialization-whitespace:m_PlatformBlob:",
            "serialization-whitespace:path:",
            "serialization-whitespace:referencedFontAssetGUID:",
            "serialization-whitespace:referencedTextAssetGUID:",
            "serialization-whitespace:m_SourceFontFilePath:",
            "serialization-whitespace:Name:",
            "serialization-whitespace:m_LockedProperties:",
        }
        scale_ratio_properties = {
            "_ScaleRatioA:1->0.9",
            "_ScaleRatioC:1->0.73125",
        }
        observed_properties = set(entry["changed_properties"].split(","))
        if observed_properties not in (
            required_whitespace_properties,
            required_whitespace_properties | scale_ratio_properties,
        ):
            raise SystemExit(f"ERROR: {name} changed property set is not exact")
    elif entry["mutation_detected"] != "0" or entry["classification"] != "NO_MUTATION":
        raise SystemExit(f"ERROR: {name} no-mutation classification is invalid")

print("Objective HUD visual manifest verification: PASS")
PY

    echo "Objective HUD visual evidence capture: PASS"
    echo "  output directory: $output_dir"
    echo "  manifest: $manifest"
    echo "  runner mutation: $runner_mutation_evidence"
    echo "  recorded revision: $expected_head"
    echo "  Climate SDF hash: preserved"
    visual_guard_finish 0
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

    if { [ "$RUN_MODE" = "typography-visual" ] || [ "$RUN_MODE" = "typography-hud-visual" ]; } &&
       [ -n "$TEST_FILTER" ]; then
        echo "ERROR: visual evidence lanes do not accept test filters."
        print_usage
        exit 1
    fi
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
        require_file "$UNITY_PATH" "Unity executable"
        if [ "$mode" = "ui" ] ||
           [ "$mode" = "typography-visual" ] ||
           [ "$mode" = "typography-hud-visual" ]; then
            require_command git
            require_command sha256sum
            verify_climate_committed_source_integrity
            verify_climate_worktree_source_integrity
        fi
        if [ "$mode" = "typography-visual" ] || [ "$mode" = "typography-hud-visual" ]; then
            require_command setsid
            ensure_result_dirs
        else
            require_file "$DOTNET_PATH" "dotnet executable"
            require_file "$STRATIFICATION_CHECKER_PATH" "stratification governance checker"
            require_file "$SEMANTIC_QUERY_CHECKER_PATH" "semantic query governance checker"
            require_file "$ACTION_PLAN_CORRELATION_CHECKER_PATH" "ActionPlanId correlation governance checker"
            ensure_result_dirs

            run_governance_check
            run_semantic_query_migration_check
            run_action_plan_correlation_check
        fi
    else
        if [ "$mode" = "typography-visual" ] || [ "$mode" = "typography-hud-visual" ]; then
            echo "Dry run: revision gate and Unity typography capture will not execute."
        else
            echo "Dry run: governance checks, dotnet builds, and Unity stages will not execute."
        fi
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
        typography-visual)
            run_typography_visual
            ;;
        typography-hud-visual)
            run_objective_hud_visual
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

    if [ "$DRY_RUN" -eq 0 ] &&
       [ "$mode" != "typography-visual" ] &&
       [ "$mode" != "typography-hud-visual" ]; then
        echo "ALL TESTS PASSED"
    fi
}

if [ "${RUN_TESTS_LIBRARY_ONLY:-0}" -eq 0 ]; then
    main "$@"
fi
