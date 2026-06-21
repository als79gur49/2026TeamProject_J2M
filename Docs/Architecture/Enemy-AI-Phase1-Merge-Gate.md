# Enemy AI Phase 1 Merge Gate

This checklist locks Phase 1 behavior-module work before merge. It does not add
features and does not start Phase 2.

## Checklist

- Charge resolver profiles must fail fast when no Charge behavior module is
  declared.
- Non-charge profiles must fail asset-contract validation when they declare an
  unused Charge behavior module.
- Old Core charge-timing API residue must be absent from production runtime
  surfaces.
- Repository YAML must not contain `chargeTimingSettings:`.
- Forbidden follow-up patterns must be absent:
  - unified logic-module serialized root
  - behavior-module set authoring companion
  - generic world-state update hook
- Core validation evidence must be refreshed on the same revision with
  `./run_tests.sh core`.
- UI validation must be reported as not run unless UI files changed and
  `./run_tests.sh ui` was executed.
- Full/project-wide green must not be claimed unless the matching broad lane ran
  and passed on the same revision.

## Requirement Matrix

| Requirement | Gate |
| --- | --- |
| Core owns common and locomotion only | Core serialized-field asset contract |
| Charge timing is not a Core lane | old API scan and YAML scan |
| Charge resolver requires Charge behavior | compiler/runtime fail-fast tests |
| BehaviorModule has only Charge in Phase 1 | behavior key duplicate and unknown-key validation |
| Capability lane remains active | profile root contract keeps `capabilityAssets` |
| Utility and Summon are not migrated | audit-only follow-up, no runtime migration |
| Phase 2 is not started | forbidden pattern scan |

## Reviewer Focus

- `EnemyAiProfileCompiler` behavior compile and requirement validation
- `EnemyAiRuntimeDefinition.Behaviors` and `TryGetChargeBehavior`
- `EnemyLogic` charge resolver construction and charge runtime timing source
- `EnemyAiProfileAssetContractTests` repository profile guards
- Standard Charge behavior module/profile assets and paired `.meta` files

## Evidence Template

Use this wording in merge reports:

- Tests run: `./run_tests.sh core`
- Tests not run: `./run_tests.sh ui` when UI did not change
- Scan evidence: old API residue, YAML charge timing, deleted Charge variant
  GUID/path residue, and forbidden pattern scans
- Known baseline: full lane is documented red unless rerun and proven otherwise

Do not use broad claims such as project-wide green, full lane green, full
regression closed, or all regressions fixed without matching broad-lane evidence.
