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
VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_NAME="none"
VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS=0
VISUAL_GUARD_OBSERVED_SIGNAL_MASK=0
VISUAL_GUARD_OBSERVED_INT=0
VISUAL_GUARD_OBSERVED_TERM=0
VISUAL_GUARD_CO_PENDING_DETECTED="false"
VISUAL_GUARD_SIGNAL_ORDER_CONTRACT="FIRST_OBSERVED_SEQUENTIAL_CO_PENDING_UNSPECIFIED"
VISUAL_GUARD_ORIGINAL_COMMAND_STATUS=0
VISUAL_GUARD_OBSERVATION_COMPLETED=0
VISUAL_GUARD_LANE_VERDICT="NOT_STARTED"
VISUAL_GUARD_CLEANUP_STARTED=0
VISUAL_GUARD_CLEANUP_COMPLETED=0
VISUAL_GUARD_CLEANUP_EFFECTIVE_COUNT=0
VISUAL_GUARD_PROCESS_CLEANUP_EFFECTIVE_COUNT=0
VISUAL_GUARD_MUTATION_OBSERVATION_EFFECTIVE_COUNT=0
VISUAL_GUARD_ASSET_RESTORE_EFFECTIVE_COUNT=0
VISUAL_GUARD_CLEANUP_STATUS=0
VISUAL_GUARD_RESTORE_RESULT="NOT_STARTED"
VISUAL_GUARD_ACTIVE_CHILD_PID=""
VISUAL_GUARD_ACTIVE_CHILD_PGID=""
VISUAL_GUARD_OWNED_CHILD_PID=""
VISUAL_GUARD_OWNED_CHILD_PGID=""
VISUAL_GUARD_PREEXISTING_UNITY_KEYS=""
VISUAL_GUARD_FALLBACK_USED=0
VISUAL_GUARD_PRIMARY_WAS_LIVE=0
VISUAL_GUARD_PRIMARY_TERMINATION_ATTEMPTED=0
VISUAL_GUARD_PRIMARY_TERMINATION_RESULT="NOT_ATTEMPTED"
VISUAL_GUARD_PRIMARY_WAIT_RESULT="NOT_ATTEMPTED"
VISUAL_GUARD_FALLBACK_SCAN_PERFORMED=0
VISUAL_GUARD_FALLBACK_SCAN_PASSES=0
VISUAL_GUARD_FALLBACK_CANDIDATE_COUNT=0
VISUAL_GUARD_FALLBACK_TERMINATION_COUNT=0
VISUAL_GUARD_FALLBACK_EMPTY_STREAK=0
VISUAL_GUARD_EARLY_EMPTY_STREAK=0
VISUAL_GUARD_POST_GRACE_QUIET_STREAK=0
VISUAL_GUARD_FALLBACK_TIMEOUT=0
VISUAL_GUARD_FALLBACK_LAST_ELIGIBLE_COUNT=0
VISUAL_GUARD_FALLBACK_SEEN_KEYS=""
VISUAL_GUARD_PRIMARY_TERMINATED_AT_MS=""
VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS=""
VISUAL_GUARD_STARTUP_GRACE_DEADLINE_MS=""
VISUAL_GUARD_STARTUP_GRACE_ELAPSED_MS=0
VISUAL_GUARD_STARTUP_GRACE_COMPLETED=0
VISUAL_GUARD_LAST_CANDIDATE_SEEN_AT_MS=""
VISUAL_GUARD_LAST_CANDIDATE_TERMINATED_AT_MS=""
VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS=""
VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS=""
VISUAL_GUARD_QUIET_PERIOD_COMPLETED=0
VISUAL_GUARD_LATE_CANDIDATE_DETECTED=0
VISUAL_GUARD_HARD_CLEANUP_DEADLINE_MS=""
VISUAL_GUARD_HARD_TIMEOUT_REACHED=0
VISUAL_GUARD_FALLBACK_FAILURE_REASON=""
VISUAL_GUARD_CURRENT_SCAN_AT_MS=""
VISUAL_GUARD_FINAL_SURVIVOR_COUNT=0
VISUAL_GUARD_FINAL_SURVIVOR_PIDS=""
VISUAL_GUARD_CLEANUP_PROCESS_RESULT="NOT_STARTED"
VISUAL_GUARD_PROCESS_CLEANUP_STATUS=0
VISUAL_GUARD_RESTORE_ON_SUCCESS=1
UNITY_DETACHED_STARTUP_GRACE_MS="${UNITY_DETACHED_STARTUP_GRACE_MS:-6000}"
UNITY_DETACHED_POLL_INTERVAL_MS="${UNITY_DETACHED_POLL_INTERVAL_MS:-200}"
UNITY_DETACHED_QUIET_PERIOD_MS="${UNITY_DETACHED_QUIET_PERIOD_MS:-400}"
UNITY_DETACHED_HARD_TIMEOUT_MS="${UNITY_DETACHED_HARD_TIMEOUT_MS:-12000}"
VISUAL_GUARD_EXPECTED_PROJECT_PATH_CANONICAL=""
VISUAL_GUARD_CANDIDATE_SOURCES=()
VISUAL_GUARD_CANDIDATE_PIDS=()
VISUAL_GUARD_CANDIDATE_START_IDENTITIES=()
VISUAL_GUARD_CANDIDATE_PROJECT_PATHS_RAW=()
VISUAL_GUARD_CANDIDATE_PROJECT_PATHS_CANONICAL=()
VISUAL_GUARD_CANDIDATE_PREEXISTING=()
VISUAL_GUARD_CANDIDATE_MATCHES=()
VISUAL_GUARD_CANDIDATE_TERMINATION_ATTEMPTED=()
VISUAL_GUARD_CANDIDATE_TERMINATION_RESULTS=()
VISUAL_GUARD_EXITING=0
VISUAL_GUARD_PHASE="DONE"
VISUAL_GUARD_FINAL_STATUS_SNAPSHOT=0
CAPTURE_GUARD_PROFILE="visual"

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
NANUM_SOURCE_TTF_SHA256="48a28e97b34fc8e5b157657633670cd1b7de126cfc414da65ce9c3d5bc8be733"
NANUM_SDF_GUID="4662feb1d501d1f479b757a82e304069"
NANUM_MATERIAL_LOCAL_ID="2769584723452840789"
NANUM_ATLAS_LOCAL_ID="3176292610376301967"
NANUM_SYNTHETIC_BOLD_GUID="2a2e67f1c1d143dc9f2d4af986ba7f21"
OBJECTIVE_HUD_VISUAL_OUTPUT_ROOT="$PROJECT_PATH_WSL/TestLogs/ObjectiveHudVisualQA"
OBJECTIVE_HUD_VISUAL_WIDTH=1920
OBJECTIVE_HUD_VISUAL_HEIGHT=1080
OBJECTIVE_HUD_VISUAL_EXECUTE_METHOD="Game.Feature.UI.Tests.ObjectiveHudVisualEvidenceUtility.CaptureFromCommandLine"
OBJECTIVE_HUD_VISUAL_CLIMATE_ASSET="Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset"
M1A_HUD_GUIDE_VISUAL_OUTPUT_ROOT="$PROJECT_PATH_WSL/TestLogs/M1aHudGuideVisualQA"
M1A_HUD_GUIDE_VISUAL_WIDTH=1920
M1A_HUD_GUIDE_VISUAL_HEIGHT=1080
CLIMATE_GLYPH_UPDATE_EXECUTE_METHOD="Game.Feature.UI.Composition.Editor.ClimateCrisisKrGlyphUpdateUtility.GenerateFromCommandLine"
CLIMATE_SOURCE_TTF_ASSET="Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000.ttf"
CLIMATE_SOURCE_TTF_META="$CLIMATE_SOURCE_TTF_ASSET.meta"
CLIMATE_SDF_ASSET="$OBJECTIVE_HUD_VISUAL_CLIMATE_ASSET"
CLIMATE_SDF_META="$CLIMATE_SDF_ASSET.meta"
CLIMATE_COMMITTED_SDF_SHA256="d7c5f8586ef25dee65232c0054deb249d2937622b99e8f85773935a9a1432188"
CLIMATE_SOURCE_TTF_SHA256="aa0e58ef1dd54ae760c29bdd0ce28d6b710c2d5910e88efadf5e23416b01d0f1"
CLIMATE_SOURCE_TTF_GUID="5360535d0de75234ca21822297323672"
CLIMATE_SDF_GUID="40d61154fd6576b4d85c2d78460b16ad"
CLIMATE_MATERIAL_LOCAL_ID="1352911973252649374"
CLIMATE_ATLAS_LOCAL_ID="-2536001923755311345"
GLYPH_STRING_TABLE_INPUT="Assets/Localization/StringTables/UI/UI Shared Data.asset"
GLYPH_STRING_TABLE_INPUT_META="$GLYPH_STRING_TABLE_INPUT.meta"
GLYPH_STRING_TABLE_INPUT_GUID="1139bb803b655c2488dd6dca46c97cf5"
RESULT_DIR="$PROJECT_PATH_WSL/TestResults"
CLIMATE_GLYPH_UPDATE_LOG="$RESULT_DIR/wsl-climate-glyph-update.log"
CLIMATE_GLYPH_UPDATE_EVIDENCE="$RESULT_DIR/wsl-climate-glyph-update-evidence.log"
CLIMATE_GLYPH_UPDATE_LIFECYCLE="$RESULT_DIR/wsl-climate-glyph-update-cleanup.log"
TERMINAL_RESULT_VISUAL_OUTPUT_ROOT="$PROJECT_PATH_WSL/TestLogs/TerminalResultVisualQA"
TERMINAL_RESULT_VISUAL_EXECUTE_METHOD="Game.Feature.UI.Tests.TerminalResultVisualEvidenceUtility.CaptureFromCommandLine"
TERMINAL_RESULT_VISUAL_WIDTH=1920
TERMINAL_RESULT_VISUAL_HEIGHT=1080
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
    local candidate_nanum_ttf_hash
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
    candidate_nanum_ttf_hash="$(
        sha256sum "$PROJECT_PATH_WSL/$NANUM_SOURCE_TTF_ASSET" | awk '{print $1}'
    )"
    if [ "$candidate_ttf_hash" != "$CLIMATE_SOURCE_TTF_SHA256" ]; then
        echo "ERROR: Candidate worktree Climate source TTF mismatch."
        echo "  expected: $CLIMATE_SOURCE_TTF_SHA256"
        echo "  actual:   $candidate_ttf_hash"
        return 1
    fi
    if [ "$candidate_nanum_ttf_hash" != "$NANUM_SOURCE_TTF_SHA256" ]; then
        echo "ERROR: Candidate worktree Nanum source TTF mismatch."
        echo "  expected: $NANUM_SOURCE_TTF_SHA256"
        echo "  actual:   $candidate_nanum_ttf_hash"
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
    require_worktree_file_text \
        "$CLIMATE_SDF_ASSET" \
        "--- !u!28 &$CLIMATE_ATLAS_LOCAL_ID" \
        "atlas texture localID"

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
        "$NANUM_SDF_ASSET" \
        "--- !u!21 &$NANUM_MATERIAL_LOCAL_ID" \
        "Nanum material localID"
    require_worktree_file_text \
        "$NANUM_SDF_ASSET" \
        "--- !u!28 &$NANUM_ATLAS_LOCAL_ID" \
        "Nanum atlas texture localID"
    require_worktree_file_text \
        "$NANUM_SYNTHETIC_BOLD_ASSET" \
        "guid: $NANUM_SDF_GUID" \
        "Nanum synthetic-bold atlas reference"
    require_worktree_file_text \
        "$GLYPH_STRING_TABLE_INPUT_META" \
        "guid: $GLYPH_STRING_TABLE_INPUT_GUID" \
        "glyph String Table input GUID"

    echo "Climate/Nanum candidate worktree integrity: PASS"
    echo "  Climate candidate SHA-256 (record only): $candidate_sdf_hash"
    echo "  Candidate output hash is not a pre-update acceptance condition."
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
    if [ "$hash" = "$CLIMATE_COMMITTED_SDF_SHA256" ]; then
        echo "  Classification: CANDIDATE_SOURCE_SHAPE"
        return 0
    fi
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
    echo "Usage: ./run_tests.sh [--print-config|--dry-run <lane>|core|core-feature-gate|ui|climate-glyph-update|typography-visual|typography-hud-visual|typography-hud-guide-visual|typography-result-visual|full|--integration-simulation|--integration-replay|--integration-fuzz] [--filter <test-filter>|--test-filter <test-filter>]"
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
    if [ "${CAPTURE_GUARD_PROFILE:-visual}" = "glyph-update" ]; then
        printf '%s\n' \
            "$CLIMATE_SDF_ASSET" \
            "$NANUM_SDF_ASSET"
        return 0
    fi

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

    if [ "${VISUAL_GUARD_TRAP_INSTALLED:-0}" -eq 1 ]; then
        VISUAL_GUARD_MUTATION_OBSERVATION_EFFECTIVE_COUNT=$((VISUAL_GUARD_MUTATION_OBSERVATION_EFFECTIVE_COUNT + 1))
    fi
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

