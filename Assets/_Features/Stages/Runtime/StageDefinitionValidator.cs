using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Stages
{
    public static class StageDefinitionValidator
    {
        public static void Validate(StageDefinition stage)
        {
            ValidateAndNormalize(stage);
        }

        internal static ValidatedStageData ValidateAndNormalize(StageDefinition stage)
        {
            if (stage == null)
            {
                throw new ArgumentNullException(nameof(stage), "StageDefinition cannot be null.");
            }

            var stageName = GetStageName(stage);
            var board = stage.Board;
            var boardBounds = CreateBoardBounds(stageName, board);
            var spawnEntries = NormalizeExplicitSpawns(stage.GetSpawnGroups());
            var playerEntityId = ValidateEntities(stageName, spawnEntries, boardBounds);

            return new ValidatedStageData(
                boardBounds,
                new CubeTopologyState(board.InitialBottomFace),
                ExtractSpawns(spawnEntries),
                playerEntityId);
        }

        private static BoardBounds CreateBoardBounds(string stageName, StageBoardDefinition board)
        {
            if (board.MaxInclusive.x < board.MinInclusive.x ||
                board.MaxInclusive.y < board.MinInclusive.y)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' has invalid board bounds. MinInclusive=({board.MinInclusive.x},{board.MinInclusive.y}) must be less than or equal to MaxInclusive=({board.MaxInclusive.x},{board.MaxInclusive.y}).");
            }

            return new BoardBounds(board.MinInclusive, board.MaxInclusive);
        }

        private static int ValidateEntities(
            string stageName,
            IReadOnlyList<ExplicitSpawnEntry> spawnEntries,
            BoardBounds boardBounds)
        {
            var occupiedCells = new HashSet<SurfaceCell>();
            var explicitEntityIds = new HashSet<int>();
            var playerCount = 0;
            var playerEntityId = 0;

            for (var i = 0; i < spawnEntries.Count; i++)
            {
                var spawnEntry = spawnEntries[i];
                var spawn = spawnEntry.Spawn;
                var spawnLabel = FormatSpawnLabel(spawnEntry);

                if (spawn.Kind != spawnEntry.ExpectedKind)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnEntry.GroupName}[{spawnEntry.GroupIndex}] must use Kind {spawnEntry.ExpectedKind}, but found {spawn.Kind}.");
                }

                if (spawn.EntityId <= 0)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnLabel} must use a positive entity id.");
                }

                if (!explicitEntityIds.Add(spawn.EntityId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' contains duplicate entity id {spawn.EntityId}.");
                }

                if (!boardBounds.Contains(spawn.Cell.PlanarPosition))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnLabel} is outside the configured board bounds at {spawn.Cell}.");
                }

                if (!occupiedCells.Add(spawn.Cell))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' contains duplicate occupied cell {spawn.Cell}.");
                }

                if (spawn.Hp <= 0)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnLabel} must use a positive Hp value.");
                }

                if (spawn.Kind != StageSpawnKind.Player)
                {
                    continue;
                }

                playerCount++;
                playerEntityId = spawn.EntityId;
            }

            if (playerCount == 0)
            {
                throw new InvalidOperationException($"Stage '{stageName}' must contain exactly one player spawn, but found none.");
            }

            if (playerCount > 1)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' must contain exactly one player spawn, but found {playerCount}.");
            }

            return playerEntityId;
        }

        private static ExplicitSpawnEntry[] NormalizeExplicitSpawns(
            IReadOnlyList<StageDefinition.StageSpawnGroup> spawnGroups)
        {
            var entries = new List<ExplicitSpawnEntry>();

            for (var groupIndex = 0; groupIndex < spawnGroups.Count; groupIndex++)
            {
                var spawnGroup = spawnGroups[groupIndex];
                var groupSpawns = spawnGroup.Spawns ?? Array.Empty<StageSpawnDefinition>();

                for (var spawnIndex = 0; spawnIndex < groupSpawns.Length; spawnIndex++)
                {
                    entries.Add(new ExplicitSpawnEntry(
                        spawnGroup.GroupName,
                        spawnGroup.ExpectedKind,
                        spawnIndex,
                        groupSpawns[spawnIndex]));
                }
            }

            if (entries.Count == 0)
            {
                return Array.Empty<ExplicitSpawnEntry>();
            }

            return entries.ToArray();
        }

        private static StageSpawnDefinition[] ExtractSpawns(IReadOnlyList<ExplicitSpawnEntry> spawnEntries)
        {
            if (spawnEntries.Count == 0)
            {
                return Array.Empty<StageSpawnDefinition>();
            }

            var spawns = new StageSpawnDefinition[spawnEntries.Count];
            for (var i = 0; i < spawnEntries.Count; i++)
            {
                spawns[i] = spawnEntries[i].Spawn;
            }

            return spawns;
        }

        private static string FormatSpawnLabel(ExplicitSpawnEntry spawnEntry)
        {
            var spawn = spawnEntry.Spawn;
            return $"{spawnEntry.GroupName}[{spawnEntry.GroupIndex}] ({spawn.Kind}, EntityId={spawn.EntityId}, Cell={spawn.Cell})";
        }

        private static string GetStageName(StageDefinition stage)
        {
            return string.IsNullOrWhiteSpace(stage.name) ? "<unnamed stage>" : stage.name;
        }

        internal readonly struct ExplicitSpawnEntry
        {
            public ExplicitSpawnEntry(
                string groupName,
                StageSpawnKind expectedKind,
                int groupIndex,
                StageSpawnDefinition spawn)
            {
                GroupName = groupName ?? string.Empty;
                ExpectedKind = expectedKind;
                GroupIndex = groupIndex;
                Spawn = spawn;
            }

            public string GroupName { get; }

            public StageSpawnKind ExpectedKind { get; }

            public int GroupIndex { get; }

            public StageSpawnDefinition Spawn { get; }
        }

        internal sealed class ValidatedStageData
        {
            public ValidatedStageData(
                BoardBounds boardBounds,
                CubeTopologyState initialTopology,
                StageSpawnDefinition[] spawns,
                int playerEntityId)
            {
                BoardBounds = boardBounds;
                InitialTopology = initialTopology;
                Spawns = spawns ?? Array.Empty<StageSpawnDefinition>();
                PlayerEntityId = playerEntityId;
            }

            public BoardBounds BoardBounds { get; }

            public CubeTopologyState InitialTopology { get; }

            public StageSpawnDefinition[] Spawns { get; }

            public int PlayerEntityId { get; }
        }
    }
}
