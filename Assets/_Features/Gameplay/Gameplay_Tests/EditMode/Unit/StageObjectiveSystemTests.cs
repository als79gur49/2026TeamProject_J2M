using System;
using System.Collections.Generic;
using System.Linq;
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
        public void ObjectiveClear_PlayerOnGoal_WithSatisfiedCondition_Clears()
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
                    goalZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: new StageConditionAsset[] { condition });
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
        public void ObjectiveClear_RequireAllConditions_ClearsWithoutGoalZone()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 20);
                SetPrivateField(condition, "zoneId", "target");
                SetPrivateField(condition, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    goalZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: new StageConditionAsset[] { condition },
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
        public void ObjectiveClear_RequireAllConditions_UnmetConditionDoesNotClear()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 20);
                SetPrivateField(condition, "zoneId", "target");
                SetPrivateField(condition, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    goalZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: new StageConditionAsset[] { condition },
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
        public void ObjectiveClear_RequireAllConditions_LegacyGoalZoneDoesNotStrengthenClear()
        {
            var condition = ScriptableObject.CreateInstance<SpecificEntityAtZoneConditionAsset>();

            try
            {
                SetPrivateField(condition, "entityId", 20);
                SetPrivateField(condition, "zoneId", "target");
                SetPrivateField(condition, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    goalZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(2, 2, 2, 2)),
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: new StageConditionAsset[] { condition },
                    completionPolicy: StageCompletionPolicy.RequireAllConditions);
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
                    status.ConditionId == "legacy-primary-goal" &&
                    status.Required == false), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveClear_ExplicitPrimaryGoalWithoutLegacyGoalZones_ClearsWithOtherConditions()
        {
            var primaryGoal = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            var allEnemiesDefeated = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();

            try
            {
                SetPrivateField(primaryGoal, "zoneIds", new[] { "goal" });
                SetPrivateField(primaryGoal, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    goalZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: Array.Empty<StageConditionAsset>(),
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
                Assert.That(blocked.GoalReached, Is.True);
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
        public void ObjectiveClear_ExplicitPrimaryGoalSuppressesLegacySyntheticPrimary()
        {
            var explicitPrimary = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();

            try
            {
                SetPrivateField(explicitPrimary, "zoneIds", new[] { "explicit-goal" });
                SetPrivateField(explicitPrimary, "requireAlive", true);
                var objective = BuildObjectiveDefinition(
                    goalZoneIds: new[] { "legacy-goal" },
                    zones: new[]
                    {
                        CreateZone("legacy-goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
                        CreateZone("explicit-goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: Array.Empty<StageConditionAsset>(),
                    completionPolicy: StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                    timing: StageSimulationTiming.Default,
                    conditionEntries: new[]
                    {
                        CreateConditionEntry(explicitPrimary, required: true, StageObjectiveConditionRole.PrimaryGoal, "explicit-primary"),
                    });
                var tracker = objective.CreateTracker();

                var result = tracker.Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                    CreateObjectiveTickFacts(1));

                Assert.That(result.GoalReached, Is.False);
                Assert.That(result.IsCleared, Is.False);
                Assert.That(
                    result.ConditionStatuses.Count(status => status.Role == StageObjectiveConditionRole.PrimaryGoal),
                    Is.EqualTo(1));
                Assert.That(
                    result.ConditionStatuses.Any(status => status.ConditionId == "legacy-primary-goal"),
                    Is.False);
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
            var legacyRequired = ScriptableObject.CreateInstance<SpecificEntityRemovedConditionAsset>();
            var explicitOptional = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();

            try
            {
                legacyRequired.name = "RemoveNinetyNine";
                SetPrivateField(legacyRequired, "entityId", 99);
                var objective = BuildObjectiveDefinition(
                    goalZoneIds: new[] { "goal" },
                    zones: new[]
                    {
                        CreateZone("goal", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: new StageConditionAsset[] { legacyRequired },
                    completionPolicy: StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                    timing: StageSimulationTiming.Default,
                    conditionEntries: new[]
                    {
                        CreateConditionEntry(explicitOptional, required: false, StageObjectiveConditionRole.Challenge, "explicit-optional"),
                    });

                var result = objective.CreateTracker().Advance(
                    CreateSnapshot(CreatePlayerEntity(10, new SurfaceCell(FaceId.Floor, 1, 1))),
                    CreateObjectiveTickFacts(1));

                CollectionAssert.AreEqual(
                    new[] { "legacy-required-0-RemoveNinetyNine", "explicit-optional", "legacy-primary-goal" },
                    result.ConditionStatuses.Select(status => status.ConditionId).ToArray());
                Assert.That(
                    result.ConditionStatuses.Count(status => status.Role == StageObjectiveConditionRole.PrimaryGoal),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacyRequired);
                UnityEngine.Object.DestroyImmediate(explicitOptional);
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
                    Array.Empty<string>(),
                    Array.Empty<StageConditionAsset>()),
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
                        Array.Empty<string>(),
                        new StageConditionAsset[] { condition }),
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
                    CreateActiveObjective(new[] { "goal" }, new StageConditionAsset[] { condition }),
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
                        Array.Empty<string>(),
                        conditionEntries: new[]
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
                        Array.Empty<string>(),
                        conditionEntries: new[]
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
                        Array.Empty<string>(),
                        conditionEntries: new[]
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
                        Array.Empty<string>(),
                        conditionEntries: new[]
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
                    goalZoneIds: Array.Empty<string>(),
                    zones: new[]
                    {
                        CreateZone("target", FaceId.Floor, CreateRegion(1, 1, 1, 1)),
                    },
                    requiredConditions: new StageConditionAsset[] { zoneCondition, timeCondition },
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
            return BuildSingleConditionRuntime(conditionAsset, StageSimulationTiming.Default, zones);
        }

        private static IStageConditionRuntime BuildSingleConditionRuntime(
            StageConditionAsset conditionAsset,
            StageSimulationTiming timing,
            StageZoneDefinition[] zones = null)
        {
            var effectiveZones = zones ?? new[]
            {
                CreateZone("goal", FaceId.Floor, CreateRegion(0, 0, 0, 0)),
            };
            var objective = BuildObjectiveDefinition(
                goalZoneIds: new[] { effectiveZones[0].ZoneId },
                zones: effectiveZones,
                requiredConditions: new[] { conditionAsset },
                completionPolicy: StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                timing: timing);

            return objective.RequiredConditions[0].CreateRuntime();
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            string[] goalZoneIds,
            StageZoneDefinition[] zones,
            StageConditionAsset[] requiredConditions = null,
            StageCompletionPolicy completionPolicy = StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions)
        {
            return BuildObjectiveDefinition(
                goalZoneIds,
                zones,
                requiredConditions,
                completionPolicy,
                StageSimulationTiming.Default);
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            string[] goalZoneIds,
            StageZoneDefinition[] zones,
            StageConditionAsset[] requiredConditions,
            StageCompletionPolicy completionPolicy,
            StageSimulationTiming timing)
        {
            return BuildObjectiveDefinition(
                goalZoneIds,
                zones,
                requiredConditions,
                completionPolicy,
                timing,
                conditionEntries: null);
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveDefinition(
            string[] goalZoneIds,
            StageZoneDefinition[] zones,
            StageConditionAsset[] requiredConditions,
            StageCompletionPolicy completionPolicy,
            StageSimulationTiming timing,
            StageObjectiveConditionEntry[] conditionEntries)
        {
            var stage = CreateStage(
                "ObjectiveStage",
                CreateBoard(),
                zones,
                CreateObjective(completionPolicy, goalZoneIds, requiredConditions, conditionEntries),
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
            return CreateObjective(
                StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                goalZoneIds,
                requiredConditions);
        }

        private static StageObjectiveAuthoring CreateObjective(
            StageCompletionPolicy completionPolicy,
            string[] goalZoneIds,
            StageConditionAsset[] requiredConditions = null,
            StageObjectiveConditionEntry[] conditionEntries = null)
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = completionPolicy,
                GoalZoneIds = goalZoneIds ?? Array.Empty<string>(),
                RequiredConditions = requiredConditions ?? Array.Empty<StageConditionAsset>(),
                ConditionEntries = conditionEntries ?? Array.Empty<StageObjectiveConditionEntry>(),
            };
        }

        private static StageObjectiveConditionEntry CreateConditionEntry(
            StageConditionAsset condition,
            bool required,
            StageObjectiveConditionRole role,
            string stableConditionId = "")
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = required,
                Role = role,
                StableConditionId = stableConditionId,
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
    }
}
