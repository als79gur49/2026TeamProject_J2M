# Proposed Deletion PR Plan

## PR 1: unused input binding / prompt / adapter cleanup

Scope:

- Clean only input/UI residue proven not to be active.
- Do not delete `Player/Push`, `Player/Flip`, `GameplayInputHost`, `GameplayHostCommandGateway`, `PlayerTickCommand.PushPressed`, or `PlayerTickCommand.FlipPressed`.

Files:

- `Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab`
- Related UI settings presenter/view files only if serialized references prove the legacy-named objects are inactive or duplicate.
- Input docs if they still mention retired ActionBar/HelpScreen prompt ownership.

Expected deletions:

- Remove inactive duplicate legacy Push/Flip duplicate prefab objects if Unity inspection confirms they are not referenced.
- Otherwise rename them to current vocabulary and keep references intact.

Migration required:

- Unity prefab serialized reference validation.
- If object names are active, prefer rename over delete.

Tests to run:

- `./run_tests.sh ui`
- Targeted settings input UI tests if available.

Risk:

- Medium. Prefab references can break silently if edited without Unity validation.

Rollback:

- Restore prefab and rerun UI lane.

## PR 2: docs-only / stale legacy Push/Flip residue cleanup

Scope:

- Remove or consolidate stale docs that imply current Push uses contact accumulation or threshold timing.
- Consolidate retired ActionBar vocabulary around current HUDRoot/PlayerStatus query ownership.
- Preserve archive docs if archive retention policy requires historical records.

Files:

- `Docs/Architecture/Immediate-Push-Input-Semantics.md`
- `Docs/Architecture/UI-Architecture-Guidelines.md`
- `Docs/Archive/Architecture/*` only if archive cleanup is allowed.
- Any stale ledgers mentioning removed Push/Flip behavior as current.

Expected deletions:

- Non-archive stale wording only.
- No C# or prefab deletion.

Migration required:

- None for runtime.
- Docs governance review for archive policy.

Tests to run:

- No runtime tests required for docs-only change.
- If docs are asserted by boundary tests, run `./run_tests.sh core` or the specific boundary inventory tests.

Risk:

- Low for docs-only; medium if tests assert exact docs vocabulary.

Rollback:

- Restore docs wording.

## PR 3: legacy compat shim migration 후 삭제

Scope:

- Migrate and then remove legacy ordinary fallback compatibility aliases where owners approve compatibility churn.
- This is not Push-specific, but it is the concrete legacy movement/input compatibility residue found during the audit.

Files:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayRuntimeFeatureFlags.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/BoundaryInventoryScenarioTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/EnemyKinematicLocomotionReplayTests.cs`
- `Docs/Testing/Legacy-Ordinary-Unit-Movement-*.md`
- `Docs/Architecture/ADR/ADR-005-Grid-Authoritative-Unit-Kinematics.md`

Expected deletions:

- `RemovedDiagnosticBaselineAlias` after consumers migrate to `removed diagnostic baseline preset (historical, deleted)`.
- `RemovedDiagnosticHelperAlias` after tests/docs no longer require alias compatibility.
- Old diagnostics API projection after the C안 API/replay decision; keep `RemovedLegacyFallbackDiagnosticsEnabled` as the canonical field.

Migration required:

- Replay/golden trace compatibility review.
- Test helper and docs vocabulary migration.
- Possible `[Obsolete]` warning phase before removal.

Tests to run:

- `./run_tests.sh core`
- `BoundaryInventoryScenarioTests`
- `TickReplayDeterminismTests`
- `EnemyKinematicLocomotionReplayTests`

Risk:

- High. This touches public runtime flag shape and diagnostic determinism.

Rollback:

- Restore aliases/field and docs/test expectations.

## PR 4: UI/audio naming 또는 asset migration

Scope:

- Rename misleading assets/vocabulary that are active but look test-only or retired.
- Do not delete production-bound Push/Flip audio or presentation drivers.

Files:

- `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset`
- `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset.meta`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayActionAudioRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/AudioRepositoryAssetSmokeCoreTests.cs`
- Docs that name the profile.

Expected deletions:

- No audio behavior deletion.
- Rename `_Test` profile to production vocabulary, or split a true test fixture if product owners want separate assets.

Migration required:

- Preserve GUID by file rename in Unity, or update prefab GUID references if a new asset is created.
- Update test constants and docs.

Tests to run:

- `./run_tests.sh core`
- `GameplayActionAudioRuntimeTests`
- `AudioRepositoryAssetSmokeCoreTests`

Risk:

- Medium-high. Deleting or regenerating the profile incorrectly breaks production player prefab audio.

Rollback:

- Restore profile asset/meta/prefab reference.

## Explicit Non-Deletion Items

These should not be deleted in any cleanup PR without a new feature removal decision:

- `Assets/InputSystem_Actions.inputactions` `Player/Push` and `Player/Flip`.
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs` physical Push/Flip route.
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/UIAccess/GameplayHostCommandGateway.cs` UI-held movement command route.
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/PlayerTickCommand.cs` `PushPressed` / `FlipPressed`.
- `PlayerActionKind.Push` / `PlayerActionKind.Flip`.
- `PlayerActionRuntimeState`.
- `MovementExpander` Push/Flip branches.
- `BoxCapabilities.Push` / `BoxCapabilities.Flip`.
- `FlipImpactPresentationSignal` and `BoxFlipInteractionDriver`.
- `GameplayActionKind.Push` / `GameplayActionKind.Flip`.
- `Player_S1_GameplayActionAudioProfile.asset` until renamed/migrated.
