using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualHandlerIsolationTests
    {
        [Test]
        [Category("Extended")]
        public void FeatureSpecificHandlers_OnlyHandleTheirOwnCueFamilies()
        {
            AssertHandlerCues(
                new ButtonTileFeatureVisualHandler(),
                TileFeatureVisualCueId.ButtonActivated,
                TileFeatureVisualCueId.ButtonVisibleLoop,
                TileFeatureVisualCueId.ButtonActiveLoop);
            AssertHandlerCues(
                new DestroyTileFeatureVisualHandler(),
                TileFeatureVisualCueId.DestroyTileTriggered,
                TileFeatureVisualCueId.DestroyTileActivated,
                TileFeatureVisualCueId.DestroyTileDeactivated,
                TileFeatureVisualCueId.DestroyTileActiveState,
                TileFeatureVisualCueId.DestroyTileLaserActive);
            AssertHandlerCues(
                new SlideTileFeatureVisualHandler(),
                TileFeatureVisualCueId.SlideTileRedirected,
                TileFeatureVisualCueId.SlideTileActiveState);
            AssertHandlerCues(
                new BarricadeTileFeatureVisualHandler(),
                TileFeatureVisualCueId.BarricadeBlocked,
                TileFeatureVisualCueId.BarricadeCrushed,
                TileFeatureVisualCueId.BarricadeActivated,
                TileFeatureVisualCueId.BarricadeDeactivated,
                TileFeatureVisualCueId.BarricadeActiveState,
                TileFeatureVisualCueId.BarricadeActiveLoop);
            AssertHandlerCues(
                new ExitTileFeatureVisualHandler(),
                TileFeatureVisualCueId.ExitOpened,
                TileFeatureVisualCueId.ExitEntered,
                TileFeatureVisualCueId.ExitOpenState,
                TileFeatureVisualCueId.ExitOpenLoop,
                TileFeatureVisualCueId.EntranceSpawn);
            AssertHandlerCues(
                new MoonBlockGeneratorTileFeatureVisualHandler(),
                TileFeatureVisualCueId.MoonBlockGenerated,
                TileFeatureVisualCueId.MoonBlockGeneratorBlocked);
        }

        private static void AssertHandlerCues(
            ITileFeatureVisualHandler handler,
            params TileFeatureVisualCueId[] expectedCues)
        {
            var expected = new HashSet<TileFeatureVisualCueId>(expectedCues);
            foreach (TileFeatureVisualCueId cueId in Enum.GetValues(typeof(TileFeatureVisualCueId)))
            {
                if (cueId == TileFeatureVisualCueId.None)
                {
                    continue;
                }

                Assert.That(handler.CanHandle(cueId), Is.EqualTo(expected.Contains(cueId)), $"{handler.GetType().Name}:{cueId}");
            }
        }
    }
}
