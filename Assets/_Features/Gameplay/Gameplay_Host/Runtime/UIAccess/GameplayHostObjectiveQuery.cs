using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostObjectiveQuery : IGameplayObjectiveQuery
    {
        private static readonly IReadOnlyList<GameplayObjectiveConditionReadModel> EmptyConditions =
            Array.Empty<GameplayObjectiveConditionReadModel>();

        private readonly TickRunner _tickRunner;
        private readonly GameplayPresentationBarrierTracker _barrierTracker;
        private StageObjectiveRuntimeDefinition _lastObjectiveDefinition;
        private StageObjectiveTickResult _lastObjectiveResult;
        private int _lastBarrierVersion = -1;
        private GameplayObjectiveReadModel _lastReadModel = GameplayObjectiveReadModel.NoObjective;

        public GameplayHostObjectiveQuery(
            TickRunner tickRunner,
            GameplayPresentationBarrierTracker barrierTracker = null)
        {
            _tickRunner = tickRunner;
            _barrierTracker = barrierTracker;
        }

        public GameplayObjectiveReadModel Read()
        {
            var objectiveResult = _tickRunner?.CurrentObjectiveResult;
            var objectiveDefinition = _tickRunner?.ObjectiveDefinition;
            if (objectiveResult == null ||
                objectiveDefinition == null ||
                !objectiveResult.HasObjective ||
                !objectiveDefinition.HasObjective)
            {
                Cache(objectiveDefinition, objectiveResult, GameplayObjectiveReadModel.NoObjective);
                return _lastReadModel;
            }

            var barrierVersion = _barrierTracker?.Version ?? 0;
            if (ReferenceEquals(_lastObjectiveDefinition, objectiveDefinition) &&
                ReferenceEquals(_lastObjectiveResult, objectiveResult) &&
                _lastBarrierVersion == barrierVersion)
            {
                return _lastReadModel;
            }

            var displayMetadata = objectiveDefinition.DisplayMetadata ?? StageObjectiveDisplayMetadata.Empty;
            var visibleGoalReached = objectiveResult.GoalReached &&
                                     !HasPendingPrimaryGoalCompletionGate(objectiveResult, _barrierTracker);
            var visibleAllConditionsSatisfied = objectiveResult.AllConditionsSatisfied &&
                                                !HasPendingRequiredCompletionGate(objectiveResult, _barrierTracker);
            var visibleIsCleared = objectiveResult.IsCleared &&
                                   !HasPendingRequiredCompletionGate(objectiveResult, _barrierTracker);
            Cache(
                objectiveDefinition,
                objectiveResult,
                new GameplayObjectiveReadModel(
                    objectiveResult.HasObjective,
                    visibleGoalReached,
                    visibleAllConditionsSatisfied,
                    visibleIsCleared,
                    displayMetadata.ObjectiveTitle,
                    displayMetadata.ObjectiveSummary,
                    BuildConditionRows(objectiveResult, displayMetadata, _barrierTracker),
                    objectiveResult.GoalReached,
                    objectiveResult.AllConditionsSatisfied,
                    objectiveResult.IsCleared));
            return _lastReadModel;
        }

        private void Cache(
            StageObjectiveRuntimeDefinition objectiveDefinition,
            StageObjectiveTickResult objectiveResult,
            GameplayObjectiveReadModel readModel)
        {
            _lastObjectiveDefinition = objectiveDefinition;
            _lastObjectiveResult = objectiveResult;
            _lastBarrierVersion = _barrierTracker?.Version ?? 0;
            _lastReadModel = readModel;
        }

        private static IReadOnlyList<GameplayObjectiveConditionReadModel> BuildConditionRows(
            StageObjectiveTickResult objectiveResult,
            StageObjectiveDisplayMetadata displayMetadata,
            GameplayPresentationBarrierTracker barrierTracker)
        {
            var statuses = objectiveResult.ConditionStatuses;
            var metadataEntries = displayMetadata.ConditionEntries;
            if (statuses.Count == 0 || metadataEntries.Count == 0)
            {
                return EmptyConditions;
            }

            var statusesByStableId = new Dictionary<string, StageConditionStatus>(StringComparer.Ordinal);
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (!string.IsNullOrWhiteSpace(status.ConditionId))
                {
                    statusesByStableId[status.ConditionId] = status;
                }
            }

            if (statusesByStableId.Count == 0)
            {
                return EmptyConditions;
            }

            var sortedMetadata = new List<StageObjectiveConditionDisplayMetadata>(metadataEntries.Count);
            for (var i = 0; i < metadataEntries.Count; i++)
            {
                sortedMetadata.Add(metadataEntries[i]);
            }

            sortedMetadata.Sort(CompareConditionDisplayMetadata);

            var rows = new List<GameplayObjectiveConditionReadModel>(sortedMetadata.Count);
            for (var i = 0; i < sortedMetadata.Count; i++)
            {
                var metadata = sortedMetadata[i];
                if (string.IsNullOrWhiteSpace(metadata.DisplayText) ||
                    string.IsNullOrWhiteSpace(metadata.StableConditionId) ||
                    !statusesByStableId.TryGetValue(metadata.StableConditionId, out var status))
                {
                    continue;
                }

                var isSatisfied = status.IsSatisfied &&
                                  !IsVisibleCompletionGated(status, barrierTracker);
                rows.Add(new GameplayObjectiveConditionReadModel(
                    status.ConditionId,
                    MapRole(status.Role),
                    status.Required,
                    isSatisfied,
                    metadata.DisplayText,
                    string.Empty,
                    metadata.SortOrder));
            }

            return rows.Count == 0 ? EmptyConditions : rows.ToArray();
        }

        private static int CompareConditionDisplayMetadata(
            StageObjectiveConditionDisplayMetadata left,
            StageObjectiveConditionDisplayMetadata right)
        {
            var sortOrderComparison = left.SortOrder.CompareTo(right.SortOrder);
            return sortOrderComparison != 0
                ? sortOrderComparison
                : left.AuthoringOrder.CompareTo(right.AuthoringOrder);
        }

        private static GameplayObjectiveConditionRole MapRole(StageObjectiveConditionRole role)
        {
            switch (role)
            {
                case StageObjectiveConditionRole.PrimaryGoal:
                    return GameplayObjectiveConditionRole.PrimaryGoal;

                case StageObjectiveConditionRole.SecondaryGoal:
                    return GameplayObjectiveConditionRole.SecondaryGoal;

                case StageObjectiveConditionRole.Challenge:
                    return GameplayObjectiveConditionRole.Challenge;

                case StageObjectiveConditionRole.None:
                default:
                    return GameplayObjectiveConditionRole.None;
            }
        }

        private static bool IsVisibleCompletionGated(
            StageConditionStatus status,
            GameplayPresentationBarrierTracker barrierTracker)
        {
            if (barrierTracker == null ||
                !status.IsSatisfied ||
                !string.Equals(status.ConditionType, nameof(ButtonActivatedConditionAsset), StringComparison.Ordinal))
            {
                return false;
            }

            return TryParseButtonTileId(status.Details, out var tileId) &&
                   barrierTracker.IsButtonActivationPending(tileId);
        }

        private static bool HasPendingPrimaryGoalCompletionGate(
            StageObjectiveTickResult objectiveResult,
            GameplayPresentationBarrierTracker barrierTracker)
        {
            return HasPendingCompletionGate(
                objectiveResult,
                barrierTracker,
                status => status.Role == StageObjectiveConditionRole.PrimaryGoal);
        }

        private static bool HasPendingRequiredCompletionGate(
            StageObjectiveTickResult objectiveResult,
            GameplayPresentationBarrierTracker barrierTracker)
        {
            return HasPendingCompletionGate(
                objectiveResult,
                barrierTracker,
                status => status.Required);
        }

        private static bool HasPendingCompletionGate(
            StageObjectiveTickResult objectiveResult,
            GameplayPresentationBarrierTracker barrierTracker,
            Func<StageConditionStatus, bool> predicate)
        {
            if (objectiveResult == null || barrierTracker == null || predicate == null)
            {
                return false;
            }

            var statuses = objectiveResult.ConditionStatuses;
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (predicate(status) && IsVisibleCompletionGated(status, barrierTracker))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseButtonTileId(string details, out int tileId)
        {
            tileId = 0;
            if (string.IsNullOrWhiteSpace(details))
            {
                return false;
            }

            var parts = details.Split('|');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                const string prefix = "TileId=";
                if (!part.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                return int.TryParse(part.Substring(prefix.Length), out tileId);
            }

            return false;
        }
    }
}
