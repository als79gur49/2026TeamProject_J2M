#!/usr/bin/env python3
from __future__ import annotations

import csv
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
import xml.etree.ElementTree as ET


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
CONTRACT_PATH = SCRIPT_DIR / "evidence-contract-v1.json"


def default_evidence_root(project_root: Path) -> Path:
    resolved = project_root.resolve()
    if (
        resolved.parent.name.casefold() == "worktrees"
        and resolved.parent.parent.name.casefold() == "j2m"
    ):
        return (
            resolved.parent.parent
            / "evidence/TerminalIrisCoreArtEvidenceIntegrity"
        )
    return (
        resolved.parent
        / "J2M-Evidence/TerminalIrisCoreArtEvidenceIntegrity"
    )


DEFAULT_EVIDENCE_ROOT = default_evidence_root(PROJECT_ROOT)
DEFAULT_BUNDLE_PARENT = DEFAULT_EVIDENCE_ROOT / "Bundles"
EVIDENCE_BUNDLE_ROOT_ENV = "TERMINAL_IRIS_EVIDENCE_BUNDLE_ROOT"

SOURCE_PATHS = [
    "Assets/_Features/UI/UI_Composition/Runtime/GameplayTerminalFocusTargetSource.cs",
    "Assets/_Features/UI/UI_Composition/Runtime/GameplayTerminalTransitionPort.cs",
    "Assets/_Features/UI/UI_Composition/Runtime/TerminalIrisOverlayView.cs",
    "Assets/_Features/UI/UI_Composition/Runtime/TerminalIrisPlayerVisualQualityProbe.cs",
    "Assets/_Features/UI/UI_Composition/Shaders/TerminalIris.shader",
    "Assets/_Features/UI/UI_Composition/Shaders/TerminalIrisOverlay.mat",
    "Assets/_Features/UI/UI_Composition/Runtime/TerminalIrisMotionProfile.cs",
    "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/TerminalIrisMotionProfile.asset",
    "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/ResultTransitionVisualStyle.asset",
    "Assets/_Features/Stages/Runtime/Queries/SceneTransitionRoutePolicy.cs",
    "Assets/_Features/Stages/Runtime/Queries/TerminalTransitionTypes.cs",
    "Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/TerminalIrisEvidenceAnalyzer.cs",
    "Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/TerminalIrisVisualQualityPlayModeTests.cs",
    "Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/ActualSceneBootstrapSmokePlayModeTests.cs",
    "Assets/_Features/Stages/Editor/Tests/SceneTransitionRoutePolicyCatalogTests.cs",
    "Assets/_Features/UI/UI_Tests/EditMode/SceneTransitionRoutePolicyArchitectureTests.cs",
    "Assets/_Features/Stages/Editor/Tests/TerminalSessionAuthorityTests.cs",
    "Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/Gameplay.PlayModeTests.asmdef",
    "Assets/_Features/Stages/Stages.asmdef",
    "Assets/_Features/Stages/Editor/Tests/Game.Feature.Stages.Editor.Tests.asmdef",
    "Assets/_Features/UI/UI_Composition/UI.Composition.asmdef",
    "Assets/_Features/UI/UI_Tests/EditMode/UI.Tests.asmdef",
    "run_tests.sh",
    "Tools/TerminalIrisEvidenceIntegrity/run_bundle.sh",
    "Tools/TerminalIrisEvidenceIntegrity/produce_bundle.py",
    "Tools/TerminalIrisEvidenceIntegrity/verify_bundle.py",
    "Tools/TerminalIrisEvidenceIntegrity/run_negative_tests.py",
    "Tools/TerminalIrisEvidenceIntegrity/test_verify_bundle.py",
    "Tools/TerminalIrisEvidenceIntegrity/test_produce_bundle.py",
    "Tools/TerminalIrisEvidenceIntegrity/evidence-contract-v1.json",
    "Tools/TerminalIrisEvidenceIntegrity/README.md",
    "ProjectSettings/ProjectVersion.txt",
    "ProjectSettings/GraphicsSettings.asset",
    "ProjectSettings/QualitySettings.asset",
    "ProjectSettings/URPProjectSettings.asset",
    "Assets/Settings/PC_RPAsset.asset",
    "Assets/Settings/Mobile_RPAsset.asset",
]

