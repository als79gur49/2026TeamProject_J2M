# Push/Flip Residual Dead Code Audit

## Executive Summary

This is a second-pass residual audit against the current repository HEAD. It does not reuse the previous audit verdicts as evidence; prior audit documents were treated only as comparison material and were excluded from current runtime/content reachability decisions.

No core Push/Flip gameplay feature qualifies for immediate deletion. `Player/Push`, `Player/Flip`, `GameplayInputHost` buffering, `PlayerTickCommand.PushPressed/FlipPressed`, `PlayerActionKind.Push/Flip`, `MovementExpander` Push/Flip handling, `BoxCapabilities.Push/Flip`, `FlipImpactPresentationSignal`, and production action-audio profile attachment are still runtime reachable.

The highest-value residual candidates are:

- `P0/P1`: docs-only stale ledger rows and historical Push contact threshold wording.
- `P1/P3`: `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled` is now the canonical removed-fallback diagnostics field, not a Push/Flip runtime feature.
- `P2`: the gameplay UI Push/Flip action command injection route was not wired to any production UI button/surface and is removed by current product policy. Settings/rebind Push/Flip UI remains active.
- `P2`: Flip is keyboard-only by current product input policy; the existing Push controller binding remains authored.
- `P2`: low-usage box capability combos exist only in `combined-gameplay-showcase`.
- `P2`: `PlayerFlipInteractionDriver.cs` and `PlayerFlipInteractionDriver.cs.meta` had no production prefab/scene/asset GUID reference and are removed.
- `P2`: several action-audio moments are planner-emitted but have no production profile binding.

## What Was Rechecked From Previous Audit

Verified removed or current status:

- `PushChange_Legacy` / `FlipChange_Legacy`: no active repo artifact found.
- `Player_S1_GameplayActionAudioProfile.asset`: canonical production profile exists at `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset`.
- Old `_Test` action-audio profile name: no active repo artifact found; historical-only after rename.
- Action audio GUID `42a2e109fc5141ec9e866925a0a85c3b`: points to the canonical production asset and is referenced by `Player_S1.prefab`.
- removed baseline/helper aliases: no runtime aliases found in `Assets`; current code uses canonical removed-fallback diagnostics vocabulary.
- `RemovedLegacyFallbackDiagnosticsEnabled`: still present in `GameplayRuntimeFeatureFlags` and tests as canonical diagnostic routing.
- `GroupId`: active internal action-group vocabulary remains. The public `DamageResolutionRecord.GroupId` / `DestroyResolutionRecord.GroupId` compatibility alias pattern was not found.
- `SourceActionGroupId`: not found as active compatibility alias. `SourceActionPlanId` remains active presentation/VFX/audio correlation vocabulary.
- `StageSpawnDefinition.PresentationId`: generated gameplay assets have no `PresentationId` rows. Authoring placements and presentation bindings still use `PresentationId` for active editor/content presentation selection.
- `[Obsolete]`: no active C# `[Obsolete]` attribute was found; only docs discuss potential obsolete windows.

## Current Alive Map

| Area | Current evidence | Verdict |
| --- | --- | --- |
| Physical input | `InputSystem_Actions.inputactions` has `Player/Push` and `Player/Flip`; `GameplayInputHost` requires both actions | `KEEP_CURRENTLY_USED` |
| Command contract | `GameplayInputHost.BuildPlayerCommand()` emits `pushPressed` / `flipPressed`; `PlayerLogic` and `PlayerControlStateLogic` consume them | `KEEP_CURRENTLY_USED` |
| Runtime branches | `PlayerControlState`, `PlayerControlStateLogic`, `MovementExpander`, impact/finalization/presentation builders consume Push/Flip state | `KEEP_CURRENTLY_USED` |
| Stage content | Box capability counts are high for Push and Destroy-bearing combos; Flip-only/Item combos are low usage | mixed |
| Presentation carriers | `FlipImpactSignals` consumed by host track planning, VFX production runtime, block audio, and tests | `KEEP_CURRENTLY_USED` |
| Box flip driver | `BoxFlipInteractionDriver` attached to 4 production static box prefabs | `KEEP_CURRENTLY_USED` |
| Player hand flip driver | `PlayerFlipInteractionDriver.cs` and `.meta` had no production prefab/scene/asset GUID reference | `REMOVED_BY_PRODUCT_DECISION` |
| UI command route | Gateway remains for UI-held movement only; UI Push/Flip gameplay action command injection requests are removed | `REMOVED_BY_PRODUCT_DECISION` |
| Settings/rebind | Push/Flip display, rebinding, save/restore, and prefab rows are wired | `KEEP_CURRENTLY_USED` with hardcoded-path risk |
| Audio | Player prefab references production action-audio profile; moment coverage is uneven | mixed |

