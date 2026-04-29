using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostObjectiveQuery : IGameplayObjectiveQuery
    {
        private static readonly IReadOnlyList<GameplayObjectiveConditionReadModel> EmptyConditions =
            Array.Empty<GameplayObjectiveConditionReadModel>();

        private readonly TickRunner _tickRunner;
        private StageObjectiveRuntimeDefinition _lastObjectiveDefinition;
        private StageObjectiveTickResult _lastObjectiveResult;
        private GameplayObjectiveReadModel _lastReadModel = GameplayObjectiveReadModel.NoObjective;

        public GameplayHostObjectiveQuery(TickRunner tickRunner)
        {
            _tickRunner = tickRunner;
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

            if (ReferenceEquals(_lastObjectiveDefinition, objectiveDefinition) &&
                ReferenceEquals(_lastObjectiveResult, objectiveResult))
            {
                return _lastReadModel;
            }

            var displayMetadata = objectiveDefinition.DisplayMetadata ?? StageObjectiveDisplayMetadata.Empty;
            Cache(
                objectiveDefinition,
                objectiveResult,
                new GameplayObjectiveReadModel(
                    objectiveResult.HasObjective,
                    objectiveResult.GoalReached,
                    objectiveResult.AllConditionsSatisfied,
                    objectiveResult.IsCleared,
                    displayMetadata.ObjectiveTitle,
                    displayMetadata.ObjectiveSummary,
                    BuildConditionRows(objectiveResult, displayMetadata)));
            return _lastReadModel;
        }

        private void Cache(
            StageObjectiveRuntimeDefinition objectiveDefinition,
            StageObjectiveTickResult objectiveResult,
            GameplayObjectiveReadModel readModel)
        {
            _lastObjectiveDefinition = objectiveDefinition;
            _lastObjectiveResult = objectiveResult;
            _lastReadModel = readModel;
        }

        private static IReadOnlyList<GameplayObjectiveConditionReadModel> BuildConditionRows(
            StageObjectiveTickResult objectiveResult,
            StageObjectiveDisplayMetadata displayMetadata)
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

                rows.Add(new GameplayObjectiveConditionReadModel(
                    status.ConditionId,
                    MapRole(status.Role),
                    status.Required,
                    status.IsSatisfied,
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
    }
}
