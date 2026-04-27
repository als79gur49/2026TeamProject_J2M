using System;
using System.Globalization;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Conditions/Clear Within Time Limit",
        fileName = "ClearWithinTimeLimitCondition")]
    public sealed class ClearWithinTimeLimitConditionAsset : StageConditionAsset
    {
        [SerializeField] private float clearBeforeOrAtSeconds = 60f;

        public float ClearBeforeOrAtSeconds => clearBeforeOrAtSeconds;

        public override void Validate(in StageConditionValidationContext context)
        {
            if (float.IsNaN(clearBeforeOrAtSeconds) ||
                float.IsInfinity(clearBeforeOrAtSeconds) ||
                clearBeforeOrAtSeconds <= 0f)
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' requires a finite positive clear time in seconds.");
            }
        }

        internal override StageConditionRuntimeDefinition Compile(in StageConditionCompilationContext context)
        {
            var clearBeforeOrAtTick = context.Timing.SecondsToTicksCeil(clearBeforeOrAtSeconds);
            return new ClearWithinTimeLimitConditionRuntimeDefinition(
                CreateConditionId(this, clearBeforeOrAtSeconds.ToString("0.###", CultureInfo.InvariantCulture)),
                string.IsNullOrWhiteSpace(name) ? nameof(ClearWithinTimeLimitConditionAsset) : name,
                clearBeforeOrAtSeconds,
                clearBeforeOrAtTick);
        }

        private sealed class ClearWithinTimeLimitConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            private readonly int _clearBeforeOrAtTick;
            private readonly float _clearBeforeOrAtSeconds;

            public ClearWithinTimeLimitConditionRuntimeDefinition(
                string conditionId,
                string displayName,
                float clearBeforeOrAtSeconds,
                int clearBeforeOrAtTick)
                : base(conditionId, displayName)
            {
                _clearBeforeOrAtSeconds = clearBeforeOrAtSeconds;
                _clearBeforeOrAtTick = clearBeforeOrAtTick;
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new ClearWithinTimeLimitConditionRuntime(
                    ConditionId,
                    DisplayName,
                    _clearBeforeOrAtSeconds,
                    _clearBeforeOrAtTick);
            }
        }

        private sealed class ClearWithinTimeLimitConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly int _clearBeforeOrAtTick;
            private readonly float _clearBeforeOrAtSeconds;
            private readonly string _displayName;
            private bool _isSatisfied;
            private int _lastTickIndex;

            public ClearWithinTimeLimitConditionRuntime(
                string conditionId,
                string displayName,
                float clearBeforeOrAtSeconds,
                int clearBeforeOrAtTick)
            {
                _conditionId = conditionId;
                _displayName = displayName;
                _clearBeforeOrAtSeconds = clearBeforeOrAtSeconds;
                _clearBeforeOrAtTick = clearBeforeOrAtTick;
            }

            public bool IsSatisfied => _isSatisfied;

            public void Reset()
            {
                _isSatisfied = true;
                _lastTickIndex = 0;
            }

            public void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
            {
                if (finalSnapshot == null)
                {
                    throw new ArgumentNullException(nameof(finalSnapshot));
                }

                _lastTickIndex = tickFacts.TickIndex;
                _isSatisfied = tickFacts.TickIndex <= _clearBeforeOrAtTick;
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    nameof(ClearWithinTimeLimitConditionAsset),
                    _isSatisfied,
                    "Seconds=" + _clearBeforeOrAtSeconds.ToString("0.###", CultureInfo.InvariantCulture) +
                    $"|DeadlineTick={_clearBeforeOrAtTick}|TickIndex={_lastTickIndex}");
            }
        }
    }
}