KNOWN_UNITY_IMPORT_DRIFT_PATH = (
    "Assets/_Shared/UI/Fonts/KBODiaGothic-Medium SDF.asset"
)


@dataclass(frozen=True)
class Lane:
    lane_id: str
    command_id: str
    argv: tuple[str, ...]
    kind: str = "unity-test-framework"


def resolve_bundle_parent() -> Path:
    override = os.environ.get(EVIDENCE_BUNDLE_ROOT_ENV, "").strip()
    resolved = (
        Path(override).expanduser().resolve()
        if override
        else DEFAULT_BUNDLE_PARENT.resolve()
    )
    if resolved == PROJECT_ROOT.resolve() or resolved.is_relative_to(
        PROJECT_ROOT.resolve()
    ):
        raise ValueError(
            f"{EVIDENCE_BUNDLE_ROOT_ENV} must resolve outside the repository: "
            f"{resolved}"
        )
    return resolved


def resolve_test_results_root() -> Path:
    explicit = os.environ.get("TEST_RESULTS_ROOT", "").strip()
    if explicit:
        return Path(explicit).expanduser().resolve()
    validation_root = os.environ.get("CODEX_VALIDATION_ROOT", "").strip()
    if validation_root:
        return Path(validation_root).expanduser().resolve() / "test-results"
    return PROJECT_ROOT / "TestResults"


LANES = [
    Lane(
        "legacy-analyzer-regression",
        "CMD-01",
        ("./run_tests.sh", "terminal-iris-legacy-analyzer-regression"),
    ),
    Lane(
        "known-center-analyzer",
        "CMD-02",
        ("./run_tests.sh", "terminal-iris-known-center-analyzer"),
    ),
    Lane(
        "production-offcenter-focus",
        "CMD-03",
        ("./run_tests.sh", "terminal-production-offcenter-focus"),
    ),
    Lane(
        "final-close-frames",
        "CMD-04",
        ("./run_tests.sh", "terminal-iris-final-close-frames"),
    ),
    Lane(
        "architecture",
        "CMD-05",
        (
            "./run_tests.sh",
            "terminal-transition-architecture",
            "--filter",
            "SceneTransitionRoutePolicyCatalogTests;"
            "SceneTransitionRoutePolicyArchitectureTests;"
            "TerminalSessionAuthorityTests",
        ),
    ),
    Lane(
        "lifecycle",
        "CMD-06",
        (
            "./run_tests.sh",
            "terminal-production-render-coverage",
            "--filter",
            "TerminalVictoryBlueHandoff_ActualVictoryCreatesResultAndCleansBlockers;"
            "M2LevelFailedRestart_PreservesSourceScreenUntilOpaqueAndCompletesCanonicalEntry;"
            "TerminalStageEntryOpening_ActualStageResultLoadsNewSceneAndReleasesGameplay",
        ),
    ),
    Lane(
        "static-edge",
        "CMD-07",
        ("./run_tests.sh", "terminal-iris-static-edge-quality"),
    ),
    Lane(
        "small-radius",
        "CMD-08",
        ("./run_tests.sh", "terminal-iris-small-radius"),
    ),
    Lane(
        "temporal-stability",
        "CMD-09",
        ("./run_tests.sh", "terminal-iris-temporal-stability"),
    ),
    Lane(
        "player-visual-quality",
        "CMD-10",
        ("./run_tests.sh", "terminal-iris-player-visual-quality"),
        "player-visual",
    ),
    Lane("core", "CMD-11", ("./run_tests.sh", "core")),
    Lane("ui", "CMD-12", ("./run_tests.sh", "ui")),
]

GRAPHICS_LANES = {
    "legacy-analyzer-regression",
    "known-center-analyzer",
    "production-offcenter-focus",
    "final-close-frames",
    "static-edge",
    "small-radius",
    "temporal-stability",
    "player-visual-quality",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def run_git(*args: str, check: bool = True) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=PROJECT_ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=check,
    )
    return result.stdout.decode("utf-8", errors="replace")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