discard_capture_asset_baseline() {
    local baseline_root="$1"

    if [ -z "$baseline_root" ] ||
       [ "$baseline_root" = "/" ] ||
       [ "$baseline_root" = "$PROJECT_PATH_WSL" ]; then
        echo "ERROR: Refusing to discard an unsafe asset baseline path: $baseline_root"
        return 1
    fi
    rm -rf -- "$baseline_root"
    if [ -e "$baseline_root" ]; then
        echo "ERROR: Asset baseline snapshot was not discarded: $baseline_root"
        return 1
    fi
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
    VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_NAME="none"
    VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS=0
    VISUAL_GUARD_OBSERVED_SIGNAL_MASK=0
    VISUAL_GUARD_OBSERVED_INT=0
    VISUAL_GUARD_OBSERVED_TERM=0
    VISUAL_GUARD_CO_PENDING_DETECTED="false"
    VISUAL_GUARD_SIGNAL_ORDER_CONTRACT="FIRST_OBSERVED_SEQUENTIAL_CO_PENDING_UNSPECIFIED"
    VISUAL_GUARD_ORIGINAL_COMMAND_STATUS=0
    VISUAL_GUARD_OBSERVATION_COMPLETED=0
    VISUAL_GUARD_LANE_VERDICT="NOT_STARTED"
    VISUAL_GUARD_CLEANUP_STARTED=0
    VISUAL_GUARD_CLEANUP_COMPLETED=0
    VISUAL_GUARD_CLEANUP_EFFECTIVE_COUNT=0
    VISUAL_GUARD_PROCESS_CLEANUP_EFFECTIVE_COUNT=0
    VISUAL_GUARD_MUTATION_OBSERVATION_EFFECTIVE_COUNT=0
    VISUAL_GUARD_ASSET_RESTORE_EFFECTIVE_COUNT=0
    VISUAL_GUARD_CLEANUP_STATUS=0
    VISUAL_GUARD_RESTORE_RESULT="NOT_STARTED"
    VISUAL_GUARD_ACTIVE_CHILD_PID=""
    VISUAL_GUARD_ACTIVE_CHILD_PGID=""
    VISUAL_GUARD_OWNED_CHILD_PID=""
    VISUAL_GUARD_OWNED_CHILD_PGID=""
    VISUAL_GUARD_PREEXISTING_UNITY_KEYS=""
    VISUAL_GUARD_FALLBACK_USED=0
    VISUAL_GUARD_PRIMARY_WAS_LIVE=0
    VISUAL_GUARD_PRIMARY_TERMINATION_ATTEMPTED=0
    VISUAL_GUARD_PRIMARY_TERMINATION_RESULT="NOT_ATTEMPTED"
    VISUAL_GUARD_PRIMARY_WAIT_RESULT="NOT_ATTEMPTED"
    VISUAL_GUARD_FALLBACK_SCAN_PERFORMED=0
    VISUAL_GUARD_FALLBACK_SCAN_PASSES=0
    VISUAL_GUARD_FALLBACK_CANDIDATE_COUNT=0
    VISUAL_GUARD_FALLBACK_TERMINATION_COUNT=0
    VISUAL_GUARD_FALLBACK_EMPTY_STREAK=0
    VISUAL_GUARD_EARLY_EMPTY_STREAK=0
    VISUAL_GUARD_POST_GRACE_QUIET_STREAK=0
    VISUAL_GUARD_FALLBACK_TIMEOUT=0
    VISUAL_GUARD_FALLBACK_LAST_ELIGIBLE_COUNT=0
    VISUAL_GUARD_FALLBACK_SEEN_KEYS=""
    VISUAL_GUARD_PRIMARY_TERMINATED_AT_MS=""
    VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS=""
    VISUAL_GUARD_STARTUP_GRACE_DEADLINE_MS=""
    VISUAL_GUARD_STARTUP_GRACE_ELAPSED_MS=0
    VISUAL_GUARD_STARTUP_GRACE_COMPLETED=0
    VISUAL_GUARD_LAST_CANDIDATE_SEEN_AT_MS=""
    VISUAL_GUARD_LAST_CANDIDATE_TERMINATED_AT_MS=""
    VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS=""
    VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS=""
    VISUAL_GUARD_QUIET_PERIOD_COMPLETED=0
    VISUAL_GUARD_LATE_CANDIDATE_DETECTED=0
    VISUAL_GUARD_HARD_CLEANUP_DEADLINE_MS=""
    VISUAL_GUARD_HARD_TIMEOUT_REACHED=0
    VISUAL_GUARD_FALLBACK_FAILURE_REASON=""
    VISUAL_GUARD_CURRENT_SCAN_AT_MS=""
    VISUAL_GUARD_FINAL_SURVIVOR_COUNT=0
    VISUAL_GUARD_FINAL_SURVIVOR_PIDS=""
    VISUAL_GUARD_CLEANUP_PROCESS_RESULT="NOT_STARTED"
    VISUAL_GUARD_PROCESS_CLEANUP_STATUS=0
    VISUAL_GUARD_RESTORE_ON_SUCCESS=1
    VISUAL_GUARD_EXPECTED_PROJECT_PATH_CANONICAL=""
    VISUAL_GUARD_CANDIDATE_SOURCES=()
    VISUAL_GUARD_CANDIDATE_PIDS=()
    VISUAL_GUARD_CANDIDATE_START_IDENTITIES=()
    VISUAL_GUARD_CANDIDATE_PROJECT_PATHS_RAW=()
    VISUAL_GUARD_CANDIDATE_PROJECT_PATHS_CANONICAL=()
    VISUAL_GUARD_CANDIDATE_PREEXISTING=()
    VISUAL_GUARD_CANDIDATE_MATCHES=()
    VISUAL_GUARD_CANDIDATE_TERMINATION_ATTEMPTED=()
    VISUAL_GUARD_CANDIDATE_TERMINATION_RESULTS=()
    VISUAL_GUARD_EXITING=0
    VISUAL_GUARD_PHASE="RUNNING"
    VISUAL_GUARD_FINAL_STATUS_SNAPSHOT=0
}

