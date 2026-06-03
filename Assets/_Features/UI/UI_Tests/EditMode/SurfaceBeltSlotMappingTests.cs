using System.Linq;
using Game.Feature.UI.HUD;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class SurfaceBeltSlotMappingTests
    {
        [Test]
        public void WrapSlot_NormalizesNegativeAndOverflowIndexes()
        {
            Assert.That(SurfaceBeltSlotMapping.WrapSlot(-1), Is.EqualTo(SurfaceBeltSlotMapping.SurfaceCount - 1));
            Assert.That(SurfaceBeltSlotMapping.WrapSlot(SurfaceBeltSlotMapping.SurfaceCount), Is.EqualTo(0));
        }

        [Test]
        public void ResolveDirection_HandlesWraparoundTransitions()
        {
            var lastSlot = SurfaceBeltSlotMapping.SurfaceCount - 1;

            Assert.That(SurfaceBeltSlotMapping.ResolveDirection(lastSlot, 0), Is.EqualTo(SurfaceBeltDirection.Forward));
            Assert.That(SurfaceBeltSlotMapping.ResolveDirection(0, lastSlot), Is.EqualTo(SurfaceBeltDirection.Backward));
        }

        [Test]
        public void BuildCells_CurrentBackSurfaceCreatesExpectedWrappedSequence()
        {
            var currentSlotIndex = SurfaceBeltSlotMapping.SurfaceCount - 1;
            var cells = SurfaceBeltSlotMapping.BuildCells(currentSlotIndex);
            var firstOffset = -(SurfaceBeltViewModel.AuthoredCellCount / 2);
            var expectedOffsets = Enumerable.Range(firstOffset, SurfaceBeltViewModel.AuthoredCellCount).ToArray();
            var expectedSlots = expectedOffsets
                .Select(offset => SurfaceBeltSlotMapping.WrapSlot(currentSlotIndex + offset))
                .ToArray();

            Assert.That(cells, Has.Length.EqualTo(SurfaceBeltViewModel.AuthoredCellCount));
            Assert.That(cells.Select(cell => cell.Offset).ToArray(), Is.EqualTo(expectedOffsets));
            Assert.That(cells.Select(cell => cell.SlotIndex).ToArray(), Is.EqualTo(expectedSlots));
            Assert.That(cells.Single(cell => cell.IsCurrent).Offset, Is.EqualTo(0));
        }
    }
}
