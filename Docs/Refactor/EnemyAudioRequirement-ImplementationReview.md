# Enemy Audio Requirement Implementation Review

Date: 2026-06-08 KST

This is an implementation review report, not a feature implementation PR. No code or production asset change is proposed here.

## Summary

Decision: **Approve with WARN follow-up**

The implemented `EnemyAudioRequirementPolicy` / `EnemyAudioRequirementBinding` structure matches the intended sparse governance model:

- `EnemyAudioRequirementPolicy` serializes only `requiredCues` and `optionalCues`.
- Unspecified runtime cues are effective `Disabled`.
- `EnemyAudioRequirementBinding` connects one `EnemyAudioProfile` to one policy and applies sparse overrides before policy fallback.
- Binding validation iterates the full `EnemyAudioCueCatalog.RuntimeCues` domain.
- Required missing binding fails; Optional missing binding passes; Disabled authored binding fails.
- Optional authored invalid binding fails through target profile validation.
- Runtime planner/controller no-op policy for missing owner/authoring/profile/cue is unchanged.
- Requirement policy/binding assets are enemy-lane-owned governance metadata, not playback owners.

Largest risk: **Startis PassiveContact is a content coverage change, not a runtime code change.** The runtime missing cue no-op policy remains intact, but Startis now authors `PassiveContact`; if that cue was previously missing, the same runtime signal can now play audio.

Required fixes: none found in implementation/code/assets. Documentation or PR body should explicitly distinguish runtime no-op preservation from Startis authored content coverage.

## Review Baseline

Decision: **PASS**

Initial state recorded before editing this report:

| Check | Result |
|---|---|
| `git status --short --branch` | `## worktree/ui-audio...origin/worktree/ui-audio [ahead 13]` |
| `git diff --stat` | no output |
| `git diff --check` | no output |
| Recent head | `aeec2941 docs: Gameplay/EnemyAudio - requirement governance 문서 갱신` |

Recent commits:

```text
aeec2941 docs: Gameplay/EnemyAudio - requirement governance 문서 갱신
f3031172 feat: Gameplay/EnemyAudio - sparse requirement policy binding 추가
21c9e6ac refactor: Data - DrSaturn enemy audio profile identity rename
b2245664 Merge pull request #128 from als79gur49/worktree/ui-audio
49051ba9 chore: Audio - meta whitespace normalize
323618c9 refactor: Gameplay/ActionAudio - 미사용 action audio moment 제거
cf47ab68 docs: VFX - evidence reports current vocabulary
d03ca08a chore: VFX - orphan sample asset cleanup
604a66d1 refactor: VFX - presentation vocabulary cleanup
4fe8802c test: VFX - visibility policy source diagnostics
```

PR body was not inspected. `gh` is not installed in this environment (`gh: command not found`), and no PR URL/number was provided in the prompt.

## Structure Review

| Type | Expected responsibility | Actual | Decision |
|---|---|---|---|
| `EnemyAudioCueRequirement` | `Required`, `Optional`, `Disabled` enum only | Exactly three enum values, no extra behavior | PASS |
| `EnemyAudioRequirementPolicy` | Archetype-level required/optional cue policy | ScriptableObject with `requiredCues`, `optionalCues`, validation, and implicit disabled fallback | PASS |
| `EnemyAudioRequirementBinding` | Target profile + policy + sparse overrides | ScriptableObject with `targetProfile`, `policy`, `overrides`, full RuntimeCues validation | PASS |
| `EnemyAudioCueCatalog.RuntimeCues` | Complete runtime cue domain | 10 runtime cues, excludes `None`, deterministic array order | PASS |

Relevant files:

- `Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime/EnemyAudioCueRequirement.cs`
- `Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime/EnemyAudioRequirementPolicy.cs`
- `Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime/EnemyAudioRequirementBinding.cs`
- `Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime/EnemyAudioTypes.cs`

## EnemyAudioRequirementPolicy

Decision: **PASS**

Observed behavior:

- Serialized fields are only `requiredCues` and `optionalCues`; no serialized `disabledCues` exists.
- `GetRequirement(cue)` checks required, then optional, then returns `Disabled`.
- Duplicate required cue fails.
- Duplicate optional cue fails.
- Required/optional overlap fails.
- Non-runtime cue, including `None`, fails.
- Null arrays are treated as empty by public accessors and validation helper.
- Error messages include asset name, field name, and formatted cue label.
- Lookup is linear over small serialized arrays and not order-dependent except for deterministic required-before-optional precedence.