visual_guard_write_lifecycle_evidence() {
    local final_exit_status="$1"
    local index
    local observed_signal_mask_name="none"

    if [ -z "$VISUAL_GUARD_LIFECYCLE_EVIDENCE" ]; then
        return 0
    fi

    case "$VISUAL_GUARD_OBSERVED_SIGNAL_MASK" in
        1)
            observed_signal_mask_name="INT"
            ;;
        2)
            observed_signal_mask_name="TERM"
            ;;
        3)
            observed_signal_mask_name="INT|TERM"
            ;;
    esac

    {
        echo "schema_version=4"
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
        echo "signal_order_contract=$VISUAL_GUARD_SIGNAL_ORDER_CONTRACT"
        echo "first_observed_signal=$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_NAME"
        echo "first_observed_status=$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS"
        echo "observed_signal_mask=$observed_signal_mask_name"
        echo "observed_int=$(
            if [ "$VISUAL_GUARD_OBSERVED_INT" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "observed_term=$(
            if [ "$VISUAL_GUARD_OBSERVED_TERM" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "co_pending_detected=$VISUAL_GUARD_CO_PENDING_DETECTED"
        echo "final_interruption_status=$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS"
        echo "original_command_status=$VISUAL_GUARD_ORIGINAL_COMMAND_STATUS"
        echo "observation_order=ALL_GUARDED_PATHS_BEFORE_ANY_RESTORE"
        echo "mutation_observation_completed=$VISUAL_GUARD_OBSERVATION_COMPLETED"
        echo "lane_verdict=$VISUAL_GUARD_LANE_VERDICT"
        echo "cleanup_started=$VISUAL_GUARD_CLEANUP_STARTED"
        echo "cleanup_completed=$VISUAL_GUARD_CLEANUP_COMPLETED"
        echo "cleanup_effective_count=$VISUAL_GUARD_CLEANUP_EFFECTIVE_COUNT"
        echo "process_cleanup_effective_count=$VISUAL_GUARD_PROCESS_CLEANUP_EFFECTIVE_COUNT"
        echo "mutation_observation_effective_count=$VISUAL_GUARD_MUTATION_OBSERVATION_EFFECTIVE_COUNT"
        echo "asset_restore_effective_count=$VISUAL_GUARD_ASSET_RESTORE_EFFECTIVE_COUNT"
        echo "cleanup_status=$VISUAL_GUARD_CLEANUP_STATUS"
        echo "restore_result=$VISUAL_GUARD_RESTORE_RESULT"
        echo "guard_phase=$VISUAL_GUARD_PHASE"
        echo "final_status_snapshot=$VISUAL_GUARD_FINAL_STATUS_SNAPSHOT"
        echo "owned_child_pid=$VISUAL_GUARD_OWNED_CHILD_PID"
        echo "owned_child_pgid=$VISUAL_GUARD_OWNED_CHILD_PGID"
        echo "fallback_used=$VISUAL_GUARD_FALLBACK_USED"
        echo "primary_pid=$VISUAL_GUARD_OWNED_CHILD_PID"
        echo "primary_pgid=$VISUAL_GUARD_OWNED_CHILD_PGID"
        echo "primary_was_live=$VISUAL_GUARD_PRIMARY_WAS_LIVE"
        echo "primary_termination_attempted=$VISUAL_GUARD_PRIMARY_TERMINATION_ATTEMPTED"
        echo "primary_termination_result=$VISUAL_GUARD_PRIMARY_TERMINATION_RESULT"
        echo "primary_wait_result=$VISUAL_GUARD_PRIMARY_WAIT_RESULT"
        echo "primary_terminated_at=$VISUAL_GUARD_PRIMARY_TERMINATED_AT_MS"
        echo "fallback_scan_performed=$(
            if [ "$VISUAL_GUARD_FALLBACK_SCAN_PERFORMED" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "fallback_scan_passes=$VISUAL_GUARD_FALLBACK_SCAN_PASSES"
        echo "fallback_candidate_count=$VISUAL_GUARD_FALLBACK_CANDIDATE_COUNT"
        echo "fallback_termination_count=$VISUAL_GUARD_FALLBACK_TERMINATION_COUNT"
        echo "fallback_empty_streak=$VISUAL_GUARD_FALLBACK_EMPTY_STREAK"
        echo "startup_grace_ms=$UNITY_DETACHED_STARTUP_GRACE_MS"
        echo "startup_grace_started_at=$VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS"
        echo "startup_grace_deadline=$VISUAL_GUARD_STARTUP_GRACE_DEADLINE_MS"
        echo "startup_grace_elapsed_ms=$VISUAL_GUARD_STARTUP_GRACE_ELAPSED_MS"
        echo "startup_grace_completed=$(
            if [ "$VISUAL_GUARD_STARTUP_GRACE_COMPLETED" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "poll_interval_ms=$UNITY_DETACHED_POLL_INTERVAL_MS"
        echo "early_empty_streak=$VISUAL_GUARD_EARLY_EMPTY_STREAK"
        echo "post_grace_quiet_streak=$VISUAL_GUARD_POST_GRACE_QUIET_STREAK"
        echo "quiet_period_ms=$UNITY_DETACHED_QUIET_PERIOD_MS"
        echo "quiet_period_started_at=$VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS"
        echo "quiet_period_deadline=$VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS"
        echo "quiet_period_completed=$(
            if [ "$VISUAL_GUARD_QUIET_PERIOD_COMPLETED" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "late_candidate_detected=$(
            if [ "$VISUAL_GUARD_LATE_CANDIDATE_DETECTED" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "last_candidate_seen_at=$VISUAL_GUARD_LAST_CANDIDATE_SEEN_AT_MS"
        echo "last_candidate_terminated_at=$VISUAL_GUARD_LAST_CANDIDATE_TERMINATED_AT_MS"
        echo "hard_timeout_ms=$UNITY_DETACHED_HARD_TIMEOUT_MS"
        echo "hard_cleanup_deadline=$VISUAL_GUARD_HARD_CLEANUP_DEADLINE_MS"
        echo "hard_timeout_reached=$(
            if [ "$VISUAL_GUARD_HARD_TIMEOUT_REACHED" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "clock_source=MONOTONIC_PROC_UPTIME"
        echo "fallback_failure_reason=$VISUAL_GUARD_FALLBACK_FAILURE_REASON"
        echo "fallback_timeout=$(
            if [ "$VISUAL_GUARD_FALLBACK_TIMEOUT" -eq 1 ]; then
                printf true
            else
                printf false
            fi
        )"
        echo "final_survivor_count=$VISUAL_GUARD_FINAL_SURVIVOR_COUNT"
        echo "final_survivor_pids=$VISUAL_GUARD_FINAL_SURVIVOR_PIDS"
        echo "cleanup_process_result=$VISUAL_GUARD_CLEANUP_PROCESS_RESULT"
        echo "process_cleanup_status=$VISUAL_GUARD_PROCESS_CLEANUP_STATUS"
        echo "restore_on_success=$VISUAL_GUARD_RESTORE_ON_SUCCESS"
        echo "expected_project_path_canonical=$VISUAL_GUARD_EXPECTED_PROJECT_PATH_CANONICAL"
        echo "final_exit_status=$final_exit_status"
        for index in "${!VISUAL_GUARD_CANDIDATE_PIDS[@]}"; do
            echo
            echo "[process-candidate/$index]"
            echo "candidate_source=${VISUAL_GUARD_CANDIDATE_SOURCES[$index]}"
            echo "candidate_pid=${VISUAL_GUARD_CANDIDATE_PIDS[$index]}"
            echo "candidate_start_identity=${VISUAL_GUARD_CANDIDATE_START_IDENTITIES[$index]}"
            echo "candidate_project_path_raw=${VISUAL_GUARD_CANDIDATE_PROJECT_PATHS_RAW[$index]}"
            echo "candidate_project_path_canonical=${VISUAL_GUARD_CANDIDATE_PROJECT_PATHS_CANONICAL[$index]}"
            echo "expected_project_path_canonical=$VISUAL_GUARD_EXPECTED_PROJECT_PATH_CANONICAL"
            echo "preexisting=${VISUAL_GUARD_CANDIDATE_PREEXISTING[$index]}"
            echo "match=${VISUAL_GUARD_CANDIDATE_MATCHES[$index]}"
            echo "termination_attempted=${VISUAL_GUARD_CANDIDATE_TERMINATION_ATTEMPTED[$index]}"
            echo "termination_result=${VISUAL_GUARD_CANDIDATE_TERMINATION_RESULTS[$index]}"
        done
    } > "$VISUAL_GUARD_LIFECYCLE_EVIDENCE"
}

visual_guard_prepare_interrupted_mutation_evidence() {
    if [ -z "$VISUAL_GUARD_MUTATION_EVIDENCE" ] ||
       [ -s "$VISUAL_GUARD_MUTATION_EVIDENCE" ]; then
        return 0
    fi

    VISUAL_GUARD_MUTATION_OBSERVATION_EFFECTIVE_COUNT=$((VISUAL_GUARD_MUTATION_OBSERVATION_EFFECTIVE_COUNT + 1))
    {
        echo "schema_version=1"
        echo "observation_order=INTERRUPTED_BEFORE_COMPLETE_CLASSIFICATION"
        echo "lane_verdict_before_restore=INTERRUPTED"
        echo "termination_signal=$VISUAL_GUARD_TERMINATION_SIGNAL"
        echo "signal_exit_status=$VISUAL_GUARD_SIGNAL_STATUS"
    } > "$VISUAL_GUARD_MUTATION_EVIDENCE"
}

visual_guard_record_signal() {
    local signal_name="$1"
    local signal_status="$2"
    local first_observation=0

    case "$signal_name" in
        INT)
            VISUAL_GUARD_OBSERVED_INT=1
            VISUAL_GUARD_OBSERVED_SIGNAL_MASK=$((VISUAL_GUARD_OBSERVED_SIGNAL_MASK | 1))
            ;;
        TERM)
            VISUAL_GUARD_OBSERVED_TERM=1
            VISUAL_GUARD_OBSERVED_SIGNAL_MASK=$((VISUAL_GUARD_OBSERVED_SIGNAL_MASK | 2))
            ;;
    esac
    if [ "$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS" -eq 0 ]; then
        VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_NAME="$signal_name"
        VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS="$signal_status"
        first_observation=1
    fi
    VISUAL_GUARD_CO_PENDING_DETECTED="unknown"
    VISUAL_GUARD_INTERRUPTED=1
    VISUAL_GUARD_TERMINATION_SIGNAL="$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_NAME"
    VISUAL_GUARD_SIGNAL_STATUS="$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS"
    VISUAL_GUARD_LANE_VERDICT="INTERRUPTED"
    if [ "${RUN_TESTS_LIBRARY_ONLY:-0}" -eq 1 ] &&
       [ "${VISUAL_GUARD_TEST_HOOKS_ENABLED:-0}" -eq 1 ] &&
       declare -F visual_guard_signal_observed_test_hook >/dev/null; then
        visual_guard_signal_observed_test_hook \
            "$signal_name" \
            "$signal_status" \
            "$first_observation"
    fi
}

visual_guard_request_active_child_stop() {
    if { [ -n "$VISUAL_GUARD_ACTIVE_CHILD_PID" ] &&
         kill -0 "$VISUAL_GUARD_ACTIVE_CHILD_PID" 2>/dev/null; } ||
       { [ -n "$VISUAL_GUARD_ACTIVE_CHILD_PGID" ] &&
         kill -0 -- "-$VISUAL_GUARD_ACTIVE_CHILD_PGID" 2>/dev/null; }; then
        VISUAL_GUARD_PRIMARY_WAS_LIVE=1
        VISUAL_GUARD_PRIMARY_TERMINATION_ATTEMPTED=1
        VISUAL_GUARD_PRIMARY_TERMINATION_RESULT="REQUESTED"
    fi
    if [ -n "$VISUAL_GUARD_ACTIVE_CHILD_PID" ] &&
       kill -0 "$VISUAL_GUARD_ACTIVE_CHILD_PID" 2>/dev/null; then
        kill -TERM "$VISUAL_GUARD_ACTIVE_CHILD_PID" 2>/dev/null || true
    fi
    if [ -n "$VISUAL_GUARD_ACTIVE_CHILD_PGID" ] &&
       kill -0 -- "-$VISUAL_GUARD_ACTIVE_CHILD_PGID" 2>/dev/null; then
        kill -TERM -- "-$VISUAL_GUARD_ACTIVE_CHILD_PGID" 2>/dev/null || true
    fi
}

visual_guard_calculate_final_status() {
    local original_status="$1"
    local cleanup_status="$2"

    if [ "$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS" -ne 0 ]; then
        printf '%s\n' "$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS"
    elif [ "$original_status" -ne 0 ]; then
        printf '%s\n' "$original_status"
    elif [ "$cleanup_status" -ne 0 ]; then
        printf '%s\n' "$cleanup_status"
    else
        printf '0\n'
    fi
}

visual_guard_run_finalization_test_hook() {
    local phase="$1"

    if [ "${RUN_TESTS_LIBRARY_ONLY:-0}" -ne 1 ] ||
       [ "${VISUAL_GUARD_TEST_HOOKS_ENABLED:-0}" -ne 1 ] ||
       ! declare -F visual_guard_finalization_test_hook >/dev/null; then
        return 0
    fi
    visual_guard_finalization_test_hook "$phase"
}

visual_guard_monotonic_ms() {
    local uptime
    local seconds
    local fraction

    if [ ! -r /proc/uptime ]; then
        echo "ERROR: Monotonic /proc/uptime clock is unavailable." >&2
        return 1
    fi
    IFS=' ' read -r uptime _ < /proc/uptime
    seconds="${uptime%%.*}"
    fraction="${uptime#*.}000"
    fraction="${fraction:0:3}"
    printf '%s\n' "$((10#$seconds * 1000 + 10#$fraction))"
}

visual_guard_sleep_ms() {
    local duration_ms="$1"
    local duration

    if [ "$duration_ms" -le 0 ]; then
        return 0
    fi
    printf -v duration '%d.%03d' \
        "$((duration_ms / 1000))" \
        "$((duration_ms % 1000))"
    sleep "$duration"
}

visual_guard_validate_cleanup_timing_config() {
    local name
    local value

    for name in \
        UNITY_DETACHED_STARTUP_GRACE_MS \
        UNITY_DETACHED_POLL_INTERVAL_MS \
        UNITY_DETACHED_QUIET_PERIOD_MS \
        UNITY_DETACHED_HARD_TIMEOUT_MS
    do
        value="${!name}"
        if ! [[ "$value" =~ ^[1-9][0-9]*$ ]]; then
            echo "ERROR: $name must be a positive integer millisecond value: $value"
            return 1
        fi
    done
    if [ "$UNITY_DETACHED_HARD_TIMEOUT_MS" -le \
         "$((UNITY_DETACHED_STARTUP_GRACE_MS + UNITY_DETACHED_QUIET_PERIOD_MS))" ]; then
        echo "ERROR: UNITY_DETACHED_HARD_TIMEOUT_MS must exceed startup grace plus quiet period."
        return 1
    fi
}

visual_guard_stop_active_child() {
    local child_pid="$VISUAL_GUARD_ACTIVE_CHILD_PID"
    local child_pgid="$VISUAL_GUARD_ACTIVE_CHILD_PGID"
    local attempt
    local primary_failed=0
    local fallback_failed=0

    if { [ -n "$child_pid" ] && kill -0 "$child_pid" 2>/dev/null; } ||
       { [ -n "$child_pgid" ] && kill -0 -- "-$child_pgid" 2>/dev/null; }; then
        VISUAL_GUARD_PRIMARY_WAS_LIVE=1
        VISUAL_GUARD_PRIMARY_TERMINATION_ATTEMPTED=1
        VISUAL_GUARD_PRIMARY_TERMINATION_RESULT="REQUESTED"
    fi

    if [ -n "$child_pid" ] && kill -0 "$child_pid" 2>/dev/null; then
        kill -TERM "$child_pid" 2>/dev/null || true
        for attempt in $(seq 1 20); do
            if ! kill -0 "$child_pid" 2>/dev/null; then
                break
            fi
            sleep 0.1
        done
        if kill -0 "$child_pid" 2>/dev/null; then
            kill -KILL "$child_pid" 2>/dev/null || true
            for attempt in $(seq 1 20); do
                if ! kill -0 "$child_pid" 2>/dev/null; then
                    break
                fi
                sleep 0.1
            done
        fi
    fi
    if [ -n "$child_pgid" ] && kill -0 -- "-$child_pgid" 2>/dev/null; then
        kill -TERM -- "-$child_pgid" 2>/dev/null || true
        for attempt in $(seq 1 30); do
            if ! kill -0 -- "-$child_pgid" 2>/dev/null; then
                break
            fi
            sleep 0.1
        done
        if kill -0 -- "-$child_pgid" 2>/dev/null; then
            kill -KILL -- "-$child_pgid" 2>/dev/null || true
            for attempt in $(seq 1 20); do
                if ! kill -0 -- "-$child_pgid" 2>/dev/null; then
                    break
                fi
                sleep 0.1
            done
        fi
    fi
    if [ -n "$child_pid" ]; then
        wait "$child_pid" 2>/dev/null || true
    fi

    if { [ -n "$child_pid" ] && kill -0 "$child_pid" 2>/dev/null; } ||
       { [ -n "$child_pgid" ] && kill -0 -- "-$child_pgid" 2>/dev/null; }; then
        VISUAL_GUARD_PRIMARY_TERMINATION_RESULT="FAILED"
        VISUAL_GUARD_PRIMARY_WAIT_RESULT="STILL_LIVE"
        primary_failed=1
    elif [ "$VISUAL_GUARD_PRIMARY_WAS_LIVE" -eq 1 ]; then
        VISUAL_GUARD_PRIMARY_TERMINATION_RESULT="TERMINATED"
        VISUAL_GUARD_PRIMARY_WAIT_RESULT="EXITED"
    else
        VISUAL_GUARD_PRIMARY_TERMINATION_RESULT="NOT_NEEDED"
        VISUAL_GUARD_PRIMARY_WAIT_RESULT="ALREADY_EXITED"
    fi
    if [ "$primary_failed" -eq 0 ]; then
        VISUAL_GUARD_PRIMARY_TERMINATED_AT_MS="$(visual_guard_monotonic_ms)"
    fi
    VISUAL_GUARD_ACTIVE_CHILD_PID=""
    VISUAL_GUARD_ACTIVE_CHILD_PGID=""

    if ! visual_guard_poll_fallback_candidates; then
        fallback_failed=1
    fi
    if [ "$primary_failed" -ne 0 ]; then
        VISUAL_GUARD_CLEANUP_PROCESS_RESULT="FAILED_PRIMARY_TERMINATION"
        echo "ERROR: Visual guard could not terminate the runner-recorded primary process."
    elif [ "$fallback_failed" -ne 0 ]; then
        if [ "$VISUAL_GUARD_FINAL_SURVIVOR_COUNT" -ne 0 ]; then
            VISUAL_GUARD_CLEANUP_PROCESS_RESULT="FAILED_RUNNER_OWNED_PROCESS_REMAINS"
        else
            VISUAL_GUARD_CLEANUP_PROCESS_RESULT="FAILED_CLEANUP_QUIET_PERIOD_TIMEOUT"
        fi
        if [ "$VISUAL_GUARD_INTERRUPTED" -eq 0 ]; then
            VISUAL_GUARD_LANE_VERDICT="$VISUAL_GUARD_CLEANUP_PROCESS_RESULT"
        fi
        echo "ERROR: Visual guard fallback cleanup did not reach a clean quiescent state: $VISUAL_GUARD_FALLBACK_FAILURE_REASON"
    else
        VISUAL_GUARD_CLEANUP_PROCESS_RESULT="PASS"
    fi
    [ "$primary_failed" -eq 0 ] && [ "$fallback_failed" -eq 0 ]
}

visual_guard_cleanup_process_once() {
    if [ "$VISUAL_GUARD_PROCESS_CLEANUP_EFFECTIVE_COUNT" -ne 0 ]; then
        return "$VISUAL_GUARD_PROCESS_CLEANUP_STATUS"
    fi

    VISUAL_GUARD_PROCESS_CLEANUP_EFFECTIVE_COUNT=1
    if visual_guard_stop_active_child; then
        VISUAL_GUARD_PROCESS_CLEANUP_STATUS=0
    else
        VISUAL_GUARD_PROCESS_CLEANUP_STATUS=1
    fi
    return "$VISUAL_GUARD_PROCESS_CLEANUP_STATUS"
}

visual_guard_cleanup() {
    local original_status="${1:-0}"
    local cleanup_status=0

    if [ "$VISUAL_GUARD_CLEANUP_STARTED" -eq 1 ]; then
        return "$VISUAL_GUARD_CLEANUP_STATUS"
    fi

    VISUAL_GUARD_PHASE="CLEANING"
    VISUAL_GUARD_CLEANUP_STARTED=1
    VISUAL_GUARD_CLEANUP_EFFECTIVE_COUNT=$((VISUAL_GUARD_CLEANUP_EFFECTIVE_COUNT + 1))
    VISUAL_GUARD_ORIGINAL_COMMAND_STATUS="$original_status"

    if ! visual_guard_cleanup_process_once; then
        cleanup_status=1
    fi

    if declare -F visual_guard_cleanup_test_hook >/dev/null; then
        visual_guard_cleanup_test_hook "before_restore"
    fi
    if [ "$VISUAL_GUARD_BASELINE_READY" -eq 1 ] &&
       [ "$VISUAL_GUARD_RESTORE_ON_SUCCESS" -eq 0 ] &&
       [ "$original_status" -eq 0 ] &&
       [ "$cleanup_status" -eq 0 ] &&
       [ "$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS" -eq 0 ] &&
       [ "$VISUAL_GUARD_OBSERVATION_COMPLETED" -eq 1 ] &&
       [ "$VISUAL_GUARD_LANE_VERDICT" = "PASS" ]; then
        if discard_capture_asset_baseline "$VISUAL_GUARD_BASELINE_ROOT"; then
            VISUAL_GUARD_RESTORE_RESULT="COMMITTED_SUCCESS_SNAPSHOT_DISCARDED"
        else
            VISUAL_GUARD_RESTORE_RESULT="SNAPSHOT_DISCARD_FAILED"
            cleanup_status=1
        fi
    elif [ "$VISUAL_GUARD_BASELINE_READY" -eq 1 ]; then
        visual_guard_prepare_interrupted_mutation_evidence
        VISUAL_GUARD_ASSET_RESTORE_EFFECTIVE_COUNT=$((VISUAL_GUARD_ASSET_RESTORE_EFFECTIVE_COUNT + 1))
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
    if declare -F visual_guard_cleanup_test_hook >/dev/null; then
        visual_guard_cleanup_test_hook "after_restore_before_completed"
    fi
    visual_guard_run_finalization_test_hook "before_cleanup_completed"

    VISUAL_GUARD_CLEANUP_STATUS="$cleanup_status"
    VISUAL_GUARD_CLEANUP_COMPLETED=1
    visual_guard_run_finalization_test_hook "after_cleanup_completed"
    visual_guard_write_lifecycle_evidence "$original_status"
    return "$cleanup_status"
}

visual_guard_handle_signal() {
    visual_guard_record_signal "$1" "$2"
    case "$VISUAL_GUARD_PHASE" in
        RUNNING)
            visual_guard_request_active_child_stop
            exit "$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS"
            ;;
        CLEANING)
            return 0
            ;;
        FINALIZING|DONE)
            visual_guard_write_lifecycle_evidence \
                "$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS" || true
            exit "$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS"
            ;;
        *)
            exit "$VISUAL_GUARD_FIRST_OBSERVED_SIGNAL_STATUS"
            ;;
    esac
}

