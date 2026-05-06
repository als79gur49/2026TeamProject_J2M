# TileFeature Hardening Readiness - 2026-05-07

## A. Changed Files

- `Docs/Architecture/ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core/TileFeatureOverlayArchitectureTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/MoonBlockGeneratorRespawnTests.cs`
- `Docs/Testing/TileFeature-Hardening-Readiness-2026-05-07.md`

## B. Hardening Items

- ADR-006 was updated from the original gate/future-surface shape to the current implemented TileFeature pipeline.
- Architecture guards now keep prohibited EntityType, BoxCapabilities, TerrainFlags, UI, TickPipeline, visual, audio, and `MoonBlockGeneratorBlocked` surfaces closed.
- MoonBlockGenerator defer paths remain silent and do not produce blocked presentation facts.

## C. ADR-006 Update

ADR-006 now records TileFeature as a `SurfaceCell` overlay layer, not occupancy, not EntityType, not TerrainFlags, and not a presentation binding owner. It records the implemented order from `TileFeatureState` through `MoonBlockGenerated` and keeps Dynamic TileEffect mutation after state/query/export/hash.

## D. Feature Policy Results

- Button: `TileFeatureFlags.Activated` is runtime latch state; objective evidence is final `WorldSnapshot` only.
- MoonBlock: identity remains `EntityType.Box + BoxArchetype.Moon`; `EntityType.MoonBlock` and `BoxCapabilities.Moon` remain forbidden.
- DestroyTile: moving-contact facts are transient resolver input; final snapshot scanning is not authority.
- SlideTile: redirect changes facing only; DestroyTile wins when both would affect the same box.
- Barricade: movement block and active-transition crush remain distinct sources and event kinds.
- Exit: open is derived from objective state plus active Exit; no mutable TileFeature flag or UI/StageResult change is introduced.
- MoonBlockGenerator: generator-bound initial MoonBlock remains the stable template/id source; unit and wall-like conflicts defer silently.

## E. MoonBlockGeneratorBlocked Closed Policy

`MoonBlockGeneratorBlocked` remains unimplemented. There is no event kind, request kind, audio cue, or visual target interface. Unit defer, wall-like defer, inactive generator, live MoonBlock no-op, invalid/skipped path, and repeated defer emit no blocked event/request/audio/visual.

Future work must first decide debounce/noise policy, first-vs-every-tick emission, blocker identity changes, UnitDefer vs SolidBlocked split, inactive-generator silence, wall-like validation severity, optional/throttled audio, generator-local visual scope, and transient presentation memory.

## F. Added Architecture Guards

- Forbidden TileFeature entity names include Exit.
- Forbidden TerrainFlags effect tokens include MoonBlockGenerator, Barricade, and Exit.
- UI source must not call `WorldState.CreateSnapshot` or consume Tile presentation facts as objective evidence.
- Host and TileFeatureAudio must not infer TileFeature state through `WorldState.CreateSnapshot`.
- `MoonBlockGeneratorBlocked` must stay absent from event/request/audio/visual surfaces.
- TileFeatureAudio must not add Play3D/spatial or `StagePresentationDefinition` binding references.
- TickPipeline must not reference request planner/request, TileFeatureAudio, TileFeatureVisual, audio playback, prefabs, GameObjects, UI, Animator, ParticleSystem, or UnityEvent.

## G. Search Results

