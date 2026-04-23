using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests
{
    internal sealed class EnemyAiTestProfileSpec
    {
        public EnemyAiCommonAuthoringSettings CommonSettings = EnemyAiCommonAuthoringSettings.CreateDefaultMelee();
        public EnemyLocomotionTimingAuthoringSettings LocomotionTimingSettings = EnemyLocomotionTimingAuthoringSettings.CreateDefaultMelee();
        public EnemyChargeTimingAuthoringSettings ChargeTimingSettings = EnemyChargeTimingAuthoringSettings.CreateDefault();
        public EnemyAiStateResolverKind StateResolverKind = EnemyAiStateResolverKind.Default;
        public PatrolStrategyKind PatrolStrategyKind = PatrolStrategyKind.Forward;
        public PatrolSettings PatrolSettings = PatrolSettings.CreateDefault();
        public DetectionStrategyKind DetectionStrategyKind = DetectionStrategyKind.NearestOpponent;
        public DetectionSettings DetectionSettings = DetectionSettings.CreateDefaultMelee();
        public ChaseSettings ChaseSettings = ChaseSettings.CreateDefault();
        public AttackDecisionStrategyKind AttackDecisionStrategyKind = AttackDecisionStrategyKind.Melee;
        public AttackDecisionSettings AttackDecisionSettings = AttackDecisionSettings.CreateDefaultMelee();
        public EnemyAttackTimingAuthoringSettings AttackTimingSettings = EnemyAttackTimingAuthoringSettings.CreateDefaultMelee();
        public bool IncludePassiveContact;
        public MovementSkillStrategyKind MovementSkillStrategyKind = MovementSkillStrategyKind.None;
        public EnemyJumpTimingAuthoringSettings JumpTimingSettings = EnemyJumpTimingAuthoringSettings.CreateDefault();
    }

    internal static class EnemyAiProfileTestFactory
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

        public static EnemyAiProfile Create(EnemyAiTestProfileSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var profile = CreateHiddenAsset<EnemyAiProfile>("Test_EnemyAiProfile");
            var core = CreateHiddenAsset<EnemyCoreAuthoring>("Test_EnemyCoreAuthoring");
            var brain = CreateHiddenAsset<EnemyBrainAuthoring>("Test_EnemyBrainAuthoring");
            var stateResolver = CreateStateResolver(spec.StateResolverKind);
            var patrol = CreatePatrol(spec.PatrolStrategyKind, spec.PatrolSettings);
            var detection = CreateDetection(spec.DetectionStrategyKind, spec.DetectionSettings);
            var chase = CreateChase(spec.ChaseSettings);
            var capabilities = CreateCapabilities(spec).Cast<EnemyCapabilityAsset>().ToList();

            SetSerializedField(core, "commonSettings", spec.CommonSettings);
            SetSerializedField(core, "locomotionTimingSettings", spec.LocomotionTimingSettings);
            SetSerializedField(core, "chargeTimingSettings", spec.ChargeTimingSettings);
            SetSerializedField(brain, "stateResolver", stateResolver);
            SetSerializedField(brain, "patrolStrategy", patrol);
            SetSerializedField(brain, "detectionStrategy", detection);
            SetSerializedField(brain, "chaseStrategy", chase);
            SetSerializedField(profile, "coreAuthoring", core);
            SetSerializedField(profile, "brainAuthoring", brain);
            SetSerializedField(profile, "capabilityAssets", capabilities);

            return profile;
        }

        public static EnemyAiProfile CreateDefaultMelee(
            int windupTicks = 0,
            int moveCooldownTicks = 0,
            int recoverTicks = 1,
            bool includePassiveContact = false)
        {
            return Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = ToAuthoring(new EnemyAiCommonSettings(
                    movementPriority: 50,
                    attackPriority: 50,
                    recoverTicks: recoverTicks)),
                LocomotionTimingSettings = ToAuthoring(new EnemyLocomotionTimingSettings(moveCooldownTicks)),
                AttackTimingSettings = ToAuthoring(new EnemyAttackTimingSettings(windupTicks)),
                IncludePassiveContact = includePassiveContact,
            });
        }

        public static EnemyAiProfile CreateNonAttacking(int moveCooldownTicks = 0, bool includePassiveContact = false)
        {
            return Create(new EnemyAiTestProfileSpec
            {
                LocomotionTimingSettings = ToAuthoring(new EnemyLocomotionTimingSettings(moveCooldownTicks)),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = includePassiveContact,
            });
        }

        // Charging profiles rely on same-cell passive contact, so repeated hits follow receiver cooldown cadence.
        public static EnemyAiProfile CreateCharging(
            int moveCooldownTicks = 0,
            bool includePassiveContact = true,
            int windupTicks = 0,
            int recoverTicks = 0)
        {
            return Create(new EnemyAiTestProfileSpec
            {
                LocomotionTimingSettings = ToAuthoring(new EnemyLocomotionTimingSettings(moveCooldownTicks)),
                ChargeTimingSettings = ToAuthoring(new EnemyChargeTimingSettings(windupTicks, recoverTicks)),
                StateResolverKind = EnemyAiStateResolverKind.Charge,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = includePassiveContact,
            });
        }

        public static EnemyAiProfile CreateWallFollower(
            WallFollowTurnPreference turnPreference = WallFollowTurnPreference.Right,
            int moveCooldownTicks = 0,
            bool includePassiveContact = false)
        {
            return Create(new EnemyAiTestProfileSpec
            {
                LocomotionTimingSettings = ToAuthoring(new EnemyLocomotionTimingSettings(moveCooldownTicks)),
                PatrolStrategyKind = PatrolStrategyKind.WallFollow,
                PatrolSettings = new PatrolSettings(
                    PatrolBlockedMovementResponse.Stop,
                    turnPreference,
                    followWalls: true,
                    followBoxes: true),
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = includePassiveContact,
            });
        }

        public static EnemyAiProfile CreateJumpChaser(
            EnemyJumpTimingSettings jumpTimingSettings,
            int moveCooldownTicks = 0,
            bool includePassiveContact = false)
        {
            return Create(new EnemyAiTestProfileSpec
            {
                LocomotionTimingSettings = ToAuthoring(new EnemyLocomotionTimingSettings(moveCooldownTicks)),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                MovementSkillStrategyKind = MovementSkillStrategyKind.JumpToLockedTarget,
                JumpTimingSettings = ToAuthoring(jumpTimingSettings),
                IncludePassiveContact = includePassiveContact,
            });
        }

        public static EnemyAiProfile CreateJumpPatrol(
            EnemyJumpTimingSettings jumpTimingSettings,
            int moveCooldownTicks = 0,
            bool includePassiveContact = false)
        {
            return Create(new EnemyAiTestProfileSpec
            {
                LocomotionTimingSettings = ToAuthoring(new EnemyLocomotionTimingSettings(moveCooldownTicks)),
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                MovementSkillStrategyKind = MovementSkillStrategyKind.JumpToLockedTarget,
                JumpTimingSettings = ToAuthoring(jumpTimingSettings),
                IncludePassiveContact = includePassiveContact,
            });
        }

        public static EnemyAiProfile CreateContactDamage(int moveCooldownTicks = 0, int recoverTicks = 1)
        {
            return Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = ToAuthoring(new EnemyAiCommonSettings(
                    movementPriority: 50,
                    attackPriority: 50,
                    recoverTicks: recoverTicks)),
                LocomotionTimingSettings = ToAuthoring(new EnemyLocomotionTimingSettings(moveCooldownTicks)),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = true,
            });
        }

        public static EnemyAiProfile CreateStationaryPassiveContact()
        {
            return Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = true,
                LocomotionTimingSettings = ToAuthoring(new EnemyLocomotionTimingSettings(moveCooldownTicks: 0)),
            });
        }

        public static void Destroy(EnemyAiProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            var assets = new List<ScriptableObject>();
            var seenInstanceIds = new HashSet<int>();

            Collect(assets, seenInstanceIds, profile);

            var brain = GetSerializedField<EnemyBrainAuthoring>(profile, "brainAuthoring");
            Collect(assets, seenInstanceIds, brain);
            Collect(assets, seenInstanceIds, GetSerializedField<EnemyCoreAuthoring>(profile, "coreAuthoring"));

            if (brain != null)
            {
                Collect(assets, seenInstanceIds, GetSerializedField<EnemyStateResolverAsset>(brain, "stateResolver"));
                Collect(assets, seenInstanceIds, GetSerializedField<EnemyPatrolStrategyAsset>(brain, "patrolStrategy"));
                Collect(assets, seenInstanceIds, GetSerializedField<EnemyDetectionStrategyAsset>(brain, "detectionStrategy"));
                Collect(assets, seenInstanceIds, GetSerializedField<EnemyChaseStrategyAsset>(brain, "chaseStrategy"));
            }

            var capabilities = GetSerializedField<List<EnemyCapabilityAsset>>(profile, "capabilityAssets");
            if (capabilities != null)
            {
                for (var i = 0; i < capabilities.Count; i++)
                {
                    Collect(assets, seenInstanceIds, capabilities[i]);
                }
            }

            for (var i = assets.Count - 1; i >= 0; i--)
            {
                if (assets[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(assets[i]);
                }
            }
        }

        public static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        public static T GetSerializedField<T>(object target, string fieldName)
            where T : class
        {
            var field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }

        private static EnemyStateResolverAsset CreateStateResolver(EnemyAiStateResolverKind kind)
        {
            return kind switch
            {
                EnemyAiStateResolverKind.Default => CreateHiddenAsset<DefaultEnemyStateResolverAsset>("Test_DefaultEnemyStateResolver"),
                EnemyAiStateResolverKind.Charge => CreateHiddenAsset<ChargingEnemyStateResolverAsset>("Test_ChargingEnemyStateResolver"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported enemy state resolver kind for tests."),
            };
        }

        private static EnemyPatrolStrategyAsset CreatePatrol(PatrolStrategyKind kind, PatrolSettings settings)
        {
            switch (kind)
            {
                case PatrolStrategyKind.Forward:
                {
                    var patrol = CreateHiddenAsset<ForwardPatrolAsset>("Test_ForwardPatrol");
                    SetSerializedField(patrol, "blockedMovementResponse", settings.BlockedMovementResponse);
                    return patrol;
                }

                case PatrolStrategyKind.WallFollow:
                {
                    var patrol = CreateHiddenAsset<WallFollowPatrolAsset>("Test_WallFollowPatrol");
                    SetSerializedField(patrol, "blockedMovementResponse", settings.BlockedMovementResponse);
                    SetSerializedField(patrol, "turnPreference", settings.TurnPreference);
                    SetSerializedField(patrol, "followWalls", settings.FollowWalls);
                    SetSerializedField(patrol, "followBoxes", settings.FollowBoxes);
                    return patrol;
                }

                case PatrolStrategyKind.Stationary:
                    return CreateHiddenAsset<StationaryPatrolAsset>("Test_StationaryPatrol");

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported patrol strategy kind for tests.");
            }
        }

        private static EnemyDetectionStrategyAsset CreateDetection(DetectionStrategyKind kind, DetectionSettings settings)
        {
            switch (kind)
            {
                case DetectionStrategyKind.NearestOpponent:
                {
                    var detection = CreateHiddenAsset<NearestOpponentDetectionAsset>("Test_NearestOpponentDetection");
                    SetSerializedField(detection, "senseRange", settings.SenseRange);
                    SetSerializedField(detection, "requireSameFace", settings.RequireSameFace);
                    SetSerializedField(detection, "canTargetMarkedForDeath", settings.CanTargetMarkedForDeath);
                    return detection;
                }

                case DetectionStrategyKind.None:
                    return CreateHiddenAsset<NoDetectionStrategyAsset>("Test_NoDetection");

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported detection strategy kind for tests.");
            }
        }

        private static EnemyChaseStrategyAsset CreateChase(ChaseSettings settings)
        {
            var chase = CreateHiddenAsset<AxisPriorityChaseAsset>("Test_AxisPriorityChase");
            SetSerializedField(chase, "axisPriority", settings.AxisPriority);
            SetSerializedField(chase, "trySecondaryAxisWhenBlocked", settings.TrySecondaryAxisWhenBlocked);
            SetSerializedField(chase, "desiredChaseDistance", settings.DesiredChaseDistance);
            return chase;
        }

        private static IEnumerable<EnemyCapabilityAsset> CreateCapabilities(EnemyAiTestProfileSpec spec)
        {
            switch (spec.AttackDecisionStrategyKind)
            {
                case AttackDecisionStrategyKind.None:
                    break;

                case AttackDecisionStrategyKind.Melee:
                {
                    var melee = CreateHiddenAsset<MeleeCombatCapabilityAsset>("Test_MeleeCombatCapability");
                    SetSerializedField(melee, "attackDecisionSettings", spec.AttackDecisionSettings);
                    SetSerializedField(melee, "attackTimingSettings", spec.AttackTimingSettings);
                    yield return melee;
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(spec.AttackDecisionStrategyKind),
                        spec.AttackDecisionStrategyKind,
                        "Unsupported combat capability kind for tests.");
            }

            if (spec.IncludePassiveContact)
            {
                yield return CreateHiddenAsset<EnemyPassiveContactCapabilityAsset>("Test_EnemyPassiveContactCapability");
            }

            switch (spec.MovementSkillStrategyKind)
            {
                case MovementSkillStrategyKind.None:
                    yield break;

                case MovementSkillStrategyKind.JumpToLockedTarget:
                {
                    var jump = CreateHiddenAsset<JumpToLockedTargetCapabilityAsset>("Test_JumpToLockedTargetCapability");
                    SetSerializedField(jump, "jumpTimingSettings", spec.JumpTimingSettings);
                    yield return jump;
                    yield break;
                }

                case MovementSkillStrategyKind.PhaseThroughLockedTarget:
                {
                    var phase = CreateHiddenAsset<TestPhaseThroughLockedTargetCapabilityAsset>("Test_PhaseThroughLockedTargetCapability");
                    SetSerializedField(phase, "jumpTimingSettings", spec.JumpTimingSettings);
                    yield return phase;
                    yield break;
                }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(spec.MovementSkillStrategyKind),
                        spec.MovementSkillStrategyKind,
                        "Unsupported movement skill capability kind for tests.");
            }
        }

        private static T CreateHiddenAsset<T>(string assetName)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            asset.hideFlags = HideFlags.HideAndDontSave;
            return asset;
        }

        private static EnemyAiCommonAuthoringSettings ToAuthoring(EnemyAiCommonSettings settings)
        {
            return EnemyAiCommonAuthoringSettings.FromRuntimeSettings(
                settings,
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        private static EnemyAttackTimingAuthoringSettings ToAuthoring(EnemyAttackTimingSettings settings)
        {
            return EnemyAttackTimingAuthoringSettings.FromRuntimeSettings(
                settings,
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        private static EnemyLocomotionTimingAuthoringSettings ToAuthoring(EnemyLocomotionTimingSettings settings)
        {
            return EnemyLocomotionTimingAuthoringSettings.FromRuntimeSettings(
                settings,
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        private static EnemyChargeTimingAuthoringSettings ToAuthoring(EnemyChargeTimingSettings settings)
        {
            return EnemyChargeTimingAuthoringSettings.FromRuntimeSettings(
                settings,
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        private static EnemyJumpTimingAuthoringSettings ToAuthoring(EnemyJumpTimingSettings settings)
        {
            return EnemyJumpTimingAuthoringSettings.FromRuntimeSettings(
                settings,
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        private static void Collect(
            ICollection<ScriptableObject> assets,
            ISet<int> seenInstanceIds,
            ScriptableObject asset)
        {
            if (asset == null)
            {
                return;
            }

            if (seenInstanceIds.Add(asset.GetInstanceID()))
            {
                assets.Add(asset);
            }
        }

        private sealed class TestPhaseThroughLockedTargetCapabilityAsset : EnemyMovementSkillCapabilityAsset
        {
            [SerializeField] private EnemyJumpTimingAuthoringSettings jumpTimingSettings = new(0f, 0f, 0f);

            public override MovementSkillStrategyKind Kind => MovementSkillStrategyKind.PhaseThroughLockedTarget;

            public override EnemyJumpTimingAuthoringSettings JumpTimingSettings => jumpTimingSettings;
        }
    }
}
