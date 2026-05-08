using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum StageAuthoringEntityKind
    {
        Player = 0,
        Enemy = 1,
        Box = 2,
        Wall = 3,
    }

    [Serializable]
    public sealed class StagePlacedEntityAuthoring
    {
        public string StableGuid = string.Empty;
        public string DisplayName = string.Empty;
        public StageAuthoringEntityKind Kind;
        public SurfaceCell Cell;
        public Direction Facing = Direction.None;
        public int Hp = 1;
        public string UnitStackGroup = string.Empty;
        public BoxCapabilities BoxCapabilities = BoxCapabilities.None;
        public BoxArchetype BoxArchetype = BoxArchetype.Normal;
        public EnemyAiMode EnemyAiMode = EnemyAiMode.None;
        public int EnemyAiStateTimer;
        public EnemyAiProfile EnemyAiProfileOverride;
        public string PresentationId = string.Empty;

        public StagePlacedEntityAuthoring Clone()
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = StableGuid ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                Kind = Kind,
                Cell = Cell,
                Facing = Facing,
                Hp = Hp,
                UnitStackGroup = UnitStackGroup ?? string.Empty,
                BoxCapabilities = BoxCapabilities,
                BoxArchetype = BoxArchetype,
                EnemyAiMode = EnemyAiMode,
                EnemyAiStateTimer = EnemyAiStateTimer,
                EnemyAiProfileOverride = EnemyAiProfileOverride,
                PresentationId = PresentationId ?? string.Empty,
            };
        }
    }

    [Serializable]
    public struct StageAuthoringIdMapping
    {
        public string StableGuid;
        public int EntityId;
        public bool Retired;
        public string LastKnownDisplayName;
    }

    public sealed class StageAuthoringGenerateOptions
    {
        public static StageAuthoringGenerateOptions DryRunValidation => new()
        {
            DryRun = true,
            WriteGameplay = false,
            WritePresentationBindings = false,
            ValidateAfterGenerate = true,
        };

        public static StageAuthoringGenerateOptions WriteAll => new()
        {
            DryRun = false,
            WriteGameplay = true,
            WritePresentationBindings = true,
            ValidateAfterGenerate = true,
        };

        public bool DryRun { get; set; }

        public bool WriteGameplay { get; set; } = true;

        public bool WritePresentationBindings { get; set; } = true;

        public bool ValidateAfterGenerate { get; set; } = true;
    }
}
