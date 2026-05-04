using System;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public sealed class GameplayVfxHostEntityAnchorProjector : IGameplayVfxEntityAnchorProjector
    {
        private readonly Game.Feature.Gameplay.Host.GameplayPresentationStateStore stateStore;
        private readonly Game.Feature.Gameplay.Host.GameplayEntityViewRegistry viewRegistry;

        public GameplayVfxHostEntityAnchorProjector(
            Game.Feature.Gameplay.Host.GameplayPresentationStateStore stateStore,
            Game.Feature.Gameplay.Host.GameplayEntityViewRegistry viewRegistry = null)
        {
            this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            this.viewRegistry = viewRegistry;
        }

        public bool TryResolveEntity(
            int entityId,
            VfxAnchorSlot slot,
            out VfxResolvedAnchor resolvedAnchor)
        {
            if (entityId <= 0 || slot != VfxAnchorSlot.EntityCenter)
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }

            if (stateStore.ViewsByEntityId.ContainsKey(entityId) ||
                (viewRegistry != null && viewRegistry.TryGetView(entityId, out _)) ||
                stateStore.PresentedLocalPosesByEntityId.ContainsKey(entityId) ||
                stateStore.RetainedLocalTargetPoses.ContainsKey(entityId) ||
                stateStore.CommittedLocalTargetPoses.ContainsKey(entityId))
            {
                resolvedAnchor = VfxResolvedAnchor.ForEntity(entityId, slot, default, default);
                return true;
            }

            resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
            return false;
        }
    }
}
