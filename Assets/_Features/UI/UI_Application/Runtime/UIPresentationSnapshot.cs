using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.UI.Application
{
    public readonly struct UITickSlice : IEquatable<UITickSlice>
    {
        public UITickSlice(
            int lastReducedTickIndex,
            GameplayUiTopology finalTopology,
            bool isStageCleared,
            bool isTopologyTransitionActive)
        {
            LastReducedTickIndex = lastReducedTickIndex;
            FinalTopology = finalTopology;
            IsStageCleared = isStageCleared;
            IsTopologyTransitionActive = isTopologyTransitionActive;
        }

        public int LastReducedTickIndex { get; }

        public GameplayUiTopology FinalTopology { get; }

        public bool IsStageCleared { get; }

        public bool IsTopologyTransitionActive { get; }

        public bool Equals(UITickSlice other)
        {
            return LastReducedTickIndex == other.LastReducedTickIndex &&
                   FinalTopology.Equals(other.FinalTopology) &&
                   IsStageCleared == other.IsStageCleared &&
                   IsTopologyTransitionActive == other.IsTopologyTransitionActive;
        }

        public override bool Equals(object obj)
        {
            return obj is UITickSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(LastReducedTickIndex, FinalTopology, IsStageCleared, IsTopologyTransitionActive);
        }
    }

    public readonly struct UIInteractionSlice : IEquatable<UIInteractionSlice>
    {
        public UIInteractionSlice(
            bool isPaused,
            bool canAcceptGameplayCommands,
            bool hasBlockingGameplayPresentation,
            bool isUiGameplayInputBlocked)
        {
            IsPaused = isPaused;
            CanAcceptGameplayCommands = canAcceptGameplayCommands;
            HasBlockingGameplayPresentation = hasBlockingGameplayPresentation;
            IsUiGameplayInputBlocked = isUiGameplayInputBlocked;
        }

        public bool IsPaused { get; }

        public bool CanAcceptGameplayCommands { get; }

        public bool HasBlockingGameplayPresentation { get; }

        public bool IsUiGameplayInputBlocked { get; }

        public bool Equals(UIInteractionSlice other)
        {
            return IsPaused == other.IsPaused &&
                   CanAcceptGameplayCommands == other.CanAcceptGameplayCommands &&
                   HasBlockingGameplayPresentation == other.HasBlockingGameplayPresentation &&
                   IsUiGameplayInputBlocked == other.IsUiGameplayInputBlocked;
        }

        public override bool Equals(object obj)
        {
            return obj is UIInteractionSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsPaused, CanAcceptGameplayCommands, HasBlockingGameplayPresentation, IsUiGameplayInputBlocked);
        }
    }

    public readonly struct UIPlayerActionSlice : IEquatable<UIPlayerActionSlice>
    {
        public UIPlayerActionSlice(
            int playerEntityId,
            int currentHp,
            GameplayUiDirection facing,
            GameplayUiActionKind activeActionKind,
            bool isRecoveryPhase,
            bool canMoveThisTick,
            bool canStartActionThisTick,
            GameplayUiActionResolutionKind lastResolvedOutcome,
            int lastResolvedTickIndex,
            bool tookDamageThisTick,
            int lastDamageAmount,
            int lastDamageTickIndex)
        {
            PlayerEntityId = playerEntityId;
            CurrentHp = currentHp;
            Facing = facing;
            ActiveActionKind = activeActionKind;
            IsRecoveryPhase = isRecoveryPhase;
            CanMoveThisTick = canMoveThisTick;
            CanStartActionThisTick = canStartActionThisTick;
            LastResolvedOutcome = lastResolvedOutcome;
            LastResolvedTickIndex = lastResolvedTickIndex;
            TookDamageThisTick = tookDamageThisTick;
            LastDamageAmount = lastDamageAmount;
            LastDamageTickIndex = lastDamageTickIndex;
        }

        public int PlayerEntityId { get; }

        public int CurrentHp { get; }

        public GameplayUiDirection Facing { get; }

        public GameplayUiActionKind ActiveActionKind { get; }

        public bool IsRecoveryPhase { get; }

        public bool CanMoveThisTick { get; }

        public bool CanStartActionThisTick { get; }

        public GameplayUiActionResolutionKind LastResolvedOutcome { get; }

        public int LastResolvedTickIndex { get; }

        public bool TookDamageThisTick { get; }

        public int LastDamageAmount { get; }

        public int LastDamageTickIndex { get; }

        public bool Equals(UIPlayerActionSlice other)
        {
            return PlayerEntityId == other.PlayerEntityId &&
                   CurrentHp == other.CurrentHp &&
                   Facing == other.Facing &&
                   ActiveActionKind == other.ActiveActionKind &&
                   IsRecoveryPhase == other.IsRecoveryPhase &&
                   CanMoveThisTick == other.CanMoveThisTick &&
                   CanStartActionThisTick == other.CanStartActionThisTick &&
                   LastResolvedOutcome == other.LastResolvedOutcome &&
                   LastResolvedTickIndex == other.LastResolvedTickIndex &&
                   TookDamageThisTick == other.TookDamageThisTick &&
                   LastDamageAmount == other.LastDamageAmount &&
                   LastDamageTickIndex == other.LastDamageTickIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is UIPlayerActionSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(
                PlayerEntityId,
                CurrentHp,
                Facing,
                ActiveActionKind,
                IsRecoveryPhase,
                CanMoveThisTick,
                CanStartActionThisTick,
                LastResolvedOutcome);
            hash = HashCode.Combine(hash, LastResolvedTickIndex, TookDamageThisTick, LastDamageAmount, LastDamageTickIndex);
            return hash;
        }
    }

    public readonly struct UINotificationRecord : IEquatable<UINotificationRecord>
    {
        public UINotificationRecord(
            UITickEventKey key,
            UITickEventKind eventKind,
            int damageAmount,
            int expireAfterTickIndex)
        {
            Key = key;
            EventKind = eventKind;
            DamageAmount = damageAmount;
            ExpireAfterTickIndex = expireAfterTickIndex;
        }

        public UITickEventKey Key { get; }

        public UITickEventKind EventKind { get; }

        public int DamageAmount { get; }

        public int ExpireAfterTickIndex { get; }

        public int TickIndex => Key.TickIndex;

        public int ActorEntityId => Key.ActorEntityId;

        public GameplayUiActionKind ActionKind => Key.ActionKind;

        public int ActionSequence => Key.ActionSequence;

        public GameplayUiActionResolutionKind ResolutionKind => Key.ResolutionKind;

        public bool Equals(UINotificationRecord other)
        {
            return Key.Equals(other.Key) &&
                   EventKind == other.EventKind &&
                   DamageAmount == other.DamageAmount &&
                   ExpireAfterTickIndex == other.ExpireAfterTickIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is UINotificationRecord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, EventKind, DamageAmount, ExpireAfterTickIndex);
        }
    }

    public readonly struct UINotificationLedgerSlice : IEquatable<UINotificationLedgerSlice>
    {
        public static readonly UINotificationLedgerSlice Empty = new(Array.Empty<UINotificationRecord>());

        private readonly ReadOnlyCollection<UINotificationRecord> _activeNotifications;

        public UINotificationLedgerSlice(IEnumerable<UINotificationRecord> activeNotifications)
        {
            if (activeNotifications == null)
            {
                throw new ArgumentNullException(nameof(activeNotifications));
            }

            _activeNotifications = new ReadOnlyCollection<UINotificationRecord>(new List<UINotificationRecord>(activeNotifications));
        }

        public IReadOnlyList<UINotificationRecord> ActiveNotifications => _activeNotifications != null
            ? _activeNotifications
            : Array.Empty<UINotificationRecord>();

        public bool Equals(UINotificationLedgerSlice other)
        {
            var left = ActiveNotifications;
            var right = other.ActiveNotifications;
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (!left[i].Equals(right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is UINotificationLedgerSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = ActiveNotifications.Count;
            for (var i = 0; i < ActiveNotifications.Count; i++)
            {
                hash = HashCode.Combine(hash, ActiveNotifications[i]);
            }

            return hash;
        }
    }

    public readonly struct UIPresentationSnapshot : IEquatable<UIPresentationSnapshot>
    {
        public static readonly UIPresentationSnapshot Empty = new(
            new UITickSlice(0, new GameplayUiTopology(GameplayUiFace.Floor), false, false),
            new UIInteractionSlice(false, false, false, false),
            new UIPlayerActionSlice(
                0,
                0,
                GameplayUiDirection.None,
                GameplayUiActionKind.None,
                false,
                false,
                false,
                GameplayUiActionResolutionKind.None,
                0,
                false,
                0,
                0),
            UINotificationLedgerSlice.Empty);

        public UIPresentationSnapshot(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIPlayerActionSlice player,
            UINotificationLedgerSlice notifications)
        {
            Tick = tick;
            Interaction = interaction;
            Player = player;
            Notifications = notifications;
        }

        public UITickSlice Tick { get; }

        public UIInteractionSlice Interaction { get; }

        public UIPlayerActionSlice Player { get; }

        public UINotificationLedgerSlice Notifications { get; }

        public bool Equals(UIPresentationSnapshot other)
        {
            return Tick.Equals(other.Tick) &&
                   Interaction.Equals(other.Interaction) &&
                   Player.Equals(other.Player) &&
                   Notifications.Equals(other.Notifications);
        }

        public override bool Equals(object obj)
        {
            return obj is UIPresentationSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Tick, Interaction, Player, Notifications);
        }
    }
}
