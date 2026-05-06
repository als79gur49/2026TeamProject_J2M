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
            new("EnableEnemyJumpTargetVfx", "enableEnemyJumpTargetVfx", "EnemyVfxCue.JumperLandingTarget"),
            new("EnableEnemyJumpLandingDustVfx", "enableEnemyJumpLandingDustVfx", "EnemyVfxCue.JumperLandingDust"),
            new("EnableGameplayVfxDamageBurstMigration", "enableGameplayVfxDamageBurstMigration", "PlayerVfxCue.Damage"),
            new("EnableGameplayVfxEnemyDamageBurstMigration", "enableGameplayVfxEnemyDamageBurstMigration", "EnemyVfxCue.Damage"),
            new("EnableGameplayVfxBoxDestroySmokeMigration", "enableGameplayVfxBoxDestroySmokeMigration", "BoxVfxCue.DestroySmoke"),
            new("EnableGameplayVfxItemConsumeBurstMigration", "enableGameplayVfxItemConsumeBurstMigration", "BoxVfxCue.ItemConsume"),
            new("EnableGameplayVfxEnemyDeathBurstMigration", "enableGameplayVfxEnemyDeathBurstMigration", "EnemyVfxCue.Death"),
            new("EnableGameplayVfxEnemyDeathMotionMigration", "enableGameplayVfxEnemyDeathMotionMigration", "EnemyVfxCue.DeathMotion"),
            new("EnableGameplayVfxUtilityWindupMigration", "enableGameplayVfxUtilityWindupMigration", "EnemyVfxCue.UtilityWindup"),
            new("EnableGameplayVfxFrontFaceShieldActiveMigration", "enableGameplayVfxFrontFaceShieldActiveMigration", "EnemyVfxCue.FrontFaceShieldActive"),
            new("EnableGameplayVfxFrontFaceShieldBlockMigration", "enableGameplayVfxFrontFaceShieldBlockMigration", "EnemyVfxCue.FrontFaceShieldBlock"),
            new("EnableGameplayVfxFlipImpactBurstMigration", "enableGameplayVfxFlipImpactBurstMigration", "BoxVfxCue.FlipImpactBurst"),
            new("EnableGameplayVfxFlipDestroySelfMotionMigration", "enableGameplayVfxFlipDestroySelfMotionMigration", "BoxVfxCue.FlipDestroySelfMotion"),
            new("EnableGameplayVfxBoxSlideTrail", "enableGameplayVfxBoxSlideTrail", "BoxVfxCue.SlideDustTrail"),
        };

        [Test]
        [Category("Extended")]
        public void RuntimeDefaults_AreProductionSafe()
        {
            var owner = new GameObject("GameplayVfxFlagRolloutDefaults");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                foreach (var flag in VfxFlags)
                {
                    var property = typeof(GameplayVfxProductionRuntime).GetProperty(flag.PropertyName);
                    Assert.That(property, Is.Not.Null, $"{flag.PropertyName} must remain a public rollout flag.");
                    Assert.That(property.GetValue(runtime), Is.False, $"{flag.PropertyName} must default false.");
                }

                Assert.That(runtime.SuppressLegacyPlayerDamageHitEffects, Is.False);
                Assert.That(runtime.SuppressLegacyBoxDestroySmokeEffects, Is.False);
                Assert.That(runtime.SuppressLegacyItemConsumeEffects, Is.False);
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.False);
                Assert.That(runtime.SuppressLegacyFlipDestroySelfEffects, Is.False);
                Assert.That(runtime.SuppressLegacyUtilityWindupVfx, Is.False);
                Assert.That(runtime.SuppressLegacyFrontFaceShieldActiveVfx, Is.False);
                Assert.That(runtime.SuppressLegacyFrontFaceShieldBlockVfx, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathSuppressOwnedByDeathMotionFlag()
        {
            var owner = new GameObject("GameplayVfxEnemyDeathSuppressOwner");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.EnableGameplayVfxEnemyDeathBurstMigration = true;
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.False);

                runtime.EnableGameplayVfxEnemyDeathMotionMigration = true;
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.True);

                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.True);

                runtime.EnableGameplayVfxEnemyDeathMotionMigration = false;
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void MigrationMissingBinding_NoOldFallbackPolicy_Documented()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("migration flag is on and its binding, prefab, anchor, source pose, or target context is missing"));
            Assert.That(document, Does.Contain("diagnostic/no-op and does not fall back to the old presenter path"));
            Assert.That(document, Does.Contain("missing DeathMotion binding, prefab, source pose, output camera, or target context is diagnostic/no-op"));
            Assert.That(document, Does.Contain("missing binding under the true flag is diagnostic/no-op"));
        }

        [Test]
        [Category("Extended")]
        public void AugmentationFlags_DoNotExposeLegacySuppressGates()
        {
            var suppressGateNames = typeof(IGameplayPresentationMigrationGate)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            Assert.That(suppressGateNames, Does.Not.Contain("SuppressLegacyEnemyDamageBurstEffects"));
            Assert.That(suppressGateNames, Does.Not.Contain("SuppressLegacyEnemyJumpTargetVfx"));
            Assert.That(suppressGateNames, Does.Not.Contain("SuppressLegacyEnemyJumpLandingDustVfx"));
            Assert.That(suppressGateNames, Does.Not.Contain("SuppressLegacyBoxSlideTrailEffects"));

            var document = ReadRepoFile(GovernancePath);
            Assert.That(document, Does.Contain("Augmentation flags do not own legacy fallback or suppress gates."));
            Assert.That(document, Does.Contain("EnableGameplayVfxBoxSlideTrail"));
            Assert.That(document, Does.Contain("no legacy suppress gate"));
        }

        [Test]
        [Category("Extended")]
        public void ShowcaseScene_FlagsAreExplicit()
        {
            var combinedScene = ReadRepoFile(CombinedGameplayShowcaseScenePath);
            Assert.That(combinedScene, Does.Contain("enableEnemyJumpTargetVfx: 1"));
            Assert.That(combinedScene, Does.Contain("enableEnemyJumpLandingDustVfx: 1"));
            foreach (var flag in VfxFlags.Where(flag => !flag.PropertyName.StartsWith("EnableEnemyJump", StringComparison.Ordinal)))
            {
                Assert.That(combinedScene, Does.Not.Contain($"{flag.SerializedFieldName}: 1"));
            }

            var uiAudioScene = ReadRepoFile(UIAudioScenePath);
            foreach (var flag in VfxFlags)
            {
                Assert.That(uiAudioScene, Does.Contain($"{flag.SerializedFieldName}: 1"), $"{flag.PropertyName} must be explicit in UIAudioScene review override.");
            }

            var tutorialScene = ReadRepoFile(TutorialScenePath);
            Assert.That(tutorialScene, Does.Contain("enableEnemyJumpTargetVfx: 0"));
            foreach (var flag in VfxFlags)
            {
                Assert.That(tutorialScene, Does.Not.Contain($"{flag.SerializedFieldName}: 1"), $"{flag.PropertyName} must not be on in TutorialScene.");
            }
        }

        [Test]
        [Category("Extended")]
        public void Governance_DocumentsFlagRolloutPolicy()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## Gameplay VFX Flag Rollout Policy"));
            Assert.That(document, Does.Contain("Production runtime defaults are default false first."));
            Assert.That(document, Does.Contain("Scene-local overrides are separate from runtime defaults"));
            Assert.That(document, Does.Contain("High-risk parameterized motion and clone/source-view VFX require manual visual validation"));
            Assert.That(document, Does.Contain("`EnableGameplayVfxEnemyDeathMotionMigration` owns `SuppressLegacyEnemyDeathEffects`"));
            Assert.That(document, Does.Contain("`EnableGameplayVfxEnemyDeathBurstMigration` does not suppress the old enemy death fly-away"));
        }

        [Test]
        [Category("Extended")]
        public void AllVfxFlags_AreListedInGovernance()
        {
            var document = ReadRepoFile(GovernancePath);
            var runtimeFlagNames = typeof(GameplayVfxProductionRuntime)
                .GetProperties()
                .Where(property => property.PropertyType == typeof(bool))
                .Select(property => property.Name)
                .Where(name => name.StartsWith("Enable", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var expectedFlagNames = VfxFlags
                .Select(flag => flag.PropertyName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(expectedFlagNames, runtimeFlagNames);
            foreach (var flag in VfxFlags)
            {
                Assert.That(document, Does.Contain($"`{flag.PropertyName}`"));
                Assert.That(document, Does.Contain($"`{flag.CueName}`"));
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
            public FlagInfo(string propertyName, string serializedFieldName, string cueName)
            {
                PropertyName = propertyName;
                SerializedFieldName = serializedFieldName;
                CueName = cueName;
            }

            public string PropertyName { get; }

            public string SerializedFieldName { get; }

            public string CueName { get; }
        }
    }
}
