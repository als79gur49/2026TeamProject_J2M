using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Conditions/Visit Zone Sequence",
        fileName = "VisitZoneSequenceCondition")]
    public sealed class VisitZoneSequenceConditionAsset : StageConditionAsset
    {
        [SerializeField] private string[] zoneIds = Array.Empty<string>();

        public string[] ZoneIds => zoneIds ?? Array.Empty<string>();

        public override void Validate(in StageConditionValidationContext context)
        {
            if (ZoneIds.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' requires at least one zone id.");
            }

            for (var i = 0; i < ZoneIds.Length; i++)
            {
                var zoneId = ZoneIds[i];
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{context.StageName}' condition '{name}' contains an empty zone id at index {i}.");
                }

                if (!context.TryGetZone(zoneId, out _))
                {
                    throw new InvalidOperationException(
                        $"Stage '{context.StageName}' condition '{name}' references unknown zone id '{zoneId.Trim()}'.");
                }
            }
        }

        internal override StageConditionRuntimeDefinition Compile(in StageConditionCompilationContext context)
        {
            var orderedZones = new StageZoneRuntimeDefinition[ZoneIds.Length];
            for (var i = 0; i < ZoneIds.Length; i++)
            {
                if (!context.TryGetZone(ZoneIds[i], out orderedZones[i]))
                {
                    throw new InvalidOperationException(
                        $"Stage '{context.StageName}' condition '{name}' references unknown zone id '{ZoneIds[i]}'.");
                }
            }

            return new VisitZoneSequenceConditionRuntimeDefinition(
                CreateConditionId(this),
                string.IsNullOrWhiteSpace(name) ? nameof(VisitZoneSequenceConditionAsset) : name,
                context.PlayerEntityId,
                orderedZones);
        }

        private sealed class VisitZoneSequenceConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            private readonly int _playerEntityId;
            private readonly StageZoneRuntimeDefinition[] _orderedZones;

            public VisitZoneSequenceConditionRuntimeDefinition(
                string conditionId,
                string displayName,
                int playerEntityId,
                StageZoneRuntimeDefinition[] orderedZones)
                : base(conditionId, displayName)
            {
                _playerEntityId = playerEntityId;
                _orderedZones = orderedZones ?? Array.Empty<StageZoneRuntimeDefinition>();
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new VisitZoneSequenceConditionRuntime(
                    ConditionId,
                    DisplayName,
                    _playerEntityId,
                    _orderedZones);
            }
        }

        private sealed class VisitZoneSequenceConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly string _displayName;
            private readonly StageZoneRuntimeDefinition[] _orderedZones;
            private readonly int _playerEntityId;
            private int _nextZoneIndex;
            private bool _playerWasAlreadyOnExpectedZone;

            public VisitZoneSequenceConditionRuntime(
                string conditionId,
                string displayName,
                int playerEntityId,
                StageZoneRuntimeDefinition[] orderedZones)
            {
                _conditionId = conditionId;
                _displayName = displayName;
                _playerEntityId = playerEntityId;
                _orderedZones = orderedZones ?? Array.Empty<StageZoneRuntimeDefinition>();
            }

            public bool IsSatisfied => _nextZoneIndex >= _orderedZones.Length;

            public void Reset()
            {
                _nextZoneIndex = 0;
                _playerWasAlreadyOnExpectedZone = false;
            }

            public void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
            {
                if (finalSnapshot == null)
                {
                    throw new ArgumentNullException(nameof(finalSnapshot));
                }

                if (IsSatisfied)
                {
                    return;
                }

                if (!TryGetLivePlayerCell(finalSnapshot, out var playerCell))
                {
                    _playerWasAlreadyOnExpectedZone = false;
                    return;
                }

                var isPlayerOnExpectedZone = _orderedZones[_nextZoneIndex].Contains(playerCell);
                if (isPlayerOnExpectedZone && !_playerWasAlreadyOnExpectedZone)
                {
                    _nextZoneIndex++;
                    _playerWasAlreadyOnExpectedZone = !IsSatisfied && _orderedZones[_nextZoneIndex].Contains(playerCell);
                    return;
                }

                _playerWasAlreadyOnExpectedZone = isPlayerOnExpectedZone;
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    nameof(VisitZoneSequenceConditionAsset),
                    IsSatisfied,
                    $"Visited={Math.Min(_nextZoneIndex, _orderedZones.Length)}/{_orderedZones.Length}");
            }

            private bool TryGetLivePlayerCell(WorldSnapshot finalSnapshot, out SurfaceCell playerCell)
            {
                if (finalSnapshot.TryGetEntity(_playerEntityId, out var player) &&
                    player.hp > 0 &&
                    !player.markedForDeath)
                {
                    playerCell = player.position;
                    return true;
                }

                playerCell = default;
                return false;
            }
        }
    }
}
