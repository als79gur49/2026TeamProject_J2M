# Enemy FrontFaceInactive Visual Policy

This document fixes the authoring policy for campaign enemy materials that respond to the presentation-only `FrontFaceInactive` semantic.

## Runtime Boundary

`EnemyInactiveVisualController` owns the runtime material-property write through `MaterialPropertyBlock`. Gameplay state, tick simulation, enemy AI, movement, combat, autonomy, `WorldState`, semantic resolution, and presentation applier flow must not be changed to solve material visibility.

The shader contract is:

- `_InactiveBlend`
- `_DesaturateStrength`
- `_EmissionSuppression`
- `_InactiveTint`

Materials must render as their source material when `_InactiveBlend == 0`. Desaturation, inactive tint, and emission suppression are applied only when `_InactiveBlend > 0`.

## Campaign Duplicate Root

Campaign enemy duplicates live under:

`Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Materials/InactiveCompatible/`

Duplicates are grouped by enemy under `EnemyView_<PrefabName>/` folders.

Duplicate names use:

`EnemyView_<PrefabName>_<SourceMaterialName>_Inactive.mat`

Each duplicate records its explicit source material through `AssetImporter.userData`:

`InactiveCompatibleSource=<source material path>`

Original artpack materials are not edited directly.

## Shader Strategy

Opaque URP Lit materials are duplicated into `Game/Enemy/CustomEnemyLit` and copy source material values instead of assigning visual defaults. Copied state includes base color, base/main texture and transform, normal map and scale, emission map/color, metallic/smoothness, render queue, blend/zwrite/cull state, instancing, GI flags, and shader keywords.

Already compatible `Game/Enemy/CustomEnemyLit` materials may remain unchanged when they satisfy the full inactive contract.

BlackEye `BE_LS_M1` keeps its ShaderGraph behavior through `Game/Enemy/BlackEyeInactiveBridge`. The bridge preserves the BE_LS1 color calculation using `_B`, `_W`, and `_Border`, then applies the inactive contract after the source color is calculated.

BlackEye leg renderers that originally used `Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat` must use a separate `EnemyView_BlackEye_Lit_Inactive.mat` duplicate. They must not be collapsed into the BE_LS1 bridge material.

SecBot additive glow keeps additive render behavior through `Game/Enemy/AdditiveInactiveBridge`. The bridge preserves transparent/additive render state and uses `_EmissionSuppression` to reduce glow intensity only while inactive.

Default or unresolved enemy renderer slots must not keep Unity's default material GUID. If a slot needs a material, it must be assigned an inactive-compatible duplicate with an explicit source mapping or fail authoring validation.

## Validation

`EnemyInactiveMaterialAuthoringTests` validates campaign enemy prefabs, all renderer material slots, allowed shaders, inactive contract properties, duplicate naming/root policy, source mapping, source fidelity for copied properties, BlackEye bridge-specific properties, BlackEye package Lit leg duplication, SecBot additive state, and absence of the Unity default material GUID in campaign enemy prefabs.

Manual QA remains required for art fidelity: Startis, BlackEye, SecBot, and Jumping are the minimum visual pass. Check normal state fidelity, FrontFaceInactive tint/desaturation, emission suppression, inactive recovery, missing/pink shaders, missing textures, partial-renderer omissions, and additive/BlackEye special behavior.
