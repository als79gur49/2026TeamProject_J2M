# Combined Gameplay Showcase Board Tile / Tile Feature Visual QA

Date: 2026-05-08
Target stage: `combined-gameplay-showcase`
Scope: presentation-only asset authoring and validation. No gameplay behavior, TickPipeline, WorldState, TileEffect, TerrainFlags, StageDefinition schema, StageRuntimeBuildResult schema, UI, audio, or VFX playback system changes were made.

## A. Created / Modified Assets

Created board presentation assets under `Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Board/`:
- `Catalogs/BoardTilePresentationCatalog_CombinedGameplayShowcase.asset`
- `Prefabs/BoardTile_GenericDefault.prefab`
- `Prefabs/BoardTile_ActiveBottom.prefab`
- `Prefabs/BoardTile_ActiveFront.prefab`
- `Prefabs/BoardTile_DecorativeTop.prefab`
- `Prefabs/BoardTile_DecorativeBack.prefab`
- `Materials/M_BoardTile_GenericDefault.mat`
- `Materials/M_BoardTile_ActiveBottom.mat`
- `Materials/M_BoardTile_ActiveFront.mat`
- `Materials/M_BoardTile_DecorativeTop.mat`
- `Materials/M_BoardTile_DecorativeBack.mat`

Created TileFeature presentation assets under `Assets/_Features/Stages/Stage_CombinedGameplayShowcase/TileFeature/`:
- `Catalogs/TileFeaturePresentationCatalog_CombinedGameplayShowcase.asset`
- `Prefabs/TileFeature_Button_Default.prefab`
- `Prefabs/TileFeature_Button_MoonOnly.prefab`
- `Prefabs/TileFeature_Destroy_Default.prefab`
- `Prefabs/TileFeature_Slide_Up.prefab`
- `Prefabs/TileFeature_Slide_Right.prefab`
- `Prefabs/TileFeature_Slide_Down.prefab`
- `Prefabs/TileFeature_Slide_Left.prefab`
- `Prefabs/TileFeature_Slide_Right_DirectVariant.prefab`
- `Prefabs/TileFeature_Barricade_Default.prefab`
- `Prefabs/TileFeature_Exit_Default.prefab`
- `Prefabs/TileFeature_MoonGenerator_Default.prefab`
- `Materials/M_TileFeature_Button.mat`
- `Materials/M_TileFeature_Button_MoonOnly.mat`
- `Materials/M_TileFeature_Destroy.mat`
- `Materials/M_TileFeature_Slide.mat`
- `Materials/M_TileFeature_Slide_DirectVariant.mat`
- `Materials/M_TileFeature_Barricade.mat`
- `Materials/M_TileFeature_Exit.mat`
- `Materials/M_TileFeature_MoonGenerator.mat`
- `Materials/M_TileFeature_DarkLine.mat`
- `Materials/M_TileFeature_LightAccent.mat`

Modified stage content:
- `Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase_Presentation.asset`
- `Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase_Authoring.asset`
- `Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase.asset`

## B. BoardTilePresentationCatalog Entries

- `board.generic.default`: `GenericDefault`, default for role, prefab plus material fallback.
- `board.active.bottom`: `ActiveBottom`, default for role, prefab plus material fallback.
- `board.active.front`: `ActiveFront`, default for role, prefab plus material fallback.
- `board.decorative.top`: `DecorativeTop`, default for role, prefab plus material fallback.
- `board.decorative.back`: `DecorativeBack`, default for role, prefab plus material fallback.

All board tile prefabs use primitive mesh art with root scale `(1,1,1)`. Materials are authored on renderers and also present as catalog fallbacks.

## C. TileFeaturePresentationCatalog Entries

