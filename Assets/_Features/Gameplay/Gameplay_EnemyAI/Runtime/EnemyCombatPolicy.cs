using System;
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

        public static AttackDecisionSettings CreateAdjacentRange()
        {
            return new AttackDecisionSettings(attackRange: 1);
        }
    }

    [Serializable]
    public struct ProjectileWindupSettings
    {
        public const float DefaultVisualRangeSlackCells = 0.10f;
        public const float MaxVisualRangeSlackCells = 0.15f;

        [SerializeField] private float visualRangeSlackCells;

        public ProjectileWindupSettings(float visualRangeSlackCells)
        {
            this.visualRangeSlackCells = visualRangeSlackCells;
        }

        public float VisualRangeSlackCells => visualRangeSlackCells;

        public int VisualRangeSlackUnits =>
            Mathf.RoundToInt(Mathf.Clamp(visualRangeSlackCells, 0f, MaxVisualRangeSlackCells) * SimulationFixed.UnitsPerCell);

        public void Validate(string paramName)
        {
            if (visualRangeSlackCells < 0f ||
                visualRangeSlackCells > MaxVisualRangeSlackCells)
            {
                throw new ArgumentException(
                    $"ProjectileWindup visual range slack must be between 0 and {MaxVisualRangeSlackCells} cells.",
                    paramName);
            }
        }

        public static ProjectileWindupSettings CreateDefault()
        {
            return new ProjectileWindupSettings(DefaultVisualRangeSlackCells);
        }
    }

    [Serializable]
    public struct WindupForwardCellProjectileSettings
    {
        [SerializeField] private float visualStartSlackCells;
        [SerializeField] private int impactDelayTicks;
        [SerializeField] private int impactDelayTicksPerCell;
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
            bool showDangerMarkerOnWindupStart = true,
            int impactDelayTicksPerCell = 0)
        {
            this.visualStartSlackCells = visualStartSlackCells;
            this.impactDelayTicks = impactDelayTicks;
            this.impactDelayTicksPerCell = impactDelayTicksPerCell;
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
            Mathf.RoundToInt(Mathf.Clamp(visualStartSlackCells, 0f, ProjectileWindupSettings.MaxVisualRangeSlackCells) * SimulationFixed.UnitsPerCell);

        public int ImpactDelayTicks => impactDelayTicks;

        public int ImpactDelayTicksPerCell => impactDelayTicksPerCell;

        public int Damage => damage;

        public int ActivePendingImpactLimitPerOwner => activePendingImpactLimitPerOwner;

        public int AttackCooldownTicks => attackCooldownTicks;

        public bool SameSurfaceOnly => sameSurfaceOnly;

        public bool SameFaceOnly => sameFaceOnly;

        public bool RequireValidForwardCell => requireValidForwardCell;

        public bool ShowDangerMarkerOnWindupStart => showDangerMarkerOnWindupStart;

        public ProjectileWindupSettings ToWindupStartSettings()
        {
            return new ProjectileWindupSettings(visualStartSlackCells);
        }

        public int ResolveImpactDelayTicks(int distanceCells)
        {
            if (impactDelayTicksPerCell <= 0)
            {
                return impactDelayTicks;
            }

            return checked(Math.Max(1, distanceCells) * impactDelayTicksPerCell);
        }

        public void Validate(string paramName)
        {
            if (visualStartSlackCells < 0f ||
                visualStartSlackCells > ProjectileWindupSettings.MaxVisualRangeSlackCells)
            {
                throw new ArgumentException(
                    $"WindupForwardCellProjectile visual start slack must be between 0 and {ProjectileWindupSettings.MaxVisualRangeSlackCells} cells.",
                    paramName);
            }

            if (impactDelayTicks <= 0)
            {
                throw new ArgumentException("WindupForwardCellProjectile impact delay must be positive.", paramName);
            }

            if (impactDelayTicksPerCell < 0)
            {
                throw new ArgumentException("WindupForwardCellProjectile per-cell impact delay must be non-negative.", paramName);
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
                ProjectileWindupSettings.DefaultVisualRangeSlackCells,
                impactDelayTicks: 1,
                damage: 1,
                activePendingImpactLimitPerOwner: 1);
        }
    }

    public interface IAttackDecisionStrategy
    {
        bool IsTargetInRange(
            in EntityState source,
            in EntityState target,
            in AttackDecisionSettings settings);
    }

    public sealed class NoAttackDecisionStrategy : IAttackDecisionStrategy
    {
        public static readonly NoAttackDecisionStrategy Instance = new();

        public bool IsTargetInRange(
            in EntityState source,
            in EntityState target,
            in AttackDecisionSettings settings)
        {
            return false;
        }
    }

    public sealed class WindupForwardCellProjectileAttackDecisionStrategy : IAttackDecisionStrategy
    {
        public static readonly WindupForwardCellProjectileAttackDecisionStrategy Instance = new();

        public bool IsTargetInRange(
            in EntityState source,
            in EntityState target,
            in AttackDecisionSettings settings)
        {
            return EnemyAttackRangeQueries.IsTargetInRange(source, target, settings);
        }
    }

    public sealed class ContactSameCellAttackDecisionStrategy : IAttackDecisionStrategy
    {
        public static readonly ContactSameCellAttackDecisionStrategy Instance = new();

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
