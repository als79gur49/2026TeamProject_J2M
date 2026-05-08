using System.Linq;
using Game.Feature.Gameplay.BoardState;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringGovernanceTests
    {
        [Test]
        public void GovernanceDetectsGeneratedDrift()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: true);

            try
            {
                SetFirstPlayerHp(fixture.Gameplay, 2);
                var report = new StageCatalogValidator().ValidateEntries(
                    new[] { fixture.Entry },
                    aliasTable: null,
                    new StageCatalogValidationOptions
                    {
                        Timing = StageValidationTiming.TestOrCi,
                    });

                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Severity == StageValidationSeverity.Error &&
                        issue.Code == "GameplayDrift.SpawnFieldMismatch"),
                    Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageWithoutAuthoringDefinitionStillValid()
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                entry.name = "legacy-stage_Entry";
                entry.AssignStageId(StageId.CreateOrThrow("legacy-stage"));
                entry.AssignGameplayDefinition(gameplay);
                entry.AssignPresentationDefinition(presentation);
                presentation.SetOwnerMetadata(entry, string.Empty);
                SetMinimalStage(gameplay);

                var report = new StageCatalogValidator().ValidateEntries(
                    new[] { entry },
                    aliasTable: null,
                    new StageCatalogValidationOptions
                    {
                        Timing = StageValidationTiming.TestOrCi,
                    });

                Assert.That(report.HasErrors, Is.False);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.missing"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entry);
                UnityEngine.Object.DestroyImmediate(gameplay);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringBarricadePolicy_ValidDefinitionPasses()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(
                        100,
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });

                var report = ValidateFixture(fixture);

                Assert.That(
                    report.Issues.Any(issue => issue.Code.StartsWith("authoring.tile-feature.barricade", System.StringComparison.Ordinal)),
                    Is.False);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringBarricadePolicy_RejectsUnsupportedActivation()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(
                        100,
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.Always,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });

                var report = ValidateFixture(fixture);

                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.barricade-activation-unsupported"), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringBarricadePolicy_RejectsDirectionAndSelector()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(
                        100,
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.Right,
                        TileFeatureBoxSelector.AnyPushableBox),
                    CreateTileFeature(
                        101,
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        (Direction2D)99,
                        (TileFeatureBoxSelector)99,
                        new SurfaceCell(FaceId.Floor, 2, 1)),
                });

                var report = ValidateFixture(fixture);

                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.barricade-direction-unsupported"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.barricade-box-selector-unsupported"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.direction-invalid"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.box-selector-invalid"), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringBarricadePolicy_RejectsDuplicateSameCell()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(100, TileFeatureKind.Barricade, TileFeatureActivationRule.FrontFaceOnly, Direction2D.None, TileFeatureBoxSelector.None, cell),
                    CreateTileFeature(101, TileFeatureKind.Barricade, TileFeatureActivationRule.FrontFaceOnly, Direction2D.None, TileFeatureBoxSelector.None, cell),
                });

                var report = ValidateFixture(fixture);

                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.barricade-cell-duplicate"), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringExitPolicy_ValidDefinitionPasses()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(
                        100,
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.BottomFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });

                var report = ValidateFixture(fixture);

                Assert.That(
                    report.Issues.Any(issue => issue.Code.StartsWith("authoring.tile-feature.exit", System.StringComparison.Ordinal)),
                    Is.False);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringExitPolicy_RejectsUnsupportedActivation()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(
                        100,
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.Always,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });

                var report = ValidateFixture(fixture);

                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.exit-activation-unsupported"), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringExitPolicy_RejectsDirectionAndSelector()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(
                        100,
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.BottomFaceOnly,
                        Direction2D.Right,
                        TileFeatureBoxSelector.AnyPushableBox),
                    CreateTileFeature(
                        101,
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.BottomFaceOnly,
                        (Direction2D)99,
                        (TileFeatureBoxSelector)99,
                        new SurfaceCell(FaceId.Floor, 2, 1)),
                });

                var report = ValidateFixture(fixture);

                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.exit-direction-unsupported"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.exit-box-selector-unsupported"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.direction-invalid"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.box-selector-invalid"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.exit-duplicate"), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringMoonBlockGeneratorPolicy_ValidShapePassesSourceChecks()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(
                        100,
                        TileFeatureKind.MoonBlockGenerator,
                        TileFeatureActivationRule.BottomFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None,
                        boundEntityId: 20),
                });

                var report = ValidateFixture(fixture);

                Assert.That(
                    report.Issues.Any(issue => issue.Code.StartsWith("authoring.tile-feature.moon-block-generator", System.StringComparison.Ordinal)),
                    Is.False);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageCatalogValidator_AuthoringMoonBlockGeneratorPolicy_RejectsInvalidShape()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: false);

            try
            {
                var duplicateCell = new SurfaceCell(FaceId.Floor, 1, 1);
                fixture.Authoring.SetTileFeatures(new[]
                {
                    CreateTileFeature(
                        100,
                        TileFeatureKind.MoonBlockGenerator,
                        TileFeatureActivationRule.Always,
                        Direction2D.Right,
                        TileFeatureBoxSelector.AnyPushableBox,
                        duplicateCell),
                    CreateTileFeature(
                        101,
                        TileFeatureKind.MoonBlockGenerator,
                        TileFeatureActivationRule.BottomFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None,
                        duplicateCell),
                });

                var report = ValidateFixture(fixture);

                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.moon-block-generator-activation-unsupported"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.moon-block-generator-direction-unsupported"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.moon-block-generator-box-selector-unsupported"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.moon-block-generator-cell-duplicate"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.moon-block-generator-duplicate"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.tile-feature.moon-block-generator-bound-id-non-positive"), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static StageValidationReport ValidateFixture(GovernanceFixture fixture)
        {
            return new StageCatalogValidator().ValidateEntries(
                new[] { fixture.Entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });
        }

        private static StageTileFeatureDefinition CreateTileFeature(
            int tileId,
            TileFeatureKind kind,
            TileFeatureActivationRule activationRule,
            Direction2D direction,
            TileFeatureBoxSelector boxSelector,
            SurfaceCell? cell = null,
            int boundEntityId = 0)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = cell ?? new SurfaceCell(FaceId.Floor, 1, 1),
                Kind = kind,
                ActivationRule = activationRule,
                Direction = direction,
                BoxSelector = boxSelector,
                BoundEntityId = boundEntityId,
            };
        }

        private static GovernanceFixture CreateSyncedEntry(bool enforceGeneratedSync)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            entry.name = "governance-stage_Entry";
            gameplay.name = "governance-stage";
            presentation.name = "governance-stage_Presentation";
            authoring.name = "governance-stage_Authoring";
            entry.AssignStageId(StageId.CreateOrThrow("governance-stage"));
            entry.AssignGameplayDefinition(gameplay);
            entry.AssignPresentationDefinition(presentation);
            entry.AssignAuthoringDefinition(authoring);
            authoring.SetOwnerMetadata(entry, string.Empty);
            presentation.SetOwnerMetadata(entry, string.Empty);
            authoring.AssignGeneratedDefinitions(gameplay, presentation);
            authoring.SetBoard(new StageBoardDefinition
            {
                MinInclusive = new Vector2Int(0, 0),
                MaxInclusive = new Vector2Int(2, 2),
                InitialBottomFace = FaceId.Floor,
            });
            authoring.SetPlacements(new[]
            {
                new StagePlacedEntityAuthoring
                {
                    StableGuid = "player",
                    DisplayName = "Player",
                    Kind = StageAuthoringEntityKind.Player,
                    Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                    Facing = Direction.Right,
                    Hp = 1,
                },
            });
            authoring.SetEnforceGeneratedSync(enforceGeneratedSync);
            var generationReport = StageAuthoringGenerator.Generate(authoring, StageAuthoringGenerateOptions.WriteAll);
            Assert.That(generationReport.HasErrors, Is.False);
            return new GovernanceFixture(entry, authoring, gameplay, presentation);
        }

        private static void SetMinimalStage(StageDefinition gameplay)
        {
            var serializedObject = new SerializedObject(gameplay);
            var board = serializedObject.FindProperty("board");
            board.FindPropertyRelative("MinInclusive").vector2IntValue = new Vector2Int(0, 0);
            board.FindPropertyRelative("MaxInclusive").vector2IntValue = new Vector2Int(2, 2);
            board.FindPropertyRelative("InitialBottomFace").intValue = (int)FaceId.Floor;
            var players = serializedObject.FindProperty("playerSpawns");
            players.arraySize = 1;
            var player = players.GetArrayElementAtIndex(0);
            player.FindPropertyRelative("EntityId").intValue = 1;
            player.FindPropertyRelative("Kind").intValue = (int)StageSpawnKind.Player;
            var cell = player.FindPropertyRelative("Cell");
            cell.FindPropertyRelative("face").intValue = (int)FaceId.Floor;
            cell.FindPropertyRelative("x").intValue = 0;
            cell.FindPropertyRelative("y").intValue = 0;
            player.FindPropertyRelative("Facing").intValue = (int)Direction.Right;
            player.FindPropertyRelative("Hp").intValue = 1;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFirstPlayerHp(StageDefinition gameplay, int hp)
        {
            var serializedObject = new SerializedObject(gameplay);
            serializedObject
                .FindProperty("playerSpawns")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("Hp")
                .intValue = hp;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class GovernanceFixture
        {
            private readonly UnityEngine.Object[] ownedObjects;

            public GovernanceFixture(
                StageContentEntry entry,
                StageAuthoringDefinition authoring,
                StageDefinition gameplay,
                StagePresentationDefinition presentation)
            {
                Entry = entry;
                Authoring = authoring;
                Gameplay = gameplay;
                ownedObjects = new UnityEngine.Object[] { entry, authoring, gameplay, presentation };
            }

            public StageContentEntry Entry { get; }

            public StageAuthoringDefinition Authoring { get; }

            public StageDefinition Gameplay { get; }

            public void Destroy()
            {
                for (var i = 0; i < ownedObjects.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }
        }
    }
}
