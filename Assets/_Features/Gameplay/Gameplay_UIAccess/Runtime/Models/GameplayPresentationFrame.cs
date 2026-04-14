using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.UIAccess.Presentation;

namespace Game.Feature.Gameplay.UIAccess.Models
{
    public readonly struct GameplayPresentationFrame
    {
        public GameplayPresentationFrame(
            int tickIndex,
            CubeTopologyState finalTopology,
            GameplayTopologyPresentationSlice? topology = null,
            GameplayPlayerPresentationSlice? player = null,
            GameplayStageEventPresentationSlice? stageEvent = null)
        {
            if (tickIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickIndex), "Presentation frames require a positive tick index.");
            }

            TickIndex = tickIndex;
            FinalTopology = finalTopology;
            Topology = topology;
            Player = player;
            StageEvent = stageEvent;
        }

        public int TickIndex { get; }

        public CubeTopologyState FinalTopology { get; }

        public GameplayTopologyPresentationSlice? Topology { get; }

        public GameplayPlayerPresentationSlice? Player { get; }

        public GameplayStageEventPresentationSlice? StageEvent { get; }

        public bool HasAnySlice => Topology.HasValue || Player.HasValue || StageEvent.HasValue;
    }
}
