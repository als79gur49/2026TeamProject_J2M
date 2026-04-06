using System;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [Serializable]
    public struct AttackDecisionSettings
    {
        [SerializeField] private int attackRange;

        public AttackDecisionSettings(int attackRange)
        {
            this.attackRange = attackRange;
        }

        public int AttackRange => attackRange;

        public void Validate(string paramName)
        {
            if (attackRange <= 0)
            {
                throw new ArgumentException("Enemy attack decision settings require a positive attack range.", paramName);
            }
        }

        public static AttackDecisionSettings CreateDefaultMelee()
        {
            return new AttackDecisionSettings(attackRange: 1);
        }
    }

    public interface IAttackDecisionStrategy
    {
        bool TryBuildAttackIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiCommonSettings commonSettings,
            in AttackDecisionSettings settings,
            out RawAttackIntent intent);

        bool IsTargetInRange(
            in EntityState source,
            in EntityState target,
            in AttackDecisionSettings settings);
    }

    public sealed class NoAttackDecisionStrategy : IAttackDecisionStrategy
    {
        public static readonly NoAttackDecisionStrategy Instance = new();

        public bool TryBuildAttackIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiCommonSettings commonSettings,
            in AttackDecisionSettings settings,
            out RawAttackIntent intent)
        {
            intent = default;
            return false;
        }

        public bool IsTargetInRange(
            in EntityState source,
            in EntityState target,
            in AttackDecisionSettings settings)
        {
            return false;
        }
    }

    public sealed class MeleeAttackDecisionStrategy : IAttackDecisionStrategy
    {
        public static readonly MeleeAttackDecisionStrategy Instance = new();

        public bool TryBuildAttackIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiCommonSettings commonSettings,
            in AttackDecisionSettings settings,
            out RawAttackIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            if (!IsTargetInRange(source, target, settings))
            {
                return false;
            }

            intent = new RawAttackIntent(
                source.entityId,
                commonSettings.AttackPriority,
                target.entityId);
            return true;
        }

        public bool IsTargetInRange(
            in EntityState source,
            in EntityState target,
            in AttackDecisionSettings settings)
        {
            settings.Validate(nameof(settings));

            var distance = GetPlanarDistance(source.position, target.position);
            return distance.HasValue && distance.Value <= settings.AttackRange;
        }

        private static int? GetPlanarDistance(SurfaceCell source, SurfaceCell target)
        {
            if (source.face != target.face)
            {
                return null;
            }

            var delta = target - source;
            return Math.Abs(delta.x) + Math.Abs(delta.y);
        }
    }

    public sealed class ContactSameCellAttackDecisionStrategy : IAttackDecisionStrategy
    {
        public static readonly ContactSameCellAttackDecisionStrategy Instance = new();

        public bool TryBuildAttackIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiCommonSettings commonSettings,
            in AttackDecisionSettings settings,
            out RawAttackIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            if (!IsTargetInRange(source, target, settings))
            {
                return false;
            }

            intent = new RawAttackIntent(
                source.entityId,
                commonSettings.AttackPriority,
                target.entityId);
            return true;
        }

        public bool IsTargetInRange(
            in EntityState source,
            in EntityState target,
            in AttackDecisionSettings settings)
        {
            settings.Validate(nameof(settings));
            return source.position.face == target.position.face &&
                   source.position.PlanarPosition == target.position.PlanarPosition;
        }
    }
}