Decision notes:

- PASS: implicit disabled semantics are implemented.
- PASS: policy validation uses `EnemyAudioCueCatalog.RuntimeCues` as the runtime domain filter.
- PASS: no full-matrix disabled row authoring is present.

## EnemyAudioRequirementBinding

Decision: **PASS**

Observed behavior:

- `targetProfile == null` fails.
- `policy == null` fails.
- `policy.ValidateOrThrow()` is called and errors are wrapped with binding context.
- `targetProfile.ValidateOrThrow()` is called and preserves profile-local binding/category/loop/attachment validation.
- Null override rows fail.
- Duplicate override cues fail.
- Non-runtime override cues fail.
- Effective requirement is `override > policy > implicit Disabled`.
- Validation iterates every `EnemyAudioCueCatalog.RuntimeCues` value.
- Required + missing/null binding fails.
- Optional + missing binding passes.
- Disabled + authored binding fails.
- Required/authored `ChargeActiveLoop` loop and attachment rules remain enforced through `EnemyAudioProfile.ValidateOrThrow()`.

Decision notes:

- PASS: binding validation catches `AudioCategory.Sfx`, loop policy, null policy, and attachment slot issues by delegating to `EnemyAudioProfile.ValidateOrThrow()` and `AudioBindingDiagnostics`.
- PASS: `EnemyAudioRequirementBindingTests.OptionalCueWithInvalidAuthoredBinding_Fails` explicitly covers Optional + invalid authored binding fail through the target profile validation path.

## RuntimeCues

Decision: **PASS**

`EnemyAudioCue` contains:

```text
None
Move
Death
Windup
Landing
Active
Recover
ProjectileImpact
ChargeActiveLoop
StationaryActive
PassiveContact
```

`EnemyAudioCueCatalog.RuntimeCues` contains exactly:

```text
Move
Death
Windup
Landing
Active
Recover
ProjectileImpact
ChargeActiveLoop
StationaryActive
PassiveContact
```

`None` is intentionally excluded. Ordering is a static array and deterministic. `EnemyAudioRequirementPolicyTests.EnemyAudioCueCatalog_RuntimeCues_PublicSurface_IsReviewed` freezes the public surface, so adding a runtime cue requires test review.

## Runtime Behavior Review

Decision: **PASS**

Reviewed runtime paths:

- `EnemyAudioRequestPlanner`
- `EnemyAudioPresentationController`
- `EnemyChargeLoopAudioPresentationController`
- `EnemyAudioAuthoring`
- `EnemyAudioProfile`
- `GameplayPresentationAudioConfig`
- Shared audio runtime scan surface

Findings:

- One-shot controller missing owner view returns without playback.
- Missing `EnemyAudioAuthoring` returns without playback.
- Missing profile cue returns without playback.
- Missing cue is not converted to warning or fail-fast.
- Loop controller missing owner/authoring/`ChargeActiveLoop` returns without playback.
- Loop controller still stops stale/inactive/session-reset/detach handles.
- `EnemyAudioRequirementPolicy` and `EnemyAudioRequirementBinding` are not consumed by the runtime planner/controller playback paths.
- Requirement assets do not become runtime playback owners.

## Ownership And Architecture

Decision: **PASS**

Prefab-local ownership remains:

```text
Enemy view prefab
  -> EnemyAudioAuthoring
  -> EnemyAudioProfile_*
```

Requirement ownership remains enemy-lane governance metadata:

```text
EnemyAudioRequirementPolicy
  -> requiredCues / optionalCues
  -> unspecified runtime cue = implicit Disabled

EnemyAudioRequirementBinding
  -> target EnemyAudioProfile
  -> policy
  -> sparse overrides
```

`GameplayPresentationAudioConfig` exposes only the six host presentation lane maps:

- `GameplayAudioMap`
- `BlockAudioMap`
- `PlayerLocomotionAudioMap`
- `TopologyAudioMap`
- `GravityFieldAudioMap`
- `TileFeatureAudioMap`

It does not expose:

- `EnemyAudioProfile`
- `EnemyAudioRequirementPolicy`
- `EnemyAudioRequirementBinding`
- action profiles
- UI cue maps
- BGM/stage audio metadata
- runtime installers

