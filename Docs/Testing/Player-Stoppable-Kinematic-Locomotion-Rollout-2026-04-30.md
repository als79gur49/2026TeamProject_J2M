# Player Stoppable Kinematic Locomotion Rollout

Date: 2026-04-30

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnablePlayerStoppableKinematicLocomotion`.
It is effective only when `EnablePlayerSameFaceContinuousLocomotion` is also enabled.
If `EnablePlayerFree2DLocalLocomotion` is enabled, player ordinary movement bypasses the Held/reverse/queue branch and uses `UnitContinuousLocomotionState`; this rollout remains the fallback when the free2D flag is off.
`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` includes this fallback flag for explicit default gameplay host adoption. It does not change replay harness defaults, composition-root defaults, historical tests, migration comparisons, or flag-off goldens; those keep `GameplayRuntimeFeatureFlags.None` unless they opt in directly.

## Validation Contract

- Flag off: player same-face kinematic locomotion keeps the existing automatic continuation behavior.
- Flag on: releasing actual held movement input during player voluntary same-face kinematic movement stores `MotionMode.Held`.
- Flag on: player ordinary movement remains on the kinematic lane and must not emit legacy `TickEntityMotionKind.Move`.
- `MoveEntity` anchor commits are retained as grid transactions, not ordinary Unit movement.
- Deprecation Phase 1 treats this as covered locomotion fallback isolation: flag-on/default player ordinary fallback leaks are hard regressions, but flag-off legacy fallback and retained grid transactions remain supported. The Phase 1 targeted Unity XML canaries are runtime green; actual legacy fallback deletion is not complete.
- `MotionMode.Held` preserves anchor, local offset, elapsed ticks, total ticks, commit tick, started tick, and step direction.
- Held progress does not advance until the same held direction is pressed again.
- Same-direction resume switches back to `MotionMode.Voluntary` on the resume tick; progress advances on the following tick.
- Opposite input while held is accepted as same-edge reverse. The reverse tick reinterprets progress without advancing it, so the world pose does not snap.
- Perpendicular input while held is queued in `PlayerControlState.queuedKinematicTurnDirection`, resumes the current segment forward, and tries the queued move on the next Plan tick after settled-zero.
- Queued perpendicular movement is revalidated at consume time; success and blocked/rejected attempts both clear the queue.
- Held remains non-settled, so push/flip/action preview paths continue to require settled-zero pose.
- Held/reverse state is included in replay hashes through the existing `UnitKinematics` determinism section, and queued turns are included through `PlayerControl`.

## Rollback

Set `EnablePlayerStoppableKinematicLocomotion` to false to restore automatic player kinematic continuation while keeping player kinematic locomotion enabled.
Set `EnablePlayerSameFaceContinuousLocomotion` to false to return to the legacy discrete player movement baseline.
`GameplayRuntimeFeatureFlags.None` is not a player fallback authorization after Phase 3. `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` is the current removed-diagnostic preset.
Scoped deletion preparation is now covered by `Phase4_RemovedDiagnosticBaseline_PlayerFallbackRemoved`; player legacy discrete fallback is no longer a supported runtime fallback after Phase 4. Phase 5/6 also remove enemy and Charge covered fallback authorization. Retained grid transactions, `MoveEntity`, `MovementExpander`, and glide retained fallback remain retained.
