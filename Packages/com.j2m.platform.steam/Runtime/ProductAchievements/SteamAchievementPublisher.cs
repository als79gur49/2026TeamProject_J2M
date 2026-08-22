using System;
using System.Collections.Generic;
using Game.Product.Achievements;

namespace Game.Platform.Steam.ProductAchievements
{
    internal readonly struct SteamAchievementSessionPrerequisites
    {
        internal SteamAchievementSessionPrerequisites(
            bool initializationSucceeded,
            uint observedAppId,
            bool steamIdValid,
            bool loggedOn)
        {
            InitializationSucceeded = initializationSucceeded;
            ObservedAppId = observedAppId;
            SteamIdValid = steamIdValid;
            LoggedOn = loggedOn;
        }

        internal bool InitializationSucceeded { get; }
        internal uint ObservedAppId { get; }
        internal bool SteamIdValid { get; }
        internal bool LoggedOn { get; }
        internal bool IsReady =>
            InitializationSucceeded && ObservedAppId != 0 && SteamIdValid && LoggedOn;
    }

    internal sealed class SteamAchievementPublisher :
        IAchievementPublicationSink,
        IDisposable
    {
        internal const double CallbackTimeoutSeconds = 30d;

        private readonly object _gate = new();
        private readonly ISteamNativeApi _lifecycleApi;
        private readonly ISteamAchievementApi _achievementApi;
        private readonly SteamAchievementMapping _mapping;
        private readonly Func<double> _monotonicSeconds;
        private readonly Queue<PublicationBatchOperation> _pendingOperations = new();

        private SteamAchievementSessionPrerequisites _prerequisites;
        private HashSet<string> _sessionSchema;
        private PublicationBatchOperation _operation;
        private PublicationSessionState _state;
        private bool _beginAttempted;
        private bool _callbacksRegistered;
        private int _statsStoredObservationCount;
        private SteamCallbackResult? _lastStatsStoredResult;

        internal SteamAchievementPublisher(
            ISteamNativeApi lifecycleApi,
            ISteamAchievementApi achievementApi,
            SteamAchievementMapping mapping,
            Func<double> monotonicSeconds)
        {
            _lifecycleApi = lifecycleApi ??
                throw new ArgumentNullException(nameof(lifecycleApi));
            _achievementApi = achievementApi ??
                throw new ArgumentNullException(nameof(achievementApi));
            _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            _monotonicSeconds = monotonicSeconds ??
                throw new ArgumentNullException(nameof(monotonicSeconds));
        }

        internal int StatsStoredObservationCount
        {
            get
            {
                lock (_gate)
                {
                    return _statsStoredObservationCount;
                }
            }
        }

        internal SteamCallbackResult? LastStatsStoredResult
        {
            get
            {
                lock (_gate)
                {
                    return _lastStatsStoredResult;
                }
            }
        }

        internal bool BeginSession(SteamAchievementSessionPrerequisites prerequisites)
        {
            lock (_gate)
            {
                if (_state == PublicationSessionState.Disposed)
                {
                    return false;
                }

                if (_beginAttempted)
                {
                    return _state == PublicationSessionState.Active;
                }

                _beginAttempted = true;
                _prerequisites = prerequisites;
                if (!prerequisites.IsReady)
                {
                    return false;
                }
            }

            try
            {
                _achievementApi.RegisterAchievementStoreCallbacks(
                    ObserveStatsStored,
                    ObserveAchievementStored);
            }
            catch
            {
                return false;
            }

            lock (_gate)
            {
                if (_state == PublicationSessionState.Disposed)
                {
                    TryDisposeCallbacks();
                    return false;
                }

                _callbacksRegistered = true;
                _state = PublicationSessionState.Active;
                return true;
            }
        }

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

            var unavailable = false;
            lock (_gate)
            {
                unavailable = _state != PublicationSessionState.Active ||
                              !_callbacksRegistered ||
                              !_prerequisites.IsReady;
            }

            if (unavailable)
            {
                InvokeCompletion(
                    completed,
                    AchievementPublicationBatchResult.Uniform(
                        batch,
                        AchievementPublicationResult.Unavailable));
                return;
            }

            PublicationBatchOperation operation = null;
            lock (_gate)
            {
                if (_state != PublicationSessionState.Active || !_callbacksRegistered)
                {
                    unavailable = true;
                }
                else
                {
                    operation = new PublicationBatchOperation(
                        batch,
                        _prerequisites.ObservedAppId,
                        completed);
                    if (_operation != null)
                    {
                        _pendingOperations.Enqueue(operation);
                        operation = null;
                    }
                    else
                    {
                        _operation = operation;
                    }
                }
            }

            if (unavailable)
            {
                InvokeCompletion(
                    completed,
                    AchievementPublicationBatchResult.Uniform(
                        batch,
                        AchievementPublicationResult.Unavailable));
                return;
            }

            if (operation != null)
            {
                BeginMutation(operation);
            }
        }

