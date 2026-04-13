using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StageObjectiveSystemTests
    {
        private static readonly BoardBounds DefaultBoardBounds = new(new Vector2Int(0, 0), new Vector2Int(4, 4));

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_DuplicateZoneIdRejects()
        {
            var stage = CreateStage(
                "DuplicateZoneId",
                CreateBoard(),
                new[]
                {
                    CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
                    CreateZone("goal", FaceId.Front, CreateRegion(1, 1, 1, 1)),
                },
                CreateDisabledObjective(),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            AssertStageBuildThrows(stage, "duplicate zone id 'goal'");
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_OutOfBoundsRegionRejects()
        {
            var stage = CreateStage(
                "ZoneOutOfBounds",
                CreateBoard(),
                new[]
                {
                    CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 5, 0)),
                },
                CreateDisabledObjective(),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            AssertStageBuildThrows(stage, "contains out-of-bounds cell");
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_UnknownGoalZoneIdRejects()
        {
            var stage = CreateStage(
                "UnknownGoalZone",
                CreateBoard(),
                new[]
                {
                    CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
                },
                CreateActiveObjective(new[] { "missing" }),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            AssertStageBuildThrows(stage, "unknown goal zone id 'missing'");
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_EmptyZoneIdRejects()
        {
            var stage = CreateStage(
                "EmptyZoneId",
                CreateBoard(),
                new[]
                {
                    CreateZone(" ", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
                },
                CreateDisabledObjective(),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            AssertStageBuildThrows(stage, "must declare a non-empty zone id");
        }

        [Test]
        [Category("Extended")]
        public void StageZoneRuntimeDefinition_ContainsCell_FaceAware()
        {
            var zone = new StageZoneRuntimeDefinition(
                "goal",
                FaceId.Front,
                new[]
                {
                    new StageZoneRuntimeRegion(new Vector2Int(1, 1), new Vector2Int(2, 2)),
                });

            Assert.That(zone.Contains(new SurfaceCell(FaceId.Front, 1, 1)), Is.True);
            Assert.That(zone.Contains(new SurfaceCell(FaceId.Floor, 1, 1)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void StageZoneRuntimeDefinition_ContainsCell_AcrossMultipleRegions()
        {
            var zone = new StageZoneRuntimeDefinition(
                "goal",
                FaceId.Floor,
                new[]
                {
                    new StageZoneRuntimeRegion(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    new StageZoneRuntimeRegion(new Vector2Int(2, 2), new Vector2Int(3, 3)),
                });

            Assert.That(zone.Contains(new SurfaceCell(FaceId.Floor, 0, 0)), Is.True);
            Assert.That(zone.Contains(new SurfaceCell(FaceId.Floor, 3, 3)), Is.True);
            Assert.That(zone.Contains(new SurfaceCell(FaceId.Floor, 1, 1)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_PlayerOnGoal_WithNoConditions_Clears()
        {
            var objective = BuildObjectiveDefinition(
                goalZoneIds: new[] { "goal" },
                zones: new[]
                {
                    CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                });
            var tracker = objective.CreateTracker();

            var result = tracker.Advance(
                CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                StageObjectiveTickFacts.Empty);

            Assert.That(result.HasObjective, Is.True);
            Assert.That(result.GoalReached, Is.True);
            Assert.That(result.AllConditionsSatisfied, Is.True);
            Assert.That(result.ClearedThisTick, Is.True);
            Assert.That(result.IsCleared, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_PlayerNotOnGoal_DoesNotClear()
        {
            var objective = BuildObjectiveDefinition(
                goalZoneIds: new[] { "goal" },
                zones: new[]
                {
                    CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                });
            var tracker = objective.CreateTracker();

            var result = tracker.Advance(
                CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                StageObjectiveTickFacts.Empty);

            Assert.That(result.HasObjective, Is.True);
            Assert.That(result.GoalReached, Is.False);
            Assert.That(result.IsCleared, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_PlayerOnGoal_WithUnmetCondition_DoesNotClear()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityRemovedConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 99);
                var objective = BuildObjectiveDefinition(
                    goalZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: new StageConditionAsset[] { condition });
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                    StageObjectiveTickFacts.Empty);

                Assert.That(result.GoalReached, Is.True);
                Assert.That(result.AllConditionsSatisfied, Is.False);
                Assert.That(result.IsCleared, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_LevelTrigger_ClearsWhenLastConditionBecomesSatisfiedWhileAlreadyOnGoal()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityRemovedConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 99);
                var objective = BuildObjectiveDefinition(
                    goalZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: new StageConditionAsset[] { condition });
                var tracker = objective.CreateTracker();
                var goalSnapshot = CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)));

                var firstTick = tracker.Advance(goalSnapshot, StageObjectiveTickFacts.Empty);
                var secondTick = tracker.Advance(
                    goalSnapshot,
                    new StageObjectiveTickFacts(
                        2,
                        PlayerTickCommand.None,
                        new[] { 99 },
                        Array.Empty<DamageResolutionRecord>()));

                Assert.That(firstTick.IsCleared, Is.False);
                Assert.That(secondTick.GoalReached, Is.True);
                Assert.That(secondTick.AllConditionsSatisfied, Is.True);
                Assert.That(secondTick.ClearedThisTick, Is.True);
                Assert.That(secondTick.IsCleared, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_StickyClear_RemainsClearedAfterGoalIsLost()
        {
            var objective = BuildObjectiveDefinition(
                goalZoneIds: new[] { "goal" },
                zones: new[]
                {
                    CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                });
            var tracker = objective.CreateTracker();

            tracker.Advance(
                CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                StageObjectiveTickFacts.Empty);

            var result = tracker.Advance(
                CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                StageObjectiveTickFacts.Empty);

            Assert.That(result.GoalReached, Is.False);
            Assert.That(result.ClearedThisTick, Is.False);
            Assert.That(result.IsCleared, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Conditions_AllEnemiesDefeated_TracksSnapshotState()
        {
            var conditionAsset = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();

            try
            {
                var runtime = BuildSingleConditionRuntime(conditionAsset);

                runtime.Advance(
                    CreateSnapshot(
                        CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 1, 0))),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Extended")]
        public void Conditions_SpecificEntityRemoved_BecomesStickyAfterCleanupFact()
        {
            var conditionAsset = ScriptableObject.CreateInstance<SpecificEntityRemovedConditionAsset>();

            try
            {
                SetPrivateField(conditionAsset, "entityId", 42);
                var runtime = BuildSingleConditionRuntime(conditionAsset);

                runtime.Advance(CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))), StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                    new StageObjectiveTickFacts(
                        2,
                        PlayerTickCommand.None,
                        new[] { 42 },
                        Array.Empty<DamageResolutionRecord>()));
                Assert.That(runtime.IsSatisfied, Is.True);

                runtime.Advance(CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))), StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Extended")]
        public void Conditions_VisitZoneSequence_DoesNotDoubleCountContinuousStay()
        {
            var conditionAsset = ScriptableObject.CreateInstance<VisitZoneSequenceConditionAsset>();

            try
            {
                SetPrivateField(conditionAsset, "zoneIds", new[] { "checkpoint", "checkpoint" });
                var runtime = BuildSingleConditionRuntime(
                    conditionAsset,
                    zones: new[]
                    {
                        CreateZone("checkpoint", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    });

                var checkpointSnapshot = CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)));
                runtime.Advance(checkpointSnapshot, StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(checkpointSnapshot, StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                    StageObjectiveTickFacts.Empty);
                runtime.Advance(checkpointSnapshot, StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_ExposesObjectiveResultInTickResult()
        {
            var objective = CreateSimpleObjectiveDefinition(new SurfaceCell(FaceId.Floor, 1, 1));
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                },
                DefaultBoardBounds,
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                objectiveDefinition: objective);

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.ObjectiveResult.HasObjective, Is.True);
            Assert.That(result.ObjectiveResult.IsCleared, Is.True);
            Assert.That(result.ObjectiveResult.ClearedThisTick, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_DeterminismHash_ChangesWhenObjectiveStateChanges()
        {
            var clearObjective = CreateSimpleObjectiveDefinition(new SurfaceCell(FaceId.Floor, 1, 1));
            var unclearedObjective = CreateSimpleObjectiveDefinition(new SurfaceCell(FaceId.Floor, 2, 2));
            var timingProfile = GameplayTimingProfile.CreateDefault();

            var clearedResult = RunSingleTickWithObjective(
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                clearObjective,
                timingProfile);
            var unclearedResult = RunSingleTickWithObjective(
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                unclearedObjective,
                timingProfile);

            Assert.That(clearedResult.DeterminismHash, Is.Not.EqualTo(unclearedResult.DeterminismHash));
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ExistingAuthoringWithoutObjective_RemainsCompatible()
        {
            var stage = CreateStage(
                "LegacyCompatible",
                CreateBoard(),
                Array.Empty<StageZoneDefinition>(),
                StageObjectiveAuthoring.CreateDefault(),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.PlayerEntityId, Is.EqualTo(10));
                Assert.That(buildResult.ObjectiveRuntimeDefinition.HasObjective, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static TickResult RunSingleTickWithObjective(
            EntityState player,
            StageObjectiveRuntimeDefinition objectiveDefinition,
            GameplayTimingProfile timingProfile)
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { player },
                DefaultBoardBounds,
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                objectiveDefinition: objectiveDefinition);
            return pipeline.RunTick(new TickInput(1));
        }

        private static StageObjectiveRuntimeDefinition CreateSimpleObjectiveDefinition(SurfaceCell goalCell)
        {
            var goalZone = new StageZoneRuntimeDefinition(
                "goal",
                goalCell.face,
                new[]
                {
                    new StageZoneRuntimeRegion(goalCell.PlanarPosition, goalCell.PlanarPosition),
                });

            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                10,
                new[] { goalZone },
                new[] { goalZone },
                Array.Empty<StageConditionRuntimeDefinition>());
        }

        private static IStageConditionRuntime BuildSingleConditionRuntime(
            StageConditionAsset conditionAsset,
            StageZoneDefinition[] zones = null)
        {
            var effectiveZones = zones ?? new[]
            {
                CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
            };
            var objective = BuildObjectiveDefinition(
                goalZoneIds: new[] { effectiveZones[0].ZoneId },
                zones: effectiveZones,
                requiredConditions: new[] { conditionAsset });

            return objective.RequiredConditions[0].CreateRuntime();
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            string[] goalZoneIds,
            StageZoneDefinition[] zones,
            StageConditionAsset[] requiredConditions = null)
        {
            var stage = CreateStage(
                "ObjectiveStage",
                CreateBoard(),
                zones,
                CreateActiveObjective(goalZoneIds, requiredConditions),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            try
            {
                return StageRuntimeBuilder.Build(stage).ObjectiveRuntimeDefinition;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static StageDefinition CreateStage(
            string stageName,
            StageBoardDefinition board,
            StageZoneDefinition[] zones,
            StageObjectiveAuthoring objective,
            params StageSpawnDefinition[] spawns)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.name = stageName;
            SetPrivateField(stage, "board", board);
            SetPrivateField(stage, "playerSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Player));
            SetPrivateField(stage, "boxSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Box));
            SetPrivateField(stage, "enemySpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Enemy));
            SetPrivateField(stage, "wallSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Wall));
            SetPrivateField(stage, "zones", zones ?? Array.Empty<StageZoneDefinition>());
            SetPrivateField(stage, "objective", objective);
            return stage;
        }

        private static StageBoardDefinition CreateBoard()
        {
            return new StageBoardDefinition
            {
                MinInclusive = DefaultBoardBounds.MinInclusive,
                MaxInclusive = DefaultBoardBounds.MaxInclusive,
                InitialBottomFace = FaceId.Floor,
            };
        }

        private static StageObjectiveAuthoring CreateDisabledObjective()
        {
            return StageObjectiveAuthoring.CreateDefault();
        }

        private static StageObjectiveAuthoring CreateActiveObjective(
            string[] goalZoneIds,
            StageConditionAsset[] requiredConditions = null)
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                GoalZoneIds = goalZoneIds ?? Array.Empty<string>(),
                RequiredConditions = requiredConditions ?? Array.Empty<StageConditionAsset>(),
            };
        }

        private static StageZoneDefinition CreateZone(
            string zoneId,
            FaceId faceId,
            params StageZoneRegionDefinition[] regions)
        {
            return new StageZoneDefinition
            {
                ZoneId = zoneId,
                FaceId = faceId,
                Regions = regions ?? Array.Empty<StageZoneRegionDefinition>(),
            };
        }

        private static StageZoneRegionDefinition CreateRegion(int minX, int minY, int maxX, int maxY)
        {
            return new StageZoneRegionDefinition
            {
                MinInclusive = new Vector2Int(minX, minY),
                MaxInclusive = new Vector2Int(maxX, maxY),
            };
        }

        private static StageSpawnDefinition CreatePlayerSpawn(int entityId, SurfaceCell cell)
        {
            return new StageSpawnDefinition
            {
                EntityId = entityId,
                Kind = StageSpawnKind.Player,
                Cell = cell,
                Facing = Direction.Right,
                Hp = 3,
            };
        }

        private static StageSpawnDefinition[] FilterSpawnsByKind(
            StageSpawnDefinition[] spawns,
            StageSpawnKind kind)
        {
            if (spawns == null || spawns.Length == 0)
            {
                return Array.Empty<StageSpawnDefinition>();
            }

            var filtered = new List<StageSpawnDefinition>();
            for (var i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].Kind == kind)
                {
                    filtered.Add(spawns[i]);
                }
            }

            return filtered.ToArray();
        }

        private static WorldSnapshot CreateSnapshot(params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                DefaultBoardBounds,
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault()).CreateSnapshot();
        }

        private static EntityState CreatePlayerEntity(int entityId, SurfaceCell cell)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemyEntity(int entityId, SurfaceCell cell)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 1,
                maxHp = 1,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(
            GameplayTimingProfile timingProfile)
        {
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static void AssertStageBuildThrows(StageDefinition stage, string expectedMessage)
        {
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(stage));
                StringAssert.Contains(expectedMessage, exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
