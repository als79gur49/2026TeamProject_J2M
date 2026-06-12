# Enemy AI Naming Guidelines

This document is the architecture policy for future Enemy AI profile, core,
brain, capability, view, animator, presentation, test, and documentation rename
work. It is not refactor history.

Enemy AI authoritative behavior follows:

`EnemyAiProfile -> EnemyAiProfileCompiler -> EnemyAiRuntimeDefinition -> GameplayEntityLogicProviderFactory -> TickPipeline`

Prefab, animator, and view assets are presentation surfaces. They are not
authoritative logic owners.

## Ownership-Based Naming Principle

Enemy naming is ownership-based, not string-unified.
Profile/Core/Brain names describe gameplay behavior or archetype.
Capability names describe reusable gameplay capability semantics.
View prefab names describe visual archetypes.
Animator names describe visual archetype plus optional motion set.
Presentation IDs describe presentation identity.
Retired names are serialized compatibility guards and must not be renamed as routine cleanup.

Do not force profile behavior, reusable capability semantics, visual identity,
animator motion set, and presentation binding identity into one shared string.

## Naming Table

| Surface | Rule | Example | Notes |
| --- | --- | --- | --- |
| Enemy profile | EnemyAi_<BehaviorOrArchetype> | EnemyAi_GravityFieldChaser | Gameplay behavior/archetype. Not visual identity. |
| Core authoring | EnemyCore_<BehaviorOrTimingBundle> | EnemyCore_GravityFieldChaser | Core/common/timing bundle. Shared core may use shared semantic name. |
| Brain authoring | EnemyBrain_<BehaviorOrDecisionPattern> | EnemyBrain_GravityFieldChaser | Resolver/patrol/detection/chase decision bundle. |
| Capability asset | EnemyCapability_<CapabilitySemantic> | EnemyCapability_GravityFieldAura | Reusable capability semantic. Not profile behavior. |
| View prefab | EnemyView_<VisualArchetype> | EnemyView_DrSaturn | Visual identity. Not authoritative gameplay behavior. |
| Animator controller | EnemyAnimator_<VisualArchetype>[_<MotionSet>] | EnemyAnimator_DrSaturn_GravityField | Visual-first; add motion set only when necessary. |
| Presentation ID | visual identity key | dr_saturn | Catalog binding key. Not profile behavior. |
| Runtime type | Enemy<Semantic>Runtime / Enemy<Semantic>Authoring | EnemyGravityFieldAuraRuntime | Actual domain responsibility. |
| Test helper | target under test | CreateGravityFieldChaserProfile | Use behavior/capability/view target explicitly. |
| Retired slot | keep retired name | RetiredLockNearbyBoxes | Serialized compatibility guard. Do not routine rename. |

## GravityFieldChaser Example

Correct separation:

- `EnemyAi_GravityFieldChaser`
- `EnemyCore_GravityFieldChaser`
- `EnemyBrain_GravityFieldChaser`
- `EnemyCapability_GravityFieldAura`
- `EnemyView_DrSaturn`
- `PresentationId: dr_saturn`

`GravityFieldChaser` is the gameplay behavior/archetype.
`GravityFieldAura` is the capability semantic.
`DrSaturn` is the visual archetype.
`dr_saturn` is the presentation binding identity.
These names must not be forced into one string.

Current residue:

- `EnemyView_GravityFieldChaser` is a renamed non-catalog residue prefab and is
  not the canonical production view if `StagePresentationDefinition` maps
  `dr_saturn` to `EnemyView_DrSaturn`.
- `EnemyAnimator_GravityFieldChaserDrS` is acceptable as an intermediate residue
  name, but long-term naming should prefer visual-first form such as
  `EnemyAnimator_DrSaturn_GravityField` if that controller is DrSaturn-specific.
- `Capabilities/Enemy_LockNearbyBoxes` is a folder taxonomy residue and requires
  separate decision.

## Folder Rules

Profile/Core/Brain folders:

