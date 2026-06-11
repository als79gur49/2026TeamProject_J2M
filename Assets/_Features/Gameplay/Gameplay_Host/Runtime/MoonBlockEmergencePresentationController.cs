using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct MoonBlockEmergencePresentationRequest
    {
        public MoonBlockEmergencePresentationRequest(
            int entityId,
            int spawnTick,
            int spawnInteractionLockTicks)
        {
            EntityId = entityId;
            SpawnTick = spawnTick;
            SpawnInteractionLockTicks = spawnInteractionLockTicks;
        }

        public int EntityId { get; }

        public int SpawnTick { get; }

        public int SpawnInteractionLockTicks { get; }

        public int ExpiresTickExclusive => SpawnTick + Math.Max(0, SpawnInteractionLockTicks);
    }

    internal sealed class MoonBlockEmergencePresentationController
    {
        private const float DoorOpenLeadSeconds = 0.07f;
        private const float StartScaleMultiplier = 0f;
        private const float LaunchScaleMultiplier = 1.08f;
        private static readonly Vector3 StartLocalOffset = new(0f, 0f, -0.45f);
        private static readonly Vector3 LaunchLocalOffset = new(0f, 0f, 0.22f);

        private readonly Dictionary<int, MoonBlockEmergencePresentationRequest> _pendingByEntityId = new();
        private readonly List<MoonBlockEmergencePresentationDriver> _activeDrivers = new();
        private readonly List<int> _expiredPendingEntityIds = new();
        private readonly List<int> _readyPendingEntityIds = new();

        private GameplayEntityViewRegistry _viewRegistry;
        private GameplayTimingProfile _timingProfile = GameplayTimingProfile.CreateDefault();
        private int _currentTickIndex;
        private bool _allowImmediateRegisteredStart;

        public int PendingRequestCount => _pendingByEntityId.Count;

        public void Configure(GameplayEntityViewRegistry viewRegistry, GameplayTimingProfile timingProfile)
        {
            if (_viewRegistry != null)
            {
                _viewRegistry.ViewRegistered -= HandleViewRegistered;
            }

            _viewRegistry = viewRegistry;
            _timingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();

            if (_viewRegistry != null)
            {
                _viewRegistry.ViewRegistered += HandleViewRegistered;
            }
        }

        public void ResetSession()
        {
            _pendingByEntityId.Clear();
            _expiredPendingEntityIds.Clear();
            _readyPendingEntityIds.Clear();
            _allowImmediateRegisteredStart = false;
            for (var i = 0; i < _activeDrivers.Count; i++)
            {
                _activeDrivers[i]?.NormalizeToFinalState();
            }

            _activeDrivers.Clear();
        }

        public void Dispose()
        {
            if (_viewRegistry != null)
            {
                _viewRegistry.ViewRegistered -= HandleViewRegistered;
                _viewRegistry = null;
            }

            ResetSession();
        }

        public void QueueRequests(IReadOnlyList<TilePresentationRequest> requests, int currentTickIndex)
        {
            _currentTickIndex = currentTickIndex;
            _allowImmediateRegisteredStart = false;
            NormalizeExpiredPendingRequests(currentTickIndex);
            if (requests == null || requests.Count == 0)
            {
                return;
            }

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.RequestKind != TilePresentationRequestKind.MoonBlockGenerated ||
                    request.TargetEntityId <= 0)
                {
                    continue;
                }

                var emergenceRequest = new MoonBlockEmergencePresentationRequest(
                    request.TargetEntityId,
                    request.SpawnTick,
                    request.SpawnInteractionLockTicks);
                QueueRequest(emergenceRequest, currentTickIndex);
            }
        }

        public void QueueRequest(
            in MoonBlockEmergencePresentationRequest request,
            int currentTickIndex)
        {
            _currentTickIndex = currentTickIndex;
            _allowImmediateRegisteredStart = false;
            NormalizeExpiredPendingRequests(currentTickIndex);
            if (request.EntityId <= 0)
            {
                return;
            }

            _pendingByEntityId[request.EntityId] = request;
        }

        public void PrepareHiddenReady(int entityId)
        {
            if (_viewRegistry == null ||
                !_viewRegistry.TryGetView(entityId, out var view) ||
                view == null)
            {
                return;
            }

            var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>() ??
                         view.gameObject.AddComponent<MoonBlockEmergencePresentationDriver>();
            driver.PrepareHiddenReady();
        }

        public void StartReadyRequests(int currentTickIndex)
        {
            _currentTickIndex = currentTickIndex;
            NormalizeExpiredPendingRequests(currentTickIndex);
            if (_pendingByEntityId.Count == 0)
            {
                _allowImmediateRegisteredStart = true;
                return;
            }

            if (_viewRegistry == null)
            {
                _allowImmediateRegisteredStart = true;
                return;
            }

            _readyPendingEntityIds.Clear();
            foreach (var entry in _pendingByEntityId)
            {
                if (!_viewRegistry.TryGetView(entry.Key, out var view) ||
                    view == null)
                {
                    continue;
                }

                StartOrNormalize(view, entry.Value);
                _readyPendingEntityIds.Add(entry.Key);
            }

            for (var i = 0; i < _readyPendingEntityIds.Count; i++)
            {
                _pendingByEntityId.Remove(_readyPendingEntityIds[i]);
            }

            _readyPendingEntityIds.Clear();
            _allowImmediateRegisteredStart = true;
        }

        public void UpdatePresentation(float deltaTime)
        {
            for (var i = _activeDrivers.Count - 1; i >= 0; i--)
            {
                var driver = _activeDrivers[i];
                if (driver == null || !driver.IsPlaying)
                {
                    _activeDrivers.RemoveAt(i);
                    continue;
                }

                driver.Advance(deltaTime);
                if (!driver.IsPlaying)
                {
                    _activeDrivers.RemoveAt(i);
                }
            }
        }

        public void NormalizeReadyAndActive(int currentTickIndex)
        {
            _currentTickIndex = currentTickIndex;
            for (var i = 0; i < _activeDrivers.Count; i++)
            {
                _activeDrivers[i]?.NormalizeToFinalState();
            }

            _activeDrivers.Clear();
            NormalizeExpiredPendingRequests(currentTickIndex, normalizeAllReady: true);
        }

        private void HandleViewRegistered(GameplayEntityView view)
        {
            if (view == null ||
                !_pendingByEntityId.TryGetValue(view.EntityId, out var request))
            {
                return;
            }

            _pendingByEntityId.Remove(view.EntityId);
            if (!_allowImmediateRegisteredStart)
            {
                _pendingByEntityId[view.EntityId] = request;
                return;
            }

            StartOrNormalize(view, request);
        }

        private void StartOrNormalize(GameplayEntityView view, in MoonBlockEmergencePresentationRequest request)
        {
            var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>() ??
                         view.gameObject.AddComponent<MoonBlockEmergencePresentationDriver>();
            if (_currentTickIndex >= request.ExpiresTickExclusive)
            {
                driver.NormalizeToFinalState();
                return;
            }

            var durationSeconds = ResolveDurationSeconds(request);
            driver.Play(
                DoorOpenLeadSeconds,
                durationSeconds,
                StartScaleMultiplier,
                LaunchScaleMultiplier,
                StartLocalOffset,
                LaunchLocalOffset);
            if (driver.IsPlaying && !_activeDrivers.Contains(driver))
            {
                _activeDrivers.Add(driver);
            }
        }

        private float ResolveDurationSeconds(in MoonBlockEmergencePresentationRequest request)
        {
            var profileDurationSeconds = _timingProfile.MoonBlockEmergenceDurationSeconds;
            if (request.SpawnInteractionLockTicks <= 0 || _timingProfile.SimulationTicksPerSecond <= 0)
            {
                return profileDurationSeconds;
            }

            var lockDurationSeconds = request.SpawnInteractionLockTicks / (float)_timingProfile.SimulationTicksPerSecond;
            return Mathf.Max(0.0001f, Mathf.Max(profileDurationSeconds, lockDurationSeconds));
        }

        private void NormalizeExpiredPendingRequests(int currentTickIndex, bool normalizeAllReady = false)
        {
            if (_pendingByEntityId.Count == 0)
            {
                return;
            }

            _expiredPendingEntityIds.Clear();
            foreach (var entry in _pendingByEntityId)
            {
                if (!normalizeAllReady && currentTickIndex < entry.Value.ExpiresTickExclusive)
                {
                    continue;
                }

                if (_viewRegistry == null ||
                    !_viewRegistry.TryGetView(entry.Key, out var view) ||
                    view == null)
                {
                    _expiredPendingEntityIds.Add(entry.Key);
                    continue;
                }

                var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>() ??
                             view.gameObject.AddComponent<MoonBlockEmergencePresentationDriver>();
                driver.NormalizeToFinalState();
                _expiredPendingEntityIds.Add(entry.Key);
            }

            for (var i = 0; i < _expiredPendingEntityIds.Count; i++)
            {
                _pendingByEntityId.Remove(_expiredPendingEntityIds[i]);
            }

            _expiredPendingEntityIds.Clear();
        }
    }
}