visual_guard_finalize_and_exit() {
    local original_status="$1"
    local cleanup_status="$2"
    local final_status

    visual_guard_run_finalization_test_hook "before_finalizing"
    VISUAL_GUARD_PHASE="FINALIZING"
    visual_guard_run_finalization_test_hook "after_finalizing"
    visual_guard_run_finalization_test_hook "before_final_status_snapshot"
    final_status="$(
        visual_guard_calculate_final_status "$original_status" "$cleanup_status"
    )"
    VISUAL_GUARD_FINAL_STATUS_SNAPSHOT="$final_status"
    visual_guard_run_finalization_test_hook "after_final_status_snapshot"
    if [ "$cleanup_status" -ne 0 ]; then
        echo "ERROR: Visual guard cleanup failed (original status: $original_status, cleanup status: $cleanup_status)."
    fi
    visual_guard_write_lifecycle_evidence "$final_status"
    visual_guard_run_finalization_test_hook "before_final_exit"
    if [ "${RUN_TESTS_LIBRARY_ONLY:-0}" -eq 1 ] &&
       [ "${VISUAL_GUARD_TEST_HOOKS_ENABLED:-0}" -eq 1 ] &&
       [ "${VISUAL_GUARD_TEST_RETURN_AFTER_FINALIZE:-0}" -eq 1 ]; then
        VISUAL_GUARD_PHASE="DONE"
        trap - INT TERM
        return "$final_status"
    fi
    VISUAL_GUARD_PHASE="DONE"
    exit "$final_status"
}

visual_guard_handle_exit() {
    local original_status="$1"
    local cleanup_status=0

    VISUAL_GUARD_EXITING=1
    VISUAL_GUARD_ORIGINAL_COMMAND_STATUS="$original_status"
    VISUAL_GUARD_PHASE="CLEANING"
    trap - EXIT
    visual_guard_cleanup "$original_status" || cleanup_status=$?
    visual_guard_finalize_and_exit "$original_status" "$cleanup_status"
}

visual_guard_begin() {
    local baseline_root="$1"
    local mutation_evidence="$2"
    local lifecycle_evidence="$3"
    local lane="$4"
    local asset_path
    local -a guarded_paths

    visual_guard_validate_cleanup_timing_config
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
    visual_guard_snapshot_preexisting_unity_pids
    visual_guard_write_lifecycle_evidence 0
}

visual_guard_run_command() {
    local command_status

    setsid "$@" &
    VISUAL_GUARD_ACTIVE_CHILD_PID=$!
    VISUAL_GUARD_ACTIVE_CHILD_PGID="$VISUAL_GUARD_ACTIVE_CHILD_PID"
    VISUAL_GUARD_OWNED_CHILD_PID="$VISUAL_GUARD_ACTIVE_CHILD_PID"
    VISUAL_GUARD_OWNED_CHILD_PGID="$VISUAL_GUARD_ACTIVE_CHILD_PGID"
    visual_guard_write_lifecycle_evidence 0
    if wait "$VISUAL_GUARD_ACTIVE_CHILD_PID"; then
        command_status=0
    else
        command_status=$?
    fi
    VISUAL_GUARD_ACTIVE_CHILD_PID=""
    VISUAL_GUARD_ACTIVE_CHILD_PGID=""
    return "$command_status"
}

visual_guard_mark_observation_complete() {
    VISUAL_GUARD_OBSERVATION_COMPLETED=1
    VISUAL_GUARD_LANE_VERDICT="$1"
}

visual_guard_finish() {
    local original_status="$1"
    local cleanup_status=0

    VISUAL_GUARD_ORIGINAL_COMMAND_STATUS="$original_status"
    VISUAL_GUARD_PHASE="CLEANING"
    trap - EXIT
    visual_guard_cleanup "$original_status" || cleanup_status=$?
    visual_guard_finalize_and_exit "$original_status" "$cleanup_status"
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

extract_project_path_from_argv() {
    local previous_was_project_path=0
    local argument

    for argument in "$@"; do
        if [ "$previous_was_project_path" -eq 1 ]; then
            printf '%s\n' "$argument"
            return 0
        fi
        if [ "${argument,,}" = "-projectpath" ]; then
            previous_was_project_path=1
        fi
    done
    return 1
}

canonicalize_visual_project_path() {
    local raw_path="$1"
    local normalized_path

    if [ -z "$raw_path" ]; then
        return 1
    fi
    normalized_path="${raw_path//\\//}"
    if [[ "$normalized_path" =~ ^[A-Za-z]:/ ]]; then
        if command -v wslpath >/dev/null 2>&1; then
            normalized_path="$(wslpath -u "$normalized_path" 2>/dev/null || printf '%s' "$normalized_path")"
        fi
    fi
    if [[ "$normalized_path" = /* ]]; then
        normalized_path="$(realpath -m -- "$normalized_path" 2>/dev/null || printf '%s' "$normalized_path")"
    else
        normalized_path="$(realpath -m -- "$PROJECT_PATH_WSL/$normalized_path" 2>/dev/null || printf '%s' "$normalized_path")"
    fi
    if [[ "$normalized_path" =~ ^/mnt/[A-Za-z]/ ]]; then
        normalized_path="${normalized_path,,}"
    fi
    if [ "$normalized_path" != "/" ]; then
        normalized_path="${normalized_path%/}"
    fi
    printf '%s\n' "$normalized_path"
}

visual_project_paths_match() {
    local candidate_canonical
    local expected_canonical

    candidate_canonical="$(canonicalize_visual_project_path "$1")" || return 1
    expected_canonical="$(canonicalize_visual_project_path "$2")" || return 1
    [ "$candidate_canonical" = "$expected_canonical" ]
}

visual_guard_iter_wsl_unity_process_records() {
    local process_dir
    local pid
    local ppid
    local start_identity
    local stat_line
    local executable_name
    local project_path_raw
    local project_path_base64
    local -a argv

    for process_dir in /proc/[0-9]*; do
        pid="${process_dir##*/}"
        if [ ! -r "$process_dir/cmdline" ] || [ ! -r "$process_dir/stat" ]; then
            continue
        fi
        argv=()
        mapfile -d '' -t argv < "$process_dir/cmdline" 2>/dev/null || true
        if [ "${#argv[@]}" -eq 0 ]; then
            continue
        fi
        executable_name="${argv[0]##*[\\/]}"
        case "${executable_name,,}" in
            unity|unity.exe|unityeditor|unityeditor.exe)
                ;;
            *)
                continue
                ;;
        esac
        stat_line="$(sed 's/^[^)]*) //' "$process_dir/stat" 2>/dev/null || true)"
        ppid="$(printf '%s\n' "$stat_line" | awk '{print $2}')"
        start_identity="$(printf '%s\n' "$stat_line" | awk '{print $20}')"
        project_path_raw="$(extract_project_path_from_argv "${argv[@]}" || true)"
        project_path_base64="$(printf '%s' "$project_path_raw" | base64 -w0)"
        printf 'wsl\t%s\t%s\t%s\t%s\n' \
            "$pid" "$ppid" "$start_identity" "$project_path_base64"
    done
}

