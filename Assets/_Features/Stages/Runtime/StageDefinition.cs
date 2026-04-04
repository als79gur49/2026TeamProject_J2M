using System;
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

        [SerializeField] private StageSpawnDefinition[] spawns = Array.Empty<StageSpawnDefinition>();

        public StageBoardDefinition Board => board;

        public StageSpawnDefinition[] Spawns => spawns ?? Array.Empty<StageSpawnDefinition>();
    }
}
