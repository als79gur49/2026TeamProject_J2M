# Immediate Push Input Semantics

This document is the canonical source of truth for Push input semantics in the current runtime.
Archive documents that still mention `pushContactTicks`, contact accumulation, or contact threshold timing are historical only.

## Summary
- Push starts only from an explicit `PushPressed` edge on the current tick.
- The default keyboard binding is `J` for Push (`K` for Flip). Movement uses the selected WASD/arrow-key scheme.
- `Move` input by itself never starts Push.
- Push contact accumulation state and contact threshold timing are removed.

## Input Semantics
- `PlayerTickCommand.PushPressed` means "fresh press on this tick".
- Holding the Push button does not retrigger `PushPressed` on later ticks.
- `GameplayInputHost` samples keyboard Push from the `Player/Push.started` edge.
- Movement and Push/Flip use the physical gameplay input route. The unused UI-held movement gateway has been removed.
- Push/Flip capture direction when the action starts, including the input-update ordering snapshot. A later direction change does not retarget the pending action; a key pressed without movement can use the player's facing direction.

## Runtime Rules
- Priority is `Push > Flip > Move`.
- No-target and blocked Push/Flip requests follow the [fake-attempt policy](./Gameplay-PushFlip-Fake-Attempt-Policy.md), including facing fallback and presentation-only failure feedback.
- `Move` into a pushable box without `PushPressed` is a no-op.
- Action legality and failure classification remain gameplay-owned; this input cleanup does not change them.
- Push windup, execute tick, and recovery are unchanged.

## Buffering And Recovery
- Move buffering remains move-only.
- A fresh Push edge and its captured direction can be held until the next eligible tick. Holding the key does not create another edge.
- Push received during recovery or other action lock is consumed and dropped.
- A new Push after recovery requires a fresh button press.

## HUD Contract
- `ActionBar` and the later H03 Push/Flip HUD readiness mapping are retired as described in [UI architecture](./UI-Architecture-Guidelines.md). Presentation direction DTOs remain in use by the gameplay presentation feed.
- UI Session/HUD queries retain their shared admission policy and committed snapshot window. `GameplayHostUiAccessContext` owns that policy's lifetime; UI does not inject movement or Push/Flip commands.