visual_guard_iter_windows_unity_process_records() {
    if ! command -v powershell.exe >/dev/null 2>&1; then
        return 0
    fi

    powershell.exe -NoProfile -Command '
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class VisualGuardCommandLine {
    [DllImport("shell32.dll", SetLastError = true)]
    public static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string commandLine,
        out int argumentCount);
    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr memory);
}
"@
        function Split-NativeCommandLine([string]$commandLine) {
            if ([string]::IsNullOrWhiteSpace($commandLine)) { return @() }
            $count = 0
            $pointer = [VisualGuardCommandLine]::CommandLineToArgvW($commandLine, [ref]$count)
            if ($pointer -eq [IntPtr]::Zero) { return @() }
            try {
                $arguments = @()
                for ($index = 0; $index -lt $count; $index++) {
                    $item = [Runtime.InteropServices.Marshal]::ReadIntPtr(
                        $pointer,
                        $index * [IntPtr]::Size)
                    $arguments += [Runtime.InteropServices.Marshal]::PtrToStringUni($item)
                }
                return $arguments
            }
            finally {
                [void][VisualGuardCommandLine]::LocalFree($pointer)
            }
        }
        Get-CimInstance Win32_Process |
            Where-Object { $_.Name -ieq "Unity.exe" } |
            ForEach-Object {
                $arguments = @(Split-NativeCommandLine $_.CommandLine)
                $projectPath = ""
                for ($index = 0; $index -lt $arguments.Count; $index++) {
                    if ($arguments[$index] -ieq "-projectPath" -and
                        ($index + 1) -lt $arguments.Count) {
                        $projectPath = $arguments[$index + 1]
                        break
                    }
                }
                $encoded = [Convert]::ToBase64String(
                    [Text.Encoding]::UTF8.GetBytes($projectPath))
                $startIdentity = ""
                if ($_.CreationDate) {
                    $startIdentity = $_.CreationDate.ToUniversalTime().ToString("o")
                }
                "windows`t$($_.ProcessId)`t$($_.ParentProcessId)`t$startIdentity`t$encoded"
            }
    ' 2>/dev/null | tr -d '\r' || true
}

visual_guard_iter_unity_process_records() {
    visual_guard_iter_wsl_unity_process_records
    visual_guard_iter_windows_unity_process_records
}

visual_guard_decode_project_path() {
    local encoded="$1"

    if [ -z "$encoded" ]; then
        return 0
    fi
    printf '%s' "$encoded" | base64 -d 2>/dev/null || true
}

visual_guard_is_preexisting_unity_key() {
    local key="$1"

    if [ -z "$VISUAL_GUARD_PREEXISTING_UNITY_KEYS" ]; then
        return 1
    fi
    printf '%s\n' "$VISUAL_GUARD_PREEXISTING_UNITY_KEYS" | grep -Fx -- "$key" >/dev/null
}

visual_guard_snapshot_preexisting_unity_pids() {
    local source
    local pid
    local ppid
    local start_identity
    local project_path_base64
    local key

    VISUAL_GUARD_PREEXISTING_UNITY_KEYS=""
    while IFS=$'\t' read -r source pid ppid start_identity project_path_base64; do
        if [ -z "$source" ] || [ -z "$pid" ]; then
            continue
        fi
        key="$source:$pid:$start_identity"
        VISUAL_GUARD_PREEXISTING_UNITY_KEYS+="${VISUAL_GUARD_PREEXISTING_UNITY_KEYS:+$'\n'}$key"
    done < <(visual_guard_iter_unity_process_records)
    VISUAL_GUARD_EXPECTED_PROJECT_PATH_CANONICAL="$(
        canonicalize_visual_project_path "$PROJECT_PATH_WIN" || true
    )"
}

visual_guard_record_process_candidate() {
    VISUAL_GUARD_CANDIDATE_SOURCES+=("$1")
    VISUAL_GUARD_CANDIDATE_PIDS+=("$2")
    VISUAL_GUARD_CANDIDATE_START_IDENTITIES+=("$3")
    VISUAL_GUARD_CANDIDATE_PROJECT_PATHS_RAW+=("$4")
    VISUAL_GUARD_CANDIDATE_PROJECT_PATHS_CANONICAL+=("$5")
    VISUAL_GUARD_CANDIDATE_PREEXISTING+=("$6")
    VISUAL_GUARD_CANDIDATE_MATCHES+=("$7")
    VISUAL_GUARD_CANDIDATE_TERMINATION_ATTEMPTED+=("$8")
    VISUAL_GUARD_CANDIDATE_TERMINATION_RESULTS+=("$9")
}

visual_guard_terminate_candidate() {
    local source="$1"
    local pid="$2"
    local attempt

    if [ "$source" = "wsl" ]; then
        if ! kill -0 "$pid" 2>/dev/null; then
            return 0
        fi
        kill -TERM "$pid" 2>/dev/null || true
        for attempt in $(seq 1 20); do
            if ! kill -0 "$pid" 2>/dev/null; then
                return 0
            fi
            sleep 0.1
        done
        kill -KILL "$pid" 2>/dev/null || true
        ! kill -0 "$pid" 2>/dev/null
        return
    fi
    if [ "$source" = "windows" ] &&
       command -v powershell.exe >/dev/null 2>&1; then
        powershell.exe -NoProfile -Command '
            param([int]$ProcessId)
            Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
        ' "$pid" >/dev/null 2>&1 || true
        return 0
    fi
    return 1
}

visual_guard_terminate_fallback_candidates() {
    local source
    local pid
    local ppid
    local start_identity
    local project_path_base64
    local project_path_raw
    local project_path_canonical
    local preexisting
    local match
    local attempted
    local result
    local key

    VISUAL_GUARD_FALLBACK_SCAN_PERFORMED=1
    VISUAL_GUARD_FALLBACK_SCAN_PASSES=$((VISUAL_GUARD_FALLBACK_SCAN_PASSES + 1))
    VISUAL_GUARD_FALLBACK_LAST_ELIGIBLE_COUNT=0
    while IFS=$'\t' read -r source pid ppid start_identity project_path_base64; do
        if [ -z "$source" ] || [ -z "$pid" ]; then
            continue
        fi
        project_path_raw="$(visual_guard_decode_project_path "$project_path_base64")"
        project_path_canonical="$(
            canonicalize_visual_project_path "$project_path_raw" || true
        )"
        key="$source:$pid:$start_identity"
        preexisting=0
        match=0
        attempted=0
        result="NOT_ATTEMPTED"
        if visual_guard_is_preexisting_unity_key "$key"; then
            preexisting=1
        fi
        if [ -n "$project_path_canonical" ] &&
           [ "$project_path_canonical" = "$VISUAL_GUARD_EXPECTED_PROJECT_PATH_CANONICAL" ]; then
            match=1
        fi
        if [ "$match" -eq 1 ] && [ "$preexisting" -eq 0 ]; then
            VISUAL_GUARD_FALLBACK_USED=1
            VISUAL_GUARD_FALLBACK_LAST_ELIGIBLE_COUNT=$((VISUAL_GUARD_FALLBACK_LAST_ELIGIBLE_COUNT + 1))
            VISUAL_GUARD_LAST_CANDIDATE_SEEN_AT_MS="$VISUAL_GUARD_CURRENT_SCAN_AT_MS"
            if [ -n "$VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS" ] &&
               [ "$VISUAL_GUARD_CURRENT_SCAN_AT_MS" -gt \
                 "$VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS" ]; then
                VISUAL_GUARD_LATE_CANDIDATE_DETECTED=1
            fi
            if [ -z "$VISUAL_GUARD_FALLBACK_SEEN_KEYS" ] ||
               ! printf '%s\n' "$VISUAL_GUARD_FALLBACK_SEEN_KEYS" |
                   grep -Fx -- "$key" >/dev/null; then
                VISUAL_GUARD_FALLBACK_SEEN_KEYS+="$(
                    printf '%s%s' \
                        "${VISUAL_GUARD_FALLBACK_SEEN_KEYS:+$'\n'}" \
                        "$key"
                )"
                VISUAL_GUARD_FALLBACK_CANDIDATE_COUNT=$((VISUAL_GUARD_FALLBACK_CANDIDATE_COUNT + 1))
            fi
            attempted=1
            VISUAL_GUARD_FALLBACK_TERMINATION_COUNT=$((VISUAL_GUARD_FALLBACK_TERMINATION_COUNT + 1))
            if visual_guard_terminate_candidate "$source" "$pid"; then
                result="TERMINATED_OR_ALREADY_EXITED"
                VISUAL_GUARD_LAST_CANDIDATE_TERMINATED_AT_MS="$(
                    visual_guard_monotonic_ms
                )"
            else
                result="FAILED"
            fi
        fi
        visual_guard_record_process_candidate \
            "$source" \
            "$pid" \
            "$start_identity" \
            "$project_path_raw" \
            "$project_path_canonical" \
            "$preexisting" \
            "$match" \
            "$attempted" \
            "$result"
    done < <(visual_guard_iter_unity_process_records)
}

visual_guard_find_owned_remaining_unity_processes() {
    local source
    local pid
    local ppid
    local start_identity
    local project_path_base64
    local project_path_raw
    local project_path_canonical

    while IFS=$'\t' read -r source pid ppid start_identity project_path_base64; do
        if [ -z "$source" ] || [ -z "$pid" ] ||
           visual_guard_is_preexisting_unity_key "$source:$pid:$start_identity"; then
            continue
        fi
        project_path_raw="$(visual_guard_decode_project_path "$project_path_base64")"
        project_path_canonical="$(
            canonicalize_visual_project_path "$project_path_raw" || true
        )"
        if [ -n "$project_path_canonical" ] &&
           [ "$project_path_canonical" = "$VISUAL_GUARD_EXPECTED_PROJECT_PATH_CANONICAL" ]; then
            printf '%s:%s:%s\n' "$source" "$pid" "$start_identity"
        fi
    done < <(visual_guard_iter_unity_process_records)
}

visual_guard_update_final_survivors() {
    local survivor
    local source
    local pid
    local start_identity

    VISUAL_GUARD_FINAL_SURVIVOR_COUNT=0
    VISUAL_GUARD_FINAL_SURVIVOR_PIDS=""
    while IFS=: read -r source pid start_identity; do
        if [ -z "$source" ] || [ -z "$pid" ]; then
            continue
        fi
        VISUAL_GUARD_FINAL_SURVIVOR_COUNT=$((VISUAL_GUARD_FINAL_SURVIVOR_COUNT + 1))
        survivor="$source:$pid"
        VISUAL_GUARD_FINAL_SURVIVOR_PIDS+="$(
            printf '%s%s' \
                "${VISUAL_GUARD_FINAL_SURVIVOR_PIDS:+,}" \
                "$survivor"
        )"
    done < <(visual_guard_find_owned_remaining_unity_processes)
}