## Dead / Near-Dead Findings

- `PushChange_Legacy` / `FlipChange_Legacy`: removed. No further deletion.
- `MovePush`, `AutoPush`, `ContactPush`, `PushThreshold`, `pushContactTicks`: no active runtime artifact found; remaining current docs only mention these as historical/archive wording. Candidate: docs-only cleanup.
- `PushBox`, `FlipBox`, `MovableBox`, `InteractableBox`, `PlayerPushController`, `PlayerFlipController`: no active component/runtime artifact found. Some test/doc names contain `PushBox`/`FlipBox` as scenario vocabulary, not old component classes.
- `ActionBar`: no runtime type found. Current architecture docs mention it only as retired HUD vocabulary.
- `HelpScreen` Push/Flip prompt: no active prompt artifact found.
- `LegacyMovementBoundaryAssert`: no asset/code helper found; docs-only historical cleanup candidate.
- Player hand flip presentation driver: `PlayerFlipInteractionDriver.cs` and `PlayerFlipInteractionDriver.cs.meta` are removed. The path depended on unavailable player hand/IK support and had no production prefab/scene/asset GUID reference.

## Meaningless / Low-Value Findings

- `GameplayInputHost` subscribes Flip to both `started` and `performed`; Push subscribes only `started`. Since both Flip callbacks set the same bool buffer, this is potentially duplicate edge handling. It is still behavior-affecting for Input System interaction differences, so classify as `NEEDS_PRODUCT_DECISION`, not immediate deletion.
- `RemovedLegacyFallbackDiagnosticsEnabled` is the canonical removed-fallback diagnostic routing field. It must not be described as fallback authorization.
- `SourceActionPlanId` and `IntentId` are active correlation/determinism fields. They are not deletion candidates.
- `PresentationId` in authoring placements is active editor/content presentation selection. Generated gameplay spawn-side rows are absent, so no generated migration miss was found.

## Tests-Only Findings

Direct Push/Flip command construction is broad:

- `PlayerTickCommand.Push(...)`: 69 calls across 11 gameplay test files.
- `PlayerTickCommand.Flip(...)`: 63 calls across 13 gameplay test files.
- `PlayerTickCommand.Create(... pushPressed: true)`: 2 calls.
- `PlayerTickCommand.Create(... flipPressed: true)`: 0 calls.

Most direct command tests are governance for runtime contracts, not obsolete compatibility. The two `Create(... pushPressed: true)` cases are the strongest review candidates because they preserve a low-level command shape rather than product input surface.

Classification:

- Scenario/core/replay Push/Flip command tests: `KEEP_TEST_GOVERNANCE`.
- Direct `Create(... pushPressed: true)` no-direction edge tests: `REFACTOR_DUPLICATE_TEST` candidate only after confirming physical/playmode coverage.
- Plain move into push box suppression tests: `KEEP_TEST_GOVERNANCE`; this protects explicit Push semantics and prevents ordinary movement from silently becoming Push again.

## Content-Usage Findings

Box capability counts from `Assets/_Features/Stages/Content` authoring assets, filtered to `Kind: 2` box placements:

| Capability value | Meaning | Count | Content notes |
| --- | --- | ---: | --- |
| `0` | None | 10 | campaign stages only |
| `1` | Push | 85 | campaign + showcase |
| `2` | Flip | 2 | showcase only |
| `3` | Push+Flip | 2 | showcase only |
| `4` | Item | 1 | showcase only |
| `5` | Push+Item | 1 | showcase only |
| `6` | Flip+Item | 1 | showcase only |
| `7` | Push+Flip+Item | 1 | showcase only |
| `9` | Push+Destroy | 30 | tutorial-scene 28, showcase 2 |
| `10` | Flip+Destroy | 0 | unused combo |
| `11` | Push+Flip+Destroy | 6 | tutorial-scene 4, showcase 2 |
| `27` | Push+Flip+Destroy+JumpCrushable | 708 | broad campaign content |

Low-usage combos are all showcase-only: `Flip`, `Push+Flip`, `Item`, `Push+Item`, `Flip+Item`, `Push+Flip+Item`. They should be product/content decisions before deleting rules. `Flip+Destroy` is a true zero-content combo and can be used to simplify docs/tests if no runtime branch explicitly depends on that exact combo.

## Input-Specific Findings

- `Player/Push`: keyboard `E` and gamepad `buttonNorth`.
- `Player/Flip`: keyboard `Q` only.
- Input policy conclusion: Flip remains keyboard-only by current product decision. Do not add a controller binding or prompt expectation in this cleanup PR.
- Generated input wrapper residue: no generated wrapper code was found.
- InputActionReference serialized residues: no Push/Flip-specific stale reference was found beyond the active InputActionAsset and settings/rebind path.

