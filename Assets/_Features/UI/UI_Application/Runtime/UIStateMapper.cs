using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.UI.Application
{
    public readonly struct UIStateRefreshInput
    {
        public UIStateRefreshInput(
            int tickIndex,
            bool shouldUpdateTickIndex,
            GameplayUiTopology finalTopology,
            bool shouldUpdateFinalTopology,
            bool isStageCleared,
            bool isTopologyTransitionActive,
            bool hasBlockingGameplayPresentation,
            bool isPaused,
            bool canAcceptGameplayCommands,
            bool isUiGameplayInputBlocked,
            int playerEntityId,
            int currentHp,
            GameplayUiDirection facing,
            GameplayUiActionKind activeActionKind,
            bool isRecoveryPhase,
            bool canMoveThisTick,
            bool canStartActionThisTick,
            UIRecoveryCooldownSlice? recoveryCooldown)
        {
            TickIndex = tickIndex;
            ShouldUpdateTickIndex = shouldUpdateTickIndex;
            FinalTopology = finalTopology;
            ShouldUpdateFinalTopology = shouldUpdateFinalTopology;
            IsStageCleared = isStageCleared;
            IsTopologyTransitionActive = isTopologyTransitionActive;
            HasBlockingGameplayPresentation = hasBlockingGameplayPresentation;
            IsPaused = isPaused;
            CanAcceptGameplayCommands = canAcceptGameplayCommands;
            IsUiGameplayInputBlocked = isUiGameplayInputBlocked;
            PlayerEntityId = playerEntityId;
            CurrentHp = currentHp;
            Facing = facing;
            ActiveActionKind = activeActionKind;
            IsRecoveryPhase = isRecoveryPhase;
            CanMoveThisTick = canMoveThisTick;
            CanStartActionThisTick = canStartActionThisTick;
            RecoveryCooldown = recoveryCooldown;
        }

        public int TickIndex { get; }

        public bool ShouldUpdateTickIndex { get; }

        public GameplayUiTopology FinalTopology { get; }

        public bool ShouldUpdateFinalTopology { get; }

        public bool IsStageCleared { get; }

        public bool IsTopologyTransitionActive { get; }

        public bool HasBlockingGameplayPresentation { get; }

        public bool IsPaused { get; }

        public bool CanAcceptGameplayCommands { get; }

        public bool IsUiGameplayInputBlocked { get; }

        public int PlayerEntityId { get; }

        public int CurrentHp { get; }

        public GameplayUiDirection Facing { get; }

        public GameplayUiActionKind ActiveActionKind { get; }

        public bool IsRecoveryPhase { get; }

        public bool CanMoveThisTick { get; }

        public bool CanStartActionThisTick { get; }

        public UIRecoveryCooldownSlice? RecoveryCooldown { get; }
    }

    public readonly struct UIStateReductionResult
    {
        public UIStateReductionResult(
            UIPresentationSnapshot snapshot,
            IReadOnlyList<UITickEvent> appliedEvents)
        {
            Snapshot = snapshot;
            AppliedEvents = appliedEvents ?? Array.Empty<UITickEvent>();
        }

        public UIPresentationSnapshot Snapshot { get; }

        public IReadOnlyList<UITickEvent> AppliedEvents { get; }
    }

    public sealed class UIStateMapper
    {
        private const int NotificationLifetimeTicks = 2;

        public UIStateReductionResult ReduceRefresh(
            UIPresentationSnapshot previous,
            UIStateRefreshInput refreshInput)
        {
            return new UIStateReductionResult(
                ApplyBaseRefresh(previous, refreshInput, preserveTickScopedDamage: true),
                Array.Empty<UITickEvent>());
        }

        public UIStateReductionResult ReduceTick(
            UIPresentationSnapshot previous,
            UIStateRefreshInput refreshInput,
            IReadOnlyList<UITickEvent> orderedEvents)
        {
            if (orderedEvents == null)
            {
                throw new ArgumentNullException(nameof(orderedEvents));
            }

            var next = ApplyBaseRefresh(
                previous,
                refreshInput,
                preserveTickScopedDamage: refreshInput.TickIndex == previous.Tick.LastReducedTickIndex);
            var notifications = PruneExpiredNotifications(
                next.Notifications.ActiveNotifications,
                refreshInput.TickIndex);
            var appliedEvents = new List<UITickEvent>(orderedEvents.Count);

            for (var i = 0; i < orderedEvents.Count; i++)
            {
                var tickEvent = orderedEvents[i];
                if (ContainsNotification(notifications, tickEvent.Key))
                {
                    continue;
                }

                next = ApplyTickEvent(next, tickEvent);
                notifications.Add(CreateNotification(tickEvent));
                appliedEvents.Add(tickEvent);
            }

            next = new UIPresentationSnapshot(
                next.Tick,
                next.Interaction,
                next.Player,
                new UINotificationLedgerSlice(notifications));

            return new UIStateReductionResult(next, appliedEvents);
        }

        private static UIPresentationSnapshot ApplyBaseRefresh(
            UIPresentationSnapshot previous,
            UIStateRefreshInput refreshInput,
            bool preserveTickScopedDamage)
        {
            var tick = new UITickSlice(
                refreshInput.ShouldUpdateTickIndex ? refreshInput.TickIndex : previous.Tick.LastReducedTickIndex,
                refreshInput.ShouldUpdateFinalTopology ? refreshInput.FinalTopology : previous.Tick.FinalTopology,
                refreshInput.IsStageCleared,
                refreshInput.IsTopologyTransitionActive);
            var interaction = new UIInteractionSlice(
                refreshInput.IsPaused,
                refreshInput.CanAcceptGameplayCommands,
                refreshInput.HasBlockingGameplayPresentation,
                refreshInput.IsUiGameplayInputBlocked);
            var player = new UIPlayerActionSlice(
                refreshInput.PlayerEntityId,
                refreshInput.CurrentHp,
                refreshInput.Facing,
                refreshInput.ActiveActionKind,
                refreshInput.IsRecoveryPhase,
                refreshInput.CanMoveThisTick,
                refreshInput.CanStartActionThisTick,
                previous.Player.LastResolvedOutcome,
                previous.Player.LastResolvedTickIndex,
                preserveTickScopedDamage && previous.Player.TookDamageThisTick,
                previous.Player.LastDamageAmount,
                previous.Player.LastDamageTickIndex,
                refreshInput.RecoveryCooldown);

            return new UIPresentationSnapshot(
                tick,
                interaction,
                player,
                previous.Notifications);
        }

        private static UIPresentationSnapshot ApplyTickEvent(
            UIPresentationSnapshot snapshot,
            UITickEvent tickEvent)
        {
            switch (tickEvent.EventKind)
            {
                case UITickEventKind.PlayerActionResolved:
                    return new UIPresentationSnapshot(
                        snapshot.Tick,
                        snapshot.Interaction,
                        new UIPlayerActionSlice(
                            snapshot.Player.PlayerEntityId,
                            snapshot.Player.CurrentHp,
                            snapshot.Player.Facing,
                            snapshot.Player.ActiveActionKind,
                            snapshot.Player.IsRecoveryPhase,
                            snapshot.Player.CanMoveThisTick,
                            snapshot.Player.CanStartActionThisTick,
                            tickEvent.ResolutionKind,
                            tickEvent.TickIndex,
                            snapshot.Player.TookDamageThisTick,
                            snapshot.Player.LastDamageAmount,
                            snapshot.Player.LastDamageTickIndex,
                            snapshot.Player.RecoveryCooldown),
                        snapshot.Notifications);

                case UITickEventKind.PlayerDamaged:
                    return new UIPresentationSnapshot(
                        snapshot.Tick,
                        snapshot.Interaction,
                        new UIPlayerActionSlice(
                            snapshot.Player.PlayerEntityId,
                            snapshot.Player.CurrentHp,
                            snapshot.Player.Facing,
                            snapshot.Player.ActiveActionKind,
                            snapshot.Player.IsRecoveryPhase,
                            snapshot.Player.CanMoveThisTick,
                            snapshot.Player.CanStartActionThisTick,
                            snapshot.Player.LastResolvedOutcome,
                            snapshot.Player.LastResolvedTickIndex,
                            true,
                            tickEvent.DamageAmount,
                            tickEvent.TickIndex,
                            snapshot.Player.RecoveryCooldown),
                        snapshot.Notifications);

                case UITickEventKind.StageCleared:
                    return new UIPresentationSnapshot(
                        new UITickSlice(
                            snapshot.Tick.LastReducedTickIndex,
                            snapshot.Tick.FinalTopology,
                            isStageCleared: true,
                            snapshot.Tick.IsTopologyTransitionActive),
                        snapshot.Interaction,
                        snapshot.Player,
                        snapshot.Notifications);

                default:
                    return snapshot;
            }
        }

        private static List<UINotificationRecord> PruneExpiredNotifications(
            IReadOnlyList<UINotificationRecord> notifications,
            int currentTickIndex)
        {
            var retained = new List<UINotificationRecord>(notifications.Count);
            for (var i = 0; i < notifications.Count; i++)
            {
                if (notifications[i].ExpireAfterTickIndex >= currentTickIndex)
                {
                    retained.Add(notifications[i]);
                }
            }

            return retained;
        }

        private static bool ContainsNotification(
            IReadOnlyList<UINotificationRecord> notifications,
            UITickEventKey key)
        {
            for (var i = 0; i < notifications.Count; i++)
            {
                if (notifications[i].Key.Equals(key))
                {
                    return true;
                }
            }

            return false;
        }

        private static UINotificationRecord CreateNotification(UITickEvent tickEvent)
        {
            return new UINotificationRecord(
                tickEvent.Key,
                tickEvent.EventKind,
                tickEvent.DamageAmount,
                tickEvent.TickIndex + NotificationLifetimeTicks);
        }
    }
}
