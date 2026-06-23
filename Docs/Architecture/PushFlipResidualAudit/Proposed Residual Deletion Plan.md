# Proposed Residual Deletion Plan

## PR 1: Pure Dead Docs/Test Residue Cleanup

Scope:

- Remove or rewrite stale docs-only Push contact threshold wording.
- Remove stale HelpScreen/ActionBar wording unless it explicitly documents retired status.
- Clean historical references to `GameplayBoxCapabilityLabelViewFactory` where they are not deliberate archive/stale-ledger provenance.

Files:

- `Docs/Architecture/Immediate-Push-Input-Semantics.md`
- `Docs/Testing/Lane-A-Live-Row-Ledger-2026-04-22.md`
- `Docs/DeferredStaleLedger.md`
- Other docs found by targeted `rg`.

Delete candidates:

- `pushContactTicks`, `PushThreshold`, old HelpScreen prompt wording, stale label factory rows.

Migration needed:

- None for runtime.

Required owner decision:

- None, unless stale ledger policy requires preserving historical rows.

Tests to run:

- Docs-only: no lane required.
- If UI governance docs/tests are edited: `./run_tests.sh ui`.

Risk:

- Low.

Rollback:

- Revert docs-only commit.

## PR 2: Tests-Only Duplicate Command Coverage Review

Scope:

- Review the 2 direct `PlayerTickCommand.Create(... pushPressed: true)` tests.
- Replace with physical/host input coverage only if equivalent.
- Keep direct Push/Flip command scenario/core/replay tests that pin gameplay contracts.

Files:

- `PlayerControlScenarioTests.cs`
- `PlayerMovementInputTests.cs`
- Potentially `PlayerMovementPlayModeTests.cs`

Delete candidates:

- Low-level no-direction command-shape duplicate tests only.

Migration needed:

- Add or point to host/physical no-op coverage before deletion.

Required owner decision:

- Gameplay test owner should confirm command-shape edge is not a public replay contract.

Tests to run:

- `PlayerMovementInputTests`
- `PlayerMovementPlayModeTests`
- `MovementPhaseScenarioTests`
- `./run_tests.sh core`

Risk:

- Medium; command shape participates in replay/determinism contracts.

Rollback:

- Restore removed tests.

## PR 3: UI Command Route Cleanup

Scope:

- Remove the gameplay UI Push/Flip action command injection route.
- Keep UI-held movement through the gameplay command gateway.
- Keep Settings/rebind Push/Flip rows and keyboard rebinding; that UI is not the removed gameplay command injection route.
- Migrate fakes and tests to physical input, direct command coverage, or HUD ownership guards.

Files:

- `Assets/_Features/Gameplay/Gameplay_UIAccess/Runtime/Contracts/IGameplayCommandGateway.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/UIAccess/GameplayHostCommandGateway.cs`
- `Assets/_Features/UI/UI_Tests/EditMode/UiTestDoubles.cs`
- UI architecture tests and runtime board-bound tests that call command gateway.

Delete candidates:

- UI Push/Flip action request methods and buffers.

Migration needed:

- Test migration to physical input or host `BufferPush/BufferFlip`.

Required owner decision:

- Completed: current product has no touch/mobile/assist Push/Flip action button surface.

Tests to run:

- `./run_tests.sh core`
- `./run_tests.sh ui`
- `RuntimeBoardBoundsGuardTests`
- `UiArchitectureTests`

Risk:

- High; public UI-access interface and tests.

Rollback:

- Restore interface/gateway methods and tests.

## PR 4: Flip Keyboard-Only Input Policy

Scope:

- Document the current product input policy: Push/Flip gameplay actions are keyboard-driven for current product input; Push keeps its existing controller binding; Flip remains keyboard-only.
- Remove wording that frames the missing controller binding as an unresolved gap.

Files:

- Input/settings docs/tests.

Delete candidates:

- Docs/tests that expected controller support for Flip.

Migration needed:

- Rebind UI remains keyboard-only unless product asks for gamepad rebinding.

