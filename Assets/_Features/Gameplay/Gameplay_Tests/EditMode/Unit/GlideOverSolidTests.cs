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
                Assert.That(definition.GlideTimingSettings.WindupTicks, Is.EqualTo(0));
                Assert.That(definition.GlideTimingSettings.DurationTicks, Is.EqualTo(3));
                Assert.That(definition.GlideTimingSettings.RecoveryTicks, Is.EqualTo(0));
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
        public void GlideTimingAuthoring_CompilesPhaseTimingsAndRejectsInvalidDurations()
        {
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(0f, 0f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(-0.01f, 1f, 0f, 0f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(0f, 1f, -0.01f, 0f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(1f, -0.01f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

            var runtime = new EnemyGlideTimingAuthoringSettings(
                    windupSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    durationSeconds: 1f,
                    recoverySeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    cooldownSeconds: 0f)
                .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(runtime.WindupTicks, Is.EqualTo(1));
            Assert.That(runtime.DurationTicks, Is.EqualTo(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.That(runtime.RecoveryTicks, Is.EqualTo(2));
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
                Assert.That(started.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(started.IsActive, Is.True);
                Assert.That(started.ActiveUntilTickExclusive, Is.EqualTo(2));

                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var exited), Is.True);
                Assert.That(exited.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                Assert.That(exited.IsActive, Is.False);
                Assert.That(exited.IsLandingPending, Is.False);
                Assert.That(exited.CooldownUntilTickExclusive, Is.EqualTo(2));
                Assert.That(exited.LastExitedTick, Is.EqualTo(2));

                CommitPreMovement(logic, worldState, tickIndex: 3);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var restarted), Is.True);
                Assert.That(restarted.Phase, Is.EqualTo(EnemyGlidePhase.Active));
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
        public void EnemyLogic_GlideRunsFullNonZeroPhaseLifecycle()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 2, recoveryTicks: 1, cooldownTicks: 2));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 1);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var windup), Is.True);
                Assert.That(windup.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
                Assert.That(windup.WindupUntilTickExclusive, Is.EqualTo(2));

                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var active), Is.True);
                Assert.That(active.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(active.ActiveUntilTickExclusive, Is.EqualTo(4));

                CommitPreMovement(logic, worldState, tickIndex: 4);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var recovery), Is.True);
                Assert.That(recovery.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                Assert.That(recovery.RecoveryUntilTickExclusive, Is.EqualTo(5));

                CommitPreMovement(logic, worldState, tickIndex: 5);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var cooldown), Is.True);
                Assert.That(cooldown.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                Assert.That(cooldown.CooldownUntilTickExclusive, Is.EqualTo(7));
                Assert.That(cooldown.LastExitedTick, Is.EqualTo(5));

                CommitPreMovement(logic, worldState, tickIndex: 7);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var restarted), Is.True);
                Assert.That(restarted.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
                Assert.That(restarted.Sequence, Is.EqualTo(2));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_LandingPendingPreservesCurrentOverlapAndCooldownStartsAfterRecoveryClear()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 1, recoveryTicks: 1, cooldownTicks: 0));
            var solidCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(20, solidCell, BoxCapabilities.Push | BoxCapabilities.Flip),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 1, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(40, CreateActiveGlide(activeUntilTickExclusive: 2, durationTicks: 1, cooldownTicks: 0, recoveryTicks: 1));
            writeContext.MoveEntity(40, solidCell);

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var pending), Is.True);
                Assert.That(pending.IsActive, Is.False);
                Assert.That(pending.IsLandingPending, Is.True);
                Assert.That(pending.Phase, Is.EqualTo(EnemyGlidePhase.LandingPending));
                Assert.That(pending.CooldownUntilTickExclusive, Is.EqualTo(0));
                Assert.That(pending.LandingPendingCell, Is.EqualTo(solidCell));

                CommitPreMovement(logic, worldState, tickIndex: 3);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var stillPending), Is.True);
                Assert.That(stillPending.IsLandingPending, Is.True);

                worldState.CreateWriteContext().RemoveEntity(20);
                CommitPreMovement(logic, worldState, tickIndex: 4);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var recovery), Is.True);
                Assert.That(recovery.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                Assert.That(recovery.RecoveryUntilTickExclusive, Is.EqualTo(5));

                CommitPreMovement(logic, worldState, tickIndex: 5);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var cooldown), Is.True);
                Assert.That(cooldown.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                Assert.That(cooldown.CooldownUntilTickExclusive, Is.EqualTo(5));
                Assert.That(cooldown.LastExitedTick, Is.EqualTo(5));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ActiveGlideAndCooldownDoNotSuppressChaseMovementIntent()
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

                movementIntents.Clear();
                worldState.CreateWriteContext().SetEnemyGlideState(
                    40,
                    CreateGlideState(
                        EnemyGlidePhase.Cooldown,
                        cooldownUntilTickExclusive: 5,
                        durationTicks: 3,
                        cooldownTicks: 1,
                        lastExitedTick: 2));

                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(3), movementIntents);

                Assert.That(movementIntents.Select(intent => intent.SourceId), Is.EquivalentTo(new[] { 40 }));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_WindupLandingPendingAndRecoverySuppressGroundMovementIntent()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 3, recoveryTicks: 1, cooldownTicks: 1));
            var target = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);

            try
            {
                foreach (var glideState in new[]
                         {
                             CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5),
                             CreateGlideState(EnemyGlidePhase.LandingPending, landingPendingCell: target),
                             CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5),
                         })
                {
                    var movementIntents = new List<RawMovementIntent>();
                    worldState.CreateWriteContext().SetEnemyGlideState(40, glideState);

                    logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(2), movementIntents);

                    Assert.That(movementIntents, Is.Empty);
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlide_IgnoresOnlyActiveGlider()
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
                CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5));
            AssertBoxSlideBlockedBy(worldState, origin, 40);

            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateGlideState(EnemyGlidePhase.LandingPending, landingPendingCell: target));
            AssertBoxSlideBlockedBy(worldState, origin, 40);

            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5));
            AssertBoxSlideBlockedBy(worldState, origin, 40);
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

            foreach (var glideState in new[]
                     {
                         CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5),
                         CreateGlideState(EnemyGlidePhase.LandingPending, landingPendingCell: wallCell),
                         CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5),
                     })
            {
                worldState.CreateWriteContext().SetEnemyGlideState(40, glideState);
                Assert.That(
                    RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                        worldState.CreateSnapshot(),
                        EntityType.Unit,
                        wallCell,
                        40).Verdict,
                    Is.EqualTo(LegalityVerdict.Blocked));
            }
        }

        [Test]
        [Category("Extended")]
        public void MovementExpansion_FlipCanImpactActiveAndLandingPendingGliderSharingLandingCellWithSolid()
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

            AssertFlipCreatesImpactOnTarget(worldState, 40);

            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateGlideState(EnemyGlidePhase.LandingPending, activeUntilTickExclusive: 5, landingPendingCell: landingCell));

            AssertFlipCreatesImpactOnTarget(worldState, 40);
        }

        [Test]
        [Category("Extended")]
        public void MovementExpansion_FlipCanImpactNonActiveGlidePhasesWhenTargetCellHasNoSolid()
        {
            var landingCell = new SurfaceCell(FaceId.Floor, -1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.None),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip),
                CreateUnit(40, teamId: 2, landingCell, EnemyAiMode.Chase),
            });

            foreach (var glideState in new[]
                     {
                         CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5),
                         CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5),
                         CreateGlideState(EnemyGlidePhase.Cooldown, cooldownUntilTickExclusive: 8, lastExitedTick: 5),
                     })
            {
                worldState.CreateWriteContext().SetEnemyGlideState(40, glideState);

                AssertFlipCreatesImpactOnTarget(worldState, 40);
            }
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
            int cooldownTicks,
            int recoveryTicks = 0)
        {
            return CreateGlideState(
                EnemyGlidePhase.Active,
                activeUntilTickExclusive: activeUntilTickExclusive,
                durationTicks: durationTicks,
                recoveryTicks: recoveryTicks,
                cooldownTicks: cooldownTicks);
        }

        private static EnemyGlideRuntimeState CreateGlideState(
            EnemyGlidePhase phase,
            int sequence = 1,
            int windupUntilTickExclusive = 0,
            int activeUntilTickExclusive = 0,
            int recoveryUntilTickExclusive = 0,
            int cooldownUntilTickExclusive = 0,
            int windupTicks = 0,
            int durationTicks = 3,
            int recoveryTicks = 0,
            int cooldownTicks = 1,
            int lastExitedTick = 0,
            SurfaceCell landingPendingCell = default)
        {
            return EnemyGlideRuntimeState.Create(
                phase,
                sequence,
                windupUntilTickExclusive,
                activeUntilTickExclusive,
                recoveryUntilTickExclusive,
                cooldownUntilTickExclusive,
                windupTicks,
                durationTicks,
                recoveryTicks,
                cooldownTicks,
                lastExitedTick,
                landingPendingCell);
        }

        private static void AssertBoxSlideBlockedBy(WorldState worldState, SurfaceCell origin, int expectedBlockerEntityId)
        {
            var resolved = worldState.CreateSnapshot().TryResolveNextSurfaceBoxSlideStep(
                origin,
                Vector2Int.right,
                out _,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(stopper.EntityId, Is.EqualTo(expectedBlockerEntityId));
        }

        private static void AssertFlipCreatesImpactOnTarget(WorldState worldState, int expectedImpactTargetId)
        {
            var flipIntent = new FlipIntent(10, priority: 50, destination: new Vector2Int(1, 0));
            flipIntent.AssignIntentId(1);
            var groups = new List<ActionGroup>();
            var rejected = new List<string>();

            new MovementExpander().Expand(
                worldState.CreateSnapshot(),
                new[] { flipIntent },
                groups,
                rejected);

            Assert.That(groups, Has.Count.EqualTo(1), string.Join("\n", rejected));
            Assert.That(groups[0].GroupKind, Is.EqualTo(ActionGroupKind.BoxImpact));
            Assert.That(groups[0].ImpactTargetId, Is.EqualTo(expectedImpactTargetId));
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
