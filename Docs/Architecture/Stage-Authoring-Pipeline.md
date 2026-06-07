# Stage Authoring Pipeline

## Runtime Contract

Stage content remains data-driven through `StageContentEntry`.
The gameplay scene/bootstrap scene still resolves a `StageId`, loads the entry,
builds `StageDefinition` with `StageRuntimeBuilder`, resolves
`StagePresentationDefinition` with `StagePresentationAssembler`, resolves
`StageAudioDefinition` with `StageAudioAssembler`, and composes the
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

`StagePresentationDefinition` remains the visual/text presentation companion. The generator
syncs entity-id based enemy/static presentation bindings while preserving display
metadata, preview/background, catalogs, and result text.
Presentation metadata preservation is a pipeline invariant, not a generate
option.

`StageAudioDefinition` remains the stage audio companion. StageAudioDefinition v1
supports only gameplay BGM through `gameplayBgm` and direct authored `BgmProfile`
metadata, but it does not execute playback. Stage result/failure BGM,
boss/objective phase BGM, preview/menu BGM, ambience, and layered music are
intentionally out of scope and not modeled. Runtime playback is requested through
the audio flow path. Gameplay host presentation SFX map grouping belongs to
`GameplayPresentationAudioConfig`; `StageAudioDefinition` and stage content audio
companions do not enter that config.

`StagePresentationBindingNormalizer` is the narrow presentation-lane owner for
binding normalization. Enemy and static entity presentation bindings are cloned
and ordered deterministically by `EntityId`. TileFeature direct presentation
bindings are cloned while preserving authored order on the direct presentation
resolve path; gameplay-aware TileFeature resolved data remains ordered by
`TileId` after gameplay tile feature materialization. The normalizer is not a
validation owner: missing, stale, duplicate, catalog, and prefab integrity issues
belong to `StageCatalogValidator`, presentation binding integrity validation,
catalog resolvers, or host runtime validation.

`EnemyAiProfileOverride` remains gameplay seed data exported by
`StageRuntimeBuilder`. Its current `Game.Feature.Gameplay.Host` namespace is a
separate namespace debt and does not make stage runtime building presentation
owned.

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
Generated spawn array order is normalized. Presentation identity is checked
through `StagePresentationDefinition` bindings, not through gameplay spawn fields.
`StageCatalogValidator` closes this path through
`StageAuthoringProjection` and `StageAuthoringDriftComparer`; previous coarse
generated-output comparison helpers are not part of the validation contract.

`StagePresentationDefinition` drift is limited to generated enemy/static
`EntityId -> PresentationId` bindings. Display metadata, preview/background
assets, BGM reference, catalogs, and result text are preserved presentation
metadata and are not binding drift.

Presentation catalog integrity is validated separately from drift. Enemy
placements and enemy bindings must reference ids in the
`EnemyPresentationCatalog`. Box and Wall placements and static bindings must
reference ids in the `StaticEntityPresentationCatalog`. Player presentation is
not authored by this tool and does not require either catalog.

Generated enemy bindings must point at Enemy spawn EntityIds. Generated static
bindings must point at Box or Wall spawn EntityIds. Missing bindings and orphan
bindings are validator-owned issues; bindings that point at the wrong spawn kind
are always errors.

Catalog entries must have non-empty, normalized-unique `PresentationId` values.
`ViewPrefab` is required for every usable catalog entry because the runtime
catalog resolvers reject null prefabs. Validation reports these problems and
does not auto-clear stale placement ids, rewrite bindings, or mutate generated
outputs.

Enemy catalog entries may also own an optional `VfxProfileAsset`. A null profile
is valid and means host default VFX fallback. Non-null profiles are catalog
integrity data: they must be Enemy-family profiles, and profile authoring errors
or warnings are reported as catalog validation issues. These profile diagnostics
are not generated binding drift.

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
changes the selection.

The selected placement inspector also shows a read-only presentation preview.
Enemy placements resolve against `EnemyPresentationCatalog`; Box and Wall
placements resolve against `StaticEntityPresentationCatalog`. The preview shows
the selected catalog asset, resolved catalog entry status, and resolved
`ViewPrefab` in disabled object fields. Ping/Select buttons are editor
navigation helpers only. They do not write prefab references onto placements and
do not edit catalog entries.

For Enemy placements, the preview also shows the resolved catalog-owned VFX
profile as read-only derived data. The placement still stores only
`PresentationId`; VFX profile selection is not written to placements or generated
`StagePresentationDefinition` binding rows.

Player presentation is not authored by the grid editor.

## Grid Editor Generated Preview And Facing

The grid editor can preview how the selected placement will project into
generated gameplay and presentation data without writing outputs. The gameplay
preview shows the `EntityId`, cell, kind, and Facing that will be written to the
generated `StageDefinition` spawn. If the placement does not yet have a persisted
mapping, the preview uses the non-mutating allocation projection and labels the
id as not persisted.

The generated presentation binding preview shows the expected
`EntityId -> PresentationId` binding in `StagePresentationDefinition` and reports
whether the current generated binding is synced, missing, drifted, or assigned to
the wrong binding kind. The preview does not create missing bindings, remove
stale bindings, or manually edit generated binding arrays.

Generated presentation drift remains limited to `EntityId -> PresentationId`.
Enemy catalog `VfxProfileAsset` changes are catalog integrity changes, not
generated output drift.

Facing remains gameplay authoring data. The grid marker includes a compact
Facing arrow next to the placement kind marker, and the selected placement can be
rotated with editor controls or the grid-window `R` / `Shift+R` hotkeys. Rotation
updates only the existing placement `Facing` field and preserves selection,
target cell, `PresentationId`, and entity-id mappings.

Composite/Facing group validation, group rotate controls, `VisualFacingOffset`,
SceneView preview tools, and unresolved issue filters are future UX work.

The authoritative validation source for future presentation UX is
`StageCatalogValidator`. Inline presentation inspectors and SceneView authoring
tools remain intentionally deferred.

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
- Do not put BGM metadata back into `StagePresentationDefinition`.
- Do not execute BGM from stage content or scene-local visual adapters.
- Do not change `TickPipeline`, `WorldState`, `StageRuntimeBuilder`, or gameplay
  semantics for authoring convenience.
- Do not compile `EnemyAiProfile` inside the stage generator.
- Do not add box gameplay rules beyond existing `BoxCapabilities`.
