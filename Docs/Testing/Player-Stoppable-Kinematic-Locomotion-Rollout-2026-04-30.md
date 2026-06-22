# Player Stoppable Kinematic Locomotion Rollout

Date: 2026-04-30

Status: historical/superseded. This document records the earlier player stoppable kinematic rollout. Current player ordinary movement is Free2D-owned and must not use this player kinematic fallback path.

## Validation Contract

- Historical flag-off/flag-on behavior in this section is preserved only as migration context.
- Current player ordinary movement remains on the Free2D continuous lane and must not emit legacy `TickEntityMotionKind.Move`.
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

This rollback section is historical. `GameplayRuntimeFeatureFlags.None` is not a player fallback authorization, and no diagnostic preset authorizes player ordinary fallback.
Scoped deletion preparation is now covered by `Phase4_RemovedDiagnosticBaseline_PlayerFallbackRemoved`; player legacy discrete fallback is no longer a supported runtime fallback after Phase 4. Phase 5/6 also remove enemy and Charge covered fallback authorization. Retained grid transactions, `MoveEntity`, `MovementExpander`, and glide retained fallback remain retained.
