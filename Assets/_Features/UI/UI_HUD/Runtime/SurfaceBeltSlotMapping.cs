namespace Game.Feature.UI.HUD
{
    public static class SurfaceBeltSlotMapping
    {
        public const int SurfaceCount = 4;

        private static readonly int[] AuthoredOffsets =
        {
            -3,
            -2,
            -1,
            0,
            1,
            2,
            3,
        };

        public static int WrapSlot(int index)
        {
            var value = index % SurfaceCount;
            return value < 0 ? value + SurfaceCount : value;
        }

        public static SurfaceBeltDirection ResolveDirection(int sourceSlotIndex, int destinationSlotIndex)
        {
            var source = WrapSlot(sourceSlotIndex);
            var destination = WrapSlot(destinationSlotIndex);
            if (destination == WrapSlot(source + 1))
            {
                return SurfaceBeltDirection.Forward;
            }

            if (destination == WrapSlot(source - 1))
            {
                return SurfaceBeltDirection.Backward;
            }

            return SurfaceBeltDirection.None;
        }

        public static SurfaceBeltCellViewModel[] BuildCells(int centerSlotIndex)
        {
            var cells = new SurfaceBeltCellViewModel[AuthoredOffsets.Length];
            var center = WrapSlot(centerSlotIndex);
            for (var i = 0; i < AuthoredOffsets.Length; i++)
            {
                var offset = AuthoredOffsets[i];
                cells[i] = new SurfaceBeltCellViewModel(
                    WrapSlot(center + offset),
                    offset,
                    offset == 0);
            }

            return cells;
        }
    }
}