Architecture tests cover this through `GameplayPresentationAudioConfig_ExcludesOtherOwnership`, `EnemyAudioRequirementPolicy_IsEnemyLaneOwned`, and `EnemyAudioRequirementBinding_IsEnemyLaneOwned`.

## Guardrail Scan Results

Decision: **PASS**

Manual scans run:

```text
rg -n "EnemyAudioRequirementPolicy|EnemyAudioRequirementBinding|EnemyAudioCueRequirement" Assets/_Features/Gameplay/Gameplay_EnemyAudio Assets/_Features/Gameplay/Gameplay_Host Assets/_Shared/Audio
rg -n "PassiveContact|ProjectileImpact|ChargeActiveLoop|EnemyAudioCue" Assets/_Shared/Audio Assets/_Features/Flow Assets/_Features/UI
rg -n "GenericAudioDispatcher|AudioDispatcher|string.*Audio|audio.*string|AudioManager.Instance|FindObjectOfType<.*Audio|FindAnyObjectByType<.*Audio|new AudioManager|\\.PlayBgm\\(" Assets Docs ProjectSettings Packages
rg -n "GameplayPresentationAudioConfig.*EnemyAudio|EnemyAudio.*GameplayPresentationAudioConfig" Assets Docs ProjectSettings Packages
rg -n "EnemyAudioRequirementProfile|EnemyAudioRequirementPolicy|EnemyAudioRequirementBinding|EnemyAudioCueRequirement" Assets Docs ProjectSettings Packages
```

Interpretation:

- PASS: policy/binding hits are enemy audio runtime type definitions, tests, docs, and production policy/binding assets.
- PASS: no `GameplayPresentationAudioConfig` enemy profile/policy/binding exposure found.
- PASS: no generic dispatcher or string-key enemy dispatcher introduced in host runtime.
- PASS: shared audio runtime does not reference `EnemyAudioCue` or enemy cue names. The only `_Shared/Audio` cue-name hit is the data asset name `RocketFace_ChargeActiveLoop_Def.asset`.
- PASS: BGM `PlayBgm` hits are Flow_Audio/shared runtime/tests, not gameplay host SFX controllers.
- PASS: generated `Assets/InitTestScene...unity` files contain test class names for `EnemyAudioRequirement*Tests`, are not tracked by Git, and are already ignored by `.gitignore` line 88. They are not production requirement artifacts or architecture evidence.

## PR Body Review Criteria

Decision: **WARN**

PR body could not be inspected locally because `gh` is unavailable. If a PR body exists, it should explicitly include:

- policy + binding structure, not full matrix;
- Required/Optional explicit, unspecified effective Disabled;
- validation exhaustively iterates `RuntimeCues`;
- runtime missing cue no-op policy unchanged;
- Startis authored content coverage changed for `PassiveContact`;
- enemy audio remains prefab-local;
- `GameplayPresentationAudioConfig` excludes enemy audio profile/policy/binding;
- RocketFace one-shot `Active` disabled and `ChargeActiveLoop` required;
- DrSaturn `Active` required under `GravityFieldUtility`;
- Startis `PassiveContact` authored content coverage change;
- exact core test results;
- UI not run reason;
- full/broad regression not run.

## Issues

### FAIL

None.

### WARN

| Item | Decision | Detail | Action |
|---|---|---|---|
| Startis content change | WARN | Runtime no-op policy is unchanged, but Startis now authors `PassiveContact`, so previously silent content can now play. | PR/report wording must distinguish runtime behavior from authored content coverage. |
| PR body unavailable | WARN | `gh` CLI is unavailable and no PR URL/number was provided. | Review PR body separately before merge if required. |

### NEEDS_FOLLOWUP

| Item | Decision | Detail |
|---|---|---|
| Generated InitTestScene files | DEFER | `Assets/InitTestScene...unity` files contain generated test inventory text, are not tracked, and are already ignored. No EnemyAudioRequirement PR action is needed; broader local cleanup can remain a separate maintenance task. |

## Final Judgment

Decision: **Approve**

The implementation satisfies the intended strong contracts: sparse policy/binding authoring, implicit disabled semantics, exhaustive repository validation, runtime no-op preservation, prefab-local enemy audio ownership, and `GameplayPresentationAudioConfig` exclusion.

Approval is conditional only on wording discipline: describe Startis as runtime no-op policy unchanged while also stating that authored `PassiveContact` content coverage changed.
