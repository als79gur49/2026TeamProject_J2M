using System;

namespace Game.Feature.UI.HUD
{
    public enum SurfaceBeltDirection
    {
        None = 0,
        Forward = 1,
        Backward = 2,
    }

    public readonly struct SurfaceBeltCellViewModel : IEquatable<SurfaceBeltCellViewModel>
    {
        public SurfaceBeltCellViewModel(int slotIndex, int offset, bool isCurrent)
            : this(slotIndex, offset, isCurrent, showButtonBadge: true)
        {
        }

        public SurfaceBeltCellViewModel(int slotIndex, int offset, bool isCurrent, bool showButtonBadge)
        {
            SlotIndex = SurfaceBeltSlotMapping.WrapSlot(slotIndex);
            Offset = offset;
            IsCurrent = isCurrent;
            ShowButtonBadge = showButtonBadge;
        }

        public int SlotIndex { get; }

        public int Offset { get; }

        public bool IsCurrent { get; }

        public bool ShowButtonBadge { get; }

        public bool Equals(SurfaceBeltCellViewModel other)
        {
            return SlotIndex == other.SlotIndex &&
                   Offset == other.Offset &&
                   IsCurrent == other.IsCurrent &&
                   ShowButtonBadge == other.ShowButtonBadge;
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceBeltCellViewModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SlotIndex, Offset, IsCurrent, ShowButtonBadge);
        }
    }

    public readonly struct SurfaceBeltButtonRemainderViewModel : IEquatable<SurfaceBeltButtonRemainderViewModel>
    {
        public SurfaceBeltButtonRemainderViewModel(
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

        public bool Equals(SurfaceBeltButtonRemainderViewModel other)
        {
            return SlotIndex == other.SlotIndex &&
                   NormalRemaining == other.NormalRemaining &&
                   MoonBlockOnlyRemaining == other.MoonBlockOnlyRemaining;
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceBeltButtonRemainderViewModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SlotIndex, NormalRemaining, MoonBlockOnlyRemaining);
        }
    }

    public sealed class SurfaceBeltViewModel
    {
        public const int AuthoredCellCount = 7;
        public const int VisibleCellCountValue = 5;

        public event Action Changed;

        public bool Visible { get; private set; } = true;

        public int CenterSlotIndex { get; private set; }

        public int SourceSlotIndex { get; private set; }

        public int DestinationSlotIndex { get; private set; }

        public SurfaceBeltDirection Direction { get; private set; } = SurfaceBeltDirection.None;

        public bool IsTransitioning { get; private set; }

        public int TransitionSequenceId { get; private set; }

        public int VisibleCellCount { get; private set; } = VisibleCellCountValue;

        public SurfaceBeltCellViewModel[] Cells { get; private set; } = SurfaceBeltSlotMapping.BuildCells(0);

        public SurfaceBeltButtonRemainderViewModel[] ButtonRemainders { get; private set; } = CreateEmptyButtonRemainders();

        public void SetState(
            bool visible,
            int centerSlotIndex,
            int sourceSlotIndex,
            int destinationSlotIndex,
            SurfaceBeltDirection direction,
            bool isTransitioning,
            int transitionSequenceId,
            SurfaceBeltCellViewModel[] cells,
            SurfaceBeltButtonRemainderViewModel[] buttonRemainders)
        {
            var nextCenter = SurfaceBeltSlotMapping.WrapSlot(centerSlotIndex);
            var nextSource = SurfaceBeltSlotMapping.WrapSlot(sourceSlotIndex);
            var nextDestination = SurfaceBeltSlotMapping.WrapSlot(destinationSlotIndex);
            var nextCells = cells ?? Array.Empty<SurfaceBeltCellViewModel>();
            var nextButtonRemainders = buttonRemainders ?? Array.Empty<SurfaceBeltButtonRemainderViewModel>();
            if (nextCells.Length != AuthoredCellCount)
            {
                throw new ArgumentException(
                    $"{nameof(SurfaceBeltViewModel)} requires exactly {AuthoredCellCount} authored cells.",
                    nameof(cells));
            }

            if (nextButtonRemainders.Length != SurfaceBeltSlotMapping.SurfaceCount)
            {
                throw new ArgumentException(
                    $"{nameof(SurfaceBeltViewModel)} requires exactly {SurfaceBeltSlotMapping.SurfaceCount} button remainder slots.",
                    nameof(buttonRemainders));
            }

            if (Visible == visible &&
                CenterSlotIndex == nextCenter &&
                SourceSlotIndex == nextSource &&
                DestinationSlotIndex == nextDestination &&
                Direction == direction &&
                IsTransitioning == isTransitioning &&
                TransitionSequenceId == transitionSequenceId &&
                VisibleCellCount == VisibleCellCountValue &&
                CellsEqual(Cells, nextCells) &&
                ButtonRemaindersEqual(ButtonRemainders, nextButtonRemainders))
            {
                return;
            }

            Visible = visible;
            CenterSlotIndex = nextCenter;
            SourceSlotIndex = nextSource;
            DestinationSlotIndex = nextDestination;
            Direction = direction;
            IsTransitioning = isTransitioning;
            TransitionSequenceId = transitionSequenceId;
            VisibleCellCount = VisibleCellCountValue;
            Cells = CopyCells(nextCells);
            ButtonRemainders = CopyButtonRemainders(nextButtonRemainders);
            Changed?.Invoke();
        }

        public SurfaceBeltButtonRemainderViewModel GetButtonRemainderForSlot(int slotIndex)
        {
            var normalizedSlot = SurfaceBeltSlotMapping.WrapSlot(slotIndex);
            var remainders = ButtonRemainders ?? Array.Empty<SurfaceBeltButtonRemainderViewModel>();
            for (var i = 0; i < remainders.Length; i++)
            {
                if (remainders[i].SlotIndex == normalizedSlot)
                {
                    return remainders[i];
                }
            }

            return new SurfaceBeltButtonRemainderViewModel(normalizedSlot, 0, 0);
        }

        public static SurfaceBeltButtonRemainderViewModel[] CreateEmptyButtonRemainders()
        {
            var result = new SurfaceBeltButtonRemainderViewModel[SurfaceBeltSlotMapping.SurfaceCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new SurfaceBeltButtonRemainderViewModel(i, 0, 0);
            }

            return result;
        }

        private static SurfaceBeltCellViewModel[] CopyCells(SurfaceBeltCellViewModel[] cells)
        {
            var copy = new SurfaceBeltCellViewModel[cells.Length];
            Array.Copy(cells, copy, cells.Length);
            return copy;
        }

        private static SurfaceBeltButtonRemainderViewModel[] CopyButtonRemainders(
            SurfaceBeltButtonRemainderViewModel[] buttonRemainders)
        {
            var copy = CreateEmptyButtonRemainders();
            for (var i = 0; i < buttonRemainders.Length; i++)
            {
                var item = buttonRemainders[i];
                var slot = SurfaceBeltSlotMapping.WrapSlot(item.SlotIndex);
                copy[slot] = new SurfaceBeltButtonRemainderViewModel(
                    slot,
                    item.NormalRemaining,
                    item.MoonBlockOnlyRemaining);
            }

            return copy;
        }

        private static bool CellsEqual(
            SurfaceBeltCellViewModel[] left,
            SurfaceBeltCellViewModel[] right)
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

        private static bool ButtonRemaindersEqual(
            SurfaceBeltButtonRemainderViewModel[] left,
            SurfaceBeltButtonRemainderViewModel[] right)
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
