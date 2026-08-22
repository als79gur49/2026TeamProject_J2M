using System;
using System.Collections.Generic;

namespace Game.Product.Achievements
{
    public enum AchievementPublicationResult
    {
        // The provider observed its named submission path in this application lifetime.
        // Durable pending confirmation intentionally remains until a fresh lifetime.
        Submitted = 0,

        // The provider observed the target satisfied before attempting any mutation.
        AlreadySatisfied = 1,
        Deferred = 2,
        Unavailable = 3,
        Rejected = 4,
        Failed = 5,
    }

    public interface IAchievementPublicationSink
    {
        void PublishBatch(
            AchievementPublicationBatch batch,
            Action<AchievementPublicationBatchResult> completed);
    }

    public sealed class AchievementPublicationBatch
    {
        private readonly IReadOnlyList<GameAchievementId> _achievementIds;

        public AchievementPublicationBatch(IEnumerable<GameAchievementId> achievementIds)
        {
            if (achievementIds == null)
            {
                throw new ArgumentNullException(nameof(achievementIds));
            }

            var unique = new HashSet<GameAchievementId>();
            var values = new List<GameAchievementId>();
            foreach (var achievementId in achievementIds)
            {
                if (!achievementId.IsValid)
                {
                    throw new ArgumentException(
                        "Every publication batch item must have a valid achievement ID.",
                        nameof(achievementIds));
                }

                if (!unique.Add(achievementId))
                {
                    throw new ArgumentException(
                        "Publication batch achievement IDs must be unique.",
                        nameof(achievementIds));
                }

                values.Add(achievementId);
            }

            if (values.Count == 0)
            {
                throw new ArgumentException(
                    "A publication batch must contain at least one achievement ID.",
                    nameof(achievementIds));
            }

            var sorted = values.ToArray();
            Array.Sort(
                sorted,
                (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
            _achievementIds = Array.AsReadOnly(sorted);
        }

        public IReadOnlyList<GameAchievementId> AchievementIds => _achievementIds;
    }

    public readonly struct AchievementPublicationItemResult
    {
        public AchievementPublicationItemResult(
            GameAchievementId achievementId,
            AchievementPublicationResult result)
        {
            AchievementId = achievementId;
            Result = result;
        }

        public GameAchievementId AchievementId { get; }

        public AchievementPublicationResult Result { get; }
    }

    public sealed class AchievementPublicationBatchResult
    {
        private readonly IReadOnlyList<AchievementPublicationItemResult> _items;

        public AchievementPublicationBatchResult(
            IEnumerable<AchievementPublicationItemResult> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var values = new List<AchievementPublicationItemResult>();
            foreach (var item in items)
            {
                values.Add(item);
            }

            _items = Array.AsReadOnly(values.ToArray());
        }

        public IReadOnlyList<AchievementPublicationItemResult> Items => _items;

        public static AchievementPublicationBatchResult Uniform(
            AchievementPublicationBatch batch,
            AchievementPublicationResult result)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            var items = new AchievementPublicationItemResult[batch.AchievementIds.Count];
            for (var i = 0; i < items.Length; i++)
            {
                items[i] = new AchievementPublicationItemResult(
                    batch.AchievementIds[i],
                    result);
            }

            return new AchievementPublicationBatchResult(items);
        }
    }

    public sealed class UnavailableAchievementPublicationSink : IAchievementPublicationSink
    {
        public void PublishBatch(
            AchievementPublicationBatch batch,
            Action<AchievementPublicationBatchResult> completed)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            completed(AchievementPublicationBatchResult.Uniform(
                batch,
                AchievementPublicationResult.Unavailable));
        }
    }
}
