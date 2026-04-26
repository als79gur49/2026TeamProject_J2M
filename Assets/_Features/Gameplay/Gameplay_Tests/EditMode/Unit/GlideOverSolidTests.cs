using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GlideOverSolidTests
    {
        [Test]
        [Category("Extended")]
        public void GlideCapability_CompilesTimingsAndGuardsWrongTimingLane()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(durationTicks: 3, cooldownTicks: 2));

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(definition.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.GlideOverSolid));
                Assert.That(definition.GlideTimingSettings.DurationTicks, Is.EqualTo(3));
                Assert.That(definition.GlideTimingSettings.CooldownTicks, Is.EqualTo(2));
                Assert.That(definition.Capabilities.TryGetMovementSkill(out var movementSkill), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                {
                    _ = movementSkill.JumpTimingSettings;
                });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideTimingAuthoring_RejectsInvalidDurationAndCooldown()
        {
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(0f, 0f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(1f, -0.01f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

            var runtime = new EnemyGlideTimingAuthoringSettings(1f, 0f)
                .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(runtime.CooldownTicks, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_GlideStartsExpiresAndBlocksSameTickRestartWhenCooldownIsZero()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(durationTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 1);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var started), Is.True);
                Assert.That(started.IsActive, Is.True);
                Assert.That(started.ActiveUntilTickExclusive, Is.EqualTo(2));

                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var exited), Is.True);
                Assert.That(exited.IsActive, Is.False);
                Assert.That(exited.IsLandingPending, Is.False);
                Assert.That(exited.CooldownUntilTickExclusive, Is.EqualTo(2));
                Assert.That(exited.LastExitedTick, Is.EqualTo(2));

                CommitPreMovement(logic, worldState, tickIndex: 3);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var restarted), Is.True);
                Assert.That(restarted.IsActive, Is.True);
                Assert.That(restarted.Sequence, Is.EqualTo(2));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_LandingPendingPreservesCurrentOverlapAndCooldownStartsAfterClear()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(durationTicks: 1, cooldownTicks: 0));
            var solidCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(20, solidCell, BoxCapabilities.Push | BoxCapabilities.Flip),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 1, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(40, CreateActiveGlide(activeUntilTickExclusive: 2, durationTicks: 1, cooldownTicks: 0));
            writeContext.MoveEntity(40, solidCell);

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var pending), Is.True);
                Assert.That(pending.IsActive, Is.False);
                Assert.That(pending.IsLandingPending, Is.True);
                Assert.That(pending.CooldownUntilTickExclusive, Is.EqualTo(0));
                Assert.That(pending.LandingPendingCell, Is.EqualTo(solidCell));

                CommitPreMovement(logic, worldState, tickIndex: 3);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var stillPending), Is.True);
                Assert.That(stillPending.IsLandingPending, Is.True);

                worldState.CreateWriteContext().RemoveEntity(20);
                CommitPreMovement(logic, worldState, tickIndex: 4);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var cleared), Is.True);
                Assert.That(cleared.IsLandingPending, Is.False);
                Assert.That(cleared.IsActive, Is.False);
                Assert.That(cleared.CooldownUntilTickExclusive, Is.EqualTo(4));
                Assert.That(cleared.LastExitedTick, Is.EqualTo(4));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ActiveGlideDoesNotSuppressChaseMovementIntent()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(durationTicks: 3, cooldownTicks: 1));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);
            var movementIntents = new List<RawMovementIntent>();
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(2), movementIntents);

                Assert.That(movementIntents.Select(intent => intent.SourceId), Is.EquivalentTo(new[] { 40 }));
                Assert.That(movementIntents[0].Destination, Is.EqualTo(new Vector2Int(1, 0)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlide_IgnoresActiveGliderButNotLandingPendingEnemy()
        {
            var origin = new SurfaceCell(FaceId.Floor, 0, 0);
            var target = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(20, origin, BoxCapabilities.Push),
                CreateUnit(40, teamId: 2, target, EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));

            var activeResolved = worldState.CreateSnapshot().TryResolveNextSurfaceBoxSlideStep(
                origin,
                Vector2Int.right,
                out var activeDestination,
                out _);
            Assert.That(activeResolved, Is.True);
            Assert.That(activeDestination, Is.EqualTo(target));

            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                EnemyGlideRuntimeState.Create(
                    isActive: false,
                    isLandingPending: true,
                    sequence: 1,
                    activeUntilTickExclusive: 5,
                    cooldownUntilTickExclusive: 0,
                    durationTicks: 3,
                    cooldownTicks: 1,
                    lastExitedTick: 5,
                    landingPendingCell: target));

            var pendingResolved = worldState.CreateSnapshot().TryResolveNextSurfaceBoxSlideStep(
                origin,
                Vector2Int.right,
                out _,
                out var pendingStopper);
            Assert.That(pendingResolved, Is.False);
            Assert.That(pendingStopper.EntityId, Is.EqualTo(40));
        }

        [Test]
        [Category("Extended")]
        public void Legality_ActiveGlideBypassesOnlySolidBlockers()
        {
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var terrainCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var emptyCell = new SurfaceCell(FaceId.Floor, 0, -1);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateWall(30, wallCell),
                    CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
                },
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(1, 1)),
                new GameplayTerrainData(new[]
                {
                    new TerrainCellState(terrainCell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                }));
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(snapshot, EntityType.Unit, wallCell, 40).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(snapshot, EntityType.Unit, terrainCell, 40).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    snapshot,
                    EntityType.Unit,
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    40).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    snapshot,
                    EntityType.Unit,
                    emptyCell,
                    40,
                    ReservationStatus.Conflicted).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void MovementExpansion_FlipCanImpactActiveGliderSharingLandingCellWithSolid()
        {
            var landingCell = new SurfaceCell(FaceId.Floor, -1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.None),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip),
                CreateWall(30, landingCell),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.Chase),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(40, CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));
            writeContext.MoveEntity(40, landingCell);
            var flipIntent = new FlipIntent(10, priority: 50, destination: new Vector2Int(1, 0));
            flipIntent.AssignIntentId(1);
            var groups = new List<ActionGroup>();
            var rejected = new List<string>();

            new MovementExpander().Expand(
                worldState.CreateSnapshot(),
                new[] { flipIntent },
                groups,
                rejected);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].GroupKind, Is.EqualTo(ActionGroupKind.BoxImpact));
            Assert.That(groups[0].ImpactTargetId, Is.EqualTo(40));
        }

        private static void CommitPreMovement(EnemyLogic logic, WorldState worldState, int tickIndex)
        {
            ((IPreMovementStateLogic)logic).CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(tickIndex),
                worldState.CreateWriteContext(),
                new List<string>(),
                new List<PlayerActionTransition>());
        }

        private static EnemyGlideRuntimeState CreateActiveGlide(
            int activeUntilTickExclusive,
            int durationTicks,
            int cooldownTicks)
        {
            return EnemyGlideRuntimeState.Create(
                isActive: true,
                isLandingPending: false,
                sequence: 1,
                activeUntilTickExclusive: activeUntilTickExclusive,
                cooldownUntilTickExclusive: 0,
                durationTicks: durationTicks,
                cooldownTicks: cooldownTicks,
                lastExitedTick: 0,
                landingPendingCell: default);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(4, 4)),
                GameplayTerrainData.Empty);
        }

        private static EntityState CreateUnit(int entityId, int teamId, Vector2Int position, EnemyAiMode aiMode)
        {
            return CreateUnit(entityId, teamId, SurfaceCell.FromPlanar(position), aiMode);
        }

        private static EntityState CreateUnit(int entityId, int teamId, SurfaceCell position, EnemyAiMode aiMode)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = teamId == 1 ? UnitRole.Player : UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = capabilities,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
