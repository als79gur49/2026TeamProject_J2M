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

        public int DebugPlayButtonActivatedCount => ResolveDebugAdapter()?.DebugPlayButtonActivatedCount ?? 0;

        public int DebugPlayDestroyTileTriggeredCount => ResolveDebugAdapter()?.DebugPlayDestroyTileTriggeredCount ?? 0;

        public int DebugPlayDestroyTileActivatedCount => ResolveDebugAdapter()?.DebugPlayDestroyTileActivatedCount ?? 0;

        public int DebugPlayDestroyTileDeactivatedCount => ResolveDebugAdapter()?.DebugPlayDestroyTileDeactivatedCount ?? 0;

        public bool DebugDestroyTileActive => ResolveDebugAdapter()?.DebugDestroyTileActive ?? false;

        public bool DebugSlideTileActive => ResolveDebugAdapter()?.DebugSlideTileActive ?? false;

        public int DebugPlaySlideTileRedirectedCount => ResolveDebugAdapter()?.DebugPlaySlideTileRedirectedCount ?? 0;

        public int DebugPlayBarricadeBlockedCount => ResolveDebugAdapter()?.DebugPlayBarricadeBlockedCount ?? 0;

        public int DebugPlayBarricadeCrushedCount => ResolveDebugAdapter()?.DebugPlayBarricadeCrushedCount ?? 0;

        public int DebugPlayBarricadeActivatedCount => ResolveDebugAdapter()?.DebugPlayBarricadeActivatedCount ?? 0;

        public int DebugPlayBarricadeDeactivatedCount => ResolveDebugAdapter()?.DebugPlayBarricadeDeactivatedCount ?? 0;

        internal int DebugBarricadeActiveImmediateStatePlayCount =>
            ResolveDebugAdapter()?.DebugBarricadeActiveImmediateStatePlayCount ?? 0;

        public int DebugPlayExitOpenedCount => ResolveDebugAdapter()?.DebugPlayExitOpenedCount ?? 0;

        public Animator DebugAnimator => ResolveDebugAdapter()?.Animator;

        public int DebugPlayExitEnteredCount => ResolveDebugAdapter()?.DebugPlayExitEnteredCount ?? 0;

        public bool DebugExitOpen => ResolveDebugAdapter()?.DebugExitOpen ?? false;

        public int DebugPlayMoonBlockGeneratedCount => ResolveDebugAdapter()?.DebugPlayMoonBlockGeneratedCount ?? 0;

        public int DebugPlayMoonBlockGeneratorBlockedCount =>
            ResolveDebugAdapter()?.DebugPlayMoonBlockGeneratorBlockedCount ?? 0;

        public int DebugMoonBlockGeneratorBlockedUnitCount =>
            ResolveDebugAdapter()?.DebugMoonBlockGeneratorBlockedUnitCount ?? 0;

        public int DebugMoonBlockGeneratorBlockedWallLikeSolidCount =>
            ResolveDebugAdapter()?.DebugMoonBlockGeneratorBlockedWallLikeSolidCount ?? 0;

        public int DebugMoonBlockGeneratorBlockedPlacementCount =>
            ResolveDebugAdapter()?.DebugMoonBlockGeneratorBlockedPlacementCount ?? 0;

        public Direction DebugLastSlideTileDirection =>
            ResolveDebugAdapter()?.DebugLastSlideTileDirection ?? Direction.None;

        public Direction DebugLastBarricadeBlockedDirection =>
            ResolveDebugAdapter()?.DebugLastBarricadeBlockedDirection ?? Direction.None;

        public int DebugLastSlideTileTargetEntityId => ResolveDebugAdapter()?.DebugLastSlideTileTargetEntityId ?? 0;

        public int DebugLastBarricadeBlockedTargetEntityId =>
            ResolveDebugAdapter()?.DebugLastBarricadeBlockedTargetEntityId ?? 0;

        public int DebugLastBarricadeCrushedTargetEntityId =>
            ResolveDebugAdapter()?.DebugLastBarricadeCrushedTargetEntityId ?? 0;

        public int DebugLastExitEnteredPlayerEntityId =>
            ResolveDebugAdapter()?.DebugLastExitEnteredPlayerEntityId ?? 0;

        public int DebugLastMoonBlockGeneratedEntityId =>
            ResolveDebugAdapter()?.DebugLastMoonBlockGeneratedEntityId ?? 0;

        public MoonBlockGeneratorBlockedPayload DebugLastMoonBlockGeneratorBlockedPayload =>
            ResolveDebugAdapter()?.DebugLastMoonBlockGeneratorBlockedPayload ?? default;

        public MoonBlockGeneratorBlockedReason DebugLastMoonBlockGeneratorBlockedReason =>
            DebugLastMoonBlockGeneratorBlockedPayload.Reason;

        public int DebugLastMoonBlockGeneratorBlockedEntityId =>
            DebugLastMoonBlockGeneratorBlockedPayload.BlockingEntityId;

        public void Configure(int newTileId, SurfaceCell newCell)
        {
            ConfigureTileFeature(newTileId, newCell);
        }

        public void ConfigureTileFeature(int newTileId, SurfaceCell newCell)
        {
            tileId = newTileId;
            cell = newCell;
            ResolveDebugAdapter()?.ResetBarricadeActiveImmediateState();
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

        private LegacyTileFeatureVisualCueAdapter ResolveDebugAdapter()
        {
            return GetComponent<LegacyTileFeatureVisualCueAdapter>();
        }
    }
}