def source_inventory() -> list[dict[str, object]]:
    inventory: list[dict[str, object]] = []
    for relative in SOURCE_PATHS:
        path = PROJECT_ROOT / relative
        if not path.is_file():
            raise RuntimeError(f"required producer source is missing: {relative}")
        tracked = (
            subprocess.run(
                ["git", "ls-files", "--error-unmatch", "--", relative],
                cwd=PROJECT_ROOT,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            ).returncode
            == 0
        )
        inventory.append(
            {
                "path": relative,
                "state": "tracked" if tracked else "untracked",
                "size": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )
    return inventory


def diff_hash(cached: bool = False) -> str:
    args = ["git", "diff"]
    if cached:
        args.append("--cached")
    args.append("--binary")
    return sha256_bytes(subprocess.check_output(args, cwd=PROJECT_ROOT))


def baseline() -> dict[str, object]:
    upstream = run_git(
        "rev-parse",
        "--abbrev-ref",
        "--symbolic-full-name",
        "@{upstream}",
        check=False,
    ).strip()
    ahead = behind = None
    if upstream:
        values = run_git(
            "rev-list", "--left-right", "--count", f"HEAD...{upstream}"
        ).split()
        ahead, behind = (int(values[0]), int(values[1]))
    porcelain = run_git("status", "--porcelain=v1", "-uall").splitlines()
    diff_check = run_git("diff", "--check", check=False)
    return {
        "repository": str(PROJECT_ROOT),
        "branch": run_git("branch", "--show-current").strip(),
        "head": run_git("rev-parse", "HEAD").strip(),
        "tree": run_git("rev-parse", "HEAD^{tree}").strip(),
        "upstream": upstream or None,
        "ahead": ahead,
        "behind": behind,
        "trackedModified": run_git("diff", "--name-only").splitlines(),
        "staged": run_git("diff", "--cached", "--name-only").splitlines(),
        "untrackedPorcelainEntries": [
            line for line in porcelain if line.startswith("??")
        ],
        "untrackedIndividualPaths": run_git(
            "ls-files", "--others", "--exclude-standard"
        ).splitlines(),
        "gitDiffStat": run_git("diff", "--stat"),
        "trackedDiffSha256": diff_hash(),
        "cachedDiffSha256": diff_hash(cached=True),
        "gitDiffCheck": diff_check,
        "capturedUtc": utc_now(),
    }


def xml_summary(path: Path) -> dict[str, object]:
    try:
        root = ET.parse(path).getroot()
    except Exception as exc:
        return {"path": path.name, "parseError": str(exc)}
    failed = [
        node.attrib.get("fullname", "")
        for node in root.iter("test-case")
        if node.attrib.get("result") == "Failed"
    ]
    return {
        "path": path.name,
        "total": int(root.attrib.get("total", "0")),
        "passed": int(root.attrib.get("passed", "0")),
        "failed": int(root.attrib.get("failed", "0")),
        "skipped": int(
            root.attrib.get("skipped", root.attrib.get("inconclusive", "0"))
        ),
        "failedTestIds": failed,
    }


def copy_fresh(
    source: Path,
    destination: Path,
    command_start_ns: int,
) -> bool:
    if not source.is_file():
        return False
    if source.stat().st_mtime_ns < command_start_ns - 5_000_000_000:
        return False
    shutil.copy2(source, destination)
    return True


def display_origin_path(source: Path) -> str:
    try:
        return str(source.relative_to(PROJECT_ROOT))
    except ValueError:
        return str(source)


def result_origins(lane: Lane) -> list[tuple[Path, str]]:
    root = resolve_test_results_root()
    if lane.lane_id == "architecture":
        return [
            (root / "wsl-dotnet-full.log", "dotnet.log"),
            (root / "wsl-unity-full-editmode.xml", "unity-editmode.xml"),
            (root / "wsl-unity-full-editmode.log", "unity-editmode.log"),
        ]
    if lane.lane_id == "ui":
        return [
            (root / "wsl-dotnet-ui.log", "dotnet.log"),
            (root / "wsl-unity-ui-editmode.xml", "unity-editmode.xml"),
            (root / "wsl-unity-ui-editmode.log", "unity-editmode.log"),
        ]
    if lane.lane_id == "core":
        return [
            (root / "wsl-dotnet-core.log", "dotnet.log"),
            (root / "wsl-unity-core-editmode.xml", "unity-editmode.xml"),
            (root / "wsl-unity-core-editmode.log", "unity-editmode.log"),
            (root / "wsl-unity-core-playmode.xml", "unity-playmode.xml"),
            (root / "wsl-unity-core-playmode.log", "unity-playmode.log"),
        ]
    if lane.kind == "player-visual":
        # This lane owns its build log and result JSON beneath the configured
        # graphics output root.  It must not inherit a TestResults artifact
        # from an earlier lane: the Player runner intentionally does not emit
        # wsl-dotnet-core.log, and treating that unrelated file as required
        # makes an otherwise complete Player matrix impossible to close.
        return []
    return [
        (root / "wsl-dotnet-core.log", "dotnet.log"),
        (root / "wsl-unity-core-playmode.xml", "unity-playmode.xml"),
        (root / "wsl-unity-core-playmode.log", "unity-playmode.log"),
    ]


def unique_match(root: Path, pattern: str) -> Path | None:
    matches = sorted(root.glob(pattern))
    return matches[0] if len(matches) == 1 else None


def parse_unity_log_environment(log_path: Path) -> dict[str, str]:
    text = (
        log_path.read_text(encoding="utf-8", errors="replace")
        if log_path.is_file()
        else ""
    )
    renderer = re.search(r"^\s*Renderer:\s*(.+?)(?:\s+\(ID=.*)?$", text, re.MULTILINE)
    graphics_version = re.search(
        r"^\s*Version:\s*((?:Direct3D|Vulkan|OpenGL|Metal).+?)\s*$",
        text,
        re.MULTILINE,
    )
    driver = re.search(r"^\s*Driver:\s*(.+?)\s*$", text, re.MULTILINE)
    direct = re.search(r"^(Direct3D\d*):\s*$", text, re.MULTILINE)
    direct_version = re.search(
        r"^\s*Version:\s*Direct3D\s*(\d+)", text, re.MULTILINE
    )
    operating_system = re.search(r"^OS:\s*'([^']+)'", text, re.MULTILINE)
    return {
        "gpu": renderer.group(1).strip() if renderer else "",
        "graphicsApi": (
            f"Direct3D{direct_version.group(1)}"
            if direct_version
            else direct.group(1)
            if direct
            else ""
        ),
        "featureLevel": (
            graphics_version.group(1).strip() if graphics_version else ""
        ),
        "driver": driver.group(1).strip() if driver else "",
        "operatingSystem": operating_system.group(1).strip()
        if operating_system
        else "",
    }


def windows_operating_system() -> str:
    try:
        result = subprocess.check_output(
            [
                "powershell.exe",
                "-NoProfile",
                "-Command",
                "[System.Environment]::OSVersion.VersionString",
            ],
            stderr=subprocess.DEVNULL,
        )
        value = result.decode("utf-8", errors="replace").replace("\r", "").strip()
        return value or "Windows"
    except Exception:
        return "Windows"


def write_lane_environment(lane: Lane, lane_root: Path) -> str | None:
    if lane.lane_id not in GRAPHICS_LANES:
        return None
    unity_version = re.search(
        r"^m_EditorVersion:\s*(.+)$",
        (PROJECT_ROOT / "ProjectSettings/ProjectVersion.txt").read_text(
            encoding="utf-8"
        ),
        re.MULTILINE,
    ).group(1)
    if lane.kind == "player-visual":
        player_result = unique_match(lane_root, "**/player-visual-result.json")
        if player_result is None:
            return None
        payload = json.loads(player_result.read_text(encoding="utf-8"))
        player_logs = payload.get("playerLog", [])
        first_log = (
            player_result.parent / player_logs[0] if player_logs else Path()
        )
        parsed = parse_unity_log_environment(first_log)
        environment = {
            "schemaVersion": 1,
            "laneId": lane.lane_id,
            "unityVersion": unity_version,
            "operatingSystem": parsed["operatingSystem"]
            or windows_operating_system(),
            "gpu": payload.get("GPU", ""),
            "graphicsApi": payload.get("graphicsAPI", ""),
            "featureLevel": parsed["featureLevel"],
            "driver": payload.get("driver", "") or parsed["driver"],
            "colorSpace": "Linear",
            "captureResolution": ",".join(payload.get("resolutions", [])),
            "graphicsEnabled": True,
            "sourceEnvironmentPath": str(
                player_result.relative_to(lane_root).as_posix()
            ),
            "unityLogPaths": [
                str((player_result.parent / path).relative_to(lane_root).as_posix())
                for path in player_logs
            ],
        }
    else:
        source_environment = unique_match(lane_root, "**/graphics-environment.json")
        unity_log = lane_root / "unity-playmode.log"
        if not unity_log.is_file():
            return None
        parsed = parse_unity_log_environment(unity_log)
        if source_environment is not None:
            payload = json.loads(
                source_environment.read_text(encoding="utf-8-sig")
            )
            environment = {
                "schemaVersion": 1,
                "laneId": lane.lane_id,
                "unityVersion": payload.get("unityVersion", unity_version),
                "operatingSystem": payload.get("operatingSystem", ""),
                "gpu": payload.get("graphicsDeviceName", ""),
                "graphicsApi": payload.get("graphicsDeviceType", ""),
                "featureLevel": payload.get("graphicsDeviceVersion", ""),
                "driver": parsed["driver"],
                "colorSpace": payload.get("colorSpace", ""),
                "captureResolution": payload.get(
                    "renderTargets", payload.get("screenResolution", "")
                ),
                "graphicsEnabled": True,
                "sourceEnvironmentPath": str(
                    source_environment.relative_to(lane_root).as_posix()
                ),
                "unityLogPaths": ["unity-playmode.log"],
            }
        else:
            quality_manifest = unique_match(lane_root, "**/manifest.txt")
            if quality_manifest is None:
                return None
            manifest_values = {}
            for line in quality_manifest.read_text(
                encoding="utf-8", errors="replace"
            ).splitlines():
                key, separator, value = line.partition("=")
                if separator:
                    manifest_values[key] = value
            environment = {
                "schemaVersion": 1,
                "laneId": lane.lane_id,
                "unityVersion": unity_version,
                "operatingSystem": parsed["operatingSystem"],
                "gpu": parsed["gpu"],
                "graphicsApi": parsed["graphicsApi"],
                "featureLevel": parsed["featureLevel"],
                "driver": parsed["driver"],
                "colorSpace": "Linear",
                "captureResolution": manifest_values.get(
                    "RenderTextureMatrix", ""
                ),
                "graphicsEnabled": (
                    manifest_values.get("GraphicsMode") == "enabled"
                ),
                "sourceEnvironmentPath": str(
                    quality_manifest.relative_to(lane_root).as_posix()
                ),
                "unityLogPaths": ["unity-playmode.log"],
            }
    path = lane_root / "lane-environment.json"
    write_json(path, environment)
    return str(path.relative_to(lane_root).as_posix())


def run_lane(
    bundle_root: Path,
    bundle_id: str,
    source_freeze_id: str,
    lane: Lane,
) -> None:
    lane_root = bundle_root / "02-lanes" / lane.lane_id
    lane_root.mkdir(parents=True, exist_ok=False)
    wrapper_log = lane_root / "wrapper.log"
    evidence_run_id = f"{bundle_id}-{lane.command_id}"
    command_environment = {
        "TERMINAL_IRIS_QUALITY_OUTPUT_ROOT": str(lane_root),
        "TERMINAL_IRIS_EVIDENCE_BUNDLE_ID": bundle_id,
        "TERMINAL_IRIS_EVIDENCE_RUN_ID": evidence_run_id,
        "TERMINAL_IRIS_SOURCE_FREEZE_ID": source_freeze_id,
    }
    environment = os.environ.copy()
    environment.update(command_environment)
    start_utc = utc_now()
    start_ns = time.time_ns()
    with wrapper_log.open("w", encoding="utf-8") as output:
        process = subprocess.Popen(
            list(lane.argv),
            cwd=PROJECT_ROOT,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            errors="replace",
            bufsize=1,
        )
        assert process.stdout is not None
        for line in process.stdout:
            sys.stdout.write(line)
            sys.stdout.flush()
            output.write(line)
            output.flush()
        exit_code = process.wait()
    end_ns = time.time_ns()
    end_utc = utc_now()

    copied: list[str] = []
    missing_or_stale: list[str] = []
    for source, name in result_origins(lane):
        destination = lane_root / name
        if copy_fresh(source, destination, start_ns):
            copied.append(name)
        else:
            missing_or_stale.append(display_origin_path(source))

    player_result_path: str | None = None
    if lane.kind == "player-visual":
        result = unique_match(lane_root, "**/player-visual-result.json")
        if result is not None:
            player_result_path = result.relative_to(lane_root).as_posix()
            copied.append(player_result_path)
        else:
            missing_or_stale.append("**/player-visual-result.json")

    environment_path = write_lane_environment(lane, lane_root)
    xml_paths = sorted(lane_root.glob("*.xml"))
    xml_summaries = [xml_summary(path) for path in xml_paths]
    result_paths = [name for name in copied if name.endswith((".xml", ".json"))]
    result_status = "RECORDED"
    if missing_or_stale:
        result_status = "INCOMPLETE"
    elif lane.kind == "player-visual" and player_result_path:
        payload = json.loads((lane_root / player_result_path).read_text(encoding="utf-8"))
        result_status = str(payload.get("result", "UNKNOWN"))
    elif any(int(summary.get("failed", 0)) > 0 for summary in xml_summaries):
        result_status = "COMPLETED_WITH_TEST_FAILURES"
    elif exit_code == 0:
        result_status = "COMPLETED"
    else:
        result_status = "COMMAND_FAILED"

    lane_result = {
        "schemaVersion": 1,
        "bundleId": bundle_id,
        "sourceFreezeId": source_freeze_id,
        "laneId": lane.lane_id,
        "commandId": lane.command_id,
        "resultContractKind": lane.kind,
        "resultPaths": result_paths,
        "logPaths": [
            name for name in copied if name.endswith(".log")
        ]
        + ["wrapper.log"],
        "environmentPath": environment_path,
        "commandExitCode": exit_code,
        "resultStatus": result_status,
        "xmlSummaries": xml_summaries,
        "missingOrStaleOrigins": missing_or_stale,
        "createdUtc": utc_now(),
    }
    write_json(lane_root / "lane-result.json", lane_result)
    result_paths.append("lane-result.json")

    command_record = {
        "schemaVersion": 1,
        "laneId": lane.lane_id,
        "commandId": lane.command_id,
        "exactCommand": shlex.join(
            [
                "env",
                *[f"{key}={value}" for key, value in command_environment.items()],
                *lane.argv,
            ]
        ),
        "commandArgv": list(lane.argv),
        "workingDirectory": str(PROJECT_ROOT),
        "environment": command_environment,
        "startUtc": start_utc,
        "endUtc": end_utc,
        "startUnixNanoseconds": start_ns,
        "endUnixNanoseconds": end_ns,
        "exitCode": exit_code,
        "resultContractKind": lane.kind,
        "resultPaths": [
            str((lane_root / path).relative_to(bundle_root).as_posix())
            for path in result_paths
        ],
        "sourceFreezeId": source_freeze_id,
        "bundleId": bundle_id,
    }
    write_json(
        bundle_root / "01-commands" / f"{lane.command_id}-{lane.lane_id}.json",
        command_record,
    )


def manifest_entries(bundle_root: Path) -> list[tuple[str, str]]:
    exclusions = {
        "artifact-hashes.sha256",
        "bundle-produced.json",
        "BUNDLE_CLOSED",
    }
    entries: list[tuple[str, str]] = []
    for path in sorted(bundle_root.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(bundle_root).as_posix()
        if relative in exclusions:
            continue
        entries.append((sha256_file(path), relative))
    return entries


def lane_closure_failures(
    records: list[dict[str, object]],
    lane_contracts: dict[str, dict[str, object]],
) -> list[str]:
    failures: list[str] = []
    expected_ids = set(lane_contracts)
    actual_ids = [str(record.get("laneId", "")) for record in records]
    if set(actual_ids) != expected_ids or len(actual_ids) != len(expected_ids):
        failures.append("lane exact set is incomplete, unknown, or duplicated")
    for record in records:
        lane_id = str(record.get("laneId", ""))
        lane_contract = lane_contracts.get(lane_id)
        if lane_contract is None:
            continue
        exit_code = int(record.get("commandExitCode", -1))
        allowed = {int(value) for value in lane_contract["allowedExitCodes"]}
        if exit_code not in allowed:
            failures.append(f"{lane_id}: exit {exit_code} is not allowed")
        missing = list(record.get("missingOrStaleOrigins", []))
        if missing:
            failures.append(f"{lane_id}: missing or stale result artifacts")
        expected_status = (
            "PASS"
            if lane_contract.get("kind") == "player-visual"
            else "COMPLETED"
        )
        if record.get("resultStatus") != expected_status:
            failures.append(
                f"{lane_id}: result status {record.get('resultStatus')} "
                f"is not {expected_status}"
            )
    return failures


def can_restore_known_unity_import_drift(
    initial_modified: set[str], current_modified: set[str]
) -> bool:
    return (
        KNOWN_UNITY_IMPORT_DRIFT_PATH not in initial_modified
        and current_modified - initial_modified == {KNOWN_UNITY_IMPORT_DRIFT_PATH}
        and initial_modified - current_modified == set()
    )


def restore_known_unity_import_drift(
    initial_baseline: dict[str, object], initial_bytes: bytes
) -> bool:
    initial_modified = set(initial_baseline["trackedModified"])
    current_modified = set(run_git("diff", "--name-only").splitlines())
    if not can_restore_known_unity_import_drift(
        initial_modified, current_modified
    ):
        return False
    path = PROJECT_ROOT / KNOWN_UNITY_IMPORT_DRIFT_PATH
    path.write_bytes(initial_bytes)
    print(
        "Restored documented Unity import-derived drift: "
        f"{KNOWN_UNITY_IMPORT_DRIFT_PATH}"
    )
    return True


def run_lane_with_import_drift_closeout(
    bundle_root: Path,
    bundle_id: str,
    source_freeze_id: str,
    lane: Lane,
    initial_baseline: dict[str, object],
    known_import_drift_initial_bytes: bytes,
) -> None:
    run_lane(bundle_root, bundle_id, source_freeze_id, lane)
    restore_known_unity_import_drift(
        initial_baseline, known_import_drift_initial_bytes
    )


def main() -> int:
    if sys.argv[1:] != ["--produce"]:
        print("usage: produce_bundle.py --produce", file=sys.stderr)
        return 2
    if not CONTRACT_PATH.is_file():
        print(f"missing contract: {CONTRACT_PATH}", file=sys.stderr)
        return 2
    contract = json.loads(CONTRACT_PATH.read_text(encoding="utf-8"))
    if contract.get("contractVersion") != "terminal-iris-evidence-contract-v1":
        print("unsupported evidence contract", file=sys.stderr)
        return 2
    if SOURCE_PATHS != contract.get("requiredSources"):
        print(
            "producer source list and repository contract differ; review both lists explicitly",
            file=sys.stderr,
        )
        return 2

    timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    bundle_id = f"TICEI-{timestamp}"
    bundle_root = resolve_bundle_parent() / bundle_id
    if bundle_root.exists():
        print(f"bundle already exists: {bundle_root}", file=sys.stderr)
        return 2
    (bundle_root / "00-source").mkdir(parents=True)
    (bundle_root / "01-commands").mkdir()
    (bundle_root / "02-lanes").mkdir()

    initial_baseline = baseline()
    known_import_drift_initial_bytes = (
        PROJECT_ROOT / KNOWN_UNITY_IMPORT_DRIFT_PATH
    ).read_bytes()
    inventory = source_inventory()
    source_freeze_basis = json.dumps(
        inventory, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    source_freeze_id = f"TISF-{sha256_bytes(source_freeze_basis)[:24]}"
    source_freeze = {
        "schemaVersion": 3,
        "sourceFreezeId": source_freeze_id,
        "bundleId": bundle_id,
        "contractVersion": contract["contractVersion"],
        "contractSha256": sha256_file(CONTRACT_PATH),
        "repository": str(PROJECT_ROOT),
        "branch": initial_baseline["branch"],
        "head": initial_baseline["head"],
        "tree": initial_baseline["tree"],
        "upstream": initial_baseline["upstream"],
        "ahead": initial_baseline["ahead"],
        "behind": initial_baseline["behind"],
        "trackedDiffSha256": initial_baseline["trackedDiffSha256"],
        "cachedDiffSha256": initial_baseline["cachedDiffSha256"],
        "unityVersion": re.search(
            r"^m_EditorVersion:\s*(.+)$",
            (PROJECT_ROOT / "ProjectSettings/ProjectVersion.txt").read_text(
                encoding="utf-8"
            ),
            re.MULTILINE,
        ).group(1),
        "freezeUtc": utc_now(),
        "sources": inventory,
    }
    baseline_path = bundle_root / "00-source/baseline.json"
    freeze_path = bundle_root / "00-source/source-freeze.json"
    write_json(baseline_path, initial_baseline)
    write_json(freeze_path, source_freeze)
    (bundle_root / "00-source/source-freeze.sha256").write_text(
        f"{sha256_file(freeze_path)}  source-freeze.json\n",
        encoding="utf-8",
    )

    print(f"Producing Terminal Iris evidence bundle {bundle_id}")
    print(f"Source freeze {source_freeze_id}")
    for lane in LANES:
        print(f"\n[{lane.command_id}] {lane.lane_id}")
        run_lane_with_import_drift_closeout(
            bundle_root,
            bundle_id,
            source_freeze_id,
            lane,
            initial_baseline,
            known_import_drift_initial_bytes,
        )

    restore_known_unity_import_drift(
        initial_baseline, known_import_drift_initial_bytes
    )
    current_inventory = source_inventory()
    current_diff = diff_hash()
    current_cached = diff_hash(cached=True)
    if (
        current_inventory != inventory
        or current_diff != initial_baseline["trackedDiffSha256"]
        or current_cached != initial_baseline["cachedDiffSha256"]
    ):
        (bundle_root / "BUNDLE_ABORTED_SOURCE_DRIFT").write_text(
            "BLOCKED_BY_SOURCE_DRIFT\n",
            encoding="utf-8",
        )
        print(
            f"source drift detected; bundle remains unclosed: {bundle_root}",
            file=sys.stderr,
        )
        return 1

    lane_records = [
        json.loads(
            (
                bundle_root / "02-lanes" / lane.lane_id / "lane-result.json"
            ).read_text(encoding="utf-8")
        )
        for lane in LANES
    ]
    lane_failures = lane_closure_failures(
        lane_records, contract["laneResultContracts"]
    )
    if lane_failures:
        write_json(
            bundle_root / "BUNDLE_ABORTED_LANE_FAILURES",
            {"failures": lane_failures},
        )
        print(
            "lane closure gate failed; bundle remains unclosed: "
            f"{bundle_root}",
            file=sys.stderr,
        )
        for failure in lane_failures:
            print(f"- {failure}", file=sys.stderr)
        return 1

    entries = manifest_entries(bundle_root)
    manifest_path = bundle_root / "artifact-hashes.sha256"
    manifest_path.write_text(
        "".join(f"{digest}  {relative}\n" for digest, relative in entries),
        encoding="utf-8",
    )
    path_set_sha = sha256_bytes(
        "\n".join(relative for _, relative in entries).encode("utf-8")
    )
    produced = {
        "schemaVersion": 1,
        "bundleId": bundle_id,
        "sourceFreezeId": source_freeze_id,
        "contractVersion": contract["contractVersion"],
        "contractSha256": sha256_file(CONTRACT_PATH),
        "producedUtc": utc_now(),
        "artifactCount": len(entries),
        "artifactManifestSha256": sha256_file(manifest_path),
        "filesystemPathSetSha256": path_set_sha,
        "explicitExclusions": contract["allowedExplicitExclusions"],
        "producerDeclaredVerification": False,
    }
    produced_path = bundle_root / "bundle-produced.json"
    write_json(produced_path, produced)
    (bundle_root / "BUNDLE_CLOSED").write_text(
        json.dumps(
            {
                "bundleId": bundle_id,
                "sourceFreezeId": source_freeze_id,
                "bundleProducedSha256": sha256_file(produced_path),
                "closedUtc": utc_now(),
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    print(f"Closed evidence bundle produced: {bundle_root}")
    print("No verification was executed by the producer.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
