using System;

namespace Game.Feature.UI.HUD
{
    public readonly struct TopologyBeltAnimationHint : IEquatable<TopologyBeltAnimationHint>
    {
        public static readonly TopologyBeltAnimationHint None = new(false, -1, 0);

        public TopologyBeltAnimationHint(
            bool pulseDestination,
            int destinationFaceIndex,
            int sequenceId)
        {
            PulseDestination = pulseDestination;
            DestinationFaceIndex = destinationFaceIndex;
            SequenceId = sequenceId;
        }

        public bool PulseDestination { get; }

        public int DestinationFaceIndex { get; }

        public int SequenceId { get; }

        public bool Equals(TopologyBeltAnimationHint other)
        {
            return PulseDestination == other.PulseDestination &&
                   DestinationFaceIndex == other.DestinationFaceIndex &&
                   SequenceId == other.SequenceId;
        }

        public override bool Equals(object obj)
        {
            return obj is TopologyBeltAnimationHint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PulseDestination, DestinationFaceIndex, SequenceId);
        }
    }

    public readonly struct FaceChipViewModel : IEquatable<FaceChipViewModel>
    {
        public FaceChipViewModel(
            int index,
            string label,
            bool isActive,
            bool isTransitionEndpoint)
        {
            Index = index;
            Label = label ?? string.Empty;
            IsActive = isActive;
            IsTransitionEndpoint = isTransitionEndpoint;
        }

        public int Index { get; }

        public string Label { get; }

        public bool IsActive { get; }

        public bool IsTransitionEndpoint { get; }

        public bool Equals(FaceChipViewModel other)
        {
            return Index == other.Index &&
                   string.Equals(Label, other.Label, StringComparison.Ordinal) &&
                   IsActive == other.IsActive &&
                   IsTransitionEndpoint == other.IsTransitionEndpoint;
        }

        public override bool Equals(object obj)
        {
            return obj is FaceChipViewModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Label, IsActive, IsTransitionEndpoint);
        }
    }

    public sealed class TopologyBeltViewModel
    {
        public static readonly string[] CanonicalFaceLabels =
        {
            "Floor",
            "Front",
            "Ceiling",
            "Back",
        };

        public event Action Changed;

        public string CurrentFaceLabel { get; private set; } = "Floor";

        public int CurrentFaceIndex { get; private set; }

        public bool IsTransitionActive { get; private set; }

        public string TransitionLabel { get; private set; } = string.Empty;

        public float Progress01 { get; private set; } = 1.0f;

        public FaceChipViewModel[] Chips { get; private set; } = BuildChips(0, false, string.Empty, string.Empty);

        public TopologyBeltAnimationHint AnimationHint { get; private set; } = TopologyBeltAnimationHint.None;

        public void SetState(
            string currentFaceLabel,
            int currentFaceIndex,
            bool isTransitionActive,
            string sourceFaceLabel,
            string destinationFaceLabel,
            float progress01,
            TopologyBeltAnimationHint animationHint)
        {
            var nextFaceIndex = NormalizeFaceIndex(currentFaceIndex);
            var nextFaceLabel = string.IsNullOrWhiteSpace(currentFaceLabel)
                ? CanonicalFaceLabels[nextFaceIndex]
                : currentFaceLabel;
            var nextTransitionLabel = BuildTransitionLabel(
                isTransitionActive,
                sourceFaceLabel,
                destinationFaceLabel);
            var nextProgress = Math.Max(0.0f, Math.Min(1.0f, progress01));
            var nextChips = BuildChips(
                nextFaceIndex,
                isTransitionActive,
                sourceFaceLabel,
                destinationFaceLabel);

            if (string.Equals(CurrentFaceLabel, nextFaceLabel, StringComparison.Ordinal) &&
                CurrentFaceIndex == nextFaceIndex &&
                IsTransitionActive == isTransitionActive &&
                string.Equals(TransitionLabel, nextTransitionLabel, StringComparison.Ordinal) &&
                Progress01.Equals(nextProgress) &&
                AnimationHint.Equals(animationHint) &&
                ChipsEqual(Chips, nextChips))
            {
                return;
            }

            CurrentFaceLabel = nextFaceLabel;
            CurrentFaceIndex = nextFaceIndex;
            IsTransitionActive = isTransitionActive;
            TransitionLabel = nextTransitionLabel;
            Progress01 = nextProgress;
            Chips = nextChips;
            AnimationHint = animationHint;
            Changed?.Invoke();
        }

        private static string BuildTransitionLabel(
            bool isTransitionActive,
            string sourceFaceLabel,
            string destinationFaceLabel)
        {
            if (!isTransitionActive)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(sourceFaceLabel) &&
                !string.IsNullOrWhiteSpace(destinationFaceLabel))
            {
                return $"{sourceFaceLabel} -> {destinationFaceLabel}";
            }

            return "Surface shifting...";
        }

        private static FaceChipViewModel[] BuildChips(
            int currentFaceIndex,
            bool isTransitionActive,
            string sourceFaceLabel,
            string destinationFaceLabel)
        {
            var chips = new FaceChipViewModel[CanonicalFaceLabels.Length];
            for (var i = 0; i < chips.Length; i++)
            {
                var label = CanonicalFaceLabels[i];
                var isEndpoint = isTransitionActive &&
                    (string.Equals(sourceFaceLabel, label, StringComparison.Ordinal) ||
                     string.Equals(destinationFaceLabel, label, StringComparison.Ordinal));
                chips[i] = new FaceChipViewModel(
                    i,
                    label,
                    i == currentFaceIndex,
                    isEndpoint);
            }

            return chips;
        }

        private static int NormalizeFaceIndex(int index)
        {
            if (index < 0)
            {
                return 0;
            }

            return index >= CanonicalFaceLabels.Length
                ? CanonicalFaceLabels.Length - 1
                : index;
        }

        private static bool ChipsEqual(
            FaceChipViewModel[] left,
            FaceChipViewModel[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
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
