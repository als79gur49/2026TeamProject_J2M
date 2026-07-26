using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
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
        public void StageDefinitionValidation_PrimaryGoalUnknownZoneRejects()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "zoneIds", new[] { "missing" });
                var stage = CreateStage(
                    "UnknownPrimaryGoalZone",
                    CreateBoard(),
                    new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
                    },
                    CreateObjective(
                        StageCompletionPolicy.RequireAllConditions,
                        new[]
                        {
                            CreateConditionEntry(condition, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary"),
                        }),
                    CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                AssertStageBuildThrows(stage, "references unknown zone id 'missing'");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
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
                primaryZoneIds: new[] { "goal" },
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
                primaryZoneIds: new[] { "goal" },
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
                    primaryZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredAssets: new StageConditionAsset[] { condition });
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                    StageObjectiveTickFacts.Empty);

                Assert.That(result.GoalReached, Is.False);
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
        public void ObjectiveClear_PlayerOnGoal_WithSatisfiedCondition_Clears()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityRemovedConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 99);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredAssets: new StageConditionAsset[] { condition });
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                    new StageObjectiveTickFacts(
                        1,
                        PlayerTickCommand.None,
                        new[] { 99 },
                        Array.Empty<StageObjectiveDamageFact>()));

                Assert.That(result.GoalReached, Is.True);
                Assert.That(result.AllConditionsSatisfied, Is.True);
                Assert.That(result.ClearedThisTick, Is.True);
                Assert.That(result.IsCleared, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_PlayerNotOnGoal_WithSatisfiedCondition_DoesNotClear()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityRemovedConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 99);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredAssets: new StageConditionAsset[] { condition });
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                    new StageObjectiveTickFacts(
                        1,
                        PlayerTickCommand.None,
                        new[] { 99 },
                        Array.Empty<StageObjectiveDamageFact>()));

                Assert.That(result.GoalReached, Is.False);
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
                    primaryZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredAssets: new StageConditionAsset[] { condition });
                var tracker = objective.CreateTracker();
                var goalSnapshot = CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)));

                var firstTick = tracker.Advance(goalSnapshot, StageObjectiveTickFacts.Empty);
                var secondTick = tracker.Advance(
                    goalSnapshot,
                    new StageObjectiveTickFacts(
                        2,
                        PlayerTickCommand.None,
                        new[] { 99 },
                        Array.Empty<StageObjectiveDamageFact>()));

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
                primaryZoneIds: new[] { "goal" },
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
        public void ObjectiveClear_RequiredCondition_ClearsWithoutPrimaryGoal()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 20);
                SetPrivateField(condition, "zoneId", "target");
                SetPrivateField(condition, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredAssets: new StageConditionAsset[] { condition },
                    completionPolicy: StageCompletionPolicy.RequireAllConditions);
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 1, 1))),
                    new StageObjectiveTickFacts(
                        1,
                        PlayerTickCommand.None,
                        Array.Empty<int>(),
                        Array.Empty<StageObjectiveDamageFact>()));

                Assert.That(result.HasObjective, Is.True);
                Assert.That(result.GoalReached, Is.False);
                Assert.That(result.AllConditionsSatisfied, Is.True);
                Assert.That(result.ClearedThisTick, Is.True);
                Assert.That(result.IsCleared, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_UnmetRequiredConditionDoesNotClear()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 20);
                SetPrivateField(condition, "zoneId", "target");
                SetPrivateField(condition, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredAssets: new StageConditionAsset[] { condition },
                    completionPolicy: StageCompletionPolicy.RequireAllConditions);
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 0, 0))),
                    new StageObjectiveTickFacts(
                        1,
                        PlayerTickCommand.None,
                        Array.Empty<int>(),
                        Array.Empty<StageObjectiveDamageFact>()));

                Assert.That(result.HasObjective, Is.True);
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
        public void ObjectiveClear_OptionalPrimaryGoal_DoesNotStrengthenClear()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 20);
                SetPrivateField(condition, "zoneId", "target");
                SetPrivateField(condition, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(2, 2, 2, 2)),
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredAssets: new StageConditionAsset[] { condition },
                    completionPolicy: StageCompletionPolicy.RequireAllConditions,
                    primaryRequired: false);
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(
                        CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 1, 1))),
                    CreateObjectiveTickFacts(1));

                Assert.That(result.GoalReached, Is.False);
                Assert.That(result.AllConditionsSatisfied, Is.True);
                Assert.That(result.ClearedThisTick, Is.True);
                Assert.That(result.IsCleared, Is.True);
                Assert.That(result.ConditionStatuses.Any(status =>
                    status.Role == StageObjectiveConditionRole.PrimaryGoal &&
                    status.Required == false), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_ExplicitPrimaryGoal_ClearsWithOtherConditions()
        {
            var primaryGoal = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            var allEnemiesDefeated = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();

            try
            {
                SetPrivateField(primaryGoal, "zoneIds", new[] { "goal" });
                SetPrivateField(primaryGoal, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    completionPolicy: StageCompletionPolicy.RequireAllConditions,
                    timing: StageSimulationTiming.Default,
                    conditionEntries: new[]
                    {
                        CreateConditionEntry(primaryGoal, required: true, StageObjectiveConditionRole.PrimaryGoal, "explicit-primary"),
                        CreateConditionEntry(allEnemiesDefeated, required: true, StageObjectiveConditionRole.None, "all-enemies"),
                    });

                var blocked = objective.CreateTracker().Advance(
                    CreateSnapshot(
                        CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                        CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 0, 0))),
                    CreateObjectiveTickFacts(1));
                Assert.That(blocked.GoalReached, Is.False);
                Assert.That(blocked.AllConditionsSatisfied, Is.False);
                Assert.That(blocked.IsCleared, Is.False);

                var cleared = objective.CreateTracker().Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                    CreateObjectiveTickFacts(1));
                Assert.That(cleared.GoalReached, Is.True);
                Assert.That(cleared.AllConditionsSatisfied, Is.True);
                Assert.That(cleared.ClearedThisTick, Is.True);
                Assert.That(cleared.IsCleared, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primaryGoal);
                UnityEngine.Object.DestroyImmediate(allEnemiesDefeated);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_PrimaryGoalConditionStatus_UsesStableId()
        {
            var explicitPrimary = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(explicitPrimary, "zoneIds", new[] { "goal" });
                SetPrivateField(explicitPrimary, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    completionPolicy: StageCompletionPolicy.RequireAllConditions,
                    timing: StageSimulationTiming.Default,
                    conditionEntries: new[]
                    {
                        CreateConditionEntry(explicitPrimary, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary-goal"),
                    });
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                    CreateObjectiveTickFacts(1));

                Assert.That(result.GoalReached, Is.True);
                Assert.That(result.IsCleared, Is.True);
                Assert.That(
                    result.ConditionStatuses.Count(status => status.Role == StageObjectiveConditionRole.PrimaryGoal),
                    Is.EqualTo(1));
                Assert.That(
                    result.ConditionStatuses.Single(status => status.Role == StageObjectiveConditionRole.PrimaryGoal).ConditionId,
                    Is.EqualTo("primary-goal"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(explicitPrimary);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveConditionStatuses_UseDeterministicEntryOrder()
        {
            var explicitRequired = ScriptableObject.CreateInstance<SpecificEntityRemovedConditionAsset>();
            var explicitOptional = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var primaryGoal = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(explicitRequired, "entityId", 99);
                SetPrivateField(primaryGoal, "zoneIds", new[] { "goal" });
                SetPrivateField(primaryGoal, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    completionPolicy: StageCompletionPolicy.RequireAllConditions,
                    timing: StageSimulationTiming.Default,
                    conditionEntries: new[]
                    {
                        CreateConditionEntry(explicitRequired, required: true, StageObjectiveConditionRole.None, "remove-99"),
                        CreateConditionEntry(explicitOptional, required: false, StageObjectiveConditionRole.Challenge, "explicit-optional"),
                        CreateConditionEntry(primaryGoal, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary-goal"),
                    });

                var result = objective.CreateTracker().Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                    CreateObjectiveTickFacts(1));

                CollectionAssert.AreEqual(
                    new[] { "remove-99", "explicit-optional", "primary-goal" },
                    result.ConditionStatuses.Select(status => status.ConditionId).ToArray());
                Assert.That(
                    result.ConditionStatuses.Count(status => status.Role == StageObjectiveConditionRole.PrimaryGoal),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(explicitRequired);
                UnityEngine.Object.DestroyImmediate(explicitOptional);
                UnityEngine.Object.DestroyImmediate(primaryGoal);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_RequireAllConditions_RequiresCondition()
        {
            var stage = CreateStage(
                "RequireAllConditionsEmpty",
                CreateBoard(),
                Array.Empty<StageZoneDefinition>(),
                CreateObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    Array.Empty<StageObjectiveConditionEntry>()),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            AssertStageBuildThrows(stage, "requires at least one required condition");
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_RequireAllConditions_TimeLimitOnlyRejects()
        {
            var condition = ScriptableObject.CreateInstance<ClearWithinTimeLimitConditionAsset>();

            try
            {
                SetPrivateField(condition, "clearBeforeOrAtSeconds", 10f);
                var stage = CreateStage(
                    "RequireAllConditionsTimeOnly",
                    CreateBoard(),
                    Array.Empty<StageZoneDefinition>(),
                    CreateObjective(
                        StageCompletionPolicy.RequireAllConditions,
                        new[]
                        {
                            CreateConditionEntry(condition, required: true, StageObjectiveConditionRole.None, "time-limit"),
                        }),
                    CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                AssertStageBuildThrows(stage, "cannot use only time limit conditions");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_SpecificEntityAtZone_UnknownZoneRejects()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 20);
                SetPrivateField(condition, "zoneId", "missing");
                var stage = CreateStage(
                    "SpecificEntityAtZoneMissingZone",
                    CreateBoard(),
                    new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    CreateObjective(
                        StageCompletionPolicy.RequireAllConditions,
                        new[]
                        {
                            CreateConditionEntry(condition, required: true, StageObjectiveConditionRole.None, "entity-at-zone"),
                        }),
                    CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                AssertStageBuildThrows(stage, "references unknown zone id 'missing'");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_PlayerAtAnyZone_UnknownZoneRejects()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "zoneIds", new[] { "missing" });
                var stage = CreateStage(
                    "PlayerAtAnyZoneMissingZone",
                    CreateBoard(),
                    new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    CreateObjective(
                        StageCompletionPolicy.RequireAllConditions,
                        new[]
                        {
                            CreateConditionEntry(condition, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary"),
                        }),
                    CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                AssertStageBuildThrows(stage, "references unknown zone id 'missing'");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_PlayerAtAnyZone_EmptyZoneListRejects()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "zoneIds", Array.Empty<string>());
                var stage = CreateStage(
                    "PlayerAtAnyZoneEmptyZones",
                    CreateBoard(),
                    new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    CreateObjective(
                        StageCompletionPolicy.RequireAllConditions,
                        new[]
                        {
                            CreateConditionEntry(condition, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary"),
                        }),
                    CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                AssertStageBuildThrows(stage, "requires at least one zone id");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Core")]
        public void StageDefinitionValidation_PlayerAtAnyZone_DuplicateZoneIdsRejects()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "zoneIds", new[] { "goal", " goal " });
                var stage = CreateStage(
                    "PlayerAtAnyZoneDuplicateZones",
                    CreateBoard(),
                    new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    CreateObjective(
                        StageCompletionPolicy.RequireAllConditions,
                        new[]
                        {
                            CreateConditionEntry(condition, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary"),
                        }),
                    CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                AssertStageBuildThrows(stage, "contains duplicate zone id 'goal'");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageDefinitionValidation_MultiplePrimaryGoalEntriesRejects()
        {
            var first = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            var second = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(first, "zoneIds", new[] { "goal" });
                SetPrivateField(second, "zoneIds", new[] { "goal" });
                var stage = CreateStage(
                    "MultiplePrimaryGoals",
                    CreateBoard(),
                    new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    CreateObjective(
                        StageCompletionPolicy.RequireAllConditions,
                        new[]
                        {
                            CreateConditionEntry(first, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary-a"),
                            CreateConditionEntry(second, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary-b"),
                        }),
                    CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                AssertStageBuildThrows(stage, "more than one PrimaryGoal");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageObjectiveArchitecture_RemovedGoalObjectiveSymbols_DoNotRemainUnderAssets()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var assetsRoot = Path.Combine(repoRoot, "Assets");
            var removedSymbols = new[]
            {
                string.Concat("Goal", "Zone", "Ids"),
                string.Concat("goal", "Zone", "Ids"),
                string.Concat("Goal", "Zones"),
                string.Concat("Is", "Player", "On", "Goal"),
                string.Concat("Require", "Player", "On", "Goal", "With", "All", "Conditions"),
                string.Concat("legacy", "-", "primary", "-", "goal"),
            };
            var hits = new List<string>();

            foreach (var path in Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories))
            {
                if (!ShouldScanForRemovedObjectiveSymbols(path))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
                var source = File.ReadAllText(path);
                for (var i = 0; i < removedSymbols.Length; i++)
                {
                    if (source.Contains(removedSymbols[i], StringComparison.Ordinal))
                    {
                        hits.Add($"{relativePath}: {removedSymbols[i]}");
                    }
                }
            }

            Assert.That(hits, Is.Empty);
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
                        Array.Empty<StageObjectiveDamageFact>()));
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
        [Category("Core")]
        public void Conditions_PlayerAtAnyZone_TracksCurrentFaceAwareOccupancy()
        {
            var conditionAsset = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(conditionAsset, "zoneIds", new[] { "target" });
                SetPrivateField(conditionAsset, "requireAlive", true);
                var runtime = BuildSingleConditionRuntime(
                    conditionAsset,
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Front, CreateRegion(1, 1, 1, 1)),
                    });

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Front, 1, 1))),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.True);

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Front, 0, 0))),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Extended")]
        public void Conditions_PlayerAtAnyZone_RequireAliveRejectsDeadMarkedOrNonOccupyingPlayer()
        {
            var conditionAsset = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(conditionAsset, "zoneIds", new[] { "target" });
                SetPrivateField(conditionAsset, "requireAlive", true);
                var runtime = BuildSingleConditionRuntime(
                    conditionAsset,
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    });

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1), hp: 0)),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(
                        10,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        markedForDeath: true)),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(
                    CreateSnapshot(CreatePlayerEntity(
                        10,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        boardPresence: EntityBoardPresence.Detached)),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ExitActive_PlayerOnCenter_WithPrerequisitesComplete_ClearsSameTick()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var objective = CreateExitObjectiveDefinition(exitCell, includePrerequisite: true, prerequisiteSatisfied: true);
            var tracker = objective.CreateTracker();

            var result = tracker.Advance(
                CreateExitSnapshot(exitCell, new CubeTopologyState(FaceId.Floor), CreatePlayerEntity(10, exitCell)),
                CreateObjectiveTickFacts(1));

            Assert.That(result.GoalReached, Is.True);
            Assert.That(result.AllConditionsSatisfied, Is.True);
            Assert.That(result.ClearedThisTick, Is.True);
            Assert.That(result.IsCleared, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ExitActive_PlayerOnCenter_WithPrerequisitesIncomplete_DoesNotClear()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var objective = CreateExitObjectiveDefinition(exitCell, includePrerequisite: true, prerequisiteSatisfied: false);
            var tracker = objective.CreateTracker();

            var result = tracker.Advance(
                CreateExitSnapshot(exitCell, new CubeTopologyState(FaceId.Floor), CreatePlayerEntity(10, exitCell)),
                CreateObjectiveTickFacts(1));

            Assert.That(result.GoalReached, Is.False);
            Assert.That(result.AllConditionsSatisfied, Is.False);
            Assert.That(result.ClearedThisTick, Is.False);
            Assert.That(result.IsCleared, Is.False);
            var primaryGoalStatus = result.ConditionStatuses.Single(status =>
                status.Role == StageObjectiveConditionRole.PrimaryGoal);
            Assert.That(primaryGoalStatus.IsSatisfied, Is.False);
            Assert.That(primaryGoalStatus.Details, Does.Contain("LockedByRequiredNonPrimary=1"));
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ExitActive_PlayerOffCenter_DoesNotClear()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var objective = CreateExitObjectiveDefinition(exitCell, includePrerequisite: true, prerequisiteSatisfied: true);
            var tracker = objective.CreateTracker();

            var result = tracker.Advance(
                CreateExitSnapshot(
                    exitCell,
                    new CubeTopologyState(FaceId.Floor),
                    CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                CreateObjectiveTickFacts(1));

            Assert.That(result.GoalReached, Is.False);
            Assert.That(result.ClearedThisTick, Is.False);
            Assert.That(result.IsCleared, Is.False);
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ExitInactiveTopology_BlocksUntilExitBecomesBottomFace()
        {
            var exitCell = new SurfaceCell(FaceId.Front, 1, 1);
            var objective = CreateExitObjectiveDefinition(exitCell, includePrerequisite: false, prerequisiteSatisfied: true);
            var tracker = objective.CreateTracker();

            var inactive = tracker.Advance(
                CreateExitSnapshot(exitCell, new CubeTopologyState(FaceId.Floor), CreatePlayerEntity(10, exitCell)),
                CreateObjectiveTickFacts(1));
            Assert.That(inactive.GoalReached, Is.False);
            Assert.That(inactive.IsCleared, Is.False);

            var active = tracker.Advance(
                CreateExitSnapshot(exitCell, new CubeTopologyState(FaceId.Front), CreatePlayerEntity(10, exitCell)),
                CreateObjectiveTickFacts(2));
            Assert.That(active.GoalReached, Is.True);
            Assert.That(active.ClearedThisTick, Is.True);
            Assert.That(active.IsCleared, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ExitCenterNonPlayerOccupants_DoNotTriggerClearOrDestroyBox()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var objective = CreateExitObjectiveDefinition(exitCell, includePrerequisite: false, prerequisiteSatisfied: true);
            var tracker = objective.CreateTracker();

            var enemyResult = tracker.Advance(
                CreateExitSnapshot(
                    exitCell,
                    new CubeTopologyState(FaceId.Floor),
                    CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateEnemyEntity(20, exitCell)),
                CreateObjectiveTickFacts(1));
            Assert.That(enemyResult.IsCleared, Is.False);

            var boxResult = tracker.Advance(
                CreateExitSnapshot(
                    exitCell,
                    new CubeTopologyState(FaceId.Floor),
                    CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBoxEntity(30, exitCell, BoxCapabilities.Push, BoxArchetype.Normal)),
                CreateObjectiveTickFacts(2));
            Assert.That(boxResult.IsCleared, Is.False);

            var moonBlockSnapshot = CreateExitSnapshot(
                exitCell,
                new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBoxEntity(
                    40,
                    exitCell,
                    BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                    BoxArchetype.Moon));
            var moonBlockResult = tracker.Advance(moonBlockSnapshot, CreateObjectiveTickFacts(3));
            Assert.That(moonBlockResult.IsCleared, Is.False);
            Assert.That(moonBlockSnapshot.TryGetEntity(40, out var moonBlock), Is.True);
            Assert.That(moonBlock.hp, Is.EqualTo(1));

            Assert.Throws<InvalidOperationException>(() =>
                CreateExitSnapshot(
                    exitCell,
                    new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, exitCell),
                CreateBoxEntity(41, exitCell, BoxCapabilities.Push)));
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ExitActive_ReplayIsDeterministicAndDoesNotMutateExitFlags()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var firstCondition = CreatePlayerAtAnyZoneCondition("goal");
            var secondCondition = CreatePlayerAtAnyZoneCondition("goal");

            try
            {
                var tileFeature = CreateStageTileFeature(
                    100,
                    exitCell,
                    TileFeatureKind.Exit,
                    TileFeatureActivationRule.ActiveFaceOnly,
                    TileFeatureBoxSelector.None);
                var firstBuild = BuildExitObjectiveStage(firstCondition, tileFeature);
                var secondBuild = BuildExitObjectiveStage(secondCondition, tileFeature);
                var firstWorld = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreatePlayerEntity(10, exitCell) },
                    firstBuild.BoardBounds,
                    firstBuild.InitialTopology,
                    firstBuild.InitialTileFeatures);
                var secondWorld = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreatePlayerEntity(10, exitCell) },
                    secondBuild.BoardBounds,
                    secondBuild.InitialTopology,
                    secondBuild.InitialTileFeatures);

                var firstResult = CreatePipeline(
                        firstWorld,
                        firstBuild.ObjectiveRuntimeDefinition,
                        firstBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(7));
                var secondResult = CreatePipeline(
                        secondWorld,
                        secondBuild.ObjectiveRuntimeDefinition,
                        secondBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(7));

                Assert.That(firstResult.ObjectiveResult.ClearedThisTick, Is.True);
                Assert.That(firstResult.ObjectiveResult.IsCleared, Is.EqualTo(secondResult.ObjectiveResult.IsCleared));
                Assert.That(firstResult.ObjectiveResult.AllConditionsSatisfied, Is.EqualTo(secondResult.ObjectiveResult.AllConditionsSatisfied));
                Assert.That(firstResult.DeterminismHash, Is.EqualTo(secondResult.DeterminismHash));
                Assert.That(firstWorld.CreateSnapshot().TryGetTileFeature(100, out var firstExit), Is.True);
                Assert.That(secondWorld.CreateSnapshot().TryGetTileFeature(100, out var secondExit), Is.True);
                Assert.That(firstExit.Flags, Is.EqualTo(secondExit.Flags));
                Assert.That(
                    firstExit.Flags & TileFeatureFlags.Activated,
                    Is.EqualTo(TileFeatureFlags.None));
                Assert.That(
                    firstResult.PresentationData.TileEvents.Count(tileEvent =>
                        tileEvent.EventKind == TilePresentationEventKind.ExitEntered),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstCondition);
                UnityEngine.Object.DestroyImmediate(secondCondition);
            }
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_OpenedEmitsWhenRequiredNonPrimaryCompletesAndExitIsActive()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var objective = CreateExitObjectiveDefinitionWithTickPrerequisite(exitCell, prerequisiteSatisfiedTick: 8);
            var tileFeatureDefinitions = CreateExitTileFeatureDefinitions();
            var worldState = CreateExitWorldState(
                exitCell,
                new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)));
            var pipeline = CreatePipeline(worldState, objective, tileFeatureDefinitions);

            var incompleteResult = pipeline.RunTick(new TickInput(7));
            var openedResult = pipeline.RunTick(new TickInput(8));
            var laterResult = pipeline.RunTick(new TickInput(9));

            Assert.That(incompleteResult.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitOpened), Is.False);
            var exitOpened = openedResult.PresentationData.TileEvents.Single(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitOpened);
            Assert.That(exitOpened.TileId, Is.EqualTo(100));
            Assert.That(exitOpened.Cell, Is.EqualTo(exitCell));
            Assert.That(exitOpened.TileFeatureKind, Is.EqualTo(TileFeatureKind.Exit));
            Assert.That(exitOpened.TargetEntityId, Is.Zero);
            Assert.That(exitOpened.Direction, Is.EqualTo(Direction.None));
            Assert.That(openedResult.ObjectiveResult.HasRequiredNonPrimaryConditions, Is.True);
            Assert.That(openedResult.ObjectiveResult.RequiredNonPrimaryConditionsSatisfied, Is.True);
            Assert.That(openedResult.ObjectiveResult.RequiredNonPrimaryConditionsSatisfiedThisTick, Is.True);
            Assert.That(laterResult.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitOpened), Is.False);
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_ActiveFaceOnlyFrontFace_OpenedEmitsAndOpenStateIsActive()
        {
            var exitCell = new SurfaceCell(FaceId.Front, 1, 1);
            var objective = CreateExitObjectiveDefinitionWithTickPrerequisite(
                exitCell,
                prerequisiteSatisfiedTick: 8,
                activationRule: TileFeatureActivationRule.ActiveFaceOnly);
            var tileFeatureDefinitions = CreateExitTileFeatureDefinitions(TileFeatureActivationRule.ActiveFaceOnly);
            var worldState = CreateExitWorldState(
                exitCell,
                new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)));
            var pipeline = CreatePipeline(worldState, objective, tileFeatureDefinitions);

            var incompleteResult = pipeline.RunTick(new TickInput(7));
            var openedResult = pipeline.RunTick(new TickInput(8));

            Assert.That(incompleteResult.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitOpened), Is.False);
            var exitOpened = openedResult.PresentationData.TileEvents.Single(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitOpened);
            Assert.That(exitOpened.TileId, Is.EqualTo(100));
            Assert.That(exitOpened.Cell, Is.EqualTo(exitCell));
            var exitVisualState = openedResult.PresentationData.TileFeatureVisualStates.Single(state =>
                state.TileFeatureKind == TileFeatureKind.Exit);
            Assert.That(exitVisualState.IsActive, Is.True);
            Assert.That(openedResult.ObjectiveResult.RequiredNonPrimaryConditionsSatisfiedThisTick, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_ActiveFaceOnlyInactiveFace_DoesNotOpenOrClear()
        {
            var exitCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var objective = CreateExitObjectiveDefinitionWithTickPrerequisite(
                exitCell,
                prerequisiteSatisfiedTick: 8,
                activationRule: TileFeatureActivationRule.ActiveFaceOnly);
            var tileFeatureDefinitions = CreateExitTileFeatureDefinitions(TileFeatureActivationRule.ActiveFaceOnly);
            var worldState = CreateExitWorldState(
                exitCell,
                new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, exitCell));
            var pipeline = CreatePipeline(worldState, objective, tileFeatureDefinitions);

            var incompleteResult = pipeline.RunTick(new TickInput(7));
            var result = pipeline.RunTick(new TickInput(8));

            Assert.That(incompleteResult.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitOpened), Is.False);
            Assert.That(result.ObjectiveResult.RequiredNonPrimaryConditionsSatisfiedThisTick, Is.True);
            Assert.That(result.ObjectiveResult.ClearedThisTick, Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitOpened), Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitEntered), Is.False);
            var exitVisualState = result.PresentationData.TileFeatureVisualStates.Single(state =>
                state.TileFeatureKind == TileFeatureKind.Exit);
            Assert.That(exitVisualState.IsActive, Is.False);
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_ObjectiveClearedEmitsAtActiveExit()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var objective = CreateExitObjectiveDefinitionWithTickPrerequisite(exitCell, prerequisiteSatisfiedTick: 8);
            var tileFeatureDefinitions = CreateExitTileFeatureDefinitions();
            var worldState = CreateExitWorldState(
                exitCell,
                new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, exitCell));
            var pipeline = CreatePipeline(worldState, objective, tileFeatureDefinitions);

            var incompleteResult = pipeline.RunTick(new TickInput(7));
            var clearedResult = pipeline.RunTick(new TickInput(8));
            var laterResult = pipeline.RunTick(new TickInput(9));

            Assert.That(incompleteResult.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitObjectiveCleared), Is.False);
            var objectiveCleared = clearedResult.PresentationData.TileEvents.Single(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitObjectiveCleared);
            Assert.That(objectiveCleared.TileId, Is.EqualTo(100));
            Assert.That(objectiveCleared.Cell, Is.EqualTo(exitCell));
            Assert.That(objectiveCleared.TileFeatureKind, Is.EqualTo(TileFeatureKind.Exit));
            Assert.That(objectiveCleared.TargetEntityId, Is.Zero);
            Assert.That(clearedResult.ObjectiveResult.ClearedThisTick, Is.True);
            var outcome = clearedResult.PresentationData.PlayerOutcomeSignals.Single();
            Assert.That(outcome.EntityId, Is.EqualTo(10));
            Assert.That(outcome.OutcomeKind, Is.EqualTo(TickPlayerOutcomePresentationKind.StageClearVictory));
            Assert.That(outcome.SourceTileId, Is.EqualTo(100));
            Assert.That(outcome.SourceCell, Is.EqualTo(exitCell));
            Assert.That(laterResult.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitObjectiveCleared), Is.False);
            Assert.That(laterResult.PresentationData.PlayerOutcomeSignals, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_VisualStateStaysClosedUntilRequiredNonPrimaryCompletes()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var objective = CreateExitObjectiveDefinitionWithTickPrerequisite(exitCell, prerequisiteSatisfiedTick: 8);
            var tileFeatureDefinitions = CreateExitTileFeatureDefinitions();
            var worldState = CreateExitWorldState(
                exitCell,
                new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)));
            var pipeline = CreatePipeline(worldState, objective, tileFeatureDefinitions);

            var closedResult = pipeline.RunTick(new TickInput(7));
            var openedResult = pipeline.RunTick(new TickInput(8));

            var closedExitState = closedResult.PresentationData.TileFeatureVisualStates.Single(state =>
                state.TileFeatureKind == TileFeatureKind.Exit);
            var openedExitState = openedResult.PresentationData.TileFeatureVisualStates.Single(state =>
                state.TileFeatureKind == TileFeatureKind.Exit);

            Assert.That(closedExitState.TileId, Is.EqualTo(100));
            Assert.That(closedExitState.IsActive, Is.False);
            Assert.That(openedExitState.TileId, Is.EqualTo(100));
            Assert.That(openedExitState.IsActive, Is.True);
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_OpenedDoesNotEmitWhenExitInactive()
        {
            var exitCell = new SurfaceCell(FaceId.Front, 1, 1);
            var objective = CreateExitObjectiveDefinitionWithTickPrerequisite(exitCell, prerequisiteSatisfiedTick: 8);
            var tileFeatureDefinitions = CreateExitTileFeatureDefinitions();
            var worldState = CreateExitWorldState(
                exitCell,
                new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)));
            var pipeline = CreatePipeline(worldState, objective, tileFeatureDefinitions);

            pipeline.RunTick(new TickInput(7));
            var result = pipeline.RunTick(new TickInput(8));

            Assert.That(result.ObjectiveResult.RequiredNonPrimaryConditionsSatisfiedThisTick, Is.True);
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.ExitOpened), Is.False);
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_EnteredEmitsOnActiveExitCenterClearTickOnlyForPlayer()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var primaryGoal = CreatePlayerAtAnyZoneCondition("goal");

            try
            {
                var buildResult = BuildExitObjectiveStage(
                    primaryGoal,
                    CreateStageTileFeature(
                        100,
                        exitCell,
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.ActiveFaceOnly,
                        TileFeatureBoxSelector.None));
                var worldState = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreatePlayerEntity(10, exitCell) },
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    buildResult.InitialTileFeatures);
                var pipeline = CreatePipeline(
                    worldState,
                    buildResult.ObjectiveRuntimeDefinition,
                    buildResult.TileFeatureDefinitions);

                var firstResult = pipeline.RunTick(new TickInput(7));
                var secondResult = pipeline.RunTick(new TickInput(8));

                var exitEntered = firstResult.PresentationData.TileEvents.Single(tileEvent =>
                    tileEvent.EventKind == TilePresentationEventKind.ExitEntered);
                Assert.That(exitEntered.TileId, Is.EqualTo(100));
                Assert.That(exitEntered.Cell, Is.EqualTo(exitCell));
                Assert.That(exitEntered.TileFeatureKind, Is.EqualTo(TileFeatureKind.Exit));
                Assert.That(exitEntered.TargetEntityId, Is.EqualTo(10));
                Assert.That(exitEntered.Direction, Is.EqualTo(Direction.None));
                Assert.That(firstResult.PresentationData.PlayerOutcomeSignals.Single().OutcomeKind, Is.EqualTo(TickPlayerOutcomePresentationKind.StageClearVictory));
                Assert.That(secondResult.PresentationData.TileEvents.Any(tileEvent =>
                    tileEvent.EventKind == TilePresentationEventKind.ExitEntered), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primaryGoal);
            }
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_SameTickOpenAndEnterEmitsOpenedThenEntered()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var objective = CreateExitObjectiveDefinitionWithTickPrerequisite(exitCell, prerequisiteSatisfiedTick: 8);
            var tileFeatureDefinitions = CreateExitTileFeatureDefinitions();
            var worldState = CreateExitWorldState(
                exitCell,
                new CubeTopologyState(FaceId.Floor),
                CreatePlayerEntity(10, exitCell));
            var pipeline = CreatePipeline(worldState, objective, tileFeatureDefinitions);

            pipeline.RunTick(new TickInput(7));
            var result = pipeline.RunTick(new TickInput(8));

            var exitEvents = result.PresentationData.TileEvents
                .Where(tileEvent =>
                    tileEvent.EventKind == TilePresentationEventKind.ExitOpened ||
                    tileEvent.EventKind == TilePresentationEventKind.ExitEntered)
                .ToArray();

            Assert.That(
                exitEvents.Select(tileEvent => tileEvent.EventKind).ToArray(),
                Is.EqualTo(new[]
                {
                    TilePresentationEventKind.ExitOpened,
                    TilePresentationEventKind.ExitEntered,
                }));
            Assert.That(exitEvents[1].TargetEntityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Core")]
        public void ExitPresentation_NonPlayerOccupantsOrInactiveExitDoNotEmitEntered()
        {
            var exitCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
            var primaryGoal = CreatePlayerAtAnyZoneCondition("goal");

            try
            {
                var inactiveBuild = BuildExitObjectiveStage(
                    primaryGoal,
                    CreateStageTileFeature(
                        100,
                        exitCell,
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.ActiveFaceOnly,
                        TileFeatureBoxSelector.None));
                var inactiveWorld = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreatePlayerEntity(10, exitCell) },
                    inactiveBuild.BoardBounds,
                    inactiveBuild.InitialTopology,
                    inactiveBuild.InitialTileFeatures);
                var inactiveResult = CreatePipeline(
                        inactiveWorld,
                        inactiveBuild.ObjectiveRuntimeDefinition,
                        inactiveBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(7));

                Assert.That(inactiveResult.PresentationData.TileEvents.Any(tileEvent =>
                    tileEvent.EventKind == TilePresentationEventKind.ExitEntered), Is.False);

                var activeExitCell = new SurfaceCell(FaceId.Floor, 1, 1);
                var activeBuild = BuildExitObjectiveStage(
                    primaryGoal,
                    CreateStageTileFeature(
                        100,
                        activeExitCell,
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.ActiveFaceOnly,
                        TileFeatureBoxSelector.None));
                var activeWorld = GameplayCompositionRoot.CreateWorldState(
                    new[]
                    {
                        CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateEnemyEntity(20, activeExitCell),
                    },
                    activeBuild.BoardBounds,
                    activeBuild.InitialTopology,
                    activeBuild.InitialTileFeatures);
                var activeResult = CreatePipeline(
                        activeWorld,
                        activeBuild.ObjectiveRuntimeDefinition,
                        activeBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(8));

                Assert.That(activeResult.PresentationData.TileEvents.Any(tileEvent =>
                    tileEvent.EventKind == TilePresentationEventKind.ExitEntered), Is.False);

                var boxWorld = GameplayCompositionRoot.CreateWorldState(
                    new[]
                    {
                        CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBoxEntity(30, activeExitCell, BoxCapabilities.Push, BoxArchetype.Normal),
                    },
                    activeBuild.BoardBounds,
                    activeBuild.InitialTopology,
                    activeBuild.InitialTileFeatures);
                var boxResult = CreatePipeline(
                        boxWorld,
                        activeBuild.ObjectiveRuntimeDefinition,
                        activeBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(9));
                Assert.That(boxResult.PresentationData.TileEvents.Any(tileEvent =>
                    tileEvent.EventKind == TilePresentationEventKind.ExitEntered), Is.False);

                var moonWorld = GameplayCompositionRoot.CreateWorldState(
                    new[]
                    {
                        CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateBoxEntity(
                            40,
                            activeExitCell,
                            BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                            BoxArchetype.Moon),
                    },
                    activeBuild.BoardBounds,
                    activeBuild.InitialTopology,
                    activeBuild.InitialTileFeatures);
                var moonResult = CreatePipeline(
                        moonWorld,
                        activeBuild.ObjectiveRuntimeDefinition,
                        activeBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(10));
                Assert.That(moonResult.PresentationData.TileEvents.Any(tileEvent =>
                    tileEvent.EventKind == TilePresentationEventKind.ExitEntered), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primaryGoal);
            }
        }

        [Test]
        [Category("Extended")]
        public void Conditions_SpecificEntityAtZone_TracksAnchorCellAndAliveState()
        {
            var conditionAsset = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();

            try
            {
                SetPrivateField(conditionAsset, "entityId", 20);
                SetPrivateField(conditionAsset, "zoneId", "target");
                SetPrivateField(conditionAsset, "requireAlive", true);
                var runtime = BuildSingleConditionRuntime(
                    conditionAsset,
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    });

                runtime.Advance(
                    CreateSnapshot(CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 1, 1))),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.True);

                runtime.Advance(
                    CreateSnapshot(CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 0, 0))),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(CreateSnapshot(), StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(
                    CreateSnapshot(CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 1, 1), hp: 0)),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void Conditions_ButtonActivated_IsIncompleteWhenTileIsMissing()
        {
            var conditionAsset = CreateButtonActivatedCondition(10);

            try
            {
                var runtime = BuildSingleButtonConditionRuntime(
                    conditionAsset,
                    CreateStageTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button));

                runtime.Advance(CreateSnapshot(), StageObjectiveTickFacts.Empty);

                Assert.That(runtime.IsSatisfied, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void Conditions_ButtonActivated_IsIncompleteWhenTileIsNotButton()
        {
            var conditionAsset = CreateButtonActivatedCondition(10);

            try
            {
                var runtime = BuildSingleButtonConditionRuntime(
                    conditionAsset,
                    CreateStageTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button));

                runtime.Advance(
                    CreateSnapshotWithTileFeatures(
                        new[]
                        {
                            CreateTileFeatureState(
                                10,
                                new SurfaceCell(FaceId.Floor, 1, 1),
                                TileFeatureKind.Exit,
                                TileFeatureFlags.Activated),
                        }),
                    StageObjectiveTickFacts.Empty);

                Assert.That(runtime.IsSatisfied, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void Conditions_ButtonActivated_TracksActivatedFlag()
        {
            var conditionAsset = CreateButtonActivatedCondition(10);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var runtime = BuildSingleButtonConditionRuntime(
                    conditionAsset,
                    CreateStageTileFeature(10, cell, TileFeatureKind.Button));

                runtime.Advance(
                    CreateSnapshotWithTileFeatures(new[] { CreateButtonState(10, cell) }),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.False);

                runtime.Advance(
                    CreateSnapshotWithTileFeatures(new[] { CreateButtonState(10, cell, TileFeatureFlags.Activated) }),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.True);

                runtime.Advance(
                    CreateSnapshotWithTileFeatures(new[] { CreateButtonState(10, cell, TileFeatureFlags.Activated) }),
                    StageObjectiveTickFacts.Empty);
                Assert.That(runtime.IsSatisfied, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void Conditions_ButtonActivated_DoesNotMutateWorldState()
        {
            var conditionAsset = CreateButtonActivatedCondition(10);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                DefaultBoardBounds,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { CreateButtonState(10, cell, TileFeatureFlags.Activated) });

            try
            {
                var runtime = BuildSingleButtonConditionRuntime(
                    conditionAsset,
                    CreateStageTileFeature(10, cell, TileFeatureKind.Button));
                var before = worldState.CreateSnapshot();

                runtime.Advance(before, StageObjectiveTickFacts.Empty);

                var after = worldState.CreateSnapshot();
                Assert.That(after.TryGetTileFeature(10, out var afterButton), Is.True);
                Assert.That(before.TryGetTileFeature(10, out var beforeButton), Is.True);
                Assert.That(afterButton, Is.EqualTo(beforeButton));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void Conditions_ButtonActivated_DoesNotCreateSnapshot()
        {
            var conditionAsset = CreateButtonActivatedCondition(10);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var runtime = BuildSingleButtonConditionRuntime(
                    conditionAsset,
                    CreateStageTileFeature(10, cell, TileFeatureKind.Button));
                var snapshot = CreateSnapshotWithTileFeatures(
                    new[] { CreateButtonState(10, cell, TileFeatureFlags.Activated) });

                SnapshotMaterializationCounts counts;
                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    runtime.Advance(snapshot, StageObjectiveTickFacts.Empty);
                    counts = capture.Counts;
                }

                Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void Conditions_ButtonActivated_AssetDoesNotImplementRuntimeInterface()
        {
            var conditionAsset = CreateButtonActivatedCondition(10);

            try
            {
                Assert.That(typeof(IStageConditionRuntime).IsAssignableFrom(conditionAsset.GetType()), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ButtonLatch_CompletesSameTickAfterFinalSnapshot()
        {
            var conditionAsset = CreateButtonActivatedCondition(100);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var buildResult = BuildButtonObjectiveStage(
                    conditionAsset,
                    CreateStageTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Button,
                        TileFeatureActivationRule.Always,
                        TileFeatureBoxSelector.AnyPushableBox));
                var worldState = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreateSlidingBoxEntity(20, cell, BoxCapabilities.Push), CreateSlideStopWallEntity(120, cell) },
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    buildResult.InitialTileFeatures);
                var pipeline = CreatePipeline(
                    worldState,
                    buildResult.ObjectiveRuntimeDefinition,
                    buildResult.TileFeatureDefinitions);

                var result = pipeline.RunTick(new TickInput(7));

                Assert.That(result.ObjectiveResult.IsCleared, Is.True);
                Assert.That(result.ObjectiveResult.ClearedThisTick, Is.True);
                Assert.That(result.ObjectiveResult.AllConditionsSatisfied, Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetTileFeature(100, out var button), Is.True);
                Assert.That((button.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));
                Assert.That(result.PresentationData.TileEvents.Count, Is.EqualTo(1));
                var tileEvent = result.PresentationData.TileEvents.Single();
                Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.ButtonActivated));
                Assert.That(tileEvent.TileId, Is.EqualTo(100));
                Assert.That(tileEvent.Cell, Is.EqualTo(cell));
                Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.Button));
                Assert.That(tileEvent.SourceEntityId, Is.Zero);
                Assert.That(tileEvent.OwnerEntityId, Is.Zero);
                Assert.That(tileEvent.TeamId, Is.Zero);
                Assert.That(result.PresentationData.PlayerOutcomeSignals, Is.Empty);

                var nextResult = pipeline.RunTick(new TickInput(8));

                Assert.That(nextResult.PresentationData.TileEvents, Is.Empty);
                Assert.That(nextResult.PresentationData.PlayerOutcomeSignals, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_MoonBlockOnlyButtonLatch_CompletesSameTickAfterFinalSnapshot()
        {
            var conditionAsset = CreateButtonActivatedCondition(100);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var moonCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy;

            try
            {
                var buildResult = BuildButtonObjectiveStage(
                    conditionAsset,
                    CreateStageTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Button,
                        TileFeatureActivationRule.Always,
                        TileFeatureBoxSelector.MoonBlockOnly),
                    CreateBoxSpawn(20, new SurfaceCell(FaceId.Floor, 2, 1), moonCapabilities, BoxArchetype.Moon));
                var worldState = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreateSlidingBoxEntity(20, cell, moonCapabilities, BoxArchetype.Moon), CreateSlideStopWallEntity(120, cell) },
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    buildResult.InitialTileFeatures);
                var pipeline = CreatePipeline(
                    worldState,
                    buildResult.ObjectiveRuntimeDefinition,
                    buildResult.TileFeatureDefinitions);

                var result = pipeline.RunTick(new TickInput(7));

                Assert.That(result.ObjectiveResult.IsCleared, Is.True);
                Assert.That(result.ObjectiveResult.ClearedThisTick, Is.True);
                Assert.That(result.ObjectiveResult.AllConditionsSatisfied, Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetTileFeature(100, out var button), Is.True);
                Assert.That((button.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ButtonLatch_InactiveTopologyDoesNotComplete()
        {
            var conditionAsset = CreateButtonActivatedCondition(100);
            var cell = new SurfaceCell(FaceId.Back, 1, 1);

            try
            {
                var buildResult = BuildButtonObjectiveStage(
                    conditionAsset,
                    CreateStageTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Button,
                        TileFeatureActivationRule.ActiveFaceOnly,
                        TileFeatureBoxSelector.AnyPushableBox));
                var worldState = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreateBoxEntity(20, cell, BoxCapabilities.Push) },
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    buildResult.InitialTileFeatures);
                var pipeline = CreatePipeline(
                    worldState,
                    buildResult.ObjectiveRuntimeDefinition,
                    buildResult.TileFeatureDefinitions);

                var result = pipeline.RunTick(new TickInput(7));

                Assert.That(result.ObjectiveResult.IsCleared, Is.False);
                Assert.That(result.ObjectiveResult.AllConditionsSatisfied, Is.False);
                Assert.That(result.PresentationData.TileEvents, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ButtonLatch_NonPushableBoxDoesNotComplete()
        {
            var conditionAsset = CreateButtonActivatedCondition(100);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var buildResult = BuildButtonObjectiveStage(
                    conditionAsset,
                    CreateStageTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Button,
                        TileFeatureActivationRule.Always,
                        TileFeatureBoxSelector.AnyPushableBox));
                var worldState = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreateBoxEntity(20, cell, BoxCapabilities.Flip) },
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    buildResult.InitialTileFeatures);
                var pipeline = CreatePipeline(
                    worldState,
                    buildResult.ObjectiveRuntimeDefinition,
                    buildResult.TileFeatureDefinitions);

                var result = pipeline.RunTick(new TickInput(7));

                Assert.That(result.ObjectiveResult.IsCleared, Is.False);
                Assert.That(result.ObjectiveResult.AllConditionsSatisfied, Is.False);
                Assert.That(result.PresentationData.TileEvents, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_ButtonLatch_ProducesButtonActivatedRequestThroughPresenter()
        {
            var conditionAsset = CreateButtonActivatedCondition(100);
            var rootObject = new GameObject(nameof(ObjectiveClear_ButtonLatch_ProducesButtonActivatedRequestThroughPresenter));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var box = CreateSlidingBoxEntity(20, cell, BoxCapabilities.Push);

            try
            {
                var buildResult = BuildButtonObjectiveStage(
                    conditionAsset,
                    CreateStageTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Button,
                        TileFeatureActivationRule.Always,
                        TileFeatureBoxSelector.AnyPushableBox));
                var worldState = GameplayCompositionRoot.CreateWorldState(
                    new[] { box, CreateSlideStopWallEntity(120, cell) },
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    buildResult.InitialTileFeatures);
                var pipeline = CreatePipeline(
                    worldState,
                    buildResult.ObjectiveRuntimeDefinition,
                    buildResult.TileFeatureDefinitions);
                var presenter = CreateInitializedTileRequestPresenter(rootObject, buildResult, new[] { box });
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                var result = pipeline.RunTick(new TickInput(7));
                var hashBeforePresent = result.DeterminismHash;
                presenter.Present(result);

                Assert.That(result.DeterminismHash, Is.EqualTo(hashBeforePresent));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                var request = presenter.CurrentTilePresentationRequests[0];
                Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.ButtonActivated));
                Assert.That(request.TileId, Is.EqualTo(100));
                Assert.That(request.Cell, Is.EqualTo(cell));
                Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.Button));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_ButtonLatch_AlreadyActivatedNextTickProducesNoRequestThroughPresenter()
        {
            var conditionAsset = CreateButtonActivatedCondition(100);
            var rootObject = new GameObject(nameof(ObjectiveClear_ButtonLatch_AlreadyActivatedNextTickProducesNoRequestThroughPresenter));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var box = CreateSlidingBoxEntity(20, cell, BoxCapabilities.Push);

            try
            {
                var buildResult = BuildButtonObjectiveStage(
                    conditionAsset,
                    CreateStageTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Button,
                        TileFeatureActivationRule.Always,
                        TileFeatureBoxSelector.AnyPushableBox));
                var worldState = GameplayCompositionRoot.CreateWorldState(
                    new[] { box, CreateSlideStopWallEntity(120, cell) },
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    buildResult.InitialTileFeatures);
                var pipeline = CreatePipeline(
                    worldState,
                    buildResult.ObjectiveRuntimeDefinition,
                    buildResult.TileFeatureDefinitions);
                var presenter = CreateInitializedTileRequestPresenter(rootObject, buildResult, new[] { box });
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(pipeline.RunTick(new TickInput(7)));
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));

                presenter.Present(pipeline.RunTick(new TickInput(8)));

                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_ButtonLatch_FailedLatchProducesNoRequestThroughPresenter()
        {
            var conditionAsset = CreateButtonActivatedCondition(100);
            var rootObject = new GameObject(nameof(ObjectiveClear_ButtonLatch_FailedLatchProducesNoRequestThroughPresenter));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var box = CreateBoxEntity(20, cell, BoxCapabilities.Flip);

            try
            {
                var buildResult = BuildButtonObjectiveStage(
                    conditionAsset,
                    CreateStageTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Button,
                        TileFeatureActivationRule.Always,
                        TileFeatureBoxSelector.AnyPushableBox));
                var worldState = GameplayCompositionRoot.CreateWorldState(
                    new[] { box },
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    buildResult.InitialTileFeatures);
                var pipeline = CreatePipeline(
                    worldState,
                    buildResult.ObjectiveRuntimeDefinition,
                    buildResult.TileFeatureDefinitions);
                var presenter = CreateInitializedTileRequestPresenter(rootObject, buildResult, new[] { box });
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(pipeline.RunTick(new TickInput(7)));

                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_ButtonLatch_ReplayIsDeterministic()
        {
            var firstCondition = CreateButtonActivatedCondition(100);
            var secondCondition = CreateButtonActivatedCondition(100);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var tileFeature = CreateStageTileFeature(
                    100,
                    cell,
                    TileFeatureKind.Button,
                    TileFeatureActivationRule.Always,
                    TileFeatureBoxSelector.AnyPushableBox);
                var firstBuild = BuildButtonObjectiveStage(firstCondition, tileFeature);
                var secondBuild = BuildButtonObjectiveStage(secondCondition, tileFeature);
                var firstWorld = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreateSlidingBoxEntity(20, cell, BoxCapabilities.Push), CreateSlideStopWallEntity(120, cell) },
                    firstBuild.BoardBounds,
                    firstBuild.InitialTopology,
                    firstBuild.InitialTileFeatures);
                var secondWorld = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreateSlidingBoxEntity(20, cell, BoxCapabilities.Push), CreateSlideStopWallEntity(120, cell) },
                    secondBuild.BoardBounds,
                    secondBuild.InitialTopology,
                    secondBuild.InitialTileFeatures);

                var firstResult = CreatePipeline(
                        firstWorld,
                        firstBuild.ObjectiveRuntimeDefinition,
                        firstBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(7));
                var secondResult = CreatePipeline(
                        secondWorld,
                        secondBuild.ObjectiveRuntimeDefinition,
                        secondBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(7));

                Assert.That(firstResult.ObjectiveResult.IsCleared, Is.EqualTo(secondResult.ObjectiveResult.IsCleared));
                Assert.That(firstResult.ObjectiveResult.AllConditionsSatisfied, Is.EqualTo(secondResult.ObjectiveResult.AllConditionsSatisfied));
                Assert.That(firstResult.DeterminismHash, Is.EqualTo(secondResult.DeterminismHash));
                Assert.That(firstWorld.CreateSnapshot().TryGetTileFeature(100, out var firstButton), Is.True);
                Assert.That(secondWorld.CreateSnapshot().TryGetTileFeature(100, out var secondButton), Is.True);
                Assert.That(firstButton.Flags, Is.EqualTo(secondButton.Flags));
                Assert.That((firstButton.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstCondition);
                UnityEngine.Object.DestroyImmediate(secondCondition);
            }
        }

        [Test]
        [Category("Core")]
        public void ObjectiveClear_MoonBlockOnlyButtonLatch_ReplayIsDeterministic()
        {
            var firstCondition = CreateButtonActivatedCondition(100);
            var secondCondition = CreateButtonActivatedCondition(100);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var moonCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy;

            try
            {
                var tileFeature = CreateStageTileFeature(
                    100,
                    cell,
                    TileFeatureKind.Button,
                    TileFeatureActivationRule.Always,
                    TileFeatureBoxSelector.MoonBlockOnly);
                var authoredMoonSpawn = CreateBoxSpawn(
                    20,
                    new SurfaceCell(FaceId.Floor, 2, 1),
                    moonCapabilities,
                    BoxArchetype.Moon);
                var firstBuild = BuildButtonObjectiveStage(firstCondition, tileFeature, authoredMoonSpawn);
                var secondBuild = BuildButtonObjectiveStage(secondCondition, tileFeature, authoredMoonSpawn);
                var firstWorld = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreateSlidingBoxEntity(20, cell, moonCapabilities, BoxArchetype.Moon), CreateSlideStopWallEntity(120, cell) },
                    firstBuild.BoardBounds,
                    firstBuild.InitialTopology,
                    firstBuild.InitialTileFeatures);
                var secondWorld = GameplayCompositionRoot.CreateWorldState(
                    new[] { CreateSlidingBoxEntity(20, cell, moonCapabilities, BoxArchetype.Moon), CreateSlideStopWallEntity(120, cell) },
                    secondBuild.BoardBounds,
                    secondBuild.InitialTopology,
                    secondBuild.InitialTileFeatures);

                var firstResult = CreatePipeline(
                        firstWorld,
                        firstBuild.ObjectiveRuntimeDefinition,
                        firstBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(7));
                var secondResult = CreatePipeline(
                        secondWorld,
                        secondBuild.ObjectiveRuntimeDefinition,
                        secondBuild.TileFeatureDefinitions)
                    .RunTick(new TickInput(7));

                Assert.That(firstResult.ObjectiveResult.IsCleared, Is.EqualTo(secondResult.ObjectiveResult.IsCleared));
                Assert.That(firstResult.ObjectiveResult.AllConditionsSatisfied, Is.EqualTo(secondResult.ObjectiveResult.AllConditionsSatisfied));
                Assert.That(firstResult.DeterminismHash, Is.EqualTo(secondResult.DeterminismHash));
                Assert.That(firstWorld.CreateSnapshot().TryGetTileFeature(100, out var firstButton), Is.True);
                Assert.That(secondWorld.CreateSnapshot().TryGetTileFeature(100, out var secondButton), Is.True);
                Assert.That(firstButton.Flags, Is.EqualTo(secondButton.Flags));
                Assert.That((firstButton.Flags & TileFeatureFlags.Activated), Is.Not.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstCondition);
                UnityEngine.Object.DestroyImmediate(secondCondition);
            }
        }

        [TestCase(0.5f, 10f, 20, 21)]
        [TestCase(0.25f, 10f, 40, 41)]
        [TestCase(0.3f, 10f, 34, 35)]
        [Category("Extended")]
        public void Conditions_ClearWithinTimeLimit_UsesCeilDeadlineFromTiming(
            float tickDeltaSeconds,
            float seconds,
            int lastAcceptedTick,
            int firstRejectedTick)
        {
            var conditionAsset = ScriptableObject.CreateInstance<ClearWithinTimeLimitConditionAsset>();

            try
            {
                SetPrivateField(conditionAsset, "clearBeforeOrAtSeconds", seconds);
                var runtime = BuildSingleConditionRuntime(
                    conditionAsset,
                    timing: StageSimulationTiming.FromTickDeltaSeconds(tickDeltaSeconds));
                var snapshot = CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                runtime.Advance(
                    snapshot,
                    new StageObjectiveTickFacts(
                        lastAcceptedTick,
                        PlayerTickCommand.None,
                        Array.Empty<int>(),
                        Array.Empty<StageObjectiveDamageFact>()));
                Assert.That(runtime.IsSatisfied, Is.True);

                runtime.Advance(
                    snapshot,
                    new StageObjectiveTickFacts(
                        firstRejectedTick,
                        PlayerTickCommand.None,
                        Array.Empty<int>(),
                        Array.Empty<StageObjectiveDamageFact>()));
                Assert.That(runtime.IsSatisfied, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(conditionAsset);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_RequireAllConditions_CombinesEntityZoneAndTimeLimit()
        {
            var zoneCondition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();
            var timeCondition = ScriptableObject.CreateInstance<ClearWithinTimeLimitConditionAsset>();

            try
            {
                SetPrivateField(zoneCondition, "entityId", 20);
                SetPrivateField(zoneCondition, "zoneId", "target");
                SetPrivateField(zoneCondition, "requireAlive", true);
                SetPrivateField(timeCondition, "clearBeforeOrAtSeconds", 10f);
                var objective = BuildObjectiveDefinition(
                    primaryZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredAssets: new StageConditionAsset[] { zoneCondition, timeCondition },
                    completionPolicy: StageCompletionPolicy.RequireAllConditions,
                    timing: StageSimulationTiming.FromTickDeltaSeconds(0.5f));

                var insideSnapshot = CreateSnapshot(CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 1, 1)));
                var outsideSnapshot = CreateSnapshot(CreateEnemyEntity(20, new SurfaceCell(FaceId.Floor, 0, 0)));

                var clearResult = objective.CreateTracker().Advance(
                    insideSnapshot,
                    CreateObjectiveTickFacts(20));
                Assert.That(clearResult.IsCleared, Is.True);

                var outsideResult = objective.CreateTracker().Advance(
                    outsideSnapshot,
                    CreateObjectiveTickFacts(20));
                Assert.That(outsideResult.IsCleared, Is.False);

                var overTimeResult = objective.CreateTracker().Advance(
                    insideSnapshot,
                    CreateObjectiveTickFacts(21));
                Assert.That(overTimeResult.IsCleared, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(zoneCondition);
                UnityEngine.Object.DestroyImmediate(timeCondition);
            }
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

        [Test]
        [Category("Extended")]
        public void StageObjectiveAuthoring_StoresObjectiveTitleSummary()
        {
            var objective = CreateObjective(
                StageCompletionPolicy.RequireAllConditions,
                Array.Empty<StageObjectiveConditionEntry>(),
                "Reach the Exit",
                "Move to the exit zone.");

            Assert.That(objective.ObjectiveTitle, Is.EqualTo("Reach the Exit"));
            Assert.That(objective.ObjectiveSummary, Is.EqualTo("Move to the exit zone."));
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_CompilesExitObjectiveSemanticPresentationIdentity()
        {
            var condition = CreatePrimaryGoalCondition(new[] { "goal" });
            var stage = CreateStage(
                "DisplayObjectiveStage",
                CreateBoard(),
                new[] { CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)) },
                CreateObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    new[]
                    {
                        CreateConditionEntry(
                            condition,
                            required: true,
                            StageObjectiveConditionRole.PrimaryGoal,
                            "primary-goal",
                            "Reach the exit zone",
                            sortOrder: 3),
                    },
                    "Reach the Exit",
                    "Move to the exit zone."),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            try
            {
                var objective = StageRuntimeBuilder.Build(stage).ObjectiveRuntimeDefinition;

                Assert.That(objective.ConditionEntries.Count, Is.EqualTo(1));
                Assert.That(
                    objective.ConditionEntries[0].PresentationId,
                    Is.EqualTo(StageObjectiveConditionPresentationIds.ReachExit));
                Assert.That(
                    objective.ConditionEntries[0].StableGroupKey,
                    Is.EqualTo(StageObjectiveConditionPresentationIds.ReachExit));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ConditionRuntimeEntries_PreserveStableIdentityRoleRequiredSort()
        {
            var first = CreatePrimaryGoalCondition(new[] { "goal" });
            var second = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var stage = CreateStage(
                "DisplayConditionMetadataStage",
                CreateBoard(),
                new[] { CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)) },
                CreateObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    new[]
                    {
                        CreateConditionEntry(
                            first,
                            required: true,
                            StageObjectiveConditionRole.PrimaryGoal,
                            "primary-goal",
                            "Reach the exit zone",
                            sortOrder: 10),
                        CreateConditionEntry(
                            second,
                            required: false,
                            StageObjectiveConditionRole.Challenge,
                            "defeat-all",
                            "Defeat every enemy",
                            sortOrder: 20),
                    }),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            try
            {
                var metadata = StageRuntimeBuilder.Build(stage).ObjectiveRuntimeDefinition.ConditionEntries;

                Assert.That(metadata.Count, Is.EqualTo(2));
                Assert.That(metadata[0].StableConditionId, Is.EqualTo("primary-goal"));
                Assert.That(metadata[0].Role, Is.EqualTo(StageObjectiveConditionRole.PrimaryGoal));
                Assert.That(metadata[0].Required, Is.True);
                Assert.That(metadata[0].SortOrder, Is.EqualTo(10));
                Assert.That(metadata[0].AuthoringOrder, Is.EqualTo(0));
                Assert.That(
                    metadata[0].PresentationId,
                    Is.EqualTo(StageObjectiveConditionPresentationIds.ReachExit));
                Assert.That(metadata[1].StableConditionId, Is.EqualTo("defeat-all"));
                Assert.That(metadata[1].Role, Is.EqualTo(StageObjectiveConditionRole.Challenge));
                Assert.That(metadata[1].Required, Is.False);
                Assert.That(metadata[1].SortOrder, Is.EqualTo(20));
                Assert.That(metadata[1].AuthoringOrder, Is.EqualTo(1));
                Assert.That(metadata[1].PresentationId, Is.Empty);
                Assert.That(metadata[1].StableGroupKey, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        private static IStageConditionRuntime BuildSingleConditionRuntime(
            StageConditionAsset conditionAsset,
            StageZoneDefinition[] zones = null)
        {
            return BuildSingleConditionRuntime(conditionAsset, StageSimulationTiming.Default, zones);
        }

        private static IStageConditionRuntime BuildSingleConditionRuntime(
            StageConditionAsset conditionAsset,
            StageSimulationTiming timing,
            StageZoneDefinition[] zones = null)
        {
            if (conditionAsset is ClearWithinTimeLimitConditionAsset)
            {
                var dummyCondition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();
                try
                {
                    SetPrivateField(dummyCondition, "entityId", 999);
                    SetPrivateField(dummyCondition, "zoneId", "dummy");
                    SetPrivateField(dummyCondition, "requireAlive", true);
                    var clearWithinTimeObjective = BuildObjectiveDefinition(
                        primaryZoneIds: Array.Empty<string>(),
                        zones: new[]
                        {
                            CreateZone("dummy", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
                        },
                        requiredAssets: new[] { dummyCondition, conditionAsset },
                        completionPolicy: StageCompletionPolicy.RequireAllConditions,
                        timing: timing);

                    for (var i = 0; i < clearWithinTimeObjective.ConditionEntries.Count; i++)
                    {
                        var runtime = clearWithinTimeObjective.ConditionEntries[i].Condition.CreateRuntime();
                        if (runtime.CreateStatus().ConditionType == nameof(ClearWithinTimeLimitConditionAsset))
                        {
                            return runtime;
                        }
                    }

                    Assert.Fail("Missing compiled clear-within-time-limit condition runtime.");
                    throw new InvalidOperationException("Missing compiled clear-within-time-limit condition runtime.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummyCondition);
                }
            }

            var effectiveZones = zones ?? new[]
            {
                CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
            };
            var objective = BuildObjectiveDefinition(
                primaryZoneIds: Array.Empty<string>(),
                zones: effectiveZones,
                requiredAssets: new[] { conditionAsset },
                completionPolicy: StageCompletionPolicy.RequireAllConditions,
                timing: timing);

            return objective.ConditionEntries[0].Condition.CreateRuntime();
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            string[] primaryZoneIds,
            StageZoneDefinition[] zones,
            StageConditionAsset[] requiredAssets = null,
            StageCompletionPolicy completionPolicy = StageCompletionPolicy.RequireAllConditions,
            bool primaryRequired = true)
        {
            return BuildObjectiveDefinition(
                primaryZoneIds,
                zones,
                requiredAssets,
                completionPolicy,
                StageSimulationTiming.Default,
                primaryRequired);
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            string[] primaryZoneIds,
            StageZoneDefinition[] zones,
            StageConditionAsset[] requiredAssets,
            StageCompletionPolicy completionPolicy,
            StageSimulationTiming timing,
            bool primaryRequired = true)
        {
            return BuildObjectiveDefinition(
                primaryZoneIds,
                zones,
                requiredAssets,
                completionPolicy,
                timing,
                conditionEntries: null,
                primaryRequired);
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            string[] primaryZoneIds,
            StageZoneDefinition[] zones,
            StageConditionAsset[] requiredAssets,
            StageCompletionPolicy completionPolicy,
            StageSimulationTiming timing,
            StageObjectiveConditionEntry[] conditionEntries,
            bool primaryRequired = true)
        {
            var generatedPrimary = CreatePrimaryGoalCondition(primaryZoneIds);

            try
            {
                var entries = BuildConditionEntries(generatedPrimary, primaryRequired, requiredAssets, conditionEntries);
                var stage = CreateStage(
                    "ObjectiveStage",
                    CreateBoard(),
                    zones,
                    CreateObjective(completionPolicy, entries),
                    CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

                try
                {
                    return StageRuntimeBuilder.Build(stage, timing).ObjectiveRuntimeDefinition;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(stage);
                }
            }
            finally
            {
                if (generatedPrimary != null)
                {
                    UnityEngine.Object.DestroyImmediate(generatedPrimary);
                }
            }
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            string[] primaryZoneIds,
            StageZoneDefinition[] zones,
            StageCompletionPolicy completionPolicy,
            StageSimulationTiming timing,
            StageObjectiveConditionEntry[] conditionEntries,
            bool primaryRequired = true)
        {
            return BuildObjectiveDefinition(
                primaryZoneIds,
                zones,
                requiredAssets: null,
                completionPolicy,
                timing,
                conditionEntries,
                primaryRequired);
        }

        private static StageObjectiveConditionEntry[] BuildConditionEntries(
            PlayerAtAnyZoneConditionAsset generatedPrimary,
            bool primaryRequired,
            IReadOnlyList<StageConditionAsset> requiredAssets,
            IReadOnlyList<StageObjectiveConditionEntry> explicitEntries)
        {
            var entries = new List<StageObjectiveConditionEntry>();
            if (generatedPrimary != null)
            {
                entries.Add(CreateConditionEntry(
                    generatedPrimary,
                    primaryRequired,
                    StageObjectiveConditionRole.PrimaryGoal,
                    "primary-goal"));
            }

            if (requiredAssets != null)
            {
                for (var i = 0; i < requiredAssets.Count; i++)
                {
                    entries.Add(CreateConditionEntry(
                        requiredAssets[i],
                        required: true,
                        StageObjectiveConditionRole.None,
                        $"required-{i}-{ResolveConditionAssetName(requiredAssets[i])}"));
                }
            }

            if (explicitEntries != null)
            {
                for (var i = 0; i < explicitEntries.Count; i++)
                {
                    entries.Add(explicitEntries[i]);
                }
            }

            return entries.ToArray();
        }

        private static PlayerAtAnyZoneConditionAsset CreatePrimaryGoalCondition(IReadOnlyList<string> primaryZoneIds)
        {
            if (primaryZoneIds == null || primaryZoneIds.Count == 0)
            {
                return null;
            }

            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            SetPrivateField(condition, "zoneIds", primaryZoneIds.ToArray());
            SetPrivateField(condition, "requireAlive", true);
            return condition;
        }

        private static string ResolveConditionAssetName(StageConditionAsset condition)
        {
            if (condition == null)
            {
                return "null";
            }

            return string.IsNullOrWhiteSpace(condition.name)
                ? condition.GetType().Name
                : condition.name.Trim();
        }

        private static bool ShouldScanForRemovedObjectiveSymbols(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            StageZoneDefinition[] zones,
            StageObjectiveConditionEntry[] conditionEntries,
            StageCompletionPolicy completionPolicy = StageCompletionPolicy.RequireAllConditions,
            StageSimulationTiming? timing = null)
        {
            var stage = CreateStage(
                "ObjectiveStage",
                CreateBoard(),
                zones,
                CreateObjective(completionPolicy, conditionEntries),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));

            try
            {
                return StageRuntimeBuilder.Build(stage, timing ?? StageSimulationTiming.Default).ObjectiveRuntimeDefinition;
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

        private static StageObjectiveAuthoring CreateObjective(
            StageCompletionPolicy completionPolicy,
            StageObjectiveConditionEntry[] conditionEntries = null,
            string objectiveTitle = "",
            string objectiveSummary = "")
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = completionPolicy,
                ObjectiveTitle = objectiveTitle,
                ObjectiveSummary = objectiveSummary,
                ConditionEntries = conditionEntries ?? Array.Empty<StageObjectiveConditionEntry>(),
            };
        }

        private static StageObjectiveConditionEntry CreateConditionEntry(
            StageConditionAsset condition,
            bool required,
            StageObjectiveConditionRole role,
            string stableConditionId = "",
            string displayText = "",
            int sortOrder = 0)
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = required,
                Role = role,
                StableConditionId = stableConditionId,
                DisplayText = displayText,
                SortOrder = sortOrder,
            };
        }

        private static IStageConditionRuntime BuildSingleButtonConditionRuntime(
            ButtonActivatedConditionAsset conditionAsset,
            StageTileFeatureDefinition tileFeature)
        {
            var buildResult = BuildButtonObjectiveStage(conditionAsset, tileFeature);
            return buildResult.ObjectiveRuntimeDefinition.ConditionEntries[0].Condition.CreateRuntime();
        }

        private static StageRuntimeBuildResult BuildButtonObjectiveStage(
            ButtonActivatedConditionAsset conditionAsset,
            StageTileFeatureDefinition tileFeature,
            params StageSpawnDefinition[] additionalSpawns)
        {
            var spawns = new List<StageSpawnDefinition>
            {
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)),
            };
            if (additionalSpawns != null)
            {
                spawns.AddRange(additionalSpawns);
            }

            var stage = CreateStage(
                "ButtonObjectiveStage",
                CreateBoard(),
                Array.Empty<StageZoneDefinition>(),
                CreateObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    new[]
                    {
                        CreateConditionEntry(conditionAsset, required: true, StageObjectiveConditionRole.PrimaryGoal, "button-activated"),
                    }),
                spawns.ToArray());
            SetPrivateField(stage, "tileFeatures", new[] { tileFeature });

            try
            {
                return StageRuntimeBuilder.Build(stage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static StageRuntimeBuildResult BuildExitObjectiveStage(
            PlayerAtAnyZoneConditionAsset conditionAsset,
            StageTileFeatureDefinition tileFeature)
        {
            var exitPosition = tileFeature.Cell.PlanarPosition;
            var stage = CreateStage(
                "ExitObjectiveStage",
                CreateBoard(),
                new[]
                {
                    CreateZone(
                        "goal",
                        tileFeature.Cell.face,
                        CreateRegion(exitPosition.x, exitPosition.y, exitPosition.x, exitPosition.y)),
                },
                CreateObjective(
                    StageCompletionPolicy.RequireAllConditions,
                    new[]
                    {
                        CreateConditionEntry(conditionAsset, required: true, StageObjectiveConditionRole.PrimaryGoal, "primary-goal"),
                    }),
                CreatePlayerSpawn(10, new SurfaceCell(FaceId.Floor, 0, 0)));
            SetPrivateField(stage, "tileFeatures", new[] { tileFeature });

            try
            {
                return StageRuntimeBuilder.Build(stage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static ButtonActivatedConditionAsset CreateButtonActivatedCondition(int tileId)
        {
            var condition = ScriptableObject.CreateInstance<ButtonActivatedConditionAsset>();
            SetPrivateField(condition, "tileId", tileId);
            return condition;
        }

        private static PlayerAtAnyZoneConditionAsset CreatePlayerAtAnyZoneCondition(string zoneId)
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            SetPrivateField(condition, "zoneIds", new[] { zoneId });
            SetPrivateField(condition, "requireAlive", true);
            return condition;
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            StageObjectiveRuntimeDefinition objectiveDefinition,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new TickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                GameplayEntityLogicProviderFactory.CreateDefault(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                playerRespawnDelayTicks: 1,
                objectiveDefinition: objectiveDefinition,
                enemySpawnDefaultsByArchetypeId: null,
                allowPlayerRespawn: true,
                runtimeFeatureFlags: default,
                unitKinematicLocomotionTiming: default,
                playerContinuousLocomotion: default,
                tileFeatureDefinitions: tileFeatureDefinitions,
                tileEffectResolver: null);
        }

        private static GameplayTickViewPresenter CreateInitializedTileRequestPresenter(
            GameObject rootObject,
            StageRuntimeBuildResult buildResult,
            IReadOnlyList<EntityState> initialEntities)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new SimpleViewFactory(registry.transform));

            presenter.Initialize(
                binder,
                buildResult.BoardBounds,
                buildResult.InitialTopology,
                1f,
                GameplayTimingProfile.CreateDefault());
            presenter.PresentInitial(initialEntities, buildResult.InitialTopology);
            return presenter;
        }

        private static RecordingButtonTileFeatureVisualTarget AttachTileVisualTarget(
            GameObject rootObject,
            GameplayTickViewPresenter presenter,
            int tileId,
            SurfaceCell cell)
        {
            var registry = rootObject.GetComponent<TileFeatureVisualRegistry>() ??
                rootObject.AddComponent<TileFeatureVisualRegistry>();
            var targetObject = new GameObject($"TileFeatureVisualTarget_{tileId}");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var target = targetObject.AddComponent<RecordingButtonTileFeatureVisualTarget>();
            target.Configure(tileId, cell);
            registry.ConfigureSearchRoot(rootObject.transform);
            presenter.AttachTileFeatureVisualRegistry(registry);
            return target;
        }

        private sealed class RecordingButtonTileFeatureVisualTarget : MonoBehaviour, ITileFeatureVisualTarget
        {
            public int TileId { get; private set; }

            public SurfaceCell Cell { get; private set; }

            public int DebugPlayButtonActivatedCount { get; private set; }

            public void Configure(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public void PlayButtonActivated()
            {
                DebugPlayButtonActivatedCount++;
            }
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

        private static StageSpawnDefinition CreateBoxSpawn(
            int entityId,
            SurfaceCell cell,
            BoxCapabilities boxCapabilities,
            BoxArchetype boxArchetype = BoxArchetype.Normal)
        {
            return new StageSpawnDefinition
            {
                EntityId = entityId,
                Kind = StageSpawnKind.Box,
                Cell = cell,
                Facing = Direction.Right,
                Hp = 1,
                BoxCapabilities = boxCapabilities,
                BoxArchetype = boxArchetype,
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
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault()).CreateSnapshot();
        }

        private static WorldSnapshot CreateSnapshotWithTileFeatures(
            IEnumerable<TileFeatureState> tileFeatures,
            params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                DefaultBoardBounds,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                tileFeatures).CreateSnapshot();
        }

        private static WorldSnapshot CreateSnapshotWithTileFeatures(
            IEnumerable<TileFeatureState> tileFeatures,
            CubeTopologyState topology,
            params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                DefaultBoardBounds,
                topology,
                GameplayTimingProfile.CreateDefault(),
                tileFeatures).CreateSnapshot();
        }

        private static WorldSnapshot CreateExitSnapshot(
            SurfaceCell exitCell,
            CubeTopologyState topology,
            params EntityState[] entities)
        {
            return CreateSnapshotWithTileFeatures(
                new[] { CreateTileFeatureState(100, exitCell, TileFeatureKind.Exit) },
                topology,
                entities);
        }

        private static StageObjectiveRuntimeDefinition CreateExitObjectiveDefinition(
            SurfaceCell exitCell,
            bool includePrerequisite,
            bool prerequisiteSatisfied,
            TileFeatureActivationRule activationRule = TileFeatureActivationRule.BottomFaceOnly)
        {
            var goalZone = new StageZoneRuntimeDefinition(
                "goal",
                exitCell.face,
                new[]
                {
                    new StageZoneRuntimeRegion(exitCell.PlanarPosition, exitCell.PlanarPosition),
                });
            var entries = new List<StageObjectiveConditionRuntimeDefinitionEntry>();
            if (includePrerequisite)
            {
                entries.Add(new StageObjectiveConditionRuntimeDefinitionEntry(
                    new FixedConditionRuntimeDefinition("required-condition", "Required Condition", prerequisiteSatisfied),
                    required: true,
                    StageObjectiveConditionRole.None,
                    "required-condition"));
            }

            entries.Add(new StageObjectiveConditionRuntimeDefinitionEntry(
                new PlayerAtActiveExitConditionRuntimeDefinition(
                    "primary-goal",
                    "Primary Goal",
                    10,
                    100,
                    new TileFeatureRuntimeDefinition(
                        100,
                        activationRule,
                        Direction2D.None,
                        TileFeatureBoxSelector.None,
                        boundEntityId: 0,
                        presentationKey: string.Empty),
                    goalZone,
                    requireAlive: true),
                required: true,
                StageObjectiveConditionRole.PrimaryGoal,
                "primary-goal"));

            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequireAllConditions,
                10,
                new[] { goalZone },
                entries.ToArray());
        }

        private static StageObjectiveRuntimeDefinition CreateExitObjectiveDefinitionWithTickPrerequisite(
            SurfaceCell exitCell,
            int prerequisiteSatisfiedTick,
            TileFeatureActivationRule activationRule = TileFeatureActivationRule.BottomFaceOnly)
        {
            var goalZone = new StageZoneRuntimeDefinition(
                "goal",
                exitCell.face,
                new[]
                {
                    new StageZoneRuntimeRegion(exitCell.PlanarPosition, exitCell.PlanarPosition),
                });
            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequireAllConditions,
                10,
                new[] { goalZone },
                new[]
                {
                    new StageObjectiveConditionRuntimeDefinitionEntry(
                        new TickThresholdConditionRuntimeDefinition(
                            "required-condition",
                            "Required Condition",
                            prerequisiteSatisfiedTick),
                        required: true,
                        StageObjectiveConditionRole.None,
                        "required-condition"),
                    new StageObjectiveConditionRuntimeDefinitionEntry(
                        new PlayerAtActiveExitConditionRuntimeDefinition(
                            "primary-goal",
                            "Primary Goal",
                            10,
                            100,
                            CreateExitTileFeatureDefinitions(activationRule)[0],
                            goalZone,
                            requireAlive: true),
                        required: true,
                        StageObjectiveConditionRole.PrimaryGoal,
                        "primary-goal"),
                });
        }

        private static TileFeatureRuntimeDefinition[] CreateExitTileFeatureDefinitions(
            TileFeatureActivationRule activationRule = TileFeatureActivationRule.BottomFaceOnly)
        {
            return new[]
            {
                new TileFeatureRuntimeDefinition(
                    100,
                    activationRule,
                    Direction2D.None,
                    TileFeatureBoxSelector.None,
                    boundEntityId: 0,
                    presentationKey: string.Empty),
            };
        }

        private static WorldState CreateExitWorldState(
            SurfaceCell exitCell,
            CubeTopologyState topology,
            params EntityState[] entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                DefaultBoardBounds,
                topology,
                new[] { CreateTileFeatureState(100, exitCell, TileFeatureKind.Exit) });
        }

        private static StageTileFeatureDefinition CreateStageTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            TileFeatureActivationRule activationRule = TileFeatureActivationRule.Always,
            TileFeatureBoxSelector boxSelector = TileFeatureBoxSelector.AnyPushableBox)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = cell,
                Kind = kind,
                ActivationRule = activationRule,
                Direction = Direction2D.None,
                BoxSelector = boxSelector,
                BoundEntityId = 0,
                PresentationKey = string.Empty,
            };
        }

        private static TileFeatureState CreateButtonState(
            int tileId,
            SurfaceCell cell,
            TileFeatureFlags flags = TileFeatureFlags.None)
        {
            return CreateTileFeatureState(tileId, cell, TileFeatureKind.Button, flags);
        }

        private static TileFeatureState CreateTileFeatureState(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            TileFeatureFlags flags = TileFeatureFlags.None)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                flags,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static EntityState CreatePlayerEntity(
            int entityId,
            SurfaceCell cell,
            int hp = 3,
            bool markedForDeath = false,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
            };
        }

        private static EntityState CreateBoxEntity(
            int entityId,
            SurfaceCell cell,
            BoxCapabilities boxCapabilities,
            BoxArchetype boxArchetype = BoxArchetype.Normal)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = boxCapabilities,
                boxArchetype = boxArchetype,
            };
        }

        private static EntityState CreateSlidingBoxEntity(
            int entityId,
            SurfaceCell cell,
            BoxCapabilities boxCapabilities,
            BoxArchetype boxArchetype = BoxArchetype.Normal)
        {
            var box = CreateBoxEntity(entityId, cell, boxCapabilities, boxArchetype);
            box.state = EntityPhaseState.Sliding;
            box.stateTimer = 0;
            box.facing = Direction.Right;
            return box;
        }

        private static EntityState CreateSlideStopWallEntity(int entityId, SurfaceCell cell)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(cell.face, cell.x + 1, cell.y),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemyEntity(int entityId, SurfaceCell cell, int hp = 1, bool markedForDeath = false)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = hp,
                maxHp = Math.Max(1, hp),
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = markedForDeath,
            };
        }

        private static StageObjectiveTickFacts CreateObjectiveTickFacts(int tickIndex)
        {
            return new StageObjectiveTickFacts(
                tickIndex,
                PlayerTickCommand.None,
                Array.Empty<int>(),
                Array.Empty<StageObjectiveDamageFact>());
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

        private sealed class FixedConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            private readonly bool _isSatisfied;

            public FixedConditionRuntimeDefinition(string conditionId, string displayName, bool isSatisfied)
                : base(conditionId, displayName)
            {
                _isSatisfied = isSatisfied;
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new FixedConditionRuntime(ConditionId, DisplayName, _isSatisfied);
            }
        }

        private sealed class TickThresholdConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            private readonly int _satisfiedTickInclusive;

            public TickThresholdConditionRuntimeDefinition(
                string conditionId,
                string displayName,
                int satisfiedTickInclusive)
                : base(conditionId, displayName)
            {
                _satisfiedTickInclusive = satisfiedTickInclusive;
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new TickThresholdConditionRuntime(
                    ConditionId,
                    DisplayName,
                    _satisfiedTickInclusive);
            }
        }

        private sealed class FixedConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly string _displayName;
            private readonly bool _isSatisfied;

            public FixedConditionRuntime(string conditionId, string displayName, bool isSatisfied)
            {
                _conditionId = conditionId;
                _displayName = displayName;
                _isSatisfied = isSatisfied;
            }

            public bool IsSatisfied => _isSatisfied;

            public void Reset()
            {
            }

            public void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
            {
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    nameof(FixedConditionRuntime),
                    _isSatisfied);
            }
        }

        private sealed class TickThresholdConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly string _displayName;
            private readonly int _satisfiedTickInclusive;
            private bool _isSatisfied;

            public TickThresholdConditionRuntime(
                string conditionId,
                string displayName,
                int satisfiedTickInclusive)
            {
                _conditionId = conditionId;
                _displayName = displayName;
                _satisfiedTickInclusive = satisfiedTickInclusive;
            }

            public bool IsSatisfied => _isSatisfied;

            public void Reset()
            {
                _isSatisfied = false;
            }

            public void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
            {
                _isSatisfied = tickFacts.TickIndex >= _satisfiedTickInclusive;
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    nameof(TickThresholdConditionRuntime),
                    _isSatisfied,
                    $"SatisfiedTickInclusive={_satisfiedTickInclusive}|Satisfied={(_isSatisfied ? 1 : 0)}");
            }
        }

        private sealed class SimpleViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;

            public SimpleViewFactory(Transform parent)
            {
                _parent = parent;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                return view;
            }
        }
    }
}
