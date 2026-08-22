using System;
using System.Collections.Generic;

namespace Game.Product.Achievements
{
    public interface IProductAchievementEarningSink
    {
        AchievementEarnResult Earn(GameAchievementId achievementId);

        AchievementEarnBatchResult EarnBatch(
            IReadOnlyList<GameAchievementId> achievementIds);
    }

    public enum AchievementEarnResult
    {
        EarnedNew = 0,
        AlreadyEarned = 1,
        InvalidAchievement = 2,
        PersistenceFailed = 3,
        UnavailableState = 4,
    }

    public readonly struct AchievementEarnBatchResult
    {
        public AchievementEarnBatchResult(
            AchievementEarnResult result,
            GameAchievementId[] newlyEarnedAchievementIds)
        {
            Result = result;
            var values = newlyEarnedAchievementIds == null
                ? Array.Empty<GameAchievementId>()
                : (GameAchievementId[])newlyEarnedAchievementIds.Clone();
            NewlyEarnedAchievementIds = Array.AsReadOnly(values);
        }

        public AchievementEarnResult Result { get; }

        public IReadOnlyList<GameAchievementId> NewlyEarnedAchievementIds { get; }
    }

    public readonly struct ProductAchievementSnapshot
    {
        public ProductAchievementSnapshot(
            bool isUsable,
            GameAchievementId[] earnedAchievementIds,
            GameAchievementId[] pendingAchievementPublicationIds,
            int inFlightCount)
        {
            IsUsable = isUsable;
            EarnedAchievementIds = earnedAchievementIds ?? Array.Empty<GameAchievementId>();
            PendingAchievementPublicationIds =
                pendingAchievementPublicationIds ?? Array.Empty<GameAchievementId>();
            InFlightCount = inFlightCount;
        }

        public bool IsUsable { get; }

        public IReadOnlyList<GameAchievementId> EarnedAchievementIds { get; }

        public IReadOnlyList<GameAchievementId> PendingAchievementPublicationIds { get; }

        public int InFlightCount { get; }
    }

    public sealed class ProductAchievementCoordinator : IDisposable, IProductAchievementEarningSink
    {
        private readonly object _gate = new();
        private readonly IAchievementDocumentRepository _repository;
        private readonly GameAchievementCatalog _catalog;
        private readonly IAchievementPublicationSink _publicationSink;
        private readonly Dictionary<GameAchievementId, PublicationAttempt> _inFlight = new();
        private readonly HashSet<GameAchievementId> _sessionReconciledIds = new();

        private HashSet<GameAchievementId> _earned = new();
        private HashSet<GameAchievementId> _pending = new();
        private AchievementDocumentLoadResult _loadResult;
        private bool _initializeAttempted;
        private bool _usable;
        private bool _disposed;
        private long _nextAttemptId;

        public ProductAchievementCoordinator(
            IAchievementDocumentRepository repository,
            GameAchievementCatalog catalog,
            IAchievementPublicationSink publicationSink)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _publicationSink = publicationSink ?? throw new ArgumentNullException(nameof(publicationSink));
        }

        public AchievementDocumentLoadResult LoadResult
        {
            get
            {
                lock (_gate)
                {
                    return _loadResult;
                }
            }
        }

        public bool Initialize()
        {
            PublicationAttempt attempt;
            lock (_gate)
            {
                if (_disposed)
                {
                    return false;
                }

                if (_initializeAttempted)
                {
                    return _usable;
                }

                _initializeAttempted = true;
                try
                {
                    _loadResult = _repository.Load();
                }
                catch (Exception exception)
                {
                    _loadResult = new AchievementDocumentLoadResult(
                        AchievementDocumentLoadStatus.IoFailed,
                        null,
                        exception.Message);
                }

                if (!_loadResult.IsUsable ||
                    ProductAchievementDocumentNormalizer.TryNormalize(
                        _loadResult.Document,
                        out var normalized) != AchievementDocumentValidationStatus.Valid)
                {
                    _usable = false;
                    return false;
                }

                _earned = ParseIds(normalized.EarnedAchievementIds);
                _pending = ParseIds(normalized.PendingAchievementPublicationIds);
                _usable = true;
                attempt = RegisterInitializationReconciliationLocked();
            }

            Publish(attempt);
            return true;
        }

        public AchievementEarnResult Earn(GameAchievementId achievementId)
        {
            return EarnBatch(new[] { achievementId }).Result;
        }

