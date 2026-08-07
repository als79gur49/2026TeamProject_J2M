# Push/Flip Input and Feature Deletion Audit

Date: 2026-06-07 KST

Scope: Player `Push` / `Flip`, physical input, command gateway, runtime feature branches, stage content, presentation, UI/HUD, audio, tests, docs, and serialized assets.

## Executive Summary

The current Push/Flip feature is not dead. Physical input, UI command input, command fields, runtime action state, movement expansion, stage box capability data, presentation signals, and action-audio planning are all reachable in the current repo.

The deletion opportunity is narrower and mostly outside the canonical feature path:

| Group | Conclusion |
| --- | --- |
| Immediate delete | No production Push/Flip input action, command route, runtime branch, prefab component, or audio asset qualifies for immediate deletion. Old `MovePush`, `AutoPush`, `PushBox`, `FlipBox`, `MovableBox`, generated input wrapper, and HelpScreen Push/Flip prompt were not found as active repo artifacts. |
| Migration 후 삭제 | Legacy diagnostics aliases, `StageSpawnDefinition.PresentationId`, and public action-plan `GroupId` / `SourceActionGroupId` compatibility aliases were migrated to canonical names and removed. `deleted legacy fallback diagnostic flag` is retained as the canonical removed-fallback diagnostics field. |
| Rename/refactor 후 유지 또는 삭제 | `Player_S1_GameplayActionAudioProfile.asset` was renamed from the misleading `_Test` name while preserving GUID `42a2e109fc5141ec9e866925a0a85c3b`. `SettingsScreen.prefab` inactive duplicate Push/Flip change-button objects were deleted after serialized reference checks. |
| 삭제 금지 | `Push`/`Flip` InputActions, `GameplayInputHost` physical Push/Flip buffers, `PlayerTickCommand.PushPressed/FlipPressed`, `PlayerActionKind.Push/Flip`, `BoxCapabilities.Push/Flip`, `MovementExpander` Push/Flip handling, `FlipImpactPresentationSignal`, and `GameplayActionKind.Push/Flip` are currently used. |

Primary evidence:

- `Assets/InputSystem_Actions.inputactions` contains `Player/Push` and `Player/Flip`.
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs` binds `Player/Push` and `Player/Flip`, subscribes to action callbacks, buffers them, and emits `PlayerTickCommand.PushPressed` / `FlipPressed`.
- Historical state: `Assets/_Features/Gameplay/Gameplay_Host/Runtime/UIAccess/GameplayHostCommandGateway.cs` forwarded UI Push/Flip action requests into input buffering. Current cleanup removes that route.
- PlayMode tests press real keyboard bindings for Push/Flip and verify edge buffering and runtime action outcomes.
- Stage content contains active `BoxCapabilities` rows with Push and Flip capability data.
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab` references `Player_S1_GameplayActionAudioProfile.asset` by GUID.

## Current Actual Use Map

