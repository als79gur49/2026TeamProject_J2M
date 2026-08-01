using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    internal sealed class SceneTransitionOverlayContentResolver
    {
        public SceneTransitionOverlayContentView Resolve(
            SceneTransitionOverlayModel model,
            SceneTransitionOverlayContentCatalog catalog)
        {
            var transitionMatch = FindByTransitionKind(catalog, model.TransitionKind);
            if (transitionMatch != null)
            {
                return transitionMatch;
            }

            return null;
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

    }
}