        public AchievementEarnBatchResult EarnBatch(
            IReadOnlyList<GameAchievementId> achievementIds)
        {
            PublicationAttempt attempt = null;
            lock (_gate)
            {
                if (_disposed || !_initializeAttempted || !_usable)
                {
                    return BatchResult(AchievementEarnResult.UnavailableState);
                }

                if (achievementIds == null || achievementIds.Count == 0)
                {
                    return BatchResult(AchievementEarnResult.InvalidAchievement);
                }

                var requested = new HashSet<GameAchievementId>();
                for (var i = 0; i < achievementIds.Count; i++)
                {
                    var achievementId = achievementIds[i];
                    if (!achievementId.IsValid ||
                        !_catalog.Contains(achievementId) ||
                        !requested.Add(achievementId))
                    {
                        return BatchResult(AchievementEarnResult.InvalidAchievement);
                    }
                }

                var newlyEarned = new List<GameAchievementId>();
                foreach (var achievementId in requested)
                {
                    if (!_earned.Contains(achievementId))
                    {
                        newlyEarned.Add(achievementId);
                    }
                }

                if (newlyEarned.Count == 0)
                {
                    return BatchResult(AchievementEarnResult.AlreadyEarned);
                }

                newlyEarned.Sort(
                    (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
                var candidateEarned = new HashSet<GameAchievementId>(_earned);
                var candidatePending = new HashSet<GameAchievementId>(_pending);
                for (var i = 0; i < newlyEarned.Count; i++)
                {
                    candidateEarned.Add(newlyEarned[i]);
                    candidatePending.Add(newlyEarned[i]);
                }

                var candidate = CreateDocument(candidateEarned, candidatePending);
                if (!TrySave(candidate))
                {
                    return BatchResult(AchievementEarnResult.PersistenceFailed);
                }

                _earned = candidateEarned;
                _pending = candidatePending;
                attempt = RegisterPublicationLocked(newlyEarned);
            }

            if (attempt != null)
            {
                Publish(attempt);
            }

            return new AchievementEarnBatchResult(
                AchievementEarnResult.EarnedNew,
                attempt?.AchievementIds ?? Array.Empty<GameAchievementId>());
        }

        public bool ReconcileAllEarnedForNewPublicationSession()
        {
            PublicationAttempt attempt;
            lock (_gate)
            {
                if (_disposed || !_initializeAttempted || !_usable)
                {
                    return false;
                }

                attempt = RegisterAllKnownEarnedLocked();
            }

            Publish(attempt);
            return true;
        }

        public ProductAchievementSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new ProductAchievementSnapshot(
                    _usable,
                    ToSortedArray(_earned),
                    ToSortedArray(_pending),
                    _inFlight.Count);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                foreach (var attempt in _inFlight.Values)
                {
                    attempt.Completed = true;
                }

                _inFlight.Clear();
            }
        }

        private PublicationAttempt RegisterInitializationReconciliationLocked()
        {
            var achievementIds = new List<GameAchievementId>();
            var definitions = _catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var achievementId = definitions[i].Id;
                if (!_earned.Contains(achievementId) || !_sessionReconciledIds.Add(achievementId))
                {
                    continue;
                }

                achievementIds.Add(achievementId);
            }

