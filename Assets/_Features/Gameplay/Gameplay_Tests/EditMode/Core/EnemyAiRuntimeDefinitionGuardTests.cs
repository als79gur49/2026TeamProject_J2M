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

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_SummonBehaviorOnly_CompilesRealTypedRuntime()
        {
            var minionProfile = CreateProfile();
            var archetype = CreateArchetype("BehaviorMinion", minionProfile);
            var summonModule = CreateSummonBehaviorModule(
                archetype,
                initialDelaySeconds: 0.2f,
                cooldownSeconds: 0.5f,
                spawnCountPerTrigger: 2,
                maxAliveChildren: 4,
                overrideHp: true,
                hpOverride: 3,
                windupSeconds: 0.3f,
                recoverySeconds: 0.1f);
            var profile = CreateProfile(behaviors: new EnemyBehaviorModuleAsset[] { summonModule });

            try
            {
                var definition = profile.CreateRuntimeDefinition(10);

                Assert.That(definition.TryGetSummonBehavior(out var summon), Is.True);
                Assert.That(summon.Key, Is.EqualTo(EnemyBehaviorModuleKey.Summon));
                Assert.That(summon.InitialDelayTicks, Is.EqualTo(2));
                Assert.That(summon.CooldownTicks, Is.EqualTo(5));
                Assert.That(summon.SpawnCountPerTrigger, Is.EqualTo(2));
                Assert.That(summon.MaxAliveChildren, Is.EqualTo(4));
                Assert.That(summon.SummonedArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("BehaviorMinion")));
                Assert.That(summon.OverrideHp, Is.True);
                Assert.That(summon.HpOverride, Is.EqualTo(3));
                Assert.That(summon.WindupTicks, Is.EqualTo(3));
                Assert.That(summon.RecoveryTicks, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(summonModule);
                UnityEngine.Object.DestroyImmediate(archetype);
                UnityEngine.Object.DestroyImmediate(minionProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_DuplicateSummonBehaviorModules_FailsDuringCompile()
        {
            var minionProfile = CreateProfile();
            var archetype = CreateArchetype("BehaviorMinion", minionProfile);
            var first = CreateSummonBehaviorModule(archetype, moduleName: "Test_FirstSummonBehaviorModule");
            var second = CreateSummonBehaviorModule(archetype, moduleName: "Test_SecondSummonBehaviorModule");
            var profile = CreateProfile(behaviors: new EnemyBehaviorModuleAsset[] { first, second });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                AssertGuardMessage(
                    exception,
                    "multiple behavior modules",
                    EnemyBehaviorModuleKey.Summon.ToString(),
                    "Test_FirstSummonBehaviorModule",
                    "Test_SecondSummonBehaviorModule");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(archetype);
                UnityEngine.Object.DestroyImmediate(minionProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_RetiredSummonMinion_FailsWithBehaviorMigrationGuidance()
        {
            var minionProfile = CreateProfile();
            var archetype = CreateArchetype("BehaviorMinion", minionProfile);
            var utility = CreateUtilityCapability(CreateUtilityEffect(EnemyUtilityEffectKind.RetiredSummonMinion, archetype));
            var profile = CreateProfile(capabilities: new EnemyCapabilityAsset[] { utility });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                AssertGuardMessage(
                    exception,
                    "Utility Summon is retired",
                    nameof(EnemySummonBehaviorModuleAsset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(utility);
                UnityEngine.Object.DestroyImmediate(archetype);
                UnityEngine.Object.DestroyImmediate(minionProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_GravityFieldAuraWithBehaviorSummon_DoesNotTriggerSummonDuplicateGuard()
        {
            var minionProfile = CreateProfile();
            var archetype = CreateArchetype("BehaviorMinion", minionProfile);
            var utility = CreateUtilityCapability(CreateUtilityEffect(EnemyUtilityEffectKind.GravityFieldAura, archetype));
            var summonModule = CreateSummonBehaviorModule(archetype);
            var profile = CreateProfile(
                capabilities: new EnemyCapabilityAsset[] { utility },
                behaviors: new EnemyBehaviorModuleAsset[] { summonModule });

            try
            {
                var definition = profile.CreateRuntimeDefinition(60);

                Assert.That(definition.TryGetSummonBehavior(out _), Is.True);
                Assert.That(definition.Capabilities.TryGetUtility(out var compiledUtility), Is.True);
                Assert.That(compiledUtility.Effects[0].Kind, Is.EqualTo(EnemyUtilityEffectKind.GravityFieldAura));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(summonModule);
                UnityEngine.Object.DestroyImmediate(utility);
                UnityEngine.Object.DestroyImmediate(archetype);
                UnityEngine.Object.DestroyImmediate(minionProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_RetiredLockNearbyBoxesWithBehaviorSummon_FailsRetiredGuard()
        {
            var minionProfile = CreateProfile();
            var archetype = CreateArchetype("BehaviorMinion", minionProfile);
            var utility = CreateUtilityCapability(CreateUtilityEffect(EnemyUtilityEffectKind.RetiredLockNearbyBoxes, archetype));
            var summonModule = CreateSummonBehaviorModule(archetype);
            var profile = CreateProfile(
                capabilities: new EnemyCapabilityAsset[] { utility },
                behaviors: new EnemyBehaviorModuleAsset[] { summonModule });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                AssertGuardMessage(exception, "LockNearbyBoxes", "retired");
                Assert.That(exception.Message, Does.Not.Contain("Utility SummonMinion"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(summonModule);
                UnityEngine.Object.DestroyImmediate(utility);
                UnityEngine.Object.DestroyImmediate(archetype);
                UnityEngine.Object.DestroyImmediate(minionProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_InvalidSummonBehaviorAuthoring_FailsDuringCompile()
        {
            var minionProfile = CreateProfile();
            var archetype = CreateArchetype("BehaviorMinion", minionProfile);
            var cases = new (string Name, Action<EnemySummonBehaviorModuleAsset> Mutate, string ExpectedToken)[]
            {
                (
                    "negative initial delay",
                    module => SetSerializedField(module, "initialDelaySeconds", -0.1f),
                    "non-negative initial delay"),
                (
                    "non-positive cooldown",
                    module => SetSerializedField(module, "cooldownSeconds", 0f),
                    "positive cooldown"),
                (
                    "null summon authoring",
                    module => SetSerializedField(module, "summon", null),
                    "requires summon authoring data"),
                (
                    "missing archetype",
                    module => SetSerializedField(module, "summon", CreateEnemySummonAuthoring(null)),
                    "summoned archetype asset"),
                (
                    "non-positive spawn count",
                    module => SetSerializedField(module, "summon", CreateEnemySummonAuthoring(archetype, spawnCountPerTrigger: 0)),
                    "positive spawn count"),
                (
                    "non-positive max alive",
                    module => SetSerializedField(module, "summon", CreateEnemySummonAuthoring(archetype, maxAliveChildren: 0)),
                    "positive max alive child count"),
                (
                    "invalid hp override",
                    module => SetSerializedField(module, "summon", CreateEnemySummonAuthoring(archetype, overrideHp: true, hpOverride: 0)),
                    "HP override"),
                (
                    "non-positive windup",
                    module => SetSerializedField(module, "summon", CreateEnemySummonAuthoring(archetype, windupSeconds: 0f)),
                    "positive windup duration"),
                (
                    "negative recovery",
                    module => SetSerializedField(module, "summon", CreateEnemySummonAuthoring(archetype, recoverySeconds: -0.1f)),
                    "non-negative recovery duration"),
            };

            try
            {
                foreach (var testCase in cases)
                {
                    var summonModule = CreateSummonBehaviorModule(archetype);
                    var profile = CreateProfile(behaviors: new EnemyBehaviorModuleAsset[] { summonModule });

                    try
                    {
                        testCase.Mutate(summonModule);

                        var exception = Assert.Throws<ArgumentException>(
                            () => profile.CreateRuntimeDefinition(60),
                            testCase.Name);

                        AssertGuardMessage(exception, testCase.ExpectedToken);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(profile);
                        UnityEngine.Object.DestroyImmediate(summonModule);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetype);
                UnityEngine.Object.DestroyImmediate(minionProfile);
            }
        }

        private static EnemyAiProfile CreateProfile(
            EnemyDetectionStrategyAsset detectionAsset = null,
            EnemyCapabilityAsset[] capabilities = null,
            EnemyBehaviorModuleAsset[] behaviors = null)
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
            SetSerializedField(brain, "stateResolver", stateResolver);
            SetSerializedField(brain, "patrolStrategy", patrol);
            SetSerializedField(brain, "detectionStrategy", detection);
            SetSerializedField(brain, "chaseStrategy", chase);
            SetSerializedField(profile, "coreAuthoring", core);
            SetSerializedField(profile, "brainAuthoring", brain);
            SetSerializedField(profile, "capabilityAssets", new List<EnemyCapabilityAsset>(capabilities ?? Array.Empty<EnemyCapabilityAsset>()));
            SetSerializedField(profile, "behaviorModuleAssets", new List<EnemyBehaviorModuleAsset>(behaviors ?? Array.Empty<EnemyBehaviorModuleAsset>()));

            return profile;
        }

        private static EnemySummonBehaviorModuleAsset CreateSummonBehaviorModule(
            EnemyUnitArchetypeAsset archetype,
            string moduleName = "Test_EnemySummonBehaviorModule",
            float initialDelaySeconds = 0f,
            float cooldownSeconds = 1f,
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            bool overrideHp = false,
            int hpOverride = 1,
            float windupSeconds = 1f,
            float recoverySeconds = 0f)
        {
            var module = CreateAsset<EnemySummonBehaviorModuleAsset>(moduleName);
            SetSerializedField(module, "initialDelaySeconds", initialDelaySeconds);
            SetSerializedField(module, "cooldownSeconds", cooldownSeconds);
            SetSerializedField(
                module,
                "summon",
                CreateEnemySummonAuthoring(
                    archetype,
                    spawnCountPerTrigger,
                    maxAliveChildren,
                    overrideHp,
                    hpOverride,
                    windupSeconds,
                    recoverySeconds));
            return module;
        }

        private static EnemySummonAuthoring CreateEnemySummonAuthoring(
            EnemyUnitArchetypeAsset archetype,
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            bool overrideHp = false,
            int hpOverride = 1,
            float windupSeconds = 1f,
            float recoverySeconds = 0f)
        {
            var summon = new EnemySummonAuthoring();
            SetSerializedField(summon, "spawnCountPerTrigger", spawnCountPerTrigger);
            SetSerializedField(summon, "maxAliveChildren", maxAliveChildren);
            SetSerializedField(summon, "summonedArchetype", archetype);
            SetSerializedField(summon, "overrideHp", overrideHp);
            SetSerializedField(summon, "hpOverride", hpOverride);
            SetSerializedField(summon, "windupSeconds", windupSeconds);
            SetSerializedField(summon, "recoverySeconds", recoverySeconds);
            return summon;
        }

        private static EnemyUtilityCapabilityAsset CreateUtilityCapability(params EnemyUtilityEffectAuthoring[] effects)
        {
            var utility = CreateAsset<EnemyUtilityCapabilityAsset>("Test_EnemyUtilityCapability");
            SetSerializedField(utility, "effects", effects ?? Array.Empty<EnemyUtilityEffectAuthoring>());
            return utility;
        }

        private static EnemyUtilityEffectAuthoring CreateUtilityEffect(
            EnemyUtilityEffectKind kind,
            EnemyUnitArchetypeAsset summonArchetype)
        {
            var effect = new EnemyUtilityEffectAuthoring();
            SetSerializedField(effect, "kind", kind);
            SetSerializedField(effect, "initialDelaySeconds", 0f);
            SetSerializedField(effect, "cooldownSeconds", 1f);
            return effect;
        }

        private static EnemyUnitArchetypeAsset CreateArchetype(string archetypeId, EnemyAiProfile profile)
        {
            var archetype = CreateAsset<EnemyUnitArchetypeAsset>("Test_EnemyUnitArchetype");
            SetSerializedField(archetype, "archetypeId", new EnemyUnitArchetypeId(archetypeId));
            SetSerializedField(archetype, "aiProfile", profile);
            SetSerializedField(archetype, "spawnDefaults", EnemyUnitSpawnDefaults.CreateDefault());
            return archetype;
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
