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
    internal readonly struct StateChangeWritePayload
    {
        public StateChangeWritePayload(int entityId, EntityPhaseState state, int stateTimer)
        {
            EntityId = entityId;
            State = state;
            StateTimer = stateTimer;
        }

        public int EntityId { get; }

        public EntityPhaseState State { get; }

        public int StateTimer { get; }
    }

    internal readonly struct DamageWritePayload
    {
        public DamageWritePayload(
            int targetEntityId,
            int amount,
            bool hasPlayerDamageState,
            PlayerDamageState playerDamageState,
            AttackSourceKind attackSourceKind)
        {
            TargetEntityId = targetEntityId;
            Amount = amount;
            HasPlayerDamageState = hasPlayerDamageState;
            PlayerDamageState = playerDamageState;
            AttackSourceKind = attackSourceKind;
        }

        public int TargetEntityId { get; }

        public int Amount { get; }

        public bool HasPlayerDamageState { get; }

        public PlayerDamageState PlayerDamageState { get; }

        public AttackSourceKind AttackSourceKind { get; }
    }

    internal readonly struct SpawnWritePayload
    {
        public SpawnWritePayload(EntityState entityTemplate, SpawnSourceKind spawnSourceKind)
        {
            EntityTemplate = entityTemplate;
            SpawnSourceKind = spawnSourceKind;
        }

        public EntityState EntityTemplate { get; }

        public SpawnSourceKind SpawnSourceKind { get; }
    }

    internal readonly struct DelayedEnqueueWritePayload
    {
        public DelayedEnqueueWritePayload(
            int targetEntityId,
            int damage,
            int tickGenerated,
            int executeAtTick,
            int effectSequence)
        {
            TargetEntityId = targetEntityId;
            Damage = damage;
            TickGenerated = tickGenerated;
            ExecuteAtTick = executeAtTick;
            EffectSequence = effectSequence;
        }

        public int TargetEntityId { get; }

        public int Damage { get; }

        public int TickGenerated { get; }

        public int ExecuteAtTick { get; }

        public int EffectSequence { get; }
    }

    internal sealed class AttackActionPlanPayload : ActionPlanPayload
    {
        public AttackActionPlanPayload(
            int actionPlanId,
            int intentId,
            int sourceActorEntityId,
            int priority,
            ResolvedActionSemanticKind semanticKind,
            IReadOnlyList<StateChangeWritePayload> stateChangeWrites,
            IReadOnlyList<DamageWritePayload> damageWrites,
            IReadOnlyList<SpawnWritePayload> spawnWrites,
            IReadOnlyList<DestroyWritePayload> destroyWrites,
            IReadOnlyList<DelayedEnqueueWritePayload> delayedEnqueueWrites)
            : base(actionPlanId, intentId, sourceActorEntityId, priority, semanticKind)
        {
            StateChangeWrites = stateChangeWrites ?? throw new ArgumentNullException(nameof(stateChangeWrites));
            DamageWrites = damageWrites ?? throw new ArgumentNullException(nameof(damageWrites));
            SpawnWrites = spawnWrites ?? throw new ArgumentNullException(nameof(spawnWrites));
            DestroyWrites = destroyWrites ?? throw new ArgumentNullException(nameof(destroyWrites));
            DelayedEnqueueWrites = delayedEnqueueWrites ?? throw new ArgumentNullException(nameof(delayedEnqueueWrites));
        }

        public IReadOnlyList<StateChangeWritePayload> StateChangeWrites { get; }

        public IReadOnlyList<DamageWritePayload> DamageWrites { get; }

        public IReadOnlyList<SpawnWritePayload> SpawnWrites { get; }

        public IReadOnlyList<DestroyWritePayload> DestroyWrites { get; }

        public IReadOnlyList<DelayedEnqueueWritePayload> DelayedEnqueueWrites { get; }
    }

    internal sealed class JumpLandingActionPlanPayload : ActionPlanPayload
    {
        public JumpLandingActionPlanPayload(
            int actionPlanId,
            int sourceActorEntityId,
            int priority,
            JumpLandingKind landingKind,
            SurfaceCell destinationCell,
            int contestedTargetEntityId,
            string landingRule,
            EnemyJumpRuntimeState successJumpState,
            EnemyJumpRuntimeState retryJumpState)
            : base(actionPlanId, intentId: 0, sourceActorEntityId, priority, ResolvedActionSemanticKind.JumpLanding)
        {
            LandingKind = landingKind;
            DestinationCell = destinationCell;
            ContestedTargetEntityId = contestedTargetEntityId;
            LandingRule = landingRule ?? string.Empty;
            SuccessJumpState = successJumpState;
            RetryJumpState = retryJumpState;
        }

        public JumpLandingKind LandingKind { get; }

        public SurfaceCell DestinationCell { get; }

        public int ContestedTargetEntityId { get; }

        public string LandingRule { get; }

        public EnemyJumpRuntimeState SuccessJumpState { get; }

        public EnemyJumpRuntimeState RetryJumpState { get; }
    }

}
