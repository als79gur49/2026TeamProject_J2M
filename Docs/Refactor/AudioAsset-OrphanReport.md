# Audio Asset Orphan Report

Investigation date: 2026-06-06 KST

Asset inventory scan summary:

| Asset Type | Count |
|---|---:|
| `AudioClip` under `_Shared/Audio` | 84 |
| `SingleAudioDefinition` | 51 |
| `RandomAudioDefinition` | 32 |
| `BgmProfile` | 7 |
| `StageAudioDefinition` | 12 |
| `GameplayAudioMap` | 1 |
| `GameplayActionAudioProfile` | 1 |
| Additional production gameplay audio maps/profiles | 14 |
| `UiAudioCueMap` | 1 |

Important scan notes:

- `AudioBinding` is serialized inline inside maps/profiles, not as a separate `.asset` type in this repository.
- GUID reverse lookup was used for `.asset`, `.prefab`, `.unity`, `.meta`, and scene/prefab serialized references.
- C# `AssetDatabase.LoadAssetAtPath` and docs path references were checked for relevant candidate paths.
- `Resources.Load` did not target audio clips/definitions/maps. Resource loads found were UI shell/catalog/font paths.
- Addressables migration tooling mentions `_Shared/Audio`, but no active Addressables asset loading path for these candidates was found.

Count convention:

- Counts exclude `.meta` files unless explicitly stated.
- Definition/clip pairs are counted by asset file, not by logical pair.
- `World_EndCredit.ogg` is technically unreferenced but product/content-confirmation gated.

## Orphan / Candidate Assets

| Asset Type | Path | GUID | Referenced By | Clip/Definition/Binding Chain | Production/Test | Decision |
|---|---|---|---|---|---|---|
| AudioClip | `Assets/_Shared/Audio/Clips/Bgm/World_EndCredit.ogg` | `4d9cf3682b260774f9716cde2687fe8a` | no serialized refs beyond `.meta` | no `AudioDefinition` | Production name, unreferenced | DELETE_SAFE, content confirmation recommended |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_Nebulus_dead.wav` | `cc2f38d04e258b047ab5bb4f0bf836c0` | no serialized refs beyond `.meta` | no active definition uses this wav | Production name, unreferenced duplicate candidate | DELETE_SAFE |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_bot_dead.wav` | `f7e6d9b2584b48d47b8b6a37306a17cd` | no serialized refs beyond `.meta` | no active definition | Production name, unreferenced duplicate candidate | DELETE_SAFE |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_bot_move.wav` | `e392d668fea0e6346a1fb644e91e7e01` | no serialized refs beyond `.meta` | no active definition | Production name, unreferenced duplicate candidate | DELETE_SAFE |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/Sucked-into-the-Black-Hole-1_TTX041201_Test.wav` | `bd2fd29b1d7e7b34d921b1cd3b7a358f` | no serialized refs beyond `.meta` | no definition | `_Test` clip, unreferenced | DELETE_SAFE |
| RandomAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/Archery 6073_50_1_Test_Def.asset` | `19e0694c2eeadf2408316d19229bfa90` | no external serialized refs | owns `Archery 6073_50_1_Test.wav` only | `_Test` | DELETE_SAFE with clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/Archery 6073_50_1_Test.wav` | `112a8f0897bb8dc42915499a7ee08768` | only `Archery 6073_50_1_Test_Def.asset` | definition has no consumers | `_Test` | DELETE_SAFE with definition |
| RandomAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/LowThudPunch FS040206_Test_Def.asset` | `b2ea499dbd6c5d346998ea8d2bb0bcf6` | no external serialized refs | owns `LowThudPunch FS040206_Test.wav` only | `_Test` | DELETE_SAFE with clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/LowThudPunch FS040206_Test.wav` | `c07e99a1cd6ac794cad1b5b2b5f15653` | only matching definition | no consumers after definition removal | `_Test` | DELETE_SAFE with definition |
| RandomAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/Monster Attack 3_Test_Def.asset` | `b8e8e64c8849e2744aee0188f2264468` | no external serialized refs | owns `Monster Attack 3_Test.mp3` only | `_Test` | DELETE_SAFE with clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/Monster Attack 3_Test.mp3` | `7a11a1359903dfe4f93ab2da1240009a` | only matching definition | no consumers after definition removal | `_Test` | DELETE_SAFE with definition |
| RandomAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/Monster Laugh 1_Test_Def.asset` | `df0fe84f7f4b919428e8ddd52cedbde2` | no external serialized refs | owns `Monster Laugh 1_Test.mp3` only | `_Test` | DELETE_SAFE with clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/Monster Laugh 1_Test.mp3` | `e165d1f11ca302942b84c4d875d71c64` | only matching definition | no consumers after definition removal | `_Test` | DELETE_SAFE with definition |
| RandomAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/PunchesGruntsPunch FS040901_Test_Def.asset` | `0e23faa43d5c109429b34d5580afcd51` | no external serialized refs | owns `PunchesGruntsPunch FS040901_Test.wav` only | `_Test` | DELETE_SAFE with clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/PunchesGruntsPunch FS040901_Test.wav` | `b066774419a13684eafeec9ef3374acb` | only matching definition | no consumers after definition removal | `_Test` | DELETE_SAFE with definition |
| RandomAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/Sci-Fi-Special-Fx_GEN-HD4-43862_Test_Def.asset` | `bce7ccc3940938741be5f7b35c081861` | no external serialized refs | owns `Sci-Fi-Special-Fx_GEN-HD4-43862_Test.wav` only | `_Test` | DELETE_SAFE with clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/Sci-Fi-Special-Fx_GEN-HD4-43862_Test.wav` | `98c33df6d75199d4c80ea1438c27e0c5` | only matching definition | no consumers after definition removal | `_Test` | DELETE_SAFE with definition |
| RandomAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/SpacePod 8003_90_1_Test_Def.asset` | `d710d9942b90d9d44a24ea88ade7b0b6` | no external serialized refs | owns `SpacePod 8003_90_1_Test.wav` only | `_Test` | DELETE_SAFE with clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/SpacePod 8003_90_1_Test.wav` | `ce3102629b8de764bb7688ecf2eb8275` | only matching definition | no consumers after definition removal | `_Test` | DELETE_SAFE with definition |
| SingleAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/MonsterSounds/DrSaturn_Act_Def.asset` | `5646112bb2e343d18745567c3ba5ec6a` | no external serialized refs | references `cre_Dr.saturn_act.wav`; the clip is also used by `DrSaturn_Move_Def` random variant | Production definition orphan | DELETE_AFTER_TEST_UPDATE; keep clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_Dr.saturn_act.wav` | `bf85540a9fe408e45b9222f535e43966` | `DrSaturn_Move_Def`, orphan `DrSaturn_Act_Def`, `EnemyAudioRuntimeTests` path load | active random variant clip | Production/test-covered clip | KEEP_CANONICAL |
| SingleAudioDefinition | `Assets/_Shared/Audio/Definitions/Sfx/MonsterSounds/RocketFace_Act_Def.asset` | `322ed63326e445de84998446805cbcbc` | no external serialized refs | references `cre_RocketFace_act_fix.wav`; the clip is also used by `RocketFace_ChargeActiveLoop_Def` | Production definition orphan | DELETE_AFTER_TEST_UPDATE; keep clip |
| AudioClip | `Assets/_Shared/Audio/Clips/Sfx/MonsterSounds/cre_RocketFace_act_fix.wav` | `083feadc5c349eb4b99436c2c69b5e16` | `RocketFace_ChargeActiveLoop_Def`, orphan `RocketFace_Act_Def` | active charge loop clip | Production | KEEP_CANONICAL |