        internal void Tick()
        {
            PublicationBatchOperation timedOut = null;
            lock (_gate)
            {
                if (_state != PublicationSessionState.Active ||
                    _operation == null ||
                    !_operation.CallbackWaitStarted ||
                    _monotonicSeconds() - _operation.CallbackWaitStartedAt <
                    CallbackTimeoutSeconds)
                {
                    return;
                }

                timedOut = _operation;
            }

            AbortPublicationSession(timedOut, AchievementPublicationResult.Failed);
        }

        public void Dispose()
        {
            var completions = new List<BatchCompletion>();
            var disposeCallbacks = false;
            lock (_gate)
            {
                if (_state == PublicationSessionState.Disposed)
                {
                    return;
                }

                _state = PublicationSessionState.Disposed;
                _sessionSchema = null;
                if (_operation != null)
                {
                    completions.Add(CompleteUniformLocked(
                        _operation,
                        AchievementPublicationResult.Unavailable));
                    _operation = null;
                }

                while (_pendingOperations.Count > 0)
                {
                    completions.Add(CompleteUniformLocked(
                        _pendingOperations.Dequeue(),
                        AchievementPublicationResult.Unavailable));
                }

                disposeCallbacks = _callbacksRegistered;
                _callbacksRegistered = false;
            }

            if (disposeCallbacks)
            {
                TryDisposeCallbacks();
            }

            InvokeCompletions(completions);
        }

        private void BeginMutation(PublicationBatchOperation operation)
        {
            try
            {
                lock (_gate)
                {
                    if (!IsCurrentLocked(operation))
                    {
                        return;
                    }
                }

                if (!TryObserveReadyPublicationSession(
                        out var currentAppId,
                        out var readinessThrew) ||
                    currentAppId != operation.AppId)
                {
                    AbortPublicationSession(
                        operation,
                        readinessThrew
                            ? AchievementPublicationResult.Failed
                            : AchievementPublicationResult.Unavailable);
                    return;
                }

                for (var i = 0; i < operation.Batch.AchievementIds.Count; i++)
                {
                    var achievementId = operation.Batch.AchievementIds[i];
                    if (!_mapping.TryGetExpectedSteamApiName(
                            achievementId,
                            out var expectedSteamApiName) ||
                        !EnsureSchemaContains(expectedSteamApiName.Value))
                    {
                        operation.Results[achievementId] =
                            AchievementPublicationResult.Rejected;
                        continue;
                    }

                    if (!_achievementApi.GetAchievement(
                            expectedSteamApiName.Value,
                            out var achieved))
                    {
                        operation.Results[achievementId] =
                            AchievementPublicationResult.Failed;
                        continue;
                    }

                    if (achieved)
                    {
                        operation.Results[achievementId] =
                            AchievementPublicationResult.AlreadySatisfied;
                        continue;
                    }

                    if (!_achievementApi.SetAchievement(expectedSteamApiName.Value))
                    {
                        operation.Results[achievementId] =
                            AchievementPublicationResult.Failed;
                        continue;
                    }

                    operation.StoreCandidates.Add(achievementId);
                    operation.ExpectedByName.Add(expectedSteamApiName.Value, achievementId);
                }

                if (operation.StoreCandidates.Count == 0)
                {
                    Complete(operation);
                    return;
                }

                lock (_gate)
                {
                    if (!IsCurrentLocked(operation))
                    {
                        return;
                    }

                    operation.StoreCallStarted = true;
                }

                var storeSucceeded = _achievementApi.StoreStats();
                lock (_gate)
                {
                    if (!IsCurrentLocked(operation))
                    {
                        return;
                    }

                    operation.StoreCallReturned = true;
                    operation.StoreSucceeded = storeSucceeded;
                    if (storeSucceeded)
                    {
                        operation.CallbackWaitStarted = true;
                        operation.CallbackWaitStartedAt = _monotonicSeconds();
                    }
                }

                if (!storeSucceeded)
                {
                    MarkStoreCandidates(
                        operation,
                        AchievementPublicationResult.Failed,
                        overwrite: true);
                    Complete(operation);
                    return;
                }

                TryCompleteAfterNamedCallbacks(operation);
            }
            catch
            {
                AbortPublicationSession(operation, AchievementPublicationResult.Failed);
            }
        }

