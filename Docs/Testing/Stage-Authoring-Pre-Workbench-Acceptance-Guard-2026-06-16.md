# Stage Authoring Pre-Workbench Acceptance Guard 2026-06-16

This is the acceptance guard before Authoring Workbench structure planning.
It freezes the current stage authoring classification vocabulary and blocks
retired load/companion labels from returning as active contract language.

This is not a Workbench implementation, runtime change, asset schema change, or
serialized-field migration.

## Acceptance Guard Matrix

| Contract | Required Label / Behavior | Forbidden Label / Behavior | Guard Location | Test Needed |
|---|---|---|---|---|
| `StageContentEntry` | `Stage Root` | `Gameplay Companion`, retired load detector | CI report, audit report, this document | yes |
| `StageDefinition` | `Gameplay Companion` | `Stage Root`, active root | CI report, creation tests, this document | yes |
| `StagePresentationDefinition` | `Presentation Companion` | gameplay root, gameplay authority | CI report, this document | yes |
| `StageAudioDefinition` | `Audio Companion` | gameplay root, gameplay authority | CI report, this document | yes |
| Reward / Progression / ClearEvaluation | `Retired Companion Guard` | active companion, missing companion, active missing companion | CI report, creation tests, vocabulary scan | yes |
| `RetiredStageLoadPathGuard` | `Retired Load Guard`, editor governance guard | runtime load option, runtime recovery path | architecture tests, vocabulary scan | yes |
| `defaultStageId` | `Retired Load Detector` residue only | defaultStageId fallback, defaultStageId runtime fallback, active fallback | CI report, direct-play tests, vocabulary scan | yes |
| direct `stageDefinition` | `Retired Load Detector` residue only | Direct StageDefinition option, supported authoring option | CI report, vocabulary scan | yes |
| serialized `StageContentEntry` residue | `Retired Load Detector` | active load mode | CI report | yes |
| compat mode residue | `Retired Load Detector` | active load mode | CI report | yes |
| `StageEditorDirectPlayCatalog` | `Editor Direct-Play Support` | production fallback | CI report, direct-play tests | yes |
| `StageEditorDirectPlayLauncher` | `Editor Direct-Play Support` | production fallback | CI report, direct-play tests | yes |
| `StageEditorDirectPlayWindow` | `Editor Direct-Play Support` | production fallback | source wording test | yes |
| `PresentationId` | `Presentation-Only Binding` | gameplay authority | CI report, this document | yes |
| UI helper | `Weak Helper / Reference` | gameplay root | CI report, this document | yes |
| Audio helper | `Weak Helper / Reference` | gameplay root | CI report, this document | yes |
| Topology visual helper | `Weak Helper / Reference` | gameplay root | CI report, this document | yes |
| `StageAuthoringSurfaceKind` | editor-only classification enum | runtime dependency, schema field | architecture tests | yes |
| `StageAuthoringSurfaceClassificationLabels` | editor-only display label formatter | runtime dependency, schema field | architecture tests | yes |

## Forbidden Vocabulary Policy

The following vocabulary must not return as active production/editor/docs
contract language. It may appear only in negative assertions, this forbidden
vocabulary policy, or clearly historical retired notes:

- StageDefinition root
- Create Stage Content Entry From Selected StageDefinition
- Stage Compat Audit
- CanonicalGameplayAssetCount
- GameplayAssets as root
- StageGameplayAssetInventoryItem
- Scene Mode Summary
- StageLoadSourceMode
- CatalogResolvedStageId
- defaultStageId fallback
- defaultStageId runtime fallback
- Direct StageDefinition option
- production fallback
- runtime recovery path
- RewardAuthoring
- ProgressionAuthoring
- ClearEvaluationAuthoring
- active missing companion

## Do-Not-Touch Confirmation

Maintain these existing contracts while adding guards only:

- `StageRuntimeContentResolver` launch-context-only path
- `StageLoadRequest.CreateLaunchContextOnly`
- `StageRuntimeBuilder`
- `StageCatalog -> StageContentEntry -> companion definitions`
- `RetiredStageLoadPathGuard`
- `defaultStageId` detector
- direct `stageDefinition` detector
- serialized `StageContentEntry` residue detector
- compat mode residue detector
- `StageSceneBootstrapValidator` issue codes
- `StageCatalogCiValidationEntryPoint` guard report
- `StageContentInventoryAndAudit` residue scan
- `StageEditorDirectPlayCatalog`
- `StageEditorDirectPlayLauncher`
- `StageResult`
- `MinimalStageCompletionReadModel`
- `StageSessionTracker`
- `StageNavigationRequest`
- `PresentationId`
- `StagePresentationDefinition`
- `StageAudioDefinition`
- `TileFeature`
- `SurfaceCell`

## Workbench Handoff Gate

Ready for Workbench planning only when all of the following are true:

- root/companion/guard/helper classification is fixed
- old labels are blocked
- no runtime behavior changed
- tests pass
- `StageContentEntry` remains root
- `StageDefinition` remains gameplay companion
- retired companion/load paths remain guard-only