- `EntityType.TileFeature|EntityType.MoonBlock|EntityType.SlideTile|EntityType.DestroyTile|EntityType.Barricade|EntityType.Exit`: no matches.
- `BoxCapabilities.Moon`: no matches.
- TerrainFlags effect semantic search: no forbidden flag values.
- Public `WorldState` TileFeature mutation API search: no matches.
- `WorldState.CreateSnapshot` in Gameplay_Host, Gameplay_TileFeatureAudio, UI: no matches.
- `TilePresentationRequestPlanner|TilePresentationRequest` in `TickPipeline*.cs`: no matches.
- `TileFeatureAudio` in UI, Gameplay_Audio, Gameplay_ActionAudio: no matches.
- TileFeature visual binding/prefab in `StageRuntimeBuildResult.cs` and `StageDefinition.cs`: no matches.
- `MoonBlockGenerated` exists intentionally; `MoonBlockGeneratorBlocked` remains absent.
- `Play3D|spatial|Spatial` hits are allowed docs/tests, `SpatialState` gameplay vocabulary, or 2D audio implementation details such as `spatialBlend = 0`.

## H. Test Results

Baseline observations before this hardening implementation:

- `git diff --check`: pass.
- `Game.Feature.Gameplay.Tests.csproj`: build pass.
- `Game.Feature.Gameplay.PlayModeTests.csproj`: build pass.
- `Game.Feature.Stages.Editor.Tests.csproj`: build pass.
- `PROJECT_PATH_WIN="$(wslpath -w "$PWD")" ./run_tests.sh core`: pass, EditMode `32/0`, PlayMode `2/0`.
- Direct targeted Unity smoke `MoonBlockGeneratorRespawnTests`: pass, `10/0`.
- Direct full EditMode: `2993 total`, `97 failed`, `1 skipped`.

Post-change validation:

- `git diff --check`: pass.
- `Game.Feature.Gameplay.Tests.csproj`: build pass.
- `Game.Feature.Gameplay.PlayModeTests.csproj`: build pass.
- `Game.Feature.Stages.Editor.Tests.csproj`: build pass.
- Targeted Unity EditMode `TileFeatureOverlayArchitectureTests;MoonBlockGeneratorRespawnTests`: pass, `33/0`.
- `PROJECT_PATH_WIN="$(wslpath -w "$PWD")" ./run_tests.sh core`: pass, EditMode `35/0`, PlayMode `2/0`.
- Direct full EditMode: `2997 total`, `97 failed`, `1 skipped`.

## I. Remaining Known Red / Baseline Debt

- `./run_tests.sh full` fails before Unity because it hardcodes `2026TeamProject_J2M.sln`; actual solution is `2026teamproject_j2m-board-tile-feature.sln`.
- Full EditMode baseline is red: `97` failures.
- `FullEditModeKnownFailureBaselineTests` still expects `95`, so the extracted baseline list is stale.
- CombinedShowcase drift failures are not TileFeature-specific.
- GameplayAudioHostOrchestration trace mismatch is in the core GameplayAudio ordering lane, not TileFeatureAudio.
- Time-limit-only objective failures are unrelated to Button, MoonBlock, and Exit TileFeature authority.
- UI targeted HUD layout failure is unrelated to TileFeature UI/HUD notifications.
- `WorldState.cs` and `TileFeatureRuntimeStateTests.cs` were not dirty in the pre-implementation exploration snapshot.

## J. Kept Unimplemented

GravityField, `MoonBlockGeneratorBlocked`, new TileFeature effects, UI/HUD notifications, StageResult/ObjectiveStatus changes, spatial audio/Play3D, StagePresentationDefinition TileFeature audio binding, generator-only MoonBlock templates, multiple generators/MoonBlocks, Unit kill/eject, Projectile destroy, TerrainFlags effect semantics, TileFeature/MoonBlock/Barricade/Exit EntityTypes, `BoxCapabilities.Moon`, public WorldState TileFeature mutation API, and TickPipeline direct prefab/audio/UI execution remain closed.

## K. GravityField Readiness

GravityField may proceed after the post-change TileFeature-specific gate is green. The existing full-suite baseline red is not a GravityField blocker unless a failure touches TileFeature authority or presentation boundaries.

## L. TileFeature-Specific Blocker Judgment

No TileFeature-specific blocker is known after this hardening pass, assuming the updated core/targeted tests pass. Remaining baseline debt is operational or pre-existing feature-suite debt.