        private bool EnsureSchemaContains(string achievementName)
        {
            lock (_gate)
            {
                if (_sessionSchema != null)
                {
                    return _sessionSchema.Contains(achievementName);
                }
            }

            var enumerated = new HashSet<string>(StringComparer.Ordinal);
            var count = _achievementApi.GetNumAchievements();
            for (uint index = 0; index < count; index++)
            {
                enumerated.Add(_achievementApi.GetAchievementName(index));
            }

            lock (_gate)
            {
                _sessionSchema ??= enumerated;
                return _sessionSchema.Contains(achievementName);
            }
        }

        private bool TryObserveReadyPublicationSession(
            out uint currentAppId,
            out bool threw)
        {
            currentAppId = 0;
            threw = false;
            try
            {
                currentAppId = _lifecycleApi.GetAppId();
                return currentAppId != 0 &&
                       currentAppId == _prerequisites.ObservedAppId &&
                       _lifecycleApi.IsSteamIdValid() &&
                       _lifecycleApi.IsLoggedOn();
            }
            catch
            {
                currentAppId = 0;
                threw = true;
                return false;
            }
        }

        private void ObserveStatsStored(SteamStatsStoredObservation observation)
        {
            lock (_gate)
            {
                if (_state == PublicationSessionState.Disposed ||
                    observation.AppId != _prerequisites.ObservedAppId)
                {
                    return;
                }

                _statsStoredObservationCount++;
                _lastStatsStoredResult = observation.Result;
            }
        }

        private void ObserveAchievementStored(
            SteamAchievementStoredObservation observation)
        {
            PublicationBatchOperation operation;
            lock (_gate)
            {
                operation = _operation;
                if (_state != PublicationSessionState.Active ||
                    !IsCurrentLocked(operation) ||
                    !operation.StoreCallStarted ||
                    observation.AppId != operation.AppId ||
                    !observation.IsFullUnlock ||
                    !operation.ExpectedByName.TryGetValue(
                        observation.AchievementName,
                        out var achievementId))
                {
                    return;
                }

                operation.Results[achievementId] = AchievementPublicationResult.Submitted;
                operation.ExpectedByName.Remove(observation.AchievementName);
            }

            TryCompleteAfterNamedCallbacks(operation);
        }

        private void TryCompleteAfterNamedCallbacks(PublicationBatchOperation operation)
        {
            lock (_gate)
            {
                if (!IsCurrentLocked(operation) ||
                    !operation.StoreCallReturned ||
                    !operation.StoreSucceeded ||
                    operation.ExpectedByName.Count != 0)
                {
                    return;
                }
            }

            Complete(operation);
        }

        private void Complete(PublicationBatchOperation operation)
        {
            BatchCompletion completion;
            PublicationBatchOperation next = null;
            lock (_gate)
            {
                if (!IsCurrentLocked(operation))
                {
                    return;
                }

                completion = CompleteLocked(
                    operation,
                    AchievementPublicationResult.Failed);
                _operation = null;
                if (_state == PublicationSessionState.Active &&
                    _callbacksRegistered &&
                    _pendingOperations.Count > 0)
                {
                    next = _pendingOperations.Dequeue();
                    _operation = next;
                }
            }

            InvokeCompletion(completion.Completion, completion.Result);
            if (next != null)
            {
                BeginMutation(next);
            }
        }

        private void AbortPublicationSession(
            PublicationBatchOperation operation,
            AchievementPublicationResult unresolvedResult)
        {
            var completions = new List<BatchCompletion>();
            var disposeCallbacks = false;
            lock (_gate)
            {
                if (!IsCurrentLocked(operation))
                {
                    return;
                }

                completions.Add(CompleteLocked(operation, unresolvedResult));
                _operation = null;
                _state = PublicationSessionState.Quarantined;
                _sessionSchema = null;

                while (_pendingOperations.Count > 0)
                {
                    completions.Add(CompleteUniformLocked(
                        _pendingOperations.Dequeue(),
                        AchievementPublicationResult.Unavailable));
                }

                disposeCallbacks = _callbacksRegistered;
                _callbacksRegistered = false;
            }

            if (disposeCallbacks)
            {
                TryDisposeCallbacks();
            }

            InvokeCompletions(completions);
        }

