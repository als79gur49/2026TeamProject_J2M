using System;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;

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

        private static readonly string[] FormerMigrationCueNames =
        {
            "DamageBurst",
            "EnemyDamageBurst",
            "BoxDestroySmoke",
            "BoxDestroyShrink",
            "ItemConsumeBurst",
            "ImpactTransientBreak",
            "OutOfBoundsExit",
            "UtilityWindup",
            "FrontFaceShieldActive",
            "FrontFaceShieldBlock",
            "FrontFaceShieldWindup",
            "FlipImpactBurst",
            "EnemyDeathBurst",
            "EnemyDeathMotion",
            "FlipDestroySelfMotion",
        };

        private static readonly string[] ScenePaths =
        {
            ScenePath("UIAudioScene"),
            ScenePath("CombinedGameplayShowcase"),
            ScenePath("TutorialScene"),
        };

        [Test]
        [Category("Extended")]
        public void RuntimeSurface_FormerMigrationFlagsAreDeleted()
        {
            var runtimeProperties = typeof(GameplayVfxProductionRuntime)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();
            var runtimeSource = ReadRepoFile(RuntimePath);

            foreach (var cueName in FormerMigrationCueNames)
            {
                Assert.That(runtimeProperties, Does.Not.Contain(PropertyNameFor(cueName)), cueName);
                Assert.That(runtimeSource, Does.Not.Contain(SerializedFieldNameFor(cueName)), cueName);
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
        public void RuntimePolicy_DoesNotRequireRawScenePathFixturesForFormerMigrationResidue()
        {
            var document = ReadRepoFile(GovernancePath);
            Assert.That(document, Does.Contain("UIAudioScene shell plus StageId/profile ownership"));
            Assert.That(document, Does.Contain("Raw scene-path VFX policy fixtures are deprecated"));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSource_FormerMigrationFieldsAreDeletedAndCanonicalCueGateRemains()
        {
            var runtimeSource = ReadRepoFile(RuntimePath);

            Assert.That(runtimeSource, Does.Contain("IsCanonicalMigratedCue"));
            Assert.That(runtimeSource, Does.Contain("CanonicalMigratedGameplayVfxEnabled"));
            foreach (var cueName in FormerMigrationCueNames)
            {
                Assert.That(runtimeSource, Does.Not.Contain(PropertyNameFor(cueName)), cueName);
                Assert.That(runtimeSource, Does.Not.Contain(SerializedFieldNameFor(cueName)), cueName);
            }
        }

        [Test]
        [Category("Extended")]
        public void SceneYaml_FormerMigrationResidueIsDeleted()
        {
            foreach (var scenePath in ScenePaths)
            {
                var sceneYaml = ReadRepoFile(scenePath);
                foreach (var cueName in FormerMigrationCueNames)
                {
                    Assert.That(sceneYaml, Does.Not.Contain(SerializedFieldNameFor(cueName)), $"{scenePath}: {cueName}");
                }
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

        private static string ScenePath(string sceneName)
        {
            return Path.Combine("Assets", "Scenes", sceneName + ".unity").Replace('\\', '/');
        }

        private static string PropertyNameFor(string cueName)
        {
            return string.Concat("Enable", "Gameplay", "Vfx", cueName, "Migration");
        }

        private static string SerializedFieldNameFor(string cueName)
        {
            return string.Concat("enable", "Gameplay", "Vfx", cueName, "Migration");
        }
    }
}
