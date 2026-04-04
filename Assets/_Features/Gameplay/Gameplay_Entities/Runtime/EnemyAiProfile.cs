using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy AI Profile", fileName = "EnemyAiProfile")]
    public sealed class EnemyAiProfile : ScriptableObject
    {
        [SerializeField] private EnemyAiStateResolverKind stateResolverKind = EnemyAiStateResolverKind.Default;
        [SerializeField] private PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Forward;
        [SerializeField] private DetectionStrategyKind detectionStrategyKind = DetectionStrategyKind.NearestOpponent;
        [SerializeField] private ChaseStrategyKind chaseStrategyKind = ChaseStrategyKind.AxisPriority;
        [SerializeField] private AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee;
        [SerializeField] private EnemyAiCommonSettings commonSettings = new(50, 50, 1);
        [SerializeField] private PatrolSettings patrolSettings = new(PatrolBlockedMovementResponse.Stop);
        [SerializeField] private DetectionSettings detectionSettings = new(8, true, false);
        [SerializeField] private ChaseSettings chaseSettings = new(ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak, true);
        [SerializeField] private AttackDecisionSettings attackDecisionSettings = new(1);

        public EnemyAiStateResolverKind StateResolverKind => stateResolverKind;

        public PatrolStrategyKind PatrolStrategyKind => patrolStrategyKind;

        public DetectionStrategyKind DetectionStrategyKind => detectionStrategyKind;

        public ChaseStrategyKind ChaseStrategyKind => chaseStrategyKind;

        public AttackDecisionStrategyKind AttackDecisionStrategyKind => attackDecisionStrategyKind;

        public EnemyAiCommonSettings CommonSettings => commonSettings;

        public PatrolSettings PatrolSettings => patrolSettings;

        public DetectionSettings DetectionSettings => detectionSettings;

        public ChaseSettings ChaseSettings => chaseSettings;

        public AttackDecisionSettings AttackDecisionSettings => attackDecisionSettings;

        public void ResetToDefaultMelee()
        {
            ApplyConfiguration(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                EnemyAiStateResolverKind.Default,
                PatrolStrategyKind.Forward,
                DetectionStrategyKind.NearestOpponent,
                ChaseStrategyKind.AxisPriority,
                AttackDecisionStrategyKind.Melee);
        }

        public void ApplyConfiguration(
            EnemyAiCommonSettings commonSettings,
            PatrolSettings patrolSettings,
            DetectionSettings detectionSettings,
            ChaseSettings chaseSettings,
            AttackDecisionSettings attackDecisionSettings,
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
            this.stateResolverKind = stateResolverKind;
            this.patrolStrategyKind = patrolStrategyKind;
            this.detectionStrategyKind = detectionStrategyKind;
            this.chaseStrategyKind = chaseStrategyKind;
            this.attackDecisionStrategyKind = attackDecisionStrategyKind;
        }

        public EnemyAiRuntimeDefinition CreateRuntimeDefinition()
        {
            return EnemyAiRuntimeDefinition.CreateFromProfile(this);
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

        public static EnemyAiProfile CreateRuntimeInstance(
            EnemyAiCommonSettings commonSettings,
            PatrolSettings patrolSettings,
            DetectionSettings detectionSettings,
            ChaseSettings chaseSettings,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAiStateResolverKind stateResolverKind = EnemyAiStateResolverKind.Default,
            PatrolStrategyKind patrolStrategyKind = PatrolStrategyKind.Forward,
            DetectionStrategyKind detectionStrategyKind = DetectionStrategyKind.NearestOpponent,
            ChaseStrategyKind chaseStrategyKind = ChaseStrategyKind.AxisPriority,
            AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee)
        {
            var profile = CreateInstance<EnemyAiProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.ApplyConfiguration(
                commonSettings,
                patrolSettings,
                detectionSettings,
                chaseSettings,
                attackDecisionSettings,
                stateResolverKind,
                patrolStrategyKind,
                detectionStrategyKind,
                chaseStrategyKind,
                attackDecisionStrategyKind);
            return profile;
        }
    }
}
