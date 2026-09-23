#!/usr/bin/env python3
"""Validate J2M's four-locale review CSV against production English tables."""

from __future__ import annotations

import argparse
import csv
import hashlib
import re
import sys
from pathlib import Path

try:
    import yaml
except ImportError as error:  # pragma: no cover - environment failure message
    raise SystemExit("PyYAML is required to read Unity String Table YAML.") from error


DRAFT_PATH = Path("Docs/Localization/Four-Locale-Translation-Draft.csv")
FONT_ROOT = Path("Assets/_Shared/UI/Fonts/NotoSansCJK")
FONT_HASHES = {
    "NotoSansJP-Regular.otf": "dff723ba59d57d136764a04b9b2d03205544f7cd785a711442d6d2d085ac5073",
    "NotoSansJP-Bold.otf": "1b0edfb500b73a4fa8a4fcaae1bbbd403994e08e73e3e0da37e70d3853f42c5f",
    "NotoSansSC-Regular.otf": "faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9",
    "NotoSansSC-Bold.otf": "c6cb5a93abaa9edc8ee7463b7ebb7f42d618d40e6ed2f7a5371c97b0b64767c0",
}
EXPECTED_FIELDS = (
    "collection",
    "key",
    "source_en_US",
    "ja_JP",
    "zh_CN",
    "review_state",
)
PLACEHOLDER_PATTERN = re.compile(r"\{\d+\}")


def load_unity_yaml(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    payload = "\n".join(
        line for line in text.splitlines()
        if not line.startswith("%") and not line.startswith("---")
    )
    return yaml.safe_load(payload)["MonoBehaviour"]


def load_english_inventory(root: Path) -> dict[tuple[str, str], str]:
    inventory: dict[tuple[str, str], str] = {}
    for collection in ("UI", "Stage"):
        table_root = root / "Assets/Localization/StringTables" / collection
        shared = load_unity_yaml(table_root / f"{collection} Shared Data.asset")
        english = load_unity_yaml(table_root / f"{collection}_en-US.asset")
        keys_by_id = {str(entry["m_Id"]): entry["m_Key"] for entry in shared["m_Entries"]}
        for entry in english["m_TableData"]:
            entry_id = str(entry["m_Id"])
            if entry_id not in keys_by_id:
                raise ValueError(f"{collection}: English entry id {entry_id} has no shared key")
            value = entry.get("m_Localized", "")
            if isinstance(value, bool):
                value = "On" if value else "Off"
            inventory[(collection, keys_by_id[entry_id])] = str(value)
    return inventory


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def validate_fonts(root: Path, errors: list[str]) -> None:
    for filename, expected_hash in FONT_HASHES.items():
        path = root / FONT_ROOT / filename
        if not path.is_file():
            errors.append(f"missing font source: {path}")
            continue
        actual_hash = sha256(path)
        if actual_hash != expected_hash:
            errors.append(
                f"font hash mismatch: {filename}; expected {expected_hash}; got {actual_hash}"
            )
    license_path = root / FONT_ROOT / "OFL-1.1.txt"
    if not license_path.is_file():
        errors.append(f"missing font license: {license_path}")


def validate(root: Path, require_approved: bool) -> int:
    errors: list[str] = []
    inventory = load_english_inventory(root)
    draft_path = root / DRAFT_PATH
    with draft_path.open(encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream)
        if tuple(reader.fieldnames or ()) != EXPECTED_FIELDS:
            errors.append(
                f"CSV header mismatch; expected {EXPECTED_FIELDS}; got {tuple(reader.fieldnames or ())}"
            )
        rows = list(reader)

    rows_by_key: dict[tuple[str, str], dict[str, str]] = {}
    for line_number, row in enumerate(rows, start=2):
        identity = (row.get("collection", ""), row.get("key", ""))
        if identity in rows_by_key:
            errors.append(f"line {line_number}: duplicate row {identity}")
            continue
        rows_by_key[identity] = row

        state = row.get("review_state", "")
        if state not in ("Draft", "Approved"):
            errors.append(f"line {line_number}: invalid review_state {state!r}")
        if require_approved and state != "Approved":
            errors.append(f"line {line_number}: {identity} is not Approved")

        source = row.get("source_en_US", "").replace("\\n", "\n")
        expected_source = inventory.get(identity)
        if expected_source is not None and source != expected_source:
            errors.append(f"line {line_number}: stale English source for {identity}")

        expected_placeholders = sorted(PLACEHOLDER_PATTERN.findall(source))
        for locale_field in ("ja_JP", "zh_CN"):
            translation = row.get(locale_field, "")
            if not translation.strip():
                errors.append(f"line {line_number}: empty {locale_field} translation for {identity}")
                continue
            actual_placeholders = sorted(PLACEHOLDER_PATTERN.findall(translation))
            if actual_placeholders != expected_placeholders:
                errors.append(
                    f"line {line_number}: {locale_field} placeholders for {identity}; "
                    f"expected {expected_placeholders}; got {actual_placeholders}"
                )

    missing = sorted(set(inventory) - set(rows_by_key))
    unexpected = sorted(set(rows_by_key) - set(inventory))
    for identity in missing:
        errors.append(f"missing governed row: {identity}")
    for identity in unexpected:
        errors.append(f"unexpected or removed governed row: {identity}")

    validate_fonts(root, errors)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print(f"TRANSLATION_DRAFT_VALIDATION=FAIL errors={len(errors)}", file=sys.stderr)
        return 1

    approved_count = sum(row["review_state"] == "Approved" for row in rows)
    print(
        "TRANSLATION_DRAFT_VALIDATION=PASS "
        f"keys={len(rows)} draft={len(rows) - approved_count} approved={approved_count} "
        f"fonts={len(FONT_HASHES)} require_approved={str(require_approved).lower()}"
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--require-approved", action="store_true")
    args = parser.parse_args()
    return validate(args.project_root.resolve(), args.require_approved)


if __name__ == "__main__":
    raise SystemExit(main())
