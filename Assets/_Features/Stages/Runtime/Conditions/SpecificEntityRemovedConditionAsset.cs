using System;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Conditions/Specific Entity Removed",
        fileName = "SpecificEntityRemovedCondition")]
    public sealed class SpecificEntityRemovedConditionAsset : StageConditionAsset
    {
        [SerializeField] private int entityId;

        public int EntityId => entityId;

        public override void Validate(in StageConditionValidationContext context)
        {
            if (entityId <= 0)
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' requires a positive entity id.");
            }
        }

        internal override StageConditionRuntimeDefinition Compile(in StageConditionCompilationContext context)
        {
            return new SpecificEntityRemovedConditionRuntimeDefinition(
                CreateConditionId(this, entityId.ToString()),
                string.IsNullOrWhiteSpace(name) ? nameof(SpecificEntityRemovedConditionAsset) : name,
                entityId);
        }

        private sealed class SpecificEntityRemovedConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            private readonly int _entityId;

            public SpecificEntityRemovedConditionRuntimeDefinition(string conditionId, string displayName, int entityId)
                : base(conditionId, displayName)
            {
                _entityId = entityId;
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new SpecificEntityRemovedConditionRuntime(ConditionId, DisplayName, _entityId);
            }
        }

        private sealed class SpecificEntityRemovedConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly string _displayName;
            private readonly int _entityId;
            private bool _isSatisfied;

            public SpecificEntityRemovedConditionRuntime(string conditionId, string displayName, int entityId)
            {
                _conditionId = conditionId;
                _displayName = displayName;
                _entityId = entityId;
            }

            public bool IsSatisfied => _isSatisfied;

            public void Reset()
            {
                _isSatisfied = false;
            }

            public void Advance(Game.Feature.Gameplay.BoardState.WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
            {
                if (_isSatisfied)
                {
                    return;
                }

                var removedEntityIds = tickFacts.CleanupRemovedEntityIds;
                for (var i = 0; i < removedEntityIds.Count; i++)
                {
                    if (removedEntityIds[i] == _entityId)
                    {
                        _isSatisfied = true;
                        return;
                    }
                }
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    nameof(SpecificEntityRemovedConditionAsset),
                    _isSatisfied,
                    $"EntityId={_entityId}");
            }
        }
    }
}
