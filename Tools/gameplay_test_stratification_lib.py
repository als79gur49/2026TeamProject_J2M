from __future__ import annotations

import json
import math
import os
import re
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
from functools import lru_cache
from pathlib import Path
from typing import Dict, Iterable, List, Mapping, Sequence, Tuple


PRIMARY_CATEGORIES = ("Core", "Extended", "Full")
FULL_FILE_NAMES = {
    "CombinedGameplayShowcaseInstallerTests.cs",
    "EnemyViewAnimatorControllerContractTests.cs",
    "EnemyPrefabScaffoldTests.cs",
    "FuzzDeterminismTests.cs",
    "GameplayShowcaseScaffoldTests.cs",
    "RuntimeBoardBoundsGuardTests.cs",
}
FULL_KEYWORDS = (
    "wallfollower",
    "chargingprofile",
    "showcase",
    "scaffold",
    "prefab",
    "longrunning",
    "topologychange",
    "sharededge",
    "projectilereservations",
    "tworingfallback",
    "retry",
)

SOFT_GOVERNANCE_MODE = "soft"
STRICT_GOVERNANCE_MODE = "strict"

FEATURE_ASSEMBLY_NAME = "Game.Feature.Gameplay.Tests"
PLAYMODE_ASSEMBLY_NAME = "Game.Feature.Gameplay.PlayModeTests"
CORE_ASSEMBLY_NAME = "Game.Core.Tests"
INTEGRATION_REPLAY_ASSEMBLY_NAME = "Game.Integration.Replay.Tests"
INTEGRATION_FUZZ_ASSEMBLY_NAME = "Game.Integration.Fuzz.Tests"
INTEGRATION_SIMULATION_ASSEMBLY_NAME = "Game.Integration.Simulation.Tests"
PURE_SUPPORT_ASSEMBLY_NAME = "Game.TestSupport.Pure"
UNITY_SUPPORT_ASSEMBLY_NAME = "Game.TestSupport.Unity"
INFRASTRUCTURE_ASSEMBLY_NAME = "Game.TestInfrastructure"

INTEGRATION_SUBTYPE_PRIORITY = ("Replay", "Fuzz", "Simulation")
EXPECTED_ASSEMBLY_BY_PATH = {
    "/EditMode/Core/": CORE_ASSEMBLY_NAME,
    "/EditMode/Replay/": INTEGRATION_REPLAY_ASSEMBLY_NAME,
    "/EditMode/Fuzz/": INTEGRATION_FUZZ_ASSEMBLY_NAME,
    "/EditMode/Scenario/": INTEGRATION_SIMULATION_ASSEMBLY_NAME,
    "/EditMode/TestSupport/Infrastructure/": INFRASTRUCTURE_ASSEMBLY_NAME,
    "/PlayMode/": PLAYMODE_ASSEMBLY_NAME,
}

FORBIDDEN_CORE_IMPORTS = (
    "UnityEditor",
    "Game.Feature.Gameplay.Host",
)
FORBIDDEN_CORE_SYMBOLS = (
    "GameplayWorldStateTestFactory",
    "EnemyAiProfileTestFactory",
    "GameplayTestSourceDiscovery",
    "TickReplayHarness",
    "ReplayDivergenceArtifact",
    "CreateWorldState(",
)
EXECUTION_ENTRYPOINT_PATTERNS = (
    "RunTick(",
    "RunNextTick(",
    "CreateTickPipeline(",
    "CreateTickRunner(",
    "new TickPipeline(",
    "new TickRunner(",
)
EXECUTION_CONTEXT_PATTERNS = (
    "CompletedAllPhases",
    "CompletedPhases",
    "PhaseTrace",
    "MovementPhaseResult",
    "AttackPhaseResult",
    "CleanupPhaseResult",
    "CommitEvents",
    "RejectedReasons",
    "TickIndex",
)
EXECUTION_CHAIN_SYMBOLS = (
    "MovementCommitter",
    "AttackCommitter",
    "CleanupProcessor",
    "CommitEvents",
)
FORBIDDEN_CORE_STRUCTURE_PATTERNS = (
    "BindingFlags.NonPublic",
    "GetFields(",
    "GetConstructor(",
    "GetConstructors(",
)
FORBIDDEN_CORE_COMPOSITION_SYMBOLS = (
    "GameplayCompositionRoot",
    "GameplayBootstrapper",
    "ISnapshotEntityLogicProvider",
    "MovementCommitter",
    "AttackCommitter",
    "CleanupProcessor",
    "WorldStateWriteContext",
)
FORBIDDEN_CORE_DETERMINISM_PATTERNS = (
    "new Random(",
    "Random.",
    "Guid.NewGuid(",
    "DateTime.Now",
    "DateTime.UtcNow",
    "DateTimeOffset.Now",
    "DateTimeOffset.UtcNow",
    "Stopwatch",
    "Thread.Sleep(",
    "Task.Delay(",
    "Time.",
    "EditorApplication.",
    "Application.",
    "WaitForSeconds",
    "WaitUntil",
    "WaitWhile",
    "[UnityTest]",
    "UnitySetUp",
    "UnityTearDown",
    "MonoBehaviour",
    "SceneManager",
)
BEHAVIOR_ASSERTION_PATTERNS = (
    "CompletedAllPhases",
    "CompletedPhases",
    "PhaseTrace",
    "CommitEvents",
    "RejectedReasons",
    "PresentationData.",
    "GetEntityPosition(",
    "GetEntityHp(",
    "IsMarkedForDeath(",
    "TickIndex",
)
FORBIDDEN_INFRASTRUCTURE_SYMBOLS = (
    "GameplayWorldStateTestFactory",
    "EnemyAiProfileTestFactory",
    "CreateWorldState(",
    ".CreateWriteContext(",
    ".MoveEntity(",
    ".SpawnEntity(",
    ".ApplyDamage(",
)


