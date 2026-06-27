using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal enum GameplayAudioPlaybackBlockReason
    {
        None = 0,
        TopologyPresentationLock = 1,
    }

    internal enum GameplayAudioPlaybackDecision
    {
        PlayNow = 0,
        DeferUntilUnlock = 1,
        Suppress = 2,
    }

    internal readonly struct GameplayAudioPlaybackGateState
    {
        public GameplayAudioPlaybackGateState(
            bool isBlocked,
            GameplayAudioPlaybackBlockReason reason)
        {
            IsBlocked = isBlocked;
            Reason = isBlocked ? reason : GameplayAudioPlaybackBlockReason.None;
        }

        public bool IsBlocked { get; }

        public GameplayAudioPlaybackBlockReason Reason { get; }

        public static GameplayAudioPlaybackGateState Open =>
            new(false, GameplayAudioPlaybackBlockReason.None);

        public static GameplayAudioPlaybackGateState TopologyLocked =>
            new(true, GameplayAudioPlaybackBlockReason.TopologyPresentationLock);
    }

    internal readonly struct GameplayAudioPlaybackRequestKey : IEquatable<GameplayAudioPlaybackRequestKey>
    {
        public GameplayAudioPlaybackRequestKey(
            int laneId,
            int tickIndex,
            int semanticId,
            int ownerEntityId,
            int orderIndex,
            int targetFace = 0,
            int targetX = 0,
            int targetY = 0,
            int eventTick = 0,
            int eventId = 0,
            int presentationKey = 0)
        {
            LaneId = laneId;
            TickIndex = tickIndex;
            SemanticId = semanticId;
            OwnerEntityId = ownerEntityId;
            OrderIndex = orderIndex;
            TargetFace = targetFace;
            TargetX = targetX;
            TargetY = targetY;
            EventTick = eventTick;
            EventId = eventId;
            PresentationKey = presentationKey;
        }

        public int LaneId { get; }

        public int TickIndex { get; }

        public int SemanticId { get; }

        public int OwnerEntityId { get; }

        public int OrderIndex { get; }

        public int TargetFace { get; }

        public int TargetX { get; }

        public int TargetY { get; }

        public int EventTick { get; }

        public int EventId { get; }

        public int PresentationKey { get; }

        public bool Equals(GameplayAudioPlaybackRequestKey other)
        {
            return LaneId == other.LaneId &&
                   TickIndex == other.TickIndex &&
                   SemanticId == other.SemanticId &&
                   OwnerEntityId == other.OwnerEntityId &&
                   OrderIndex == other.OrderIndex &&
                   TargetFace == other.TargetFace &&
                   TargetX == other.TargetX &&
                   TargetY == other.TargetY &&
                   EventTick == other.EventTick &&
                   EventId == other.EventId &&
                   PresentationKey == other.PresentationKey;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayAudioPlaybackRequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = LaneId;
                hash = (hash * 397) ^ TickIndex;
                hash = (hash * 397) ^ SemanticId;
                hash = (hash * 397) ^ OwnerEntityId;
                hash = (hash * 397) ^ OrderIndex;
                hash = (hash * 397) ^ TargetFace;
                hash = (hash * 397) ^ TargetX;
                hash = (hash * 397) ^ TargetY;
                hash = (hash * 397) ^ EventTick;
                hash = (hash * 397) ^ EventId;
                hash = (hash * 397) ^ PresentationKey;
                return hash;
            }
        }
    }

    internal interface IGameplayAudioPlaybackPort
    {
        void Play2D(AudioDefinition definition, in AudioPlaybackContext context);

        void PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context);
    }

    internal interface IGameplayAudioLoopPlaybackPort
    {
        AudioPlaybackHandle PlayAttachedLoop(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context);
    }

    internal sealed class GameplayAudioPlaybackPortAdapter : IGameplayAudioPlaybackPort, IGameplayAudioLoopPlaybackPort
    {
        private readonly IAudioService _audioService;

        public GameplayAudioPlaybackPortAdapter(IAudioService audioService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
        {
            _audioService.Play2D(definition, context);
        }

        public void PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context)
        {
            _audioService.PlayAttached(definition, owner, slot, context);
        }

        public AudioPlaybackHandle PlayAttachedLoop(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context)
        {
            return _audioService.PlayAttached(definition, owner, slot, context);
        }
    }

}
