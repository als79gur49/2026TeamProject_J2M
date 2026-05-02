# Legacy Ordinary Unit Movement Deprecation Phase 3: Explicit Legacy Fallback Policy

Date: 2026-05-02

## Decision

Phase 3 chooses Option B. Player ordinary, enemy ordinary, and Charge active legacy fallback are no longer authorized by `GameplayRuntimeFeatureFlags.None`.

`GameplayRuntimeFeatureFlags.None` now means no advanced locomotion flags and no explicit legacy ordinary fallback authorization. The only supported player/enemy/Charge covered fallback baseline is `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline`, which is a test/replay/migration preset only.

This phase does not delete the player fallback branch, enemy ordinary fallback branch, Charge fallback branch, `MoveEntity`, `MovementExpander`, retained grid transactions, or active glide retained fallback.

## Runtime Contract

| Flag preset | Legacy ordinary fallback | Player/enemy/Charge replacement flags | Glide kinematic |
| --- | --- | --- | --- |
| `None` | false | false | false |
| `DefaultGameplayLocomotion` | false | true | false |
| `LegacyOrdinaryFallbackBaseline` | true | false | false |
| `AllKinematicLocomotionEnabled` | false | true | true |

`GameplaySceneHostConfiguration` does not expose `EnableLegacyOrdinaryUnitFallback`. Scene-authored hosts cannot enable this fallback accidentally; tests and migration replay helpers must pass the preset directly.

## Validation Policy

`TickPipeline.ValidateLegacyExpansionIntents` rejects covered player ordinary, enemy ordinary, and Charge active fallback when `EnableLegacyOrdinaryUnitFallback` is false. The diagnostic reason is `LegacyOrdinaryFallbackRequiresExplicitBaseline`.

Phase 4 supersedes the player portion of this policy. `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` no longer authorizes player ordinary fallback; player attempts are rejected with `PlayerLegacyFallbackRemovedFromRuntime`.

Phase 5 supersedes the enemy portion of this policy. `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` no longer authorizes enemy ordinary fallback; enemy attempts are rejected with `EnemyLegacyFallbackRemovedFromRuntime`.

Phase 6 supersedes the Charge portion of this policy. `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` no longer authorizes Charge active fallback; Charge attempts are rejected with `ChargeLegacyFallbackRemovedFromRuntime`.

Retained grid transaction paths remain allowed without the fallback preset:

- `TopologyMaterialization`
- `BoxActionMovement`
- `SpawnRespawnPlacement`
- `CleanupRemoval`
- `ScriptedRelocation`
- `LocomotionAnchorCommit`

Glide policy is unchanged. `EnableEnemyGlideKinematicLocomotion` remains explicit, and `DefaultGameplayLocomotion` still excludes it.

## Test Migration

Canonical Phase 3 tests use explicit names such as:

- `Phase3_None_NoPlayerEnemyChargeLegacyFallback`
- `Phase4_LegacyOrdinaryFallbackBaseline_PlayerFallbackRemoved`
- `Phase5_LegacyOrdinaryFallbackBaseline_EnemyFallbackRemoved`
- `Phase6_LegacyOrdinaryFallbackBaseline_ChargeFallbackRemoved`
- `Replay_Phase3_None_NoCoveredFallback`
- `Replay_Phase6_LegacyBaseline_PlayerEnemyChargeRemoved`

Older Phase 2 `FlagOffBaseline` tests remain as compatibility wrappers, but they now use `LegacyOrdinaryFallbackBaseline` internally.

## Replay And Golden Policy

Replay harness defaults remain equivalent to `GameplayRuntimeFeatureFlags.None`. That default no longer authorizes covered fallback. Intentional legacy fallback replay tests must pass `LegacyOrdinaryFallbackBaseline`.

No golden files are rewritten in Phase 3. Golden migration remains owner-approved future work.

## Phase 4 Preconditions

Branch deletion can be considered only after:

- `None`, default, and explicit replacement flag-on lanes have no covered player/enemy/Charge fallback.
- Explicit legacy baseline tests remain deterministic or are owner-approved for removal.
- Replay/golden owners approve any trace or hash migration.
- Glide default adoption remains separately decided.
