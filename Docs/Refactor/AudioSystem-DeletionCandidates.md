# AudioSystem Deletion Candidate Report

Investigation date: 2026-06-06 KST

This report is a deletion plan input. It records the investigation snapshot before any deletion execution.

Update note, 2026-06-16 KST:

- The verified orphan audio asset cleanup PR already removed `Game_Clear.m4a`, `Game_Over.m4a`, `DrSaturn_Act_Def.asset`, `RocketFace_Act_Def.asset`, and their matching `.meta` files.
- The active `.wav`/source clips and active monster definition chains remain present.
- Completed cleanup targets are historical evidence only and are no longer active deletion candidates.

Update note, 2026-06-17 KST:

- Product/content owner confirmed end-credit content is unused.
- The owner-approved `World_EndCredit.ogg` cleanup removed `Assets/_Shared/Audio/Clips/Bgm/World_EndCredit.ogg` and its matching `.meta` file.
- BGM runtime code, `BgmProfile`, `AudioDefinition`, stage metadata, `SceneBgmRequestSource`, and `BgmFlowCoordinator` were unchanged.

Decision vocabulary:

- `KEEP_CANONICAL`: current runtime, scene, prefab, or public contract path.
- `KEEP_STAGE_COMPANION`: catalog-owned stage companion content that must remain while its owner entry/reference remains in stage governance.
- `KEEP_RESERVED`: currently hidden or future-facing, but explicitly governed.
- `DELETE_SAFE`: no C# reference, no serialized GUID reference beyond owned pair, no scene/prefab/asset reference, no Resources/Addressables/string-path load, no test/docs canonical contract.
- `DELETE_AFTER_TEST_UPDATE`: production runtime is not using it, but tests/docs still pin it or an adjacent asset chain.
- `REFACTOR_NOT_DELETE`: connected path exists, but ownership or naming should be corrected rather than deleted.
- `DEFER_DECISION`: more product/content decision is needed.
- `COMPLETED_REMOVED`: already removed by a verified asset-only cleanup PR; retained source clips or adjacent active definitions stay canonical.
- `BLOCKED_CONTENT_INTENT`: do not remove based on reference count alone; product/content or stage-content audit is required first.

## Summary Counts

Count convention:

- Counts exclude `.meta` files unless explicitly stated.
- Definition/clip pairs are counted by asset file, not by logical pair.
- `World_EndCredit.ogg` was technically unreferenced and is now removed after product/content owner confirmation.

| Decision | Count | Notes |
|---|---:|---|
| KEEP_CANONICAL | 32 | Shared runtime, BGM flow, gameplay host audio maps/controllers, UI SFX/settings bridge, active scene/bootstrap assets. |
| KEEP_STAGE_COMPANION | 1 | AUD-012 `legacy-stage-5-1_Audio.asset` is a catalog-owned archived stage companion. |
| KEEP_RESERVED | 8 | `Crossfade`, `AudioBinding.Policy`, hidden channels, symbolic BGM contract, persistence internals. |
| DELETE_SAFE | 18 | Pure orphan clip/assets or orphan definitions with only their owned clip reference; excludes completed cleanup and content-risk items. |
| DELETE_AFTER_TEST_UPDATE | 0 | Previous orphan definition targets were removed by the verified asset-only cleanup PR. |
| REFACTOR_NOT_DELETE | 5 | Production `_Test` naming and stage BGM/scene BGM content naming cleanup. |
| DEFER_DECISION | 5 | Optional/future enum members or content cues that are valid but not always emitted. |
| BLOCKED_CONTENT_INTENT | 0 | No active content-intent-blocked audio deletion candidates remain from this snapshot. |
| COMPLETED_REMOVED | 5 | Verified orphan audio cleanup removed two `.m4a` clips, two orphan monster definitions, and owner-approved `World_EndCredit.ogg`. |

## Candidate Table