| Step | Production code exists? | Asset/prefab/scene connected? | Runtime enabled? | Tests only? | Docs only? | Delete? |
| --- | --- | --- | --- | --- | --- | --- |
| Physical keyboard Push | Yes: `Player/Push` InputAction, `<Keyboard>/j` | Yes: input asset referenced by project settings and scenes | Yes: `GameplayInputHost.BindActions()` calls `_actions.Enable()` | No | No | No |
| Physical keyboard Flip | Yes: `Player/Flip` InputAction, `<Keyboard>/k` | Yes | Yes | No | No | No |
| Gamepad Push | Yes: `Player/Push`, `<Gamepad>/buttonNorth` | Yes | Yes | No | No | No |
| Flip controller binding | No matching controller binding observed in the inspected `Player/Flip` action | Not applicable | Not applicable | No | No | Keyboard-only by current product policy |
| Generated input wrapper | No: `generateWrapperCode: 0`, wrapper class/path empty | No wrapper refs found | No | No | No | Nothing to delete |
| `GameplayInputHost` adapter | Yes | Used by gameplay host runtime | Yes | No | No | No |
| UI command gateway route | Removed for Push/Flip action requests | `GameplayUiFlowPorts` carries `IGameplayCommandGateway` for UI-held movement | Historical tests migrated | No | No | No current touch/mobile/assist action surface |
| Command contract | Yes: `PlayerTickCommand.PushPressed`, `FlipPressed` | Emitted by host and directly built by tests | Yes | No | No | No |
| Runtime state | Yes: `PlayerActionRuntimeState`, `PlayerActionKind.Push/Flip` | Used by player control logic and presentation snapshots | Yes | No | No | No |
| Movement runtime | Yes: `MovementExpander`, impact/disposition paths | Stage boxes make it reachable | Yes | No | No | No |
| Presentation | Yes: `GameplayEntityPresentationApplier`, flip drivers, `TickPresentationData` carriers | Runtime view components/tests | Yes | No | No | No |
| Audio | Yes: `GameplayActionAudioRequestPlanner`, profile entries | `Player_S1.prefab` references profile GUID `42a2e109fc5141ec9e866925a0a85c3b` | Yes | No | No | Rename candidate only |

Input source trace:

```text
Assets/InputSystem_Actions.inputactions
  Player/Push (<Keyboard>/j, <Gamepad>/buttonNorth)
  Player/Flip (<Keyboard>/k)
    -> GameplayInputHost.BindActions()
    -> OnPushStarted / OnFlipStarted / OnFlipPerformed
    -> buffered Push/Flip flags
    -> BuildPlayerCommand()
    -> PlayerTickCommand.PushPressed / FlipPressed
    -> TickRunner / TickPipeline
    -> SnapshotEntityLogicProvider inserts PlayerControlStateLogic
    -> PlayerControlStateLogic starts PlayerActionKind.Push/Flip
    -> MovementIntentCollector / MovementExpander / Attack / Finalize
    -> TickPresentationData + GameplayActionAudioRequestPlanner consumers
```

UI source trace:

```text
UI.Flow / ViewModel intent
  -> historical UI Push/Flip action request forwarding
  -> removed in current cleanup
  -> physical input remains the active Push/Flip command source
```

Mandatory input verdicts:

| Verdict | Result |
| --- | --- |
| A. Push/Flip physical input is currently used in play | Yes for keyboard Push/Flip and existing Push controller binding. |
| B. Command field exists but physical input source is dead | No for keyboard Push/Flip. |
| C. Tests only construct command directly | Tests do construct commands directly, but this is not the only source. Physical input also exists. |
| D. UI/help/prompt only residue | Settings/rebind UI is active. No active HelpScreen Push/Flip prompt was found. |
| E. Old route parallel to canonical route | No old Push/Flip input adapter or generated wrapper found. Legacy ordinary fallback diagnostics are separate movement compatibility residue. |

## Input Reachability Map

| Input item | Physical binding exists? | Runtime adapter exists? | Command emitted? | Used in production scene? | Used in tests only? | Delete? |
| --- | --- | --- | --- | --- | --- | --- |
| Push input | Yes: `Player/Push` | Yes | Yes | Yes | No | No |
| Flip input | Yes: `Player/Flip` keyboard | Yes | Yes | Yes | No | No |
| `PushPressed` | Not physical itself | Yes | Yes | Yes | No | No |
| `FlipPressed` | Not physical itself | Yes | Yes | Yes | No | No |
| Old Push binding | No separate old binding found | No | No | No | No | No active artifact |
| Old Flip binding | No separate old binding found | No | No | No | No | No active artifact |
| Move-to-push fallback | No canonical fallback; plain movement into Push target is suppressed | Runtime suppression exists | No Push command emitted | Yes as blocked/no-op behavior | Governance tested | Do not delete governance without migration |
| Push prompt | Settings/rebind label exists | Uses binding service | No command ownership | Yes | No | No |
| Flip prompt | Settings/rebind label exists | Uses binding service | No command ownership | Yes | No | No |
| ActionBar Push/Flip slot | No active runtime ActionBar consumer found | No | No | No | Docs/tests mention retirement | Clean docs vocabulary only |
| HelpScreen Push/Flip text | No active HelpScreen prompt found | No | No | No | No | No active artifact |

