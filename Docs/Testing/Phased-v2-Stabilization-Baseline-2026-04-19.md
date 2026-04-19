# Phased v2 Stabilization Baseline 2026-04-19

이 문서는 `Phased v2` stabilization + source-contract hardening slice의 최소 evidence만 남긴다.

## Commands

```bash
./run_tests.sh core
./run_tests.sh full
python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/users/user/2026teamproject_j2m --mode strict
```

## Before / After Summary

| Command | Scope | Before | After | Delta | Disposition |
| --- | --- | --- | --- | --- | --- |
| `./run_tests.sh core` | mandatory gate | Core EditMode `13/13`, Core PlayMode `2/2`, green | Core EditMode `13/13`, Core PlayMode `2/2`, green | unchanged | pass |
| `./run_tests.sh full` | Unity Full EditMode | `1016 total / 93 failed` | `1017 total / 90 failed` | `+1 total`, `-3 failed` | pass; baseline remains red but direct phased-related failures were removed |
| `./run_tests.sh full` | direct phased-related touched slice: `EnemyAiScenarioTests`, `ModifierCapabilityGeneralizationTests`, `SpatialStateResolverTruthTableTests`, `ReservationReadModelContractTests` | `3` direct failures (`EnemyAiScenarioTests 1`, `ModifierCapabilityGeneralizationTests 1`, `SpatialStateResolverTruthTableTests 1`) | `0` failures | `-3 direct failures` | pass |
| `./run_tests.sh full` | unrelated pre-existing failures outside the phased slice | `90` | `90` | unchanged | pass; no new unrelated failure was introduced by this slice |

## Artifacts

- `TestResults/wsl-unity-core-editmode.xml`
- `TestResults/wsl-unity-core-playmode.xml`
- `TestResults/wsl-unity-full-editmode.xml`
- `TestResults/wsl-unity-full-editmode.log`

## Notes

- `./run_tests.sh full` still stops at Full EditMode because the baseline remains red; Full PlayMode was not reached in this slice.
- `strict` stratification governance is still red and is not exit-gating for this slice.
- Known pre-existing, not exit-gating for this slice:
  - execution-placement debt in `ModifierCapabilityGeneralizationTests`
  - override/category mismatch debt already reported by the strict checker
