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
        {
            SlotIndex = SurfaceBeltSlotMapping.WrapSlot(slotIndex);
            Offset = offset;
            IsCurrent = isCurrent;
        }

        public int SlotIndex { get; }

        public int Offset { get; }

        public bool IsCurrent { get; }

        public bool Equals(SurfaceBeltCellViewModel other)
        {
            return SlotIndex == other.SlotIndex &&
                   Offset == other.Offset &&
                   IsCurrent == other.IsCurrent;
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceBeltCellViewModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SlotIndex, Offset, IsCurrent);
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

        public void SetState(
            bool visible,
            int centerSlotIndex,
            int sourceSlotIndex,
            int destinationSlotIndex,
            SurfaceBeltDirection direction,
            bool isTransitioning,
            int transitionSequenceId,
            SurfaceBeltCellViewModel[] cells)
        {
            var nextCenter = SurfaceBeltSlotMapping.WrapSlot(centerSlotIndex);
            var nextSource = SurfaceBeltSlotMapping.WrapSlot(sourceSlotIndex);
            var nextDestination = SurfaceBeltSlotMapping.WrapSlot(destinationSlotIndex);
            var nextCells = cells ?? Array.Empty<SurfaceBeltCellViewModel>();
            if (nextCells.Length != AuthoredCellCount)
            {
                throw new ArgumentException(
                    $"{nameof(SurfaceBeltViewModel)} requires exactly {AuthoredCellCount} authored cells.",
                    nameof(cells));
            }

            if (Visible == visible &&
                CenterSlotIndex == nextCenter &&
                SourceSlotIndex == nextSource &&
                DestinationSlotIndex == nextDestination &&
                Direction == direction &&
                IsTransitioning == isTransitioning &&
                TransitionSequenceId == transitionSequenceId &&
                VisibleCellCount == VisibleCellCountValue &&
                CellsEqual(Cells, nextCells))
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
            Changed?.Invoke();
        }

        private static SurfaceBeltCellViewModel[] CopyCells(SurfaceBeltCellViewModel[] cells)
        {
            var copy = new SurfaceBeltCellViewModel[cells.Length];
            Array.Copy(cells, copy, cells.Length);
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
    }
}
