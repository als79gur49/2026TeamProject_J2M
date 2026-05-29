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
    Path("Assets/_Features/Gameplay/Gameplay_Model/Runtime/Groups"),
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


@dataclass(frozen=True)
class MemberContract:
    path: Path
    type_name: str
    required_members: tuple[str, ...]
    removed_members: tuple[str, ...]


MEMBER_CONTRACTS = (
    MemberContract(
        Path("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/AttackPhaseResult.cs"),
        "DamageResolutionRecord",
        ("ActionPlanId", "IntentId"),
        ("GroupId",),
    ),
    MemberContract(
        Path("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.ResolveTypes.cs"),
        "DestroyResolutionRecord",
        ("ActionPlanId", "IntentId"),
        ("GroupId",),
    ),
    MemberContract(
        Path("Assets/_Features/Gameplay/Gameplay_Attack/Runtime/DelayedAttackEffectRecord.cs"),
        "DelayedAttackEffectRecord",
        ("SourceActionPlanId",),
        ("SourceActionGroupId",),
    ),
    MemberContract(
        Path("Assets/_Features/Gameplay/Gameplay_Model/Runtime/Groups/ActionGroup.cs"),
        "ActionGroup",
        ("GroupId", "IntentId"),
        (),
    ),
)


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


def extract_type_body(source: str, type_name: str) -> str:
    type_match = re.search(
        rf"\b(?:public|internal|private)?\s*(?:sealed\s+)?(?:readonly\s+)?(?:struct|class)\s+{re.escape(type_name)}\b",
        source,
    )
    if not type_match:
        raise ValueError(f"Could not find type declaration for {type_name}.")

    body_start = source.find("{", type_match.end())
    if body_start < 0:
        raise ValueError(f"Could not find type body for {type_name}.")

    depth = 0
    for index in range(body_start, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[body_start + 1:index]

    raise ValueError(f"Unterminated type body for {type_name}.")


def has_public_instance_property(type_body: str, member_name: str) -> bool:
    return re.search(rf"\bpublic\s+\w+(?:<[^>]+>)?\s+{re.escape(member_name)}\b", type_body) is not None


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

    for contract in MEMBER_CONTRACTS:
        file_path = root / contract.path
        relative_path = contract.path.as_posix()
        if not file_path.exists():
            issues.append(f"{relative_path}: missing file for ActionPlanId member contract")
            continue

        try:
            type_body = extract_type_body(strip_comments(file_path.read_text(encoding="utf-8")), contract.type_name)
        except ValueError as exc:
            issues.append(f"{relative_path}: {exc}")
            continue

        for member_name in contract.required_members:
            if not has_public_instance_property(type_body, member_name):
                issues.append(
                    f"{relative_path}: {contract.type_name}.{member_name} is required by the ActionPlanId correlation contract"
                )

        for member_name in contract.removed_members:
            if has_public_instance_property(type_body, member_name):
                issues.append(
                    f"{relative_path}: {contract.type_name}.{member_name} must remain removed from the ActionPlanId correlation contract"
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
