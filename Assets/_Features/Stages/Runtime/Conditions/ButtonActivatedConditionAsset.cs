using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Conditions/Button Activated",
        fileName = "ButtonActivatedCondition")]
    public sealed class ButtonActivatedConditionAsset : StageConditionAsset
    {
        [SerializeField] private int tileId;

        public int TileId => tileId;

        public override void Validate(in StageConditionValidationContext context)
        {
            if (tileId <= 0)
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' requires a positive tile id.");
            }

            if (!context.TryGetTileFeature(tileId, out var tileFeature))
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' references unknown button TileId {tileId}.");
            }

            if (tileFeature.Kind != TileFeatureKind.Button)
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' references TileId {tileId}, but that TileFeature is {tileFeature.Kind} instead of Button.");
            }

            if (tileFeature.BoxSelector == TileFeatureBoxSelector.MoonBlockOnly)
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' references Button TileId {tileId} with unsupported MoonBlockOnly selector.");
            }
        }

        internal override StageConditionRuntimeDefinition Compile(in StageConditionCompilationContext context)
        {
            if (!context.TryGetTileFeatureRuntimeDefinition(tileId, out _))
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' references Button TileId {tileId}, but no TileFeatureRuntimeDefinition was generated.");
            }

            return new ButtonActivatedConditionRuntimeDefinition(
                CreateConditionId(this, tileId.ToString()),
                string.IsNullOrWhiteSpace(name) ? nameof(ButtonActivatedConditionAsset) : name,
                tileId);
        }

        private sealed class ButtonActivatedConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            private readonly int _tileId;

            public ButtonActivatedConditionRuntimeDefinition(
                string conditionId,
                string displayName,
                int tileId)
                : base(conditionId, displayName)
            {
                _tileId = tileId;
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new ButtonActivatedConditionRuntime(ConditionId, DisplayName, _tileId);
            }
        }

        private sealed class ButtonActivatedConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly string _displayName;
            private readonly int _tileId;
            private bool _isSatisfied;

            public ButtonActivatedConditionRuntime(string conditionId, string displayName, int tileId)
            {
                _conditionId = conditionId;
                _displayName = displayName;
                _tileId = tileId;
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

                _isSatisfied =
                    finalSnapshot.TryGetTileFeature(_tileId, out var tileFeature) &&
                    tileFeature.Kind == TileFeatureKind.Button &&
                    (tileFeature.Flags & TileFeatureFlags.Activated) != 0;
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    nameof(ButtonActivatedConditionAsset),
                    _isSatisfied,
                    $"TileId={_tileId}|Activated={(_isSatisfied ? 1 : 0)}");
            }
        }
    }
}
