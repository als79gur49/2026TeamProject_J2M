using System;
using Game.Feature.Stages;
using Game.Product.Achievements.Infrastructure;

namespace Game.Product.Achievements.Composition
{
    internal interface IProductAchievementHostLifetime : IDisposable
    {
        IProductAchievementEarningSink EarningSink { get; }

        bool Initialize();
    }

    public sealed class ProductAchievementApplicationHost : IProductAchievementHostLifetime
    {
        public ProductAchievementApplicationHost(ProductAchievementCoordinator coordinator)
        {
            Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public ProductAchievementCoordinator Coordinator { get; }

        public IProductAchievementEarningSink EarningSink => Coordinator;

        public static ProductAchievementApplicationHost CreateForSaveRoot(
            string savesDirectoryPath,
            IAchievementPublicationSink publicationSink = null)
        {
            var stageStore = new AtomicTextFileStore(savesDirectoryPath);
            var achievementStore = new StageAtomicAchievementTextStoreAdapter(
                stageStore,
                savesDirectoryPath);
            var repository = new FileProductAchievementRepository(achievementStore);
            var coordinator = new ProductAchievementCoordinator(
                repository,
                GameAchievementCatalog.Production,
                publicationSink ?? new UnavailableAchievementPublicationSink());
            return new ProductAchievementApplicationHost(coordinator);
        }

        public bool Initialize()
        {
            return Coordinator.Initialize();
        }

        public void Dispose()
        {
            Coordinator.Dispose();
        }
    }
}
