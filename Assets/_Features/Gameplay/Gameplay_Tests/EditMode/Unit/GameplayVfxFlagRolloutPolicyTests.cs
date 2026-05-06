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
                "enableGameplayVfxDamageBurstMigration",
                "PlayerVfxCue.Damage",
                "Migration",
                "Tier 1",
                "targeted tests + visual spot check"),
            new(
                "EnableGameplayVfxEnemyDamageBurstMigration",
                "enableGameplayVfxEnemyDamageBurstMigration",
                "EnemyVfxCue.Damage",
                "Augmentation-style VFX lane",
                "Tier 1",
                "targeted tests + visual spot check"),
            new(
                "EnableGameplayVfxBoxDestroySmokeMigration",
                "enableGameplayVfxBoxDestroySmokeMigration",
                "BoxVfxCue.DestroySmoke",
                "Migration",
                "Tier 1",
                "targeted tests + visual spot check"),
            new(
                "EnableGameplayVfxBoxDestroyShrinkMigration",
                "enableGameplayVfxBoxDestroyShrinkMigration",
                "BoxVfxCue.DestroyShrink",
                "Migration / parameterized clone motion",
                "Tier 2",
                "manual visual approval + targeted regression"),
            new(
                "EnableGameplayVfxItemConsumeBurstMigration",
                "enableGameplayVfxItemConsumeBurstMigration",
                "BoxVfxCue.ItemConsume",
                "Migration",
                "Tier 1",
                "targeted tests + visual spot check"),
            new(
                "EnableEnemyJumpLandingDustVfx",
                "enableEnemyJumpLandingDustVfx",
                "EnemyVfxCue.JumperLandingDust",
                "Augmentation",
                "Tier 1",
                "targeted tests + visual spot check"),
            new(
                "EnableGameplayVfxBoxSlideTrail",
                "enableGameplayVfxBoxSlideTrail",
                "BoxVfxCue.SlideDustTrail",
                "Augmentation / parameterized motion",
                "Tier 1",
                "targeted tests + density visual spot check"),
            new(
                "EnableEnemyJumpTargetVfx",
                "enableEnemyJumpTargetVfx",
                "EnemyVfxCue.JumperLandingTarget",
                "Augmentation",
                "Tier 2",
                "manual visual approval + targeted regression"),
            new(
                "EnableGameplayVfxUtilityWindupMigration",
                "enableGameplayVfxUtilityWindupMigration",
                "EnemyVfxCue.UtilityWindup",
                "Migration",
                "Tier 2",
                "manual visual approval + targeted regression"),
            new(
                "EnableGameplayVfxFrontFaceShieldActiveMigration",
                "enableGameplayVfxFrontFaceShieldActiveMigration",
                "EnemyVfxCue.FrontFaceShieldActive",
                "Migration",
                "Tier 2",
                "manual visual approval + targeted regression"),
            new(
                "EnableGameplayVfxFrontFaceShieldBlockMigration",
                "enableGameplayVfxFrontFaceShieldBlockMigration",
                "EnemyVfxCue.FrontFaceShieldBlock",
                "Migration",
                "Tier 2",
                "manual visual approval + targeted regression"),
            new(
                "EnableGameplayVfxFlipImpactBurstMigration",
                "enableGameplayVfxFlipImpactBurstMigration",
                "BoxVfxCue.FlipImpactBurst",
                "Migration",
                "Tier 2",
                "manual visual approval + targeted regression"),
            new(
                "EnableGameplayVfxEnemyDeathBurstMigration",
                "enableGameplayVfxEnemyDeathBurstMigration",
                "EnemyVfxCue.Death",
                "Migration burst",
                "Tier 3",
                "approved in Tier 3 rollout batch; requires post-rollout visual monitoring + rollback review"),
            new(
                "EnableGameplayVfxEnemyDeathMotionMigration",
                "enableGameplayVfxEnemyDeathMotionMigration",
                "EnemyVfxCue.DeathMotion",
                "Migration / parameterized motion",
                "Tier 3",
                "approved in Tier 3 rollout batch; requires post-rollout visual monitoring + rollback review"),
            new(
                "EnableGameplayVfxFlipDestroySelfMotionMigration",
                "enableGameplayVfxFlipDestroySelfMotionMigration",
                "BoxVfxCue.FlipDestroySelfMotion",
                "Migration / parameterized clone motion",
                "Tier 3",
                "approved in Tier 3 rollout batch; requires post-rollout visual monitoring + rollback review"),
        };

        private static readonly string[] HighRiskDefaultTrueCandidateFlags =
        {
            "EnableGameplayVfxEnemyDeathBurstMigration",
            "EnableGameplayVfxEnemyDeathMotionMigration",
            "EnableGameplayVfxFlipDestroySelfMotionMigration",
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
                    Assert.That(property, Is.Not.Null, $"{flag.PropertyName} must remain a public rollout flag.");
                    Assert.That(property.GetValue(runtime), Is.True, $"{flag.PropertyName} must default true after Tier 3 rollout.");
                }

                Assert.That(runtime.SuppressLegacyPlayerDamageHitEffects, Is.True);
                Assert.That(runtime.SuppressLegacyBoxDestroySmokeEffects, Is.False);
                Assert.That(runtime.SuppressLegacyBoxDestroyShrinkEffects, Is.True);
                Assert.That(runtime.SuppressLegacyItemConsumeEffects, Is.True);
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.True);
                Assert.That(runtime.SuppressLegacyFlipDestroySelfEffects, Is.True);
                Assert.That(runtime.SuppressLegacyUtilityWindupVfx, Is.True);
                Assert.That(runtime.SuppressLegacyFrontFaceShieldActiveVfx, Is.True);
                Assert.That(runtime.SuppressLegacyFrontFaceShieldBlockVfx, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void HighRiskSuppressGates_AreCompatibilityAliases()
        {
            var owner = new GameObject("GameplayVfxEnemyDeathSuppressOwner");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.EnableGameplayVfxEnemyDeathMotionMigration = false;
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = true;
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = false;
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.True);
                Assert.That(runtime.SuppressLegacyFlipDestroySelfEffects, Is.True);

                runtime.EnableGameplayVfxEnemyDeathMotionMigration = true;
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.True);

                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.True);

                runtime.EnableGameplayVfxEnemyDeathMotionMigration = false;
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = true;
                Assert.That(runtime.SuppressLegacyEnemyDeathEffects, Is.True);
                Assert.That(runtime.SuppressLegacyFlipDestroySelfEffects, Is.True);
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
            var document = ReadRepoFile(GovernancePath);
            var combinedScene = ReadRepoFile(CombinedGameplayShowcaseScenePath);
            Assert.That(combinedScene, Does.Contain("enableEnemyJumpTargetVfx: 1"));
            Assert.That(combinedScene, Does.Contain("enableEnemyJumpLandingDustVfx: 1"));
            foreach (var flag in VfxFlags.Where(flag => flag.Tier == "Tier 3"))
            {
                Assert.That(combinedScene, Does.Not.Contain($"{flag.SerializedFieldName}: 1"));
            }

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

            Assert.That(document, Does.Contain("CombinedGameplayShowcase.unity` is a jump VFX visual review scene override"));
            Assert.That(document, Does.Contain("Tier 3 flags use runtime default-on for broad VFX review"));
            Assert.That(document, Does.Contain("UIAudioScene.unity` is an explicit visual review scene override with all current Gameplay VFX flags on"));
            Assert.That(document, Does.Contain("TutorialScene.unity` remains production-safe/off"));
            Assert.That(document, Does.Contain("explicit scene-local false overrides"));
            Assert.That(document, Does.Contain("high-risk flag combinations for review only"));
            foreach (var flagName in HighRiskDefaultTrueCandidateFlags)
            {
                var flag = VfxFlags.Single(candidate => candidate.PropertyName == flagName);
                Assert.That(uiAudioScene, Does.Contain($"{flag.SerializedFieldName}: 1"));
            }
        }

        [Test]
        [Category("Extended")]
        public void Governance_DocumentsFlagRolloutPolicy()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## Gameplay VFX Flag Rollout Policy"));
            Assert.That(document, Does.Contain("Every current Gameplay VFX flag is a long-term default-true candidate"));
            Assert.That(document, Does.Contain("After Tier 3 rollout, all current Gameplay VFX flags are runtime default-on."));
            Assert.That(document, Does.Contain("approved in the Tier 3 rollout batch"));
            Assert.That(document, Does.Contain("Scene-local overrides are separate from runtime defaults"));
            Assert.That(document, Does.Contain("High-risk parameterized motion and clone/source-view VFX required manual parity approval"));
            Assert.That(document, Does.Contain("`SuppressLegacyEnemyDeathEffects` is an always true compatibility alias"));
            Assert.That(document, Does.Contain("`SuppressLegacyFlipDestroySelfEffects` is an always true compatibility alias"));
            Assert.That(document, Does.Contain("After legacy old path cleanup, all current migrated cue flags use canonical/off semantics"));
            Assert.That(document, Does.Contain("Old BoxDestroy shrink/fade playback is disabled independently of `EnableGameplayVfxBoxDestroyShrinkMigration`"));
            Assert.That(document, Does.Contain("`EnableGameplayVfxBoxDestroySmokeMigration` gates smoke only and does not own shrink playback"));
            Assert.That(document, Does.Contain("Their old presenter fallbacks are finalized and removed"));
            Assert.That(document, Does.Contain("Burst + Motion simultaneous output remains visually monitored"));
        }

        [Test]
        [Category("Extended")]
        public void AllVfxFlags_AreListedAsDefaultTrueCandidates()
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
                Assert.That(document, Does.Contain(BuildFlagTableRow(flag)));
            }
        }

        [Test]
        [Category("Extended")]
        public void AllVfxFlags_HaveRiskTier()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(VfxFlags.Select(flag => flag.Tier).Distinct().ToArray(), Is.EquivalentTo(new[] { "Tier 1", "Tier 2", "Tier 3" }));
            foreach (var flag in VfxFlags)
            {
                Assert.That(document, Does.Contain(BuildFlagTableRow(flag)));
                Assert.That(document, Does.Contain($"| `{flag.PropertyName}` | `{flag.CueName}` | {flag.Type} | {flag.ActualDefault} | {flag.Tier} | Yes | {flag.ApprovalGate} |"));
            }
        }

        [Test]
        [Category("Extended")]
        public void HighRiskFlags_RequirePostRolloutVisualMonitoring()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("High-risk parameterized motion and clone/source-view VFX required manual parity approval"));
            Assert.That(document, Does.Contain("Their old presenter fallbacks are finalized and removed"));
            foreach (var flagName in HighRiskDefaultTrueCandidateFlags)
            {
                var flag = VfxFlags.Single(candidate => candidate.PropertyName == flagName);
                Assert.That(flag.Tier, Is.EqualTo("Tier 3"));
                Assert.That(flag.ApprovalGate, Does.Contain("approved in Tier 3 rollout batch"));
                Assert.That(flag.ApprovalGate, Does.Contain("post-rollout visual monitoring"));
                Assert.That(flag.ApprovalGate, Does.Contain("rollback review"));
                Assert.That(document, Does.Contain(BuildFlagTableRow(flag)));
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

        private static string BuildFlagTableRow(FlagInfo flag)
        {
            return $"| `{flag.PropertyName}` | `{flag.CueName}` | {flag.Type} | {flag.ActualDefault} | {flag.Tier} | Yes | {flag.ApprovalGate} |";
        }

        private readonly struct FlagInfo
        {
            public FlagInfo(
                string propertyName,
                string serializedFieldName,
                string cueName,
                string type,
                string tier,
                string approvalGate)
            {
                PropertyName = propertyName;
                SerializedFieldName = serializedFieldName;
                CueName = cueName;
                Type = type;
                Tier = tier;
                ApprovalGate = approvalGate;
            }

            public string PropertyName { get; }

            public string SerializedFieldName { get; }

            public string CueName { get; }

            public string Type { get; }

            public string Tier { get; }

            public string ApprovalGate { get; }

            public string ActualDefault => "True";
        }
    }
}