visual_guard_poll_fallback_candidates() {
    local pass=0
    local now_ms
    local after_scan_ms
    local next_wake_ms
    local wait_ms

    VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS="$VISUAL_GUARD_PRIMARY_TERMINATED_AT_MS"
    if [ -z "$VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS" ]; then
        VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS="$(visual_guard_monotonic_ms)"
    fi
    VISUAL_GUARD_STARTUP_GRACE_DEADLINE_MS="$((
        10#$VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS +
        10#$UNITY_DETACHED_STARTUP_GRACE_MS
    ))"
    VISUAL_GUARD_HARD_CLEANUP_DEADLINE_MS="$((
        10#$VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS +
        10#$UNITY_DETACHED_HARD_TIMEOUT_MS
    ))"

    while :; do
        now_ms="$(visual_guard_monotonic_ms)"
        if [ "$now_ms" -ge "$VISUAL_GUARD_HARD_CLEANUP_DEADLINE_MS" ]; then
            VISUAL_GUARD_FALLBACK_TIMEOUT=1
            VISUAL_GUARD_HARD_TIMEOUT_REACHED=1
            break
        fi

        pass=$((pass + 1))
        VISUAL_GUARD_CURRENT_SCAN_AT_MS="$now_ms"
        if declare -F visual_guard_fallback_scan_test_hook >/dev/null; then
            visual_guard_fallback_scan_test_hook "$pass" "before_scan"
        fi
        visual_guard_terminate_fallback_candidates
        if declare -F visual_guard_fallback_scan_test_hook >/dev/null; then
            visual_guard_fallback_scan_test_hook "$pass" "after_scan"
        fi

        if [ "$VISUAL_GUARD_FALLBACK_LAST_ELIGIBLE_COUNT" -eq 0 ]; then
            VISUAL_GUARD_FALLBACK_EMPTY_STREAK=$((VISUAL_GUARD_FALLBACK_EMPTY_STREAK + 1))
        else
            VISUAL_GUARD_FALLBACK_EMPTY_STREAK=0
            VISUAL_GUARD_POST_GRACE_QUIET_STREAK=0
            VISUAL_GUARD_QUIET_PERIOD_COMPLETED=0
            if [ -n "$VISUAL_GUARD_LAST_CANDIDATE_TERMINATED_AT_MS" ]; then
                VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS="$VISUAL_GUARD_LAST_CANDIDATE_TERMINATED_AT_MS"
                VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS="$((
                    10#$VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS +
                    10#$UNITY_DETACHED_QUIET_PERIOD_MS
                ))"
            else
                VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS=""
                VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS=""
            fi
        fi

        after_scan_ms="$(visual_guard_monotonic_ms)"
        VISUAL_GUARD_STARTUP_GRACE_ELAPSED_MS="$((
            10#$after_scan_ms -
            10#$VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS
        ))"
        if [ "$after_scan_ms" -ge "$VISUAL_GUARD_STARTUP_GRACE_DEADLINE_MS" ]; then
            VISUAL_GUARD_STARTUP_GRACE_COMPLETED=1
        elif [ "$VISUAL_GUARD_FALLBACK_LAST_ELIGIBLE_COUNT" -eq 0 ]; then
            VISUAL_GUARD_EARLY_EMPTY_STREAK=$((VISUAL_GUARD_EARLY_EMPTY_STREAK + 1))
        else
            VISUAL_GUARD_EARLY_EMPTY_STREAK=0
        fi

        if [ "$VISUAL_GUARD_STARTUP_GRACE_COMPLETED" -eq 1 ] &&
           [ "$VISUAL_GUARD_FALLBACK_LAST_ELIGIBLE_COUNT" -eq 0 ]; then
            VISUAL_GUARD_POST_GRACE_QUIET_STREAK=$((VISUAL_GUARD_POST_GRACE_QUIET_STREAK + 1))
            if [ -z "$VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS" ]; then
                VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS="$after_scan_ms"
                VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS="$((
                    10#$VISUAL_GUARD_QUIET_PERIOD_STARTED_AT_MS +
                    10#$UNITY_DETACHED_QUIET_PERIOD_MS
                ))"
            fi
            if [ "$after_scan_ms" -ge "$VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS" ]; then
                VISUAL_GUARD_QUIET_PERIOD_COMPLETED=1
                break
            fi
        fi

        if [ "$after_scan_ms" -ge "$VISUAL_GUARD_HARD_CLEANUP_DEADLINE_MS" ]; then
            VISUAL_GUARD_FALLBACK_TIMEOUT=1
            VISUAL_GUARD_HARD_TIMEOUT_REACHED=1
            break
        fi

        next_wake_ms="$((after_scan_ms + UNITY_DETACHED_POLL_INTERVAL_MS))"
        if [ "$VISUAL_GUARD_STARTUP_GRACE_COMPLETED" -eq 0 ] &&
           [ "$VISUAL_GUARD_STARTUP_GRACE_DEADLINE_MS" -lt "$next_wake_ms" ]; then
            next_wake_ms="$VISUAL_GUARD_STARTUP_GRACE_DEADLINE_MS"
        fi
        if [ "$VISUAL_GUARD_STARTUP_GRACE_COMPLETED" -eq 1 ] &&
           [ -n "$VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS" ] &&
           [ "$VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS" -lt "$next_wake_ms" ]; then
            next_wake_ms="$VISUAL_GUARD_QUIET_PERIOD_DEADLINE_MS"
        fi
        if [ "$VISUAL_GUARD_HARD_CLEANUP_DEADLINE_MS" -lt "$next_wake_ms" ]; then
            next_wake_ms="$VISUAL_GUARD_HARD_CLEANUP_DEADLINE_MS"
        fi
        wait_ms="$((next_wake_ms - after_scan_ms))"
        visual_guard_sleep_ms "$wait_ms" || true
    done

    now_ms="$(visual_guard_monotonic_ms)"
    VISUAL_GUARD_STARTUP_GRACE_ELAPSED_MS="$((
        10#$now_ms -
        10#$VISUAL_GUARD_STARTUP_GRACE_STARTED_AT_MS
    ))"
    visual_guard_update_final_survivors
    if [ "$VISUAL_GUARD_FINAL_SURVIVOR_COUNT" -ne 0 ]; then
        VISUAL_GUARD_FALLBACK_FAILURE_REASON="ELIGIBLE_SURVIVOR_AFTER_FINAL_INVENTORY"
        return 1
    fi
    if [ "$VISUAL_GUARD_STARTUP_GRACE_COMPLETED" -ne 1 ]; then
        VISUAL_GUARD_FALLBACK_FAILURE_REASON="STARTUP_GRACE_NOT_COMPLETED"
        return 1
    fi
    if [ "$VISUAL_GUARD_QUIET_PERIOD_COMPLETED" -ne 1 ]; then
        VISUAL_GUARD_FALLBACK_FAILURE_REASON="POST_GRACE_QUIET_PERIOD_NOT_COMPLETED"
        return 1
    fi
    VISUAL_GUARD_FALLBACK_FAILURE_REASON=""
    return 0
}

find_current_project_unity_processes() {
    local source
    local pid
    local ppid
    local start_identity
    local project_path_base64
    local project_path_raw

    while IFS=$'\t' read -r source pid ppid start_identity project_path_base64; do
        project_path_raw="$(visual_guard_decode_project_path "$project_path_base64")"
        if visual_project_paths_match "$project_path_raw" "$PROJECT_PATH_WIN"; then
            printf '%s %s %s projectPath=%s\n' "$pid" "$ppid" "$source" "$project_path_raw"
        fi
    done < <(visual_guard_iter_unity_process_records)
}

find_current_project_windows_unity_processes() {
    find_current_project_unity_processes | awk '$3 == "windows"'
}

current_project_unity_pids() {
    local source
    local pid
    local ppid
    local start_identity
    local project_path_base64
    local project_path_raw

    while IFS=$'\t' read -r source pid ppid start_identity project_path_base64; do
        if [ "$source" != "wsl" ]; then
            continue
        fi
        project_path_raw="$(visual_guard_decode_project_path "$project_path_base64")"
        if visual_project_paths_match "$project_path_raw" "$PROJECT_PATH_WIN"; then
            printf '%s\n' "$pid"
        fi
    done < <(visual_guard_iter_unity_process_records)
}

current_project_windows_unity_pids() {
    local source
    local pid
    local ppid
    local start_identity
    local project_path_base64
    local project_path_raw

    while IFS=$'\t' read -r source pid ppid start_identity project_path_base64; do
        if [ "$source" != "windows" ]; then
            continue
        fi
        project_path_raw="$(visual_guard_decode_project_path "$project_path_base64")"
        if visual_project_paths_match "$project_path_raw" "$PROJECT_PATH_WIN"; then
            printf '%s\n' "$pid"
        fi
    done < <(visual_guard_iter_unity_process_records)
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

normalize_glyph_serialized_output() {
    local asset_path

    for asset_path in "$CLIMATE_SDF_ASSET" "$NANUM_SDF_ASSET"; do
        if ! perl -pi -e 's/[ \t]+(?=\r?$)//' -- \
            "$PROJECT_PATH_WSL/$asset_path"; then
            echo "ERROR: Failed to normalize serialized whitespace: $asset_path"
            return 1
        fi
    done
}

run_climate_glyph_update() {
    local log_path_win
    local baseline_root
    local climate_before_hash
    local climate_after_hash
    local nanum_before_hash
    local nanum_after_hash
    local command_status=0
    local validation_status=0
    local process_cleanup_status=0
    local -a unity_command

    log_path_win="$(wslpath -w "$CLIMATE_GLYPH_UPDATE_LOG")"
    unity_command=(
        timeout --kill-after=10 300
        "$UNITY_PATH"
        -batchmode
        -nographics
        -projectPath "$PROJECT_PATH_WIN"
        -logFile "$log_path_win"
        -executeMethod "$CLIMATE_GLYPH_UPDATE_EXECUTE_METHOD"
    )

    if [ "$DRY_RUN" -eq 1 ]; then
        echo "Would atomically update the canonical Climate and retained Nanum managed glyph corpus:"
        echo "  preflight: immutable source/GUID/localID/String Table/projectPath identity"
        echo "  snapshot:  $CLIMATE_SDF_ASSET"
        echo "  snapshot:  $NANUM_SDF_ASSET"
        echo "  restore:   command failure, timeout, INT, TERM, process cleanup failure, validation failure"
        print_shell_command "${unity_command[@]}"
        return 0
    fi

    ensure_no_current_project_unity_process
    ensure_no_current_project_unity_lock
    CAPTURE_GUARD_PROFILE="glyph-update"
    baseline_root="$(mktemp -d "$RESULT_DIR/.glyph-update-baseline.XXXXXX")"
    prepare_capture_asset_baseline "$baseline_root"
    climate_before_hash="$(
        sha256sum "$baseline_root/$CLIMATE_SDF_ASSET" | awk '{print $1}'
    )"
    nanum_before_hash="$(
        sha256sum "$baseline_root/$NANUM_SDF_ASSET" | awk '{print $1}'
    )"
    rm -f "$CLIMATE_GLYPH_UPDATE_LOG" \
        "$CLIMATE_GLYPH_UPDATE_EVIDENCE" \
        "$CLIMATE_GLYPH_UPDATE_LIFECYCLE"
    visual_guard_begin \
        "$baseline_root" \
        "$CLIMATE_GLYPH_UPDATE_EVIDENCE" \
        "$CLIMATE_GLYPH_UPDATE_LIFECYCLE" \
        "ClimateGlyphUpdate"

    echo "Running atomic Climate/Nanum managed glyph update..."
    if visual_guard_run_command "${unity_command[@]}"; then
        command_status=0
    else
        command_status=$?
    fi
    if visual_guard_cleanup_process_once; then
        process_cleanup_status=0
    else
        process_cleanup_status=$?
    fi
    if [ "$command_status" -ne 0 ]; then
        echo "ERROR: Unity glyph update failed with exit code $command_status; restoring both font assets."
        visual_guard_mark_observation_complete "FAIL"
        visual_guard_finish "$command_status"
        return 0
    fi
    if [ "$process_cleanup_status" -ne 0 ]; then
        echo "ERROR: Unity glyph update process cleanup failed; restoring both font assets."
        visual_guard_mark_observation_complete "FAIL"
        visual_guard_finish 1
        return 0
    fi
    if ! normalize_glyph_serialized_output; then
        validation_status=1
    fi

    climate_after_hash="$(climate_working_sha256)"
    nanum_after_hash="$(
        sha256sum "$PROJECT_PATH_WSL/$NANUM_SDF_ASSET" | awk '{print $1}'
    )"
    require_worktree_file_text \
        "$CLIMATE_SDF_ASSET" \
        "--- !u!21 &$CLIMATE_MATERIAL_LOCAL_ID" \
        "material localID after glyph update" || validation_status=1
    require_worktree_file_text \
        "$CLIMATE_SDF_ASSET" \
        "--- !u!28 &$CLIMATE_ATLAS_LOCAL_ID" \
        "atlas localID after glyph update" || validation_status=1
    require_worktree_file_text \
        "$NANUM_SDF_ASSET" \
        "--- !u!21 &$NANUM_MATERIAL_LOCAL_ID" \
        "Nanum material localID after glyph update" || validation_status=1
    require_worktree_file_text \
        "$NANUM_SDF_ASSET" \
        "--- !u!28 &$NANUM_ATLAS_LOCAL_ID" \
        "Nanum atlas localID after glyph update" || validation_status=1
    if ! grep -F \
        "GLYPH_UPDATE_VALIDATION missing=0 fallback=0 glyph_loss=0 glyph_remap=0 atlas_page_drift=0 source_linkage=PASS scale_ratio=PASS" \
        "$CLIMATE_GLYPH_UPDATE_LOG" >/dev/null; then
        echo "ERROR: Unity glyph update log is missing the complete post-update validation marker."
        validation_status=1
    fi
    {
        echo "snapshot_target_climate=$CLIMATE_SDF_ASSET"
        echo "snapshot_target_nanum=$NANUM_SDF_ASSET"
        echo "climate_before_sha256=$climate_before_hash"
        echo "climate_after_sha256=$climate_after_hash"
        echo "nanum_before_sha256=$nanum_before_hash"
        echo "nanum_after_sha256=$nanum_after_hash"
        echo "missing=0"
        echo "fallback=0"
        echo "process_survivor_count=$VISUAL_GUARD_FINAL_SURVIVOR_COUNT"
    } > "$CLIMATE_GLYPH_UPDATE_EVIDENCE"

    if [ "$validation_status" -ne 0 ]; then
        echo "ERROR: Glyph output validation failed; restoring both font assets."
        visual_guard_mark_observation_complete "FAIL"
        visual_guard_finish 1
        return 0
    fi

    VISUAL_GUARD_RESTORE_ON_SUCCESS=0
    visual_guard_mark_observation_complete "PASS"
    echo "Climate/Nanum managed glyph update: PASS"
    echo "  Climate before: $climate_before_hash"
    echo "  Climate after:  $climate_after_hash"
    echo "  Nanum before:   $nanum_before_hash"
    echo "  Nanum after:    $nanum_after_hash"
    echo "  missing: 0"
    echo "  fallback: 0"
    visual_guard_finish 0
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