| Decision | Path/Symbol/Asset | Layer | Evidence | Serialized GUID refs | Runtime refs | Test refs | Risk | Required Action |
|---|---|---|---|---|---|---|---|---|
| KEEP_CANONICAL | `IAudioService`, `AudioRuntimeInstaller`, `AudioRuntimeRoot`, `AudioManager`, `AudioPlaybackService`, `AudioMixingService` | Shared Runtime | Architecture docs name these canonical; `AudioRuntimeInstaller` serialized in `MainMenuScene` and `UIAudioScene`; source search shows feature paths use ports, not `AudioManager.Instance`. | installer script refs in both scenes | runtime bootstrap | `AudioArchitectureTests`, `AudioRuntimePlayModeTests` | High if removed | Preserve. |
| KEEP_CANONICAL | `PlayerPrefsAudioSettingsStore` | Settings | `AudioManager.InitializeRuntime` creates `AudioMixingService(... new PlayerPrefsAudioSettingsStore())`; settings bridge flush uses `IAudioSettingsService.FlushSettings`. | none | default persistence | `AudioSettingsBridgeTests`, settings UI tests | Medium | Preserve unless replacing persistence store in same PR. |
| KEEP_RESERVED | `AudioRuntimeExternalRootRegistry` | BGM bootstrap | BGM docs define bootstrap plumbing only; `AudioRuntimeInstaller` uses it for `PreferRegisteredPersistentRuntime`; no direct feature service locator use found. | none | persistent runtime access seam | `BgmFlowArchitectureTests`, `PersistentBgmFlowPlayModeTests` | High | Preserve. |
| KEEP_RESERVED | `AudioBinding.Policy` / `AudioPlaybackPolicy` | Binding policy seam | Grade 1 reserved future seam; docs state v1 must be null; `AudioBindingDiagnostics` validates reserved seam; tests assert reserved/null behavior. | embedded null rid in maps/profiles | validation only | `AudioArchitectureTests`, `GameplayAudioOverlapRefactorTests` | Medium | Preserve, do not populate. Deletion is prohibited without a migration plan because SerializeReference/public-surface risk exists. |
| KEEP_RESERVED | `AudioChannel.Ui`, `Voice`, `Ambience` | Hidden channels | Grade 1 hidden channel/schema slots; docs state hidden internal channels; `PlayerPrefsAudioSettingsStore` persists all channels; UI settings exposes only `Main/Bgm/Sfx`; `Ui` effective mix follows `Sfx` settings while `Voice`/`Ambience` do not. | enum serialized value possible | mix/settings | `AudioRuntimePlayModeTests` and `UiAudioUserSettingsPolicyTests` cover the `Ui` Sfx-setting dependency | High | Preserve. Do not remove `Voice` or `Ambience` from playback asset reference count alone; PlayerPrefs/settings schema migration would be required. |
| KEEP_RESERVED | `BgmTransitionMode.Crossfade` | BGM transition governance | `BgmTransitionMode` comments mark future multi-source runtime; `BgmFlowCoordinator` warns and falls back; docs reserve it. | serialized enum possible in profiles | fallback path | `BgmFlowRuntimeTests`, `AudioRepositoryAssetSmokeCoreTests` | Medium | Preserve. |
| KEEP_CANONICAL | `BgmTransitionMode.FadeOutIn` | BGM transition execution | Docs and `BgmFlowCoordinator.ResolvePlaybackTransition` execute it; `AudioPlaybackService` has request-based fade path. BGM transition policy must be verified against current active code/assets before any cleanup. | BGM profiles may serialize mode | active runtime | BGM flow tests | High | Preserve. This is not part of orphan asset deletion cleanup. |
| KEEP_CANONICAL | `GlobalAudioFlowBootstrap`, `GlobalAudioFlowRoot`, `BgmFlowCoordinator`, `SceneBgmRequestSource`, `BgmProfile` | BGM flow | Scenes serialize bootstrap/request source; `SceneBgmRequestSource.Start` delegates to coordinator; no direct scene `PlayBgm`. | scene refs | active runtime | BGM architecture/runtime/playmode tests | High | Preserve. |
| REFACTOR_NOT_DELETE | `HorrorVol2FactoryMain_BgmProfile` / `HorrorVol2FactoryMain_BgmDef` / clip | BGM content | GUID reverse lookup: profile serialized in `UIAudioScene`; definition referenced by profile; clip referenced by definition. Production name is normalized. | profile in scene; def in profile; clip in def | scene entry BGM | scene contract tests | Medium | Preserve; do not delete. |
| KEEP_CANONICAL | `StageAudioDefinition`, `StageAudioRuntimeRequestSource`, `BgmRequestRouter` | Stage gameplay BGM companion path | Stage validator validates direct `gameplayBgm` profile metadata; runtime source submits priority requests to the router; flow coordinator executes playback. | `*_Audio.asset` companions serialized from each stage entry | stage audio runtime source | `CampaignStageFlowTests`, `BgmFlowArchitectureTests`, `StageAudioDefinitionValidationTests` | High | Preserve; treat disconnects as integration issues, not deletion. |
| KEEP_STAGE_COMPANION | AUD-012 `Assets/_Features/Stages/Content/Campaigns/campaign-main/Levels/level-01/Stages/legacy-stage-5-1/legacy-stage-5-1_Audio.asset` | Archived stage audio companion | Stage companion schema audit classifies this as `KEEP_STAGE_COMPANION`. `StageContentEntry.audioDefinition` on `legacy-stage-5-1_Entry.asset` directly references it, and `CampaignMain_StageCatalog.asset` includes that owner entry. `mode: None` with a null profile is an intentional no-gameplay-BGM marker. | owner entry GUID `df156370538abbdf66d15215fb0d804e`; asset GUID `c0bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb` | stage audio runtime request path and stage governance validation | Stage content catalog/pre-build validation and `StageAudioDefinitionValidationTests` | High | Preserve. Future removal is blocked unless a separate stage governance migration first removes the owner entry/reference and passes catalog validation. |
| KEEP_CANONICAL | `GameplayAudioSemanticId` six required members and `GameplayAudioMap_CampaignV1` | Gameplay core one-shot | Map has all six required entries; map serialized in `UIAudioScene`; planner emits damage/exit only from `TickResult.PresentationData`. | scene map ref | active host presentation | `AudioArchitectureTests`, `GameplayAudioHostOrchestrationTests` | High | Preserve. |
| REFACTOR_NOT_DELETE | `GameplayAudioMap_CampaignV1` name | Gameplay core one-shot content | Serialized in production scene; tests load by path; production name is normalized. | `UIAudioScene` | active runtime | path-pinned tests | Medium | Preserve; do not delete. |
| KEEP_CANONICAL | `Player_S1_GameplayActionAudioProfile` | Gameplay action audio | Serialized by `Player_S1.prefab`; planner emits Push/Flip moments; authoring validates profile. | player prefab ref | active player action audio | `GameplayActionAudioRuntimeTests` | High | Preserve. |
| REFACTOR_NOT_DELETE | `Player_S1_GameplayActionAudioProfile` name | Gameplay action content | Production prefab-local content with normalized name. | player prefab ref | active runtime | path-pinned tests | Medium | Preserve; do not delete. |
| REMOVED_CURRENT_POLICY | `GameplayActionAudioMoment.Execute`, `Recovery`, optional null action profile entries for removed moments | Gameplay action audio | Action governance now removes these moments from the public surface and planner emission; gameplay execute/recovery timeline remains. | serialized enum values reserved | no action-audio runtime | action audio tests | Medium | Keep removed unless a future public-surface decision reintroduces them. |
| KEEP_CANONICAL | `BlockAudioMap_PlayerSounds`, `PlayerLocomotionAudioMap_PlayerSounds` | Gameplay host audio lanes | Both maps serialized in `UIAudioScene`; host factory attaches runtimes when fields are assigned; tests exist for each lane. | `UIAudioScene` | active host presentation | `BlockAudioRuntimeTests`, `PlayerLocomotionAudioRuntimeTests` | High | Preserve. |
| REFACTOR_NOT_DELETE | normalized block/locomotion map names | Gameplay host content | Production scene serialized references exist; names are normalized. | `UIAudioScene` | active runtime | lane tests | Medium | Preserve; do not delete. |
| KEEP_CANONICAL | `TopologyAudioMap_ObjectSounds`, `GravityFieldAudioMap_ObjectSounds`, `TileFeatureAudioMap_ObjectSounds` | Gameplay host audio lanes | Maps are serialized in `UIAudioScene` or referenced through host path; required cue validation exists. | scene refs for topology/gravity; tile-feature host path | active runtime | topology/gravity/tile-feature audio tests | High | Preserve. |
| KEEP_CANONICAL | Enemy audio profiles under `Stages/.../Enemy/AudioProfiles` | Enemy presentation audio | Each profile serialized by enemy prefab; host has `EnemyAudioPresentationController`; tests load profiles and definitions. | enemy prefab refs | active host presentation | `EnemyAudioRuntimeTests` | High | Preserve. |
| KEEP_CANONICAL | `UiAudioCueMap_V1` and all 18 `UiAudioCueId` entries | UI SFX | Cue map serialized in `MainMenuScene` and `UIAudioScene`; validation requires all enum values; runtime/tests call expanded cue set. | two scene refs | active UI flow/local/HUD/transition | `UiAudioSfxContractTests`, `UIFlowCoordinatorTests`, `MainMenuUiAudioFeedbackTests`, `UiScreenRuntimeAudioCueTests` | High | Preserve. |
| KEEP_CANONICAL | `UiAudioPortAdapter`, `AudioSettingsPortAdapter`, `UIAudioChannelMapper`, `AudioSettingsLifecycleRelay` | UI audio/settings bridge | Runtime installers construct adapters; mapper policy is tested; lifecycle relay flushes pause/quit. These are architecture boundaries that keep UI.Application from directly depending on Shared runtime. | installer script refs in scenes | active UI composition | `AudioSettingsBridgeTests`, `MainMenuAudioSceneContractTests` | High | Preserve. |
| COMPLETED_REMOVED | `World_EndCredit.ogg` | AudioClip | Product/content owner confirmed end-credit content is unused. Pre-deletion GUID reverse lookup found only its `.meta`; no `AudioDefinition`, BGM profile, scene request source, stage metadata, or path string refs were found in scoped search. | removed | none | none | Low | Historical cleanup evidence only; BGM runtime/code/profile/stage metadata remained unchanged. |
| DELETE_SAFE | `cre_Nebulus_dead.wav` | AudioClip | GUID reverse lookup found only `.meta`; `Nebulous_Death_Def` uses another clip; no path string refs found. | none | none | none | Low | Delete clip and `.meta`. |
| DELETE_SAFE | `cre_bot_dead.wav`, `cre_bot_move.wav` | AudioClip | GUID reverse lookup found only `.meta`; SecBot definitions use current authored clips; no path refs found. | none | none | none | Low | Delete clips and `.meta`. |
| DELETE_SAFE | `Sucked-into-the-Black-Hole-1_TTX041201_Test.wav` | AudioClip | GUID reverse lookup found only `.meta`; no definition references it; `_Test` name and no path refs. | none | none | none | Low | Delete clip and `.meta`. |
| DELETE_SAFE | `Archery 6073_50_1_Test_Def.asset` + clip | SFX definition/clip pair | Definition has zero external serialized refs; clip is referenced only by that definition; no test/path refs found except asset self. | def none; clip only def | none | none | Low | Delete definition, clip, and both `.meta` files together. |
| DELETE_SAFE | `LowThudPunch FS040206_Test_Def.asset` + clip | SFX definition/clip pair | Definition zero external refs; clip only referenced by definition; `_Test` name. | def none; clip only def | none | none | Low | Delete pair. |
| DELETE_SAFE | `Monster Attack 3_Test_Def.asset` + clip | SFX definition/clip pair | Definition zero external refs; clip only referenced by definition; no runtime map/profile. | def none; clip only def | none | none | Low | Delete pair. |
| DELETE_SAFE | `Monster Laugh 1_Test_Def.asset` + clip | SFX definition/clip pair | Definition zero external refs; clip only referenced by definition. | def none; clip only def | none | none | Low | Delete pair. |
| DELETE_SAFE | `PunchesGruntsPunch FS040901_Test_Def.asset` + clip | SFX definition/clip pair | Definition zero external refs; clip only referenced by definition. | def none; clip only def | none | none | Low | Delete pair. |
| DELETE_SAFE | `Sci-Fi-Special-Fx_GEN-HD4-43862_Test_Def.asset` + clip | SFX definition/clip pair | Definition zero external refs; clip only referenced by definition. | def none; clip only def | none | none | Low | Delete pair. |
| DELETE_SAFE | `SpacePod 8003_90_1_Test_Def.asset` + clip | SFX definition/clip pair | Definition zero external refs; clip only referenced by definition. | def none; clip only def | none | none | Low | Delete pair. |
| COMPLETED_REMOVED | `Game_Clear.m4a` | AudioClip | Removed in verified orphan audio asset cleanup PR. `Game_Clear.wav` remains present. | removed | none | none | Low | Historical cleanup evidence only; no active deletion action. |
| COMPLETED_REMOVED | `Game_Over.m4a` | AudioClip | Removed in verified orphan audio asset cleanup PR. `Game_Over.wav` remains present. | removed | none | none | Low | Historical cleanup evidence only; no active deletion action. |
| COMPLETED_REMOVED | `DrSaturn_Act_Def.asset` | SFX definition | Removed in verified orphan audio asset cleanup PR. The same source clip remains used by `DrSaturn_Move_Def` random variant and `EnemyAudioRuntimeTests` explicitly loads `cre_Dr.saturn_act.wav`. | removed; retained clip used by move def | no direct runtime def | clip path test | Low | Historical cleanup evidence only; keep retained clip and active definition chain. |
| COMPLETED_REMOVED | `RocketFace_Act_Def.asset` | SFX definition | Removed in verified orphan audio asset cleanup PR. `cre_RocketFace_act_fix.wav` remains used by `RocketFace_ChargeActiveLoop_Def`. | removed; retained clip used by charge loop def | no direct runtime def | no direct path test found | Low | Historical cleanup evidence only; keep retained clip and active definition chain. |
| KEEP_CANONICAL | `Monster_Attack_8_Def`, `Monster_Grunt_6_Def`, `Monster_TakingDamage_1_Def`, `Monster_Death_4_Def` | Gameplay core map dependencies | Production names are normalized and serialized in `GameplayAudioMap_CampaignV1` for required semantics. | map refs | active gameplay core SFX | map/tests | High | Preserve or replace in semantic map first. |
| KEEP_RESERVED | `ObjectiveComplete`, `TopologyShift` | UI SFX | Governance decision keeps these as frozen-v1 reserved cue IDs. Current production producers do not emit them, but `UiAudioCueId` public-surface and `UiAudioCueMap` completeness tests require stable enum slots and explicit map entries. | cue map refs | none current; reserved only | `UiAudioSfxContractTests` | Medium | Preserve enum values `10`/`11` and `UiAudioCueMap_V1` entries; do not add playback without a separate product/governance decision. |

## Code Residue Assessment

No direct deletion candidates were found in shared runtime code.

Evidence:

- `rg` found no runtime `Play3D`, public spatial contract, distance attenuation API, `AudioManager.Instance`, or feature-side `new AudioManager`.
- Tests explicitly guard no `Play3D` on public API and gameplay authoritative layers.
- `PlayerPrefsAudioSettingsStore` is not legacy residue; it is the current default persistence store behind `AudioMixingService`.
- `AudioPlaybackPolicy` is reserved via `AudioBinding.Policy`, not a dead policy implementation.
- `GameplayActionAudioPresentationController` deferred/suppress policy is a follow-up investigation candidate only. Do not remove deferred/suppress branches in this docs cleanup PR.
- AUD-012 is closed as `KEEP_STAGE_COMPANION`: the archived stage companion remains catalog-owned, runtime/governance consumed, and blocked from removal until a future stage governance migration removes the owner entry/reference first.

Potential refactor-only items:

- Production assets named `_Test` should be renamed or replaced because the name conflicts with their serialized production use.
- Stage audio companions and scene BGM request sources both exist. Stage-backed gameplay uses `StageAudioRuntimeRequestSource`; any simplification should preserve Flow_Audio ownership.
