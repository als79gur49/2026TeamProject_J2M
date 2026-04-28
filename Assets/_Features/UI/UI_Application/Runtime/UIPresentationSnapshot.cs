using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;

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

    public readonly struct UIRecoveryCooldownSlice : IEquatable<UIRecoveryCooldownSlice>
    {
        public UIRecoveryCooldownSlice(
            GameplayUiActionKind actionKind,
            int remainingRecoveryTicks,
            int totalRecoveryTicks)
        {
            ActionKind = actionKind;
            RemainingRecoveryTicks = remainingRecoveryTicks;
            TotalRecoveryTicks = totalRecoveryTicks;
        }

        public GameplayUiActionKind ActionKind { get; }

        public int RemainingRecoveryTicks { get; }

        public int TotalRecoveryTicks { get; }

        public bool Equals(UIRecoveryCooldownSlice other)
        {
            return ActionKind == other.ActionKind &&
                   RemainingRecoveryTicks == other.RemainingRecoveryTicks &&
                   TotalRecoveryTicks == other.TotalRecoveryTicks;
        }

        public override bool Equals(object obj)
        {
            return obj is UIRecoveryCooldownSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ActionKind, RemainingRecoveryTicks, TotalRecoveryTicks);
        }
    }

    public readonly struct UIStageSlice : IEquatable<UIStageSlice>
    {
        public static readonly UIStageSlice Empty = new(StageId.None, string.Empty);

        public UIStageSlice(
            StageId stageId,
            string displayName)
        {
            StageId = stageId;
            DisplayName = displayName ?? string.Empty;
        }

        public StageId StageId { get; }

        public string DisplayName { get; }

        public bool HasDisplayName => !string.IsNullOrWhiteSpace(DisplayName);

        public bool Equals(UIStageSlice other)
        {
            return StageId.Equals(other.StageId) &&
                   string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is UIStageSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StageId, DisplayName);
        }
    }

    public enum UIObjectiveConditionRole
    {
        None = 0,
        PrimaryGoal = 1,
        SecondaryGoal = 2,
        Challenge = 3,
    }

    public readonly struct UIObjectiveConditionSlice : IEquatable<UIObjectiveConditionSlice>
    {
        public UIObjectiveConditionSlice(
            string titleText,
            string progressText,
            bool isSatisfied,
            bool required,
            UIObjectiveConditionRole role,
            int sortOrder)
        {
            TitleText = titleText ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            IsSatisfied = isSatisfied;
            Required = required;
            Role = role;
            SortOrder = sortOrder;
        }

        public string TitleText { get; }

        public string ProgressText { get; }

        public bool IsSatisfied { get; }

        public bool Required { get; }

        public UIObjectiveConditionRole Role { get; }

        public int SortOrder { get; }

        public bool Equals(UIObjectiveConditionSlice other)
        {
            return string.Equals(TitleText, other.TitleText, StringComparison.Ordinal) &&
                   string.Equals(ProgressText, other.ProgressText, StringComparison.Ordinal) &&
                   IsSatisfied == other.IsSatisfied &&
                   Required == other.Required &&
                   Role == other.Role &&
                   SortOrder == other.SortOrder;
        }

        public override bool Equals(object obj)
        {
            return obj is UIObjectiveConditionSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TitleText, ProgressText, IsSatisfied, Required, Role, SortOrder);
        }
    }

    public readonly struct UIObjectiveSlice : IEquatable<UIObjectiveSlice>
    {
        public static readonly UIObjectiveSlice Empty = new(
            false,
            string.Empty,
            string.Empty,
            false,
            false,
            false,
            Array.Empty<UIObjectiveConditionSlice>());

        private readonly ReadOnlyCollection<UIObjectiveConditionSlice> _conditions;

        public UIObjectiveSlice(
            bool hasObjective,
            string title,
            string summary,
            bool goalReached,
            bool allConditionsSatisfied,
            bool isCleared,
            IEnumerable<UIObjectiveConditionSlice> conditions)
        {
            HasObjective = hasObjective;
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            GoalReached = goalReached;
            AllConditionsSatisfied = allConditionsSatisfied;
            IsCleared = isCleared;
            _conditions = new ReadOnlyCollection<UIObjectiveConditionSlice>(
                new List<UIObjectiveConditionSlice>(conditions ?? Array.Empty<UIObjectiveConditionSlice>()));
        }

        public bool HasObjective { get; }

        public string Title { get; }

        public string Summary { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool IsCleared { get; }

        public IReadOnlyList<UIObjectiveConditionSlice> Conditions => _conditions != null
            ? _conditions
            : Array.Empty<UIObjectiveConditionSlice>();

        public bool Equals(UIObjectiveSlice other)
        {
            if (HasObjective != other.HasObjective ||
                !string.Equals(Title, other.Title, StringComparison.Ordinal) ||
                !string.Equals(Summary, other.Summary, StringComparison.Ordinal) ||
                GoalReached != other.GoalReached ||
                AllConditionsSatisfied != other.AllConditionsSatisfied ||
                IsCleared != other.IsCleared)
            {
                return false;
            }

            var left = Conditions;
            var right = other.Conditions;
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
            return obj is UIObjectiveSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(HasObjective, Title, Summary, GoalReached, AllConditionsSatisfied, IsCleared);
            var conditions = Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                hash = HashCode.Combine(hash, conditions[i]);
            }

            return hash;
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
            int lastDamageTickIndex,
            UIRecoveryCooldownSlice? recoveryCooldown = null,
            bool canStartAnyActionThisTick = false,
            bool hasExplicitPushCandidateInCurrentDirection = false,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0)
            : this(
                playerEntityId,
                currentHp,
                currentHp,
                facing,
                activeActionKind,
                isRecoveryPhase,
                canMoveThisTick,
                canStartActionThisTick,
                lastResolvedOutcome,
                lastResolvedTickIndex,
                tookDamageThisTick,
                lastDamageAmount,
                lastDamageTickIndex,
                recoveryCooldown,
                canStartAnyActionThisTick,
                hasExplicitPushCandidateInCurrentDirection,
                hasRemainingChances,
                remainingChances,
                maxChances)
        {
        }

        public UIPlayerActionSlice(
            int playerEntityId,
            int currentHp,
            int maxHp,
            GameplayUiDirection facing,
            GameplayUiActionKind activeActionKind,
            bool isRecoveryPhase,
            bool canMoveThisTick,
            bool canStartActionThisTick,
            GameplayUiActionResolutionKind lastResolvedOutcome,
            int lastResolvedTickIndex,
            bool tookDamageThisTick,
            int lastDamageAmount,
            int lastDamageTickIndex,
            UIRecoveryCooldownSlice? recoveryCooldown = null,
            bool canStartAnyActionThisTick = false,
            bool hasExplicitPushCandidateInCurrentDirection = false,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0)
        {
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
            LastResolvedOutcome = lastResolvedOutcome;
            LastResolvedTickIndex = lastResolvedTickIndex;
            TookDamageThisTick = tookDamageThisTick;
            LastDamageAmount = lastDamageAmount;
            LastDamageTickIndex = lastDamageTickIndex;
            RecoveryCooldown = recoveryCooldown;
        }

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

        public GameplayUiActionResolutionKind LastResolvedOutcome { get; }

        public int LastResolvedTickIndex { get; }

        public bool TookDamageThisTick { get; }

        public int LastDamageAmount { get; }

        public int LastDamageTickIndex { get; }

        public UIRecoveryCooldownSlice? RecoveryCooldown { get; }

        public bool Equals(UIPlayerActionSlice other)
        {
            return PlayerEntityId == other.PlayerEntityId &&
                   CurrentHp == other.CurrentHp &&
                   MaxHp == other.MaxHp &&
                   Facing == other.Facing &&
                   ActiveActionKind == other.ActiveActionKind &&
                   IsRecoveryPhase == other.IsRecoveryPhase &&
                   CanMoveThisTick == other.CanMoveThisTick &&
                   CanStartActionThisTick == other.CanStartActionThisTick &&
                   CanStartAnyActionThisTick == other.CanStartAnyActionThisTick &&
                   HasExplicitPushCandidateInCurrentDirection == other.HasExplicitPushCandidateInCurrentDirection &&
                   HasRemainingChances == other.HasRemainingChances &&
                   RemainingChances == other.RemainingChances &&
                   MaxChances == other.MaxChances &&
                   LastResolvedOutcome == other.LastResolvedOutcome &&
                   LastResolvedTickIndex == other.LastResolvedTickIndex &&
                   TookDamageThisTick == other.TookDamageThisTick &&
                   LastDamageAmount == other.LastDamageAmount &&
                   LastDamageTickIndex == other.LastDamageTickIndex &&
                   RecoveryCooldown.Equals(other.RecoveryCooldown);
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
                MaxHp,
                Facing,
                ActiveActionKind,
                IsRecoveryPhase,
                CanMoveThisTick,
                CanStartActionThisTick);
            hash = HashCode.Combine(hash, CanStartAnyActionThisTick);
            hash = HashCode.Combine(hash, HasExplicitPushCandidateInCurrentDirection, LastResolvedOutcome);
            hash = HashCode.Combine(hash, HasRemainingChances, RemainingChances, MaxChances);
            hash = HashCode.Combine(hash, LastResolvedTickIndex, TookDamageThisTick, LastDamageAmount, LastDamageTickIndex);
            hash = HashCode.Combine(hash, RecoveryCooldown);
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
            UIStageSlice.Empty,
            UIObjectiveSlice.Empty,
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
            : this(
                tick,
                interaction,
                UIStageSlice.Empty,
                UIObjectiveSlice.Empty,
                player,
                notifications)
        {
        }

        public UIPresentationSnapshot(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIStageSlice stage,
            UIPlayerActionSlice player,
            UINotificationLedgerSlice notifications)
            : this(
                tick,
                interaction,
                stage,
                UIObjectiveSlice.Empty,
                player,
                notifications)
        {
        }

        public UIPresentationSnapshot(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIStageSlice stage,
            UIObjectiveSlice objective,
            UIPlayerActionSlice player,
            UINotificationLedgerSlice notifications)
        {
            Tick = tick;
            Interaction = interaction;
            Stage = stage;
            Objective = objective;
            Player = player;
            Notifications = notifications;
        }

        public UITickSlice Tick { get; }

        public UIInteractionSlice Interaction { get; }

        public UIStageSlice Stage { get; }

        public UIObjectiveSlice Objective { get; }

        public UIPlayerActionSlice Player { get; }

        public UINotificationLedgerSlice Notifications { get; }

        public bool Equals(UIPresentationSnapshot other)
        {
            return Tick.Equals(other.Tick) &&
                   Interaction.Equals(other.Interaction) &&
                   Stage.Equals(other.Stage) &&
                   Objective.Equals(other.Objective) &&
                   Player.Equals(other.Player) &&
                   Notifications.Equals(other.Notifications);
        }

        public override bool Equals(object obj)
        {
            return obj is UIPresentationSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Tick, Interaction, Stage, Objective, Player, Notifications);
        }
    }
}
