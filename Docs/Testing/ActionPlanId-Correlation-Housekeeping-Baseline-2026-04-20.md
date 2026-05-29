# ActionPlanId Correlation Housekeeping Baseline 2026-04-20

이 문서는 ActionPlanId canonical correlation migration 이후 남은 housekeeping만 기록한다. runtime behavior, reservation/export contract, fixed tick determinism, WorldState authoritative contract, Finalize no-recheck, `IntentId` carry-forward rule은 이번 단계에서 변경하지 않았다.

## Commands

```bash
python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/users/user/2026teamproject_j2m --mode soft
python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/users/user/2026teamproject_j2m --mode strict
python3 Tools/check_gameplay_action_plan_correlation_migration.py --root /mnt/c/users/user/2026teamproject_j2m
./run_tests.sh core
./run_tests.sh full
```

## Classification Decision

| Test row | Decision | Why |
| --- | --- | --- |
| `DamageResolutionRecord_ActionPlanId_RemainsWithoutLegacyGroupIdAlias` | `Extended` | feature-assembly deterministic carrier-contract row; not a Core assembly gate row |
| `DestroyResolutionRecord_ActionPlanId_RemainsWithoutLegacyGroupIdAlias` | `Extended` | feature-assembly deterministic carrier-contract row; not a Core assembly gate row |
| `DelayedAttackEffectRecord_SourceActionPlanId_RemainsWithoutLegacySourceActionGroupIdAlias` | `Extended` | feature-assembly deterministic carrier-contract row; not a Core assembly gate row |
| `ActionGroup_GroupId_RemainsCompatibilityIrVocabulary` | `Extended` | preserves the compatibility IR vocabulary boundary while result carrier aliases stay removed |

## Before / After Summary

| Scope | Before | After | Disposition |
| --- | --- | --- | --- |
| stratification mismatch rows introduced by this migration | `ActionPlanCorrelationContractTests` 3 rows reported as `Core != Extended` | those exact 3 rows no longer appear | pass |
| strict stratification overall status | red | red | expected; pre-existing execution-placement debt and older unrelated mismatch debt remain |
| ActionPlanId correlation governance | pass | pass | asserts removed result-carrier aliases stay absent while canonical IDs remain |
| `./run_tests.sh core` | green | green | pass |
| `ActionPlanCorrelationDocumentationTests` rows in `full` EditMode XML | absent | `4/4` passed | pass |
| `./run_tests.sh full` overall status | red baseline | red baseline (`1041 total / 92 failed`) | expected; touched docs rows passed and baseline red remains unrelated |
| runtime/data shape | canonical `ActionPlanId` contract active | result-carrier compatibility alias properties removed; canonical `ActionPlanId` / `SourceActionPlanId`, `IntentId`, and `ActionGroup.GroupId` remain | pass |

## Exact mismatch rows removed

- `Game.Feature.Gameplay.Tests.Unit.ActionPlanCorrelationContractTests.DamageResolutionRecord_ActionPlanId_AliasesLegacyGroupId`
- `Game.Feature.Gameplay.Tests.Unit.ActionPlanCorrelationContractTests.DestroyResolutionRecord_ActionPlanId_AliasesLegacyGroupId`
- `Game.Feature.Gameplay.Tests.Unit.ActionPlanCorrelationContractTests.DelayedAttackEffectRecord_SourceActionPlanId_AliasesLegacySourceActionGroupId`

## Alias removal closeout

- `DamageResolutionRecord.GroupId`, `DestroyResolutionRecord.GroupId`, and `DelayedAttackEffectRecord.SourceActionGroupId` are removed from the result carrier API.
- `DamageResolutionRecord.ActionPlanId`, `DestroyResolutionRecord.ActionPlanId`, `DelayedAttackEffectRecord.SourceActionPlanId`, `DamageResolutionRecord.IntentId`, `DestroyResolutionRecord.IntentId`, and `ActionGroup.GroupId` remain available.

## Notes

- parity 테스트 3건은 runtime bug가 아니라 metadata drift였다. 세 row는 `RunTick`/pipeline execution이 아니라 record carrier alias parity만 검증한다.
- structured trace `Plan=` / `SourcePlan=`는 canonical structured trace surface다.
- free-form `G=`는 compatibility token in free-form event log다. current `ActionPlanId` value를 mirror하지만 old semantic GroupId revival이 아니다.
- 새 테스트/도구는 `G=`를 canonical parser surface로 읽지 않는다.

## Handoff

- 다음 구현자는 trace/debug/log를 볼 때 typed runtime carrier와 structured trace `Plan=` / `SourcePlan=`를 primary source-of-truth로 본다.
- free-form `G=` rename, legacy string assertion rewrite, broader free-form logging cleanup은 later logging cleanup 단계로 넘긴다.
