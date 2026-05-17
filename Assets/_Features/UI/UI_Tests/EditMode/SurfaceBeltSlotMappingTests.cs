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
            Assert.That(SurfaceBeltSlotMapping.WrapSlot(-1), Is.EqualTo(3));
            Assert.That(SurfaceBeltSlotMapping.WrapSlot(4), Is.EqualTo(0));
        }

        [Test]
        public void ResolveDirection_HandlesWraparoundTransitions()
        {
            Assert.That(SurfaceBeltSlotMapping.ResolveDirection(3, 0), Is.EqualTo(SurfaceBeltDirection.Forward));
            Assert.That(SurfaceBeltSlotMapping.ResolveDirection(0, 3), Is.EqualTo(SurfaceBeltDirection.Backward));
        }

        [Test]
        public void BuildCells_CurrentBackSurfaceCreatesExpectedWrappedSequence()
        {
            var cells = SurfaceBeltSlotMapping.BuildCells(3);

            Assert.That(cells.Select(cell => cell.Offset).ToArray(), Is.EqualTo(new[] { -3, -2, -1, 0, 1, 2, 3 }));
            Assert.That(cells.Select(cell => cell.SlotIndex).ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 0, 1, 2 }));
            Assert.That(cells.Single(cell => cell.IsCurrent).Offset, Is.EqualTo(0));
        }
    }
}