## UI/Prompt Findings

- UI Push/Flip gameplay action command injection requests had no production UI caller. `HUDRootPresenter` no longer depends on the command gateway in its constructor, and no touch/mobile/assist action button exists.
- Settings/rebind Push/Flip rows are active: `PushInputRow`, `FlipInputRow`, `PushKeyDisplay`, `FlipKeyDisplay`.
- Rebind is effective, not display-only: `KeyboardBindingSettingsService` applies binding overrides, serializes Push/Flip overrides, and `GameplayHostRuntimeFactory` applies saved settings to the runtime InputActionAsset.
- Rebind limitation: keyboard-only; gamepad bindings are not managed by this service.
- Path contract: `GameplayInputHost` and `KeyboardBindingSettingsService` share `GameplayInputActionPaths` for Player/Move, Player/Push, Player/Flip, and UI/Navigate. Missing required settings actions or Push/Flip keyboard bindings fail fast during settings binding service setup.

## Audio Findings

Production asset:

- `Player_S1.prefab` references `Player_S1_GameplayActionAudioProfile.asset` by GUID `42a2e109fc5141ec9e866925a0a85c3b`.
- Old `_Test` name is absent.

Moment coverage:

| Action | Moment | Planner can emit | Profile state | Verdict |
| --- | --- | --- | --- | --- |
| Push | Windup | yes | assigned binding | keep |
| Push | Execute | yes | no entry | optional v1 lifecycle moment |
| Push | Recovery | yes | no entry | optional v1 lifecycle moment |
| Flip | Windup | yes | assigned binding | keep |
| Flip | Execute | yes | no entry | optional v1 lifecycle moment |
| Flip | Recovery | yes | no entry | optional v1 lifecycle moment |
| Push/Flip | AssistOutOfRange/NoTarget/Invalid | yes, attempt signals | assigned bindings | keep |

`Contact`, `ImpactEnemy`, and `Blocked` are not current `GameplayActionAudioMoment` members and are not emitted by the current production action-audio planner. Impact and blocked presentation/audio remain owned by their existing gameplay presentation lanes.

## Presentation Findings

- `FlipImpactPresentationSignal` is consumed by `GameplayTrackPlanner`, `GameplayExitPresentationController`, `PlayerViewPresentationMapper`, VFX production runtime, `FlipImpactBurstVfxRequestPlanner`, and block audio. Keep.
- `PlayerActionAttemptSignals` are consumed by action audio, input buffering clear logic, `GameplayTrackPlanner`, and `PlayerViewPresentationMapper`. Keep.
- `BoxFlipInteractionDriver` is attached to 4 static box prefabs: tutorial, metal, moon showcase, gravity block. Keep.
- The player hand flip presentation driver is removed. `BoxFlipInteractionDriver` remains production-authored and owns box-side flip interaction visuals.
- `GameplayBoxCapabilityLabelViewFactory` runtime class was not found. Remaining mentions are docs/stale-ledger/test-name history. Candidate: docs cleanup only.

## Risk Summary

- Immediate deletion risk is low only for docs-only stale residue.
- UI command gateway deletion is medium/high because it is a public UI-access interface and tests exercise it, even though product UI does not call it.
- Capability simplification risk is high without content owner approval because campaign content heavily uses `Push+Flip+Destroy+JumpCrushable` and `Push+Destroy`.
- Audio cleanup risk is medium: deleting optional null entries changes authoring diagnostics and tests; adding bindings changes audible product behavior.
- API/compat cleanup was high risk because it changed runtime flag shape, replay traces, and named constructor/API uses.

## Final Recommendation

Immediate deletion:

- Docs-only stale Push contact threshold wording and stale ledger residue.
- Old HelpScreen/ActionBar wording where it is not explicitly documenting retired status.

Test/docs migration before deletion:

- Direct low-level `PlayerTickCommand.Create(... pushPressed: true)` edge tests if covered by physical/playmode input tests.
- Historical `LegacyMovementBoundaryAssert` docs if no helper exists.

Product/content decision before deletion or simplification:

- Showcase-only capability combos and item priority rules.
- Empty/no-entry action-audio moments.

API/replay compatibility decision:

- old diagnostics API projection removal.

Do not delete:

- Current physical InputActions, host route, command fields, Push/Flip runtime branches, broad campaign box capabilities, `FlipImpactSignals`, `PlayerActionAttemptSignals`, `BoxFlipInteractionDriver`, production action-audio profile asset, current settings/rebind rows, and canonical removed-fallback diagnostics routing.