verify_terminal_result_visual_manifest() {
    local output_dir="$1"
    local manifest="$2"
    local expected_head="$3"

    python3 - "$output_dir" "$manifest" "$expected_head" <<'PY'
import hashlib
import re
import sys
from pathlib import Path

output_dir = Path(sys.argv[1])
manifest = Path(sys.argv[2])
expected_head = sys.argv[3]
if not manifest.is_file():
    raise SystemExit(f"ERROR: Missing terminal-result manifest: {manifest}")
text = manifest.read_text(encoding="utf-8")
required = {
    "capture_target_implementation_sha": expected_head,
    "canonical_count": "6",
    "diagnostic_count": "2",
    "stage_result_title_pixel_proof_count": "2",
    "stage_result_title_pixel_proof_pass_count": "2",
    "error_count": "0",
}
for key, expected in required.items():
    match = re.search(rf"^{re.escape(key)}=(.*)$", text, re.MULTILINE)
    if not match or match.group(1).strip() != expected:
        raise SystemExit(f"ERROR: {key} expected {expected!r}")
if len(re.findall(r"^title_pixel_proof=PASS$", text, re.MULTILINE)) != 2:
    raise SystemExit("ERROR: expected two StageResult title pixel proofs")
if len(re.findall(r"^title_pixel_changed_count=[1-9][0-9]*$", text, re.MULTILINE)) != 2:
    raise SystemExit("ERROR: StageResult title pixel delta must be non-zero")

canonical = sorted(
    path for path in output_dir.glob("*.png")
    if path.is_file()
)
diagnostic = sorted(
    path for path in (output_dir / "Diagnostics").glob("*.png")
    if path.is_file()
)
if len(canonical) != 6 or len(diagnostic) != 2:
    raise SystemExit(
        f"ERROR: terminal-result PNG count mismatch: canonical={len(canonical)} diagnostic={len(diagnostic)}"
    )
for path in canonical + diagnostic:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if f"png_sha256={digest}" not in text or f"png_byte_count={len(data)}" not in text:
        raise SystemExit(f"ERROR: manifest identity mismatch for {path}")
print("Terminal result visual manifest validation: PASS")
PY
}

