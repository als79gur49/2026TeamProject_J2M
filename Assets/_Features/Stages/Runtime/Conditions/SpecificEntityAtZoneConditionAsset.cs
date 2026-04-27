using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Conditions/Specific Entity At Zone",
        fileName = "SpecificEntityAtZoneCondition")]
    public sealed class SpecificEntityAtZoneConditionAsset : StageConditionAsset
    {
        [SerializeField] private int entityId;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private bool requireAlive = true;

        public int EntityId => entityId;

        public string ZoneId => zoneId ?? string.Empty;

        public bool RequireAlive => requireAlive;

        public override void Validate(in StageConditionValidationContext context)
        {
            if (entityId <= 0)
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' requires a positive entity id.");
            }

            if (string.IsNullOrWhiteSpace(zoneId))
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' requires a non-empty zone id.");
            }

            if (!context.TryGetZone(zoneId, out _))
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' references unknown zone id '{zoneId.Trim()}'.");
            }
        }

        internal override StageConditionRuntimeDefinition Compile(in StageConditionCompilationContext context)
        {
            if (!context.TryGetZone(zoneId, out var targetZone))
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' references unknown zone id '{ZoneId}'.");
            }

            return new SpecificEntityAtZoneConditionRuntimeDefinition(
                CreateConditionId(this, $"{entityId}:{targetZone.ZoneId}"),
                string.IsNullOrWhiteSpace(name) ? nameof(SpecificEntityAtZoneConditionAsset) : name,
                entityId,
                targetZone,
                requireAlive);
        }

        private sealed class SpecificEntityAtZoneConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            private readonly int _entityId;
            private readonly bool _requireAlive;
            private readonly StageZoneRuntimeDefinition _targetZone;

            public SpecificEntityAtZoneConditionRuntimeDefinition(
                string conditionId,
                string displayName,
                int entityId,
                StageZoneRuntimeDefinition targetZone,
                bool requireAlive)
                : base(conditionId, displayName)
            {
                _entityId = entityId;
                _targetZone = targetZone;
                _requireAlive = requireAlive;
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new SpecificEntityAtZoneConditionRuntime(
                    ConditionId,
                    DisplayName,
                    _entityId,
                    _targetZone,
                    _requireAlive);
            }
        }

        private sealed class SpecificEntityAtZoneConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly string _displayName;
            private readonly int _entityId;
            private readonly bool _requireAlive;
            private readonly StageZoneRuntimeDefinition _targetZone;
            private bool _isSatisfied;

            public SpecificEntityAtZoneConditionRuntime(
                string conditionId,
                string displayName,
                int entityId,
                StageZoneRuntimeDefinition targetZone,
                bool requireAlive)
            {
                _conditionId = conditionId;
                _displayName = displayName;
                _entityId = entityId;
                _targetZone = targetZone;
                _requireAlive = requireAlive;
            }

            public bool IsSatisfied => _isSatisfied;

            public void Reset()
            {
                _isSatisfied = false;
            }

            public void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
            {
                if (finalSnapshot == null)
                {
                    throw new ArgumentNullException(nameof(finalSnapshot));
                }

                if (!finalSnapshot.TryGetEntity(_entityId, out var entity))
                {
                    _isSatisfied = false;
                    return;
                }

                if (_requireAlive && (entity.hp <= 0 || entity.markedForDeath))
                {
                    _isSatisfied = false;
                    return;
                }

                _isSatisfied = _targetZone.Contains(entity.position);
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    nameof(SpecificEntityAtZoneConditionAsset),
                    _isSatisfied,
                    $"EntityId={_entityId}|ZoneId={_targetZone.ZoneId}|RequireAlive={(_requireAlive ? 1 : 0)}|InZone={(_isSatisfied ? 1 : 0)}");
            }
        }
    }
}