- `button.default`: `Button`, `ReplaceBaseTile`, default for kind.
- `button.moon-only`: `Button`, `ReplaceBaseTile`.
- `destroy.default`: `Destroy`, `ReplaceBaseTile`, default for kind.
- `slide.up`: `Slide`, `ReplaceBaseTile`, `DirectionHint=Up`.
- `slide.right`: `Slide`, `ReplaceBaseTile`, `DirectionHint=Right`.
- `slide.down`: `Slide`, `ReplaceBaseTile`, `DirectionHint=Down`.
- `slide.left`: `Slide`, `ReplaceBaseTile`, `DirectionHint=Left`.
- `barricade.default`: `Barricade`, `Overlay`, default for kind.
- `exit.default`: `Exit`, `ReplaceBaseTile`, default for kind.
- `moon-generator.default`: `MoonBlockGenerator`, `ReplaceBaseTile`, default for kind.

Each TileFeature visual prefab includes `TileFeatureVisualTargetView`. Slide prefabs visibly encode direction; the direct right-slide variant has a distinct material.

## D. BoardTilePresentationOverrides Used

- `SurfaceCell(face=0,x=12,y=1)` -> `board.decorative.top`
- `SurfaceCell(face=3,x=2,y=6)` -> `board.decorative.back`

## E. TileFeature Placements And PresentationKey

- `901`: `SurfaceCell(face=0,x=2,y=1)`, `Button`, `AnyPushableBox`, empty key for default catalog resolve.
- `902`: `SurfaceCell(face=0,x=12,y=1)`, `Button`, `MoonBlockOnly`, key `button.moon-only`.
- `903`: `SurfaceCell(face=0,x=10,y=1)`, `Destroy`, empty key for default catalog resolve.
- `904`: `SurfaceCell(face=0,x=10,y=3)`, `Slide`, `Direction=Up`, key `slide.up`.
- `905`: `SurfaceCell(face=0,x=11,y=3)`, `Slide`, `Direction=Right`, key `slide.right`.
- `906`: `SurfaceCell(face=0,x=10,y=5)`, `Barricade`, empty key for default catalog resolve.
- `907`: `SurfaceCell(face=3,x=0,y=6)`, `Exit`, empty key for default catalog resolve.
- `908`: `SurfaceCell(face=0,x=12,y=4)`, `MoonBlockGenerator`, `BoundEntityId=201`, empty key for default catalog resolve.
- `910`: `SurfaceCell(face=0,x=11,y=5)`, `Button`, key `button.default`.
- QA moon box `EntityId=201`: `SurfaceCell(face=0,x=12,y=2)`, `BoxArchetype=Moon`, capabilities set to current validator-required push/flip/destroy mask.

Current validator constraints required Slide and Barricade QA features to use `FrontFaceOnly` with selector `None`, and Exit to use `BottomFaceOnly`.

Objective connection: `ButtonActivated_Tile901.asset` is linked as a required `SecondaryGoal` condition with stable id `button-901`. The existing Exit zone remains the `PrimaryGoal`, so `RequireAllConditions` now requires both Button 901 activation and the Exit primary goal before the stage is cleared.

## F. ReplaceBaseTile Cells

- `SurfaceCell(face=0,x=2,y=1)`: `button.default`.
- `SurfaceCell(face=0,x=12,y=1)`: `button.moon-only`.
- `SurfaceCell(face=0,x=10,y=1)`: `destroy.default`.
- `SurfaceCell(face=0,x=10,y=3)`: `slide.up`.
- `SurfaceCell(face=0,x=11,y=3)`: `slide.right`, direct prefab override.
- `SurfaceCell(face=3,x=0,y=6)`: `exit.default`.
- `SurfaceCell(face=0,x=12,y=4)`: `moon-generator.default`.
- `SurfaceCell(face=0,x=11,y=5)`: `button.default`.

Overlay cells expected to keep base board visible:
- `SurfaceCell(face=0,x=10,y=5)`: `barricade.default`.

Button visuals are authored as Button-owned replacement tiles, so Button cells are expected to suppress the base board tile rather than layer over it. The former same-cell Button-over-Destroy overlay check was removed to avoid duplicate `ReplaceBaseTile` suppressors on one `SurfaceCell`.