run_terminal_result_visual() {
    local timestamp
    local output_dir
    local output_dir_win
    local unity_log
    local unity_log_win
    local manifest
    local baseline_root
    local runner_mutation_evidence
    local runner_lifecycle_evidence
    local expected_head
    local climate_before
    local climate_observed
    local climate_restored
    local nanum_before
    local nanum_observed
    local nanum_restored
    local command_status=0
    local process_cleanup_status=0
    local capture_guard_status=0
    local restore_status=0
    local manifest_status=0
    local -a unity_command

    timestamp="$(date +%Y%m%d-%H%M%S)"
    output_dir="$TERMINAL_RESULT_VISUAL_OUTPUT_ROOT/CommandLine-$timestamp"
    output_dir_win="$(wslpath -w "$output_dir")"
    unity_log="$output_dir/terminal-result-unity.log"
    unity_log_win="$(wslpath -w "$unity_log")"
    manifest="$output_dir/terminal-result-capture.log"
    baseline_root="$output_dir/pre-capture-assets"
    runner_mutation_evidence="$output_dir/runner-asset-mutation.log"
    runner_lifecycle_evidence="$output_dir/runner-cleanup-lifecycle.log"
    unity_command=(
        timeout --kill-after=10 600
        "$UNITY_PATH"
        -batchmode
        -quit
        -projectPath "$PROJECT_PATH_WIN"
        -logFile "$unity_log_win"
        -executeMethod "$TERMINAL_RESULT_VISUAL_EXECUTE_METHOD"
        -terminalResultVisualOutput "$output_dir_win"
        -terminalResultVisualWidth "$TERMINAL_RESULT_VISUAL_WIDTH"
        -terminalResultVisualHeight "$TERMINAL_RESULT_VISUAL_HEIGHT"
    )

    if [ "$DRY_RUN" -eq 1 ]; then
        echo "Would capture terminal-result production composition:"
        echo "  canonical: StageResult/LevelFailed/GameClear x en-US/ko-KR at 1920x1080"
        echo "  diagnostic: LevelFailed x en-US/ko-KR at 960x540 under Diagnostics/"
        echo "  manifest: canonical and diagnostic classifications remain separate"
        print_shell_command "${unity_command[@]}"
        return 0
    fi

    verify_typography_visual_revision_gate
    ensure_no_current_project_unity_process
    ensure_no_current_project_unity_lock
    mkdir -p "$TERMINAL_RESULT_VISUAL_OUTPUT_ROOT"
    if ! mkdir "$output_dir"; then
        echo "ERROR: Terminal result visual output directory already exists: $output_dir"
        return 1
    fi

    expected_head="$(git rev-parse HEAD)"
    CAPTURE_GUARD_PROFILE="visual"
    prepare_capture_asset_baseline "$baseline_root"
    climate_before="$(sha256sum "$baseline_root/$CLIMATE_SDF_ASSET" | awk '{print $1}')"
    nanum_before="$(sha256sum "$baseline_root/$NANUM_SDF_ASSET" | awk '{print $1}')"
    visual_guard_begin \
        "$baseline_root" \
        "$runner_mutation_evidence" \
        "$runner_lifecycle_evidence" \
        "TerminalResultVisual"

    echo "Running terminal result visual evidence capture..."
    if visual_guard_run_command "${unity_command[@]}"; then
        command_status=0
    else
        command_status=$?
    fi
    if visual_guard_cleanup_process_once; then
        process_cleanup_status=0
    else
        process_cleanup_status=$?
    fi
    climate_observed="$(climate_working_sha256)"
    nanum_observed="$(sha256sum "$PROJECT_PATH_WSL/$NANUM_SDF_ASSET" | awk '{print $1}')"
    if ! observe_capture_assets_before_restore \
        "$baseline_root" \
        "$runner_mutation_evidence"; then
        capture_guard_status=1
    fi
    if [ "$command_status" -eq 0 ] &&
       [ "$process_cleanup_status" -eq 0 ] &&
       [ "$capture_guard_status" -eq 0 ]; then
        visual_guard_mark_observation_complete "PASS"
    else
        visual_guard_mark_observation_complete "FAIL"
    fi
    if ! visual_guard_cleanup "$command_status"; then
        restore_status=1
    fi
    climate_restored="$(climate_working_sha256)"
    nanum_restored="$(sha256sum "$PROJECT_PATH_WSL/$NANUM_SDF_ASSET" | awk '{print $1}')"
    if [ "$climate_restored" != "$climate_before" ] ||
       [ "$nanum_restored" != "$nanum_before" ]; then
        echo "ERROR: Terminal result visual runner did not restore guarded font assets."
        restore_status=1
    fi
    if [ "$command_status" -eq 0 ] &&
       [ "$process_cleanup_status" -eq 0 ] &&
       [ "$capture_guard_status" -eq 0 ] &&
       [ "$restore_status" -eq 0 ]; then
        verify_terminal_result_visual_manifest \
            "$output_dir" \
            "$manifest" \
            "$expected_head" || manifest_status=1
    fi
    if [ -f "$manifest" ]; then
        {
            echo
            echo "[runner-safety]"
            echo "guarded_climate_before_sha256=$climate_before"
            echo "guarded_climate_after_capture_sha256=$climate_observed"
            echo "guarded_climate_restored_sha256=$climate_restored"
            echo "guarded_nanum_before_sha256=$nanum_before"
            echo "guarded_nanum_after_capture_sha256=$nanum_observed"
            echo "guarded_nanum_restored_sha256=$nanum_restored"
            echo "process_survivor_count=$VISUAL_GUARD_FINAL_SURVIVOR_COUNT"
            echo "runner_cleanup_lifecycle=runner-cleanup-lifecycle.log"
        } >> "$manifest"
    fi

    if [ "$command_status" -ne 0 ]; then
        echo "ERROR: Terminal result visual Unity capture failed with exit code $command_status."
        visual_guard_finish "$command_status"
        return 0
    fi
    if [ "$process_cleanup_status" -ne 0 ] ||
       [ "$capture_guard_status" -ne 0 ] ||
       [ "$restore_status" -ne 0 ] ||
       [ "$manifest_status" -ne 0 ]; then
        echo "ERROR: Terminal result visual runner safety or manifest validation failed."
        visual_guard_finish 1
        return 0
    fi

    echo "Terminal result visual evidence capture: PASS"
    echo "  output directory: $output_dir"
    echo "  manifest:         $manifest"
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

run_m1a_hud_guide_visual() {
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
    local expected_tree
    local runner_mutation_evidence
    local runner_lifecycle_evidence
    local climate_hash_before
    local climate_hash_after
    local climate_restored_hash
    local process_before
    local unity_exit=0
    local capture_guard_exit=0
    local restore_exit=0
    local manifest_exit=0
    local -a unity_command

    timestamp="$(date +%Y%m%d-%H%M%S)"
    output_dir="$M1A_HUD_GUIDE_VISUAL_OUTPUT_ROOT/CommandLine-$timestamp"
    unity_log="$output_dir/m1a-hud-guide-unity.log"
    test_results="$output_dir/m1a-hud-guide-playmode.xml"
    manifest="$output_dir/m1a-hud-guide-capture.log"
    runner_mutation_evidence="$output_dir/runner-asset-mutation.log"
    runner_lifecycle_evidence="$output_dir/runner-cleanup-lifecycle.log"
    output_dir_win="$(wslpath -w "$output_dir")"
    baseline_root="$output_dir/pre-capture-assets"
    baseline_root_win="$(wslpath -w "$baseline_root")"
    unity_log_win="$(wslpath -w "$unity_log")"
    test_results_win="$(wslpath -w "$test_results")"
    expected_head="$(git rev-parse HEAD)"
    expected_tree="$(git rev-parse 'HEAD^{tree}')"
    unity_command=(
        timeout --kill-after=10 600
        "$UNITY_PATH"
        -projectPath "$PROJECT_PATH_WIN"
        -logFile "$unity_log_win"
        -runTests
        -testPlatform PlayMode
        -testFilter "Game.Feature.Gameplay.Tests.PlayMode.M1aHudGuideVisualEvidencePlayModeTests.CaptureStage0_1HudAndWorldGuideCanonicalEvidence"
        -testResults "$test_results_win"
        -m1aHudGuideVisualOutput "$output_dir_win"
        -m1aHudGuideVisualWidth "$M1A_HUD_GUIDE_VISUAL_WIDTH"
        -m1aHudGuideVisualHeight "$M1A_HUD_GUIDE_VISUAL_HEIGHT"
        -m1aHudGuideVisualHead "$expected_head"
        -m1aHudGuideVisualTree "$expected_tree"
        -captureAssetBaselineRoot "$baseline_root_win"
    )

    if [ "$DRY_RUN" -eq 1 ]; then
        echo "M1A HUD/World Guide visual evidence plan:"
        echo "  output directory: $output_dir"
        echo "  resolution: ${M1A_HUD_GUIDE_VISUAL_WIDTH}x${M1A_HUD_GUIDE_VISUAL_HEIGHT}"
        echo "  revision gate: tracked repository files and Unity inputs must match Git HEAD"
        echo "  canonical captures: HUD en-US/ko-KR and World Guide en-US/ko-KR"
        print_shell_command "${unity_command[@]}"
        return 0
    fi

    verify_typography_visual_revision_gate
    ensure_no_current_project_unity_process
    ensure_no_current_project_unity_lock
    mkdir -p "$M1A_HUD_GUIDE_VISUAL_OUTPUT_ROOT"
    mkdir "$output_dir"
    prepare_capture_asset_baseline "$baseline_root"
    visual_guard_begin \
        "$baseline_root" \
        "$runner_mutation_evidence" \
        "$runner_lifecycle_evidence" \
        "M1aHudGuide"

    climate_hash_before="$(sha256sum "$OBJECTIVE_HUD_VISUAL_CLIMATE_ASSET" | awk '{print $1}')"
    process_before="$(find_current_project_unity_processes)"
    echo "Running M1A HUD/World Guide production-composition visual evidence..."
    echo "  output directory: $output_dir"
    echo "  manifest: $manifest"
    if visual_guard_run_command "${unity_command[@]}"; then
        unity_exit=0
    else
        unity_exit=$?
    fi

    if [ "$unity_exit" -eq 124 ] || [ "$unity_exit" -eq 137 ]; then
        capture_unity_timeout_artifacts \
            "typography-hud-guide-visual" \
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
    if [ "$unity_exit" -eq 0 ] && [ "$capture_guard_exit" -eq 0 ]; then
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
    if [ "$climate_restored_hash" != "$climate_hash_before" ]; then
        echo "ERROR: M1A visual runner did not restore the guarded Climate asset."
        restore_exit=1
    fi

    if [ -f "$manifest" ]; then
        {
            echo
            echo "[runner-safety]"
            echo "guarded_climate_before_sha256=$climate_hash_before"
            echo "guarded_climate_after_capture_sha256=$climate_hash_after"
            echo "guarded_climate_restored_sha256=$climate_restored_hash"
            echo "process_survivor_count=$VISUAL_GUARD_FINAL_SURVIVOR_COUNT"
            echo "asset_restore=$(
                if [ "$restore_exit" -eq 0 ]; then
                    printf 'PASS'
                else
                    printf 'FAIL'
                fi
            )"
            echo "runner_mutation_evidence=runner-asset-mutation.log"
            echo "runner_cleanup_lifecycle=runner-cleanup-lifecycle.log"
        } >> "$manifest"
    fi

    if [ "$unity_exit" -ne 0 ]; then
        echo "ERROR: M1A HUD/World Guide visual capture failed with exit code $unity_exit."
        echo "Diagnostics were preserved in: $output_dir"
        visual_guard_finish "$unity_exit" || return $?
        return 0
    fi
    if [ "$capture_guard_exit" -ne 0 ] || [ "$restore_exit" -ne 0 ]; then
        echo "ERROR: M1A visual runner asset safety checks failed."
        echo "  mutation evidence: $runner_mutation_evidence"
        visual_guard_finish 1 || return $?
        return 0
    fi
    if ! assert_no_generated_test_scenes; then
        cleanup_generated_test_scenes
        visual_guard_finish 1 || return $?
        return 0
    fi

    if ! python3 - \
        "$output_dir" \
        "$manifest" \
        "$expected_head" \
        "$expected_tree" \
        "$PROJECT_PATH_WSL" <<'PY'
import hashlib
import re
import sys
from pathlib import Path

output_dir = Path(sys.argv[1]).resolve()
manifest_path = Path(sys.argv[2]).resolve()
expected_head = sys.argv[3]
expected_tree = sys.argv[4]
expected_worktree = Path(sys.argv[5]).resolve()

if not manifest_path.is_file():
    raise SystemExit(f"ERROR: M1A HUD/World Guide manifest missing: {manifest_path}")

root = {}
sections = {}
current = root
for raw_line in manifest_path.read_text(encoding="utf-8").splitlines():
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
    if key in current:
        raise SystemExit(f"ERROR: duplicate manifest key: {key}")
    current[key] = value

if root.get("schema_version") != "1":
    raise SystemExit("ERROR: M1A manifest schema_version mismatch")
if root.get("git_head") != expected_head or root.get("git_tree") != expected_tree:
    raise SystemExit("ERROR: M1A manifest revision mismatch")
if Path(root.get("worktree_path", "")).resolve() != expected_worktree:
    raise SystemExit("ERROR: M1A manifest worktree mismatch")
if root.get("scene") != "Assets/Scenes/UIAudioScene.unity":
    raise SystemExit("ERROR: M1A manifest scene mismatch")
if root.get("stage_id") != "stage-0-1":
    raise SystemExit("ERROR: M1A manifest stage mismatch")
if not re.fullmatch(r"[1-9][0-9]*x[1-9][0-9]*", root.get("resolution", "")):
    raise SystemExit("ERROR: M1A manifest resolution is invalid")
if root.get("capture_count") != "4" or root.get("locale_runtime_count") != "2":
    raise SystemExit("ERROR: M1A manifest capture/runtime count mismatch")
if root.get("errors") != "0" or root.get("overall_result") != "PASS":
    raise SystemExit("ERROR: M1A manifest did not record a clean PASS")
try:
    pixel_threshold = int(root["pixel_delta_threshold"])
except (KeyError, ValueError):
    raise SystemExit("ERROR: M1A pixel threshold is invalid")
if pixel_threshold < 1:
    raise SystemExit("ERROR: M1A pixel threshold must be positive")

expected_text = {
    "en-US": {
        "pause": "Pause",
        "chance": "CHANCES",
        "movement": "Move",
        "push": "Push",
        "flip": "Flip",
    },
    "ko-KR": {
        "pause": "일시 정지",
        "chance": "기회",
        "movement": "이동",
        "push": "밀기",
        "flip": "뒤집기",
    },
}
if set(sections) != {"en-US", "ko-KR", "runner-safety"}:
    raise SystemExit("ERROR: M1A manifest locale/safety section set mismatch")

hex32 = re.compile(r"[0-9a-f]{32}")
hex64 = re.compile(r"[0-9a-f]{64}")
for locale, expected in expected_text.items():
    entry = sections[locale]
    if entry.get("chance_count") != "2/3":
        raise SystemExit(f"ERROR: {locale} chance fixture mismatch")
    for field in (
        "missing_glyph_count",
        "fallback_count",
        "mixed_locale",
        "stale_locale",
    ):
        if entry.get(field) != "0":
            raise SystemExit(f"ERROR: {locale} {field} is nonzero")
    if entry.get("layout") != "PASS" or entry.get("graphics") != "PASS":
        raise SystemExit(f"ERROR: {locale} layout/graphics failed")
    for identity in ("semantic_fixture_hash", "non_text_graphic_hash"):
        if not hex64.fullmatch(entry.get(identity, "")):
            raise SystemExit(f"ERROR: {locale} {identity} is invalid")
    for prefix in ("hud", "guide"):
        png = output_dir / entry.get(prefix + "_file", "")
        if not png.is_file():
            raise SystemExit(f"ERROR: {locale} {prefix} PNG is missing")
        data = png.read_bytes()
        if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
            raise SystemExit(f"ERROR: {locale} {prefix} is not a PNG")
        if int(entry.get(prefix + "_bytes", "0")) != len(data):
            raise SystemExit(f"ERROR: {locale} {prefix} byte count mismatch")
        if hashlib.sha256(data).hexdigest() != entry.get(prefix + "_sha256"):
            raise SystemExit(f"ERROR: {locale} {prefix} SHA-256 mismatch")
    for target, text in expected.items():
        prefix = f"target_{target}_"
        if not entry.get(prefix + "path"):
            raise SystemExit(f"ERROR: {locale} {target} TMP path is blank")
        if entry.get(prefix + "text") != text:
            raise SystemExit(f"ERROR: {locale} {target} text mismatch")
        if not hex32.fullmatch(entry.get(prefix + "font_guid", "")):
            raise SystemExit(f"ERROR: {locale} {target} font GUID is invalid")
        if not hex32.fullmatch(entry.get(prefix + "material_guid", "")):
            raise SystemExit(f"ERROR: {locale} {target} material GUID is invalid")
        for local_id in ("font_local_id", "material_local_id"):
            if int(entry.get(prefix + local_id, "0")) == 0:
                raise SystemExit(f"ERROR: {locale} {target} {local_id} is zero")
        try:
            bounds = [float(value) for value in entry[prefix + "bounds"].split(",")]
            alpha = float(entry[prefix + "alpha"])
            mesh_characters = int(entry[prefix + "mesh_characters"])
            mesh_vertices = int(entry[prefix + "mesh_vertices"])
            fallback = int(entry[prefix + "fallback"])
            pixel_delta = int(entry[prefix + "pixel_delta"])
        except (KeyError, ValueError):
            raise SystemExit(f"ERROR: {locale} {target} evidence is invalid")
        if len(bounds) != 4 or bounds[2] <= 0 or bounds[3] <= 0:
            raise SystemExit(f"ERROR: {locale} {target} bounds are invalid")
        if alpha <= 0 or mesh_characters <= 0 or mesh_vertices <= 0:
            raise SystemExit(f"ERROR: {locale} {target} raster identity is empty")
        if fallback != 0 or pixel_delta <= pixel_threshold:
            raise SystemExit(f"ERROR: {locale} {target} fallback/pixel proof failed")

english = sections["en-US"]
korean = sections["ko-KR"]
for field in ("movement_keycap", "push_keycap", "flip_keycap"):
    if not english.get(field) or english.get(field) != korean.get(field):
        raise SystemExit(f"ERROR: cross-locale {field} mismatch")
for field in ("semantic_fixture_hash", "non_text_graphic_hash"):
    if english.get(field) != korean.get(field):
        raise SystemExit(f"ERROR: cross-locale {field} mismatch")

safety = sections["runner-safety"]
for field in (
    "guarded_climate_before_sha256",
    "guarded_climate_after_capture_sha256",
    "guarded_climate_restored_sha256",
):
    if not hex64.fullmatch(safety.get(field, "")):
        raise SystemExit(f"ERROR: runner safety {field} is invalid")
if safety["guarded_climate_before_sha256"] != safety["guarded_climate_restored_sha256"]:
    raise SystemExit("ERROR: runner safety Climate restore mismatch")
if safety.get("process_survivor_count") != "0":
    raise SystemExit("ERROR: runner safety found a Unity survivor")
if safety.get("asset_restore") != "PASS":
    raise SystemExit("ERROR: runner safety asset restore failed")

print("M1A HUD/World Guide visual manifest verification: PASS")
PY
    then
        manifest_exit=1
    fi

    if [ "$manifest_exit" -ne 0 ]; then
        echo "ERROR: M1A HUD/World Guide manifest validation failed."
        visual_guard_finish 1 || return $?
        return 0
    fi

    echo "M1A HUD/World Guide visual evidence capture: PASS"
    echo "  output directory: $output_dir"
    echo "  manifest: $manifest"
    echo "  runner mutation: $runner_mutation_evidence"
    echo "  recorded revision: $expected_head"
    echo "  recorded tree: $expected_tree"
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

    if { [ "$RUN_MODE" = "climate-glyph-update" ] ||
         [ "$RUN_MODE" = "typography-visual" ] ||
         [ "$RUN_MODE" = "typography-hud-visual" ] ||
         [ "$RUN_MODE" = "typography-hud-guide-visual" ] ||
         [ "$RUN_MODE" = "typography-result-visual" ]; } &&
       [ -n "$TEST_FILTER" ]; then
        echo "ERROR: asset generation and visual evidence lanes do not accept test filters."
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
           [ "$mode" = "climate-glyph-update" ] ||
           [ "$mode" = "typography-visual" ] ||
           [ "$mode" = "typography-hud-visual" ] ||
           [ "$mode" = "typography-hud-guide-visual" ] ||
           [ "$mode" = "typography-result-visual" ]; then
            require_command git
            require_command sha256sum
            verify_climate_committed_source_integrity
            verify_climate_worktree_source_integrity
        fi
        if [ "$mode" = "climate-glyph-update" ] ||
           [ "$mode" = "typography-visual" ] ||
           [ "$mode" = "typography-hud-visual" ] ||
           [ "$mode" = "typography-hud-guide-visual" ] ||
           [ "$mode" = "typography-result-visual" ]; then
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
        if [ "$mode" = "climate-glyph-update" ] ||
           [ "$mode" = "typography-visual" ] ||
           [ "$mode" = "typography-hud-visual" ] ||
           [ "$mode" = "typography-hud-guide-visual" ] ||
           [ "$mode" = "typography-result-visual" ]; then
            echo "Dry run: asset generation or revision-gated capture will not execute."
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
        climate-glyph-update)
            run_climate_glyph_update
            ;;
        typography-visual)
            run_typography_visual
            ;;
        typography-hud-visual)
            run_objective_hud_visual
            ;;
        typography-hud-guide-visual)
            run_m1a_hud_guide_visual
            ;;
        typography-result-visual)
            run_terminal_result_visual
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
       [ "$mode" != "climate-glyph-update" ] &&
       [ "$mode" != "typography-visual" ] &&
       [ "$mode" != "typography-hud-visual" ] &&
       [ "$mode" != "typography-hud-guide-visual" ] &&
       [ "$mode" != "typography-result-visual" ]; then
        echo "ALL TESTS PASSED"
    fi
}

if [ "${RUN_TESTS_LIBRARY_ONLY:-0}" -eq 0 ]; then
    main "$@"
fi
