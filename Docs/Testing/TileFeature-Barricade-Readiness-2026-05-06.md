# TileFeature Barricade Readiness - 2026-05-06

## A. Changed Files

- `Docs/Architecture/ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core/TileFeatureOverlayArchitectureTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/TileFeatureEffectResolverTests.cs`
- `Docs/Testing/TileFeature-Barricade-Readiness-2026-05-06.md`

## B. Hardening Completed

- Documented current TileFeature policy for Button, MoonBlock, DestroyTile, SlideTile, presentation requests, visual path, audio path, and StagePresentationDefinition binding.
- Added architecture guards for forbidden TileFeature entity/capability modeling, public WorldState TileFeature mutation API exposure, visual binding ownership, and TileFeatureAudio lane separation.
- Updated the stale SlideTile test expectation to assert the already-implemented `SlideTileRedirected` events with `TargetEntityId` and `Direction`.
- Did not implement Barricade, MoonBlockGenerator, Exit, GravityField, new TileFeature behavior, same-tick Slide movement, spatial audio, or new public gameplay API.

## C. Policy Documentation / Test Coverage

- TileFeature remains a `SurfaceCell` overlay, not Unit/Solid occupancy, terrain flags, an `EntityType`, or any removed occupancy concept.
- TileFeature + Unit/Box same cell remains allowed. Wall-like solid overlap remains future explicit policy. Same-cell TileFeature storage and gameplay policy are separated.
- Button latch is documented as `TileFeatureFlags.Activated` runtime state. Authored initial Activated remains disallowed. Condition completion reads final snapshot state, not presentation events or requests.
- MoonBlock identity remains `EntityType.Box + BoxArchetype.Moon`; `EntityType.MoonBlock` and `BoxCapabilities.Moon` remain forbidden.
- DestroyTile remains movement-contact based and does not consume the tile feature. Stationary/spawn/topology/follow-through/flip landing false positives remain excluded by policy.
- SlideTile remains facing-retarget only, FrontFaceOnly, cardinal direction, selector None, PushEnter/SlideEnter plus Push/Slide ImpactFollowThrough, with DestroyTile winning.
- Presentation events remain presentation-only. Request planner converts events to requests and does not decide gameplay.
- Visual and audio consumers remain request-cache consumers, not authority readers.

## D. Additional Architecture Guards

- `EntityType_DoesNotContainTileFeatureKinds`
- `WorldState_DoesNotExposePublicTileFeatureMutationApis`
- `TileFeatureOverlayGate_DocumentsImplementedPolicyBeforeBarricade`
- `TileFeatureAudio_RemainsSeparateFromUiCoreAndActionAudioLanes`
- `TileFeatureVisualBinding_RemainsPresentationOwned` now also asserts StagePresentationDefinition owns visual binding and has no TileFeatureAudio binding.

## E. Search Results

All requested searches were executed.

- `rg "EntityType\.TileFeature|EntityType\.MoonBlock|EntityType\.SlideTile|EntityType\.DestroyTile|EntityType\.Barricade" Assets`: no matches.
- `rg "BoxCapabilities\.Moon" Assets`: no matches.
- `rg "Trap|Hazard|Buff|Trigger|Aura|TileFeature" Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/TerrainFlags.cs`: no matches.
- `rg "public void (AddTileFeature|UpdateTileFeature|RemoveTileFeature)" Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs`: no matches.
- `rg "WorldState\.CreateSnapshot" Assets/_Features/Gameplay/Gameplay_Host Assets/_Features/Gameplay/Gameplay_TileFeatureAudio Assets/_Features/UI -g "*.cs"`: no matches.
- `rg "TilePresentationRequestPlanner|TilePresentationRequest" Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline*.cs`: no matches.
- `rg "TileFeatureAudio" Assets/_Features/UI Assets/_Features/Gameplay/Gameplay_Audio Assets/_Features/Gameplay/Gameplay_ActionAudio -g "*.cs"`: no matches.
- `rg "TileFeaturePresentationBinding|VisualPrefab" Assets/_Features/Stages/Runtime/StageRuntimeBuildResult.cs Assets/_Features/Stages/Runtime/StageDefinition.cs`: no matches.

