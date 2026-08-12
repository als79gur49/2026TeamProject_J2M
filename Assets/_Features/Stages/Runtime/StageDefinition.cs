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
    }

    [Serializable]
    public struct StageSpawnDefinition
    {
        public int EntityId;
        public StageSpawnKind Kind;
        public SurfaceCell Cell;
        public Direction Facing;
        public int Hp;
        public UnitMobilityKind UnitMobilityKind;
        public BoxCapabilities BoxCapabilities;
        public BoxArchetype BoxArchetype;
        public EnemyAiMode EnemyAiMode;
        public int EnemyAiStateTimer;
        public EnemyAiProfile EnemyAiProfile;
        public string UnitStackGroup;
    }

    [Serializable]
    public struct StageTileFeatureDefinition
    {
        public int TileId;
        public SurfaceCell Cell;
        public TileFeatureKind Kind;
        public TileFeatureActivationRule ActivationRule;
        public Direction2D Direction;
        public TileFeatureBoxSelector BoxSelector;
        public int BoundEntityId;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Definition", fileName = "StageDefinition")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] private StageBoardDefinition board = new()
        {
            InitialBottomFace = FaceId.Floor,
        };

        [Header("Player Spawns")]
        [SerializeField] private StageSpawnDefinition[] playerSpawns = Array.Empty<StageSpawnDefinition>();

        [Header("Box Spawns")]
        [SerializeField] private StageSpawnDefinition[] boxSpawns = Array.Empty<StageSpawnDefinition>();

        [Header("Enemy Spawns")]
        [SerializeField] private StageSpawnDefinition[] enemySpawns = Array.Empty<StageSpawnDefinition>();

        [Header("Wall Spawns")]
        [SerializeField] private StageSpawnDefinition[] wallSpawns = Array.Empty<StageSpawnDefinition>();

        [Header("Zones")]
        [SerializeField] private StageZoneDefinition[] zones = Array.Empty<StageZoneDefinition>();

        [Header("Tile Features")]
        [SerializeField] private StageTileFeatureDefinition[] tileFeatures = Array.Empty<StageTileFeatureDefinition>();

        [Header("Objective")]
        [SerializeField] private StageObjectiveAuthoring objective = StageObjectiveAuthoring.CreateDefault();

        [Header("Gameplay Companion")]
        [SerializeField] private EnemyUnitArchetypeCatalog enemyUnitArchetypeCatalog;

        public StageBoardDefinition Board => board;

        public StageSpawnDefinition[] PlayerSpawns => playerSpawns ?? Array.Empty<StageSpawnDefinition>();

        public StageSpawnDefinition[] BoxSpawns => boxSpawns ?? Array.Empty<StageSpawnDefinition>();

        public StageSpawnDefinition[] EnemySpawns => enemySpawns ?? Array.Empty<StageSpawnDefinition>();

        public StageSpawnDefinition[] WallSpawns => wallSpawns ?? Array.Empty<StageSpawnDefinition>();

        public StageZoneDefinition[] Zones => zones ?? Array.Empty<StageZoneDefinition>();

        public StageTileFeatureDefinition[] TileFeatures => tileFeatures ?? Array.Empty<StageTileFeatureDefinition>();

        public StageObjectiveAuthoring Objective => NormalizeObjective(objective);

        public EnemyUnitArchetypeCatalog EnemyUnitArchetypeCatalog => enemyUnitArchetypeCatalog;

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

        private static StageObjectiveAuthoring NormalizeObjective(StageObjectiveAuthoring authoring)
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = authoring.CompletionPolicy,
                ConditionEntries = authoring.GetConditionEntriesOrEmpty(),
            };
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
