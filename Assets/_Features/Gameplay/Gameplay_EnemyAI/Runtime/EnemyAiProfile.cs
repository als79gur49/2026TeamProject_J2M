using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    /// <summary>
    /// Authoritative enemy AI profile.
    /// Locomotion cadence belongs here and resolves into runtime definitions and world-state counters.
    /// </summary>
    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy AI Profile", fileName = "EnemyAiProfile")]
    public sealed class EnemyAiProfile : ScriptableObject, ISerializationCallbackReceiver
    {
        private const int CurrentSerializedVersion = 5;
        private const int AttackTimingAuthoringSerializedVersion = 1;
        private const int LocomotionTimingSerializedVersion = 2;
        private const int PatrolWallFollowSettingsSerializedVersion = 3;
        private const int JumpMovementSkillSerializedVersion = 4;

        [SerializeField] private EnemyCoreAuthoring coreAuthoring;
        [SerializeField] private EnemyBrainAuthoring brainAuthoring;
        [SerializeField] private List<EnemyCapabilityAsset> capabilityAssets = new();

        [SerializeField] [HideInInspector] private EnemyAiStateResolverKind stateResolverKind = EnemyAiStateResolverKind.Default;
        [SerializeField] [HideInInspector] private PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Forward;
        [SerializeField] [HideInInspector] private DetectionStrategyKind detectionStrategyKind = DetectionStrategyKind.NearestOpponent;
        [SerializeField] [HideInInspector] private ChaseStrategyKind chaseStrategyKind = ChaseStrategyKind.AxisPriority;
        [SerializeField] [HideInInspector] private AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee;
        [SerializeField] [HideInInspector] private MovementSkillStrategyKind movementSkillStrategyKind = MovementSkillStrategyKind.None;
        [SerializeField] [HideInInspector] private EnemyAiCommonAuthoringSettings commonSettings = new(50, 50, 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        [SerializeField] [HideInInspector] private PatrolSettings patrolSettings = new(PatrolBlockedMovementResponse.Stop);
        [SerializeField] [HideInInspector] private DetectionSettings detectionSettings = new(8, true, false);
        [SerializeField] [HideInInspector] private ChaseSettings chaseSettings = new(ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak, true);
        [SerializeField] [HideInInspector] private AttackDecisionSettings attackDecisionSettings = new(1);
        [SerializeField] [HideInInspector] private EnemyAttackTimingAuthoringSettings attackTimingSettings = new(0f);
        [SerializeField] [HideInInspector] private EnemyLocomotionTimingAuthoringSettings locomotionTimingSettings = new(0f);
        [SerializeField] [HideInInspector] private EnemyJumpTimingAuthoringSettings jumpTimingSettings = new(0f, 0f, 0f);
        [SerializeField] [HideInInspector] private int serializedVersion = CurrentSerializedVersion;
        [SerializeField] [HideInInspector] private EnemyAiCommonSettings legacyCommonSettings = EnemyAiCommonSettings.CreateDefaultMelee();
        [SerializeField] [HideInInspector] private EnemyAttackTimingSettings legacyAttackTimingSettings = EnemyAttackTimingSettings.CreateDefaultMelee();

        internal EnemyCoreAuthoring CoreAuthoring => coreAuthoring;

        internal EnemyBrainAuthoring BrainAuthoring => brainAuthoring;

        internal IReadOnlyList<EnemyCapabilityAsset> CapabilityAssets => capabilityAssets;

        internal bool UsesAuthoringConfiguration =>
            coreAuthoring != null ||
            brainAuthoring != null ||
            (capabilityAssets != null && capabilityAssets.Count > 0);

        internal EnemyAiStateResolverKind LegacyStateResolverKind => stateResolverKind;

        internal PatrolStrategyKind LegacyPatrolStrategyKind => patrolStrategyKind;

        internal DetectionStrategyKind LegacyDetectionStrategyKind => detectionStrategyKind;

        internal ChaseStrategyKind LegacyChaseStrategyKind => chaseStrategyKind;

        internal AttackDecisionStrategyKind LegacyAttackDecisionStrategyKind => attackDecisionStrategyKind;

        internal MovementSkillStrategyKind LegacyMovementSkillStrategyKind => movementSkillStrategyKind;

        internal EnemyAiCommonAuthoringSettings LegacyCommonSettings => commonSettings;

        internal PatrolSettings LegacyPatrolSettings => patrolSettings;

        internal DetectionSettings LegacyDetectionSettings => detectionSettings;

        internal ChaseSettings LegacyChaseSettings => chaseSettings;

        internal AttackDecisionSettings LegacyAttackDecisionSettings => attackDecisionSettings;

        internal EnemyAttackTimingAuthoringSettings LegacyAttackTimingSettings => attackTimingSettings;

        internal EnemyLocomotionTimingAuthoringSettings LegacyLocomotionTimingSettings => locomotionTimingSettings;

        internal EnemyJumpTimingAuthoringSettings LegacyJumpTimingSettings => jumpTimingSettings;

        public EnemyAiStateResolverKind StateResolverKind => coreAuthoring == null && brainAuthoring == null
            ? stateResolverKind
            : brainAuthoring?.StateResolver?.Kind ?? stateResolverKind;

        public PatrolStrategyKind PatrolStrategyKind => coreAuthoring == null && brainAuthoring == null
            ? patrolStrategyKind
            : brainAuthoring?.PatrolStrategy?.Kind ?? patrolStrategyKind;

        public DetectionStrategyKind DetectionStrategyKind => coreAuthoring == null && brainAuthoring == null
            ? detectionStrategyKind
            : brainAuthoring?.DetectionStrategy?.Kind ?? detectionStrategyKind;

        public ChaseStrategyKind ChaseStrategyKind => coreAuthoring == null && brainAuthoring == null
            ? chaseStrategyKind
            : brainAuthoring?.ChaseStrategy?.Kind ?? chaseStrategyKind;

        public AttackDecisionStrategyKind AttackDecisionStrategyKind
        {
            get
            {
                if (!UsesAuthoringConfiguration)
                {
                    return attackDecisionStrategyKind;
                }

                var combat = GetCombatCapabilityAsset();
                return combat?.Kind ?? global::Game.Feature.Gameplay.Entities.AttackDecisionStrategyKind.None;
            }
        }

        public MovementSkillStrategyKind MovementSkillStrategyKind
        {
            get
            {
                if (!UsesAuthoringConfiguration)
                {
                    return movementSkillStrategyKind;
                }

                var movementSkill = GetMovementSkillCapabilityAsset();
                return movementSkill?.Kind ?? global::Game.Feature.Gameplay.Entities.MovementSkillStrategyKind.None;
            }
        }

        public EnemyAiCommonAuthoringSettings CommonSettings => coreAuthoring != null
            ? coreAuthoring.CommonSettings
            : commonSettings;

        public PatrolSettings PatrolSettings => brainAuthoring?.PatrolStrategy?.Settings ?? patrolSettings;

        public DetectionSettings DetectionSettings => brainAuthoring?.DetectionStrategy?.Settings ?? detectionSettings;

        public ChaseSettings ChaseSettings => brainAuthoring?.ChaseStrategy?.Settings ?? chaseSettings;

        public AttackDecisionSettings AttackDecisionSettings => GetCombatCapabilityAsset()?.AttackDecisionSettings ?? attackDecisionSettings;

        public EnemyAttackTimingAuthoringSettings AttackTimingSettings => GetCombatCapabilityAsset()?.AttackTimingSettings ?? attackTimingSettings;

        public EnemyLocomotionTimingAuthoringSettings LocomotionTimingSettings => coreAuthoring != null
            ? coreAuthoring.LocomotionTimingSettings
            : locomotionTimingSettings;

        public EnemyJumpTimingAuthoringSettings JumpTimingSettings => GetMovementSkillCapabilityAsset()?.JumpTimingSettings ?? jumpTimingSettings;

        public void ResetToDefaultMelee()
        {
            ApplyConfiguration(
                EnemyAiCommonAuthoringSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                EnemyAttackTimingAuthoringSettings.CreateDefaultMelee(),
                EnemyLocomotionTimingAuthoringSettings.CreateDefaultMelee(),
                EnemyJumpTimingAuthoringSettings.CreateDefault(),
                EnemyAiStateResolverKind.Default,
                PatrolStrategyKind.Forward,
                DetectionStrategyKind.NearestOpponent,
                ChaseStrategyKind.AxisPriority,
                AttackDecisionStrategyKind.Melee,
                MovementSkillStrategyKind.None);
        }

        public void ApplyConfiguration(
            EnemyAiCommonAuthoringSettings commonSettings,
            PatrolSettings patrolSettings,
            DetectionSettings detectionSettings,
            ChaseSettings chaseSettings,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAttackTimingAuthoringSettings attackTimingSettings = default,
            EnemyLocomotionTimingAuthoringSettings locomotionTimingSettings = default,
            EnemyJumpTimingAuthoringSettings jumpTimingSettings = default,
            EnemyAiStateResolverKind stateResolverKind = EnemyAiStateResolverKind.Default,
            PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Forward,
            DetectionStrategyKind detectionStrategyKind = DetectionStrategyKind.NearestOpponent,
            ChaseStrategyKind chaseStrategyKind = ChaseStrategyKind.AxisPriority,
            AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee,
            MovementSkillStrategyKind movementSkillStrategyKind = MovementSkillStrategyKind.None)
        {
            coreAuthoring = null;
            brainAuthoring = null;
            capabilityAssets ??= new List<EnemyCapabilityAsset>();
            capabilityAssets.Clear();

            this.commonSettings = commonSettings;
            this.patrolSettings = patrolSettings;
            this.detectionSettings = detectionSettings;
            this.chaseSettings = chaseSettings;
            this.attackDecisionSettings = attackDecisionSettings;
            this.attackTimingSettings = attackTimingSettings;
            this.locomotionTimingSettings = locomotionTimingSettings;
            this.jumpTimingSettings = jumpTimingSettings;
            this.stateResolverKind = stateResolverKind;
            this.patrolStrategyKind = patrolStrategyKind;
            this.detectionStrategyKind = detectionStrategyKind;
            this.chaseStrategyKind = chaseStrategyKind;
            this.attackDecisionStrategyKind = attackDecisionStrategyKind;
            this.movementSkillStrategyKind = movementSkillStrategyKind;
            serializedVersion = CurrentSerializedVersion;
        }

        public EnemyAiRuntimeDefinition CreateRuntimeDefinition(int simulationTicksPerSecond)
        {
            return EnemyAiProfileCompiler.Compile(this, simulationTicksPerSecond);
        }

        public void OnBeforeSerialize()
        {
            capabilityAssets ??= new List<EnemyCapabilityAsset>();
            serializedVersion = CurrentSerializedVersion;
        }

        public void OnAfterDeserialize()
        {
            capabilityAssets ??= new List<EnemyCapabilityAsset>();

            if (serializedVersion >= CurrentSerializedVersion)
            {
                return;
            }

            if (serializedVersion < AttackTimingAuthoringSerializedVersion)
            {
                commonSettings = EnemyAiCommonAuthoringSettings.FromRuntimeSettings(
                    legacyCommonSettings,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                attackTimingSettings = EnemyAttackTimingAuthoringSettings.FromRuntimeSettings(
                    legacyAttackTimingSettings,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            }

            if (serializedVersion < PatrolWallFollowSettingsSerializedVersion)
            {
                patrolSettings = new PatrolSettings(patrolSettings.BlockedMovementResponse);
            }

            if (serializedVersion < LocomotionTimingSerializedVersion)
            {
                locomotionTimingSettings = EnemyLocomotionTimingAuthoringSettings.CreateDefaultMelee();
            }

            if (serializedVersion < JumpMovementSkillSerializedVersion)
            {
                movementSkillStrategyKind = MovementSkillStrategyKind.None;
                jumpTimingSettings = EnemyJumpTimingAuthoringSettings.CreateDefault();
            }

            serializedVersion = CurrentSerializedVersion;
        }

        public static EnemyAiProfile CreateRuntimeDefault()
        {
            return CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee());
        }

        public static EnemyAiProfile CreateRuntimeNonAttacking()
        {
            return CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                attackDecisionStrategyKind: AttackDecisionStrategyKind.None);
        }

        public static EnemyAiProfile CreateRuntimeCharging()
        {
            return CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                stateResolverKind: EnemyAiStateResolverKind.Charge,
                attackDecisionStrategyKind: AttackDecisionStrategyKind.None);
        }

        public static EnemyAiProfile CreateRuntimeWallFollower(
            WallFollowTurnPreference turnPreference = WallFollowTurnPreference.Right,
            int moveCooldownTicks = 0)
        {
            return CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                new PatrolSettings(
                    PatrolBlockedMovementResponse.Stop,
                    turnPreference,
                    followWalls: true,
                    followBoxes: true),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                EnemyAttackTimingSettings.CreateDefaultMelee(),
                new EnemyLocomotionTimingSettings(moveCooldownTicks),
                patrolStrategyKind: PatrolStrategyKind.WallFollow,
                detectionStrategyKind: DetectionStrategyKind.None,
                attackDecisionStrategyKind: AttackDecisionStrategyKind.None);
        }

        public static EnemyAiProfile CreateRuntimeContactDamage()
        {
            return CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                attackDecisionStrategyKind: AttackDecisionStrategyKind.ContactSameCell);
        }

        public static EnemyAiProfile CreateRuntimeJumpChaser(
            EnemyJumpTimingSettings jumpTimingSettings)
        {
            return CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                EnemyAttackTimingSettings.CreateDefaultMelee(),
                EnemyLocomotionTimingSettings.CreateDefaultMelee(),
                jumpTimingSettings,
                attackDecisionStrategyKind: AttackDecisionStrategyKind.None,
                movementSkillStrategyKind: MovementSkillStrategyKind.JumpToLockedTarget);
        }

        public static EnemyAiProfile CreateRuntimeInstance(
            EnemyAiCommonSettings commonSettings,
            PatrolSettings patrolSettings,
            DetectionSettings detectionSettings,
            ChaseSettings chaseSettings,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAttackTimingSettings attackTimingSettings = default,
            EnemyLocomotionTimingSettings locomotionTimingSettings = default,
            EnemyJumpTimingSettings jumpTimingSettings = default,
            EnemyAiStateResolverKind stateResolverKind = EnemyAiStateResolverKind.Default,
            PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Forward,
            DetectionStrategyKind detectionStrategyKind = DetectionStrategyKind.NearestOpponent,
            ChaseStrategyKind chaseStrategyKind = ChaseStrategyKind.AxisPriority,
            AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee,
            MovementSkillStrategyKind movementSkillStrategyKind = MovementSkillStrategyKind.None)
        {
            var profile = CreateInstance<EnemyAiProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.ApplyConfiguration(
                EnemyAiCommonAuthoringSettings.FromRuntimeSettings(
                    commonSettings,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                patrolSettings,
                detectionSettings,
                chaseSettings,
                attackDecisionSettings,
                EnemyAttackTimingAuthoringSettings.FromRuntimeSettings(
                    attackTimingSettings,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                EnemyLocomotionTimingAuthoringSettings.FromRuntimeSettings(
                    locomotionTimingSettings,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                EnemyJumpTimingAuthoringSettings.FromRuntimeSettings(
                    jumpTimingSettings,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                stateResolverKind,
                patrolStrategyKind,
                detectionStrategyKind,
                chaseStrategyKind,
                attackDecisionStrategyKind,
                movementSkillStrategyKind);
            profile.legacyCommonSettings = commonSettings;
            profile.legacyAttackTimingSettings = attackTimingSettings;
            return profile;
        }

        private EnemyCombatCapabilityAsset GetCombatCapabilityAsset()
        {
            if (capabilityAssets == null)
            {
                return null;
            }

            for (var i = 0; i < capabilityAssets.Count; i++)
            {
                if (capabilityAssets[i] is EnemyCombatCapabilityAsset combatCapability)
                {
                    return combatCapability;
                }
            }

            return null;
        }

        private EnemyMovementSkillCapabilityAsset GetMovementSkillCapabilityAsset()
        {
            if (capabilityAssets == null)
            {
                return null;
            }

            for (var i = 0; i < capabilityAssets.Count; i++)
            {
                if (capabilityAssets[i] is EnemyMovementSkillCapabilityAsset movementSkillCapability)
                {
                    return movementSkillCapability;
                }
            }

            return null;
        }
    }
}