## G. Direct Override Usage

One direct TileFeature override is authored:
- `TileId=905` -> `TileFeature_Slide_Right_DirectVariant.prefab`

The catalog key remains `slide.right`, so the direct prefab wins while placement mode still resolves from the catalog entry.

## H. StageCatalogValidator / Validation Results

Passed:
- `TileFeaturePresentationCatalogTests`: `37 total / 37 passed / 0 failed`.
- `StageAuthoringPresentationCatalogValidationTests`: `29 total / 29 passed / 0 failed`.
- `BoardSurfaceRendererBoardTileCatalogTests`: `16 total / 16 passed / 0 failed`.
- `PROJECT_PATH_WIN="$(wslpath -w "$PWD")" ./run_tests.sh core`: passed. Unity core EditMode `47/0`; Unity core PlayMode `2/0`.

Known non-combined failure still present:
- `StageCatalogCiValidationEntryPointTests`: `1 total / 0 passed / 1 failed`.
- Failure source after removing the temporary combined ledger rows: existing `stage-1-1_Authoring.asset` emits `GameplayDrift.TileFeatureMissing` as an unexpected known-warning governance issue. `stage-1-1` was already dirty before this work and was not changed by this task.

Generated `Temp/StageCatalogValidation/stage-catalog-validation.md` was referenced by the failing test output, but no project-local report file was left in this headless run. A temporary dump run during investigation showed no combined-gameplay-showcase catalog errors after the authored static presentation catalog warnings were eliminated.

After the Button entries were reauthored from `Overlay` to `ReplaceBaseTile` and Button 901 was connected as a required objective condition, `PROJECT_PATH_WIN="$(wslpath -w "$PWD")" ./run_tests.sh core` was rerun and passed: Unity core EditMode `47/0`, Unity core PlayMode `2/0`.

## I. Runtime Visual Smoke Result

Automated runtime scene load smoke was attempted with:
`GameplayVfxSceneRuntimeRootPlayModeTests.CombinedGameplayShowcase_DirectPlayTick_CreatesGameplayVfxRuntimeRoot`.

Result: failed on the existing Full-category VFX runtime-root assertion:
`UIAudioScene` must attach the VFX runtime as a tick presentation extension when launched with the `combined-gameplay-showcase` stage id.

The scene did load in PlayMode before the assertion. Because UI/VFX playback system changes are explicitly out of scope, this was recorded as a remaining scene setup issue rather than fixed in this pass. Manual visual inspection of alignment, layering, event animation counters, and scene reload persistence is still required in an interactive Unity editor.

## J. Topology Transition Visual Result

Not manually verified in an interactive editor during this headless pass. The authored content includes both active bottom/default and active front board role entries plus ReplaceBaseTile and Overlay cells needed for the requested topology smoke. Manual QA must verify:
- bottom/front face alignment
- cell gap and tile thickness
- prefab pivot stability
- suppressed board tiles staying suppressed in steady and transition visuals
- TileFeature pose placement during bottom/front topology transition

## K. Remaining Art / Prefab Issues

- Manual visual QA is still required for exact pivot, thickness, and cell seam alignment.
- DirectPlay scene smoke is currently blocked by the existing VFX runtime-root test expectation, outside this task's allowed change scope.
- Runtime event animations are primitive hooks only; no audio verification was performed.
- Moon QA box capabilities were authored with the current validator-required mask rather than push-only.
- Exit goal zone was kept as a single center cell on `face=3` to match the requested QA exit placement.

## L. Next Step Judgment

Keep Terrain/Board selector expansion deferred. The next work should first run interactive visual QA on these authored assets and fix prefab alignment, pose placement, suppress behavior, and transition visibility issues found there. Audio polish should come after visual/prefab alignment unless QA finds an audio-only blocker.
