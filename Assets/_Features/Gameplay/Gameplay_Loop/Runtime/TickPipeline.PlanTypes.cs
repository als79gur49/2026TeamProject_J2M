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

}
