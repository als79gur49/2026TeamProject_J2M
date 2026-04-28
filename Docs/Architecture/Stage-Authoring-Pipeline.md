# Stage Authoring Pipeline

## Runtime Contract

Stage content remains data-driven through `StageContentEntry`.
The gameplay scene/bootstrap scene still resolves a `StageId`, loads the entry,
builds `StageDefinition` with `StageRuntimeBuilder`, resolves
`StagePresentationDefinition` with `StagePresentationAssembler`, and composes the
same runtime scene.

This pipeline does not create one Unity scene per stage. Production scene
GameObjects are not authoritative stage layout data.

## Authoring Source And Outputs

`StageAuthoringDefinition` is the editor-facing source asset. It stores the board,
face-aware `SurfaceCell` placements, zones, objective data, presentation ids, and
stable authoring identity.

`StageDefinition` remains the gameplay output. It is still the runtime build seed
and does not receive prefab references, view bindings, UI text ownership, or audio
playback ownership.

`StagePresentationDefinition` remains the presentation companion. The generator
syncs entity-id based enemy/static presentation bindings while preserving display
metadata, preview/background, BGM reference, catalogs, and result text.

## Entity Identity

Placements own stable authoring GUIDs. The generator maintains
`StableGuid -> positive EntityId` mappings on the authoring asset.

Reordering placements does not change EntityIds. Removed placements leave retired
mappings by default, so new placements allocate after the max used positive id
instead of reusing deleted ids. EntityIds must never be derived from placement
array indices.

## Generator And Validation

Use the `StageAuthoringDefinition` inspector buttons:

- `Validate` or `Dry Run Generate` checks source data and generated output without
  modifying the output assets.
- `Generate Gameplay + Presentation` writes grouped spawn arrays to
  `StageDefinition` and entity bindings to `StagePresentationDefinition`.
- `Open Grid Editor` opens the face-aware grid MVP for placement edits.

Validation checks stable GUIDs, positive unique mappings, board bounds,
face-aware cells, player count, HP, duplicate occupancy using the existing
`StageDefinitionValidator` stacking policy, zones/objectives, runtime builder
smoke, presentation binding ids, and presentation binding drift.

## Migration

Use `Tools/Stages/Authoring/Migrate Selected Stage Content Entry` or the
migration window to create a co-located `<stageId>_Authoring.asset`.

Migration copies existing `StageDefinition` board, spawns, zones, objective, and
existing EntityIds. Initial stable GUIDs use `{stageId}:{kind}:{entityId}`.
Presentation bindings are reverse-mapped by EntityId into placement
`PresentationId` fields. Migration does not destroy or replace the generated
outputs; running Generate after migration should reproduce the same gameplay
semantics.

## Governance

Stages without an authoring asset remain valid. CI reports them as informational
authoring-missing rows so migration can proceed incrementally.

When an authoring asset is assigned, validators check owner metadata, generated
output references, duplicate stable GUIDs, duplicate EntityId mappings, invalid
surface cells, gameplay output drift, and presentation binding drift.

If `EnforceGeneratedSync` is false, generated drift is a warning. If it is true,
generated drift is a CI/build-blocking error.

## Forbidden Patterns

- Do not add prefab references or visual binding fields to `StageDefinition`.
- Do not flatten `SurfaceCell` to `Vector2Int`.
- Do not generate EntityIds from placement array order.
- Do not create one gameplay scene per stage.
- Do not read production scene GameObjects as authoritative placement data.
- Do not change `TickPipeline`, `WorldState`, `StageRuntimeBuilder`, or gameplay
  semantics for authoring convenience.
- Do not compile `EnemyAiProfile` inside the stage generator.
- Do not add box gameplay rules beyond existing `BoxCapabilities`.
