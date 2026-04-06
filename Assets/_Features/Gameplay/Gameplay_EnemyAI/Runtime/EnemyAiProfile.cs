using Game.Feature.Gameplay.Loop;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Feature.Gameplay.Entities
{
    /// <summary>
    /// Authoritative enemy AI profile.
    /// Locomotion cooldown cadence belongs here and resolves into runtime definitions and world-state counters,
    /// never prefab-local presentation authoring.
    /// </summary>
    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy AI Profile", fileName = "EnemyAiProfile")]
    public sealed class EnemyAiProfile : ScriptableObject, ISerializationCallbackReceiver
    {
        private const int CurrentSerializedVersion = 3;
        private const int AttackTimingAuthoringSerializedVersion = 1;
        private const int LocomotionTimingSerializedVersion = 2;
        private const int PatrolWallFollowSettingsSerializedVersion = 3;

        [SerializeField] private EnemyAiStateResolverKind stateResolverKind = EnemyAiStateResolverKind.Default;
        [SerializeField] private PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Forward;
        [SerializeField] private DetectionStrategyKind detectionStrategyKind = DetectionStrategyKind.NearestOpponent;
        [SerializeField] private ChaseStrategyKind chaseStrategyKind = ChaseStrategyKind.AxisPriority;
        [SerializeField] private AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee;
        [SerializeField] private EnemyAiCommonAuthoringSettings commonSettings = new(50, 50, 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        [SerializeField] private PatrolSettings patrolSettings = new(PatrolBlockedMovementResponse.Stop);
        [SerializeField] private DetectionSettings detectionSettings = new(8, true, false);
        [SerializeField] private ChaseSettings chaseSettings = new(ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak, true);
        [SerializeField] private AttackDecisionSettings attackDecisionSettings = new(1);
        [SerializeField] private EnemyAttackTimingAuthoringSettings attackTimingSettings = new(0f);
        [SerializeField] private EnemyLocomotionTimingAuthoringSettings locomotionTimingSettings = new(0f);
        [SerializeField] [HideInInspector] private int serializedVersion = CurrentSerializedVersion;
        [FormerlySerializedAs("commonSettings")]
        [SerializeField] [HideInInspector] private EnemyAiCommonSettings legacyCommonSettings = EnemyAiCommonSettings.CreateDefaultMelee();
        [FormerlySerializedAs("attackTimingSettings")]
        [SerializeField] [HideInInspector] private EnemyAttackTimingSettings legacyAttackTimingSettings = EnemyAttackTimingSettings.CreateDefaultMelee();

        public EnemyAiStateResolverKind StateResolverKind => stateResolverKind;

        public PatrolStrategyKind PatrolStrategyKind => patrolStrategyKind;

        public DetectionStrategyKind DetectionStrategyKind => detectionStrategyKind;

        public ChaseStrategyKind ChaseStrategyKind => chaseStrategyKind;

        public AttackDecisionStrategyKind AttackDecisionStrategyKind => attackDecisionStrategyKind;

        public EnemyAiCommonAuthoringSettings CommonSettings => commonSettings;

        public PatrolSettings PatrolSettings => patrolSettings;

        public DetectionSettings DetectionSettings => detectionSettings;

        public ChaseSettings ChaseSettings => chaseSettings;

        public AttackDecisionSettings AttackDecisionSettings => attackDecisionSettings;

        public EnemyAttackTimingAuthoringSettings AttackTimingSettings => attackTimingSettings;

        /// <summary>
        /// Authoritative locomotion cadence authoring that is converted into tick-based runtime settings.
        /// </summary>
        public EnemyLocomotionTimingAuthoringSettings LocomotionTimingSettings => locomotionTimingSettings;

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
                EnemyAiStateResolverKind.Default,
                PatrolStrategyKind.Forward,
                DetectionStrategyKind.NearestOpponent,
                ChaseStrategyKind.AxisPriority,
                AttackDecisionStrategyKind.Melee);
        }

        public void ApplyConfiguration(
            EnemyAiCommonAuthoringSettings commonSettings,
            PatrolSettings patrolSettings,
            DetectionSettings detectionSettings,
            ChaseSettings chaseSettings,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAttackTimingAuthoringSettings attackTimingSettings = default,
            EnemyLocomotionTimingAuthoringSettings locomotionTimingSettings = default,
            EnemyAiStateResolverKind stateResolverKind = EnemyAiStateResolverKind.Default,
            PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Forward,
            DetectionStrategyKind detectionStrategyKind = DetectionStrategyKind.NearestOpponent,
            ChaseStrategyKind chaseStrategyKind = ChaseStrategyKind.AxisPriority,
            AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee)
        {
            this.commonSettings = commonSettings;
            this.patrolSettings = patrolSettings;
            this.detectionSettings = detectionSettings;
            this.chaseSettings = chaseSettings;
            this.attackDecisionSettings = attackDecisionSettings;
            this.attackTimingSettings = attackTimingSettings;
            this.locomotionTimingSettings = locomotionTimingSettings;
            this.stateResolverKind = stateResolverKind;
            this.patrolStrategyKind = patrolStrategyKind;
            this.detectionStrategyKind = detectionStrategyKind;
            this.chaseStrategyKind = chaseStrategyKind;
            this.attackDecisionStrategyKind = attackDecisionStrategyKind;
            serializedVersion = CurrentSerializedVersion;
        }

        public EnemyAiRuntimeDefinition CreateRuntimeDefinition(int simulationTicksPerSecond)
        {
            return EnemyAiRuntimeDefinition.CreateFromProfile(this, simulationTicksPerSecond);
        }

        public void OnBeforeSerialize()
        {
            serializedVersion = CurrentSerializedVersion;
        }

        public void OnAfterDeserialize()
        {
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

        public static EnemyAiProfile CreateRuntimeInstance(
            EnemyAiCommonSettings commonSettings,
            PatrolSettings patrolSettings,
            DetectionSettings detectionSettings,
            ChaseSettings chaseSettings,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAttackTimingSettings attackTimingSettings = default,
            EnemyLocomotionTimingSettings locomotionTimingSettings = default,
            EnemyAiStateResolverKind stateResolverKind = EnemyAiStateResolverKind.Default,
            PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Forward,
            DetectionStrategyKind detectionStrategyKind = DetectionStrategyKind.NearestOpponent,
            ChaseStrategyKind chaseStrategyKind = ChaseStrategyKind.AxisPriority,
            AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee)
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
                stateResolverKind,
                patrolStrategyKind,
                detectionStrategyKind,
                chaseStrategyKind,
                attackDecisionStrategyKind);
            profile.legacyCommonSettings = commonSettings;
            profile.legacyAttackTimingSettings = attackTimingSettings;
            return profile;
        }
    }
}
