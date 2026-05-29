using System;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public readonly struct GameplayVfxCloneSource
    {
        public GameplayVfxCloneSource(Transform modelRoot)
        {
            ModelRoot = modelRoot != null ? modelRoot : throw new ArgumentNullException(nameof(modelRoot));
            LocalScale = modelRoot.localScale;
        }

        public Transform ModelRoot { get; }

        public Vector3 LocalScale { get; }

        internal VfxRendererInactiveVisualSnapshotSet CaptureInactiveVisualSnapshot()
        {
            return VfxRendererInactiveVisualSnapshotSet.CaptureFromModelRoot(ModelRoot);
        }
    }

    public interface IGameplayVfxCloneSourceProvider
    {
        bool TryResolveCloneSource(
            int sourceEntityId,
            out GameplayVfxCloneSource source);
    }

    public sealed class GameplayVfxStateStoreCloneSourceProvider : IGameplayVfxCloneSourceProvider
    {
        private readonly GameplayPresentationStateStore stateStore;

        public GameplayVfxStateStoreCloneSourceProvider(GameplayPresentationStateStore stateStore)
        {
            this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public bool TryResolveCloneSource(
            int sourceEntityId,
            out GameplayVfxCloneSource source)
        {
            source = default;
            if (sourceEntityId <= 0 ||
                !stateStore.ViewsByEntityId.TryGetValue(sourceEntityId, out var view) ||
                view == null ||
                view.ModelRoot == null ||
                view.ModelRoot.childCount <= 0)
            {
                return false;
            }

            source = new GameplayVfxCloneSource(view.ModelRoot);
            return true;
        }
    }
}
