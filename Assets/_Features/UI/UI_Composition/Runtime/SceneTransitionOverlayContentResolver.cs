using System;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    internal sealed class SceneTransitionOverlayContentResolver
    {
        public SceneTransitionOverlayContentView Resolve(
            SceneTransitionOverlayViewModel model,
            SceneTransitionOverlayContentCatalog catalog,
            Func<StageTransitionKind, SceneTransitionOverlayContentView> transitionKindFallback = null,
            Func<TransitionOverlayKind, SceneTransitionOverlayContentView> overlayKindFallback = null,
            Func<SceneTransitionOverlayContentView> genericFallback = null)
        {
            var transitionMatch = FindByTransitionKind(catalog, model.TransitionKind);
            if (transitionMatch != null)
            {
                return transitionMatch;
            }

            var transitionFallback = transitionKindFallback?.Invoke(model.TransitionKind);
            if (transitionFallback != null)
            {
                return transitionFallback;
            }

            var overlayMatch = FindByOverlayKind(catalog, model.OverlayKind);
            if (overlayMatch != null)
            {
                return overlayMatch;
            }

            var overlayFallback = overlayKindFallback?.Invoke(model.OverlayKind);
            if (overlayFallback != null)
            {
                return overlayFallback;
            }

            return catalog != null && catalog.GenericFallbackPrefab != null
                ? catalog.GenericFallbackPrefab
                : genericFallback?.Invoke();
        }

        private static SceneTransitionOverlayContentView FindByTransitionKind(
            SceneTransitionOverlayContentCatalog catalog,
            StageTransitionKind transitionKind)
        {
            if (catalog == null || transitionKind == StageTransitionKind.Unknown)
            {
                return null;
            }

            foreach (var entry in catalog.Entries)
            {
                if (entry == null ||
                    entry.ContentPrefab == null ||
                    entry.TransitionKind != transitionKind)
                {
                    continue;
                }

                return entry.ContentPrefab;
            }

            return null;
        }

        private static SceneTransitionOverlayContentView FindByOverlayKind(
            SceneTransitionOverlayContentCatalog catalog,
            TransitionOverlayKind overlayKind)
        {
            if (catalog == null || overlayKind == TransitionOverlayKind.None)
            {
                return null;
            }

            foreach (var entry in catalog.Entries)
            {
                if (entry == null ||
                    entry.ContentPrefab == null ||
                    entry.FallbackOverlayKind != overlayKind)
                {
                    continue;
                }

                return entry.ContentPrefab;
            }

            return null;
        }
    }
}
