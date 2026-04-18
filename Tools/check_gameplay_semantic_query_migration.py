#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path

RUNTIME_CORE_DIRS = (
    Path("Assets/_Features/Gameplay/Gameplay_Movement/Runtime"),
    Path("Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime"),
    Path("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime"),
    Path("Assets/_Features/Gameplay/Gameplay_Entities/Runtime"),
    Path("Assets/_Features/Gameplay/Gameplay_Loop/Runtime"),
)

FORBIDDEN_PATTERNS = {
    "TryGetBoxAt(": "compatibility solid query",
    "TryGetSolidOccupantAt(": "low-level solid storage query",
    "TryGetPrimaryUnitAt(": "primary-unit convenience query",
    "blocker.EntityType ==": "blocker entity-type meaning recovery",
    "BlocksUnitMovement(": "planar terrain compatibility helper",
    "CreateDefaultQueryCell(": "snapshot planar compatibility seam",
    "SurfaceCell.FromPlanar(": "planar cell reconstruction",
}

COMPOSITE_METHOD_NAME_PATTERN = re.compile(r"(^Can|^IsLegal|Accept|Blocked|Obstacle)")
METHOD_DECLARATION_PATTERN = re.compile(
    r"(?P<prefix>(?:public|private|internal|protected|static|sealed|virtual|override|readonly|unsafe|extern|\s)+)"
    r"bool\s+(?P<name>[A-Za-z_]\w*)\s*\(",
    re.MULTILINE,
)
COMMENT_PATTERN = re.compile(r"//.*?$|/\*.*?\*/", re.MULTILINE | re.DOTALL)

QUERY_TOKENS = (
    "IsInsideBoard(",
    "TryGetTerrain(",
    "IsTerrainBlockedForUnit(",
    "TryGetSolidSemanticAt(",
    "IsWallAt(",
    "IsBoxAt(",
    "HasAnyUnitAt(",
    "TryGetPrimaryUnitAt(",
    "EnumerateUnitsAt(",
    "TryGetUnitTraversalBlocker(",
    "TryGetPlacementBlocker(",
    "TryGetAuthoritativePlacementBlocker(",
    "TryGetSolidOccupantAt(",
    "TryGetBoxAt(",
)

KNOWN_QUARANTINE_METHODS = {
    ("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyJumpState.cs", "IsLegalJumpLandingCell"),
    ("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyMovementPolicy.cs", "CanOccupyStep"),
    ("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyMovementPolicy.cs", "IsChargeStoppingObstacle"),
    ("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs", "CanAcceptJumpLandingCell"),
    ("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs", "CanAcceptImpactFollowThrough"),
}


@dataclass(frozen=True)
class AllowlistEntry:
    path: str
    symbol: str
    pattern: str
    reason: str
    remove_by_stage: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Check gameplay semantic query migration governance.")
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Repository root.",
    )
    parser.add_argument(
        "--allowlist",
        type=Path,
        default=Path(__file__).resolve().with_name("semantic_query_migration_allowlist.json"),
        help="Allowlist metadata path.",
    )
    return parser.parse_args()


def strip_comments(source: str) -> str:
    return COMMENT_PATTERN.sub("", source)


def iter_runtime_core_files(root: Path) -> list[Path]:
    files: list[Path] = []
    for relative_dir in RUNTIME_CORE_DIRS:
        runtime_dir = root / relative_dir
        if not runtime_dir.exists():
            continue
        files.extend(sorted(runtime_dir.rglob("*.cs")))
    return files


def load_allowlist(path: Path) -> list[AllowlistEntry]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    entries = payload.get("entries", [])
    loaded: list[AllowlistEntry] = []
    for entry in entries:
        loaded.append(
            AllowlistEntry(
                path=str(entry.get("path", "")).strip(),
                symbol=str(entry.get("symbol", "")).strip(),
                pattern=str(entry.get("pattern", "")).strip(),
                reason=str(entry.get("reason", "")).strip(),
                remove_by_stage=str(entry.get("remove_by_stage", "")).strip(),
            )
        )
    return loaded


def validate_allowlist(entries: list[AllowlistEntry]) -> list[str]:
    issues: list[str] = []
    seen_pairs = set()
    for entry in entries:
        pair = (entry.path, entry.symbol)
        if pair in seen_pairs:
            issues.append(f"Duplicate allowlist entry for {entry.path}:{entry.symbol}")
        seen_pairs.add(pair)

        for field_name, value in (
            ("path", entry.path),
            ("symbol", entry.symbol),
            ("pattern", entry.pattern),
            ("reason", entry.reason),
            ("remove_by_stage", entry.remove_by_stage),
        ):
            if not value:
                issues.append(f"Allowlist entry {pair} is missing {field_name}")

        if entry.pattern != "composite_legality_owner":
            issues.append(f"Allowlist entry {pair} uses unsupported pattern {entry.pattern}")

        if pair not in KNOWN_QUARANTINE_METHODS:
            issues.append(f"Allowlist entry {pair} is not a known quarantine owner")

    return issues


def find_matching_brace(source: str, open_index: int) -> int:
    depth = 0
    for index in range(open_index, len(source)):
        character = source[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return index
    return -1


def iter_bool_methods(source: str) -> list[tuple[str, str]]:
    methods: list[tuple[str, str]] = []
    for match in METHOD_DECLARATION_PATTERN.finditer(source):
        brace_index = source.find("{", match.end())
        if brace_index == -1:
            continue
        close_index = find_matching_brace(source, brace_index)
        if close_index == -1:
            continue
        methods.append((match.group("name"), source[brace_index:close_index + 1]))
    return methods


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    allowlist_path = args.allowlist.resolve()
    runtime_files = iter_runtime_core_files(root)
    issues: list[str] = []

    if not allowlist_path.exists():
        print(f"ERROR: missing allowlist metadata: {allowlist_path}", file=sys.stderr)
        return 1

    allowlist_entries = load_allowlist(allowlist_path)
    issues.extend(validate_allowlist(allowlist_entries))
    allowlisted_pairs = {(entry.path, entry.symbol) for entry in allowlist_entries}

    for file_path in runtime_files:
        relative_path = file_path.relative_to(root).as_posix()
        source = strip_comments(file_path.read_text(encoding="utf-8"))

        for token, description in FORBIDDEN_PATTERNS.items():
            if token in source:
                issues.append(f"{relative_path} uses forbidden {description}: {token}")

        discovered_methods = {name for name, _ in iter_bool_methods(source)}
        for known_path, known_symbol in KNOWN_QUARANTINE_METHODS:
            if known_path == relative_path and known_symbol in discovered_methods:
                if (known_path, known_symbol) not in allowlisted_pairs:
                    issues.append(
                        f"{relative_path}:{known_symbol} is a known quarantine owner but is missing allowlist metadata"
                    )

        for method_name, method_body in iter_bool_methods(source):
            if not COMPOSITE_METHOD_NAME_PATTERN.search(method_name):
                continue

            matched_tokens = [token for token in QUERY_TOKENS if token in method_body]
            if len(matched_tokens) < 2:
                continue

            pair = (relative_path, method_name)
            if pair in allowlisted_pairs:
                continue

            issues.append(
                f"{relative_path}:{method_name} reassembles composite legality from semantic queries "
                f"({', '.join(sorted(matched_tokens))})"
            )

    print("Semantic query migration governance")
    print(f"Runtime files scanned: {len(runtime_files)}")
    print(f"Allowlist entries: {len(allowlist_entries)}")

    if issues:
        for issue in issues:
            print(f"ERROR: {issue}", file=sys.stderr)
        return 1

    print("No semantic query migration governance issues found.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
