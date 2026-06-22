using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class JumpLandingPlan
    {
        public JumpLandingPlan(
            int actionPlanId,
            int contestId,
            int sourceId,
            int priority,
            int targetId,
            SurfaceCell destinationCell,
            string landingRule,
            EnemyJumpRuntimeState successState,
            EnemyJumpRuntimeState retryState,
            JumpLandingKind landingKind)
        {
            ActionPlanId = actionPlanId;
            ContestId = contestId;
            SourceId = sourceId;
            Priority = priority;
            TargetId = targetId;
            DestinationCell = destinationCell;
            LandingRule = landingRule ?? string.Empty;
            SuccessState = successState;
            RetryState = retryState;
            LandingKind = landingKind;
        }

        public int ActionPlanId { get; }

        public int ContestId { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public int TargetId { get; }

        public SurfaceCell DestinationCell { get; }

        public string LandingRule { get; }

        public EnemyJumpRuntimeState SuccessState { get; }

        public EnemyJumpRuntimeState RetryState { get; }

        public JumpLandingKind LandingKind { get; }
    }

    internal enum SpawnSourceKind
    {
        Attack = 0,
    }

    internal enum JumpLandingKind
    {
        ExactStack = 0,
        Contested = 1,
        RetryOnly = 2,
        CrushBoxAndLand = 3,
    }

    internal abstract class ActionPlanPayload
    {
        protected ActionPlanPayload(
            int actionPlanId,
            int intentId,
            int sourceActorEntityId,
            int priority,
            ResolvedActionSemanticKind semanticKind)
        {
            ActionPlanId = actionPlanId;
            IntentId = intentId;
            SourceActorEntityId = sourceActorEntityId;
            Priority = priority;
            SemanticKind = semanticKind;
        }

        public int ActionPlanId { get; }

        public int IntentId { get; }

        public int SourceActorEntityId { get; }

        public int Priority { get; }

        public ResolvedActionSemanticKind SemanticKind { get; }
    }

    internal sealed class MovementIntentPartitions
    {
        public MovementIntentPartitions(
            IReadOnlyList<MoveIntent> playerFree2DOrdinaryIntents,
            IReadOnlyList<MoveIntent> playerExplicitActionIntents,
            IReadOnlyList<MoveIntent> genericExpansionIntents)
        {
            PlayerFree2DOrdinaryIntents = playerFree2DOrdinaryIntents ?? throw new ArgumentNullException(nameof(playerFree2DOrdinaryIntents));
            PlayerExplicitActionIntents = playerExplicitActionIntents ?? throw new ArgumentNullException(nameof(playerExplicitActionIntents));
            GenericExpansionIntents = genericExpansionIntents ?? throw new ArgumentNullException(nameof(genericExpansionIntents));
        }

        public IReadOnlyList<MoveIntent> PlayerFree2DOrdinaryIntents { get; }

        public IReadOnlyList<MoveIntent> PlayerExplicitActionIntents { get; }

        public IReadOnlyList<MoveIntent> GenericExpansionIntents { get; }
    }

    internal static class MovementIntentPartitioner
    {
        public static MovementIntentPartitions Partition(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            ISet<int> consumedPlayerActionAttemptEntityIds = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            var playerFree2DOrdinaryIntents = new List<MoveIntent>();
            var playerExplicitActionIntents = new List<MoveIntent>();
            var genericExpansionIntents = new List<MoveIntent>();

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];
                if (intent == null ||
                    !snapshot.TryGetEntity(intent.SourceId, out var entity) ||
                    entity.type != EntityType.Unit ||
                    !snapshot.TryGetPlayerControlState(intent.SourceId, out _))
                {
                    genericExpansionIntents.Add(intent);
                    continue;
                }

                if (consumedPlayerActionAttemptEntityIds != null &&
                    consumedPlayerActionAttemptEntityIds.Contains(intent.SourceId))
                {
                    playerExplicitActionIntents.Add(intent);
                    continue;
                }

                if (IsPlayerExplicitActionIntent(snapshot, entity, intent))
                {
                    playerExplicitActionIntents.Add(intent);
                    genericExpansionIntents.Add(intent);
                    continue;
                }

                if (intent.CommandKind == Movement.MovementCommandKind.Move)
                {
                    playerFree2DOrdinaryIntents.Add(intent);
                    continue;
                }

                genericExpansionIntents.Add(intent);
            }

            return new MovementIntentPartitions(
                playerFree2DOrdinaryIntents,
                playerExplicitActionIntents,
                genericExpansionIntents);
        }

        private static bool IsPlayerExplicitActionIntent(
            WorldSnapshot snapshot,
            in EntityState entity,
            MoveIntent intent)
        {
            if (intent.CommandKind == Movement.MovementCommandKind.Push ||
                intent.CommandKind == Movement.MovementCommandKind.Flip)
            {
                return true;
            }

            if (intent.CommandKind != Movement.MovementCommandKind.Move)
            {
                return false;
            }

            var delta = intent.Destination - entity.position.PlanarPosition;
            if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1 ||
                !snapshot.TryResolveUnitStep(
                    entity.position,
                    delta,
                    out var destination,
                    out _,
                    out var resolvedTopology))
            {
                return false;
            }

            return snapshot.TryGetSolidSemanticAt(resolvedTopology, destination, out var targetSemantic) &&
                   targetSemantic.Kind == SolidKind.Box &&
                   (targetSemantic.Entity.boxCapabilities & BoxCapabilities.Item) == BoxCapabilities.Item;
        }
    }

}