@dataclass
class TestMethod:
    absolute_path: Path
    relative_path: str
    mode: str
    namespace: str
    class_name: str
    method_name: str
    fully_qualified_name: str
    line_number: int
    attribute_block_start: int
    attribute_block_end: int
    attribute_lines: List[str]
    body: str
    newline: str


@dataclass
class GovernanceSummary:
    mode: str
    core_l1_count: int
    core_l2_count: int
    feature_count: int
    infrastructure_count: int
    integration_simulation_count: int
    integration_replay_count: int
    integration_fuzz_count: int
    playmode_core_count: int
    playmode_core_cap: int
    candidate_count: int
    candidate_threshold: int
    candidate_debt_streak: int


def get_repo_paths(root: Path) -> Dict[str, Path]:
    test_root = root / "Assets/_Features/Gameplay/Gameplay_Tests"
    result_root = root / "TestResults"
    return {
        "root": root,
        "test_root": test_root,
        "override_path": test_root / "EditMode/TestSupport/GameplayTestStratificationOverrides.json",
        "governance_history_path": result_root / ".governance/authoritative-history.json",
        "metrics_dir": result_root / ".metrics",
    }


def determine_governance_mode(env: Mapping[str, str] | None = None, explicit_mode: str | None = None) -> str:
    if explicit_mode:
        normalized = explicit_mode.strip().lower()
        if normalized in {SOFT_GOVERNANCE_MODE, STRICT_GOVERNANCE_MODE}:
            return normalized

    env = env or os.environ
    raw_mode = env.get("STRATIFICATION_GOVERNANCE_MODE", "").strip().lower()
    if raw_mode in {SOFT_GOVERNANCE_MODE, STRICT_GOVERNANCE_MODE}:
        return raw_mode

    return STRICT_GOVERNANCE_MODE if env.get("CI", "").strip().lower() == "true" else SOFT_GOVERNANCE_MODE


