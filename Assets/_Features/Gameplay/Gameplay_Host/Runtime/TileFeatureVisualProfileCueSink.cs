using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class TileFeatureVisualProfileCueSink : MonoBehaviour, ITileFeatureVisualCueSink
    {
        private readonly ButtonTileFeatureVisualHandler buttonHandler = new();
        private readonly DestroyTileFeatureVisualHandler destroyHandler = new();
        private readonly SlideTileFeatureVisualHandler slideHandler = new();
        private readonly BarricadeTileFeatureVisualHandler barricadeHandler = new();
        private readonly ExitTileFeatureVisualHandler exitHandler = new();
        private readonly MoonBlockGeneratorTileFeatureVisualHandler moonGeneratorHandler = new();
        private readonly GenericTileFeatureVfxHandler genericHandler = new();

        private TileFeatureVisualTargetView targetView;
        private TileFeatureVisualProfileProvider profileProvider;
        private Animator animator;
        private IGameplayVfxPlaybackPort gameplayVfxPlaybackPort;

        public void Configure(
            TileFeatureVisualTargetView target,
            TileFeatureVisualProfileProvider provider)
        {
            var sameBinding = ReferenceEquals(targetView, target) &&
                              ReferenceEquals(profileProvider, provider);
            targetView = target;
            profileProvider = provider;
            RefreshCachedReferences(target);

            if (!sameBinding)
            {
                ResetHandlerState();
            }
        }

        private void RefreshCachedReferences(TileFeatureVisualTargetView target)
        {
            animator = target != null
                ? target.GetComponentInChildren<Animator>(includeInactive: true)
                : GetComponentInChildren<Animator>(includeInactive: true);
        }

        public void AttachGameplayVfxPlaybackPort(IGameplayVfxPlaybackPort playbackPort)
        {
            gameplayVfxPlaybackPort = playbackPort;
        }

        public bool TryHandle(in TileFeatureVisualRequest request)
        {
            var target = ResolveTarget();
            var provider = ResolveProfileProvider(target);
            if (target == null ||
                provider == null ||
                !provider.TryGetProfile(request.FeatureKind, out var profile))
            {
                return false;
            }

            var handler = ResolveHandler(request.FeatureKind);
            return handler.TryHandle(request, target, profile, ResolveAnimator(target), gameplayVfxPlaybackPort);
        }

        private TileFeatureVisualTargetView ResolveTarget()
        {
            if (targetView == null)
            {
                targetView = GetComponent<TileFeatureVisualTargetView>();
            }

            return targetView;
        }

        private TileFeatureVisualProfileProvider ResolveProfileProvider(TileFeatureVisualTargetView target)
        {
            if (profileProvider != null)
            {
                return profileProvider;
            }

            profileProvider = TileFeatureVisualCueSinkResolver.ResolveProfileProvider(this, target);
            return profileProvider;
        }

        private Animator ResolveAnimator(TileFeatureVisualTargetView target)
        {
            if (animator == null)
            {
                animator = target != null
                    ? target.GetComponentInChildren<Animator>(includeInactive: true)
                    : GetComponentInChildren<Animator>(includeInactive: true);
            }

            return animator;
        }

        private ITileFeatureVisualHandler ResolveHandler(TileFeatureKind featureKind)
        {
            switch (featureKind)
            {
                case TileFeatureKind.Button:
                    return buttonHandler;
                case TileFeatureKind.Destroy:
                    return destroyHandler;
                case TileFeatureKind.Slide:
                    return slideHandler;
                case TileFeatureKind.Barricade:
                    return barricadeHandler;
                case TileFeatureKind.Exit:
                case TileFeatureKind.Entrance:
                    return exitHandler;
                case TileFeatureKind.MoonBlockGenerator:
                    return moonGeneratorHandler;
                default:
                    return genericHandler;
            }
        }

        private void ResetHandlerState()
        {
            barricadeHandler.ResetActiveStateCache();
            exitHandler.ResetOpenStateCache();
        }
    }

    internal static class TileFeatureVisualCueSinkResolver
    {
        private static readonly ITileFeatureVisualCueSink NoProfileNoOpSink = new NoProfileTileFeatureVisualCueSink();

        public static ITileFeatureVisualCueSink Resolve(
            ITileFeatureVisualTarget target,
            TileFeatureKind featureKind)
        {
            if (target is ITileFeatureVisualCueSink targetSink)
            {
                return targetSink;
            }

            if (target is Component component)
            {
                var componentSink = component.GetComponent<ITileFeatureVisualCueSink>();
                if (componentSink != null &&
                    componentSink is not TileFeatureVisualProfileCueSink)
                {
                    return componentSink;
                }

                var targetView = target as TileFeatureVisualTargetView;
                var provider = ResolveProfileProvider(component, targetView);
                if (provider != null)
                {
                    return ConfigureProfileSink(component.gameObject, targetView, provider);
                }

                if (componentSink != null)
                {
                    return componentSink;
                }

                if (IsNoProfileNoOpPolicyTarget(component, featureKind))
                {
                    return NoProfileNoOpSink;
                }
            }

            return null;
        }

        public static TileFeatureVisualProfileProvider ResolveProfileProvider(
            Component component,
            TileFeatureVisualTargetView target)
        {
            var provider = component != null
                ? component.GetComponent<TileFeatureVisualProfileProvider>()
                : null;
            if (provider != null)
            {
                return provider;
            }

            if (target == null)
            {
                return null;
            }

            provider = target.GetComponent<TileFeatureVisualProfileProvider>();
            if (provider != null)
            {
                return provider;
            }

            provider = target.GetComponentInParent<TileFeatureVisualProfileProvider>(true);
            if (provider != null)
            {
                return provider;
            }

            return target.GetComponentInChildren<TileFeatureVisualProfileProvider>(true);
        }

        private static TileFeatureVisualProfileCueSink ConfigureProfileSink(
            GameObject gameObject,
            TileFeatureVisualTargetView target,
            TileFeatureVisualProfileProvider provider)
        {
            var sink = gameObject.GetComponent<TileFeatureVisualProfileCueSink>() ??
                       gameObject.AddComponent<TileFeatureVisualProfileCueSink>();
            sink.Configure(target, provider);
            return sink;
        }

        private static bool IsNoProfileNoOpPolicyTarget(
            Component component,
            TileFeatureKind featureKind)
        {
            if (component.GetComponentInChildren<Animator>(includeInactive: true) != null)
            {
                return false;
            }

            return featureKind == TileFeatureKind.Button ||
                   featureKind == TileFeatureKind.Entrance ||
                   featureKind == TileFeatureKind.Exit;
        }

        private sealed class NoProfileTileFeatureVisualCueSink : ITileFeatureVisualCueSink
        {
            public bool TryHandle(in TileFeatureVisualRequest request)
            {
                return request.FeatureKind == TileFeatureKind.Button ||
                       request.FeatureKind == TileFeatureKind.Entrance ||
                       request.FeatureKind == TileFeatureKind.Exit;
            }
        }
    }
}
