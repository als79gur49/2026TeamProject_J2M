using System;
using System.Collections.Generic;

namespace Game.Product.Achievements
{
    public enum AchievementEarnResult
    {
        EarnedNew = 0,
        AlreadyEarned = 1,
        InvalidAchievement = 2,
        PersistenceFailed = 3,
        UnavailableState = 4,
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

    public sealed class ProductAchievementCoordinator : IDisposable
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
            List<PublicationAttempt> attempts;
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
                attempts = RegisterInitializationReconciliationLocked();
            }

            PublishAll(attempts);
            return true;
        }

        public AchievementEarnResult Earn(GameAchievementId achievementId)
        {
            PublicationAttempt attempt;
            lock (_gate)
            {
                if (_disposed || !_initializeAttempted || !_usable)
                {
                    return AchievementEarnResult.UnavailableState;
                }

                if (!achievementId.IsValid || !_catalog.Contains(achievementId))
                {
                    return AchievementEarnResult.InvalidAchievement;
                }

                if (_earned.Contains(achievementId))
                {
                    return AchievementEarnResult.AlreadyEarned;
                }

                var candidateEarned = new HashSet<GameAchievementId>(_earned) { achievementId };
                var candidatePending = new HashSet<GameAchievementId>(_pending) { achievementId };
                var candidate = CreateDocument(candidateEarned, candidatePending);
                if (!TrySave(candidate))
                {
                    return AchievementEarnResult.PersistenceFailed;
                }

                _earned = candidateEarned;
                _pending = candidatePending;
                attempt = RegisterPublicationLocked(achievementId);
            }

            if (attempt != null)
            {
                Publish(attempt);
            }

            return AchievementEarnResult.EarnedNew;
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

        private List<PublicationAttempt> RegisterInitializationReconciliationLocked()
        {
            var attempts = new List<PublicationAttempt>();
            var definitions = _catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var achievementId = definitions[i].Id;
                if (!_earned.Contains(achievementId) || !_sessionReconciledIds.Add(achievementId))
                {
                    continue;
                }

                var attempt = RegisterPublicationLocked(achievementId);
                if (attempt != null)
                {
                    attempts.Add(attempt);
                }
            }

            return attempts;
        }

        private PublicationAttempt RegisterPublicationLocked(GameAchievementId achievementId)
        {
            if (_disposed || !_usable || _inFlight.ContainsKey(achievementId))
            {
                return null;
            }

            var attempt = new PublicationAttempt(++_nextAttemptId, achievementId);
            _inFlight.Add(achievementId, attempt);
            return attempt;
        }

        private void PublishAll(List<PublicationAttempt> attempts)
        {
            for (var i = 0; i < attempts.Count; i++)
            {
                Publish(attempts[i]);
            }
        }

        private void Publish(PublicationAttempt attempt)
        {
            try
            {
                _publicationSink.Publish(
                    attempt.AchievementId,
                    result => CompletePublication(attempt, result));
            }
            catch
            {
                CompletePublication(attempt, AchievementPublicationResult.Failed);
            }
        }

        private void CompletePublication(
            PublicationAttempt attempt,
            AchievementPublicationResult result)
        {
            lock (_gate)
            {
                if (_disposed ||
                    attempt.Completed ||
                    !_inFlight.TryGetValue(attempt.AchievementId, out var current) ||
                    !ReferenceEquals(current, attempt) ||
                    current.AttemptId != attempt.AttemptId)
                {
                    return;
                }

                attempt.Completed = true;
                _inFlight.Remove(attempt.AchievementId);

                if (result != AchievementPublicationResult.Accepted &&
                    result != AchievementPublicationResult.AlreadySatisfied)
                {
                    return;
                }

                if (!_pending.Contains(attempt.AchievementId))
                {
                    return;
                }

                var candidatePending = new HashSet<GameAchievementId>(_pending);
                candidatePending.Remove(attempt.AchievementId);
                var candidate = CreateDocument(_earned, candidatePending);
                if (!TrySave(candidate))
                {
                    return;
                }

                _pending = candidatePending;
            }
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
            public PublicationAttempt(long attemptId, GameAchievementId achievementId)
            {
                AttemptId = attemptId;
                AchievementId = achievementId;
            }

            public long AttemptId { get; }

            public GameAchievementId AchievementId { get; }

            public bool Completed { get; set; }
        }
    }
}