        private static void MarkStoreCandidates(
            PublicationBatchOperation operation,
            AchievementPublicationResult result,
            bool overwrite)
        {
            for (var i = 0; i < operation.StoreCandidates.Count; i++)
            {
                var achievementId = operation.StoreCandidates[i];
                if (overwrite || !operation.Results.ContainsKey(achievementId))
                {
                    operation.Results[achievementId] = result;
                }
            }

            operation.ExpectedByName.Clear();
        }

        private static BatchCompletion CompleteLocked(
            PublicationBatchOperation operation,
            AchievementPublicationResult unresolvedResult)
        {
            operation.Completed = true;
            var items = new AchievementPublicationItemResult[
                operation.Batch.AchievementIds.Count];
            for (var i = 0; i < items.Length; i++)
            {
                var achievementId = operation.Batch.AchievementIds[i];
                if (!operation.Results.TryGetValue(achievementId, out var itemResult))
                {
                    itemResult = unresolvedResult;
                }

                items[i] = new AchievementPublicationItemResult(
                    achievementId,
                    itemResult);
            }

            return new BatchCompletion(
                operation.Completion,
                new AchievementPublicationBatchResult(items));
        }

        private static BatchCompletion CompleteUniformLocked(
            PublicationBatchOperation operation,
            AchievementPublicationResult result)
        {
            operation.Completed = true;
            return new BatchCompletion(
                operation.Completion,
                AchievementPublicationBatchResult.Uniform(operation.Batch, result));
        }

        private bool IsCurrentLocked(PublicationBatchOperation operation)
        {
            return operation != null &&
                   !operation.Completed &&
                   ReferenceEquals(_operation, operation);
        }

        private void TryDisposeCallbacks()
        {
            try
            {
                _achievementApi.DisposeAchievementStoreCallbacks();
            }
            catch
            {
                // Quarantined/disposed state is authoritative even if cleanup fails.
            }
        }

        private static void InvokeCompletions(List<BatchCompletion> completions)
        {
            for (var i = 0; i < completions.Count; i++)
            {
                InvokeCompletion(completions[i].Completion, completions[i].Result);
            }
        }

        private static void InvokeCompletion(
            Action<AchievementPublicationBatchResult> completion,
            AchievementPublicationBatchResult result)
        {
            try
            {
                completion(result);
            }
            catch
            {
                // Product completion observers cannot escape the Steam runtime boundary.
            }
        }

        private enum PublicationSessionState
        {
            Inactive = 0,
            Active = 1,
            Quarantined = 2,
            Disposed = 3,
        }

        private readonly struct BatchCompletion
        {
            internal BatchCompletion(
                Action<AchievementPublicationBatchResult> completion,
                AchievementPublicationBatchResult result)
            {
                Completion = completion;
                Result = result;
            }

            internal Action<AchievementPublicationBatchResult> Completion { get; }
            internal AchievementPublicationBatchResult Result { get; }
        }

        private sealed class PublicationBatchOperation
        {
            internal PublicationBatchOperation(
                AchievementPublicationBatch batch,
                uint appId,
                Action<AchievementPublicationBatchResult> completion)
            {
                Batch = batch;
                AppId = appId;
                Completion = completion;
            }

            internal AchievementPublicationBatch Batch { get; }
            internal uint AppId { get; }
            internal Action<AchievementPublicationBatchResult> Completion { get; }
            internal Dictionary<GameAchievementId, AchievementPublicationResult> Results
                { get; } = new();
            internal List<GameAchievementId> StoreCandidates { get; } = new();
            internal Dictionary<string, GameAchievementId> ExpectedByName { get; } =
                new(StringComparer.Ordinal);
            internal bool StoreCallStarted { get; set; }
            internal bool StoreCallReturned { get; set; }
            internal bool StoreSucceeded { get; set; }
            internal bool CallbackWaitStarted { get; set; }
            internal double CallbackWaitStartedAt { get; set; }
            internal bool Completed { get; set; }
        }
    }
}
