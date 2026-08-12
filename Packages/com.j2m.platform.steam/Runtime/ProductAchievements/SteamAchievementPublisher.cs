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

        private SteamAchievementSessionPrerequisites _prerequisites;
        private HashSet<string> _sessionSchema;
        private PublicationOperation _operation;
        private bool _beginAttempted;
        private bool _callbacksRegistered;
        private bool _sessionActive;
        private bool _disposed;

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

        internal bool BeginSession(SteamAchievementSessionPrerequisites prerequisites)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return false;
                }

                if (_beginAttempted)
                {
                    return _sessionActive;
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
                try
                {
                    _achievementApi.DisposeAchievementStoreCallbacks();
                }
                catch
                {
                    // Callback registration cleanup is best effort and session-local.
                }

                return false;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    try
                    {
                        _achievementApi.DisposeAchievementStoreCallbacks();
                    }
                    catch
                    {
                        // Native shutdown must remain available after callback cleanup failure.
                    }

                    return false;
                }

                _callbacksRegistered = true;
                _sessionActive = true;
                return true;
            }
        }

        public void Publish(
            GameAchievementId achievementId,
            Action<AchievementPublicationResult> completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            if (!_mapping.TryGetExpectedSteamApiName(
                achievementId,
                out var expectedSteamApiName))
            {
                InvokeCompletion(completed, AchievementPublicationResult.Rejected);
                return;
            }

            PublicationOperation operation = null;
            AchievementPublicationResult? immediateResult = null;
            lock (_gate)
            {
                if (_disposed ||
                    !_sessionActive ||
                    !_callbacksRegistered ||
                    !_prerequisites.IsReady)
                {
                    immediateResult = AchievementPublicationResult.Unavailable;
                }
                else if (_operation != null)
                {
                    immediateResult = AchievementPublicationResult.Deferred;
                }
            }

            if (immediateResult.HasValue)
            {
                InvokeCompletion(completed, immediateResult.Value);
                return;
            }

            if (!TryObserveReadyPublicationSession(out var currentAppId))
            {
                InvokeCompletion(completed, AchievementPublicationResult.Unavailable);
                return;
            }

            lock (_gate)
            {
                if (_disposed || !_sessionActive || !_callbacksRegistered)
                {
                    immediateResult = AchievementPublicationResult.Unavailable;
                }
                else if (_operation != null)
                {
                    immediateResult = AchievementPublicationResult.Deferred;
                }
                else
                {
                    operation = new PublicationOperation(
                        expectedSteamApiName,
                        currentAppId,
                        completed);
                    _operation = operation;
                }
            }

            if (immediateResult.HasValue)
            {
                InvokeCompletion(completed, immediateResult.Value);
                return;
            }

            BeginMutation(operation);
        }

        internal void Tick()
        {
            PublicationOperation timedOut = null;
            lock (_gate)
            {
                if (_disposed ||
                    _operation == null ||
                    !_operation.CallbackWaitStarted ||
                    _monotonicSeconds() - _operation.CallbackWaitStartedAt <
                    CallbackTimeoutSeconds)
                {
                    return;
                }

                timedOut = _operation;
            }

            Complete(timedOut, AchievementPublicationResult.Failed);
        }

        public void Dispose()
        {
            Action<AchievementPublicationResult> completion = null;
            var disposeCallbacks = false;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _sessionActive = false;
                _sessionSchema = null;
                if (_operation != null)
                {
                    _operation.Completed = true;
                    completion = _operation.Completion;
                    _operation = null;
                }

                disposeCallbacks = _callbacksRegistered;
                _callbacksRegistered = false;
            }

            if (disposeCallbacks)
            {
                try
                {
                    _achievementApi.DisposeAchievementStoreCallbacks();
                }
                catch
                {
                    // Callback cleanup must not block the canonical native shutdown owner.
                }
            }

            if (completion != null)
            {
                InvokeCompletion(completion, AchievementPublicationResult.Unavailable);
            }
        }

        private void BeginMutation(PublicationOperation operation)
        {
            try
            {
                if (!EnsureSchemaContains(operation.ExpectedSteamApiName.Value))
                {
                    Complete(operation, AchievementPublicationResult.Rejected);
                    return;
                }

                if (!_achievementApi.GetAchievement(
                    operation.ExpectedSteamApiName.Value,
                    out var achieved))
                {
                    Complete(operation, AchievementPublicationResult.Failed);
                    return;
                }

                if (achieved)
                {
                    Complete(operation, AchievementPublicationResult.AlreadySatisfied);
                    return;
                }

                if (!_achievementApi.SetAchievement(operation.ExpectedSteamApiName.Value))
                {
                    Complete(operation, AchievementPublicationResult.Failed);
                    return;
                }

                lock (_gate)
                {
                    if (!IsCurrentLocked(operation))
                    {
                        return;
                    }
                }

                var storeSucceeded = _achievementApi.StoreStats();
                if (!storeSucceeded)
                {
                    Complete(operation, AchievementPublicationResult.Failed);
                    return;
                }

                lock (_gate)
                {
                    if (!IsCurrentLocked(operation))
                    {
                        return;
                    }

                    operation.StoreSucceeded = true;
                    operation.CallbackWaitStarted = true;
                    operation.CallbackWaitStartedAt = _monotonicSeconds();
                }

                TryBeginPostRead(operation);
            }
            catch
            {
                Complete(operation, AchievementPublicationResult.Failed);
            }
        }

        private bool EnsureSchemaContains(string expectedSteamApiName)
        {
            HashSet<string> schema;
            lock (_gate)
            {
                schema = _sessionSchema;
            }

            if (schema == null)
            {
                var enumerated = new HashSet<string>(StringComparer.Ordinal);
                var count = _achievementApi.GetNumAchievements();
                for (uint index = 0; index < count; index++)
                {
                    enumerated.Add(_achievementApi.GetAchievementName(index));
                }

                lock (_gate)
                {
                    _sessionSchema ??= enumerated;
                    schema = _sessionSchema;
                }
            }

            return schema.Contains(expectedSteamApiName);
        }

        private bool TryObserveReadyPublicationSession(out uint currentAppId)
        {
            currentAppId = 0;
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
                return false;
            }
        }

        private void ObserveStatsStored(SteamStatsStoredObservation observation)
        {
            PublicationOperation operation;
            lock (_gate)
            {
                operation = _operation;
                if (_disposed ||
                    operation == null ||
                    observation.AppId != operation.AppId)
                {
                    return;
                }

                if (observation.Result != SteamCallbackResult.Ok)
                {
                    operation = _operation;
                }
                else
                {
                    operation.StatsStoredObserved = true;
                    operation = null;
                }
            }

            if (operation != null)
            {
                Complete(operation, AchievementPublicationResult.Failed);
                return;
            }

            PublicationOperation current;
            lock (_gate)
            {
                current = _operation;
            }

            TryBeginPostRead(current);
        }

        private void ObserveAchievementStored(
            SteamAchievementStoredObservation observation)
        {
            PublicationOperation operation;
            lock (_gate)
            {
                operation = _operation;
                if (_disposed ||
                    operation == null ||
                    observation.AppId != operation.AppId ||
                    !string.Equals(
                        observation.AchievementName,
                        operation.ExpectedSteamApiName.Value,
                        StringComparison.Ordinal) ||
                    !observation.IsFullUnlock)
                {
                    return;
                }

                operation.AchievementStoredObserved = true;
            }

            TryBeginPostRead(operation);
        }

        private void TryBeginPostRead(PublicationOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            lock (_gate)
            {
                if (!IsCurrentLocked(operation) ||
                    !operation.StoreSucceeded ||
                    !operation.StatsStoredObserved ||
                    !operation.AchievementStoredObserved ||
                    operation.PostReadStarted)
                {
                    return;
                }

                operation.PostReadStarted = true;
            }

            try
            {
                var readSucceeded = _achievementApi.GetAchievement(
                    operation.ExpectedSteamApiName.Value,
                    out var achieved);
                Complete(
                    operation,
                    readSucceeded && achieved
                        ? AchievementPublicationResult.Accepted
                        : AchievementPublicationResult.Failed);
            }
            catch
            {
                Complete(operation, AchievementPublicationResult.Failed);
            }
        }

        private void Complete(
            PublicationOperation operation,
            AchievementPublicationResult result)
        {
            Action<AchievementPublicationResult> completion;
            lock (_gate)
            {
                if (!IsCurrentLocked(operation))
                {
                    return;
                }

                operation.Completed = true;
                completion = operation.Completion;
                _operation = null;
            }

            InvokeCompletion(completion, result);
        }

        private bool IsCurrentLocked(PublicationOperation operation)
        {
            return operation != null &&
                   !operation.Completed &&
                   ReferenceEquals(_operation, operation);
        }

        private static void InvokeCompletion(
            Action<AchievementPublicationResult> completion,
            AchievementPublicationResult result)
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

        private sealed class PublicationOperation
        {
            internal PublicationOperation(
                ExpectedSteamAchievementApiName expectedSteamApiName,
                uint appId,
                Action<AchievementPublicationResult> completion)
            {
                ExpectedSteamApiName = expectedSteamApiName;
                AppId = appId;
                Completion = completion;
            }

            internal ExpectedSteamAchievementApiName ExpectedSteamApiName { get; }

            internal uint AppId { get; }

            internal Action<AchievementPublicationResult> Completion { get; }

            internal bool StoreSucceeded { get; set; }

            internal bool StatsStoredObserved { get; set; }

            internal bool AchievementStoredObserved { get; set; }

            internal bool CallbackWaitStarted { get; set; }

            internal double CallbackWaitStartedAt { get; set; }

            internal bool PostReadStarted { get; set; }

            internal bool Completed { get; set; }
        }
    }
}
