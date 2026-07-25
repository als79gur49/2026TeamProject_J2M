using System;
using System.Collections.Generic;
using System.Text;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;
using Game.Feature.Stages;
using Game.Feature.UI.HUD;

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
            UIRecoveryCooldownSlice? recoveryCooldown,
            bool canStartAnyActionThisTick = false,
            bool hasExplicitPushCandidateInCurrentDirection = false,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0,
            StageId stageId = default,
            string stageDisplayNameKey = null,
            GameplayObjectiveReadModel objective = default,
            GameplayTopologyPresentationSlice? topologyPresentation = null,
            GameplayChanceAudioPolicy chanceAudioPolicy = GameplayChanceAudioPolicy.Default,
            IReadOnlyList<UISurfaceButtonRemainderInput> surfaceButtonRemainders = null)
            : this(
                tickIndex,
                shouldUpdateTickIndex,
                finalTopology,
                shouldUpdateFinalTopology,
                isStageCleared,
                isTopologyTransitionActive,
                hasBlockingGameplayPresentation,
                isPaused,
                canAcceptGameplayCommands,
                isUiGameplayInputBlocked,
                playerEntityId,
                currentHp,
                currentHp,
                facing,
                activeActionKind,
                isRecoveryPhase,
                canMoveThisTick,
                canStartActionThisTick,
                recoveryCooldown,
                canStartAnyActionThisTick,
                hasExplicitPushCandidateInCurrentDirection,
                hasRemainingChances,
                remainingChances,
                maxChances,
                stageId,
                stageDisplayNameKey,
                objective,
                topologyPresentation,
                chanceAudioPolicy,
                surfaceButtonRemainders)
        {
        }

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
            int maxHp,
            GameplayUiDirection facing,
            GameplayUiActionKind activeActionKind,
            bool isRecoveryPhase,
            bool canMoveThisTick,
            bool canStartActionThisTick,
            UIRecoveryCooldownSlice? recoveryCooldown,
            bool canStartAnyActionThisTick = false,
            bool hasExplicitPushCandidateInCurrentDirection = false,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0,
            StageId stageId = default,
            string stageDisplayNameKey = null,
            GameplayObjectiveReadModel objective = default,
            GameplayTopologyPresentationSlice? topologyPresentation = null,
            GameplayChanceAudioPolicy chanceAudioPolicy = GameplayChanceAudioPolicy.Default,
            IReadOnlyList<UISurfaceButtonRemainderInput> surfaceButtonRemainders = null)
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
            MaxHp = maxHp > 0 ? maxHp : currentHp;
            Facing = facing;
            ActiveActionKind = activeActionKind;
            IsRecoveryPhase = isRecoveryPhase;
            CanMoveThisTick = canMoveThisTick;
            CanStartActionThisTick = canStartActionThisTick;
            CanStartAnyActionThisTick = canStartAnyActionThisTick || canStartActionThisTick;
            HasExplicitPushCandidateInCurrentDirection = hasExplicitPushCandidateInCurrentDirection;
            HasRemainingChances = hasRemainingChances;
            RemainingChances = remainingChances;
            MaxChances = maxChances > 0
                ? maxChances
                : (hasRemainingChances ? remainingChances : 0);
            ChanceAudioPolicy = hasRemainingChances
                ? chanceAudioPolicy
                : GameplayChanceAudioPolicy.Default;
            StageId = stageId;
            StageDisplayNameKey = StageDisplayNameKeys.Normalize(stageDisplayNameKey);
            Objective = objective;
            TopologyPresentation = topologyPresentation;
            RecoveryCooldown = recoveryCooldown;
            SurfaceButtonRemainders = CopySurfaceButtonRemainders(surfaceButtonRemainders);
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

        public int MaxHp { get; }

        public GameplayUiDirection Facing { get; }

        public GameplayUiActionKind ActiveActionKind { get; }

        public bool IsRecoveryPhase { get; }

        public bool CanMoveThisTick { get; }

        public bool CanStartActionThisTick { get; }

        public bool CanStartAnyActionThisTick { get; }

        public bool HasExplicitPushCandidateInCurrentDirection { get; }

        public bool HasRemainingChances { get; }

        public int RemainingChances { get; }

        public int MaxChances { get; }

        public GameplayChanceAudioPolicy ChanceAudioPolicy { get; }

        public StageId StageId { get; }

        public string StageDisplayNameKey { get; }

        public GameplayObjectiveReadModel Objective { get; }

        public GameplayTopologyPresentationSlice? TopologyPresentation { get; }

        public UIRecoveryCooldownSlice? RecoveryCooldown { get; }

        public IReadOnlyList<UISurfaceButtonRemainderInput> SurfaceButtonRemainders { get; }

        private static UISurfaceButtonRemainderInput[] CopySurfaceButtonRemainders(
            IReadOnlyList<UISurfaceButtonRemainderInput> source)
        {
            var copy = CreateEmptySurfaceButtonRemainders();
            if (source == null)
            {
                return copy;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var slot = SurfaceBeltSlotMapping.WrapSlot((int)item.Face);
                copy[slot] = item;
            }

            return copy;
        }

        private static UISurfaceButtonRemainderInput[] CreateEmptySurfaceButtonRemainders()
        {
            return new[]
            {
                new UISurfaceButtonRemainderInput(GameplayUiFace.Floor, 0, 0),
                new UISurfaceButtonRemainderInput(GameplayUiFace.Front, 0, 0),
                new UISurfaceButtonRemainderInput(GameplayUiFace.Ceiling, 0, 0),
                new UISurfaceButtonRemainderInput(GameplayUiFace.Back, 0, 0),
            };
        }
    }

    public readonly struct UISurfaceButtonRemainderInput
    {
        public UISurfaceButtonRemainderInput(
            GameplayUiFace face,
            int normalRemaining,
            int moonBlockOnlyRemaining)
        {
            Face = face;
            NormalRemaining = normalRemaining > 0 ? normalRemaining : 0;
            MoonBlockOnlyRemaining = moonBlockOnlyRemaining > 0 ? moonBlockOnlyRemaining : 0;
        }

        public GameplayUiFace Face { get; }

        public int NormalRemaining { get; }

        public int MoonBlockOnlyRemaining { get; }
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
                next.Stage,
                next.Objective,
                next.Chance,
                next.Topology,
                next.SurfaceBelt,
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
            var stage = new UIStageSlice(
                refreshInput.StageId,
                refreshInput.StageDisplayNameKey);
            var objective = MapObjective(refreshInput.Objective, refreshInput.StageId);
            var topology = MapTopology(tick, refreshInput);
            var surfaceBelt = MapSurfaceBelt(tick, refreshInput);
            var player = new UIPlayerActionSlice(
                refreshInput.PlayerEntityId,
                refreshInput.CurrentHp,
                refreshInput.MaxHp,
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
                refreshInput.RecoveryCooldown,
                refreshInput.CanStartAnyActionThisTick,
                refreshInput.HasExplicitPushCandidateInCurrentDirection,
                refreshInput.HasRemainingChances,
                refreshInput.RemainingChances,
                refreshInput.MaxChances);

            return new UIPresentationSnapshot(
                tick,
                interaction,
                stage,
                objective,
                new UIChanceSlice(
                    refreshInput.HasRemainingChances,
                    refreshInput.RemainingChances,
                    refreshInput.MaxChances,
                    refreshInput.ChanceAudioPolicy),
                topology,
                surfaceBelt,
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
                        snapshot.Stage,
                        snapshot.Objective,
                        snapshot.Chance,
                        snapshot.Topology,
                        snapshot.SurfaceBelt,
                        new UIPlayerActionSlice(
                            snapshot.Player.PlayerEntityId,
                            snapshot.Player.CurrentHp,
                            snapshot.Player.MaxHp,
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
                            snapshot.Player.RecoveryCooldown,
                            snapshot.Player.CanStartAnyActionThisTick,
                            snapshot.Player.HasExplicitPushCandidateInCurrentDirection,
                            snapshot.Player.HasRemainingChances,
                            snapshot.Player.RemainingChances,
                            snapshot.Player.MaxChances),
                        snapshot.Notifications);

                case UITickEventKind.PlayerDamaged:
                    return new UIPresentationSnapshot(
                        snapshot.Tick,
                        snapshot.Interaction,
                        snapshot.Stage,
                        snapshot.Objective,
                        snapshot.Chance,
                        snapshot.Topology,
                        snapshot.SurfaceBelt,
                        new UIPlayerActionSlice(
                            snapshot.Player.PlayerEntityId,
                            snapshot.Player.CurrentHp,
                            snapshot.Player.MaxHp,
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
                            snapshot.Player.RecoveryCooldown,
                            snapshot.Player.CanStartAnyActionThisTick,
                            snapshot.Player.HasExplicitPushCandidateInCurrentDirection,
                            snapshot.Player.HasRemainingChances,
                            snapshot.Player.RemainingChances,
                            snapshot.Player.MaxChances),
                        snapshot.Notifications);

                case UITickEventKind.StageCleared:
                    return new UIPresentationSnapshot(
                        new UITickSlice(
                            snapshot.Tick.LastReducedTickIndex,
                            snapshot.Tick.FinalTopology,
                            isStageCleared: true,
                            snapshot.Tick.IsTopologyTransitionActive),
                        snapshot.Interaction,
                        snapshot.Stage,
                        snapshot.Objective,
                        snapshot.Chance,
                        UITopologySlice.FromTopology(
                            snapshot.Tick.FinalTopology,
                            snapshot.Tick.IsTopologyTransitionActive),
                        SurfaceBeltSnapshot.FromTopology(
                            snapshot.Tick.FinalTopology,
                            snapshot.Tick.IsTopologyTransitionActive,
                            snapshot.SurfaceBelt.TransitionSequenceId,
                            buttonRemainders: snapshot.SurfaceBelt.ButtonRemainders),
                        snapshot.Player,
                        snapshot.Notifications);

                default:
                    return snapshot;
            }
        }

        private static UIObjectiveSlice MapObjective(
            GameplayObjectiveReadModel objective,
            StageId stageId)
        {
            if (!objective.HasObjective)
            {
                return UIObjectiveSlice.Empty;
            }

            var sourceConditions = objective.Conditions ?? Array.Empty<GameplayObjectiveConditionReadModel>();
            var conditions = new UIObjectiveConditionSlice[sourceConditions.Count];
            for (var i = 0; i < sourceConditions.Count; i++)
            {
                var condition = sourceConditions[i];
                conditions[i] = new UIObjectiveConditionSlice(
                    condition.StableId,
                    condition.TitleText,
                    condition.ProgressText,
                    condition.IsSatisfied,
                    condition.Required,
                    MapObjectiveRole(condition.Role),
                    condition.SortOrder);
            }

            return new UIObjectiveSlice(
                objective.HasObjective,
                BuildObjectiveStableId(objective, stageId),
                objective.ObjectiveTitle,
                objective.ObjectiveSummary,
                objective.GoalReached,
                objective.AllConditionsSatisfied,
                objective.IsCleared,
                conditions,
                objective.SemanticGoalReached,
                objective.SemanticAllConditionsSatisfied,
                objective.SemanticIsCleared);
        }

        private static string BuildObjectiveStableId(
            GameplayObjectiveReadModel objective,
            StageId stageId)
        {
            var builder = new StringBuilder();
            builder.Append(stageId.IsValid ? stageId.Value : string.Empty);
            builder.Append('|');
            builder.Append(objective.ObjectiveTitle ?? string.Empty);
            builder.Append('|');
            builder.Append(objective.ObjectiveSummary ?? string.Empty);

            var conditions = objective.Conditions ?? Array.Empty<GameplayObjectiveConditionReadModel>();
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                builder.Append('|');
                builder.Append(condition.SortOrder);
                builder.Append(':');
                builder.Append(condition.Role);
                builder.Append(':');
                builder.Append(condition.Required ? "required" : "optional");
                builder.Append(':');
                builder.Append(condition.StableId ?? string.Empty);
                builder.Append(':');
                builder.Append(condition.TitleText ?? string.Empty);
            }

            return builder.ToString();
        }

        private static UITopologySlice MapTopology(
            UITickSlice tick,
            UIStateRefreshInput refreshInput)
        {
            if (refreshInput.TopologyPresentation.HasValue)
            {
                var topology = refreshInput.TopologyPresentation.Value;
                return UITopologySlice.FromTopology(
                    tick.FinalTopology,
                    refreshInput.IsTopologyTransitionActive,
                    topology.SourceTopology,
                    topology.DestinationTopology,
                    progress01: 0.0f);
            }

            return UITopologySlice.FromTopology(
                tick.FinalTopology,
                refreshInput.IsTopologyTransitionActive,
                progress01: refreshInput.IsTopologyTransitionActive ? 0.0f : 1.0f);
        }

        private static SurfaceBeltSnapshot MapSurfaceBelt(
            UITickSlice tick,
            UIStateRefreshInput refreshInput)
        {
            if (refreshInput.TopologyPresentation.HasValue)
            {
                var topology = refreshInput.TopologyPresentation.Value;
                var direction = MapSurfaceBeltDirection(topology.RotationKind);
                return SurfaceBeltSnapshot.FromTopology(
                    tick.FinalTopology,
                    refreshInput.IsTopologyTransitionActive,
                    BuildSurfaceBeltTransitionSequenceId(
                        refreshInput.TickIndex,
                        topology.SourceTopology,
                        topology.DestinationTopology,
                        direction),
                    topology.SourceTopology,
                    topology.DestinationTopology,
                    direction,
                    BuildSurfaceBeltButtonRemainders(refreshInput.SurfaceButtonRemainders));
            }

            return SurfaceBeltSnapshot.FromTopology(
                tick.FinalTopology,
                refreshInput.IsTopologyTransitionActive,
                0,
                buttonRemainders: BuildSurfaceBeltButtonRemainders(refreshInput.SurfaceButtonRemainders));
        }

        private static SurfaceBeltButtonRemainderSnapshot[] BuildSurfaceBeltButtonRemainders(
            IReadOnlyList<UISurfaceButtonRemainderInput> source)
        {
            var remainders = SurfaceBeltSnapshot.CreateEmptyButtonRemainders();
            if (source == null)
            {
                return remainders;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var slot = SurfaceBeltSlotMapping.WrapSlot((int)item.Face);
                remainders[slot] = new SurfaceBeltButtonRemainderSnapshot(
                    slot,
                    item.NormalRemaining,
                    item.MoonBlockOnlyRemaining);
            }

            return remainders;
        }

        private static SurfaceBeltDirection MapSurfaceBeltDirection(GameplayUiRotationKind rotationKind)
        {
            switch (rotationKind)
            {
                case GameplayUiRotationKind.Forward:
                    return SurfaceBeltDirection.Forward;

                case GameplayUiRotationKind.Backward:
                    return SurfaceBeltDirection.Backward;

                case GameplayUiRotationKind.None:
                default:
                    return SurfaceBeltDirection.None;
            }
        }

        private static int BuildSurfaceBeltTransitionSequenceId(
            int tickIndex,
            GameplayUiTopology sourceTopology,
            GameplayUiTopology destinationTopology,
            SurfaceBeltDirection direction)
        {
            if (direction == SurfaceBeltDirection.None)
            {
                return 0;
            }

            var source = SurfaceBeltSnapshot.ToSlotIndex(sourceTopology);
            var destination = SurfaceBeltSnapshot.ToSlotIndex(destinationTopology);
            return ((Math.Max(0, tickIndex) + 1) * 1000) +
                   (source * 100) +
                   (destination * 10) +
                   (int)direction;
        }

        private static UIObjectiveConditionRole MapObjectiveRole(GameplayObjectiveConditionRole role)
        {
            switch (role)
            {
                case GameplayObjectiveConditionRole.PrimaryGoal:
                    return UIObjectiveConditionRole.PrimaryGoal;

                case GameplayObjectiveConditionRole.SecondaryGoal:
                    return UIObjectiveConditionRole.SecondaryGoal;

                case GameplayObjectiveConditionRole.Challenge:
                    return UIObjectiveConditionRole.Challenge;

                case GameplayObjectiveConditionRole.None:
                default:
                    return UIObjectiveConditionRole.None;
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
