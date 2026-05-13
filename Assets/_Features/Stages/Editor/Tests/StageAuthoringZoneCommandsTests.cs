using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringZoneCommandsTests
    {
        [Test]
        public void TryAddZone_ValidSingleCell_AddsZone()
        {
            WithAuthoring(authoring =>
            {
                var result = StageAuthoringZoneCommands.TryAddZone(
                    authoring,
                    " goal ",
                    FaceId.Floor,
                    new[] { Region(1, 1, 1, 1) });

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(result.ZoneIndex, Is.EqualTo(0));
                Assert.That(authoring.Zones.Single().ZoneId, Is.EqualTo("goal"));
                Assert.That(authoring.Zones.Single().FaceId, Is.EqualTo(FaceId.Floor));
            });
        }

        [Test]
        public void TryAddZone_ValidRectangle_AddsZone()
        {
            WithAuthoring(authoring =>
            {
                var result = StageAuthoringZoneCommands.TryAddZone(
                    authoring,
                    "room",
                    FaceId.Front,
                    new[] { Region(0, 0, 2, 2) });

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(authoring.Zones.Single().Regions.Single().MaxInclusive, Is.EqualTo(new Vector2Int(2, 2)));
            });
        }

        [TestCase("")]
        [TestCase(" ")]
        public void TryAddZone_EmptyZoneId_Fails(string zoneId)
        {
            WithAuthoring(authoring =>
            {
                var result = StageAuthoringZoneCommands.TryAddZone(
                    authoring,
                    zoneId,
                    FaceId.Floor,
                    new[] { Region(0, 0, 0, 0) });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(authoring.Zones, Is.Empty);
            });
        }

        [Test]
        public void TryAddZone_DuplicateZoneId_Fails()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetZones(new[] { Zone("goal", FaceId.Floor, Region(0, 0, 0, 0)) });

                var result = StageAuthoringZoneCommands.TryAddZone(
                    authoring,
                    " goal ",
                    FaceId.Floor,
                    new[] { Region(1, 1, 1, 1) });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(authoring.Zones, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void TryAddZone_OutOfBoundsRegion_Fails()
        {
            WithAuthoring(authoring =>
            {
                var result = StageAuthoringZoneCommands.TryAddZone(
                    authoring,
                    "bad",
                    FaceId.Floor,
                    new[] { Region(0, 0, 5, 5) });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(authoring.Zones, Is.Empty);
            });
        }

        [Test]
        public void TryAddZone_InvertedRegion_Fails()
        {
            WithAuthoring(authoring =>
            {
                var result = StageAuthoringZoneCommands.TryAddZone(
                    authoring,
                    "bad",
                    FaceId.Floor,
                    new[] { Region(2, 2, 1, 1) });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(authoring.Zones, Is.Empty);
            });
        }

        [Test]
        public void TryAddZone_EmptyRegions_Fails()
        {
            WithAuthoring(authoring =>
            {
                var result = StageAuthoringZoneCommands.TryAddZone(
                    authoring,
                    "bad",
                    FaceId.Floor,
                    Array.Empty<StageZoneRegionDefinition>());

                Assert.That(result.Succeeded, Is.False);
            });
        }

        [Test]
        public void TryAddZone_InvalidFaceId_Fails()
        {
            WithAuthoring(authoring =>
            {
                var result = StageAuthoringZoneCommands.TryAddZone(
                    authoring,
                    "bad",
                    (FaceId)999,
                    new[] { Region(0, 0, 0, 0) });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(authoring.Zones, Is.Empty);
            });
        }

        [Test]
        public void TryUpdateZone_ValidChange_UpdatesZone()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetZones(new[] { Zone("a", FaceId.Floor, Region(0, 0, 0, 0)) });

                var result = StageAuthoringZoneCommands.TryUpdateZone(
                    authoring,
                    0,
                    "b",
                    FaceId.Front,
                    new[] { Region(1, 1, 2, 2) });

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(authoring.Zones.Single().ZoneId, Is.EqualTo("b"));
                Assert.That(authoring.Zones.Single().FaceId, Is.EqualTo(FaceId.Front));
            });
        }

        [Test]
        public void TryUpdateZone_DuplicateZoneIdExceptSelf_Fails()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetZones(new[]
                {
                    Zone("a", FaceId.Floor, Region(0, 0, 0, 0)),
                    Zone("b", FaceId.Floor, Region(1, 1, 1, 1)),
                });

                var result = StageAuthoringZoneCommands.TryUpdateZone(
                    authoring,
                    0,
                    "b",
                    FaceId.Floor,
                    new[] { Region(0, 0, 0, 0) });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(authoring.Zones[0].ZoneId, Is.EqualTo("a"));
            });
        }

        [Test]
        public void TryRemoveZone_ValidIndex_RemovesZone()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetZones(new[] { Zone("a", FaceId.Floor, Region(0, 0, 0, 0)) });

                var result = StageAuthoringZoneCommands.TryRemoveZone(authoring, 0);

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(authoring.Zones, Is.Empty);
            });
        }

        [Test]
        public void TryRemoveZone_InvalidIndex_Fails()
        {
            WithAuthoring(authoring =>
            {
                var result = StageAuthoringZoneCommands.TryRemoveZone(authoring, 0);

                Assert.That(result.Succeeded, Is.False);
            });
        }

        [Test]
        public void TryDuplicateZone_NewId_CopiesRegions()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetZones(new[] { Zone("a", FaceId.Front, Region(1, 1, 2, 2)) });

                var result = StageAuthoringZoneCommands.TryDuplicateZone(authoring, 0, "b");

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(authoring.Zones, Has.Count.EqualTo(2));
                Assert.That(authoring.Zones[1].ZoneId, Is.EqualTo("b"));
                Assert.That(authoring.Zones[1].FaceId, Is.EqualTo(FaceId.Front));
                Assert.That(authoring.Zones[1].Regions.Single().MinInclusive, Is.EqualTo(new Vector2Int(1, 1)));
            });
        }

        [Test]
        public void TryDuplicateZone_DuplicateNewId_Fails()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetZones(new[]
                {
                    Zone("a", FaceId.Floor, Region(0, 0, 0, 0)),
                    Zone("b", FaceId.Floor, Region(1, 1, 1, 1)),
                });

                var result = StageAuthoringZoneCommands.TryDuplicateZone(authoring, 0, "b");

                Assert.That(result.Succeeded, Is.False);
                Assert.That(authoring.Zones, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public void SelectZoneCell_CellWithMultipleZones_CyclesUsingIndexHint()
        {
            var zones = new[]
            {
                Zone("a", FaceId.Floor, Region(0, 0, 1, 1)),
                Zone("b", FaceId.Floor, Region(1, 1, 2, 2)),
            };
            var state = new StageAuthoringGridSelectionState();

            state.SelectZoneCell(FaceId.Floor, new Vector2Int(1, 1), zones);
            Assert.That(state.SelectedZoneId, Is.EqualTo("a"));

            state.SelectZoneCell(FaceId.Floor, new Vector2Int(1, 1), zones);
            Assert.That(state.SelectedZoneId, Is.EqualTo("b"));
            Assert.That(state.CountZonesAt(zones, FaceId.Floor, 1, 1), Is.EqualTo(2));
        }

        [Test]
        public void ZoneSelection_DoesNotClearPlacementOrTileFeatureSelection()
        {
            var placements = new[] { Placement("p", FaceId.Floor, 0, 0) };
            var tileFeatures = new[] { TileFeature(10, FaceId.Floor, 0, 0) };
            var zones = new[] { Zone("z", FaceId.Floor, Region(0, 0, 0, 0)) };
            var state = new StageAuthoringGridSelectionState();

            state.SelectPlacement(0, placements);
            state.SelectTileFeature(0, tileFeatures);
            state.SelectZoneCell(FaceId.Floor, new Vector2Int(0, 0), zones);
            state.ClearZoneSelection();

            Assert.That(state.ResolveSelectedPlacementIndex(placements), Is.EqualTo(0));
            Assert.That(state.ResolveSelectedTileFeatureIndex(tileFeatures), Is.EqualTo(0));
            Assert.That(state.HasSelectedZone, Is.False);
        }

        private static void WithAuthoring(Action<StageAuthoringDefinition> action)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                authoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(3, 3),
                    InitialBottomFace = FaceId.Floor,
                });
                action(authoring);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        private static StageZoneDefinition Zone(
            string zoneId,
            FaceId face,
            StageZoneRegionDefinition region)
        {
            return new StageZoneDefinition
            {
                ZoneId = zoneId,
                FaceId = face,
                Regions = new[] { region },
            };
        }

        private static StageZoneRegionDefinition Region(int minX, int minY, int maxX, int maxY)
        {
            return new StageZoneRegionDefinition
            {
                MinInclusive = new Vector2Int(minX, minY),
                MaxInclusive = new Vector2Int(maxX, maxY),
            };
        }

        private static StagePlacedEntityAuthoring Placement(string stableGuid, FaceId face, int x, int y)
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = stableGuid,
                Kind = StageAuthoringEntityKind.Box,
                Cell = new SurfaceCell(face, x, y),
                Facing = Direction.None,
                Hp = 1,
            };
        }

        private static StageTileFeatureDefinition TileFeature(int tileId, FaceId face, int x, int y)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = new SurfaceCell(face, x, y),
                Kind = TileFeatureKind.Button,
                ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                Direction = Direction2D.None,
                BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
            };
        }
    }
}
