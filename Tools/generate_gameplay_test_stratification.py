#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import gameplay_test_stratification_lib as lib


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate gameplay test stratification source truth.")
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Repository root.",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Compatibility flag. Validation is source-truth only.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    paths = lib.get_repo_paths(root)
    _ = args.check

    tests = lib.discover_tests(root, paths["test_root"])
    overrides = lib.load_overrides(paths["override_path"])
    manifest = lib.build_manifest(tests, overrides, root)
    rewrite_map = lib.build_rewrite_map(manifest)

    failures = lib.check_source_categories(manifest, tests, rewrite_map)
    failures.extend(lib.validate_inventory(tests, overrides, manifest))
    if failures:
        for failure in failures:
            print(failure, file=sys.stderr)
        return 1

    lib.print_summary(manifest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
