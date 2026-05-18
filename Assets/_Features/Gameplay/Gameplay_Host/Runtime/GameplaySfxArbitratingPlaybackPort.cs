using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplaySfxArbitratingPlaybackPort : IGameplayAudioPlaybackPort
    {
        private readonly GameplaySfxArbiter _arbiter;
        private readonly IGameplayAudioPlaybackPort _inner;
        private readonly List<GameplaySfxRequest> _pendingRequests = new();

        private bool _isCollecting;
        private int _currentTickIndex;
        private int _simulationTicksPerSecond = 20;

        public GameplaySfxArbitratingPlaybackPort(
            IGameplayAudioPlaybackPort inner,
            GameplaySfxArbiter arbiter)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        }

        public void BeginBatch(int tickIndex, int simulationTicksPerSecond)
        {
            _pendingRequests.Clear();
            _currentTickIndex = tickIndex;
            _simulationTicksPerSecond = Math.Max(1, simulationTicksPerSecond);
            _isCollecting = true;
        }

        public void FlushBatch()
        {
            if (!_isCollecting)
            {
                return;
            }

            try
            {
                PlayAccepted(_arbiter.Filter(_pendingRequests, _currentTickIndex, _simulationTicksPerSecond));
            }
            finally
            {
                _pendingRequests.Clear();
                _isCollecting = false;
            }
        }

        public void CancelBatch()
        {
            _pendingRequests.Clear();
            _isCollecting = false;
        }

        public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
        {
            EnqueueOrPlay(new GameplaySfxRequest(
                definition,
                GameplaySfxPolicyCatalog.Resolve(context.DebugTag),
                context.OwnerEntityId,
                attachOwner: null,
                attachmentSlot: default,
                context,
                _currentTickIndex,
                _arbiter.NextSequence()));
        }

        public void PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context)
        {
            EnqueueOrPlay(new GameplaySfxRequest(
                definition,
                GameplaySfxPolicyCatalog.Resolve(context.DebugTag),
                context.OwnerEntityId,
                owner,
                slot,
                context,
                _currentTickIndex,
                _arbiter.NextSequence()));
        }

        private void EnqueueOrPlay(in GameplaySfxRequest request)
        {
            if (_isCollecting)
            {
                _pendingRequests.Add(request);
                return;
            }

            var accepted = _arbiter.Filter(
                new[] { request },
                request.TickIndex,
                _simulationTicksPerSecond);
            PlayAccepted(accepted);
        }

        private void PlayAccepted(IReadOnlyList<GameplaySfxRequest> accepted)
        {
            for (var i = 0; i < accepted.Count; i++)
            {
                var request = accepted[i];
                if (request.IsAttached)
                {
                    _inner.PlayAttached(
                        request.Definition,
                        request.AttachOwner,
                        request.AttachmentSlot,
                        request.Context);
                    continue;
                }

                _inner.Play2D(request.Definition, request.Context);
            }
        }
    }
}