Required owner decision:

- Completed: keep Flip keyboard-only for this product baseline.

Tests to run:

- `./run_tests.sh core`
- `PlayerMovementInputTests`
- `PlayerMovementPlayModeTests`
- UI lane if prompt/settings UI changes.

Risk:

- Medium.

Rollback:

- Revert docs/test wording if product later adds a controller binding.

## PR 5: Low-Usage Capability Combo Decision and Migration

Scope:

- Retain showcase-only capability combos and Item priority rules for this PR.
- Do not simplify capability rules or stage content as part of keyboard/input route cleanup.

Files:

- `stage-4-2_Authoring.asset`
- Stage validation/generation docs/tests if content changes.
- Runtime tests only if rule simplification follows content migration.

Delete candidates:

- None in this PR.

Migration needed:

- Stage authoring regeneration and presentation binding validation.

Required owner decision:

- Completed for this PR: keep showcase-only content and general capability rules.

Tests to run:

- `./run_tests.sh core`
- `StageRuntimeBuilderTests`
- `StageContentAndClearFlowTests`
- `StageSceneBootstrapValidatorTests`
- Targeted movement tests for any rule deletion.

Risk:

- High for content migration; medium for docs-only combo cleanup.

Rollback:

- Restore stage assets and regenerated companions.

## PR 6: Presentation/Audio Residual Cleanup

Superseded note:
Later action-audio cleanup removed `Execute` and `Recovery` from the Push/Flip action-audio public surface. The current action-audio surface is `Windup`, `AssistOutOfRange`, `NoTarget`, and `Invalid`.
Gameplay action timeline still has execute/recovery.
Only action-audio moments were removed.

Scope:

- Remove the player hand flip interaction presentation path because current player IK support is unavailable.
- Preserve box-side flip presentation and all flip impact carrier consumers.
- Keep action-audio removed moment policy as already applied; no `Execute`/`Recovery` explicit-null/add-entry migration is planned.

Files:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerFlipInteractionDriver.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerFlipInteractionDriver.cs.meta`
- Player hand flip presentation related tests.
- `Player_S1_GameplayActionAudioProfile.asset`
- `GameplayActionAudioRuntimeTests.cs`
- `AudioRepositoryAssetSmokeCoreTests.cs`

Delete candidates:

- `PlayerFlipInteractionDriver.cs`, its `.meta`, and optional lookup path.

Migration needed:

- Tests migrate to box-side flip presentation / carrier presentation contracts.
- No audio profile migration in this PR.

Required owner decision:

- Completed for this PR: remove player hand interaction.
- Completed: action-audio `Execute`/`Recovery` removal decision.
- Future reintroduction requires a new public-surface decision.

Tests to run:

- `./run_tests.sh core`
- `GameplayActionAudioRuntimeTests`
- `AudioRepositoryAssetSmokeCoreTests`
- `PlayerMovementPlayModeTests`
- Manual/editor prefab validation if `Player_S1.prefab` changes.

Risk:

- Medium/high depending on prefab/audio behavior changes.

Rollback:

- Restore prefab/profile/component/test changes.

## PR 7: API/Replay Compatibility Cleanup

Scope:

- Keep `deleted legacy fallback diagnostic flag` as the canonical removed-fallback diagnostics field.
- Either keep with clearer docs, add `[Obsolete]` window, introduce canonical replacement field, or remove after replay/API migration.

Files:

- `GameplayRuntimeFeatureFlags.cs`
- Boundary/replay tests
- Migration docs

Delete candidates:

- Old diagnostics API projection removal and canonical constructor parameter migration after C안 approval.

Migration needed:

- Replay/golden trace review.
- External parser/diagnostic compatibility review.

Required owner decision:

- API/replay owner.

Tests to run:

- `./run_tests.sh core`
- `TickReplayDeterminismTests`
- `ActionPlanCorrelationContractTests`
- `BoundaryInventoryScenarioTests`

Risk:

- High.

Rollback:

- Restore field/constructor/test compatibility shape.
