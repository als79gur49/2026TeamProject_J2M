using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum TileFeatureVisualSlotId
    {
        Root = 0,
        Renderer = 1,
        IconRoot = 2,
        LabelRoot = 3,
        CellCenter = 4,
        CellFloor = 5,
        FeatureAnchor0 = 100,
        FeatureAnchor1 = 101,
        FeatureAnchor2 = 102,
        FeatureAnchor3 = 103,
        FeatureAnchor4 = 104,
        FeatureAnchor5 = 105,
        FeatureAnchor6 = 106,
        FeatureAnchor7 = 107,
    }

    [Serializable]
    public struct TileFeatureVisualTargetBinding
    {
        public TileFeatureVisualSlotId SlotId;
        public Transform Transform;
        public Renderer Renderer;
    }

    [DisallowMultipleComponent]
    public sealed class TileFeatureVisualTargetView :
        MonoBehaviour,
        ITileFeatureVisualTarget,
        ITileFeatureVisualTargetConfigurator
    {
        [SerializeField] private int tileId;
        [SerializeField] private SurfaceCell cell;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer primaryRenderer;
        [SerializeField] private Transform iconRoot;
        [SerializeField] private Transform labelRoot;
        [SerializeField] private TileFeatureVisualTargetBinding[] authoredBindings;

        public int TileId => tileId;

        public SurfaceCell Cell => cell;

        public Transform PresentationRoot => presentationRoot != null ? presentationRoot : transform;

        public Transform VisualRoot => visualRoot != null ? visualRoot : PresentationRoot;

        public Renderer PrimaryRenderer => primaryRenderer;

        public Animator DebugAnimator => GetComponentInChildren<Animator>(includeInactive: true);

        public void Configure(int newTileId, SurfaceCell newCell)
        {
            ConfigureTileFeature(newTileId, newCell);
        }

        public void ConfigureTileFeature(int newTileId, SurfaceCell newCell)
        {
            tileId = newTileId;
            cell = newCell;
        }

        internal void ConfigurePresentationRoot(Transform newPresentationRoot)
        {
            presentationRoot = newPresentationRoot != null ? newPresentationRoot : transform;
        }

        public bool TryGetSlot(TileFeatureVisualSlotId slotId, out Transform slotTransform)
        {
            if (TryGetAuthoredBinding(slotId, out var binding) &&
                binding.Transform != null)
            {
                slotTransform = binding.Transform;
                return true;
            }

            slotTransform = ResolveDefaultSlot(slotId);
            return slotTransform != null;
        }

        public bool TryGetRenderer(TileFeatureVisualSlotId slotId, out Renderer renderer)
        {
            if (TryGetAuthoredBinding(slotId, out var binding) &&
                binding.Renderer != null)
            {
                renderer = binding.Renderer;
                return true;
            }

            renderer = slotId == TileFeatureVisualSlotId.Renderer ||
                       slotId == TileFeatureVisualSlotId.Root
                ? primaryRenderer
                : null;
            return renderer != null;
        }

        public TileFeatureVisualBindingDiagnostics ValidateBindings()
        {
            return TileFeatureVisualBindingDiagnostics.ForTarget(this);
        }

        private Transform ResolveDefaultSlot(TileFeatureVisualSlotId slotId)
        {
            switch (slotId)
            {
                case TileFeatureVisualSlotId.Root:
                    return transform;
                case TileFeatureVisualSlotId.Renderer:
                    return primaryRenderer != null ? primaryRenderer.transform : VisualRoot;
                case TileFeatureVisualSlotId.IconRoot:
                    return iconRoot;
                case TileFeatureVisualSlotId.LabelRoot:
                    return labelRoot;
                case TileFeatureVisualSlotId.CellCenter:
                case TileFeatureVisualSlotId.CellFloor:
                    return PresentationRoot;
                default:
                    return null;
            }
        }

        private bool TryGetAuthoredBinding(TileFeatureVisualSlotId slotId, out TileFeatureVisualTargetBinding binding)
        {
            if (authoredBindings != null)
            {
                for (var i = 0; i < authoredBindings.Length; i++)
                {
                    if (authoredBindings[i].SlotId == slotId)
                    {
                        binding = authoredBindings[i];
                        return true;
                    }
                }
            }

            binding = default;
            return false;
        }
    }
}