## `_Test` Named But Production Referenced

These are not deletion candidates.

| Asset Type | Path | GUID | Referenced By | Decision |
|---|---|---|---|---|
| BgmProfile | `Assets/_Shared/Audio/Definitions/Bgm/HorrorVol2FactoryMain_Test_BgmProfile.asset` | `3459b5be7e9e63b4899e1356efe8f928` | `Assets/Scenes/UIAudioScene.unity` `SceneBgmRequestSource` | REFACTOR_NOT_DELETE |
| RandomAudioDefinition | `Assets/_Shared/Audio/Definitions/Bgm/HorrorVol2FactoryMain_Test_BgmDef.asset` | `e35de9d1e9281254482c11b13a32a88e` | `HorrorVol2FactoryMain_Test_BgmProfile` | REFACTOR_NOT_DELETE |
| AudioClip | `Assets/_Shared/Audio/Clips/Bgm/Horror Vol2 Factory Main_Test.wav` | `26b69a77542b8e240b0e9dad43e966ae` | `HorrorVol2FactoryMain_Test_BgmDef` | REFACTOR_NOT_DELETE |
| GameplayAudioMap | `Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_UI-Audio_Test.asset` | `2e17653afa1ba264a950b76bcd5ccc56` | `Assets/Scenes/UIAudioScene.unity`, architecture tests | REFACTOR_NOT_DELETE |
| GameplayActionAudioProfile | `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile_Test.asset` | `42a2e109fc5141ec9e866925a0a85c3b` | `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab` | REFACTOR_NOT_DELETE |
| BlockAudioMap | `Assets/_Features/Gameplay/Gameplay_BlockAudio/Maps/BlockAudioMap_PlayerSounds_Test.asset` | `5a6bb3f9bcde4e6ca7487767ab9ba305` | `Assets/Scenes/UIAudioScene.unity` | REFACTOR_NOT_DELETE |
| PlayerLocomotionAudioMap | `Assets/_Features/Gameplay/Gameplay_PlayerLocomotionAudio/Maps/PlayerLocomotionAudioMap_PlayerSounds_Test.asset` | `6bf2bb925f794fe98b6b8e5a406e1f3d` | `Assets/Scenes/UIAudioScene.unity` | REFACTOR_NOT_DELETE |

## Active Asset Chains To Preserve

| Chain | Evidence | Decision |
|---|---|---|
| `UiAudioCueMap_V1 -> Ui_*_Def -> UI/Sfx clips` | cue map serialized in `MainMenuScene` and `UIAudioScene`; validation requires every current enum value | KEEP_CANONICAL |
| `GameplayAudioMap_UI-Audio_Test -> Player_Hurt_Def / Monster*_Test_Def / Block_Destroyed_Def` | map serialized in `UIAudioScene`; required six semantic map | KEEP_CANONICAL |
| `Player_S1.prefab -> GameplayActionAudioAuthoring -> Player_S1_GameplayActionAudioProfile_Test -> PlayerSounds definitions` | prefab GUID ref; action audio runtime tests | KEEP_CANONICAL |
| `StageContentEntry -> StageAudioDefinition.gameplayBgm -> Stage0-1..Stage4-1_BgmProfile -> Bgm definitions -> World_*.wav` | direct stage gameplay BGM companions; stage runtime request source path | KEEP_CANONICAL |
| `MainMenuScene -> SceneBgmRequestSource -> MainMenu_BgmProfile -> MainMenu_BgmDef -> World_MainLobby.ogg` | scene contract test path | KEEP_CANONICAL |
| enemy prefabs -> `EnemyAudioProfile_*` -> MonsterSounds definitions | prefab serialized refs and `EnemyAudioRuntimeTests` | KEEP_CANONICAL |
