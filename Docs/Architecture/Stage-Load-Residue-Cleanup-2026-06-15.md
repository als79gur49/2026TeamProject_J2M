# Stage Load Residue Cleanup 2026-06-15

This note classifies remaining stage bootstrap residue before any deletion work.
It keeps the launch-context-only runtime contract explicit and separates editor
direct-play support from retired production fallback paths.

## Current Contract

- `StageContentEntry` is the canonical stage content root.
- `StageDefinition` is the active gameplay companion, not a stage root.
- Runtime loading is launch-context/catalog-resolved only:
  `StageLaunchContextStore -> StageLoadRequest.CreateLaunchContextOnly -> StageRuntimeContentResolver -> StageCatalogResolver -> StageContentEntry -> StageRuntimeBuilder -> StageSceneComposition -> GameplaySceneHost`.
- `defaultStageId` and direct scene-local `StageDefinition` loading are retired production paths.
- `StageEditorDirectPlayCatalog` and `StageEditorDirectPlayLauncher` remain active editor support. They prime launch context and are not runtime fallback mechanisms.

## Classification Matrix

| Candidate | Classification | Current evidence | Action |
| --- | --- | --- | --- |
| `StageLoadSourceMode` | Legacy compatibility shell | Runtime enum has one value, while current consumers mostly inspect old serialized field names for residue/reporting | Keep until governance removal gate passes |
| `CatalogResolvedStageId` | Report wording residue | CI report used the old enum value as a scene mode label | Prefer `LaunchContextCatalogResolvedInstallers` wording |
| `defaultStageId` | Retired-path detector | Runtime fallback is absent; validators scan scene text for serialized residue | Keep as detector |
| `DefaultStageId` | Test/doc guard vocabulary | Appears in direct-play policy tests/docs, not as active fallback | Keep guard tests; avoid active fallback wording |
| direct `stageDefinition` scene field | Removed direct-load guard | Validator/audit detect serialized installer direct references | Keep detector |
| `DirectStageDefinition` / `FallbackStage` | Search patterns only | No active code or YAML references found in the current audit | Keep in audit checklist |
| `RetiredStageLoadPathGuard` | Editor governance guard | Centralizes retired load path residue detection for validator, audit, and CI vocabulary | Keep |
| `StageEditorDirectPlayCatalog` | Editor direct-play mapping support | Asset declares canonical gameplay shell and supported stage ids | Keep |
| `StageRuntimeContentResolver` launch-context path | Active runtime path | Throws when launch context is missing and resolves only through catalog entries | Keep |
| `StageSceneBootstrapValidator` | Validator/audit detector | Reports `RetiredStageLoadPathGuard` findings as production bootstrap issues | Keep |
| `RetiredStageLoadPathGuardArchitectureTests` | Removed-behavior guard | Prevents old load strategies/default fallback tokens in runtime and blocks active-mode report vocabulary | Keep |
| `StageDefaultStageIdPolicyTests` | Editor support + removed-fallback guard | Covers pending editor direct-play and missing launch context failure | Keep |

## Serialized Reference Audit

Current targeted searches found no tracked YAML field references for
`stageLoadSourceMode:`, `defaultStageId:`, `DefaultStageId:`,
`DirectStageDefinition`, or `FallbackStage`.

`StageDefinition` hits must be split by field:

- `gameplayDefinition:` on `StageContentEntry` assets is active companion wiring.
- `stageDefinition:` on scene installers is direct-load residue and remains a
  validator/audit concern.

Untracked `Assets/InitTestScene*.unity` files can contain test method names and
must not be treated as tracked serialized production residue.

## Deletion Gates

`StageLoadSourceMode` can be deleted only when all are true:

- Runtime consumer is absent.
- Editor direct-play consumer is absent.
- Validator/audit consumer has moved to `RetiredStageLoadPathGuard`.
- Guard test consumer is absent or has a replacement guard.
- Tracked YAML asset/prefab/scene/meta references are absent.
- Docs do not describe it as an active contract.
- Public API and asmdef compile risk is checked.
- Stage bootstrap/catalog validation tests remain intact.

`defaultStageId` detector can be deleted only when all are true:

- Active fallback path is absent.
- Another architecture test blocks old field reintroduction.
- Scene bootstrap validation is not weakened.
- Tracked YAML residue is absent.
- Docs/report wording is cleaned.
- It is not coupled to editor direct-play mapping support.

Direct `StageDefinition` load detector can be deleted only when all are true:

- Production scenes have no direct references.
- Scene bootstrap contract remains covered after detector removal.
- Editor direct-play support is unaffected.
- Tests still guarantee direct-load absence.
- Docs do not describe direct load as a supported option.

If any gate item is uncertain, keep the related guard/detector.

## PR Order

1. Guard extraction: keep `StageLoadSourceMode`, route validator/audit/CI
   residue detection through `RetiredStageLoadPathGuard`, and separate active
   `gameplayDefinition` refs from direct `stageDefinition` residue.
2. Wording cleanup: use `launch-context/catalog-resolved runtime path`,
   `editor direct-play mapping support`, `retired-path guard`, and `removed
   direct-load guard`.
3. Optional code cleanup: consider deleting shells only after every gate passes
   and replacement guards are in place.
4. Authoring Tool preparation: show retired load residue as guard/detector
   status, not as active authoring or runtime options.
