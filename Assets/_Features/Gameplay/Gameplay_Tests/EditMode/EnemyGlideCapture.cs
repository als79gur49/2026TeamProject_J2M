using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests
{
    // Kept independent of EnemyGlideExecutor so the same capture runs before and after extraction.
    public static class EnemyGlideCapture
    {
        public const string Prefix = "GLIDE_CAPTURE_V1|";

        [Serializable]
        private sealed class Record
        {
            public int schema = 1;
            public string caseId;
            public string variantId;
            public string recordId;
            public string seam;
            public int tick;
            public string[] beforeStates;
            public string[] afterStates;
            public string[] proposedStates;
            public string[] beforeEntities;
            public string[] afterEntities;
            public string[] beforePending;
            public string[] afterPending;
            public string[] beforeKinematic;
            public string[] afterKinematic;
            public string[] movementIntents;
            public string[] writes;
            public string[] updates;
            public string[] events;
            public string[] presentation;
            public string[] observations;
            public string hash;
            public string trace;
        }

        public static void Logic(
            string caseId,
            string variantId,
            string recordId,
            int tick,
            WorldSnapshot before,
            WorldSnapshot after,
            IReadOnlyList<int> sourceIds,
            IReadOnlyList<EnemyGlideRuntimeState?> proposedStates,
            IReadOnlyList<string> writes,
            IReadOnlyList<string> updates,
            IReadOnlyList<RawMovementIntent> movementIntents = null,
            IReadOnlyList<string> observations = null)
        {
            Write(new Record
            {
                caseId = caseId,
                variantId = variantId,
                recordId = recordId,
                seam = "logic",
                tick = tick,
                beforeStates = sourceIds.Select(id => State(before, id)).ToArray(),
                afterStates = sourceIds.Select(id => State(after, id)).ToArray(),
                proposedStates = sourceIds.Select((id, index) => proposedStates[index].HasValue
                    ? State(id, proposedStates[index].Value)
                    : $"E={id}|State=NoWrite").ToArray(),
                beforeEntities = sourceIds.Select(id => Entity(before, id)).ToArray(),
                afterEntities = sourceIds.Select(id => Entity(after, id)).ToArray(),
                beforePending = sourceIds.Select(id => Pending(before, id)).ToArray(),
                afterPending = sourceIds.Select(id => Pending(after, id)).ToArray(),
                beforeKinematic = sourceIds.Select(id => Kinematic(before, id)).ToArray(),
                afterKinematic = sourceIds.Select(id => Kinematic(after, id)).ToArray(),
                movementIntents = Intents(movementIntents),
                writes = writes.ToArray(),
                updates = updates.ToArray(),
                events = Array.Empty<string>(),
                presentation = Array.Empty<string>(),
                observations = (observations ?? Array.Empty<string>()).ToArray(),
                hash = "<not-observable: no TickResult at direct Logic seam>",
                trace = "<not-observable: no TickResult at direct Logic seam>",
            });
        }

        public static void Tick(
            string caseId,
            string variantId,
            string recordId,
            WorldSnapshot before,
            TickResult result,
            WorldState world,
            params int[] sourceIds)
        {
            var after = world.CreateSnapshot();
            Write(new Record
            {
                caseId = caseId,
                variantId = variantId,
                recordId = recordId,
                seam = "pipeline",
                tick = result.TickIndex,
                beforeStates = sourceIds.Select(id => State(before, id)).ToArray(),
                afterStates = sourceIds.Select(id => State(after, id)).ToArray(),
                proposedStates = sourceIds.Select(id => $"E={id}|State=<not-observable: batch proposal is in trace>").ToArray(),
                beforeEntities = sourceIds.Select(id => Entity(before, id)).ToArray(),
                afterEntities = sourceIds.Select(id => Entity(after, id)).ToArray(),
                beforePending = sourceIds.Select(id => Pending(before, id)).ToArray(),
                afterPending = sourceIds.Select(id => Pending(after, id)).ToArray(),
                beforeKinematic = sourceIds.Select(id => Kinematic(before, id)).ToArray(),
                afterKinematic = sourceIds.Select(id => Kinematic(after, id)).ToArray(),
                movementIntents = Intents(result.MovementPhaseResult.RawIntents),
                writes = Array.Empty<string>(),
                updates = Array.Empty<string>(),
                events = result.EventLog.ToArray(),
                presentation = result.PresentationData.EnemyGlideSignals.Select(signal =>
                    $"Glide|E={signal.EntityId}|Cell={signal.AnchorCell}|Phase={signal.Phase}|Seq={signal.Sequence}|Elapsed={signal.PhaseElapsedTicks}|Total={signal.PhaseTotalTicks}|Progress={signal.NormalizedPhaseProgress:R}|Lift={signal.LiftHeightUnits}|Dip={signal.RecoveryDipHeightUnits}|Height={signal.CurrentHeightUnits}|Airborne={signal.IsAirborneVisual}|WantsRecover={signal.WantsRecover}|Terminal={signal.IsTerminalZero}")
                    .Concat(result.PresentationData.KinematicMotionTracks.Select(track =>
                        $"Kinematic|E={track.EntityId}|Source={track.SourceAnchorCell}|Destination={track.DestinationAnchorCell}|Mode={track.MotionMode}|Terminal={track.TerminalKind}|Started={track.StartedTick}|Elapsed={track.ElapsedTicks}|Total={track.TotalTicks}"))
                    .ToArray(),
                observations = Array.Empty<string>(),
                hash = result.DeterminismHash,
                trace = result.Trace.Text,
            });
        }

        private static string[] Intents(IEnumerable<RawMovementIntent> intents) =>
            (intents ?? Array.Empty<RawMovementIntent>()).Select(intent =>
                $"Source={intent.SourceId}|Destination={intent.Destination}|Command={intent.CommandKind}|Sequence={intent.LocalSequence}|Cooldown={intent.MoveCooldownTicks}").ToArray();

        private static string State(WorldSnapshot snapshot, int id) =>
            snapshot.TryGetEnemyGlideState(id, out var state)
                ? State(id, state)
                : $"E={id}|State=Absent";

        private static string State(int id, EnemyGlideRuntimeState state) =>
            $"E={id}|State=Present|Phase={state.Phase}|Active={state.IsActive}|WantsRecover={state.WantsRecover}|Sequence={state.Sequence}|WindupUntil={state.WindupUntilTickExclusive}|ActiveUntil={state.ActiveUntilTickExclusive}|RecoveryUntil={state.RecoveryUntilTickExclusive}|CooldownUntil={state.CooldownUntilTickExclusive}|WindupTicks={state.WindupTicks}|DurationTicks={state.DurationTicks}|RecoveryTicks={state.RecoveryTicks}|CooldownTicks={state.CooldownTicks}|GlideMoveTicks={state.GlideMoveTicks}|LastExited={state.LastExitedTick}|DelayInitialized={state.InitialDelayInitialized}|DelayRemaining={state.InitialDelayTicksRemaining}|HasLockedStep={state.HasLockedStep}|LockedX={state.LockedStepX}|LockedY={state.LockedStepY}|LockedTarget={state.LockedTargetEntityId}";

        private static string Entity(WorldSnapshot snapshot, int id) =>
            snapshot.TryGetEntity(id, out var entity)
                ? $"E={id}|Entity=Present|Cell={entity.position}|Facing={entity.facing}|Team={entity.teamId}|Hp={entity.hp}|Marked={entity.markedForDeath}|Mode={entity.aiMode}|Timer={entity.aiStateTimer}|Presence={entity.boardPresence}|LocomotionCooldown={entity.enemyLocomotionCooldownTicks}|AttackCooldown={entity.enemyAttackCooldownTicks}"
                : $"E={id}|Entity=Absent";

        private static string Pending(WorldSnapshot snapshot, int id) =>
            snapshot.TryGetPendingEnemyBlockedReaction(id, out var pending)
                ? $"E={id}|Pending=Present|Kind={pending.Kind}|Mode={pending.ModeAtBlock}|Source={pending.SourceCell}|Blocked={pending.BlockedTargetCell}|Direction={pending.BlockedDirection}|Blocker={pending.BlockerKind}|Solid={pending.BlockerSolidKind}|Type={pending.BlockerEntityType}|BlockerEntity={pending.BlockerEntityId}|Created={pending.CreatedTick}|Expire={pending.ExpireTick}"
                : $"E={id}|Pending=Absent";

        private static string Kinematic(WorldSnapshot snapshot, int id) =>
            snapshot.TryGetUnitKinematicState(id, out var state)
                ? $"E={id}|Kinematic=Present|Mode={state.mode}|Forced={state.forcedOp}|Offset={state.localOffset}|Velocity={state.velocity}|RemainingDistance={state.remainingDistanceUnits}|RemainingTicks={state.remainingTicks}|Speed={state.speedScalePermille}|Sequence={state.sequenceId}|Elapsed={state.elapsedTicks}|Total={state.totalTicks}|Commit={state.commitTick}|Started={state.startedTick}|StepX={state.stepDirectionX}|StepY={state.stepDirectionY}"
                : $"E={id}|Kinematic=Absent";

        private static void Write(Record record)
        {
            TestContext.Out.WriteLine(Prefix + JsonUtility.ToJson(record));
        }
    }
}
