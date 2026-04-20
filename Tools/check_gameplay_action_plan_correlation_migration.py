#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path

TARGET_DIRS = (
    Path("Assets/_Features/Gameplay/Gameplay_Loop/Runtime"),
    Path("Assets/_Features/Gameplay/Gameplay_Attack/Runtime"),
    Path("Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Commit"),
    Path("Assets/_Features/Gameplay/Gameplay_Debug/Runtime"),
    Path("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport"),
)

COMMENT_PATTERN = re.compile(r"//.*?$|/\*.*?\*/", re.MULTILINE | re.DOTALL)

FORBIDDEN_PATTERNS = (
    (
        re.compile(r"\b(?:damageResolution|damageResolutions\s*\[[^\]]+\]|record)\.GroupId\b"),
        "legacy DamageResolutionRecord.GroupId reader",
    ),
    (
        re.compile(r"\b(?:destroyResolution|destroyResolutions\s*\[[^\]]+\])\.GroupId\b"),
        "legacy DestroyResolutionRecord.GroupId reader",
    ),
    (
        re.compile(r"\b(?:effect|effectRecord|left|right|operation\.DelayedAttackEffect)\.SourceActionGroupId\b"),
        "legacy DelayedAttackEffectRecord.SourceActionGroupId reader",
    ),
)


@dataclass(frozen=True)
class AllowlistEntry:
    path: str
    pattern: str
    reason: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Check ActionPlanId correlation migration governance.")
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Repository root.",
    )
    parser.add_argument(
        "--allowlist",
        type=Path,
        default=Path(__file__).resolve().with_name("action_plan_correlation_migration_allowlist.json"),
        help="Allowlist metadata path.",
    )
    return parser.parse_args()


def strip_comments(source: str) -> str:
    return COMMENT_PATTERN.sub("", source)


def iter_target_files(root: Path) -> list[Path]:
    files: list[Path] = []
    for relative_dir in TARGET_DIRS:
        target_dir = root / relative_dir
        if not target_dir.exists():
            continue
        files.extend(sorted(target_dir.rglob("*.cs")))
    return files


def load_allowlist(path: Path) -> set[tuple[str, str]]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    entries = payload.get("entries", [])
    allowlist: set[tuple[str, str]] = set()
    for entry in entries:
        allowlist_entry = AllowlistEntry(
            path=str(entry.get("path", "")).strip(),
            pattern=str(entry.get("pattern", "")).strip(),
            reason=str(entry.get("reason", "")).strip(),
        )
        if not allowlist_entry.path or not allowlist_entry.pattern:
            raise ValueError(f"Malformed allowlist entry: {entry}")
        allowlist.add((allowlist_entry.path, allowlist_entry.pattern))
    return allowlist


def line_number_for_index(source: str, index: int) -> int:
    return source.count("\n", 0, index) + 1


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    allowlist_path = args.allowlist.resolve()

    if not allowlist_path.exists():
        print(f"ERROR: missing allowlist metadata: {allowlist_path}", file=sys.stderr)
        return 1

    allowlist = load_allowlist(allowlist_path)
    issues: list[str] = []

    for file_path in iter_target_files(root):
        relative_path = file_path.relative_to(root).as_posix()
        source = strip_comments(file_path.read_text(encoding="utf-8"))

        for pattern, description in FORBIDDEN_PATTERNS:
            for match in pattern.finditer(source):
                key = (relative_path, pattern.pattern)
                if key in allowlist:
                    continue
                issues.append(
                    f"{relative_path}:{line_number_for_index(source, match.start())} uses forbidden {description}: {match.group(0)}"
                )

    if issues:
        print("ActionPlanId correlation migration governance failed:", file=sys.stderr)
        for issue in issues:
            print(f" - {issue}", file=sys.stderr)
        return 1

    print("ActionPlanId correlation migration governance check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
