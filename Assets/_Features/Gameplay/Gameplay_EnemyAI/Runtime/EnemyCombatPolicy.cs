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

    [Serializable]
    public struct WindupMeleeSettings
    {
        public const float DefaultVisualRangeSlackCells = 0.10f;
        public const float MaxVisualRangeSlackCells = 0.15f;

        [SerializeField] private float visualRangeSlackCells;

        public WindupMeleeSettings(float visualRangeSlackCells)
        {
            this.visualRangeSlackCells = visualRangeSlackCells;
        }

        public float VisualRangeSlackCells => visualRangeSlackCells;

        public int VisualRangeSlackUnits =>
            Mathf.RoundToInt(Mathf.Clamp(visualRangeSlackCells, 0f, MaxVisualRangeSlackCells) * KinematicFixed.UnitsPerCell);

        public void Validate(string paramName)
        {
            if (visualRangeSlackCells < 0f ||
                visualRangeSlackCells > MaxVisualRangeSlackCells)
            {
                throw new ArgumentException(
                    $"WindupMelee visual range slack must be between 0 and {MaxVisualRangeSlackCells} cells.",
                    paramName);
            }
        }

        public static WindupMeleeSettings CreateDefault()
        {
            return new WindupMeleeSettings(DefaultVisualRangeSlackCells);
        }
    }

    [Serializable]
    public struct WindupForwardCellProjectileSettings
    {
        [SerializeField] private float visualStartSlackCells;
        [SerializeField] private int impactDelayTicks;
        [SerializeField] private int damage;
        [SerializeField] private int activePendingImpactLimitPerOwner;
        [SerializeField] private int attackCooldownTicks;
        [SerializeField] private bool sameSurfaceOnly;
        [SerializeField] private bool sameFaceOnly;
        [SerializeField] private bool requireValidForwardCell;
        [SerializeField] private bool showDangerMarkerOnWindupStart;

        public WindupForwardCellProjectileSettings(
            float visualStartSlackCells,
            int impactDelayTicks,
            int damage,
            int activePendingImpactLimitPerOwner,
            int attackCooldownTicks = 0,
            bool sameSurfaceOnly = true,
            bool sameFaceOnly = true,
            bool requireValidForwardCell = true,
            bool showDangerMarkerOnWindupStart = true)
        {
            this.visualStartSlackCells = visualStartSlackCells;
            this.impactDelayTicks = impactDelayTicks;
            this.damage = damage;
            this.activePendingImpactLimitPerOwner = activePendingImpactLimitPerOwner;
            this.attackCooldownTicks = attackCooldownTicks;
            this.sameSurfaceOnly = sameSurfaceOnly;
            this.sameFaceOnly = sameFaceOnly;
            this.requireValidForwardCell = requireValidForwardCell;
            this.showDangerMarkerOnWindupStart = showDangerMarkerOnWindupStart;
        }

        public int AttackRangeCells => 1;

        public float VisualStartSlackCells => visualStartSlackCells;

        public int VisualStartSlackUnits =>
            Mathf.RoundToInt(Mathf.Clamp(visualStartSlackCells, 0f, WindupMeleeSettings.MaxVisualRangeSlackCells) * KinematicFixed.UnitsPerCell);

        public int ImpactDelayTicks => impactDelayTicks;

        public int Damage => damage;

        public int ActivePendingImpactLimitPerOwner => activePendingImpactLimitPerOwner;

        public int AttackCooldownTicks => attackCooldownTicks;

        public bool SameSurfaceOnly => sameSurfaceOnly;

        public bool SameFaceOnly => sameFaceOnly;

        public bool RequireValidForwardCell => requireValidForwardCell;

        public bool ShowDangerMarkerOnWindupStart => showDangerMarkerOnWindupStart;

        public WindupMeleeSettings ToWindupStartSettings()
        {
            return new WindupMeleeSettings(visualStartSlackCells);
        }

        public void Validate(string paramName)
        {
            if (visualStartSlackCells < 0f ||
                visualStartSlackCells > WindupMeleeSettings.MaxVisualRangeSlackCells)
            {
                throw new ArgumentException(
                    $"WindupForwardCellProjectile visual start slack must be between 0 and {WindupMeleeSettings.MaxVisualRangeSlackCells} cells.",
                    paramName);
            }

            if (impactDelayTicks <= 0)
            {
                throw new ArgumentException("WindupForwardCellProjectile impact delay must be positive.", paramName);
            }

            if (damage <= 0)
            {
                throw new ArgumentException("WindupForwardCellProjectile damage must be positive.", paramName);
            }

            if (activePendingImpactLimitPerOwner <= 0)
            {
                throw new ArgumentException("WindupForwardCellProjectile active pending impact limit must be positive.", paramName);
            }

            if (attackCooldownTicks < 0)
            {
                throw new ArgumentException("WindupForwardCellProjectile attack cooldown must be non-negative.", paramName);
            }
        }

        public static WindupForwardCellProjectileSettings CreateDefault()
        {
            return new WindupForwardCellProjectileSettings(
                WindupMeleeSettings.DefaultVisualRangeSlackCells,
                impactDelayTicks: 1,
                damage: 1,
                activePendingImpactLimitPerOwner: 1);
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

    public sealed class WindupForwardCellProjectileAttackDecisionStrategy : IAttackDecisionStrategy
    {
        public static readonly WindupForwardCellProjectileAttackDecisionStrategy Instance = new();

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
            return MeleeAttackDecisionStrategy.Instance.IsTargetInRange(source, target, settings);
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
