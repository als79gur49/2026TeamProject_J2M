using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    public static class StagePresentationBindingNormalizer
    {
        public static EnemyPresentationBinding[] NormalizeEnemyBindings(
            IReadOnlyList<EnemyPresentationBinding> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<EnemyPresentationBinding>();
            }

            var bindings = new EnemyPresentationBinding[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                bindings[i] = source[i];
            }

            Array.Sort(bindings, EnemyPresentationBindingEntityIdComparer.Instance);
            return bindings;
        }

        public static StaticEntityPresentationBinding[] NormalizeStaticEntityBindings(
            IReadOnlyList<StaticEntityPresentationBinding> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<StaticEntityPresentationBinding>();
            }

            var bindings = new StaticEntityPresentationBinding[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                bindings[i] = source[i];
            }

            Array.Sort(bindings, StaticEntityPresentationBindingEntityIdComparer.Instance);
            return bindings;
        }

        public static TileFeaturePresentationBinding[] CloneTileFeatureBindingsPreserveOrder(
            IReadOnlyList<TileFeaturePresentationBinding> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TileFeaturePresentationBinding>();
            }

            var bindings = new TileFeaturePresentationBinding[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var binding = source[i];
                bindings[i] = binding == null
                    ? null
                    : new TileFeaturePresentationBinding
                    {
                        TileId = binding.TileId,
                        VisualPrefab = binding.VisualPrefab,
                    };
            }

            return bindings;
        }

        private sealed class EnemyPresentationBindingEntityIdComparer : IComparer<EnemyPresentationBinding>
        {
            public static readonly EnemyPresentationBindingEntityIdComparer Instance = new();

            public int Compare(EnemyPresentationBinding left, EnemyPresentationBinding right)
            {
                return left.EntityId.CompareTo(right.EntityId);
            }
        }

        private sealed class StaticEntityPresentationBindingEntityIdComparer :
            IComparer<StaticEntityPresentationBinding>
        {
            public static readonly StaticEntityPresentationBindingEntityIdComparer Instance = new();

            public int Compare(StaticEntityPresentationBinding left, StaticEntityPresentationBinding right)
            {
                return left.EntityId.CompareTo(right.EntityId);
            }
        }
    }
}
