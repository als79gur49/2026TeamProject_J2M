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
    [Category("Core")]
    [Category("Phase3BGate")]
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

        private static readonly FlagInfo[] MigrationFlags =
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
                "EnableGameplayVfxImpactTransientBreakMigration",
                "enableGameplayVfxImpactTransientBreakMigration"),
            new(
                "EnableGameplayVfxOutOfBoundsExitMigration",
                "enableGameplayVfxOutOfBoundsExitMigration"),
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
        };

        [Test]
        [Category("Extended")]
        public void RuntimeDefaults_RetainedMigrationFlagsAreDefaultOn()
        {
            var owner = new GameObject("GameplayVfxFlagRolloutDefaults");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                foreach (var flag in MigrationFlags)
                {
                    var property = typeof(GameplayVfxProductionRuntime).GetProperty(flag.PropertyName);
                    Assert.That(property, Is.Not.Null, $"{flag.PropertyName} must remain during Phase 3A serialized residue cleanup deferral.");
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
            Assert.That(document, Does.Contain("canonical Gameplay VFX runtime path"));
        }

        [Test]
        [Category("Extended")]
        public void RuntimePolicy_DoesNotRequireRawScenePathFixturesForMigrationResidue()
        {
            var document = ReadRepoFile(GovernancePath);
            Assert.That(document, Does.Contain("UIAudioScene shell plus StageId/profile ownership"));
            Assert.That(document, Does.Contain("Raw scene-path VFX policy fixtures are deprecated"));

            foreach (var flag in MigrationFlags)
            {
                Assert.That(
                    typeof(GameplayVfxProductionRuntime).GetProperty(flag.PropertyName),
                    Is.Not.Null,
                    $"{flag.PropertyName} remains only as serialized compatibility surface until Phase 3B field cleanup.");
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSource_MigrationFieldsAreCompatibilityResidueNotCueGates()
        {
            var runtimeSource = ReadRepoFile(RuntimePath);

            Assert.That(runtimeSource, Does.Contain("Phase 3A: retained only for Unity scene serialization compatibility"));
            Assert.That(runtimeSource, Does.Contain("IsCanonicalMigratedCue"));
            Assert.That(runtimeSource, Does.Contain("CanonicalMigratedGameplayVfxEnabled"));
            Assert.That(runtimeSource, Does.Not.Contain("enableGameplayVfxDamageBurstMigration && cueId"));
            Assert.That(runtimeSource, Does.Not.Contain("enableGameplayVfxEnemyDeathMotionMigration &&"));
            Assert.That(runtimeSource, Does.Not.Contain("enableGameplayVfxFlipDestroySelfMotionMigration &&"));
            Assert.That(runtimeSource, Does.Not.Contain("enableGameplayVfxImpactTransientBreakMigration &&"));
            Assert.That(runtimeSource, Does.Not.Contain("enableGameplayVfxOutOfBoundsExitMigration &&"));
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
