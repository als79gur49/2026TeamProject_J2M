# Legacy Ordinary Unit Movement Deprecation Phase 3: Explicit Legacy Fallback Policy

Date: 2026-05-02

## Decision

Phase 3 chooses Option B. Player ordinary, enemy ordinary, and Charge active legacy fallback are no longer authorized by `GameplayRuntimeFeatureFlags.None`.

`GameplayRuntimeFeatureFlags.None` now means no advanced locomotion flags and no explicit legacy ordinary fallback authorization. Phase 3 introduced `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` as a test/replay/migration preset, but Phase 4/5/6 supersede that authorization. After Phase 8B/8C, `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` is the canonical removed-diagnostic preset; old diagnostic baseline alias vocabulary is historical-only and is not accepted by runtime code.

This phase does not delete the player fallback branch, enemy ordinary fallback branch, Charge fallback branch, `MoveEntity`, `MovementExpander`, retained grid transactions, or active glide retained fallback.

## Runtime Contract

| Flag preset | Legacy ordinary fallback | Player/enemy/Charge replacement flags | Glide kinematic |
| --- | --- | --- | --- |
| `None` | false | false | false |
| `DefaultGameplayLocomotion` | false | true | false |
| `RemovedLegacyFallbackDiagnosticBaseline` | true | false | false |
| `RemovedLegacyFallbackDiagnosticBaseline` | true | false | false |
| `AllKinematicLocomotionEnabled` | false | true | true |

`GameplaySceneHostConfiguration` does not expose `RemovedLegacyFallbackDiagnosticsEnabled`. Scene-authored hosts cannot enable this fallback accidentally; tests and migration replay helpers must pass the preset directly.

## Validation Policy

`TickPipeline.ValidateLegacyExpansionIntents` rejects covered player ordinary, enemy ordinary, and Charge active fallback when `RemovedLegacyFallbackDiagnosticsEnabled` is false. The diagnostic reason is `LegacyOrdinaryFallbackRequiresExplicitBaseline`.

Phase 4 supersedes the player portion of this policy. `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` no longer authorizes player ordinary fallback; player attempts are rejected with `PlayerLegacyFallbackRemovedFromRuntime`.

Phase 5 supersedes the enemy portion of this policy. `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` no longer authorizes enemy ordinary fallback; enemy attempts are rejected with `EnemyLegacyFallbackRemovedFromRuntime`.

Phase 6 supersedes the Charge portion of this policy. `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` no longer authorizes Charge active fallback; Charge attempts are rejected with `ChargeLegacyFallbackRemovedFromRuntime`.

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
- `Phase4_RemovedDiagnosticBaseline_PlayerFallbackRemoved`
- `Phase5_RemovedDiagnosticBaseline_EnemyFallbackRemoved`
- `Phase6_RemovedDiagnosticBaseline_ChargeFallbackRemoved`
- `Replay_Phase3_None_NoCoveredFallback`
- `Replay_Phase6_LegacyBaseline_PlayerEnemyChargeRemoved`

Older Phase 2 `FlagOffBaseline` tests are historical/pre-Phase6 compatibility wrappers, and current policy delegates them to removed-diagnostic tests instead of treating them as authorization canaries.

## Replay And Golden Policy

Replay harness defaults remain equivalent to `GameplayRuntimeFeatureFlags.None`. That default no longer authorizes covered fallback. Removed-diagnostic compatibility replay tests should pass `RemovedLegacyFallbackDiagnosticBaseline`. They must assert deterministic removed diagnostics, not fallback output.

No golden files are rewritten in Phase 3. Golden migration remains owner-approved future work.

## Phase 4 Preconditions

Branch deletion can be considered only after:

- `None`, default, and explicit replacement flag-on lanes have no covered player/enemy/Charge fallback.
- Explicit legacy baseline tests remain deterministic or are owner-approved for removal.
- Replay/golden owners approve any trace or hash migration.
- Glide default adoption was later completed; flag-off glide fallback remains separately retained.
