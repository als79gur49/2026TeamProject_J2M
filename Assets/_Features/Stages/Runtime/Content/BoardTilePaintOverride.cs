using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class BoardTilePaintOverride
    {
        [SerializeField] private SurfaceCell cell;
        [SerializeField] private string styleKey = string.Empty;

        public BoardTilePaintOverride()
        {
        }

        public BoardTilePaintOverride(SurfaceCell cell, string styleKey)
        {
            this.cell = cell;
            this.styleKey = styleKey ?? string.Empty;
        }

        public SurfaceCell Cell => cell;

        public string StyleKey => BoardTileStyleCatalog.NormalizeStyleKey(styleKey);
    }
}
