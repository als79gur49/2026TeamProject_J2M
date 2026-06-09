using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class EnemyAiRuntimeDefinitionGuardTests
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        [Category("Core")]
        public void EnemyStateResolverRuntime_KindImplementationMismatch_FailsBeforeRuntimeTick()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new EnemyStateResolverRuntime(
                    EnemyAiStateResolverKind.Default,
                    ChargingEnemyAiStateResolver.Instance));

            AssertGuardMessage(exception, "state resolver runtime kind", "Default", "Charge", nameof(ChargingEnemyAiStateResolver));
        }

        [Test]
        [Category("Core")]
        public void EnemyDetectionRuntime_KindImplementationMismatch_FailsBeforeRuntimeTick()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new EnemyDetectionRuntime(
                    DetectionStrategyKind.None,
                    DetectionSettings.CreateStandardEnemyDetection(),
                    NearestOpponentDetectionStrategy.Instance));

            AssertGuardMessage(exception, "detection runtime kind", "None", "NearestOpponent", nameof(NearestOpponentDetectionStrategy));
        }

        [Test]
        [Category("Core")]
        public void EnemyDetectionRuntime_UnknownImplementation_FailsBeforeRuntimeTick()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EnemyDetectionRuntime(
                    DetectionStrategyKind.NearestOpponent,
                    DetectionSettings.CreateStandardEnemyDetection(),
                    new UnknownDetectionStrategy()));

            Assert.That(exception.Message, Does.Contain("Unknown detection strategy implementation"));
        }

        [Test]
        [Category("Core")]
        public void EnemyChaseRuntime_UnknownImplementation_FailsBeforeRuntimeTick()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EnemyChaseRuntime(
                    ChaseStrategyKind.AxisPriority,
                    ChaseSettings.CreateDefault(),
                    new UnknownChaseStrategy()));

            Assert.That(exception.Message, Does.Contain("Unknown chase strategy implementation"));
        }

        [Test]
        [Category("Core")]
        public void EnemyCombatCapabilityRuntime_KindImplementationMismatch_FailsBeforeRuntimeTick()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new EnemyCombatCapabilityRuntime(
                    AttackDecisionStrategyKind.WindupForwardCellProjectile,
                    AttackDecisionSettings.CreateAdjacentRange(),
                    EnemyAttackTimingSettings.CreateImmediate(),
                    NoAttackDecisionStrategy.Instance));

            AssertGuardMessage(exception, "requires a concrete attack decision strategy implementation");
        }

        [Test]
        [Category("Core")]
        public void EnemyCombatCapabilityRuntime_RetiredMeleeKind_FailsBeforeRuntimeTick()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new EnemyCombatCapabilityRuntime(
                    AttackDecisionStrategyKind.RetiredMelee,
                    AttackDecisionSettings.CreateAdjacentRange(),
                    EnemyAttackTimingSettings.CreateImmediate(),
                    WindupForwardCellProjectileAttackDecisionStrategy.Instance));

            Assert.That(exception.Message, Does.Contain("RetiredMelee is a serialized compatibility slot"));
        }

        [Test]
        [Category("Core")]
        public void EnemyCombatCapabilityRuntime_ContactSameCellImplementation_FailsAsCombatCapability()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new EnemyCombatCapabilityRuntime(
                    AttackDecisionStrategyKind.WindupForwardCellProjectile,
                    AttackDecisionSettings.CreateAdjacentRange(),
                    EnemyAttackTimingSettings.CreateImmediate(),
                    ContactSameCellAttackDecisionStrategy.Instance));

            Assert.That(exception.Message, Does.Contain("ContactSameCell must compile as passive contact"));
        }

        [Test]
        [Category("Core")]
        public void EnemyPassiveContactCapabilityRuntime_NonContactImplementation_FailsBeforeRuntimeTick()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new EnemyPassiveContactCapabilityRuntime(
                    AttackDecisionStrategyKind.ContactSameCell,
                    AttackDecisionSettings.CreateAdjacentRange(),
                    NoAttackDecisionStrategy.Instance));

            AssertGuardMessage(exception, "passive contact runtime kind", "ContactSameCell", nameof(NoAttackDecisionStrategy));
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_DetectionKindImplementationMismatch_FailsDuringCompile()
        {
            var profile = CreateProfile(
                detectionAsset: CreateAsset<MismatchedDetectionAsset>("Mismatch_Detection"),
                capabilities: Array.Empty<EnemyCapabilityAsset>());

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                AssertGuardMessage(exception, "detection runtime kind", "None", "NearestOpponent", nameof(NearestOpponentDetectionStrategy));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_CombatKindImplementationMismatch_FailsDuringCompile()
        {
            var combat = CreateAsset<MismatchedCombatCapabilityAsset>("Mismatch_Combat");
            var profile = CreateProfile(capabilities: new EnemyCapabilityAsset[] { combat });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                AssertGuardMessage(exception, "requires a concrete attack decision strategy implementation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_PassiveContactImplementationMismatch_FailsDuringCompile()
        {
            var passiveContact = CreateAsset<MismatchedPassiveContactCapabilityAsset>("Mismatch_PassiveContact");
            var profile = CreateProfile(capabilities: new EnemyCapabilityAsset[] { passiveContact });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                AssertGuardMessage(exception, "passive contact runtime kind", "ContactSameCell", nameof(NoAttackDecisionStrategy));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileAssets_RepositoryProfiles_CompileBeforeRuntimeTick()
        {
            var assetPaths = AssetDatabase.FindAssets("t:EnemyAiProfile");
            Assert.That(assetPaths, Is.Not.Empty, "Repository scan found no EnemyAiProfile assets.");

            var failures = new List<string>();
            for (var i = 0; i < assetPaths.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetPaths[i]);
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(assetPath);
                if (profile == null)
                {
                    failures.Add($"{assetPath}: failed to load {nameof(EnemyAiProfile)}.");
                    continue;
                }

                try
                {
                    var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                    definition.Validate(nameof(definition));
                }
                catch (Exception exception)
                {
                    failures.Add($"{assetPath} ({profile.name}): {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "EnemyAiProfile compile failures:\n" + string.Join("\n", failures));
        }

        private static EnemyAiProfile CreateProfile(
            EnemyDetectionStrategyAsset detectionAsset = null,
            EnemyCapabilityAsset[] capabilities = null)
        {
            var profile = CreateAsset<EnemyAiProfile>("Test_EnemyAiProfile");
            var core = CreateAsset<EnemyCoreAuthoring>("Test_EnemyCoreAuthoring");
            var brain = CreateAsset<EnemyBrainAuthoring>("Test_EnemyBrainAuthoring");
            var stateResolver = CreateAsset<DefaultEnemyStateResolverAsset>("Test_DefaultEnemyStateResolver");
            var patrol = CreateAsset<ForwardPatrolAsset>("Test_ForwardPatrol");
            var detection = detectionAsset ?? CreateAsset<NoDetectionStrategyAsset>("Test_NoDetection");
            var chase = CreateAsset<AxisPriorityChaseAsset>("Test_AxisPriorityChase");

            SetSerializedField(core, "commonSettings", EnemyAiCommonAuthoringSettings.CreateStandard());
            SetSerializedField(core, "locomotionTimingSettings", EnemyLocomotionTimingAuthoringSettings.CreateImmediate());
            SetSerializedField(core, "chargeTimingSettings", EnemyChargeTimingAuthoringSettings.CreateDefault());
            SetSerializedField(brain, "stateResolver", stateResolver);
            SetSerializedField(brain, "patrolStrategy", patrol);
            SetSerializedField(brain, "detectionStrategy", detection);
            SetSerializedField(brain, "chaseStrategy", chase);
            SetSerializedField(profile, "coreAuthoring", core);
            SetSerializedField(profile, "brainAuthoring", brain);
            SetSerializedField(profile, "capabilityAssets", new List<EnemyCapabilityAsset>(capabilities ?? Array.Empty<EnemyCapabilityAsset>()));

            return profile;
        }

        private static T CreateAsset<T>(string name)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            asset.hideFlags = HideFlags.HideAndDontSave;
            return asset;
        }

        private static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void AssertGuardMessage(Exception exception, params string[] expectedTokens)
        {
            Assert.That(exception, Is.Not.Null);
            foreach (var token in expectedTokens)
            {
                Assert.That(exception.Message, Does.Contain(token));
            }
        }

        private sealed class MismatchedDetectionAsset : EnemyDetectionStrategyAsset
        {
            public override DetectionStrategyKind Kind => DetectionStrategyKind.None;

            public override DetectionSettings Settings => DetectionSettings.CreateStandardEnemyDetection();

            protected override IDetectionStrategy ResolveStrategy()
            {
                return NearestOpponentDetectionStrategy.Instance;
            }
        }

        private sealed class MismatchedCombatCapabilityAsset : EnemyCombatCapabilityAsset
        {
            public override AttackDecisionStrategyKind Kind => AttackDecisionStrategyKind.WindupForwardCellProjectile;

            public override AttackDecisionSettings AttackDecisionSettings => AttackDecisionSettings.CreateAdjacentRange();

            public override EnemyAttackTimingAuthoringSettings AttackTimingSettings =>
                EnemyAttackTimingAuthoringSettings.CreateImmediate();

            protected override IAttackDecisionStrategy ResolveStrategy()
            {
                return NoAttackDecisionStrategy.Instance;
            }
        }

        private sealed class MismatchedPassiveContactCapabilityAsset : EnemyCapabilityAsset
        {
            public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.PassiveContact;

            internal override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
            {
                return new EnemyPassiveContactCapabilityRuntime(
                    AttackDecisionStrategyKind.ContactSameCell,
                    AttackDecisionSettings.CreateAdjacentRange(),
                    NoAttackDecisionStrategy.Instance);
            }
        }

        private sealed class UnknownDetectionStrategy : IDetectionStrategy
        {
            public bool TryFindTarget(
                WorldSnapshot snapshot,
                in EntityState source,
                in DetectionSettings settings,
                out EntityState target,
                EnemyDetectionQueryOptions options = default)
            {
                target = default;
                return false;
            }
        }

        private sealed class UnknownChaseStrategy : IChaseStrategy
        {
            public bool TryBuildMovementIntent(
                WorldSnapshot snapshot,
                in EntityState source,
                in EntityState target,
                in EnemyAiCommonSettings commonSettings,
                in ChaseSettings settings,
                IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
                out RawMovementIntent intent,
                Direction? excludedDirection = null)
            {
                intent = default;
                return false;
            }
        }
    }
}
