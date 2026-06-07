# AudioSystem Deletion Plan

Investigation date: 2026-06-06 KST

This document defines a safe PR sequence from the investigation snapshot.

## Preconditions

Before any deletion PR:

```bash
git status --short --branch
git diff --stat
rg -n "Missing|Missing Script|m_Script: \\{fileID: 0\\}|AudioCategory.Master|Play3D|FindObjectOfType<.*Audio|FindAnyObjectByType<.*Audio|AudioManager.Instance" Assets ProjectSettings Packages
```

Run at least:

```bash
./run_tests.sh core
```

For UI/audio scene changes also run:

```bash
./run_tests.sh ui
```

## Step 1: Pure Orphan Audio Clips

Delete targets:

- `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_Nebulus_dead.wav`
- `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_bot_dead.wav`
- `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_bot_move.wav`
- `Assets/_Shared/Audio/Clips/Sfx/Sucked-into-the-Black-Hole-1_TTX041201_Test.wav`
- matching `.meta` files

Gated exclusion:

- `Assets/_Shared/Audio/Clips/Bgm/World_EndCredit.ogg` is technically unreferenced, but deletion is product/content-confirmation gated. Exclude it unless end-credit BGM content is confirmed unnecessary in the PR.

Reason:

- GUID reverse lookup found no serialized refs beyond `.meta`.
- No `AudioDefinition`, BGM profile, scene, prefab, map, or path-based test reference was found in the scoped search.

Required evidence in PR:

```bash
for p in \
'Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_Nebulus_dead.wav' \
'Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_bot_dead.wav' \
'Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_bot_move.wav' \
'Assets/_Shared/Audio/Clips/Sfx/Sucked-into-the-Black-Hole-1_TTX041201_Test.wav'; do
  g=$(awk '/guid:/{print $2; exit}' "$p.meta")
  echo "$p $g"
  rg -n "$g|$p" Assets ProjectSettings Packages Docs --glob '!*.meta' --glob '!Library/**' || true
done
```

Tests:

- `./run_tests.sh core`
- targeted if available: `AudioRepositoryAssetSmokeCoreTests`

Rollback:

- restore deleted clip and `.meta` files from git.

Risk:

- Low compile risk.
- Low runtime risk if GUID search remains empty.
- Content risk for `World_EndCredit.ogg`; get content-owner confirmation if an end-credit scene is pending.

## Step 2: Orphan Test Definition/Clip Pairs

Delete targets:

- `Archery 6073_50_1_Test_Def.asset` + `Archery 6073_50_1_Test.wav`
- `LowThudPunch FS040206_Test_Def.asset` + `LowThudPunch FS040206_Test.wav`
- `Monster Attack 3_Test_Def.asset` + `Monster Attack 3_Test.mp3`
- `Monster Laugh 1_Test_Def.asset` + `Monster Laugh 1_Test.mp3`
- `PunchesGruntsPunch FS040901_Test_Def.asset` + `PunchesGruntsPunch FS040901_Test.wav`
- `Sci-Fi-Special-Fx_GEN-HD4-43862_Test_Def.asset` + `Sci-Fi-Special-Fx_GEN-HD4-43862_Test.wav`
- `SpacePod 8003_90_1_Test_Def.asset` + `SpacePod 8003_90_1_Test.wav`
- all matching `.meta` files

Reason:

- Each definition has zero external serialized refs.
- Each clip is referenced only by its orphan definition.
- No runtime map/profile/scene/prefab reference was found.

Required evidence in PR:

- For each definition GUID, `rg` should find only its `.meta`.
- For each clip GUID, `rg` should find only the soon-to-be-deleted definition and its `.meta`.
- `rg` path search for filenames should not find runtime/test path loads outside self.

Tests:

- `./run_tests.sh core`
- `AudioRepositoryAssetSmokeCoreTests` if running targeted Unity EditMode is practical.

Rollback:

- restore deleted definition, clip, and `.meta` pairs from git.

Risk:

- Low compile risk.
- Medium asset import risk only if a hidden serialized reference was missed; this is why the paired GUID check is required.

## Step 3: Orphan Definitions Whose Clips Stay

Delete candidates:

- `Assets/_Shared/Audio/Definitions/Sfx/MonsterSounds/DrSaturn_Act_Def.asset`
- `Assets/_Shared/Audio/Definitions/Sfx/MonsterSounds/RocketFace_Act_Def.asset`
- matching `.meta` files only

Do not delete:

- `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_Dr.saturn_act.wav`
- `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_RocketFace_act_fix.wav`

Reason:

- Both definition assets have zero external serialized refs.
- `cre_Dr.saturn_act.wav` is still used by `DrSaturn_Move_Def` random variant and loaded by `EnemyAudioRuntimeTests`.
- `cre_RocketFace_act_fix.wav` is still used by `RocketFace_ChargeActiveLoop_Def`.

Required action:

- Update or confirm tests do not load the orphan definitions by path.
- Keep the clips and verify their active definitions still validate.

Tests:

- `./run_tests.sh core`
- targeted: `EnemyAudioRuntimeTests`
- targeted: `AudioRepositoryAssetSmokeCoreTests`

Rollback:

- restore deleted definition and `.meta` files.

Risk:

- Medium, because nearby enemy audio tests pin clip composition and full-lane behavior.

## Step 4: Rename Production `_Test` Assets

Do not delete these in cleanup PRs:

- `HorrorVol2FactoryMain_Test_BgmProfile`
- `HorrorVol2FactoryMain_Test_BgmDef`
- `GameplayAudioMap_UI-Audio_Test`
- `Player_S1_GameplayActionAudioProfile_Test`
- `BlockAudioMap_PlayerSounds_Test`
- `PlayerLocomotionAudioMap_PlayerSounds_Test`

Reason:

- They are serialized by production scenes or prefabs.
- Tests pin some of these paths.

Required action:

- Use Unity/GUID-preserving asset moves.
- Update path-pinned tests and documentation in the same PR.
- State whether the rename is content naming cleanup only or replacement with final authored audio.

Tests:

- `./run_tests.sh core`
- `./run_tests.sh ui` if scenes/UI installers are touched
- targeted: `GameplayShellUiAudioContractTests`
- targeted: `MainMenuAudioSceneContractTests`
- targeted: `GameplayActionAudioRuntimeTests`
- targeted: `AudioRepositoryAssetSmokeCoreTests`

Rollback:

- revert asset moves and path test updates together.

Risk:

- Medium. GUID-preserving moves are safe, but path-pinned tests and human workflows may break if not updated.

## Step 5: Legacy Code Path Deletion

Current finding:

- No shared runtime code path qualifies for deletion now.
- No public `Play3D`, spatial blend, distance attenuation, `AudioManager.Instance`, scene-global audio lookup, or direct feature `new AudioManager` path was found.
- `PlayerPrefsAudioSettingsStore` and hidden channel persistence are canonical, not legacy.
- `AudioBinding.Policy` is reserved, not dead.

Required action:

- Do not delete shared runtime code in the first asset cleanup PR.
- If a future code cleanup claims legacy code, require a test-backed source reference and a replacement path.

Tests:

- `AudioArchitectureTests`
- `GameplayAudioOverlapRefactorTests`
- `AudioRuntimePlayModeTests`

Rollback:

- normal code revert.

Risk:

- High if runtime ownership seams are changed casually.

## Step 6: Scene / Prefab Obsolete Component Removal

Current finding:

- `MainMenuScene` and `UIAudioScene` use canonical audio bootstrap components.
- `UIAudioScene` serializes active gameplay audio maps.
- `Player_S1.prefab` serializes active action audio authoring.
- No old scene-local music player or direct `AudioManager` singleton residue was found in scoped searches.

Required action:

- No component removal is recommended until a specific obsolete component is identified.
- If scene/prefab changes are made, keep them in a separate commit where possible and include manual/editor validation evidence.

Tests:

- `./run_tests.sh ui`
- `ActualSceneBootstrapSmokePlayModeTests` if PlayMode is practical
- `PersistentBgmFlowPlayModeTests`

Rollback:

- revert scene/prefab YAML plus `.meta` if Unity generates changes.

Risk:

- High because serialized scene/prefab references are user-facing runtime bootstrap.

## Step 7: Docs / Tests Update

Update docs/tests only when the deletion changes a governed surface.

Expected updates:

- If deleting pure orphan clips/definitions: no architecture docs update should be needed, but update this report or a deletion changelog if keeping an audit trail.
- If renaming `_Test` production assets: update path-pinned tests.
- If deleting/renaming UI cues, gameplay semantics, BGM transition enum members, channels, or `StageBgmReference`: update governance docs and tests in the same change.

Never claim:

- project-wide green
- full regression closed
- all regressions fixed
- full lane green

unless the matching full/broad lane actually ran and passed on the same revision.

## Step 8: Validation Bundle After Deletion

Minimum commands:

```bash
git status --short
rg -n "Missing|Missing Script|m_Script: \\{fileID: 0\\}|AudioCategory.Master|Play3D|FindObjectOfType<.*Audio|FindAnyObjectByType<.*Audio|AudioManager.Instance" Assets ProjectSettings Packages
./run_tests.sh core
```

Targeted test candidates:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core/AudioRepositoryAssetSmokeCoreTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/AudioArchitectureTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/BgmFlowArchitectureTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/BgmFlowRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/AudioRuntimePlayModeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PersistentBgmFlowPlayModeTests.cs`
- `Assets/_Features/UI/UI_Tests/EditMode/UiAudioSfxContractTests.cs`
- `Assets/_Features/UI/UI_Tests/EditMode/AudioSettingsBridgeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyAudioRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayActionAudioRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/BlockAudioRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/PlayerLocomotionAudioRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/TopologyAudioRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GravityFieldAudioRuntimeTests.cs`

UI/scene changes additionally require:

```bash
./run_tests.sh ui
```

## Architecture Guardrails To Preserve

- Audio remains presentation-only and must not mutate authoritative gameplay state.
- `WorldState`, `TickPipeline`, committers, and entity logic must not call `IAudioService`.
- Feature code depends on `IAudioService` or narrower ports, not `AudioManager`.
- Public playback contract remains 2D non-spatial only.
- BGM continuity is owned by Flow_Audio, not scenes or gameplay host.
- UI SFX uses hidden `Ui` channel; settings UI exposes only `Main`, `Bgm`, `Sfx`.
- Gameplay core one-shot, gameplay action audio, BGM, UI SFX, and other host presentation audio lanes must not be collapsed into a generic dispatcher.
- `StageBgmReference` stays symbolic content metadata; actual playback ownership remains `Flow_Audio`.
