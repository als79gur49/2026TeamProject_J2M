using System;
using System.IO;
using System.Linq;
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
        private const string UIAudioScenePath = "Assets/Scenes/UIAudioScene.unity";

        private static readonly FlagInfo[] LiveVfxFlags =
        {
            new("EnableEnemyJumpLandingDustVfx", "enableEnemyJumpLandingDustVfx"),
            new("EnableGameplayVfxBoxSlideTrail", "enableGameplayVfxBoxSlideTrail"),
            new("EnableGameplayVfxBoxSlideSolidStop", "enableGameplayVfxBoxSlideSolidStop"),
            new("EnableEnemyJumpTargetVfx", "enableEnemyJumpTargetVfx"),
            new("EnableGameplayVfxFlipImpactStayTrail", "enableGameplayVfxFlipImpactStayTrail"),
            new("EnableGameplayVfxGlideWindTrail", "enableGameplayVfxGlideWindTrail"),
            new("EnableGameplayVfxChargeBoosterTrail", "enableGameplayVfxChargeBoosterTrail"),
            new("EnableGameplayVfxEnemyUtilityCooldownAura", "enableGameplayVfxEnemyUtilityCooldownAura"),
            new("EnableGameplayVfxTileFeatureLane", "enableGameplayVfxTileFeatureLane"),
            new("EnableGameplayVfxGravityFieldEvents", "enableGameplayVfxGravityFieldEvents"),
            new("EnableGameplayVfxGravityFieldContinuous", "enableGameplayVfxGravityFieldContinuous"),
            new("EnableGameplayVfxGravityFieldLockedTarget", "enableGameplayVfxGravityFieldLockedTarget"),
            new("EnableGameplayVfxForwardCellProjectile", "enableGameplayVfxForwardCellProjectile"),
        };

        private static readonly string[] RemovedMigrationFlagNames =
        {
            BuildRemovedFlagName("DamageBurst"),
            BuildRemovedFlagName("EnemyDamageBurst"),
            BuildRemovedFlagName("EnemyDeathBurst"),
            BuildRemovedFlagName("EnemyDeathMotion"),
            BuildRemovedFlagName("BoxDestroySmoke"),
            BuildRemovedFlagName("BoxDestroyShrink"),
            BuildRemovedFlagName("ItemConsumeBurst"),
            BuildRemovedFlagName("FlipImpactBurst"),
            BuildRemovedFlagName("FlipDestroySelfMotion"),
            BuildRemovedFlagName("ImpactTransientBreak"),
            BuildRemovedFlagName("OutOfBoundsExit"),
            BuildRemovedFlagName("UtilityWindup"),
            BuildRemovedFlagName("FrontFaceShieldActive"),
            BuildRemovedFlagName("FrontFaceShieldBlock"),
            BuildRemovedFlagName("FrontFaceShieldWindup"),
        };

        [Test]
        [Category("Extended")]
        public void RuntimeDefaults_AllCurrentLiveVfxFlagsAreDefaultOn()
        {
            var owner = new GameObject("GameplayVfxFlagRolloutDefaults");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                foreach (var flag in LiveVfxFlags)
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
            Assert.That(document, Does.Contain("Migrated cues are now canonical Gameplay VFX playback and are not scene/public flag gated."));
        }

        [Test]
        [Category("Extended")]
        public void CanonicalGameplayShell_LiveFlagsAreExplicitAndRemovedFlagsStayDeleted()
        {
            var uiAudioScene = ReadRepoFile(UIAudioScenePath);
            foreach (var flag in LiveVfxFlags)
            {
                Assert.That(uiAudioScene, Does.Contain($"{flag.SerializedFieldName}: 1"), $"{flag.PropertyName} must be explicit in UIAudioScene review override.");
            }

            foreach (var propertyName in RemovedMigrationFlagNames)
            {
                Assert.That(uiAudioScene, Does.Not.Contain(ToSerializedFieldName(propertyName)));
            }
        }

        [Test]
        [Category("Extended")]
        public void RemovedMigrationRolloutFlags_DoNotExistInRuntimeContract()
        {
            var runtimeSource = ReadRepoFile(RuntimePath);
            var runtimeProperties = typeof(GameplayVfxProductionRuntime)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            foreach (var propertyName in RemovedMigrationFlagNames)
            {
                var serializedFieldName = ToSerializedFieldName(propertyName);

                Assert.That(runtimeProperties, Does.Not.Contain(propertyName), $"{propertyName} must not remain a public rollout flag.");
                Assert.That(runtimeSource, Does.Not.Contain(propertyName), $"{propertyName} must not remain in runtime source.");
                Assert.That(runtimeSource, Does.Not.Contain(serializedFieldName), $"{serializedFieldName} must not remain as a serialized field.");
            }

            Assert.That(runtimeSource, Does.Contain("CanonicalMigratedGameplayVfxEnabled"));
            Assert.That(runtimeSource, Does.Contain("IsCanonicalMigratedCue"));
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

        private static string BuildRemovedFlagName(string cueName)
        {
            var prefix = "Enable" + "GameplayVfx";
            var suffix = "Mig" + "ration";
            return prefix + cueName + suffix;
        }

        private static string ToSerializedFieldName(string propertyName)
        {
            return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        }

        private readonly struct FlagInfo
        {
            public FlagInfo(string propertyName, string serializedFieldName)
            {
                PropertyName = propertyName;
                SerializedFieldName = serializedFieldName;
            }

            public string PropertyName { get; }

            public string SerializedFieldName { get; }
        }
    }
}
