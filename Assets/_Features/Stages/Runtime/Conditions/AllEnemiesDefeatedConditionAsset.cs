using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Conditions/All Enemies Defeated",
        fileName = "AllEnemiesDefeatedCondition")]
    public sealed class AllEnemiesDefeatedConditionAsset : StageConditionAsset
    {
        public override void Validate(in StageConditionValidationContext context)
        {
        }

        internal override StageConditionRuntimeDefinition Compile(in StageConditionCompilationContext context)
        {
            return new AllEnemiesDefeatedConditionRuntimeDefinition(
                CreateConditionId(this),
                string.IsNullOrWhiteSpace(name) ? nameof(AllEnemiesDefeatedConditionAsset) : name);
        }

        private sealed class AllEnemiesDefeatedConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            public AllEnemiesDefeatedConditionRuntimeDefinition(string conditionId, string displayName)
                : base(conditionId, displayName)
            {
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new AllEnemiesDefeatedConditionRuntime(ConditionId, DisplayName);
            }
        }

        private sealed class AllEnemiesDefeatedConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly string _displayName;
            private readonly List<EntityState> _entities = new();
            private int _remainingEnemies;

            public AllEnemiesDefeatedConditionRuntime(string conditionId, string displayName)
            {
                _conditionId = conditionId;
                _displayName = displayName;
            }

            public bool IsSatisfied => _remainingEnemies == 0;

            public void Reset()
            {
                _remainingEnemies = 0;
                _entities.Clear();
            }

            public void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
            {
                if (finalSnapshot == null)
                {
                    throw new ArgumentNullException(nameof(finalSnapshot));
                }

                finalSnapshot.EnumerateEntitiesOrdered(_entities);
                var remainingEnemies = 0;

                for (var i = 0; i < _entities.Count; i++)
                {
                    var entity = _entities[i];
                    if (!EntityRolePolicy.IsEnemyUnit(entity) ||
                        entity.hp <= 0 ||
                        entity.markedForDeath)
                    {
                        continue;
                    }

                    remainingEnemies++;
                }

                _remainingEnemies = remainingEnemies;
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    nameof(AllEnemiesDefeatedConditionAsset),
                    IsSatisfied,
                    $"RemainingEnemies={_remainingEnemies}");
            }
        }
    }
}
