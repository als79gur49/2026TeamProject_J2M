# Flip B-1 Actual Hit Timing Investigation

## Final Scope
- Applies only to hostile impact Flip.
- Ordinary Flip success remains unchanged.
- Execute tick creates the in-flight scheduled-contact state only.
- No target reservation is introduced.
- No enemy suppression is introduced.
- No `ImpactReservation` or `AttackInputNormalizer` synthetic hit is used for hostile B-1.
- `MotionTrack`, sampler behavior, action duration, and due resolver policy remain unchanged.

## Final Timing Contract
- Correct formula: `DueTick = ActionStartTick + RoundToInt(FlipInputLockDurationTicks * 0.936)`.
- Incorrect formula: `DueTick = ExecuteTick + 12`.
- Execute tick schedules only: source box becomes `BoxInFlight` and `ScheduledFlipContact` is created.
- Old `0.614` action-normalized timing is a no-hit point.
- Visual impact due tick resolves the scheduled contact.
- Due tick is action-start based, not execute-based.

The incorrect formula caused the old timing because `ExecuteTick` was already `ActionStartTick + 23`. Applying `ExecuteTick + 12` produced `ActionStartTick + 35`, and `35 / 57 = 0.614`. The final due timing uses `53 / 57 = 0.930`, matching the configured target normalized visual impact `0.936` after tick rounding.

## Trace Evidence
Actual input end-to-end trace:

- `ActionStartTick = 1`
- `ExecuteTick = 24`
- `ScheduledContactCreatedTick = 24`
- old broken timing tick = `36`
- `DueTick = 54`
- `ActionDurationTicks = 57`
- `ActionNormAtExecute = 23 / 57 = 0.404`
- `ActionNormAtOldBrokenTiming = 35 / 57 = 0.614`
- `ActionNormAtDue = 53 / 57 = 0.930`
- configured target normalized visual impact = `0.936`

| Event | Tick | ActionNorm | Result |
|---|---:|---:|---|
| action start | 1 | 0.000 | input branch |
| execute / schedule | 24 | 0.404 | BoxInFlight + ScheduledFlipContact |
| old broken timing | 36 | 0.614 | no hit |
| visual impact due | 54 | 0.930 | B1ScheduledContactDue hit |

| Timing | DamagePath | Result |
|---|---|---|
| old 0.614 point | none | no damage |
| due 0.930 point | B1ScheduledContactDue | damage/death/disposition |

Old broken timing at tick `36`:

- HP unchanged
- `markedForDeath = false`
- target not removed
- source box still `InFlight`
- `ScheduledFlipContact` still pending
- no `FlipDueContactSignals`
- no `EnemyDamageSignals`
- no enemy exit signal

Due tick at tick `54`:

- due resolver runs
- current `WorldSnapshot` requery
- hostile survives case: HP `3 -> 2`, source box `DestroySelf`
- hostile dies case: target removed, source box materializes at landing if settlement allowed
- scheduled contact removed
- `DueContactImmediate` emitted
- `DamagePath = B1ScheduledContactDue`

## Test Gap Note
- Seeded `ScheduledFlipContact` tests are resolver coverage only.
- Actual input tests are required to verify action-start based timing.
- Previous seeded tests missed the `ExecuteTick + 12` mistake because they bypassed `MovementExpander` schedule creation and therefore did not validate the actual input branch timing.

## Deferred Work
- Manual visual smoke.
- Full lane baseline red recovery.
- Ordinary Flip pure B-1.
- Legacy `AtContactTime` cleanup.