            return RegisterPublicationLocked(achievementIds);
        }

        private PublicationAttempt RegisterAllKnownEarnedLocked()
        {
            var achievementIds = new List<GameAchievementId>();
            var definitions = _catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var achievementId = definitions[i].Id;
                if (!_earned.Contains(achievementId))
                {
                    continue;
                }

                if (!_inFlight.ContainsKey(achievementId))
                {
                    achievementIds.Add(achievementId);
                }
            }

            return RegisterPublicationLocked(achievementIds);
        }

        private PublicationAttempt RegisterPublicationLocked(
            IReadOnlyList<GameAchievementId> achievementIds)
        {
            if (_disposed || !_usable || achievementIds == null || achievementIds.Count == 0)
            {
                return null;
            }

            var values = new List<GameAchievementId>();
            for (var i = 0; i < achievementIds.Count; i++)
            {
                if (!_inFlight.ContainsKey(achievementIds[i]))
                {
                    values.Add(achievementIds[i]);
                }
            }

            if (values.Count == 0)
            {
                return null;
            }

            values.Sort(
                (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
            var attempt = new PublicationAttempt(++_nextAttemptId, values.ToArray());
            for (var i = 0; i < attempt.AchievementIds.Length; i++)
            {
                _inFlight.Add(attempt.AchievementIds[i], attempt);
            }

            return attempt;
        }

        private void Publish(PublicationAttempt attempt)
        {
            if (attempt == null)
            {
                return;
            }

            try
            {
                _publicationSink.PublishBatch(
                    new AchievementPublicationBatch(attempt.AchievementIds),
                    result => CompletePublication(attempt, result));
            }
            catch
            {
                CompletePublication(
                    attempt,
                    AchievementPublicationBatchResult.Uniform(
                        new AchievementPublicationBatch(attempt.AchievementIds),
                        AchievementPublicationResult.Failed));
            }
        }

        private void CompletePublication(
            PublicationAttempt attempt,
            AchievementPublicationBatchResult result)
        {
            lock (_gate)
            {
                if (_disposed || attempt.Completed || !IsCurrentAttemptLocked(attempt))
                {
                    return;
                }

                attempt.Completed = true;
                for (var i = 0; i < attempt.AchievementIds.Length; i++)
                {
                    _inFlight.Remove(attempt.AchievementIds[i]);
                }

                if (result == null)
                {
                    return;
                }

                var expected = new HashSet<GameAchievementId>(attempt.AchievementIds);
                var observed = new HashSet<GameAchievementId>();
                var alreadySatisfied = new HashSet<GameAchievementId>();
                for (var i = 0; i < result.Items.Count; i++)
                {
                    var item = result.Items[i];
                    if (!expected.Contains(item.AchievementId) ||
                        !observed.Add(item.AchievementId) ||
                        item.Result != AchievementPublicationResult.AlreadySatisfied ||
                        !_pending.Contains(item.AchievementId))
                    {
                        continue;
                    }

                    alreadySatisfied.Add(item.AchievementId);
                }

                if (alreadySatisfied.Count == 0)
                {
                    return;
                }

                var candidatePending = new HashSet<GameAchievementId>(_pending);
                candidatePending.ExceptWith(alreadySatisfied);
                var candidate = CreateDocument(_earned, candidatePending);
                if (!TrySave(candidate))
                {
                    return;
                }

                _pending = candidatePending;
            }
        }

        private bool IsCurrentAttemptLocked(PublicationAttempt attempt)
        {
            for (var i = 0; i < attempt.AchievementIds.Length; i++)
            {
                if (!_inFlight.TryGetValue(attempt.AchievementIds[i], out var current) ||
                    !ReferenceEquals(current, attempt) ||
                    current.AttemptId != attempt.AttemptId)
                {
                    return false;
                }
            }

            return true;
        }

        private static AchievementEarnBatchResult BatchResult(AchievementEarnResult result)
        {
            return new AchievementEarnBatchResult(result, Array.Empty<GameAchievementId>());
        }

        private bool TrySave(ProductAchievementDocument candidate)
        {
            try
            {
                return _repository.Save(candidate).IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        private static ProductAchievementDocument CreateDocument(
            HashSet<GameAchievementId> earned,
            HashSet<GameAchievementId> pending)
        {
            return new ProductAchievementDocument
            {
                SchemaVersion = ProductAchievementDocument.CurrentSchemaVersion,
                EarnedAchievementIds = ToSortedTokens(earned),
                PendingAchievementPublicationIds = ToSortedTokens(pending),
            };
        }

        private static HashSet<GameAchievementId> ParseIds(string[] values)
        {
            var parsed = new HashSet<GameAchievementId>();
            for (var i = 0; i < values.Length; i++)
            {
                if (GameAchievementId.TryCreate(values[i], out var achievementId))
                {
                    parsed.Add(achievementId);
                }
            }

            return parsed;
        }

        private static string[] ToSortedTokens(HashSet<GameAchievementId> values)
        {
            var result = new string[values.Count];
            var index = 0;
            foreach (var value in values)
            {
                result[index++] = value.Value;
            }

            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static GameAchievementId[] ToSortedArray(HashSet<GameAchievementId> values)
        {
            var result = new GameAchievementId[values.Count];
            values.CopyTo(result);
            Array.Sort(
                result,
                (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
            return result;
        }

        private sealed class PublicationAttempt
        {
            public PublicationAttempt(long attemptId, GameAchievementId[] achievementIds)
            {
                AttemptId = attemptId;
                AchievementIds = achievementIds ?? Array.Empty<GameAchievementId>();
            }

            public long AttemptId { get; }

            public GameAchievementId[] AchievementIds { get; }

            public bool Completed { get; set; }
        }
    }
}
