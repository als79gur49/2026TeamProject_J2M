using System;
using System.IO;
using Game.Feature.Stages;
using Game.Product.Achievements;
using Game.Product.Achievements.Composition;
using Game.Product.Achievements.Infrastructure;

namespace Game.Exhibition.Integration
{
    public sealed class ParticipantProgressResetAdapter : IParticipantProgressReset
    {
        private readonly ISavePathProvider paths;
        public ParticipantProgressResetAdapter(ISavePathProvider paths)
            => this.paths = paths ?? throw new ArgumentNullException(nameof(paths));

        public void Reset()
        {
            ParticipantCampaignReset.Clear(paths);
            var root = paths.SaveRootPath;
            const string name = FileProductAchievementRepository.AchievementFileName;
            // Remove all old ledger recovery sources before writing a fresh, verifiable ledger.
            if (Directory.Exists(root))
            {
                File.Delete(Path.Combine(root, name));
                foreach (var file in Directory.GetFiles(root, name + ".*")) File.Delete(file);
            }
            var achievements = new FileProductAchievementRepository(
                new StageAtomicAchievementTextStoreAdapter(new AtomicTextFileStore(root), root));
            achievements.Reset();
            var loaded = achievements.Load();
            if (loaded.Status != AchievementDocumentLoadStatus.Loaded || loaded.Document == null ||
                loaded.Document.EarnedAchievementIds.Length != 0 ||
                loaded.Document.PendingAchievementPublicationIds.Length != 0)
                throw new IOException("Achievement reset could not be verified.");
        }
    }
}
