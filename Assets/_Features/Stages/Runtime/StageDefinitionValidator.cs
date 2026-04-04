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
            var perimeterFaces = NormalizePerimeterFaces(board.PerimeterFaces);
            var openingColumns = NormalizeOpeningColumns(board.SharedEdgeOpeningColumns);
            var spawns = stage.Spawns ?? Array.Empty<StageSpawnDefinition>();

            if (board.GeneratedPerimeterWallEntityIdStart <= 0)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' requires a positive {nameof(StageBoardDefinition.GeneratedPerimeterWallEntityIdStart)}.");
            }

            var generatedWalls = BuildGeneratedWalls(
                boardBounds,
                perimeterFaces,
                openingColumns,
                board.GeneratedPerimeterWallEntityIdStart);

            var playerEntityId = ValidateEntities(stageName, spawns, boardBounds, generatedWalls);

            return new ValidatedStageData(
                boardBounds,
                new CubeTopologyState(board.InitialBottomFace),
                spawns,
                generatedWalls,
                playerEntityId);
        }

        internal static GeneratedWallDefinition[] BuildGeneratedWalls(
            BoardBounds boardBounds,
            IReadOnlyList<FaceId> perimeterFaces,
            HashSet<int> openingColumns,
            int generatedPerimeterWallEntityIdStart)
        {
            var generatedWalls = new List<GeneratedWallDefinition>();
            var nextEntityId = generatedPerimeterWallEntityIdStart;

            for (var i = 0; i < perimeterFaces.Count; i++)
            {
                AddFacePerimeterWalls(
                    generatedWalls,
                    perimeterFaces[i],
                    boardBounds,
                    openingColumns,
                    ref nextEntityId);
            }

            return generatedWalls.ToArray();
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
            IReadOnlyList<StageSpawnDefinition> spawns,
            BoardBounds boardBounds,
            IReadOnlyList<GeneratedWallDefinition> generatedWalls)
        {
            var occupiedCells = new HashSet<SurfaceCell>();
            var generatedWallIds = new HashSet<int>();

            for (var i = 0; i < generatedWalls.Count; i++)
            {
                var generatedWall = generatedWalls[i];
                if (!generatedWallIds.Add(generatedWall.EntityId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' generated duplicate wall entity id {generatedWall.EntityId}.");
                }

                if (!occupiedCells.Add(generatedWall.Cell))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' generated duplicate occupied cell at {generatedWall.Cell} while building perimeter walls.");
                }
            }

            var explicitEntityIds = new HashSet<int>();
            var playerCount = 0;
            var playerEntityId = 0;

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                var spawnLabel = FormatSpawnLabel(spawn, i);

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

                if (generatedWallIds.Contains(spawn.EntityId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' explicit spawn entity id {spawn.EntityId} collides with a generated perimeter wall entity id.");
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

        private static FaceId[] NormalizePerimeterFaces(FaceId[] perimeterFaces)
        {
            return perimeterFaces ?? Array.Empty<FaceId>();
        }

        private static HashSet<int> NormalizeOpeningColumns(int[] openingColumns)
        {
            return openingColumns == null
                ? new HashSet<int>()
                : new HashSet<int>(openingColumns);
        }

        private static void AddFacePerimeterWalls(
            List<GeneratedWallDefinition> generatedWalls,
            FaceId face,
            BoardBounds boardBounds,
            HashSet<int> openingColumns,
            ref int nextEntityId)
        {
            for (var x = boardBounds.MinInclusive.x; x <= boardBounds.MaxInclusive.x; x++)
            {
                AddGeneratedWallIfNeeded(
                    generatedWalls,
                    new SurfaceCell(face, x, boardBounds.MinInclusive.y),
                    boardBounds,
                    openingColumns,
                    ref nextEntityId);
                AddGeneratedWallIfNeeded(
                    generatedWalls,
                    new SurfaceCell(face, x, boardBounds.MaxInclusive.y),
                    boardBounds,
                    openingColumns,
                    ref nextEntityId);
            }

            for (var y = boardBounds.MinInclusive.y + 1; y < boardBounds.MaxInclusive.y; y++)
            {
                AddGeneratedWallIfNeeded(
                    generatedWalls,
                    new SurfaceCell(face, boardBounds.MinInclusive.x, y),
                    boardBounds,
                    openingColumns,
                    ref nextEntityId);
                AddGeneratedWallIfNeeded(
                    generatedWalls,
                    new SurfaceCell(face, boardBounds.MaxInclusive.x, y),
                    boardBounds,
                    openingColumns,
                    ref nextEntityId);
            }
        }

        private static void AddGeneratedWallIfNeeded(
            List<GeneratedWallDefinition> generatedWalls,
            SurfaceCell cell,
            BoardBounds boardBounds,
            HashSet<int> openingColumns,
            ref int nextEntityId)
        {
            if (ShouldSkipSharedEdgeOpening(cell, boardBounds, openingColumns))
            {
                return;
            }

            generatedWalls.Add(new GeneratedWallDefinition(nextEntityId++, cell));
        }

        private static bool ShouldSkipSharedEdgeOpening(
            SurfaceCell cell,
            BoardBounds boardBounds,
            HashSet<int> openingColumns)
        {
            return openingColumns.Contains(cell.x) &&
                   (cell.y == boardBounds.MinInclusive.y || cell.y == boardBounds.MaxInclusive.y);
        }

        private static string FormatSpawnLabel(StageSpawnDefinition spawn, int index)
        {
            return $"spawn[{index}] ({spawn.Kind}, EntityId={spawn.EntityId}, Cell={spawn.Cell})";
        }

        private static string GetStageName(StageDefinition stage)
        {
            return string.IsNullOrWhiteSpace(stage.name) ? "<unnamed stage>" : stage.name;
        }

        internal readonly struct GeneratedWallDefinition
        {
            public GeneratedWallDefinition(int entityId, SurfaceCell cell)
            {
                EntityId = entityId;
                Cell = cell;
            }

            public int EntityId { get; }

            public SurfaceCell Cell { get; }
        }

        internal sealed class ValidatedStageData
        {
            public ValidatedStageData(
                BoardBounds boardBounds,
                CubeTopologyState initialTopology,
                StageSpawnDefinition[] spawns,
                GeneratedWallDefinition[] generatedWalls,
                int playerEntityId)
            {
                BoardBounds = boardBounds;
                InitialTopology = initialTopology;
                Spawns = spawns ?? Array.Empty<StageSpawnDefinition>();
                GeneratedWalls = generatedWalls ?? Array.Empty<GeneratedWallDefinition>();
                PlayerEntityId = playerEntityId;
            }

            public BoardBounds BoardBounds { get; }

            public CubeTopologyState InitialTopology { get; }

            public StageSpawnDefinition[] Spawns { get; }

            public GeneratedWallDefinition[] GeneratedWalls { get; }

            public int PlayerEntityId { get; }
        }
    }
}
