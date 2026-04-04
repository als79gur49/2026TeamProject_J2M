using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum StageSpawnKind
    {
        Player = 0,
        Enemy = 1,
        Box = 2,
        Wall = 3,
    }

    [Serializable]
    public struct StageBoardDefinition
    {
        public Vector2Int MinInclusive;
        public Vector2Int MaxInclusive;
        public FaceId InitialBottomFace;
        public FaceId[] PerimeterFaces;
        public int[] SharedEdgeOpeningColumns;
        public int GeneratedPerimeterWallEntityIdStart;
    }

    [Serializable]
    public struct StageSpawnDefinition
    {
        public int EntityId;
        public StageSpawnKind Kind;
        public SurfaceCell Cell;
        public Direction Facing;
        public int Hp;
        public BoxCapabilities BoxCapabilities;
        public EnemyAiMode EnemyAiMode;
        public int EnemyAiStateTimer;
        public EnemyAiProfile EnemyAiProfile;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Definition", fileName = "StageDefinition")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] private StageBoardDefinition board = new()
        {
            InitialBottomFace = FaceId.Floor,
            PerimeterFaces = new[] { FaceId.Floor, FaceId.Front, FaceId.Ceiling, FaceId.Back },
            SharedEdgeOpeningColumns = Array.Empty<int>(),
            GeneratedPerimeterWallEntityIdStart = 100,
        };

        [Header("Player Spawns")]
        [SerializeField] private StageSpawnDefinition[] playerSpawns = Array.Empty<StageSpawnDefinition>();

        [Header("Box Spawns")]
        [SerializeField] private StageSpawnDefinition[] boxSpawns = Array.Empty<StageSpawnDefinition>();

        [Header("Enemy Spawns")]
        [SerializeField] private StageSpawnDefinition[] enemySpawns = Array.Empty<StageSpawnDefinition>();

        [Header("Wall Spawns")]
        [SerializeField] private StageSpawnDefinition[] wallSpawns = Array.Empty<StageSpawnDefinition>();

        public StageBoardDefinition Board => board;

        public StageSpawnDefinition[] PlayerSpawns => playerSpawns ?? Array.Empty<StageSpawnDefinition>();

        public StageSpawnDefinition[] BoxSpawns => boxSpawns ?? Array.Empty<StageSpawnDefinition>();

        public StageSpawnDefinition[] EnemySpawns => enemySpawns ?? Array.Empty<StageSpawnDefinition>();

        public StageSpawnDefinition[] WallSpawns => wallSpawns ?? Array.Empty<StageSpawnDefinition>();

        public StageSpawnDefinition[] Spawns => FlattenSpawnGroups();

        internal StageSpawnGroup[] GetSpawnGroups()
        {
            return new[]
            {
                new StageSpawnGroup(nameof(playerSpawns), StageSpawnKind.Player, PlayerSpawns),
                new StageSpawnGroup(nameof(boxSpawns), StageSpawnKind.Box, BoxSpawns),
                new StageSpawnGroup(nameof(enemySpawns), StageSpawnKind.Enemy, EnemySpawns),
                new StageSpawnGroup(nameof(wallSpawns), StageSpawnKind.Wall, WallSpawns),
            };
        }

        private StageSpawnDefinition[] FlattenSpawnGroups()
        {
            var groups = GetSpawnGroups();
            var flattened = new List<StageSpawnDefinition>();

            for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                var groupSpawns = groups[groupIndex].Spawns;
                for (var spawnIndex = 0; spawnIndex < groupSpawns.Length; spawnIndex++)
                {
                    flattened.Add(groupSpawns[spawnIndex]);
                }
            }

            return flattened.ToArray();
        }

        internal readonly struct StageSpawnGroup
        {
            public StageSpawnGroup(string groupName, StageSpawnKind expectedKind, StageSpawnDefinition[] spawns)
            {
                GroupName = groupName;
                ExpectedKind = expectedKind;
                Spawns = spawns ?? Array.Empty<StageSpawnDefinition>();
            }

            public string GroupName { get; }

            public StageSpawnKind ExpectedKind { get; }

            public StageSpawnDefinition[] Spawns { get; }
        }
    }
}
