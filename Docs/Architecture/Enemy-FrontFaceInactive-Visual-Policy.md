# Enemy FrontFaceInactive Visual Policy

This document fixes the authoring policy for campaign enemy materials that respond to the presentation-only `FrontFaceInactive` semantic.

## Runtime Boundary

`EnemyInactiveVisualController` owns the runtime material-property write through `MaterialPropertyBlock`. Gameplay state, tick simulation, enemy AI, movement, combat, autonomy, `WorldState`, semantic resolution, and presentation applier flow must not be changed to solve material visibility.

The shader contract is:

- `_InactiveBlend`
- `_DesaturateStrength`
- `_EmissionSuppression`
- `_InactiveTint`
- `_InactiveNoiseMap`
- `_InactiveNoiseReveal`
- `_InactiveNoiseStrength`
- `_InactiveNoiseScale`
- `_InactiveNoiseEdgeWidth`
- `_InactiveNoiseThreshold`

Materials must render as their source material when `_InactiveBlend == 0` or `_InactiveNoiseReveal == 0`. Desaturation, inactive tint, and emission suppression are applied only while the inactive gate is enabled and reveal progress is non-zero.

Enemy FrontFaceInactive noise uses a presentation-only reveal transition. `_InactiveBlend` is the shader inactive gate. `_InactiveNoiseReveal` is the visual progress value.

On FrontFaceInactive entry, `EnemyInactiveVisualController` enables `_InactiveBlend` and advances `_InactiveNoiseReveal` from `0` to `1`. On clear, it keeps `_InactiveBlend == 1` while `_InactiveNoiseReveal` fades from `1` to `0`, then disables `_InactiveBlend` only after reveal reaches zero.

`EnemyInactiveVisualController` owns this transition locally through `Update()` and `AdvanceInactiveNoiseReveal(deltaTime)`. Tests call `AdvanceInactiveNoiseReveal(float)` directly. Gameplay state, tick simulation, semantic resolution, and `GameplayEntityPresentationApplier` do not advance noise reveal properties.

Noise is used only as inactive reveal intensity. `_InactiveNoiseStrength == 0` must match uniform inactive reveal, `_InactiveNoiseReveal == 0` must match the source material appearance, and `_InactiveNoiseReveal == 1` must fully apply inactive presentation. The readability floor is scaled by reveal progress, so entry does not create immediate ghost tint. Production inactive-compatible materials must assign `_InactiveNoiseMap`; relying on the shader's white fallback hides missing authoring and is not accepted for campaign enemy materials. `_InactiveNoiseThreshold` remains serialized as compatibility contract but is not the fixed-threshold reveal mask.

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

BlackEye inactive noise is applied only to the inactive mask after the `_B` / `_W` / `_Border` source color calculation. It must not alter the border, pupil, or fresnel source expression when `_InactiveBlend == 0`.

BlackEye leg renderers that originally used `Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat` must use a separate `EnemyView_BlackEye_Lit_Inactive.mat` duplicate. They must not be collapsed into the BE_LS1 bridge material.

SecBot additive glow keeps additive render behavior through `Game/Enemy/AdditiveInactiveBridge`. The bridge preserves transparent/additive render state and uses `_EmissionSuppression` to reduce glow intensity only while inactive.

Additive bridge noise is conservative and applies only to glow/emission suppression intensity. It must not expand the transparent/additive policy, render queue, blend, zwrite, cull, alpha clip, discard, dissolve, shadow pass, or depth behavior.

Alpha clip, discard, dissolve, transparent queue changes, shadow pass changes, depth pass changes, GravityField shader/material policy changes, runtime shader property fallback, and `_BaseColor` runtime override fallback are outside this policy.

Default or unresolved enemy renderer slots must not keep Unity's default material GUID. If a slot needs a material, it must be assigned an inactive-compatible duplicate with an explicit source mapping or fail authoring validation.

## Validation

`EnemyInactiveMaterialAuthoringTests` validates campaign enemy prefabs, all renderer material slots, allowed shaders, inactive contract and noise reveal properties, duplicate naming/root policy, source mapping, source fidelity for copied properties, BlackEye bridge-specific properties, BlackEye package Lit leg duplication, SecBot additive state, inactive noise texture assignment/ranges, `_InactiveNoiseReveal == 0` authoring defaults, and absence of the Unity default material GUID in campaign enemy prefabs.

Manual QA remains required for art fidelity: Startis, BlackEye, SecBot, Jumping, Astreton or DrSaturn, and RocketFace or Sunwheel are the minimum visual pass. Check source appearance before inactive, gradual inactive reveal on entry, gradual reveal removal on clear, inactive readability during reveal, BlackEye border/pupil fidelity, SecBot additive glow stability, texture/normal/emission fidelity, missing/pink shaders, missing textures, transparent/shadow/depth artifacts, and no ghost tint after disable/despawn.
