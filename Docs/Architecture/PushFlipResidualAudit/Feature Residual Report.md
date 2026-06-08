# Feature Residual Report

## Push Branch Recheck

Push remains active from input through runtime:

- `GameplayInputHost` builds `pushPressed`.
- `PlayerLogic` and `PlayerControlStateLogic` consume Push/Flip command state.
- `PlayerControlState` resolves push targets, suppresses ordinary move into Push-capable non-item boxes, and checks Push locks.
- `MovementExpander` expands Push and Sliding Push behavior.
- Presentation and action-audio consume Push action signals.

Verdict: `KEEP_CURRENTLY_USED`.

## Flip Branch Recheck

Flip remains active:

- `GameplayInputHost` builds `flipPressed`.
- `PlayerControlState` resolves local flip cells and landing legality.
- `MovementExpander` expands Flip movement and impact behavior.
- `FlipImpactSignals` feed host tracks, VFX, block audio, and player view mapping.

Verdict: `KEEP_CURRENTLY_USED`.

## Capability Combination Usage

Authoring box placement counts:

| Combo | Count | Classification |
| --- | ---: | --- |
| Push only | 85 | `KEEP_CONTENT_USED` |
| Flip only | 2 | `SHOWCASE_ONLY_COMBO` |
| Push+Flip | 2 | `SHOWCASE_ONLY_COMBO` |
| Push+Destroy | 30 | `KEEP_CONTENT_USED` / reachability review |
| Flip+Destroy | 0 | `DELETE_CANDIDATE_CONTENT_RULE` if no tests require exact combo |
| Push+Flip+Destroy | 6 | `LOW_USAGE_CONTENT_COMBO` |
| Push+Item | 1 | `SHOWCASE_ONLY_COMBO` |
| Flip+Item | 1 | `SHOWCASE_ONLY_COMBO` |
| Push+Flip+Item | 1 | `SHOWCASE_ONLY_COMBO` |
| Push+Flip+Destroy+JumpCrushable | 708 | `KEEP_CONTENT_USED` |
| Item included | 4 | `SHOWCASE_ONLY_COMBO` |
| Destroy included | 744 | `KEEP_CONTENT_USED` |
| JumpCrushable included | 708 | `KEEP_CONTENT_USED` |

Low-usage combos are concentrated in `combined-gameplay-showcase_Authoring.asset`. They are retained by current product decision and are not part of this cleanup PR.

## Low-Usage Capability Combos

Retained by current product decision:

- `Flip` only: 2 showcase boxes.
- `Push+Flip`: 2 showcase boxes.
- `Item`, `Push+Item`, `Flip+Item`, `Push+Flip+Item`: 4 total showcase boxes.
- `Flip+Destroy`: 0 content boxes.

These tests/docs keep capability generalization intact even when content count is low or zero. Rule simplification is out of scope for this PR.

## Impact/Disposition Branch Usage

`Push+Destroy` appears in 30 boxes: 28 in `tutorial-scene`, 2 in showcase. This is not dead. Static counting does not prove how many produce first-step blocked destroy fallback during normal play, so the next step is a layout/reachability simulation or content owner review.

`Push+Flip+Destroy+JumpCrushable` appears 708 times across campaign stages. Do not simplify this branch without a content migration plan.

`FlipImpactSignals` are not dead; they are production-consumed.

## Lock/Shield Usage

`BoxInteractionLockState` is product-reachable:

- `EnemyAi_LockNearbyBoxes` exists in campaign shared content.
- Its GUID is referenced by campaign authoring stages.
- Production capability assets set both `blocksPush: 1` and `blocksFlip: 1`.
- Runtime applies and merges lock states through finalization/batch paths.

`BoxSlideShield` is no longer backed by a Stage-reachable repository profile asset in current campaign content:

- `EnemyCapability_FrontFaceShield_BoxSlideShield.asset` was removed with the unused `EnemyAi_FrontFaceShield` profile.
- Runtime `BoxSlideBlockerQuery` handles `EnemyFrontFaceSupportEffectKind.BoxSlideShield`.
- Compiler/scenario/replay tests cover it.

Verdict: keep runtime lock/shield mechanics for a separate feature contraction decision. Profile asset removal is complete; runtime, presentation, replay, and governance residue require their own follow-up.

## Presentation Consumer Usage

Keep:

- `FlipImpactPresentationSignal`: consumed by host presentation, VFX, block audio, and tests.
- `PlayerActionAttemptSignals`: consumed by action audio, host track planning, player view mapping, and input buffering.
- `BoxFlipInteractionDriver`: attached to four production static box prefabs.

Removed:

- `PlayerFlipInteractionDriver.cs` and `PlayerFlipInteractionDriver.cs.meta`: no production prefab/scene/asset GUID reference, and the current player IK path is unavailable.

Near-dead:

- `GameplayBoxCapabilityLabelViewFactory`: runtime class not found; remaining mentions are docs/stale ledger history.

## Audio Moment Usage

Planner emits lifecycle moments for Push and Flip, but production profile coverage is uneven:

- Assigned and audible: Push Windup; Flip Windup; Flip Blocked; Push/Flip fake attempt failure moments.
- Optional null: Push Contact, Push ImpactEnemy, Push Blocked.
- No entry: Push Execute, Push Recovery, Flip Execute, Flip Contact, Flip ImpactEnemy, Flip Recovery.

Classification:

- Assigned moments: `KEEP_AUDIBLE_CURRENT`
- Optional null moments: `EMPTY_OPTIONAL_ENTRY`
- Planner-emitted no-entry moments: `AUTHORING_GAP` / `NEAR_DEAD_AUDIO_MOMENT`

## Feature Residue Delete Candidates

| Candidate | Classification | Action |
| --- | --- | --- |
| `Flip+Destroy` exact combo | `KEEP_BY_PRODUCT_DECISION` | Preserve general capability coverage |
| Showcase-only Item priority combos | `KEEP_BY_PRODUCT_DECISION` | Preserve showcase content and Item priority |
| `PlayerFlipInteractionDriver.cs` / `.meta` | `REMOVED_BY_PRODUCT_DECISION` | Remove optional player hand/IK path and tests |
| `GameplayBoxCapabilityLabelViewFactory` stale mentions | `DELETE_NOW_UNUSED` | Docs cleanup |
| Audio no-entry lifecycle moments | `DEFERRED_AUDIO_POLICY` | Do not alter action-audio planner/profile entries in this PR |
