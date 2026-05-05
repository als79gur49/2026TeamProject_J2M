using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class TileFeatureOverlayArchitectureTests
    {
        private const string TileFeatureOverlayAdrPath =
            "Docs/Architecture/ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md";
        private const string TilePresentationRequestPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TilePresentationRequestPlanner.cs";
        private static readonly string[] ForbiddenTerrainFlagTokens =
        {
            "Trap",
            "Hazard",
            "Buff",
            "Trigger",
            "Aura",
            "Zone",
            "TileFeature",
            "Effect",
        };

        [Test]
        [Category("Core")]
        public void TileFeatureOverlayGateDocument_Exists()
        {
            Assert.That(File.Exists(GetAbsolutePath(TileFeatureOverlayAdrPath)), Is.True);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureOverlayGate_ForbidsOccupancyAndTerrainReuse()
        {
            var document = File.ReadAllText(GetAbsolutePath(TileFeatureOverlayAdrPath));

            Assert.That(document, Does.Contain("TileFeature is a SurfaceCell-based gameplay overlay layer."));
            Assert.That(document, Does.Contain("TileFeature is not Unit/Solid/Projectile occupancy."));
            Assert.That(document, Does.Contain("Blocking Terrain remains owned by `TerrainData` and `TerrainFlags`."));
            Assert.That(document, Does.Contain("Box + TileFeature is allowed."));
            Assert.That(document, Does.Contain("Other Solid + TileFeature"));
            Assert.That(document, Does.Contain("requires an explicit future policy decision"));
            Assert.That(document, Does.Contain("State surface phase"));
            Assert.That(document, Does.Contain("Dynamic mutation phase"));
            Assert.That(document, Does.Contain("Presentation phase"));
            Assert.That(document, Does.Contain("Dynamic TileEffect mutation must not be implemented before TileFeature state/query/export/hash exists"));
            Assert.That(document, Does.Contain("TileEffect-free ticks must add zero snapshot materialization."));
            Assert.That(document, Does.Contain("postTileEffectSnapshot` must not be eagerly created"));
            Assert.That(document, Does.Contain("VFX, audio, and UI must not call `WorldState.CreateSnapshot`"));
        }

        [Test]
        [Category("Core")]
        public void EntityType_DoesNotContainTileFeature()
        {
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("TileFeature"));
        }

        [Test]
        [Category("Core")]
        public void TerrainFlags_DoNotContainTileFeatureOrEffectSemantics()
        {
            var flagNames = Enum.GetNames(typeof(TerrainFlags));
            for (var i = 0; i < flagNames.Length; i++)
            {
                var flagName = flagNames[i];
                for (var tokenIndex = 0; tokenIndex < ForbiddenTerrainFlagTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        flagName,
                        Does.Not.Contain(ForbiddenTerrainFlagTokens[tokenIndex]),
                        $"TerrainFlags value '{flagName}' must not encode TileFeature or effect semantics.");
                }

                if (flagName == nameof(TerrainFlags.None))
                {
                    continue;
                }

                Assert.That(
                    flagName.StartsWith("Blocks", StringComparison.Ordinal),
                    Is.True,
                    $"New TerrainFlags value '{flagName}' is not obviously blocker terrain vocabulary. Update ADR-004/ADR-006 and this test before adding non-blocker terrain semantics.");
            }
        }

        [Test]
        [Category("Core")]
        public void TickPresentationData_ExposesPresentationOwnedTileEvents()
        {
            var property = typeof(TickPresentationData).GetProperty("TileEvents");

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(IReadOnlyList<TilePresentationEvent>)));
            Assert.That(property.SetMethod, Is.Null);
        }

        [Test]
        [Category("Core")]
        public void TilePresentationRequestPlanner_DoesNotReferenceAuthorityOrMutationTypes()
        {
            var source = File.ReadAllText(GetAbsolutePath(TilePresentationRequestPlannerPath));
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"Tile presentation request planner must not reference authority or mutation token '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void OccupancyLayer_DoesNotContainTileFeature_WhenLayerTypeExists()
        {
            var occupancyLayerType = typeof(EntityType).Assembly.GetType("Game.Feature.Gameplay.BoardState.OccupancyLayer");
            if (occupancyLayerType == null)
            {
                Assert.Pass("No OccupancyLayer enum exists in this phase.");
            }

            Assert.That(occupancyLayerType.IsEnum, Is.True);
            Assert.That(Enum.GetNames(occupancyLayerType), Does.Not.Contain("TileFeature"));
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(relativePath);
        }
    }
}