- Use behavior/archetype naming.
- Example:
  - `Profiles/Enemy_GravityFieldChaser/`
  - `Core/Enemy_GravityFieldChaser/`
  - `Brain/Enemy_GravityFieldChaser/`

Capability folders:

- Prefer capability semantic when asset is reusable:
  - `Capabilities/GravityFieldAura/`
  - `Capabilities/PassiveContact/`
  - `Capabilities/WindupForwardCellProjectile/`
- Allow archetype-scoped folders only when the folder is truly a profile-local
  support bundle:
  - `Capabilities/Enemy_GravityFieldChaser/`
- Avoid stale folders such as `Capabilities/Enemy_LockNearbyBoxes` when active
  capability semantic is `GravityFieldAura`.

Presentation folders:

- Prefer visual identity or presentation family.
- Do not use behavior names for production visual prefabs unless the prefab is
  explicitly prototype/test-only.

## Serialized Field Rule

Serialized field names are schema, not cosmetic naming.
Do not rename serialized fields without FormerlySerializedAs or an explicit migration plan.

High-risk examples:

- `coreAuthoring`
- `brainAuthoring`
- `capabilityAssets`
- `kind`
- `gravityFieldAura`
- `presentationBindings`
- `viewPrefab`
- `animator/controller references`

## Retired Compatibility Rule

Retired names are not stale naming cleanup targets.
They are serialized numeric compatibility guards.

Preserve examples:

- `RetiredLockNearbyBoxes = 1`
- `RetiredMelee = 0`
- `RetiredPhaseThroughLockedTarget = 2`
- `RetiredFrontFaceSupport = 4`

Do not rename/delete these unless a dedicated migration plan exists.

## Stage And Presentation Boundary

Stage gameplay and presentation naming are separate:

- `StageDefinition` / `EnemySpawns` reference `EnemyAiProfile` for gameplay
  behavior.
- `StagePresentationDefinition` / `EnemyPresentationCatalog` reference visual
  prefab bindings.
- `StageSpawnDefinition.PresentationId` is legacy and must not be revived as the
  naming owner.

Stage reachability may prove that a visual prefab is production-reachable through
`StagePresentationDefinition -> EnemyPresentationCatalog -> ViewPrefab`. That
does not make the visual prefab name authoritative for gameplay behavior.

Example:

- Gameplay spawn uses `EnemyAi_GravityFieldChaser`.
- Stage presentation binding uses `dr_saturn`.
- `EnemyPresentationCatalog` maps `dr_saturn` to `EnemyView_DrSaturn`.

## Do / Do Not

Do:

- Use behavior names for profile/core/brain.
- Use capability semantic names for capability assets.
- Use visual names for view prefabs.
- Use visual + motion-set names for animator controllers.
- Preserve GUIDs when renaming Unity assets.
- Use git mv for Unity asset/file/folder rename.
- Record selected test counts after rename PRs.

Do not:

- Force all names to match the profile behavior.
- Name production view prefabs after AI behavior when a visual archetype exists.
- Rename retired enum compatibility slots.
- Rename serialized fields without migration.
- Restore default enemy fallback.
- Restore active `LockNearbyBoxes kind: 1`.
- Restore `PhaseThroughLockedTarget` / `FrontFaceShield` / `BoxSlideShield` /
  `FrontFaceSupport`.
- Claim project-wide green unless unfiltered full passes.

## Current Follow-Up Decisions

- Decide whether `EnemyView_GravityFieldChaser` is delete candidate, prototype,
  or needs a real visual archetype name.
- Decide whether `EnemyAnimator_GravityFieldChaserDrS` should become
  `EnemyAnimator_DrSaturn_GravityField`.
- Decide capability folder taxonomy for `Capabilities/Enemy_LockNearbyBoxes`.
- Decide `WindupMelee -> WindupProjectile` support taxonomy.
- Decide `ContactDamage -> PassiveContact / ContactSameCell` editor/code naming
  taxonomy.
