# Lane A Handoff Ledger 2026-04-22

## Scope
- current same-revision handoff proposals created from the live Lane A row import

## Source Artifact
- revision: `534e861`
- source artifact: `TestResults/wsl-unity-full-editmode.xml (2026-04-22 20:43:27 KST)`
- created at: `2026-04-22 20:43:49 KST`

## Summary
- handoff rows: `7`
- target counts:
- `Lane E`: `4`
- `Lane D`: `3`

## Rows
| source lane | target lane | row id/test name | reason | blocking claim | required evidence | created at | accepted by | status | source artifact | historical comparison artifact |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Lane D | Lane D | Game.Feature.Gameplay.Tests.Unit.AudioArchitectureTests.GameplayAudioMap_ValidateOrThrow_FailsOnDuplicateTypedSemantic | Audio/BGM rows are deferred to the dedicated ADR-gated lane. | cross-lane blocking handoff queue | same-revision row plus failure capture showing audio/BGM ownership or registry scope | 2026-04-22 20:43:49 KST | pending | pending-acceptance | TestResults/wsl-unity-full-editmode.xml (2026-04-22 20:43:27 KST) |  |
| Lane D | Lane D | Game.Feature.Gameplay.Tests.Unit.AudioArchitectureTests.GameplayAudioMap_ValidateOrThrow_FailsOnEmptyTypedSemantic | Audio/BGM rows are deferred to the dedicated ADR-gated lane. | cross-lane blocking handoff queue | same-revision row plus failure capture showing audio/BGM ownership or registry scope | 2026-04-22 20:43:49 KST | pending | pending-acceptance | TestResults/wsl-unity-full-editmode.xml (2026-04-22 20:43:27 KST) |  |
| Lane D | Lane D | Game.Feature.Gameplay.Tests.Unit.BgmFlowRuntimeTests.GlobalAudioFlowRoot_Awake_PreventsDuplicatePersistentRoots | Audio/BGM rows are deferred to the dedicated ADR-gated lane. | cross-lane blocking handoff queue | same-revision row plus failure capture showing audio/BGM ownership or registry scope | 2026-04-22 20:43:49 KST | pending | pending-acceptance | TestResults/wsl-unity-full-editmode.xml (2026-04-22 20:43:27 KST) |  |
| Lane E | Lane E | Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_PushBoxTerrainStopperScenario_ProducesSameHashTraceAndEventLog | Terrain and occupancy semantics remain deferred behind the dedicated gate ADR. | cross-lane blocking handoff queue | same-revision row plus failure capture showing terrain or occupancy semantics scope | 2026-04-22 20:43:49 KST | pending | pending-acceptance | TestResults/wsl-unity-full-editmode.xml (2026-04-22 20:43:27 KST) |  |
| Lane E | Lane E | Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_MoveAcrossBottomTopEdge_FailsWhenRotatedDestinationTerrainBlocked | Terrain and occupancy semantics remain deferred behind the dedicated gate ADR. | cross-lane blocking handoff queue | same-revision row plus failure capture showing terrain or occupancy semantics scope | 2026-04-22 20:43:49 KST | pending | pending-acceptance | TestResults/wsl-unity-full-editmode.xml (2026-04-22 20:43:27 KST) |  |
| Lane E | Lane E | Game.Feature.Gameplay.Tests.Unit.AttackInputNormalizationTests.ImpactReservationComparer_PreservesFaceBeforePlanarOrder | Terrain and occupancy semantics remain deferred behind the dedicated gate ADR. | cross-lane blocking handoff queue | same-revision row plus failure capture showing terrain or occupancy semantics scope | 2026-04-22 20:43:49 KST | pending | pending-acceptance | TestResults/wsl-unity-full-editmode.xml (2026-04-22 20:43:27 KST) | stale-literal / trace / comparer drift |
| Lane E | Lane E | Game.Feature.Gameplay.Tests.Unit.WorldStatePlacementInvariantTests.CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy | Terrain and occupancy semantics remain deferred behind the dedicated gate ADR. | cross-lane blocking handoff queue | same-revision row plus failure capture showing terrain or occupancy semantics scope | 2026-04-22 20:43:49 KST | pending | pending-acceptance | TestResults/wsl-unity-full-editmode.xml (2026-04-22 20:43:27 KST) |  |

