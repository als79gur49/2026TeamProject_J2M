using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [CreateAssetMenu(
        fileName = "TileFeatureVisualProfile",
        menuName = "Gameplay/Tile Feature Visual Profile")]
    public sealed class TileFeatureVisualProfile : ScriptableObject
    {
        [SerializeField] private TileFeatureKind featureKind;
        [SerializeField] private TileFeatureVisualCueBinding[] cueBindings;
        [SerializeField] private TileFeatureInactiveMaterialTarget[] inactiveMaterialTargets;

        public TileFeatureKind FeatureKind => featureKind;

        public IReadOnlyList<TileFeatureVisualCueBinding> CueBindings =>
            cueBindings ?? Array.Empty<TileFeatureVisualCueBinding>();

        public IReadOnlyList<TileFeatureInactiveMaterialTarget> InactiveMaterialTargets =>
            inactiveMaterialTargets ?? Array.Empty<TileFeatureInactiveMaterialTarget>();

        public bool TryGetCueBinding(TileFeatureVisualCueId cueId, out TileFeatureVisualCueBinding binding)
        {
            var bindings = CueBindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].CueId == cueId)
                {
                    binding = bindings[i];
                    return true;
                }
            }

            binding = default;
            return false;
        }
    }
}
