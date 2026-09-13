using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.HUD;
using Game.Feature.UI.ViewShared;

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

    public readonly struct UIChanceSlice : IEquatable<UIChanceSlice>
    {
        public static readonly UIChanceSlice Empty = new(false, 0, 0);

        public UIChanceSlice(
            bool hasChances,
            int remainingChances,
            int maxChances,
            GameplayChanceAudioPolicy audioPolicy = GameplayChanceAudioPolicy.Default)
        {
            MaxChances = hasChances ? Math.Max(0, maxChances) : 0;
            HasChances = hasChances && MaxChances > 0;
            RemainingChances = HasChances
                ? Math.Max(0, Math.Min(remainingChances, MaxChances))
                : 0;
            AudioPolicy = HasChances
                ? audioPolicy
                : GameplayChanceAudioPolicy.Default;
        }

        public bool HasChances { get; }

        public int RemainingChances { get; }

        public int MaxChances { get; }

        public GameplayChanceAudioPolicy AudioPolicy { get; }

        public bool Equals(UIChanceSlice other)
        {
            return HasChances == other.HasChances &&
                   RemainingChances == other.RemainingChances &&
                   MaxChances == other.MaxChances &&
                   AudioPolicy == other.AudioPolicy;
        }

        public override bool Equals(object obj)
        {
            return obj is UIChanceSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HasChances, RemainingChances, MaxChances, AudioPolicy);
        }
    }

    public readonly struct UITopologySlice : IEquatable<UITopologySlice>
    {
        public static readonly UITopologySlice Empty = FromTopology(
            new GameplayUiTopology(GameplayUiFace.Floor),
            false);

        public UITopologySlice(
            string currentFaceLabel,
            int currentFaceIndex,
            bool isTransitionActive,
            string sourceFaceLabel,
            string destinationFaceLabel,
            float progress01)
        {
            CurrentFaceLabel = currentFaceLabel ?? string.Empty;
            CurrentFaceIndex = currentFaceIndex;
            IsTransitionActive = isTransitionActive;
            SourceFaceLabel = sourceFaceLabel ?? string.Empty;
            DestinationFaceLabel = destinationFaceLabel ?? string.Empty;
            Progress01 = Math.Max(0.0f, Math.Min(1.0f, progress01));
        }

        public string CurrentFaceLabel { get; }

        public int CurrentFaceIndex { get; }

        public bool IsTransitionActive { get; }

        public string SourceFaceLabel { get; }

        public string DestinationFaceLabel { get; }

        public float Progress01 { get; }

        public static UITopologySlice FromTopology(
            GameplayUiTopology topology,
            bool isTransitionActive,
            GameplayUiTopology? sourceTopology = null,
            GameplayUiTopology? destinationTopology = null,
            float progress01 = 1.0f)
        {
            return new UITopologySlice(
                topology.BottomFace.ToString(),
                (int)topology.BottomFace,
                isTransitionActive,
                sourceTopology.HasValue ? sourceTopology.Value.BottomFace.ToString() : string.Empty,
                destinationTopology.HasValue ? destinationTopology.Value.BottomFace.ToString() : string.Empty,
                isTransitionActive ? progress01 : 1.0f);
        }

        public bool Equals(UITopologySlice other)
        {
            return string.Equals(CurrentFaceLabel, other.CurrentFaceLabel, StringComparison.Ordinal) &&
                   CurrentFaceIndex == other.CurrentFaceIndex &&
                   IsTransitionActive == other.IsTransitionActive &&
                   string.Equals(SourceFaceLabel, other.SourceFaceLabel, StringComparison.Ordinal) &&
                   string.Equals(DestinationFaceLabel, other.DestinationFaceLabel, StringComparison.Ordinal) &&
                   Progress01.Equals(other.Progress01);
        }

        public override bool Equals(object obj)
        {
            return obj is UITopologySlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                CurrentFaceLabel,
                CurrentFaceIndex,
                IsTransitionActive,
                SourceFaceLabel,
                DestinationFaceLabel,
                Progress01);
        }
    }

    public readonly struct SurfaceBeltButtonRemainderSnapshot : IEquatable<SurfaceBeltButtonRemainderSnapshot>
    {
        public SurfaceBeltButtonRemainderSnapshot(
            int slotIndex,
            int normalRemaining,
            int moonBlockOnlyRemaining)
        {
            SlotIndex = SurfaceBeltSlotMapping.WrapSlot(slotIndex);
            NormalRemaining = normalRemaining > 0 ? normalRemaining : 0;
            MoonBlockOnlyRemaining = moonBlockOnlyRemaining > 0 ? moonBlockOnlyRemaining : 0;
        }

        public int SlotIndex { get; }

        public int NormalRemaining { get; }

        public int MoonBlockOnlyRemaining { get; }

        public int TotalRemaining => NormalRemaining + MoonBlockOnlyRemaining;

        public bool HasAnyRemaining => TotalRemaining > 0;

        public bool Equals(SurfaceBeltButtonRemainderSnapshot other)
        {
            return SlotIndex == other.SlotIndex &&
                   NormalRemaining == other.NormalRemaining &&
                   MoonBlockOnlyRemaining == other.MoonBlockOnlyRemaining;
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceBeltButtonRemainderSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SlotIndex, NormalRemaining, MoonBlockOnlyRemaining);
        }
    }

    public readonly struct SurfaceBeltSnapshot : IEquatable<SurfaceBeltSnapshot>
    {
        public static readonly SurfaceBeltSnapshot Empty = FromTopology(
            new GameplayUiTopology(GameplayUiFace.Floor),
            false,
            0);

        public SurfaceBeltSnapshot(
            int currentSlotIndex,
            int sourceSlotIndex,
            int destinationSlotIndex,
            SurfaceBeltDirection direction,
            bool isTransitioning,
            int transitionSequenceId,
            IReadOnlyList<SurfaceBeltButtonRemainderSnapshot> buttonRemainders = null)
        {
            CurrentSlotIndex = SurfaceBeltSlotMapping.WrapSlot(currentSlotIndex);
            SourceSlotIndex = SurfaceBeltSlotMapping.WrapSlot(sourceSlotIndex);
            DestinationSlotIndex = SurfaceBeltSlotMapping.WrapSlot(destinationSlotIndex);
            Direction = direction;
            IsTransitioning = isTransitioning;
            TransitionSequenceId = isTransitioning ? Math.Max(0, transitionSequenceId) : 0;
            ButtonRemainders = CopyButtonRemainders(buttonRemainders);
        }

        public int CurrentSlotIndex { get; }

        public int SourceSlotIndex { get; }

        public int DestinationSlotIndex { get; }

        public SurfaceBeltDirection Direction { get; }

        public bool IsTransitioning { get; }

        public int TransitionSequenceId { get; }

        public IReadOnlyList<SurfaceBeltButtonRemainderSnapshot> ButtonRemainders { get; }

        public static SurfaceBeltSnapshot FromTopology(
            GameplayUiTopology topology,
            bool isTransitioning,
            int transitionSequenceId,
            GameplayUiTopology? sourceTopology = null,
            GameplayUiTopology? destinationTopology = null,
            SurfaceBeltDirection direction = SurfaceBeltDirection.None,
            IReadOnlyList<SurfaceBeltButtonRemainderSnapshot> buttonRemainders = null)
        {
            var current = ToSlotIndex(topology);
            var source = sourceTopology.HasValue ? ToSlotIndex(sourceTopology.Value) : current;
            var destination = destinationTopology.HasValue ? ToSlotIndex(destinationTopology.Value) : current;
            return new SurfaceBeltSnapshot(
                current,
                source,
                destination,
                isTransitioning ? direction : SurfaceBeltDirection.None,
                isTransitioning,
                transitionSequenceId,
                buttonRemainders);
        }

        public static int ToSlotIndex(GameplayUiTopology topology)
        {
            return SurfaceBeltSlotMapping.WrapSlot((int)topology.BottomFace);
        }

        public bool Equals(SurfaceBeltSnapshot other)
        {
            return CurrentSlotIndex == other.CurrentSlotIndex &&
                   SourceSlotIndex == other.SourceSlotIndex &&
                   DestinationSlotIndex == other.DestinationSlotIndex &&
                   Direction == other.Direction &&
                   IsTransitioning == other.IsTransitioning &&
                   TransitionSequenceId == other.TransitionSequenceId &&
                   ButtonRemaindersEqual(ButtonRemainders, other.ButtonRemainders);
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceBeltSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                CurrentSlotIndex,
                SourceSlotIndex,
                DestinationSlotIndex,
                Direction,
                IsTransitioning,
                TransitionSequenceId,
                BuildButtonRemaindersHash(ButtonRemainders));
        }

        public static SurfaceBeltButtonRemainderSnapshot[] CreateEmptyButtonRemainders()
        {
            var result = new SurfaceBeltButtonRemainderSnapshot[SurfaceBeltSlotMapping.SurfaceCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new SurfaceBeltButtonRemainderSnapshot(i, 0, 0);
            }

            return result;
        }

        private static SurfaceBeltButtonRemainderSnapshot[] CopyButtonRemainders(
            IReadOnlyList<SurfaceBeltButtonRemainderSnapshot> source)
        {
            var copy = CreateEmptyButtonRemainders();
            if (source == null)
            {
                return copy;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var slot = SurfaceBeltSlotMapping.WrapSlot(item.SlotIndex);
                copy[slot] = new SurfaceBeltButtonRemainderSnapshot(
                    slot,
                    item.NormalRemaining,
                    item.MoonBlockOnlyRemaining);
            }

            return copy;
        }

        private static bool ButtonRemaindersEqual(
            IReadOnlyList<SurfaceBeltButtonRemainderSnapshot> left,
            IReadOnlyList<SurfaceBeltButtonRemainderSnapshot> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
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

        private static int BuildButtonRemaindersHash(
            IReadOnlyList<SurfaceBeltButtonRemainderSnapshot> remainders)
        {
            var hash = 17;
            if (remainders == null)
            {
                return hash;
            }

            for (var i = 0; i < remainders.Count; i++)
            {
                hash = (hash * 397) ^ remainders[i].GetHashCode();
            }

            return hash;
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

    public readonly struct UIStageSlice : IEquatable<UIStageSlice>
    {
        public static readonly UIStageSlice Empty = new(StageId.None, string.Empty);

        public UIStageSlice(
            StageId stageId,
            string displayNameKey)
        {
            StageId = stageId;
            DisplayNameKey = StageDisplayNameKeys.Normalize(displayNameKey);
            DisplayNameDescriptor = !string.IsNullOrWhiteSpace(DisplayNameKey)
                ? StageDisplayNameTextDescriptors.Create(DisplayNameKey)
                : default;
        }

        public StageId StageId { get; }

        public string DisplayNameKey { get; }

        public LocalizedTextDescriptor DisplayNameDescriptor { get; }

        public bool HasDisplayName => !string.IsNullOrWhiteSpace(DisplayNameKey);

        public bool Equals(UIStageSlice other)
        {
            return StageId.Equals(other.StageId) &&
                   string.Equals(DisplayNameKey, other.DisplayNameKey, StringComparison.Ordinal) &&
                   DisplayNameDescriptor.Equals(other.DisplayNameDescriptor);
        }

        public override bool Equals(object obj)
        {
            return obj is UIStageSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StageId, DisplayNameKey, DisplayNameDescriptor);
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
            GameplayObjectivePresentationKind presentationKind,
            string stableGroupKey,
            LocalizedTextDescriptor textDescriptor,
            bool isSatisfied,
            bool required,
            UIObjectiveConditionRole role,
            int completedCount,
            int requiredCount,
            int sortOrder)
            : this(
                string.Empty,
                presentationKind,
                stableGroupKey,
                textDescriptor,
                isSatisfied,
                required,
                role,
                completedCount,
                requiredCount,
                sortOrder)
        {
        }

        public UIObjectiveConditionSlice(
            string stableId,
            GameplayObjectivePresentationKind presentationKind,
            string stableGroupKey,
            LocalizedTextDescriptor textDescriptor,
            bool isSatisfied,
            bool required,
            UIObjectiveConditionRole role,
            int completedCount,
            int requiredCount,
            int sortOrder)
        {
            StableId = stableId ?? string.Empty;
            PresentationKind = presentationKind;
            StableGroupKey = stableGroupKey ?? string.Empty;
            TextDescriptor = textDescriptor;
            IsSatisfied = isSatisfied;
            Required = required;
            Role = role;
            CompletedCount = Math.Max(0, completedCount);
            RequiredCount = Math.Max(0, requiredCount);
            SortOrder = sortOrder;
        }

        public string StableId { get; }

        public GameplayObjectivePresentationKind PresentationKind { get; }

        public string StableGroupKey { get; }

        public LocalizedTextDescriptor TextDescriptor { get; }

        public bool IsSatisfied { get; }

        public bool Required { get; }

        public UIObjectiveConditionRole Role { get; }

        public int CompletedCount { get; }

        public int RequiredCount { get; }

        public int SortOrder { get; }

        public bool Equals(UIObjectiveConditionSlice other)
        {
            return string.Equals(StableId, other.StableId, StringComparison.Ordinal) &&
                   PresentationKind == other.PresentationKind &&
                   string.Equals(StableGroupKey, other.StableGroupKey, StringComparison.Ordinal) &&
                   TextDescriptor.Equals(other.TextDescriptor) &&
                   IsSatisfied == other.IsSatisfied &&
                   Required == other.Required &&
                   Role == other.Role &&
                   CompletedCount == other.CompletedCount &&
                   RequiredCount == other.RequiredCount &&
                   SortOrder == other.SortOrder;
        }

        public override bool Equals(object obj)
        {
            return obj is UIObjectiveConditionSlice other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(
                StableId,
                PresentationKind,
                StableGroupKey,
                TextDescriptor);
            hash = HashCode.Combine(
                hash,
                IsSatisfied,
                Required,
                Role,
                CompletedCount,
                RequiredCount,
                SortOrder);
            return hash;
        }
    }

    public readonly struct UIObjectiveSlice : IEquatable<UIObjectiveSlice>
    {
        public static readonly UIObjectiveSlice Empty = new(
            false,
            string.Empty,
            false,
            false,
            false,
            Array.Empty<UIObjectiveConditionSlice>());

        private readonly ReadOnlyCollection<UIObjectiveConditionSlice> _conditions;

        public UIObjectiveSlice(
            bool hasObjective,
            string objectiveStableId,
            bool goalReached,
            bool allConditionsSatisfied,
            bool isCleared,
            IEnumerable<UIObjectiveConditionSlice> conditions,
            bool? semanticGoalReached = null,
            bool? semanticAllConditionsSatisfied = null,
            bool? semanticIsCleared = null)
        {
            HasObjective = hasObjective;
            ObjectiveStableId = objectiveStableId ?? string.Empty;
            GoalReached = goalReached;
            AllConditionsSatisfied = allConditionsSatisfied;
            IsCleared = isCleared;
            SemanticGoalReached = semanticGoalReached ?? goalReached;
            SemanticAllConditionsSatisfied = semanticAllConditionsSatisfied ?? allConditionsSatisfied;
            SemanticIsCleared = semanticIsCleared ?? isCleared;
            _conditions = new ReadOnlyCollection<UIObjectiveConditionSlice>(
                new List<UIObjectiveConditionSlice>(conditions ?? Array.Empty<UIObjectiveConditionSlice>()));
        }

        public bool HasObjective { get; }

        public string ObjectiveStableId { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool IsCleared { get; }

        public bool SemanticGoalReached { get; }

        public bool SemanticAllConditionsSatisfied { get; }

        public bool SemanticIsCleared { get; }

        public IReadOnlyList<UIObjectiveConditionSlice> Conditions => _conditions != null
            ? _conditions
            : Array.Empty<UIObjectiveConditionSlice>();

        public bool Equals(UIObjectiveSlice other)
        {
            if (HasObjective != other.HasObjective ||
                !string.Equals(ObjectiveStableId, other.ObjectiveStableId, StringComparison.Ordinal) ||
                GoalReached != other.GoalReached ||
                AllConditionsSatisfied != other.AllConditionsSatisfied ||
                IsCleared != other.IsCleared ||
                SemanticGoalReached != other.SemanticGoalReached ||
                SemanticAllConditionsSatisfied != other.SemanticAllConditionsSatisfied ||
                SemanticIsCleared != other.SemanticIsCleared)
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
            var hash = HashCode.Combine(HasObjective, ObjectiveStableId);
            hash = HashCode.Combine(hash, GoalReached, AllConditionsSatisfied, IsCleared);
            hash = HashCode.Combine(
                hash,
                SemanticGoalReached,
                SemanticAllConditionsSatisfied,
                SemanticIsCleared);
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
            GameplayUiActionResolutionKind lastResolvedOutcome,
            int lastResolvedTickIndex,
            bool tookDamageThisTick,
            int lastDamageAmount,
            int lastDamageTickIndex)
        {
            LastResolvedOutcome = lastResolvedOutcome;
            LastResolvedTickIndex = lastResolvedTickIndex;
            TookDamageThisTick = tookDamageThisTick;
            LastDamageAmount = lastDamageAmount;
            LastDamageTickIndex = lastDamageTickIndex;
        }

        public GameplayUiActionResolutionKind LastResolvedOutcome { get; }

        public int LastResolvedTickIndex { get; }

        public bool TookDamageThisTick { get; }

        public int LastDamageAmount { get; }

        public int LastDamageTickIndex { get; }

        public bool Equals(UIPlayerActionSlice other)
        {
            return LastResolvedOutcome == other.LastResolvedOutcome &&
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
            return HashCode.Combine(
                LastResolvedOutcome,
                LastResolvedTickIndex,
                TookDamageThisTick,
                LastDamageAmount,
                LastDamageTickIndex);
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
            UIChanceSlice.Empty,
            UITopologySlice.Empty,
            SurfaceBeltSnapshot.Empty,
            new UIPlayerActionSlice(
                GameplayUiActionResolutionKind.None,
                0,
                false,
                0,
                0),
            UINotificationLedgerSlice.Empty);

        public UIPresentationSnapshot(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIStageSlice stage,
            UIObjectiveSlice objective,
            UIChanceSlice chance,
            UITopologySlice topology,
            UIPlayerActionSlice player,
            UINotificationLedgerSlice notifications)
            : this(
                tick,
                interaction,
                stage,
                objective,
                chance,
                topology,
                SurfaceBeltSnapshot.FromTopology(
                    tick.FinalTopology,
                    tick.IsTopologyTransitionActive,
                    0),
                player,
                notifications)
        {
        }

        public UIPresentationSnapshot(
            UITickSlice tick,
            UIInteractionSlice interaction,
            UIStageSlice stage,
            UIObjectiveSlice objective,
            UIChanceSlice chance,
            UITopologySlice topology,
            SurfaceBeltSnapshot surfaceBelt,
            UIPlayerActionSlice player,
            UINotificationLedgerSlice notifications)
        {
            Tick = tick;
            Interaction = interaction;
            Stage = stage;
            Objective = objective;
            Chance = chance;
            Topology = topology;
            SurfaceBelt = surfaceBelt;
            Player = player;
            Notifications = notifications;
        }

        public UITickSlice Tick { get; }

        public UIInteractionSlice Interaction { get; }

        public UIStageSlice Stage { get; }

        public UIObjectiveSlice Objective { get; }

        public UIChanceSlice Chance { get; }

        public UITopologySlice Topology { get; }

        public SurfaceBeltSnapshot SurfaceBelt { get; }

        public UIPlayerActionSlice Player { get; }

        public UINotificationLedgerSlice Notifications { get; }

        public bool Equals(UIPresentationSnapshot other)
        {
            return Tick.Equals(other.Tick) &&
                   Interaction.Equals(other.Interaction) &&
                   Stage.Equals(other.Stage) &&
                   Objective.Equals(other.Objective) &&
                   Chance.Equals(other.Chance) &&
                   Topology.Equals(other.Topology) &&
                   SurfaceBelt.Equals(other.SurfaceBelt) &&
                   Player.Equals(other.Player) &&
                   Notifications.Equals(other.Notifications);
        }

        public override bool Equals(object obj)
        {
            return obj is UIPresentationSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Tick, Interaction, Stage, Objective);
            hash = HashCode.Combine(hash, Chance, Topology, SurfaceBelt);
            hash = HashCode.Combine(hash, Player);
            hash = HashCode.Combine(hash, Notifications);
            return hash;
        }
    }
}
