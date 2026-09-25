using System;
using System.Collections.Generic;

namespace Game.Feature.UI.HUD
{
    public sealed class HealthPanelViewModel
    {
        public event Action Changed;
        public bool HasHealth { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        public void SetHealth(bool hasHealth, int hp, int maxHp)
        {
            if (HasHealth == hasHealth && Hp == hp && MaxHp == maxHp) return;
            HasHealth = hasHealth;
            Hp = hp;
            MaxHp = maxHp;
            Changed?.Invoke();
        }
    }

    public enum ChanceChangeKind
    {
        None = 0,
        Gained = 1,
        Lost = 2,
        LastChanceEntered = 3,
    }

    public enum ChanceChangeAudioCuePolicy
    {
        Default = 0,
        Suppress = 1,
    }

    public readonly struct ChanceChangeAnimationHint : IEquatable<ChanceChangeAnimationHint>
    {
        public static readonly ChanceChangeAnimationHint None = new(
            ChanceChangeKind.None,
            -1,
            Array.Empty<int>(),
            0);

        public ChanceChangeAnimationHint(
            ChanceChangeKind kind,
            int primarySlotIndex,
            IReadOnlyList<int> changedSlotIndices,
            int sequenceId,
            ChanceChangeAudioCuePolicy audioCuePolicy = ChanceChangeAudioCuePolicy.Default)
        {
            Kind = kind;
            PrimarySlotIndex = primarySlotIndex;
            ChangedSlotIndices = CopyIndices(changedSlotIndices);
            SequenceId = sequenceId;
            AudioCuePolicy = audioCuePolicy;
        }

        public ChanceChangeKind Kind { get; }

        public int PrimarySlotIndex { get; }

        public IReadOnlyList<int> ChangedSlotIndices { get; }

        public int SequenceId { get; }

        public ChanceChangeAudioCuePolicy AudioCuePolicy { get; }

        public bool Equals(ChanceChangeAnimationHint other)
        {
            if (Kind != other.Kind ||
                PrimarySlotIndex != other.PrimarySlotIndex ||
                SequenceId != other.SequenceId ||
                AudioCuePolicy != other.AudioCuePolicy)
            {
                return false;
            }

            var left = ChangedSlotIndices ?? Array.Empty<int>();
            var right = other.ChangedSlotIndices ?? Array.Empty<int>();
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is ChanceChangeAnimationHint other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Kind, PrimarySlotIndex, SequenceId, AudioCuePolicy);
            var indices = ChangedSlotIndices ?? Array.Empty<int>();
            for (var i = 0; i < indices.Count; i++)
            {
                hash = HashCode.Combine(hash, indices[i]);
            }

            return hash;
        }

        private static int[] CopyIndices(IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<int>();
            }

            var copy = new int[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }

    public readonly struct ChanceSlotViewModel : IEquatable<ChanceSlotViewModel>
    {
        public ChanceSlotViewModel(
            int index,
            bool isFilled,
            bool isLastChanceSlot)
        {
            Index = index;
            IsFilled = isFilled;
            IsLastChanceSlot = isLastChanceSlot;
        }

        public int Index { get; }

        public bool IsFilled { get; }

        public bool IsLastChanceSlot { get; }

        public bool Equals(ChanceSlotViewModel other)
        {
            return Index == other.Index &&
                   IsFilled == other.IsFilled &&
                   IsLastChanceSlot == other.IsLastChanceSlot;
        }

        public override bool Equals(object obj)
        {
            return obj is ChanceSlotViewModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, IsFilled, IsLastChanceSlot);
        }
    }

    public sealed class ChancePanelViewModel
    {
        private static readonly ChanceSlotViewModel[] EmptySlots = Array.Empty<ChanceSlotViewModel>();

        public event Action Changed;

        public bool HasChances { get; private set; }

        public int RemainingChances { get; private set; }

        public int MaxChances { get; private set; }

        public bool IsLastChance { get; private set; }

        public bool IsInitialBind { get; private set; } = true;

        public IReadOnlyList<ChanceSlotViewModel> Slots { get; private set; } = EmptySlots;

        public ChanceChangeAnimationHint AnimationHint { get; private set; } = ChanceChangeAnimationHint.None;

        public void SetState(
            bool hasChances,
            int remainingChances,
            int maxChances,
            bool isInitialBind,
            ChanceChangeAnimationHint animationHint)
        {
            var resolvedMax = hasChances ? Math.Max(0, maxChances) : 0;
            var resolvedHasChances = hasChances && resolvedMax > 0;
            var resolvedRemaining = resolvedHasChances
                ? Math.Max(0, Math.Min(remainingChances, resolvedMax))
                : 0;
            var nextSlots = BuildSlots(resolvedHasChances, resolvedRemaining, resolvedMax);
            var nextIsLastChance = resolvedHasChances && resolvedRemaining == 1 && resolvedMax > 1;
            var nextHint = isInitialBind ? ChanceChangeAnimationHint.None : animationHint;

            if (HasChances == resolvedHasChances &&
                RemainingChances == resolvedRemaining &&
                MaxChances == resolvedMax &&
                IsLastChance == nextIsLastChance &&
                IsInitialBind == isInitialBind &&
                AnimationHint.Equals(nextHint) &&
                SlotsEqual(Slots, nextSlots))
            {
                return;
            }

            HasChances = resolvedHasChances;
            RemainingChances = resolvedRemaining;
            MaxChances = resolvedMax;
            IsLastChance = nextIsLastChance;
            IsInitialBind = isInitialBind;
            Slots = nextSlots;
            AnimationHint = nextHint;
            Changed?.Invoke();
        }

        private static ChanceSlotViewModel[] BuildSlots(
            bool hasChances,
            int remainingChances,
            int maxChances)
        {
            if (!hasChances || maxChances <= 0)
            {
                return EmptySlots;
            }

            var slots = new ChanceSlotViewModel[maxChances];
            for (var i = 0; i < maxChances; i++)
            {
                slots[i] = new ChanceSlotViewModel(
                    i,
                    i < remainingChances,
                    remainingChances == 1 && i == 0 && maxChances > 1);
            }

            return slots;
        }

        private static bool SlotsEqual(
            IReadOnlyList<ChanceSlotViewModel> left,
            IReadOnlyList<ChanceSlotViewModel> right)
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
    }
}