## Feature Reachability Map

### Push

| Case | Production reachable? | Stage content reachable? | Test-only? | Obsolete path? | Delete candidate? |
| --- | --- | --- | --- | --- | --- |
| Push normal slide | Yes | Yes: Push-capable boxes in campaign/tutorial/showcase content | No | No | No |
| Push blocked by wall/solid/board edge | Yes | Yes: board and solid lanes are runtime blockers | No | No | No |
| Push first-step blocked + Destroy fallback | Yes | Yes: Destroy-capable box capability rows exist | No | No | No |
| Push hostile impact + target dies | Yes | Content has boxes/enemies; runtime impact/disposition tests cover behavior | No | No | No |
| Push hostile impact + target survives | Yes | Same runtime path | No | No | No |
| Push action blocked but recovery signal emitted | Yes | Yes | No | No | No |
| Plain move into Push box | Yes as explicit non-start/no-op suppression | Yes | Governance behavior | Old fallback is obsolete, but active code is suppression | No active fallback to delete |

### Flip

| Case | Production reachable? | Stage content reachable? | Test-only? | Obsolete path? | Delete candidate? |
| --- | --- | --- | --- | --- | --- |
| Flip normal landing | Yes | Yes: Flip-capable boxes in campaign/tutorial/showcase content | No | No | No |
| Flip crosses boundary | Yes | Yes: topology-aware movement paths | No | No | No |
| Flip landing blocked with no impact | Yes | Yes | No | No | No |
| Flip landing hostile impact + target survives -> DestroySelf | Yes | Runtime impact/disposition tests cover behavior | No | No | No |
| Flip landing hostile impact + target dies + landing accepted -> FollowThrough | Yes | Runtime reachable | No | No | No |
| Flip landing hostile impact + target dies + landing denied -> Stay | Yes | Runtime reachable | No | No | No |
| Flip action blocked but recovery signal emitted | Yes | Yes | No | No | No |

## Stage/Content Reachability Map

The scan counted serialized `BoxCapabilities` rows in `Assets/_Features/Stages/Content`. Counts include generated gameplay assets and paired authoring assets.

| Stage / Asset family | Push box count | Flip box count | Push/Flip reachable by current input? | Test/showcase only? | Delete impact |
| --- | ---: | ---: | --- | --- | --- |
| All `StageContentEntry` stage assets and authoring pairs | 1666 rows containing Push bit | 1440 rows containing Flip bit | Yes | No | Removing Push/Flip breaks campaign/tutorial/showcase content |
| `stage-4-2` asset + authoring | 30 combined Push rows | 16 combined Flip rows | Yes | Showcase, but active validation content | Do not delete |
| `stage-0-1` asset + authoring | 70 combined Push rows | 8 combined Flip rows | Yes | Tutorial content | Do not delete |
| Campaign `stage-0-1` through `stage-5-1` asset + authoring | Push present in every listed stage family | Flip present in every listed stage family | Yes | Production campaign content | Do not delete |
| Destroy-capable boxes | 8 rows with Destroy bit, plus 1416 rows with value `27` (`Push|Flip|Destroy|JumpCrushable`) | Same rows include Push/Flip where applicable | Yes | No | Impact/disposition cleanup is high risk |

Serialized value counts observed:

| `BoxCapabilities` value | Count | Meaning |
| ---: | ---: | --- |
| 0 | 1850 | No box capability |
| 1 | 170 | Push |
| 2 | 4 | Flip |
| 3 | 4 | Push + Flip |
| 4 | 2 | Item |
| 5 | 2 | Push + Item |
| 6 | 2 | Flip + Item |
| 7 | 2 | Push + Flip + Item |
| 9 | 60 | Push + Destroy |
| 11 | 12 | Push + Flip + Destroy |
| 27 | 1416 | Push + Flip + Destroy + JumpCrushable |

## Asset/GUID Reference Map

| Symbol / AssetPath | Kind | Production refs | Test refs | Prefab/Scene/GUID refs | Docs refs | Runtime reachable? | Classification | Delete action |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Assets/InputSystem_Actions.inputactions` `Player/Push` | InputAction | `GameplayInputHost`, binding settings service | PlayMode keyboard input tests | GUID `052faaac586de48259a63d0c4782560b` in project settings/scenes | Input docs | Yes | KEEP_CURRENTLY_USED | None |
| `Assets/InputSystem_Actions.inputactions` `Player/Flip` | InputAction | `GameplayInputHost`, binding settings service | PlayMode keyboard input tests | Same input asset GUID | Input docs | Yes | KEEP_CURRENTLY_USED | None |
| generated input wrapper | C# generated wrapper | None found | None found | `generateWrapperCode: 0` | None | No | No artifact | None |
| `GameplayInputHost` Push/Flip buffers | C# adapter | Gameplay host runtime | PlayMode and unit tests | Host runtime composition | Architecture docs | Yes | KEEP_CURRENTLY_USED | None |
| Gameplay UI Push/Flip action request route | UI command gateway | No production UI surface | UI/host tests migrated | Runtime ports | UI architecture docs | Removed | REMOVED_BY_PRODUCT_DECISION | Current product keeps Push/Flip on physical input |
| `PlayerTickCommand.PushPressed/FlipPressed` | Command fields | Tick pipeline/player logic | Many core/playmode tests | None serialized | Architecture docs | Yes | KEEP_CURRENTLY_USED | None |
| `GameplayRuntimeFeatureFlags.RemovedDiagnosticBaselineAlias` / `RemovedDiagnosticHelperAlias` | Removed C# compatibility aliases | None after migration | Boundary/replay governance tests now use canonical names | No scene exposure | Migration docs mark historical/removed | Diagnostic only | REMOVED_ALIAS | Deleted after canonical migration |
| `GameplayRuntimeFeatureFlags.deleted legacy fallback diagnostic flag` | C# canonical field | Removed-fallback diagnostics field | Boundary/replay governance tests | Docs state scene config does not expose it | Current docs use canonical naming | Diagnostic only | CANONICAL_DIAGNOSTICS_FIELD | C안 completed |
| `StageSpawnDefinition.PresentationId` | Removed serialized legacy field | Canonical `StagePresentationDefinition` path | Stage builder/validator tests migrated | Stage gameplay assets migrated off legacy field | Stage migration docs | No legacy read path remains | REMOVED_LEGACY_FIELD | Deleted after content/test migration |
| `SettingsScreen.prefab` legacy Push/Flip duplicate change buttons | Deleted inactive duplicate GameObjects | Current `PushInputRow` / `FlipInputRow` retained | UI tests indirectly | Legacy object names removed from prefab | None found | No | REMOVED_DUPLICATE | Deleted inactive duplicate objects |
| `Player_S1_GameplayActionAudioProfile.asset` | ScriptableObject asset | Referenced by `Player_S1.prefab` | Action audio runtime/smoke tests | GUID `42a2e109fc5141ec9e866925a0a85c3b` | Audio governance docs | Yes | RENAMED_RETAINED | Renamed from `_Test`; do not delete |
| `BoxFlipInteractionDriver` / player hand flip path | Presentation components | Box-side driver remains; player hand path removed | Flip/VFX tests | Runtime view components created in tests; prefab attachment is expected by runtime view | VFX governance docs | Mixed | KEEP_BOX_REMOVE_PLAYER_HAND | Current product cannot support the player IK path |
| Push contact threshold / `pushContactTicks` | Obsolete model | No active runtime refs found | No active tests found | No GUID refs found | Archive/current docs describe historical removal | No | DELETE_CANDIDATE_OBSOLETE_FEATURE | Docs/archive cleanup only if archive policy allows |
| `PushBox` / `FlipBox` / `MovableBox` components | Old component model | None found | None found | None found | Search-only absent | No | No artifact | None |
| HelpScreen Push/Flip prompt | UI prompt | None found | Tests assert old HelpScreen residue absent | None found | No active prompt found | No | No artifact | None |

## Delete Candidate Summary

| Priority | Classification | Item | Required action |
| --- | --- | --- | --- |
| P0 | DELETE_CANDIDATE_OBSOLETE_FEATURE | Push contact accumulation / threshold mentions outside current contract | Only docs/archive cleanup is possible; no active runtime artifact found. |
| P1 | REMOVED_ALIAS | `GameplayRuntimeFeatureFlags.RemovedDiagnosticBaselineAlias` and `RemovedDiagnosticHelperAlias` | Tests/docs migrated to canonical `removed diagnostic baseline preset (historical, deleted)` / `deleted legacy fallback diagnostic flag`; aliases deleted. |
| P1/P3 | CANONICAL_DIAGNOSTICS_FIELD | `deleted legacy fallback diagnostic flag` | C안 completed; canonical diagnostics field retained and old compatibility projection removed. |
| P1 | REMOVED_LEGACY_FIELD | `StageSpawnDefinition.PresentationId` legacy field | Migrated to `StagePresentationDefinition`; generated gameplay assets and validation expectations updated. |
| P1/P3 | REMOVED_ALIAS | `GroupId` / `SourceActionGroupId` compatibility aliases | Tests migrated to `ActionPlanId` / `SourceActionPlanId`; `IntentId` remains canonical internal carry-forward. |
| P2/P3 | REMOVED_STALE_LEDGER | Push/Item stale trace ledger rows | Active DeferredStale and Lane-A ledger rows for stale legacy Push/Item trace-token drift were removed. |
| P2 | REMOVED_DUPLICATE | Settings prefab legacy Push/Flip duplicate names | Inactive duplicate GameObjects deleted; current input rows retained. |
| P2 | RETIRED_VOCABULARY | ActionBar Push/Flip vocabulary in docs | Consolidated around `PlayerStatusPresenter`/HUDRoot query reality; ActionBar remains retired/historical vocabulary. |
| P2/P3 | RENAMED_RETAINED | `_Test` action-audio profile name on production player prefab | Renamed asset/profile; GUID preserved; do not delete. |

## Mandatory Suspect Item Verdicts

| # | Suspect item | Verdict | Evidence / action |
| ---: | --- | --- | --- |
| 1 | Unused Push InputAction | Not unused; `KEEP_CURRENTLY_USED` | `Player/Push` is read by `GameplayInputHost` and covered by physical input tests. |
| 2 | Unused Flip InputAction | Not unused; `KEEP_CURRENTLY_USED` | `Player/Flip` is read by `GameplayInputHost` and covered by physical input tests. |
| 3 | Push/Flip old key binding | No separate old binding found | Current bindings are active; no delete action. |
| 4 | Push/Flip old generated input wrapper | No artifact found | Wrapper generation is disabled and no generated wrapper refs were found. |
| 5 | `GameplayInputHost` Push/Flip dead route | Not dead; `KEEP_CURRENTLY_USED` | `BindActions()` requires Push/Flip actions and emits command fields. |
| 6 | Gameplay UI Push/Flip action request route | Removed by product decision | No current UI action button surface; physical input remains. |
| 7 | Plain move -> push fallback | No active fallback; governance behavior remains | Plain move into Push box is suppressed/no-op. Keep tests that prevent fallback revival. |
| 8 | Push contact accumulation / threshold model | `DELETE_CANDIDATE_OBSOLETE_FEATURE` docs-only | No active runtime fields found; archive/current docs mark it historical. |
| 9 | Old `MovePush` / `AutoPush` path | No artifact found | No active runtime/prefab refs. |
| 10 | Old `PushBox` / `FlipBox` / `MovableBox` component | No old component artifact found | Runtime uses `EntityType.Box` + `BoxCapabilities`; test method names containing PushBox/FlipBox are behavior names, not old components. |
| 11 | Old HelpScreen Push/Flip prompt | No active artifact found | UI governance tests assert HelpScreen residue is absent. |
| 12 | ActionBar Push/Flip retired vocabulary | `REFACTOR_DUPLICATE` docs cleanup | Runtime owner is HUDRoot/PlayerStatus query path; ActionBar is documented as retired. |
| 13 | PlayerStatus vs ActionBar owner mismatch | `REFACTOR_DUPLICATE` docs cleanup | Actual runtime symbols use `HUDRootPresenter`, `PlayerStatusPresenter`, `GameplayHostPlayerHudQuery`; no active `ActionBarPresenter` found. |
| 14 | Unused Push/Flip audio binding | Not unused; `KEEP_CURRENTLY_USED` | Player prefab references action-audio profile and planner consumes Push/Flip signals. |
| 15 | `_Test` action-audio profile naming | `RENAMED_RETAINED` | Production prefab still references the same profile GUID; only misleading file/name suffix was removed. |
| 16 | Stale Push/Item trace ledgers | `REMOVED_STALE_LEDGER` | `Docs/DeferredStaleLedger.md` and Lane-A ledger stale legacy Push/Item trace-token rows were removed. |
| 17 | Obsolete action-plan `GroupId` / `IntentId` alias | `REMOVED_ALIAS` for `GroupId` aliases only | `GroupId` / `SourceActionGroupId` compatibility aliases were removed; `IntentId` remains canonical internal ID and is not a delete candidate. |
| 18 | Legacy ordinary fallback feature flags | `CANONICAL_DIAGNOSTICS_MIGRATED` | Old aliases and old diagnostics API projection were removed; `deleted legacy fallback diagnostic flag` is canonical. |
| 19 | Unused Push/Flip presentation driver | Not unused; `KEEP_CURRENTLY_USED` | `GameplayEntityPresentationApplier` resolves flip drivers and VFX/flip tests cover them. |
| 20 | Docs-only Push/Flip removed behavior | Mixed | Historical archive notes are docs-only; clean active stale wording, retain archive if policy requires. |

## Risk Summary

| Area | Risk if deleted blindly | Validation required |
| --- | --- | --- |
| Input actions/bindings | Player loses Push/Flip keyboard input; settings rebind UI breaks | `./run_tests.sh core`, PlayMode input tests |
| `GameplayInputHost` / command gateway | Physical or UI commands stop reaching simulation | `./run_tests.sh core`, UI gateway tests |
| Runtime Push/Flip branch | Campaign/tutorial stages with Push/Flip boxes become unsolvable or behavior changes | Core + touched movement/impact/replay tests |
| Stage `BoxCapabilities` | Massive production content break; asset migration required | Stage validator + core |
| Presentation drivers | Flip impact/motion tracks or VFX bridge regress | VFX/flip presentation targeted tests |
| Action audio profile | Production player action audio loses Push/Flip coverage | GameplayActionAudioRuntimeTests, AudioRepositoryAssetSmokeCoreTests |
| Legacy fallback compatibility aliases | Replay/golden diagnostics and governance tests break | Boundary/replay targeted tests |
| Settings prefab legacy names | UI serialized references may break if object is active | `./run_tests.sh ui`, manual prefab validation |