def discover_tests(root: Path, test_root: Path) -> List[TestMethod]:
    namespace_re = re.compile(r"namespace\s+(?P<name>[A-Za-z0-9_.]+)")
    class_re = re.compile(r"(?:public|internal)\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(?P<name>[A-Za-z0-9_]+)")
    method_re = re.compile(
        r"(?P<attrs>(?:^[ \t]*\[[^\n]+\]\s*\n)+)"
        r"(?P<signature>[ \t]*public[^\n(]*?\s+(?P<name>[A-Za-z0-9_]+)\s*\([^\n]*\))",
        re.MULTILINE,
    )
    discovered: List[TestMethod] = []

    for path in sorted(test_root.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        namespace_match = namespace_re.search(text)
        class_match = class_re.search(text)
        if not namespace_match or not class_match:
            continue

        namespace_name = namespace_match.group("name")
        class_name = class_match.group("name")
        newline = "\r\n" if "\r\n" in text else "\n"
        relative_path = path.relative_to(root).as_posix()
        mode = "PlayMode" if "/PlayMode/" in relative_path else "EditMode"

        for match in method_re.finditer(text):
            attribute_lines = [line.strip() for line in match.group("attrs").splitlines() if line.strip()]
            if "[Test]" not in attribute_lines and "[UnityTest]" not in attribute_lines:
                continue

            body_start = text.find("{", match.end("signature"))
            if body_start < 0:
                raise ValueError(f"Could not find method body start for {path}:{match.group('name')}")

            body_end = find_matching_brace(text, body_start)
            line_number = text.count("\n", 0, match.start("signature")) + 1
            method_name = match.group("name")
            discovered.append(
                TestMethod(
                    absolute_path=path,
                    relative_path=relative_path,
                    mode=mode,
                    namespace=namespace_name,
                    class_name=class_name,
                    method_name=method_name,
                    fully_qualified_name=f"{namespace_name}.{class_name}.{method_name}",
                    line_number=line_number,
                    attribute_block_start=match.start("attrs"),
                    attribute_block_end=match.end("attrs"),
                    attribute_lines=attribute_lines,
                    body=text[body_start : body_end + 1],
                    newline=newline,
                )
            )

    return sorted(discovered, key=lambda test: (test.relative_path, test.line_number, test.fully_qualified_name))


def find_matching_brace(text: str, opening_index: int) -> int:
    depth = 0
    i = opening_index
    in_string = False
    in_char = False
    in_verbatim_string = False
    in_line_comment = False
    in_block_comment = False
    while i < len(text):
        current = text[i]
        nxt = text[i + 1] if i + 1 < len(text) else ""

        if in_line_comment:
            if current == "\n":
                in_line_comment = False
            i += 1
            continue

        if in_block_comment:
            if current == "*" and nxt == "/":
                in_block_comment = False
                i += 2
                continue
            i += 1
            continue

        if in_verbatim_string:
            if current == '"' and nxt == '"':
                i += 2
                continue
            if current == '"':
                in_verbatim_string = False
            i += 1
            continue

        if in_string:
            if current == "\\":
                i += 2
                continue
            if current == '"':
                in_string = False
            i += 1
            continue

        if in_char:
            if current == "\\":
                i += 2
                continue
            if current == "'":
                in_char = False
            i += 1
            continue

        if current == "/" and nxt == "/":
            in_line_comment = True
            i += 2
            continue

        if current == "/" and nxt == "*":
            in_block_comment = True
            i += 2
            continue

        if current == "@" and nxt == '"':
            in_verbatim_string = True
            i += 2
            continue

        if current == '"':
            in_string = True
            i += 1
            continue

        if current == "'":
            in_char = True
            i += 1
            continue

        if current == "{":
            depth += 1
        elif current == "}":
            depth -= 1
            if depth == 0:
                return i

        i += 1

    raise ValueError("Method body brace matching failed.")


def load_overrides(path: Path) -> Dict[str, dict]:
    data = json.loads(path.read_text(encoding="utf-8"))
    overrides = {}
    for entry in data.get("overrides", []):
        key = entry["fullyQualifiedName"]
        if key in overrides:
            raise ValueError(f"Duplicate override entry: {key}")
        overrides[key] = entry
    return overrides


def build_manifest(tests: List[TestMethod], overrides: Dict[str, dict], root: Path) -> dict:
    assembly_map = discover_asmdef_map(root / "Assets/_Features/Gameplay/Gameplay_Tests")
    entries = []
    for test in tests:
        override = overrides.get(test.fully_qualified_name)
        assembly_name = resolve_assembly_name(test.absolute_path, assembly_map)
        contracts = override.get("contracts") if override else infer_contracts(test)
        if not contracts:
            contracts = [fallback_file_contract(test)]

        if override and override.get("category"):
            category = override["category"]
        else:
            category = auto_category(test, contracts, assembly_name)

        entries.append(
            {
                "fullyQualifiedName": test.fully_qualified_name,
                "category": category,
                "mode": test.mode,
                "contracts": contracts,
            }
        )

    return {"tests": entries}


def infer_contracts(test: TestMethod) -> List[str]:
    name_lower = test.method_name.lower()
    body_lower = test.body.lower()
    combined = f"{name_lower}\n{body_lower}"
    contracts: List[str] = []

    def add(contract: str) -> None:
        if contract not in contracts:
            contracts.append(contract)

    if "completesplanresolvefinalize" in name_lower or "extendsfinalstateandeventlog" in name_lower:
        add("canonical_path")
        add("resolve_finalize_contract")

    if "determin" in combined or "sameinput" in name_lower or "orderisdeterministic" in name_lower:
        add("determinism")

    if "presentationcatalog" in combined or "presentationid" in combined or "metadata" in combined:
        add("semantic_metadata")

    if "push" in combined:
        add("interaction_push")

    if "flip" in combined:
        add("interaction_flip")

    if "jump" in combined and "landing" in combined:
        add("interaction_jump")

    if "impact_killsenemy" in name_lower or "impactandstopsbeforeunitcell" in name_lower or "damage" in combined and "move" in combined:
        add("damage_affects_movement")

    if "movecommit" in name_lower or "attackconsumesit" in name_lower:
        add("movement_affects_attack")

    if "respawn" in combined or "cleanup" in combined or "spawnedentity_becomesvisibleaftertick" in name_lower or "delayedeventqueue" in combined:
        add("spawn_destroy_delayed_ordering")

    file_contract = fallback_file_contract(test)
    if file_contract not in contracts:
        contracts.append(file_contract)
    return contracts


def fallback_file_contract(test: TestMethod) -> str:
    stem = test.absolute_path.stem
    stem = stem[:-5] if stem.endswith("Tests") else stem
    return f"{camel_to_snake(stem)}_contract"


def camel_to_snake(value: str) -> str:
    first_pass = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", value)
    second_pass = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", first_pass)
    return second_pass.replace("__", "_").strip("_").lower()


def auto_category(test: TestMethod, contracts: List[str], assembly_name: str) -> str:
    lower_name = test.method_name.lower()
    if assembly_name == CORE_ASSEMBLY_NAME:
        return "Core"
    if any("[Explicit" in line for line in test.attribute_lines):
        return "Full"
    if any('Category("LongRunning")' in line for line in test.attribute_lines):
        return "Full"
    if test.absolute_path.name in FULL_FILE_NAMES:
        return "Full"
    if test.mode == "PlayMode":
        return "Full"
    if any(keyword in lower_name for keyword in FULL_KEYWORDS):
        return "Full"
    if "semantic_metadata" in contracts and "GameplayTimingOwnershipTests.cs" in test.absolute_path.name:
        return "Extended"
    return "Extended"


def auto_reason(test: TestMethod, category: str, contracts: List[str]) -> str:
    primary_contract = contracts[0] if contracts else fallback_file_contract(test)
    if category == "Core":
        return f"Structural Core coverage for {primary_contract} enforced by the Game.Core.Tests assembly."
    if category == "Full":
        if test.mode == "PlayMode":
            return "PlayMode integration coverage outside the fast Core path."
        if test.absolute_path.name in FULL_FILE_NAMES:
            return "Large scaffold, fuzz, or broad regression coverage reserved for Full runs."
        return f"Rare edge or broad variation coverage for {primary_contract} reserved for Full runs."
    return f"Variation/detail coverage for {primary_contract} outside the minimal Core representative set."


def build_rewrite_map(manifest: dict) -> Dict[str, str]:
    return {
        entry["fullyQualifiedName"]: entry["category"]
        for entry in manifest["tests"]
    }


def check_source_categories(
    manifest: dict,
    tests: List[TestMethod],
    rewrite_map: Dict[str, str],
) -> List[str]:
    failures: List[str] = []
    for test in tests:
        current_categories = [
            match.group(1)
            for line in test.attribute_lines
            for match in [re.search(r'Category\("(Core|Extended|Full)"\)', line)]
            if match
        ]
        if len(current_categories) != 1:
            failures.append(f"Source test does not have exactly one primary category: {test.fully_qualified_name}")
            continue

        expected_category = rewrite_map[test.fully_qualified_name]
        if current_categories[0] != expected_category:
            failures.append(
                f"Source category mismatch for {test.fully_qualified_name}: {current_categories[0]} != {expected_category}"
            )
    return failures


def validate_inventory(tests: Sequence[TestMethod], overrides: Mapping[str, dict], manifest: Mapping[str, object]) -> List[str]:
    failures: List[str] = []
    test_names = [test.fully_qualified_name for test in tests]
    manifest_names = [entry["fullyQualifiedName"] for entry in manifest["tests"]]

    if len(set(test_names)) != len(test_names):
        failures.append("Source discovery produced duplicate fully-qualified test names.")

    if len(set(manifest_names)) != len(manifest_names):
        failures.append("Generated manifest contains duplicate fully-qualified test names.")

    discovered_name_set = set(test_names)
    for fully_qualified_name in overrides:
        if fully_qualified_name not in discovered_name_set:
            failures.append(f"Override points at a missing test: {fully_qualified_name}")

    return failures


def print_summary(manifest: dict) -> None:
    counts = Counter(entry["category"] for entry in manifest["tests"])
    total = len(manifest["tests"])
    core_ratio = counts["Core"] / total if total else 0.0
    print(
        json.dumps(
            {
                "total": total,
                "core": counts["Core"],
                "extended": counts["Extended"],
                "full": counts["Full"],
                "core_ratio": round(core_ratio, 6),
            },
            indent=2,
        )
    )


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def discover_asmdef_map(root: Path) -> Dict[Path, str]:
    assembly_map: Dict[Path, str] = {}
    for asmdef_path in sorted(root.rglob("*.asmdef")):
        try:
            payload = json.loads(asmdef_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            continue

        assembly_name = payload.get("name")
        if not assembly_name:
            continue

        assembly_map[asmdef_path.parent] = assembly_name
    return assembly_map


def resolve_assembly_name(source_path: Path, assembly_map: Mapping[Path, str]) -> str:
    current = source_path.parent
    while True:
        if current in assembly_map:
            return assembly_map[current]
        if current.parent == current:
            return ""
        current = current.parent


def expected_assembly_name_for_test(test: TestMethod) -> str:
    normalized_path = f"/{test.relative_path}"
    for fragment, assembly_name in EXPECTED_ASSEMBLY_BY_PATH.items():
        if fragment in normalized_path:
            return assembly_name
    return FEATURE_ASSEMBLY_NAME


def resolve_integration_subtype(test: TestMethod) -> str | None:
    relative = f"/{test.relative_path}"
    matches = []
    if "/Replay/" in relative or ".Replay." in test.namespace:
        matches.append("Replay")
    if "/Fuzz/" in relative or ".Fuzz." in test.namespace:
        matches.append("Fuzz")
    if "/Scenario/" in relative or ".Scenario." in test.namespace:
        matches.append("Simulation")

    for subtype in INTEGRATION_SUBTYPE_PRIORITY:
        if subtype in matches:
            return subtype
    return None


def structural_counts(tests: Sequence[TestMethod], assembly_map: Mapping[Path, str]) -> Dict[str, int]:
    counts: Dict[str, int] = Counter()
    for test in tests:
        assembly_name = resolve_assembly_name(test.absolute_path, assembly_map)
        counts[assembly_name] += 1
    return counts


@lru_cache(maxsize=None)
def read_source_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


@lru_cache(maxsize=None)
def file_imports(path: Path) -> Tuple[str, ...]:
    imports = []
    for line in read_source_text(path).splitlines():
        match = re.match(r"\s*using\s+([A-Za-z0-9_.]+)\s*;", line)
        if match:
            imports.append(match.group(1))
    return tuple(imports)


def file_uses_unity(path: Path) -> bool:
    imports = file_imports(path)
    return "UnityEngine" in imports or "UnityEditor" in imports


def is_core_l1_file(path: Path) -> bool:
    imports = set(file_imports(path))
    return "UnityEngine" not in imports and "UnityEditor" not in imports


def test_text(test: TestMethod) -> str:
    return "\n".join(test.attribute_lines) + "\n" + test.body


def text_contains_any(text: str, patterns: Sequence[str]) -> bool:
    return any(pattern in text for pattern in patterns)


@lru_cache(maxsize=None)
def file_has_mutable_static_state(path: Path) -> bool:
    pattern = re.compile(r"\bstatic\b(?!\s+readonly\b)[^;\n{}]*\b[A-Za-z_][A-Za-z0-9_]*\s*(?:=|;)")
    return bool(pattern.search(read_source_text(path)))


def uses_nonpublic_reflection(test: TestMethod) -> bool:
    return "BindingFlags.NonPublic" in test_text(test)


def is_execution_based_test(test: TestMethod) -> bool:
    text = test_text(test)
    invokes_execution = text_contains_any(text, EXECUTION_ENTRYPOINT_PATTERNS)
    observes_execution = text_contains_any(text, EXECUTION_CONTEXT_PATTERNS)
    references_execution_runtime = text_contains_any(
        text,
        ("TickPipeline", "TickRunner", "GameplayCompositionRoot", "GameplayBootstrapper"),
    )
    references_execution_chain = text_contains_any(text, EXECUTION_CHAIN_SYMBOLS)
    return invokes_execution or (references_execution_runtime and observes_execution) or (references_execution_chain and observes_execution)


def uses_structure_validation(test: TestMethod) -> bool:
    text = test_text(test)
    if text_contains_any(text, FORBIDDEN_CORE_STRUCTURE_PATTERNS):
        return True
    return text_contains_any(text, FORBIDDEN_CORE_COMPOSITION_SYMBOLS) and text_contains_any(
        text,
        ("GetMethod(", "ReturnType", "ParameterType", "IsAssignableFrom(", "GetExportedTypes(", "IsNotPublic"),
    )


def asserts_behavioral_outcomes(test: TestMethod) -> bool:
    return text_contains_any(test_text(test), BEHAVIOR_ASSERTION_PATTERNS)


def violates_core_determinism(test: TestMethod) -> bool:
    text = test_text(test)
    return text_contains_any(text, FORBIDDEN_CORE_DETERMINISM_PATTERNS) or file_has_mutable_static_state(test.absolute_path)


def is_mixed_responsibility_test(test: TestMethod) -> bool:
    return uses_structure_validation(test) and (is_execution_based_test(test) or asserts_behavioral_outcomes(test))


def is_core_candidate(test: TestMethod, assembly_name: str) -> bool:
    if test.mode != "EditMode":
        return False
    if assembly_name != FEATURE_ASSEMBLY_NAME:
        return False

    imports = set(file_imports(test.absolute_path))
    if any(forbidden in imports for forbidden in FORBIDDEN_CORE_IMPORTS):
        return False
    if "Game.Feature.Gameplay.Tests" in imports:
        return False

    combined = f"{read_source_text(test.absolute_path)}\n{test.body}"
    if any(symbol in combined for symbol in FORBIDDEN_CORE_SYMBOLS):
        return False
    if is_execution_based_test(test):
        return False
    if uses_structure_validation(test):
        return False
    if violates_core_determinism(test):
        return False

    return True


def compute_playmode_core_cap(core_l2_count: int, feature_count: int) -> int:
    return clamp(math.floor((core_l2_count + feature_count) * 0.05), 5, 30)


def clamp(value: int, minimum: int, maximum: int) -> int:
    return max(minimum, min(maximum, value))


def load_governance_history(path: Path) -> dict:
    if not path.exists():
        return {
            "schemaVersion": 1,
            "runs": [],
        }
    content = path.read_text(encoding="utf-8").strip()
    if not content:
        return {
            "schemaVersion": 1,
            "runs": [],
        }
    return json.loads(content)


def update_governance_history(
    path: Path,
    mode: str,
    candidate_debt_exceeded: bool,
    candidate_count: int,
    candidate_threshold: int,
) -> Tuple[dict, int]:
    history = load_governance_history(path)
    runs = history.setdefault("runs", [])
    runs.append(
        {
            "timestampUtc": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
            "mode": mode,
            "candidateDebtExceeded": candidate_debt_exceeded,
            "candidateCount": candidate_count,
            "candidateThreshold": candidate_threshold,
        }
    )
    history["runs"] = runs[-20:]
    write_json(path, history)
    return history, compute_candidate_debt_streak(history)


def compute_candidate_debt_streak(history: Mapping[str, object]) -> int:
    streak = 0
    for run in reversed(history.get("runs", [])):
        if run.get("mode") != STRICT_GOVERNANCE_MODE:
            continue
        if run.get("candidateDebtExceeded"):
            streak += 1
            continue
        break
    return streak


def build_governance_summary(
    root: Path,
    tests: Sequence[TestMethod],
    manifest: Mapping[str, object],
    mode: str,
) -> Tuple[GovernanceSummary, List[str], List[str]]:
    assembly_map = discover_asmdef_map(root / "Assets/_Features/Gameplay/Gameplay_Tests")
    counts = structural_counts(tests, assembly_map)

    core_l2_count = counts.get(CORE_ASSEMBLY_NAME, 0)
    core_l1_count = sum(
        1
        for test in tests
        if resolve_assembly_name(test.absolute_path, assembly_map) == CORE_ASSEMBLY_NAME and is_core_l1_file(test.absolute_path)
    )
    feature_count = counts.get(FEATURE_ASSEMBLY_NAME, 0)
    infrastructure_count = counts.get(INFRASTRUCTURE_ASSEMBLY_NAME, 0)
    replay_count = counts.get(INTEGRATION_REPLAY_ASSEMBLY_NAME, 0)
    fuzz_count = counts.get(INTEGRATION_FUZZ_ASSEMBLY_NAME, 0)
    simulation_count = counts.get(INTEGRATION_SIMULATION_ASSEMBLY_NAME, 0)

    manifest_tests = manifest.get("tests", [])
    playmode_core_count = sum(
        1
        for entry in manifest_tests
        if entry.get("mode") == "PlayMode" and entry.get("category") == "Core"
    )
    core_tests = [entry for entry in manifest_tests if entry.get("category") == "Core"]
    core_ratio = (len(core_tests) / len(manifest_tests)) if manifest_tests else 0.0
    playmode_core_cap = compute_playmode_core_cap(core_l2_count, feature_count)

    candidate_count = sum(
        1 for test in tests if is_core_candidate(test, resolve_assembly_name(test.absolute_path, assembly_map))
    )
    candidate_threshold = max(5, math.ceil(core_l2_count * 0.10))

    warnings: List[str] = []
    failures: List[str] = []

    if not (0.10 <= core_ratio <= 0.25):
        failures.append(f"Core ratio must stay within 10%-25% (current: {core_ratio:.2%}).")

    if playmode_core_count == 0:
        failures.append("Core must include at least one PlayMode integration test.")
    elif playmode_core_count > playmode_core_cap:
        failures.append(
            f"PlayMode Core count exceeds cap: {playmode_core_count} > {playmode_core_cap}."
        )

    for test in tests:
        assembly_name = resolve_assembly_name(test.absolute_path, assembly_map)
        expected_assembly = expected_assembly_name_for_test(test)
        rule_target = failures if mode == STRICT_GOVERNANCE_MODE else warnings
        if expected_assembly != assembly_name:
            rule_target.append(
                f"Assembly mismatch for {test.fully_qualified_name}: expected {expected_assembly}, found {assembly_name or '<none>'}"
            )

        subtype = resolve_integration_subtype(test)
        if subtype == "Replay" and assembly_name != INTEGRATION_REPLAY_ASSEMBLY_NAME:
            rule_target.append(f"Replay test is assigned outside replay assembly: {test.fully_qualified_name}")
        elif subtype == "Fuzz" and assembly_name != INTEGRATION_FUZZ_ASSEMBLY_NAME:
            rule_target.append(f"Fuzz test is assigned outside fuzz assembly: {test.fully_qualified_name}")
        elif subtype == "Simulation" and assembly_name != INTEGRATION_SIMULATION_ASSEMBLY_NAME:
            rule_target.append(f"Simulation test is assigned outside simulation assembly: {test.fully_qualified_name}")

        if (
            test.mode == "EditMode"
            and is_execution_based_test(test)
            and assembly_name
            not in {
                INTEGRATION_SIMULATION_ASSEMBLY_NAME,
                INTEGRATION_REPLAY_ASSEMBLY_NAME,
                INTEGRATION_FUZZ_ASSEMBLY_NAME,
                PLAYMODE_ASSEMBLY_NAME,
            }
        ):
            rule_target.append(f"Execution-based test is assigned outside Integration: {test.fully_qualified_name}")

    structural_issues: List[str] = []
    structural_issues.extend(check_core_assembly_rules(root, tests, assembly_map))
    structural_issues.extend(check_pure_support_rules(root))
    structural_issues.extend(check_infrastructure_rules(root, tests, assembly_map))
    if mode == STRICT_GOVERNANCE_MODE:
        failures.extend(structural_issues)
    else:
        warnings.extend(structural_issues)

    history_path = get_repo_paths(root)["governance_history_path"]
    history = load_governance_history(history_path)
    candidate_debt_streak = compute_candidate_debt_streak(history)
    candidate_debt_exceeded = candidate_count > candidate_threshold
    if mode == STRICT_GOVERNANCE_MODE:
        history, candidate_debt_streak = update_governance_history(
            history_path,
            mode,
            candidate_debt_exceeded,
            candidate_count,
            candidate_threshold,
        )

    if candidate_debt_exceeded:
        warnings.append(
            f"Core candidate debt is above threshold: candidates={candidate_count}, threshold={candidate_threshold}, streak={candidate_debt_streak}"
        )
        if mode == STRICT_GOVERNANCE_MODE and candidate_debt_streak >= 3:
            failures.append(
                f"Core candidate debt persisted for {candidate_debt_streak} strict runs (candidates={candidate_count}, threshold={candidate_threshold})."
            )

    summary = GovernanceSummary(
        mode=mode,
        core_l1_count=core_l1_count,
        core_l2_count=core_l2_count,
        feature_count=feature_count,
        infrastructure_count=infrastructure_count,
        integration_simulation_count=simulation_count,
        integration_replay_count=replay_count,
        integration_fuzz_count=fuzz_count,
        playmode_core_count=playmode_core_count,
        playmode_core_cap=playmode_core_cap,
        candidate_count=candidate_count,
        candidate_threshold=candidate_threshold,
        candidate_debt_streak=candidate_debt_streak,
    )
    return summary, warnings, failures


def check_core_assembly_rules(root: Path, tests: Sequence[TestMethod], assembly_map: Mapping[Path, str]) -> List[str]:
    issues: List[str] = []
    asmdef_path = root / "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core/Game.Core.Tests.asmdef"
    if asmdef_path.exists():
        payload = json.loads(asmdef_path.read_text(encoding="utf-8"))
        references = set(payload.get("references", []))
        allowed = {"Game.Feature.Gameplay", PURE_SUPPORT_ASSEMBLY_NAME}
        forbidden_references = sorted(reference for reference in references if reference not in allowed)
        if forbidden_references:
            issues.append(f"{CORE_ASSEMBLY_NAME} has forbidden references: {', '.join(forbidden_references)}")

    core_root = root / "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core"
    for source_path in sorted(core_root.rglob("*.cs")):
        imports = set(file_imports(source_path))
        for forbidden_import in FORBIDDEN_CORE_IMPORTS:
            if forbidden_import in imports:
                issues.append(f"{source_path.relative_to(root).as_posix()} imports forbidden Core dependency {forbidden_import}")
        if "Game.Feature.Gameplay.Tests" in imports:
            issues.append(f"{source_path.relative_to(root).as_posix()} imports legacy test support namespace")

    for test in tests:
        if resolve_assembly_name(test.absolute_path, assembly_map) != CORE_ASSEMBLY_NAME:
            continue
        if is_execution_based_test(test):
            issues.append(f"Core test executes runtime flow and must move to Integration: {test.fully_qualified_name}")
        if uses_structure_validation(test):
            issues.append(f"Core test validates structure/wiring and must move to Infrastructure: {test.fully_qualified_name}")
        if violates_core_determinism(test):
            issues.append(f"Core test violates determinism guardrails: {test.fully_qualified_name}")
        if is_mixed_responsibility_test(test):
            issues.append(f"Core test mixes structure validation with behavior/execution and must be decomposed: {test.fully_qualified_name}")
    return issues


def check_pure_support_rules(root: Path) -> List[str]:
    issues: List[str] = []
    asmdef_path = root / "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/Pure/Game.TestSupport.Pure.asmdef"
    if asmdef_path.exists():
        payload = json.loads(asmdef_path.read_text(encoding="utf-8"))
        references = set(payload.get("references", []))
        forbidden = sorted(reference for reference in references if reference in {"UnityEngine", "UnityEditor", UNITY_SUPPORT_ASSEMBLY_NAME, INFRASTRUCTURE_ASSEMBLY_NAME})
        if forbidden:
            issues.append(f"{PURE_SUPPORT_ASSEMBLY_NAME} has forbidden references: {', '.join(forbidden)}")

    support_root = root / "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/Pure"
    for source_path in sorted(support_root.rglob("*.cs")):
        imports = set(file_imports(source_path))
        if "UnityEngine" in imports or "UnityEditor" in imports:
            issues.append(f"{source_path.relative_to(root).as_posix()} imports Unity into {PURE_SUPPORT_ASSEMBLY_NAME}")
    return issues


def check_infrastructure_rules(root: Path, tests: Sequence[TestMethod], assembly_map: Mapping[Path, str]) -> List[str]:
    issues: List[str] = []
    asmdef_path = root / "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/Infrastructure/Game.TestInfrastructure.asmdef"
    if asmdef_path.exists():
        payload = json.loads(asmdef_path.read_text(encoding="utf-8"))
        references = set(payload.get("references", []))
        allowed = {"Game.Feature.Gameplay", FEATURE_ASSEMBLY_NAME}
        forbidden_references = sorted(reference for reference in references if reference not in allowed)
        if forbidden_references:
            issues.append(f"{INFRASTRUCTURE_ASSEMBLY_NAME} has forbidden references: {', '.join(forbidden_references)}")

    infra_root = root / "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/Infrastructure"
    for source_path in sorted(infra_root.rglob("*.cs")):
        text = source_path.read_text(encoding="utf-8")
        matches = sorted(symbol for symbol in FORBIDDEN_INFRASTRUCTURE_SYMBOLS if symbol in text)
        if matches:
            issues.append(
                f"{source_path.relative_to(root).as_posix()} uses forbidden infrastructure symbols: {', '.join(matches)}"
            )
    for test in tests:
        if resolve_assembly_name(test.absolute_path, assembly_map) != INFRASTRUCTURE_ASSEMBLY_NAME:
            continue
        if is_execution_based_test(test):
            issues.append(f"Infrastructure test executes runtime flow and must move to Integration: {test.fully_qualified_name}")
        if asserts_behavioral_outcomes(test):
            issues.append(f"Infrastructure test asserts gameplay behavior and must move to Integration: {test.fully_qualified_name}")
        if is_mixed_responsibility_test(test):
            issues.append(f"Infrastructure test mixes structure validation with behavior/execution and must be decomposed: {test.fully_qualified_name}")
    return issues


def format_governance_summary(summary: GovernanceSummary) -> List[str]:
    return [
        f"Governance mode: {summary.mode}",
        f"Core size: L1={summary.core_l1_count} L2={summary.core_l2_count}",
        f"Feature size: {summary.feature_count}",
        f"Infrastructure size: {summary.infrastructure_count}",
        f"Integration size: Simulation={summary.integration_simulation_count} Replay={summary.integration_replay_count} Fuzz={summary.integration_fuzz_count}",
        f"PlayMode core ratio: {summary.playmode_core_count}/{summary.playmode_core_cap}",
        f"Core candidate count: {summary.candidate_count} (threshold={summary.candidate_threshold}, streak={summary.candidate_debt_streak})",
    ]
