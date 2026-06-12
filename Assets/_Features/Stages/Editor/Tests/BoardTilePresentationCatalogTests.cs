using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class BoardTilePresentationCatalogTests
    {
        [Test]
        public void BoardTilePresentationCatalog_RejectsDuplicateKeys()
        {
            var material = CreateMaterial("DuplicateKeyMaterial");
            var catalog = CreateCatalog(
                Entry("active-bottom", BoardTileVisualRole.ActiveBottom, null, material),
                Entry(" active-bottom ", BoardTileVisualRole.ActiveFront, null, material));
            var stageEntry = CreateStageEntry(CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.catalog.key-duplicate");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry, catalog, material);
            }
        }

        [Test]
        public void BoardTilePresentationCatalog_RejectsMissingPrefabAndMaterial()
        {
            var catalog = CreateCatalog(Entry("missing-visual", BoardTileVisualRole.ActiveBottom, null, null));
            var stageEntry = CreateStageEntry(CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.catalog.visual-missing");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry, catalog);
            }
        }

        [Test]
        public void BoardTilePresentationCatalog_RejectsDuplicateDefaultForRole()
        {
            var material = CreateMaterial("DuplicateDefaultMaterial");
            var catalog = CreateCatalog(
                Entry("bottom-a", BoardTileVisualRole.ActiveBottom, null, material, isDefault: true),
                Entry("bottom-b", BoardTileVisualRole.ActiveBottom, null, material, isDefault: true));
            var stageEntry = CreateStageEntry(CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.catalog.default-duplicate");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry, catalog, material);
            }
        }

        [Test]
        public void StageCatalogValidator_AllowsSingleGenericDefaultWithoutRoleDefaults()
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = "GenericDefaultTilePrefab";
            var catalog = CreateCatalog(
                Entry("board.generic.default", BoardTileVisualRole.GenericDefault, prefab, null, isDefault: true));
            var stageEntry = CreateStageEntry(CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertNoBoardTileCatalogIssues(report);
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_RejectsMissingGenericDefault()
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = "RoleDefaultOnlyTilePrefab";
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, prefab, null, isDefault: true));
            var stageEntry = CreateStageEntry(CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.catalog.default-generic-missing");
                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Code == "presentation.board-tile.catalog.default-active-bottom-missing" ||
                        issue.Code == "presentation.board-tile.catalog.default-active-front-missing"),
                    Is.False,
                    FormatIssues(report));
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_RejectsGenericDefaultWithoutPrefab()
        {
            var material = CreateMaterial("GenericDefaultMaterialOnly");
            var catalog = CreateCatalog(
                Entry("board.generic.default", BoardTileVisualRole.GenericDefault, null, material, isDefault: true));
            var stageEntry = CreateStageEntry(CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.catalog.default-generic-prefab-missing");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry, catalog, material);
            }
        }

        [Test]
        public void BoardTilePresentationCatalog_TryGetDefaultEntry_UsesRole()
        {
            var material = CreateMaterial("RoleDefaultMaterial");
            var catalog = CreateCatalog(
                Entry("generic", BoardTileVisualRole.GenericDefault, null, material, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, material, isDefault: true));

            try
            {
                Assert.That(catalog.TryGetDefaultEntry(BoardTileVisualRole.ActiveFront, out var entry), Is.True);
                Assert.That(entry.PresentationKey, Is.EqualTo("front"));
            }
            finally
            {
                DestroyObjects(catalog, material);
            }
        }

        [Test]
        public void BoardTilePresentationCatalog_TryGetDefaultEntry_FailsForDuplicateDefault()
        {
            var material = CreateMaterial("DuplicateDefaultTryGetMaterial");
            var catalog = CreateCatalog(
                Entry("front-a", BoardTileVisualRole.ActiveFront, null, material, isDefault: true),
                Entry("front-b", BoardTileVisualRole.ActiveFront, null, material, isDefault: true));

            try
            {
                Assert.That(catalog.TryGetDefaultEntry(BoardTileVisualRole.ActiveFront, out _), Is.False);
            }
            finally
            {
                DestroyObjects(catalog, material);
            }
        }

        [Test]
        public void BoardTilePresentationCatalog_TryGetEntry_UsesNormalizedKey()
        {
            var material = CreateMaterial("TryGetEntryMaterial");
            var catalog = CreateCatalog(Entry(" override-key ", BoardTileVisualRole.ActiveBottom, null, material));

            try
            {
                Assert.That(catalog.TryGetEntry("override-key", out var entry), Is.True);
                Assert.That(entry.PresentationKey, Is.EqualTo("override-key"));
            }
            finally
            {
                DestroyObjects(catalog, material);
            }
        }

        [Test]
        public void StagePresentationAssembler_PreservesBoardTileCatalog()
        {
            var catalog = CreateCatalog();
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(presentation);

                Assert.That(resolved.BoardTilePresentationCatalog, Is.SameAs(catalog));
            }
            finally
            {
                DestroyObjects(presentation, catalog);
            }
        }

        [Test]
        public void StagePresentationDefinition_BoardTileOverrides_DefaultsToEmpty()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                Assert.That(presentation.BoardTilePresentationOverrides, Is.Empty);
            }
            finally
            {
                DestroyObjects(presentation);
            }
        }

        [Test]
        public void StagePresentationAssembler_PreservesBoardTileOverrides()
        {
            var catalog = CreateCatalog();
            var source = CreatePresentation(
                catalog,
                new BoardTilePresentationOverride(new SurfaceCell(FaceId.Floor, 0, 0), "cell-key"));

            try
            {
                var resolved = StagePresentationAssembler.Resolve(source);

                Assert.That(resolved.BoardTilePresentationOverrides.Count, Is.EqualTo(1));
                Assert.That(resolved.BoardTilePresentationOverrides[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(resolved.BoardTilePresentationOverrides[0].PresentationKey, Is.EqualTo("cell-key"));
            }
            finally
            {
                DestroyObjects(source, catalog);
            }
        }

        [Test]
        public void StagePresentationDefinition_ApplyResolvedData_PreservesBoardTileOverrides()
        {
            var catalog = CreateCatalog();
            var source = CreatePresentation(
                catalog,
                new BoardTilePresentationOverride(new SurfaceCell(FaceId.Front, 1, 0), "front-key"));
            var copy = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                copy.ApplyResolvedData(StagePresentationAssembler.Resolve(source));

                Assert.That(copy.BoardTilePresentationOverrides.Count, Is.EqualTo(1));
                Assert.That(copy.BoardTilePresentationOverrides[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
                Assert.That(copy.BoardTilePresentationOverrides[0].PresentationKey, Is.EqualTo("front-key"));
            }
            finally
            {
                DestroyObjects(source, copy, catalog);
            }
        }

        [Test]
        public void StagePresentationDefinition_ApplyResolvedData_PreservesBoardTileCatalog()
        {
            var catalog = CreateCatalog();
            var source = CreatePresentation(catalog);
            var copy = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                copy.ApplyResolvedData(StagePresentationAssembler.Resolve(source));

                Assert.That(copy.BoardTilePresentationCatalog, Is.SameAs(catalog));
            }
            finally
            {
                DestroyObjects(source, copy, catalog);
            }
        }

        [Test]
        public void GameplaySceneHostConfiguration_CarriesBoardTileCatalog()
        {
            var catalog = CreateCatalog();
            var configuration = new GameplaySceneHostConfiguration
            {
                BoardTilePresentationCatalog = catalog,
            };

            try
            {
                Assert.That(configuration.BoardTilePresentationCatalog, Is.SameAs(catalog));
            }
            finally
            {
                DestroyObjects(catalog);
            }
        }

        [Test]
        [Category("Core")]
        public void StagePresentationDefinition_WorldGuideInstructions_DefaultsToEmpty()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                Assert.That(presentation.WorldGuideInstructions, Is.Empty);
            }
            finally
            {
                DestroyObjects(presentation);
            }
        }

        [Test]
        [Category("Core")]
        public void StagePresentationAssembler_PreservesWorldGuideInstructions()
        {
            var guideCatalog = CreateWorldGuideCatalog(CreateWorldGuideCatalogEntry("push", new GameObject("GuidePrefab")));
            var instruction = new StageWorldGuideInstruction(
                enabled: true,
                guideKey: " push ",
                cell: new SurfaceCell(FaceId.Floor, 2, 3),
                localOffset: new Vector3(0.1f, 0.2f, 0.3f),
                heightOffset: 0.5f,
                facingMode: StageWorldGuideFacingMode.YawOnlyBillboard,
                hideWhenFaceInactive: false);
            var presentation = CreateWorldGuidePresentation(guideCatalog, instruction);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(presentation);

                Assert.That(resolved.WorldGuideCatalog, Is.SameAs(guideCatalog));
                Assert.That(resolved.WorldGuideInstructions.Count, Is.EqualTo(1));
                Assert.That(resolved.WorldGuideInstructions[0].GuideKey, Is.EqualTo("push"));
                Assert.That(resolved.WorldGuideInstructions[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 3)));
                Assert.That(resolved.WorldGuideInstructions[0].LocalOffset, Is.EqualTo(new Vector3(0.1f, 0.2f, 0.3f)));
                Assert.That(resolved.WorldGuideInstructions[0].HeightOffset, Is.EqualTo(0.5f));
                Assert.That(resolved.WorldGuideInstructions[0].FacingMode, Is.EqualTo(StageWorldGuideFacingMode.YawOnlyBillboard));
                Assert.That(resolved.WorldGuideInstructions[0].HideWhenFaceInactive, Is.False);
            }
            finally
            {
                DestroyObjects(presentation, guideCatalog, guideCatalog.Entries[0].Prefab);
            }
        }

        [Test]
        [Category("Core")]
        public void StagePresentationAssembler_SkipsDisabledWorldGuideInstructions()
        {
            var presentation = CreateWorldGuidePresentation(
                null,
                new StageWorldGuideInstruction(
                    enabled: false,
                    guideKey: "push",
                    cell: new SurfaceCell(FaceId.Floor, 0, 0),
                    localOffset: Vector3.zero,
                    heightOffset: 0.25f,
                    facingMode: StageWorldGuideFacingMode.BillboardToCamera,
                    hideWhenFaceInactive: true));

            try
            {
                var resolved = StagePresentationAssembler.Resolve(presentation);

                Assert.That(resolved.WorldGuideInstructions, Is.Empty);
            }
            finally
            {
                DestroyObjects(presentation);
            }
        }

        [Test]
        [Category("Core")]
        public void StagePresentationDefinition_ApplyResolvedData_PreservesWorldGuideInstructions()
        {
            var guideCatalog = CreateWorldGuideCatalog(CreateWorldGuideCatalogEntry("flip", new GameObject("GuidePrefab")));
            var source = CreateWorldGuidePresentation(
                guideCatalog,
                new StageWorldGuideInstruction(
                    enabled: true,
                    guideKey: "flip",
                    cell: new SurfaceCell(FaceId.Front, 4, 5),
                    localOffset: Vector3.up,
                    heightOffset: 0.75f,
                    facingMode: StageWorldGuideFacingMode.BillboardToCamera,
                    hideWhenFaceInactive: true));
            var copy = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                copy.ApplyResolvedData(StagePresentationAssembler.Resolve(source));

                Assert.That(copy.WorldGuideCatalog, Is.SameAs(guideCatalog));
                Assert.That(copy.WorldGuideInstructions.Count, Is.EqualTo(1));
                Assert.That(copy.WorldGuideInstructions[0].GuideKey, Is.EqualTo("flip"));
                Assert.That(copy.WorldGuideInstructions[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 4, 5)));
            }
            finally
            {
                DestroyObjects(source, copy, guideCatalog, guideCatalog.Entries[0].Prefab);
            }
        }

        [Test]
        [Category("Core")]
        public void StageCatalogValidator_RejectsWorldGuideOutsideBounds()
        {
            var guideCatalog = CreateWorldGuideCatalog(CreateWorldGuideCatalogEntry("movement", new GameObject("GuidePrefab")));
            var presentation = CreateWorldGuidePresentation(
                guideCatalog,
                new StageWorldGuideInstruction(
                    enabled: true,
                    guideKey: "movement",
                    cell: new SurfaceCell(FaceId.Floor, 1, 0),
                    localOffset: Vector3.zero,
                    heightOffset: 0.25f,
                    facingMode: StageWorldGuideFacingMode.BillboardToCamera,
                    hideWhenFaceInactive: true));
            var stageEntry = CreateStageEntry(presentation, CreateStageDefinition());

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.world-guide.cell-outside-bounds");
            }
            finally
            {
                DestroyObjects(stageEntry.GameplayDefinition, stageEntry, stageEntry.PresentationDefinition, guideCatalog, guideCatalog.Entries[0].Prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_ReportsDuplicateBoardTileOverrideCell()
        {
            var material = CreateMaterial("DuplicateOverrideMaterial");
            var catalog = CreateCatalog(Entry("cell-key", BoardTileVisualRole.ActiveBottom, null, material));
            var presentation = CreatePresentation(
                catalog,
                new BoardTilePresentationOverride(new SurfaceCell(FaceId.Floor, 0, 0), "cell-key"),
                new BoardTilePresentationOverride(new SurfaceCell(FaceId.Floor, 0, 0), "cell-key"));
            var stageEntry = CreateStageEntry(presentation, CreateStageDefinition());

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.override-cell-duplicate");
            }
            finally
            {
                DestroyObjects(stageEntry.GameplayDefinition, presentation, stageEntry, catalog, material);
            }
        }

        [Test]
        public void StageCatalogValidator_ReportsBoardTileOverrideOutsideBounds()
        {
            var material = CreateMaterial("OutsideBoundsOverrideMaterial");
            var catalog = CreateCatalog(Entry("cell-key", BoardTileVisualRole.ActiveBottom, null, material));
            var presentation = CreatePresentation(
                catalog,
                new BoardTilePresentationOverride(new SurfaceCell(FaceId.Floor, 2, 0), "cell-key"));
            var stageEntry = CreateStageEntry(presentation, CreateStageDefinition());

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.override-cell-outside-bounds");
            }
            finally
            {
                DestroyObjects(stageEntry.GameplayDefinition, presentation, stageEntry, catalog, material);
            }
        }

        [Test]
        public void StageCatalogValidator_ReportsBoardTileOverrideMissingCatalog()
        {
            var presentation = CreatePresentation(
                null,
                new BoardTilePresentationOverride(new SurfaceCell(FaceId.Floor, 0, 0), "cell-key"));
            var stageEntry = CreateStageEntry(presentation, CreateStageDefinition());

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.override-catalog-missing");
            }
            finally
            {
                DestroyObjects(stageEntry.GameplayDefinition, presentation, stageEntry);
            }
        }

        [Test]
        public void StageCatalogValidator_ReportsBoardTileOverrideMissingKey()
        {
            var material = CreateMaterial("MissingKeyOverrideMaterial");
            var catalog = CreateCatalog(Entry("known-key", BoardTileVisualRole.ActiveBottom, null, material));
            var presentation = CreatePresentation(
                catalog,
                new BoardTilePresentationOverride(new SurfaceCell(FaceId.Floor, 0, 0), "missing-key"));
            var stageEntry = CreateStageEntry(presentation, CreateStageDefinition());

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.board-tile.override-key-missing");
            }
            finally
            {
                DestroyObjects(stageEntry.GameplayDefinition, presentation, stageEntry, catalog, material);
            }
        }

        [Test]
        public void StageCatalogValidator_AllowsBoardTileOverrideWithValidCatalogEntry()
        {
            var material = CreateMaterial("ValidOverrideMaterial");
            var catalog = CreateCatalog(Entry("cell-key", BoardTileVisualRole.ActiveBottom, null, material));
            var presentation = CreatePresentation(
                catalog,
                new BoardTilePresentationOverride(new SurfaceCell(FaceId.Floor, 0, 0), "cell-key"));
            var stageEntry = CreateStageEntry(presentation, CreateStageDefinition());

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertNoBoardTileOverrideErrors(report);
            }
            finally
            {
                DestroyObjects(stageEntry.GameplayDefinition, presentation, stageEntry, catalog, material);
            }
        }

        private static BoardTilePresentationCatalogEntry Entry(
            string presentationKey,
            BoardTileVisualRole role,
            GameObject tilePrefab,
            Material materialFallback,
            bool isDefault = false)
        {
            var entry = new BoardTilePresentationCatalogEntry();
            SetPrivateField(entry, "presentationKey", presentationKey);
            SetPrivateField(entry, "displayName", presentationKey);
            SetPrivateField(entry, "role", role);
            SetPrivateField(entry, "tilePrefab", tilePrefab);
            SetPrivateField(entry, "materialFallback", materialFallback);
            SetPrivateField(entry, "isDefaultForRole", isDefault);
            return entry;
        }

        private static BoardTilePresentationCatalog CreateCatalog(
            params BoardTilePresentationCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<BoardTilePresentationCatalog>();
            catalog.name = "BoardTilePresentationCatalogTests";
            SetPrivateField(catalog, "entries", entries ?? Array.Empty<BoardTilePresentationCatalogEntry>());
            return catalog;
        }

        private static StagePresentationDefinition CreatePresentation(
            BoardTilePresentationCatalog catalog,
            params BoardTilePresentationOverride[] overrides)
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            presentation.name = "BoardTileCatalogPresentation";
            SetPrivateField(presentation, "boardTilePresentationCatalog", catalog);
            SetPrivateField(presentation, "boardTilePresentationOverrides", overrides ?? Array.Empty<BoardTilePresentationOverride>());
            return presentation;
        }

        private static StagePresentationDefinition CreateWorldGuidePresentation(
            StageWorldGuideCatalog catalog,
            params StageWorldGuideInstruction[] instructions)
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            presentation.name = "WorldGuidePresentation";
            SetPrivateField(presentation, "worldGuideCatalog", catalog);
            SetPrivateField(presentation, "worldGuideInstructions", instructions ?? Array.Empty<StageWorldGuideInstruction>());
            return presentation;
        }

        private static StageWorldGuideCatalogEntry CreateWorldGuideCatalogEntry(string guideKey, GameObject prefab)
        {
            var entry = new StageWorldGuideCatalogEntry();
            SetPrivateField(entry, "guideKey", guideKey);
            SetPrivateField(entry, "prefab", prefab);
            return entry;
        }

        private static StageWorldGuideCatalog CreateWorldGuideCatalog(
            params StageWorldGuideCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<StageWorldGuideCatalog>();
            catalog.name = "WorldGuideCatalogTests";
            SetPrivateField(catalog, "entries", entries ?? Array.Empty<StageWorldGuideCatalogEntry>());
            return catalog;
        }

        private static StageContentEntry CreateStageEntry(
            StagePresentationDefinition presentation,
            StageDefinition gameplay = null)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.name = "BoardTileCatalogEntry";
            entry.AssignStageId(StageId.CreateOrThrow("board-tile-catalog-tests"));
            entry.AssignGameplayDefinition(gameplay);
            entry.AssignPresentationDefinition(presentation);
            return entry;
        }

        private static StageDefinition CreateStageDefinition()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            SetPrivateField(stage, "board", new StageBoardDefinition
            {
                MinInclusive = Vector2Int.zero,
                MaxInclusive = Vector2Int.zero,
                InitialBottomFace = FaceId.Floor,
            });
            return stage;
        }

        private static Material CreateMaterial(string materialName)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null, "Expected a test shader to be available.");
            return new Material(shader)
            {
                name = materialName,
            };
        }

        private static void AssertHasCode(StageValidationReport report, string code)
        {
            Assert.That(report.Issues.Any(issue => issue.Code == code), Is.True, $"Expected issue code '{code}'.{Environment.NewLine}{FormatIssues(report)}");
        }

        private static void AssertNoBoardTileCatalogIssues(StageValidationReport report)
        {
            var boardTileCatalogIssues = report.Issues
                .Where(issue => issue.Code.StartsWith("presentation.board-tile.catalog.", StringComparison.Ordinal))
                .Select(issue => $"{issue.Code}: {issue.Message}")
                .ToArray();

            Assert.That(boardTileCatalogIssues, Is.Empty);
        }

        private static void AssertNoBoardTileOverrideErrors(StageValidationReport report)
        {
            var boardTileOverrideErrors = report.Issues
                .Where(issue =>
                    issue.Severity == StageValidationSeverity.Error &&
                    issue.Code.StartsWith("presentation.board-tile.override-", StringComparison.Ordinal))
                .Select(issue => $"{issue.Code}: {issue.Message}")
                .ToArray();

            Assert.That(boardTileOverrideErrors, Is.Empty);
        }

        private static string FormatIssues(StageValidationReport report)
        {
            return string.Join(
                Environment.NewLine,
                report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void DestroyObjects(params UnityEngine.Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[i]);
                }
            }
        }
    }
}
