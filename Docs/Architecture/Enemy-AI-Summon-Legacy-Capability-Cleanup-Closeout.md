# Enemy AI Summon Legacy Capability Cleanup Closeout

Date: 2026-06-19 KST

## Purpose

Remove the empty legacy Utility capability reference from the production ArchetypeSummoner profile now that Summon execution is owned by the typed Summon BehaviorModule path.

This cleanup is asset-graph only. It does not retire generic Utility Summon code, synthetic Utility Summon tests, duplicate guards, replay/export fields, SourceEffectIndex / Effect metadata, or presentation/audio/VFX cue names.

## Production Graph

Before:

- `EnemyAi_ArchetypeSummoner`
  - `capabilityAssets`
    - `EnemyCapability_ArchetypeSummoner`, GUID `44788e5c202648d0bae1e8b5be647816`, `EnemyUtilityCapabilityAsset`, `effects: []`
    - `EnemyCapability_PassiveContact_Common`, GUID `bf094ca1fd8f4a378e18159ddaf0d4f0`, `ContactSameCellPassiveContactCapabilityAsset`, `attackRange: 1`
  - `behaviorModuleAssets`
    - `EnemySummonBehaviorModule_ArchetypeSummoner`, GUID `a73bf2a62ddc4cc88cc8587d38288135`

After:

- `EnemyAi_ArchetypeSummoner`
  - `capabilityAssets`
    - `EnemyCapability_PassiveContact_Common`, GUID `bf094ca1fd8f4a378e18159ddaf0d4f0`
  - `behaviorModuleAssets`
    - `EnemySummonBehaviorModule_ArchetypeSummoner`, GUID `a73bf2a62ddc4cc88cc8587d38288135`

Removed:

- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset.meta`
- Empty `Enemy_UtilitySummoner` folder and folder `.meta`, after folder GUID `8a2dc351924402d48a8b9d440706bfce` was confirmed self-only.

## Runtime Contract

Before cleanup:

- `TryGetSummonBehavior == true`
- `TryGetUtility == true`
- Utility `Effects.Count == 0`
- `TryGetPassiveContact == true`

After cleanup:

- `TryGetSummonBehavior == true`
- `TryGetUtility == false`
- `TryGetPassiveContact == true`
- Summon runtime continues to emit `SourceEffectIndex=0` / `Effect=0` for compatibility.

Replay/hash impact is bounded to the compiled capability inventory and absent Utility runtime slot. Summoned entity metadata, spawn ordering, Summon behavior state, `SummonCommitted`, `SummonSkipped`, `Final.SummonedEntities`, and `Final.EnemyDefinitionBindings` remain owned by the Behavior Summon path.

## Scans

Pre-change GUID scan:

- `44788e5c202648d0bae1e8b5be647816` appeared in the Summoner profile reference, the candidate `.meta`, and historical documentation.
- No other production profile, scene, prefab, catalog, editor fixed dependency, package, or project setting reference was found.

Post-delete GUID scan:

- Runtime-required references to `44788e5c202648d0bae1e8b5be647816`: `0`.
- Remaining references are documentation/history and the negative asset-contract assertion that verifies the deleted GUID is absent from the Summoner profile YAML.
- Folder GUID `8a2dc351924402d48a8b9d440706bfce`: `0` runtime-required references; remaining text references are this closeout record.

## Charge No-Op

Charge cleanup is a no-op.

- `EnemyAi_Charger.asset` still has exactly one `EnemyChargeBehaviorModuleAsset`.
- `EnemyAi_Charger.asset` still has common PassiveContact capability GUID `bf094ca1fd8f4a378e18159ddaf0d4f0`.
- No Charge-specific legacy capability exists.
- Charge Behavior owns timing/execution; PassiveContact owns same-cell damage.

## Validation

Focused validation:

| Command | Result |
| --- | --- |
| `git diff --check` | exit 0 |
| `./run_tests.sh full --filter EnemyAiProfileAssetContractTests` | exit 0; EditMode 24 total / 0 failed; PlayMode 0 total / 0 failed |
| `./run_tests.sh full --filter EnemyAiRuntimeDefinitionGuardTests` | exit 0; EditMode 18 total / 0 failed; PlayMode 0 total / 0 failed |
| `./run_tests.sh full --filter StageRuntimeBuilderTests` | exit 0; EditMode 69 total / 0 failed; PlayMode 0 total / 0 failed |
| `./run_tests.sh full --filter CampaignStageAssets_WithSummonArchetypes_HaveRuntimeAndPresentationArchetypeCatalogs` | exit 0; EditMode 1 total / 0 failed; PlayMode 0 total / 0 failed |
| `./run_tests.sh --integration-simulation --filter BehaviorSummon` | exit 0; EditMode 27 total / 0 failed |
| `./run_tests.sh --integration-simulation --filter MigratedSummon` | exit 0; EditMode 24 total / 0 failed |
| `./run_tests.sh --integration-replay --filter MigratedSummon` | exit 0; EditMode 1 total / 0 failed |
| `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_` | exit 0; EditMode 12 total / 0 failed |
| `./run_tests.sh full --filter ChargePassiveContact_StillUsesTargetSelection` | exit 0; EditMode 1 total / 0 failed; PlayMode 0 total / 0 failed |
| `./run_tests.sh full --filter RocketFaceChargeRuntimeContractTests` | exit 0; EditMode 17 total / 0 failed; PlayMode 0 total / 0 failed |
| `./run_tests.sh core` | exit 0; Core EditMode 189 total / 0 failed; Core PlayMode 33 total / 0 failed |

Fresh full:

- Pre-run identity: HEAD `04647b96c4d870ec44c2e3564004e39c588ea974`, branch `pr/enemy-ai-retired-melee-runtime-removal`, started `Fri Jun 19 18:51:22 KST 2026`.
- `./run_tests.sh full`: exit 1; Full EditMode 5744 total / 36 failed; Full PlayMode was not generated because EditMode failed.
- Preserved artifacts: `TestResults/Preserved/post-summon-legacy-capability-cleanup-04647b96-20260619-185508/`.
- Identity comparison against `TestResults/Preserved/post-enemy-only-remediation-48d177ce-20260618-195648/wsl-unity-full-editmode.xml`: 0 new failed identities, 0 removed failed identities.
- New Enemy/Summon/Charge/PassiveContact/Utility/RocketFace/ArchetypeSummoner/StageRuntimeBuilder failed identities vs previous snapshot: 0.

Manual play smoke:

- Not run in this CLI session; no interactive Unity/editor play session was available here.

## Rollback

Rollback order:

1. Restore `Enemy_UtilitySummoner` folder and folder `.meta` if needed.
2. Restore `EnemyCapability_ArchetypeSummoner.asset`.
3. Restore `EnemyCapability_ArchetypeSummoner.asset.meta`.
4. Restore the `EnemyAi_ArchetypeSummoner.asset` capability reference to GUID `44788e5c202648d0bae1e8b5be647816`.
5. Restore test expectations and this closeout/README link.
6. Reimport/compile through Unity and rerun focused validation.

The asset/meta restore must happen before restoring the profile reference to avoid a missing-reference intermediate state.

## Non-Goals

- Do not remove `EnemyUtilityEffectKind.SummonMinion`.
- Do not remove Utility Summon authoring/runtime compatibility.
- Do not remove Utility + Behavior duplicate guards.
- Do not alter Summon Behavior tuning.
- Do not alter common PassiveContact.
- Do not alter Charger, Charge BehaviorModule, or Charge execution assets.
- Do not rename replay/export metadata or presentation/audio/VFX cues.