## F. Test Results

- `git diff --check`: passed.
- `dotnet --version`: failed in WSL with `dotnet: command not found`.
- Windows dotnet executable builds passed:
  - `Game.Feature.Gameplay.Tests.csproj`
  - `Game.Feature.Gameplay.PlayModeTests.csproj`
  - `Game.Feature.Stages.Editor.Tests.csproj`
- `PROJECT_PATH_WIN="$(wslpath -w "$PWD")" ./run_tests.sh core`: passed.
  - Core EditMode: 31 total, 0 failed.
  - Core PlayMode: 2 total, 0 failed.
- Targeted Unity EditMode selection: 350 total, 339 passed, 11 failed.
  - Previous TileFeature stale red `TileFeatureEffectResolverTests.SlideTile_ActiveFrontFace_RedirectsPushEnterAndSlideEnterBoxes` is no longer failing.
- `PROJECT_PATH_WIN="$(wslpath -w "$PWD")" ./run_tests.sh full`: blocked before Unity full run by hardcoded missing solution `2026TeamProject_J2M.sln`; actual repo solution is `2026teamproject_j2m-board-tile-feature.sln`.

## G. Remaining Known Red / Baseline Debt

Current targeted red list is not TileFeature policy blocking:

- `GameplayTickPresentationCoordinatorTests.EnemyPupilVisualController_WindupAttackRecover_AnimatesBorderSequence`
- `GameplayTickPresentationCoordinatorTests.GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx`
- `GameplayTickPresentationCoordinatorTests.GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_Completes_ClearsDeathAnimationState`
- `StageObjectiveSystemTests.Conditions_ClearWithinTimeLimit_UsesCeilDeadlineFromTiming` parameterized cases
- `StageRuntimeBuilderTests.StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract`
- `WorldSnapshotAndPresentationTests` enemy jump, flip destroy-self, and follow-through/debug-spawn presentation failures

Existing full-suite baseline file already tracks known failures including:

- `GameplayAudioHostOrchestrationTests` trace/order expectation mismatch. This is core GameplayAudio host orchestration, not TileFeatureAudio.
- Clear-within-time-limit objective failures. These are objective validation/baseline debt, not Button/MoonBlock/TileFeature.
- CombinedShowcase configured contract drift. This is showcase content/count drift, not TileFeature behavior.

`WorldState.cs` and `TileFeatureRuntimeStateTests.cs` are not dirty in the current workspace snapshot.

## H. Still Unimplemented

- Barricade.
- Barricade blocker query.
- Barricade active-transition crush.
- Barricade visual/audio/event.
- MoonBlockGenerator.
- Exit open/clear.
- GravityField.
- Same-tick Slide extra movement.
- Recursive Slide redirect.
- Spatial or Play3D tile audio.
- StagePresentationDefinition TileFeature audio binding.
- TerrainFlags TileFeature/effect semantics.
- EntityType TileFeature/Barricade.
- WorldState public TileFeature mutation API.
- TickPipeline prefab/audio/UI direct execution.

## I. Barricade Readiness

TileFeature-specific gate is passable for the next feature after this hardening pass:

- Searches for forbidden modeling are clean.
- Core gate passes.
- Windows dotnet builds pass.
- The targeted TileFeature stale red was resolved.
- Remaining targeted failures are known non-TileFeature presentation/objective/showcase debts.

## J. Remaining Required Blockers Before Barricade

No TileFeature-policy blocker remains for starting Barricade planning/implementation.

Operational blockers remain outside TileFeature:

- Fix or intentionally update `run_tests.sh full` solution path if full-suite execution is required as a hard gate.
- Keep known red debt separated from Barricade acceptance criteria unless a failure starts touching Barricade or TileFeature authority.
