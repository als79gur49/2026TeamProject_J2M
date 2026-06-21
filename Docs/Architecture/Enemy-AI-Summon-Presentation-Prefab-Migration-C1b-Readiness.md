# Enemy AI Summon Presentation Prefab Migration C1b Readiness

## Executive Verdict

C1b is GO after C1a acceptance closure on revision `a64384f0d654e7a03cfe82cd0c932313ac2a8337`.

Selected option: GUID-preserving rename of `EnemyUtilityScalePulsePresentationDriver` to `EnemySummonScalePulsePresentationDriver`, with the production JPeter prefab migrated from legacy raw `utilityKind: 3` to component-presence Summon presentation binding.

Confidence: high. Repository evidence showed one production raw `utilityKind: 3` hit and no Gravity ownership inside the scale pulse driver.

## Driver Ownership

Source before migration:

- `Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyUtilityScalePulsePresentationDriver.cs`
- namespace: `Game.Feature.Gameplay.Host`
- assembly: `Game.Feature.Gameplay.EnemyPresentation`
- MonoBehaviour: yes
- script GUID: `d5bc7f8cc6194d2882c9cdb56e96d289`

The driver consumes `EnemyViewPresentationState` Summon lifecycle flags for windup, recover, cancel, death cleanup, and semantic pause. The previous `utilityKind` field existed only as a raw-3 compatibility adapter. Gravity presentation is owned by `EnemyUtilityCooldownAuraVfxAuthoring`, not by this scale pulse driver.

## Reference Graph

Tracked production serialized hits before migration:

| Path | Script GUID | Serialized value | Semantic | Action |
| --- | --- | --- | --- | --- |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_JPeter.prefab` | `d5bc7f8cc6194d2882c9cdb56e96d289` | `utilityKind: 3` | Summon scale pulse compatibility | migrate |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_DrSaturn.prefab` | `f18cc0d83f3741f087d10f75a8d2d59c` | `utilityKind: 2` | Gravity aura VFX authoring | keep |

The JPeter prefab is referenced by `EnemyPresentationCatalog_CampaignMain.asset` for the `j_peter` archetype. DrSaturn remains the Gravity preservation control.

Ignored `Assets/InitTestScene*.unity` files may contain stale test-run metadata; they are not tracked repository assets and are not C1b migration inputs.

## History

- `cbe942a5 feat: Gameplay/EnemyPresentation - JPater 소환 스케일 예고 연출 추가` introduced the scale pulse driver and JPeter raw `utilityKind: 3` when `EnemyUtilityPresentationKind.SummonMinion = 3` existed.
- `bc27e13d refactor: Gameplay/Presentation - Summon 표시 carrier 분리` removed the active enum member and added the bounded raw-3 adapter for asset compatibility.

Raw `3` is therefore asset-only Summon presentation compatibility. It is unrelated to active Utility Summon execution and unrelated to `RetiredSummonMinion = 0`.

## Selected Serialization Strategy

- Move the `.cs` and `.cs.meta` together so script GUID `d5bc7f8cc6194d2882c9cdb56e96d289` is preserved.
- Rename the class to `EnemySummonScalePulsePresentationDriver`.
- Add `[MovedFrom(false, "Game.Feature.Gameplay.Host", "Game.Feature.Gameplay.EnemyPresentation", "EnemyUtilityScalePulsePresentationDriver")]` for Unity API updater/class rename safety.
- Remove `utilityKind` instead of renaming it. `[FormerlySerializedAs]` is not needed because the old field is retired rather than migrated into a new field.
- Do not rely on raw enum numeric conversion. The new typed contract is component presence on the JPeter Summon prefab.

## Operation Order

1. Preserve C1a fresh full acceptance artifact.
2. Move driver `.cs` and `.meta` with GUID unchanged.
3. Rename the driver class and remove raw-3 adapter logic.
4. Update runtime component lookups to the new type.
5. Migrate JPeter prefab YAML: remove `utilityKind: 3`, update editor class identifier, keep script GUID and tuning.
6. Add replacement contract tests for Summon component binding, no raw `3`, tuning preservation, and Gravity raw `2` preservation.
7. Run static scans and focused tests.
8. Run fresh full and preserve artifacts.

## C2 Boundary

C1b does not rename or migrate `Effect`, `EffectIndex`, `SourceEffectIndex`, replay/hash/export vocabulary, spawn materialization vocabulary, or numeric compatibility value `0`. Those remain deferred to C2.
