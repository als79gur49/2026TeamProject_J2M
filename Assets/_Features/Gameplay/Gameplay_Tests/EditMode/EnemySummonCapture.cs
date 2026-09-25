using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests
{
    // The same public runtime observations are emitted by the pre-extraction and post-extraction tests.
    // The external evidence checker verifies the case/record schema before comparing ordered values.
    public static class EnemySummonCapture
    {
        public const string Prefix = "SUMMON_CAPTURE_V1|";

        [Serializable]
        private sealed class Record
        {
            public int schema = 1;
            public string caseId;
            public string recordId;
            public string seam;
            public int tick;
            public string[] states;
            public string[] entities;
            public string[] summoned;
            public string[] definitionBindings;
            public string[] pendingBlockedReactions;
            public string[] movementIntents;
            public string[] events;
            public string[] summonSignals;
            public string[] windupWarnings;
            public string[] visibility;
            public string[] presentationBindings;
            public string[] writes;
            public string[] triggers;
            public string[] updates;
            public string[] observations;
            public string hash;
            public string trace;
        }

        public static void Tick(
            string caseId,
            string recordId,
            TickResult result,
            WorldState world,
            params int[] sourceIds)
        {
            var snapshot = world.CreateSnapshot();
            var summoned = new List<SummonedEntitySnapshotEntry>();
            var bindings = new List<EnemyDefinitionBindingSnapshotEntry>();
            snapshot.EnumerateSummonedEntityStatesOrdered(summoned);
            snapshot.EnumerateEnemyDefinitionBindingStatesOrdered(bindings);
            Write(new Record
            {
                caseId = caseId,
                recordId = recordId,
                seam = "pipeline",
                tick = result.TickIndex,
                states = sourceIds.Select(id => State(snapshot, id)).ToArray(),
                entities = result.FinalEntities.Select(entity =>
                    $"E={entity.entityId}|Cell={entity.position}|Facing={entity.facing}|Team={entity.teamId}|Hp={entity.hp}|MarkedForDeath={entity.markedForDeath}|Mode={entity.aiMode}|Timer={entity.aiStateTimer}|Presence={entity.boardPresence}|LocomotionCooldown={entity.enemyLocomotionCooldownTicks}").ToArray(),
                summoned = summoned.Select(entry =>
                    $"E={entry.EntityId}|Source={entry.State.SourceEntityId}|Effect={entry.State.SourceEffectIndex}").ToArray(),
                definitionBindings = bindings.Select(entry =>
                    $"E={entry.EntityId}|Archetype={entry.State.ArchetypeId}").ToArray(),
                pendingBlockedReactions = sourceIds.Select(id => Pending(snapshot, id)).ToArray(),
                movementIntents = result.MovementPhaseResult.RawIntents.Select(intent =>
                    $"Source={intent.SourceId}|Destination={intent.Destination}|Command={intent.CommandKind}|Sequence={intent.LocalSequence}|Cooldown={intent.MoveCooldownTicks}").ToArray(),
                events = result.EventLog.ToArray(),
                summonSignals = result.PresentationData.EnemySummonSignals.Select(signal =>
                    $"E={signal.EntityId}|Phase={signal.Phase}|Start={signal.StartTick}|Execute={signal.ExecuteTick}|Duration={signal.DurationTicks}|Effect={signal.EffectIndex}|Sequence={signal.ActivationSequence}").ToArray(),
                windupWarnings = result.PresentationData.SummonWindupWarnings.Select(signal =>
                    $"E={signal.SourceEntityId}|Effect={signal.EffectIndex}|Cell={signal.SourceCell}|Facing={signal.Facing}|Start={signal.WindupStartTick}|End={signal.WindupEndTick}|Sequence={signal.ActivationSequence}|Tick={signal.TickIndex}|Seed={signal.PresentationSeed}").ToArray(),
                visibility = result.PresentationData.VisibilityChanges.Select(change =>
                    $"E={change.EntityId}|Kind={change.ChangeKind}|Cell={change.Cell}|Facing={change.Facing}").ToArray(),
                presentationBindings = result.PresentationData.SummonedEnemyPresentationBindings.Select(binding =>
                    $"E={binding.EntityId}|Source={binding.SourceEntityId}|Archetype={binding.ArchetypeId}|HasDefinition={binding.HasEnemyDefinitionBinding}").ToArray(),
                writes = Array.Empty<string>(), // Pipeline writes and requests are retained in the ordered trace.
                triggers = Array.Empty<string>(),
                updates = Array.Empty<string>(),
                observations = Array.Empty<string>(),
                hash = result.DeterminismHash,
                trace = result.Trace.Text,
            });
        }

        public static void Logic(
            string caseId,
            string recordId,
            int tick,
            WorldSnapshot snapshot,
            int sourceId,
            IReadOnlyList<string> writes,
            IReadOnlyList<string> triggers,
            IReadOnlyList<string> updates,
            IReadOnlyList<string> observations = null,
            WorldSnapshot postSnapshot = null,
            EnemySummonBehaviorRuntimeState? proposedState = null)
        {
            var directObservations = new List<string>(observations ?? Array.Empty<string>());
            if (postSnapshot != null)
            {
                directObservations.Add("WorldBefore=" + State(snapshot, sourceId));
                directObservations.Add("WorldAfter=" + State(postSnapshot, sourceId));
                directObservations.Add("Proposed=" + (proposedState.HasValue
                    ? State(sourceId, proposedState.Value)
                    : $"E={sourceId}|State=NoWrite"));
            }
            Write(new Record
            {
                caseId = caseId,
                recordId = recordId,
                seam = "logic",
                tick = tick,
                states = new[] { State(snapshot, sourceId) },
                entities = snapshot.TryGetEntity(sourceId, out var entity)
                    ? new[] { $"E={entity.entityId}|Cell={entity.position}|Facing={entity.facing}|Team={entity.teamId}|Hp={entity.hp}|MarkedForDeath={entity.markedForDeath}|Mode={entity.aiMode}|Presence={entity.boardPresence}|LocomotionCooldown={entity.enemyLocomotionCooldownTicks}" }
                    : Array.Empty<string>(),
                summoned = Array.Empty<string>(),
                definitionBindings = Array.Empty<string>(),
                pendingBlockedReactions = new[] { Pending(snapshot, sourceId) },
                movementIntents = Array.Empty<string>(),
                events = Array.Empty<string>(),
                summonSignals = Array.Empty<string>(),
                windupWarnings = Array.Empty<string>(),
                visibility = Array.Empty<string>(),
                presentationBindings = Array.Empty<string>(),
                writes = writes.ToArray(),
                triggers = triggers.ToArray(),
                updates = updates.ToArray(),
                observations = directObservations.ToArray(),
                hash = "<not-observable>",
                trace = "<not-observable>",
            });
        }

        private static string State(WorldSnapshot snapshot, int id)
        {
            if (!snapshot.TryGetEnemySummonBehaviorState(id, out var state))
            {
                return $"E={id}|State=Absent";
            }

            return State(id, state);
        }

        private static string State(int id, EnemySummonBehaviorRuntimeState state) =>
            $"E={id}|State=Present|Phase={state.phase}|Cooldown={state.cooldownTicksRemaining}|WindupStart={state.windupStartTick}|WindupEnd={state.windupEndTick}|RecoverStart={state.recoverStartTick}|RecoverEnd={state.recoverEndTickExclusive}|Sequence={state.activationSequence}|SuppressUntil={state.movementSuppressionUntilTickInclusive}";

        private static string Pending(WorldSnapshot snapshot, int id)
        {
            if (!snapshot.TryGetPendingEnemyBlockedReaction(id, out var reaction))
            {
                return $"E={id}|Pending=Absent";
            }

            return $"E={id}|Pending=Present|Kind={reaction.Kind}|Mode={reaction.ModeAtBlock}|Source={reaction.SourceCell}|Blocked={reaction.BlockedTargetCell}|Direction={reaction.BlockedDirection}|Blocker={reaction.BlockerKind}|Solid={reaction.BlockerSolidKind}|Type={reaction.BlockerEntityType}|BlockerEntity={reaction.BlockerEntityId}|Created={reaction.CreatedTick}|Expire={reaction.ExpireTick}";
        }

        private static void Write(Record record)
        {
            TestContext.Out.WriteLine(Prefix + JsonUtility.ToJson(record));
        }
    }
}
