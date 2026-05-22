using System;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxFlagRolloutPolicyTests
    {
        private const string GovernancePath = "Docs/Architecture/Gameplay-VFX-Governance.md";
        private const string RuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string TickPipelinePath = "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs";
        private const string WorldStatePath = "Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs";
        private const string WorldSnapshotPath = "Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs";
        private const string TickPresentationDataPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs";
        private const string CombinedGameplayShowcaseScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string UIAudioScenePath = "Assets/Scenes/UIAudioScene.unity";
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";

        private static readonly FlagInfo[] VfxFlags =
        {
            new(
                "EnableGameplayVfxDamageBurstMigration",
                "enableGameplayVfxDamageBurstMigration"),
            new(
                "EnableGameplayVfxEnemyDamageBurstMigration",
                "enableGameplayVfxEnemyDamageBurstMigration"),
            new(
                "EnableGameplayVfxBoxDestroySmokeMigration",
                "enableGameplayVfxBoxDestroySmokeMigration"),
            new(
                "EnableGameplayVfxBoxDestroyShrinkMigration",
                "enableGameplayVfxBoxDestroyShrinkMigration"),
            new(
                "EnableGameplayVfxItemConsumeBurstMigration",
                "enableGameplayVfxItemConsumeBurstMigration"),
            new(
                "EnableEnemyJumpLandingDustVfx",
                "enableEnemyJumpLandingDustVfx"),
            new(
                "EnableGameplayVfxBoxSlideTrail",
                "enableGameplayVfxBoxSlideTrail"),
            new(
                "EnableGameplayVfxBoxSlideSolidStop",
                "enableGameplayVfxBoxSlideSolidStop"),
            new(
                "EnableGameplayVfxImpactTransientBreakMigration",
                "enableGameplayVfxImpactTransientBreakMigration"),
            new(
                "EnableGameplayVfxOutOfBoundsExitMigration",
                "enableGameplayVfxOutOfBoundsExitMigration"),
            new(
                "EnableEnemyJumpTargetVfx",
                "enableEnemyJumpTargetVfx"),
            new(
                "EnableGameplayVfxUtilityWindupMigration",
                "enableGameplayVfxUtilityWindupMigration"),
            new(
                "EnableGameplayVfxFrontFaceShieldActiveMigration",
                "enableGameplayVfxFrontFaceShieldActiveMigration"),
            new(
                "EnableGameplayVfxFrontFaceShieldBlockMigration",
                "enableGameplayVfxFrontFaceShieldBlockMigration"),
            new(
                "EnableGameplayVfxFrontFaceShieldWindupMigration",
                "enableGameplayVfxFrontFaceShieldWindupMigration"),
            new(
                "EnableGameplayVfxFlipImpactBurstMigration",
                "enableGameplayVfxFlipImpactBurstMigration"),
            new(
                "EnableGameplayVfxEnemyDeathBurstMigration",
                "enableGameplayVfxEnemyDeathBurstMigration"),
            new(
                "EnableGameplayVfxEnemyDeathMotionMigration",
                "enableGameplayVfxEnemyDeathMotionMigration"),
            new(
                "EnableGameplayVfxFlipDestroySelfMotionMigration",
                "enableGameplayVfxFlipDestroySelfMotionMigration"),
            new(
                "EnableGameplayVfxFlipImpactStayTrail",
                "enableGameplayVfxFlipImpactStayTrail"),
            new(
                "EnableGameplayVfxGlideWindTrail",
                "enableGameplayVfxGlideWindTrail"),
            new(
                "EnableGameplayVfxChargeBoosterTrail",
                "enableGameplayVfxChargeBoosterTrail"),
            new(
                "EnableGameplayVfxEnemyUtilityCooldownAura",
                "enableGameplayVfxEnemyUtilityCooldownAura"),
            new(
                "EnableGameplayVfxTileFeatureLane",
                "enableGameplayVfxTileFeatureLane"),
            new(
                "EnableGameplayVfxGravityFieldEvents",
                "enableGameplayVfxGravityFieldEvents"),
            new(
                "EnableGameplayVfxGravityFieldContinuous",
                "enableGameplayVfxGravityFieldContinuous"),
            new(
                "EnableGameplayVfxGravityFieldLockedTarget",
                "enableGameplayVfxGravityFieldLockedTarget"),
        };

        [Test]
        [Category("Extended")]
        public void RuntimeDefaults_AllCurrentVfxFlagsAreDefaultOn()
        {
            var owner = new GameObject("GameplayVfxFlagRolloutDefaults");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                foreach (var flag in VfxFlags)
                {
                    var property = typeof(GameplayVfxProductionRuntime).GetProperty(flag.PropertyName);
                    Assert.That(property, Is.Not.Null, $"{flag.PropertyName} must remain a public VFX enable flag.");
                    Assert.That(property.GetValue(runtime), Is.True, $"{flag.PropertyName} must default true.");
                }
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxProductionRuntime_DoesNotExposeLegacySuppressAliases()
        {
            var suppressAliasNames = typeof(GameplayVfxProductionRuntime)
                .GetProperties()
                .Select(property => property.Name)
                .Where(name => name.StartsWith("SuppressLegacy", StringComparison.Ordinal))
                .ToArray();

            Assert.That(suppressAliasNames, Is.Empty);

            var runtimeSource = ReadRepoFile(RuntimePath);
            Assert.That(runtimeSource, Does.Not.Contain("SuppressLegacy"));

            var document = ReadRepoFile(GovernancePath);
            Assert.That(document, Does.Contain("flag off disables that VFX and does not restore old presenter fallback"));
        }

        [Test]
        [Category("Extended")]
        public void ShowcaseScene_FlagsAreExplicit()
        {
            var combinedScene = ReadRepoFile(CombinedGameplayShowcaseScenePath);
            Assert.That(combinedScene, Does.Contain("enableEnemyJumpTargetVfx: 1"));
            Assert.That(combinedScene, Does.Contain("enableEnemyJumpLandingDustVfx: 1"));

            var uiAudioScene = ReadRepoFile(UIAudioScenePath);
            foreach (var flag in VfxFlags)
            {
                Assert.That(uiAudioScene, Does.Contain($"{flag.SerializedFieldName}: 1"), $"{flag.PropertyName} must be explicit in UIAudioScene review override.");
            }

            var tutorialScene = ReadRepoFile(TutorialScenePath);
            foreach (var flag in VfxFlags)
            {
                Assert.That(tutorialScene, Does.Contain($"{flag.SerializedFieldName}: 0"), $"{flag.PropertyName} must be explicit off in TutorialScene.");
                Assert.That(tutorialScene, Does.Not.Contain($"{flag.SerializedFieldName}: 1"), $"{flag.PropertyName} must not be on in TutorialScene.");
            }
        }

        [Test]
        [Category("Extended")]
        public void NoTickPipelineWorldStateChanges()
        {
            var runtimeSource = ReadRepoFile(RuntimePath);

            Assert.That(runtimeSource, Does.Not.Contain("WorldState"));
            Assert.That(runtimeSource, Does.Not.Contain("WorldSnapshot"));
            Assert.That(runtimeSource, Does.Not.Contain("TickPipeline"));

            Assert.That(ReadRepoFile(TickPipelinePath), Does.Not.Contain("GameplayVfx"));
            Assert.That(ReadRepoFile(WorldStatePath), Does.Not.Contain("GameplayVfx"));
            Assert.That(ReadRepoFile(WorldSnapshotPath), Does.Not.Contain("GameplayVfx"));
            Assert.That(ReadRepoFile(TickPresentationDataPath), Does.Not.Contain("GameplayVfx"));
        }

        private static string ReadRepoFile(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"Missing repo file: {path}");
            return File.ReadAllText(path);
        }

        private readonly struct FlagInfo
        {
            public FlagInfo(
                string propertyName,
                string serializedFieldName)
            {
                PropertyName = propertyName;
                SerializedFieldName = serializedFieldName;
            }

            public string PropertyName { get; }

            public string SerializedFieldName { get; }
        }
    }
}
