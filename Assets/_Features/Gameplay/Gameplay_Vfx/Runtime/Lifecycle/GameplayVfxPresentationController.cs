using System;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class GameplayVfxPresentationController
    {
        private readonly IVfxPool pool;
        private readonly IVfxAnchorResolver anchorResolver;
        private readonly VfxPersistentHandleRegistry persistentRegistry;
        private readonly VfxLifetimeRunner lifetimeRunner;

        public GameplayVfxPresentationController(
            IVfxPool pool,
            IVfxAnchorResolver anchorResolver,
            VfxPersistentHandleRegistry persistentRegistry,
            VfxLifetimeRunner lifetimeRunner)
        {
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            this.anchorResolver = anchorResolver ?? throw new ArgumentNullException(nameof(anchorResolver));
            this.persistentRegistry = persistentRegistry ?? throw new ArgumentNullException(nameof(persistentRegistry));
            this.lifetimeRunner = lifetimeRunner ?? throw new ArgumentNullException(nameof(lifetimeRunner));
        }

        public void Refresh(GameplayVfxRequestPlan plan)
        {
            persistentRegistry.ReleaseCompleted();
            persistentRegistry.BeginReconcile();

            if (plan != null)
            {
                foreach (var request in plan.Requests)
                {
                    Process(request);
                }
            }

            persistentRegistry.EndReconcile(lifetimeRunner);
        }

        public void HardCleanupAll()
        {
            persistentRegistry.HardCleanupAll(pool);
            pool.HardCleanupAll();
        }

        private void Process(in GameplayVfxRequest request)
        {
            if (!anchorResolver.TryResolve(request, out var anchor) || !anchor.IsResolved)
            {
                HandleMissingAnchor(request);
                return;
            }

            if (request.IsPersistent)
            {
                persistentRegistry.GetOrStart(request, anchor, pool);
                persistentRegistry.MarkDesired(request.PersistentKey);
                return;
            }

            pool.PlayTransient(request, anchor);
        }

        private static void HandleMissingAnchor(in GameplayVfxRequest request)
        {
            if (request.MissingAnchorPolicy == VfxMissingAnchorPolicy.FailFast)
            {
                throw new InvalidOperationException("Gameplay VFX anchor resolution failed.");
            }
        }
    }
}
