using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class SunWheelRocketFaceBindingContractTests
    {
        private const string SunWheelPresentationId = "sunwheel";
        private const string RocketFacePresentationId = "rocket_face";
        private const string SunWheelProfileName = "EnemyAi_WallFollower";
        private const string RocketFaceProfileName = "EnemyAi_Charger";

        [Test]
        [Category("Core")]
        public void EnemyBinding_CampaignMain_SunWheelPresentationUsesWallFollowerProfile()
        {
            var occurrence = FindCampaignOccurrences(SunWheelPresentationId).FirstOrDefault();

            Assert.That(occurrence, Is.Not.Null, "CampaignMain must contain at least one SunWheel presentation binding.");
            AssertSunWheelProfile(occurrence.Profile, occurrence.DebugLabel);
        }

        [Test]
        [Category("Core")]
        public void EnemyBinding_CampaignMain_RocketFacePresentationUsesChargeProfile()
        {
            var occurrence = FindCampaignOccurrences(RocketFacePresentationId).FirstOrDefault();

            Assert.That(occurrence, Is.Not.Null, "CampaignMain must contain at least one RocketFace presentation binding.");
            AssertRocketFaceProfile(occurrence.Profile, occurrence.DebugLabel);
        }

        [Test]
        [Category("Extended")]
        public void CampaignStages_SunWheelBindings_AllUseExpectedWallFollowerProfile()
        {
            var occurrences = FindCampaignOccurrences(SunWheelPresentationId).ToArray();

            Assert.That(occurrences, Is.Not.Empty, "CampaignMain must contain SunWheel presentation bindings.");
            foreach (var occurrence in occurrences)
            {
                AssertSunWheelProfile(occurrence.Profile, occurrence.DebugLabel);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignStages_RocketFaceBindings_AllUseExpectedChargeProfile()
        {
            var occurrences = FindCampaignOccurrences(RocketFacePresentationId).ToArray();

            Assert.That(occurrences, Is.Not.Empty, "CampaignMain must contain RocketFace presentation bindings.");
            foreach (var occurrence in occurrences)
            {
                AssertRocketFaceProfile(occurrence.Profile, occurrence.DebugLabel);
            }
        }

        private static void AssertSunWheelProfile(EnemyAiProfile profile, string context)
        {
            Assert.That(profile, Is.Not.Null, context);
            Assert.That(profile.name, Is.EqualTo(SunWheelProfileName), context);
            Assert.That(profile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default), context);
            Assert.That(profile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.WallFollow), context);
            Assert.That(profile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.None), context);
            Assert.That(profile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None), context);

            var runtime = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            Assert.That(runtime.Brain.StateResolver.Kind, Is.EqualTo(EnemyAiStateResolverKind.Default), context);
            Assert.That(runtime.Brain.Patrol.Kind, Is.EqualTo(PatrolStrategyKind.WallFollow), context);
            Assert.That(runtime.Capabilities.TryGetPassiveContact(out _), Is.True, context);
            Assert.That(runtime.Capabilities.TryGetCombat(out _), Is.False, context);
            Assert.That(runtime.Capabilities.TryGetMovementSkill(out _), Is.False, context);
        }

        private static void AssertRocketFaceProfile(EnemyAiProfile profile, string context)
        {
            Assert.That(profile, Is.Not.Null, context);
            Assert.That(profile.name, Is.EqualTo(RocketFaceProfileName), context);
            Assert.That(profile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Charge), context);
            Assert.That(profile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.None), context);
            Assert.That(profile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None), context);

            var runtime = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            Assert.That(runtime.Brain.StateResolver.Kind, Is.EqualTo(EnemyAiStateResolverKind.Charge), context);
            Assert.That(runtime.TryGetChargeBehavior(out var charge), Is.True, context);
            Assert.That(charge.Timing.WindupTicks, Is.GreaterThanOrEqualTo(0), context);
            Assert.That(runtime.Capabilities.TryGetPassiveContact(out _), Is.True, context);
            Assert.That(runtime.Capabilities.TryGetCombat(out _), Is.False, context);
            Assert.That(runtime.Capabilities.TryGetMovementSkill(out _), Is.False, context);
        }

        private static IEnumerable<EnemyBindingOccurrence> FindCampaignOccurrences(string presentationId)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            Assert.That(catalog, Is.Not.Null, $"Missing campaign stage catalog at '{StageContentPaths.StageCatalogAssetPath}'.");

            foreach (var entry in catalog.Entries.Where(entry => entry != null))
            {
                var stage = entry.GameplayDefinition;
                var presentationDefinition = entry.PresentationDefinition;
                Assert.That(stage, Is.Not.Null, $"StageCatalog entry '{entry.name}' is missing gameplay definition.");
                Assert.That(presentationDefinition, Is.Not.Null, $"StageCatalog entry '{entry.name}' is missing presentation definition.");

                var presentation = StagePresentationAssembler.Resolve(presentationDefinition);
                var enemySpawnsById = stage.EnemySpawns.ToDictionary(spawn => spawn.EntityId);

                foreach (var binding in presentation.EnemyPresentationBindings)
                {
                    if (!string.Equals(binding.PresentationId, presentationId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.That(
                        enemySpawnsById.TryGetValue(binding.EntityId, out var spawn),
                        Is.True,
                        $"{entry.StageId.Value} presentation '{presentationId}' entity {binding.EntityId} must join to a gameplay EnemySpawn.");

                    yield return new EnemyBindingOccurrence(
                        entry.StageId.Value,
                        binding.EntityId,
                        binding.PresentationId,
                        spawn.EnemyAiProfile);
                }
            }
        }

        private sealed class EnemyBindingOccurrence
        {
            public EnemyBindingOccurrence(string stageId, int entityId, string presentationId, EnemyAiProfile profile)
            {
                StageId = stageId ?? string.Empty;
                EntityId = entityId;
                PresentationId = presentationId ?? string.Empty;
                Profile = profile;
            }

            public string StageId { get; }

            public int EntityId { get; }

            public string PresentationId { get; }

            public EnemyAiProfile Profile { get; }

            public string DebugLabel => $"Stage={StageId}|E={EntityId}|Presentation={PresentationId}|Profile={(Profile != null ? Profile.name : "<null>")}";
        }
    }
}
