using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVfxSurfaceCellAnchorTests
    {
        [Test]
        [Category("Extended")]
        public void TileFeatureVisualRequestPlanner_PreservesSurfaceCellFace()
        {
            var cell = new SurfaceCell(FaceId.Ceiling, 4, 5);
            var request = new TilePresentationRequest(
                TilePresentationRequestKind.ButtonActivated,
                tileId: 7,
                cell,
                TileFeatureKind.Button,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0);

            Assert.That(TileFeatureVisualRequestPlanner.TryCreate(request, out var visualRequest), Is.True);
            Assert.That(visualRequest.Cell, Is.EqualTo(cell));
            Assert.That(visualRequest.Cell.face, Is.EqualTo(FaceId.Ceiling));
        }
    }
}
