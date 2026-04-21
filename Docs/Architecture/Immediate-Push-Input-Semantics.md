# Immediate Push Input Semantics

This document is the canonical source of truth for Push input semantics in the current runtime.
Archive documents that still mention `pushContactTicks`, contact accumulation, or contact threshold timing are historical only.

## Summary
- Push starts only from an explicit `PushPressed` edge on the current tick.
- The default keyboard path is `E + direction`.
- `Move` input by itself never starts Push.
- Push contact accumulation state and contact threshold timing are removed.

## Input Semantics
- `PlayerTickCommand.PushPressed` means "fresh press on this tick".
- Holding the Push button does not retrigger `PushPressed` on later ticks.
- `GameplayInputHost` samples keyboard Push from the `Player/Push.started` edge.
- UI `RequestPush(direction)` merges into the same one-shot command path.
- If UI Push and keyboard Push happen on the same tick, runtime consumes one Push only.
- If UI Push supplies a direction, it overrides the sampled keyboard move direction for that Push.

## Runtime Rules
- Priority is `Push > Flip > Move`.
- `PushPressed` with no direction is a no-op.
- `PushPressed` with a direction but no adjacent pushable target is a no-op.
- `Move` into a pushable box without `PushPressed` is a no-op.
- `PushPressed` with a direction and an adjacent push-capable box that cannot start Push emits a `MovementRejected|Stage=PreMovement|...` reason.
- Push windup, execute tick, and recovery are unchanged.

## Buffering And Recovery
- Move buffering remains move-only.
- Push is never buffered.
- Push received during recovery or other action lock is consumed and dropped.
- A new Push after recovery requires a fresh button press.

## HUD Contract
- HUD readiness now separates:
  - `CanStartAnyActionThisTick`
  - `HasExplicitPushCandidateInCurrentDirection`
- Push slot states are:
  - disabled when actions cannot start
  - ready when actions can start but no push candidate is armed
  - armed when actions can start and the current direction resolves to a valid push candidate
