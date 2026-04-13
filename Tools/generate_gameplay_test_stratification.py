#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import gameplay_test_stratification_lib as lib


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate gameplay test stratification artifacts.")
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Repository root.",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Do not write files; validate current source and manifest instead.",
    )
    parser.add_argument(
        "--skip-source-rewrite",
        action="store_true",
        help="Update manifest without rewriting source categories.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    paths = lib.get_repo_paths(root)

    tests = lib.discover_tests(root, paths["test_root"])
    overrides = lib.load_overrides(paths["override_path"])
    manifest = lib.build_manifest(tests, overrides, root)
    rewrite_map = lib.build_rewrite_map(manifest)

    if args.check:
        failures = lib.check_outputs(
            paths["manifest_path"],
            manifest,
            tests,
            rewrite_map,
        )
        failures.extend(lib.validate_inventory(tests, overrides, manifest))
        if failures:
            for failure in failures:
                print(failure, file=sys.stderr)
            return 1

        lib.print_summary(manifest)
        return 0

    if not args.skip_source_rewrite:
        lib.rewrite_source_categories(root, tests, rewrite_map)

    lib.write_json(paths["manifest_path"], lib.build_persisted_manifest(manifest))
    lib.print_summary(manifest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
