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
  modifying source mappings or output assets.
- `Generate Gameplay + Presentation` writes grouped spawn arrays to
  `StageDefinition` and entity bindings to `StagePresentationDefinition`.
- `Open Grid Editor` opens the face-aware grid MVP for placement edits.

Validation checks stable GUIDs, positive unique mappings, board bounds,
face-aware cells, player count, HP, duplicate occupancy using the existing
`StageDefinitionValidator` stacking policy, zones/objectives, runtime builder
smoke, presentation binding ids, and presentation binding drift.

Generated sync validation uses normalized semantic snapshots, not raw Unity
serialized object equality. `StageDefinition` drift is checked against gameplay
runtime fields consumed by `StageDefinitionValidator` and `StageRuntimeBuilder`:
board, initial bottom face, entity spawns, zones, and objective entries.
Generated spawn array order is normalized and legacy
`StageSpawnDefinition.PresentationId` is not gameplay drift.
`StageCatalogValidator` closes this path through
`StageAuthoringProjection` and `StageAuthoringDriftComparer`; previous coarse
generated-output comparison helpers are not part of the validation contract.

`StagePresentationDefinition` drift is limited to generated enemy/static
`EntityId -> PresentationId` bindings. Display metadata, preview/background
assets, BGM reference, catalogs, and result text are preserved presentation
metadata and are not binding drift.

The generator is split into a non-mutating plan build and an apply step. Validate
and Dry Run build only the allocation/output plan. Write Generate is the only
path that persists `StableGuid -> EntityId` mappings, retires deleted mappings,
writes generated gameplay data, writes presentation bindings, marks assets dirty,
or saves assets.

## Grid Editor Presentation Selection

The grid editor presentation dropdown only selects the placement
`PresentationId`. Catalog missing and empty warnings use the raw catalog entry
ids, not the popup option list. The popup's `(None)` item is a UI-only empty
selection and does not count as a catalog entry.

If a selected `PresentationId` is no longer present in the relevant catalog, the
grid editor shows a warning and preserves the existing value until the user
changes the selection. ViewPrefab preview is a future UX enhancement, not part
of the MVP dropdown contract.

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

## Assembly Boundary

The runtime assembly owns runtime-safe authoring data models and pure normalized
projection/comparison code. Editor-only generation, migration, grid UI,
`AssetDatabase`, `Undo`, `EditorUtility`, `EditorWindow`, `MenuItem`, and
`Selection` stay in the Editor assembly.

`StageCatalogValidator` may call runtime-safe normalized drift comparison, but it
does not call `StageAuthoringGenerator`. Editor CI supplies asset metadata through
a validation metadata provider instead of requiring runtime code to reference
editor APIs.

Asset metadata provider resolution is explicit and deterministic:
`StageCatalogValidationOptions.AssetMetadataProvider` overrides the validator
default provider, the default provider is used when no explicit provider is
supplied, and a no-op provider returns empty path/GUID metadata when neither is
configured. Editor entrypoints register or pass editor providers; runtime code
does not auto-register `AssetDatabase`-backed providers.

`StageRuntimeBuilder` and `StageRuntimeContentResolver` do not consume
`StageContentEntry.AuthoringDefinition` as runtime input. Existing stages without
an authoring asset remain valid.

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
