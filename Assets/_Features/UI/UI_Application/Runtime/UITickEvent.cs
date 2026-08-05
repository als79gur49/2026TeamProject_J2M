using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;

namespace Game.Feature.UI.Application
{
    public enum UITickEventKind
    {
        TopologyTransitionStarted = 0,
        PlayerActionStarted = 1,
        PlayerActionResolved = 2,
        PlayerActionCompleted = 3,
        PlayerActionCanceled = 4,
        PlayerDamaged = 5,
        StageCleared = 6,
    }

    public readonly struct UITickEventKey : IEquatable<UITickEventKey>
    {
        public UITickEventKey(
            int tickIndex,
            UITickEventKind eventKind,
            int actorEntityId,
            GameplayUiActionKind actionKind,
            int actionSequence,
            GameplayUiActionResolutionKind resolutionKind)
        {
            TickIndex = tickIndex;
            EventKind = eventKind;
            ActorEntityId = actorEntityId;
            ActionKind = actionKind;
            ActionSequence = actionSequence;
            ResolutionKind = resolutionKind;
        }

        public int TickIndex { get; }

        public UITickEventKind EventKind { get; }

        public int ActorEntityId { get; }

        public GameplayUiActionKind ActionKind { get; }

        public int ActionSequence { get; }

        public GameplayUiActionResolutionKind ResolutionKind { get; }

        public bool Equals(UITickEventKey other)
        {
            return TickIndex == other.TickIndex &&
                   EventKind == other.EventKind &&
                   ActorEntityId == other.ActorEntityId &&
                   ActionKind == other.ActionKind &&
                   ActionSequence == other.ActionSequence &&
                   ResolutionKind == other.ResolutionKind;
        }

        public override bool Equals(object obj)
        {
            return obj is UITickEventKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TickIndex, EventKind, ActorEntityId, ActionKind, ActionSequence, ResolutionKind);
        }
    }

    public readonly struct UITickEvent : IEquatable<UITickEvent>
    {
        public UITickEvent(
            UITickEventKey key,
            int damageAmount = 0,
            TerminalSessionToken terminalToken = default)
        {
            Key = key;
            DamageAmount = damageAmount;
            TerminalToken = terminalToken;
        }

        public UITickEventKey Key { get; }

        public int TickIndex => Key.TickIndex;

        public UITickEventKind EventKind => Key.EventKind;

        public int ActorEntityId => Key.ActorEntityId;

        public GameplayUiActionKind ActionKind => Key.ActionKind;

        public int ActionSequence => Key.ActionSequence;

        public GameplayUiActionResolutionKind ResolutionKind => Key.ResolutionKind;

        public int DamageAmount { get; }

        public TerminalSessionToken TerminalToken { get; }

        public long TerminalClaimId => TerminalToken.Sequence;

        public bool Equals(UITickEvent other)
        {
            return Key.Equals(other.Key) &&
                   DamageAmount == other.DamageAmount &&
                   TerminalToken == other.TerminalToken;
        }

        public override bool Equals(object obj)
        {
            return obj is UITickEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, DamageAmount, TerminalToken);
        }

        public UITickEvent(
            UITickEventKey key,
            int damageAmount,
            long terminalClaimId)
            : this(
                key,
                damageAmount,
                terminalClaimId > 0
                    ? new TerminalSessionToken(
                        TerminalSessionRegistry.Authority.AuthorityGeneration,
                        terminalClaimId)
                    : default)
        {
        }
    }

    public readonly struct UITickEventBatch
    {
        public static readonly UITickEventBatch Empty = new(0, Array.Empty<UITickEvent>());

        private readonly ReadOnlyCollection<UITickEvent> _events;

        public UITickEventBatch(
            int tickIndex,
            IEnumerable<UITickEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            TickIndex = tickIndex;
            _events = new ReadOnlyCollection<UITickEvent>(new List<UITickEvent>(events));
        }

        public int TickIndex { get; }

        public IReadOnlyList<UITickEvent> Events => _events != null
            ? _events
            : Array.Empty<UITickEvent>();

        public bool HasAnyEvents => Events.Count > 0;
    }
}
