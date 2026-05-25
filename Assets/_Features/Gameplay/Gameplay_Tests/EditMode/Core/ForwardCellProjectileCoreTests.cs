using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class ForwardCellProjectileCoreTests
    {
        [Test]
        [Category("Core")]
        public void ForwardProjectile_Range4_ResolvesFourthForwardCell()
        {
            var snapshot = CreateSnapshot();

            var resolved = WindupMeleeCombatPoseQueries.TryResolveForwardTargetCell(
                snapshot,
                new SurfaceCell(FaceId.Floor, 0, 0),
                Direction.Right,
                new SurfaceCell(FaceId.Floor, 4, 0),
                maxRangeCells: 4,
                out var targetCell);

            Assert.That(resolved, Is.True);
            Assert.That(targetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
        }

        [Test]
        [Category("Core")]
        public void ForwardProjectile_Range4_RejectsDiagonalTarget()
        {
            var snapshot = CreateSnapshot();

            var resolved = WindupMeleeCombatPoseQueries.TryResolveForwardTargetCell(
                snapshot,
                new SurfaceCell(FaceId.Floor, 0, 0),
                Direction.Right,
                new SurfaceCell(FaceId.Floor, 2, 2),
                maxRangeCells: 4,
                out _);

            Assert.That(resolved, Is.False);
        }

        [Test]
        [Category("Core")]
        public void ForwardProjectile_PerCellDelay_UsesPointFourSecondsAtSixtyTicks()
        {
            var settings = new WindupForwardCellProjectileSettings(
                WindupMeleeSettings.DefaultVisualRangeSlackCells,
                impactDelayTicks: 24,
                damage: 1,
                activePendingImpactLimitPerOwner: 1,
                impactDelayTicksPerCell: 24);

            Assert.That(settings.ResolveImpactDelayTicks(1), Is.EqualTo(24));
            Assert.That(settings.ResolveImpactDelayTicks(4), Is.EqualTo(96));
        }

        [Test]
        [Category("Core")]
        public void ForwardProjectile_AuthoritativeFacing_UsesLockedAttackDirection()
        {
            var projectileAction = new EnemyActionRuntimeState
            {
                kind = EnemyActionKind.ForwardCellProjectile,
                direction = Direction.Up,
                hasLockedForwardCellImpact = true,
                lockedAttackDirection = Direction.Right,
            };
            var meleeAction = new EnemyActionRuntimeState
            {
                kind = EnemyActionKind.Melee,
                direction = Direction.Up,
            };

            Assert.That(EnemyActionQueries.ResolveAuthoritativeFacing(projectileAction), Is.EqualTo(Direction.Right));
            Assert.That(EnemyActionQueries.ResolveAuthoritativeFacing(meleeAction), Is.EqualTo(Direction.Up));
        }

        private static WorldSnapshot CreateSnapshot()
        {
            return GameplayCompositionRoot.CreateWorldState(
                    new EntityState[0],
                    new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(4, 4)),
                    Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
        }
    }
}
