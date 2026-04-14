#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import gameplay_test_stratification_lib as lib


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Check gameplay test stratification governance.")
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Repository root.",
    )
    parser.add_argument(
        "--mode",
        choices=(lib.SOFT_GOVERNANCE_MODE, lib.STRICT_GOVERNANCE_MODE),
        default=None,
        help="Override governance mode.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    paths = lib.get_repo_paths(root)
    mode = lib.determine_governance_mode(explicit_mode=args.mode)

    try:
        tests = lib.discover_tests(root, paths["test_root"])
        overrides = lib.load_overrides(paths["override_path"])
        manifest = lib.build_manifest(tests, overrides, root)
        rewrite_map = lib.build_rewrite_map(manifest)
        output_failures = lib.check_source_categories(manifest, tests, rewrite_map)
        inventory_failures = lib.validate_inventory(tests, overrides, manifest)
        summary, warnings, failures = lib.build_governance_summary(root, tests, manifest, mode)
    except Exception as error:
        print(f"ERROR: governance check failed: {error}", file=sys.stderr)
        return 2

    for line in lib.format_governance_summary(summary):
        print(line)

    all_warnings = list(warnings)
    all_failures = list(failures)
    if inventory_failures:
        if mode == lib.STRICT_GOVERNANCE_MODE:
            all_failures.extend(inventory_failures)
        else:
            all_warnings.extend(inventory_failures)
    if output_failures:
        if mode == lib.STRICT_GOVERNANCE_MODE:
            all_failures.extend(output_failures)
        else:
            all_warnings.extend(output_failures)

    for warning in all_warnings:
        print(f"WARNING: {warning}")

    for failure in all_failures:
        print(f"ERROR: {failure}", file=sys.stderr)

    return 1 if all_failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
