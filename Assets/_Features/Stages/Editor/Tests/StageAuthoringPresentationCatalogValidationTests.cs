using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringPresentationCatalogValidationTests
    {
        [Test]
        public void EnemyPlacementPresentationIdMissingFromCatalog_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Enemy,
                "missing-enemy-view",
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>(),
                enforceGeneratedSync: false);

            AssertHasCode(fixture.Validate(), "PresentationCatalog.EnemyPresentationIdMissing");
        }

        [Test]
        public void StaticPlacementPresentationIdMissingFromCatalog_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Box,
                "missing-box-view",
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: new[] { "box-view" },
                enforceGeneratedSync: false);

            AssertHasCode(fixture.Validate(), "PresentationCatalog.StaticPresentationIdMissing");
        }

        [Test]
        public void EnemyCatalogNullWithEnemyPlacement_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Enemy,
                "enemy-view",
                enemyCatalogIds: null,
                staticCatalogIds: Array.Empty<string>(),
                enforceGeneratedSync: false);

            AssertHasCode(fixture.Validate(), "PresentationCatalog.EnemyCatalogMissing");
        }

        [Test]
        public void StaticCatalogNullWithBoxPlacement_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Box,
                "box-view",
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: null,
                enforceGeneratedSync: false);

            AssertHasCode(fixture.Validate(), "PresentationCatalog.StaticCatalogMissing");
        }

        [Test]
        public void EmptyCatalogWithPlacement_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Enemy,
                "enemy-view",
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: Array.Empty<string>(),
                enforceGeneratedSync: false);

            AssertHasCode(fixture.Validate(), "PresentationCatalog.EnemyCatalogEmpty");
        }

        [Test]
        public void PlayerPlacement_DoesNotRequirePresentationCatalog()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Player,
                "player-view",
                enemyCatalogIds: null,
                staticCatalogIds: null,
                enforceGeneratedSync: true);

            var report = fixture.Validate();
            Assert.That(report.Issues.Any(IsPresentationIntegrityIssue), Is.False, FormatIssues(report));
        }

        [Test]
        public void PlacementPresentationIdEmpty_ReportsIssueWithoutAutoClearing()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Enemy,
                string.Empty,
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>(),
                enforceGeneratedSync: false);

            var before = fixture.Authoring.Placements[1].PresentationId;
            var report = fixture.Validate();

            AssertHasCode(report, "PresentationCatalog.EnemyPresentationIdEmpty");
            Assert.That(fixture.Authoring.Placements[1].PresentationId, Is.EqualTo(before));
        }

        [Test]
        public void EnemyCatalogDuplicatePresentationId_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view", " enemy-view " },
                staticCatalogIds: Array.Empty<string>());

            AssertHasCode(fixture.Validate(), "PresentationCatalog.DuplicatePresentationId");
        }

        [Test]
        public void StaticCatalogDuplicatePresentationId_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: new[] { "box-view", " box-view " });

            AssertHasCode(fixture.Validate(), "PresentationCatalog.DuplicatePresentationId");
        }

        [Test]
        public void CatalogEntryEmptyPresentationId_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { string.Empty },
                staticCatalogIds: Array.Empty<string>());

            AssertHasCode(fixture.Validate(), "PresentationCatalog.EmptyPresentationId");
        }

        [Test]
        public void CatalogEntryNullViewPrefab_ReportsExpectedSeverity()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>(),
                assignViewPrefabs: false);

            var issue = AssertHasCode(fixture.Validate(), "PresentationCatalog.ViewPrefabMissing");
            Assert.That(issue.Severity, Is.EqualTo(StageValidationSeverity.Error));
        }

        [Test]
        public void EnemyCatalogNullVfxProfile_IsAllowed()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>());

            var report = fixture.Validate();

            Assert.That(
                report.Issues.Any(issue => issue.Code.StartsWith("PresentationCatalog.EnemyVfxProfile", StringComparison.Ordinal)),
                Is.False,
                FormatIssues(report));
        }

        [Test]
        public void EnemyCatalogWrongFamilyVfxProfile_ReportsCatalogError()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>());
            var profile = CreateProfile(GameplayVfxFamily.Player);
            try
            {
                fixture.SetEnemyVfxProfile("enemy-view", profile);

                var issue = AssertHasCode(fixture.Validate(), "PresentationCatalog.EnemyVfxProfileFamilyMismatch");

                Assert.That(issue.Severity, Is.EqualTo(StageValidationSeverity.Error));
            }
            finally
            {
                Destroy(profile);
            }
        }

        [Test]
        public void EnemyCatalogInvalidVfxProfile_AggregatesDiagnostics()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>());
            var profile = CreateProfile(GameplayVfxFamily.Enemy, new VfxBindingDefinitionAsset[] { null });
            try
            {
                fixture.SetEnemyVfxProfile("enemy-view", profile);

                var issue = AssertHasCode(fixture.Validate(), "PresentationCatalog.EnemyVfxProfileInvalid");

                Assert.That(issue.Severity, Is.EqualTo(StageValidationSeverity.Error));
                Assert.That(issue.Message, Does.Contain("VFX_CUE_MAP_NULL_BINDING"));
            }
            finally
            {
                Destroy(profile);
            }
        }

        [Test]
        public void EnemyBindingReferencesMissingSpawn_ReportsOrphanEnemyBinding()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>());
            fixture.SetEnemyBindings(new[] { new EnemyPresentationBinding { EntityId = 99, PresentationId = "enemy-view" } });

            AssertHasCode(fixture.Validate(), "PresentationBinding.OrphanEnemyBinding");
        }

        [Test]
        public void StaticBindingReferencesMissingSpawn_ReportsOrphanStaticBinding()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: new[] { "box-view" });
            fixture.SetStaticBindings(new[] { new StaticEntityPresentationBinding { EntityId = 99, PresentationId = "box-view" } });

            AssertHasCode(fixture.Validate(), "PresentationBinding.OrphanStaticBinding");
        }

        [Test]
        public void EnemySpawnWithoutBinding_ReportsMissingEnemyBinding()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>());
            fixture.SetEnemyBindings(Array.Empty<EnemyPresentationBinding>());

            AssertHasCode(fixture.Validate(), "PresentationBinding.MissingEnemyBinding");
        }

        [Test]
        public void BoxSpawnWithoutBinding_ReportsMissingStaticBinding()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: new[] { "box-view" });
            fixture.SetStaticBindings(Array.Empty<StaticEntityPresentationBinding>());

            AssertHasCode(fixture.Validate(), "PresentationBinding.MissingStaticBinding");
        }

        [Test]
        public void StaticBindingReferencesEnemyEntity_ReportsWrongKind()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: new[] { "box-view" });
            fixture.SetStaticBindings(new[] { new StaticEntityPresentationBinding { EntityId = 2, PresentationId = "box-view" } });

            AssertHasCode(fixture.Validate(), "PresentationBinding.BindingReferencesWrongKind");
        }

        [Test]
        public void EnemyBindingReferencesBoxEntity_ReportsWrongKind()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: new[] { "box-view" });
            fixture.SetEnemyBindings(new[] { new EnemyPresentationBinding { EntityId = 3, PresentationId = "enemy-view" } });

            AssertHasCode(fixture.Validate(), "PresentationBinding.BindingReferencesWrongKind");
        }

        [Test]
        public void EnemyBindingPresentationIdMissingFromCatalog_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>());
            fixture.SetEnemyBindings(new[] { new EnemyPresentationBinding { EntityId = 2, PresentationId = "missing-enemy-view" } });

            AssertHasCode(fixture.Validate(), "PresentationCatalog.EnemyPresentationIdMissing");
        }

        [Test]
        public void StaticBindingPresentationIdMissingFromCatalog_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: new[] { "box-view" });
            fixture.SetStaticBindings(new[] { new StaticEntityPresentationBinding { EntityId = 3, PresentationId = "missing-box-view" } });

            AssertHasCode(fixture.Validate(), "PresentationCatalog.StaticPresentationIdMissing");
        }

        [Test]
        public void CampaignMainPresentationBindings_AllResolveAgainstAssignedCatalogs()
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath);
            Assert.That(
                provider,
                Is.Not.Null,
                $"Missing stage catalog provider at '{StageContentPaths.StageCatalogProviderAssetPath}'.");

            var report = new StageCatalogValidator().ValidateEntries(
                provider.LoadEntries(),
                provider.AliasTable,
                new StageCatalogValidationOptions { Timing = StageValidationTiming.TestOrCi });

            Assert.That(
                report.Issues.Any(IsMissingPresentationIdIssue),
                Is.False,
                FormatIssues(report));
        }

        [Test]
        public void CampaignMainBoardTilePresentationCatalog_UsesSingleGenericDefaultPrefab()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BoardTilePresentationCatalog>(
                StageContentPaths.SharedBoardPresentationRoot + "/Catalogs/BoardTilePresentationCatalog_CampaignMainBoard.asset");
            Assert.That(catalog, Is.Not.Null);

            Assert.That(catalog.Entries.Count, Is.EqualTo(1));
            var entry = catalog.Entries[0];
            Assert.That(entry.PresentationKey, Is.EqualTo("board.generic.default"));
            Assert.That(entry.Role, Is.EqualTo(BoardTileVisualRole.GenericDefault));
            Assert.That(entry.IsDefaultForRole, Is.True);
            Assert.That(entry.TilePrefab, Is.Not.Null);
            Assert.That(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.TilePrefab)),
                Is.EqualTo("80ad8bd596477764398e7080760f2985"));
        }

        [Test]
        public void CampaignMainBoardTileStyleCatalog_OnlyKeepsLevelPaintStyles()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BoardTileStyleCatalog>(
                StageContentPaths.SharedBoardPresentationRoot + "/Catalogs/BoardTileStyleCatalog_CampaignMainBoard.asset");
            Assert.That(catalog, Is.Not.Null);

            Assert.That(
                catalog.Entries.Select(entry => entry.StyleKey).ToArray(),
                Is.EqualTo(new[]
                {
                    "board.paint.level1",
                    "board.paint.level2",
                    "board.paint.level3",
                }));
        }

        [Test]
        public void CampaignMainBoardTileOverrides_DoNotReferenceRetiredPresentationOrStyleKeys()
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath);
            Assert.That(
                provider,
                Is.Not.Null,
                $"Missing stage catalog provider at '{StageContentPaths.StageCatalogProviderAssetPath}'.");

            var retiredPresentationKeys = new[]
            {
                "board.active.bottom",
                "board.active.front",
                "board.decorative.top",
                "board.decorative.back",
            };
            var retiredStyleKeys = new[]
            {
                "board.paint.neutral",
                "board.paint.path-base",
                "board.paint.objective-base",
                "board.paint.warning-base",
                "board.paint.level4",
            };

            var entries = provider.LoadEntries();
            var retiredPresentationReferences = entries
                .Where(entry => entry != null && entry.PresentationDefinition != null)
                .SelectMany(entry => entry.PresentationDefinition.BoardTilePresentationOverrides)
                .Where(boardOverride =>
                    boardOverride != null &&
                    retiredPresentationKeys.Contains(boardOverride.PresentationKey, StringComparer.Ordinal))
                .Select(boardOverride => $"{boardOverride.Cell}: {boardOverride.PresentationKey}")
                .ToArray();
            var retiredStyleReferences = entries
                .Where(entry => entry != null && entry.PresentationDefinition != null)
                .SelectMany(entry => entry.PresentationDefinition.BoardTilePaintOverrides)
                .Where(paintOverride =>
                    paintOverride != null &&
                    retiredStyleKeys.Contains(paintOverride.StyleKey, StringComparer.Ordinal))
                .Select(paintOverride => $"{paintOverride.Cell}: {paintOverride.StyleKey}")
                .ToArray();

            Assert.That(retiredPresentationReferences, Is.Empty);
            Assert.That(retiredStyleReferences, Is.Empty);
        }

        [Test]
        public void SpawnPresentationId_IsNotCanonicalPresentationBindingSource()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: new[] { "box-view" });

            Assert.That(
                fixture.HasEnemySpawnPresentationIdProperty(),
                Is.False,
                "Presentation ids are validated through StagePresentationDefinition bindings, not StageSpawnDefinition.");
        }

        [Test]
        public void TileFeatureVisualBindingPrefabWithoutConfigurableTarget_ReportsIssue()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: new[] { "box-view" });
            var invalidPrefab = new GameObject("InvalidTileFeatureVisualPrefab");
            try
            {
                fixture.SetGameplayTileFeatures(new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = 1,
                        Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                        Kind = TileFeatureKind.Button,
                        ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                        Direction = Direction2D.None,
                        BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                    },
                });
                fixture.SetTileFeatureBindings(new[]
                {
                    new TileFeaturePresentationBinding
                    {
                        TileId = 1,
                        VisualPrefab = invalidPrefab,
                    },
                });

                AssertHasCode(fixture.Validate(), "presentation.tile-feature.prefab-target-missing");
            }
            finally
            {
                Destroy(invalidPrefab);
            }
        }

        [Test]
        public void PresentationCatalogIssue_EnforceGeneratedSyncFalse_IsWarning()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Enemy,
                "missing-enemy-view",
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>(),
                enforceGeneratedSync: false);

            var issue = AssertHasCode(fixture.Validate(), "PresentationCatalog.EnemyPresentationIdMissing");
            Assert.That(issue.Severity, Is.EqualTo(StageValidationSeverity.Warning));
        }

        [Test]
        public void PresentationCatalogIssue_EnforceGeneratedSyncTrue_IsError()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Enemy,
                "missing-enemy-view",
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>(),
                enforceGeneratedSync: true);

            var issue = AssertHasCode(fixture.Validate(), "PresentationCatalog.EnemyPresentationIdMissing");
            Assert.That(issue.Severity, Is.EqualTo(StageValidationSeverity.Error));
        }

        [Test]
        public void BindingWrongKind_IsAlwaysError()
        {
            using var fixture = PresentationCatalogFixture.CreateBindingOnly(
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: new[] { "box-view" });
            fixture.SetEnemyBindings(new[] { new EnemyPresentationBinding { EntityId = 3, PresentationId = "enemy-view" } });

            var issue = AssertHasCode(fixture.Validate(), "PresentationBinding.BindingReferencesWrongKind");
            Assert.That(issue.Severity, Is.EqualTo(StageValidationSeverity.Error));
        }

        [Test]
        public void PresentationCatalogValidation_DoesNotTreatMetadataChangeAsIssue()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                SetString(fixture.Presentation, "displayName", "Edited Display");
                SetString(fixture.Presentation, "resultTitle", "Edited Result");

                var report = fixture.Validate();
                Assert.That(report.Issues.Any(IsPresentationIntegrityIssue), Is.False, FormatIssues(report));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void PresentationCatalogValidation_DoesNotRequirePlayerPresentation()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Player,
                string.Empty,
                enemyCatalogIds: Array.Empty<string>(),
                staticCatalogIds: Array.Empty<string>(),
                enforceGeneratedSync: false);

            var report = fixture.Validate();
            Assert.That(report.Issues.Any(IsPresentationIntegrityIssue), Is.False, FormatIssues(report));
        }

        [Test]
        public void PresentationCatalogValidation_DoesNotModifyAuthoringOrOutputs()
        {
            using var fixture = PresentationCatalogFixture.CreateAuthoringPlacement(
                StageAuthoringEntityKind.Enemy,
                "missing-enemy-view",
                enemyCatalogIds: new[] { "enemy-view" },
                staticCatalogIds: Array.Empty<string>(),
                enforceGeneratedSync: false);
            var placementBefore = fixture.Authoring.Placements[1].PresentationId;
            var bindingBefore = fixture.Presentation.EnemyPresentationBindings[0].PresentationId;

            fixture.Validate();

            Assert.That(fixture.Authoring.Placements[1].PresentationId, Is.EqualTo(placementBefore));
            Assert.That(fixture.Presentation.EnemyPresentationBindings[0].PresentationId, Is.EqualTo(bindingBefore));
        }

        private static StageValidationIssue AssertHasCode(StageValidationReport report, string code)
        {
            var issue = report.Issues.FirstOrDefault(found => found.Code == code);
            Assert.That(issue.Code, Is.EqualTo(code), FormatIssues(report));
            return issue;
        }

        private static bool IsPresentationIntegrityIssue(StageValidationIssue issue)
        {
            return issue.Code.StartsWith("PresentationCatalog.", StringComparison.Ordinal) ||
                   issue.Code.StartsWith("PresentationBinding.", StringComparison.Ordinal);
        }

        private static bool IsMissingPresentationIdIssue(StageValidationIssue issue)
        {
            return issue.Code == "PresentationCatalog.EnemyPresentationIdMissing" ||
                   issue.Code == "PresentationCatalog.StaticPresentationIdMissing";
        }

        private static void SetString(StagePresentationDefinition presentation, string fieldName, string value)
        {
            var serializedObject = new SerializedObject(presentation);
            serializedObject.FindProperty(fieldName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static VfxProfileAsset CreateProfile(
            GameplayVfxFamily family,
            VfxBindingDefinitionAsset[] bindings = null)
        {
            var profile = ScriptableObject.CreateInstance<VfxProfileAsset>();
            SetPrivateField(profile, "family", family);
            SetPrivateField(profile, "bindings", bindings ?? Array.Empty<VfxBindingDefinitionAsset>());
            return profile;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            typeof(T)
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static void Destroy(params UnityEngine.Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private static string FormatIssues(StageValidationReport report)
        {
            return string.Join(
                Environment.NewLine,
                report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }

        private sealed class PresentationCatalogFixture : IDisposable
        {
            private readonly UnityEngine.Object[] ownedObjects;

            private PresentationCatalogFixture(
                StageContentEntry entry,
                StageAuthoringDefinition authoring,
                StageDefinition gameplay,
                StagePresentationDefinition presentation,
                EnemyPresentationCatalog enemyCatalog,
                StaticEntityPresentationCatalog staticCatalog,
                GameplayEntityView[] viewPrefabs)
            {
                Entry = entry;
                Authoring = authoring;
                Gameplay = gameplay;
                Presentation = presentation;
                EnemyCatalog = enemyCatalog;
                ownedObjects = new UnityEngine.Object[]
                {
                    entry,
                    authoring,
                    gameplay,
                    presentation,
                    enemyCatalog,
                    staticCatalog,
                }.Concat((viewPrefabs ?? Array.Empty<GameplayEntityView>()).Select(view => view != null ? view.gameObject : null)).ToArray();
            }

            public StageContentEntry Entry { get; }

            public StageAuthoringDefinition Authoring { get; }

            public StageDefinition Gameplay { get; }

            public StagePresentationDefinition Presentation { get; }

            public EnemyPresentationCatalog EnemyCatalog { get; }

            public static PresentationCatalogFixture CreateAuthoringPlacement(
                StageAuthoringEntityKind kind,
                string presentationId,
                string[] enemyCatalogIds,
                string[] staticCatalogIds,
                bool enforceGeneratedSync)
            {
                var fixture = CreateBase(enemyCatalogIds, staticCatalogIds);
                fixture.Authoring.SetEnforceGeneratedSync(enforceGeneratedSync);
                fixture.Authoring.SetPlacements(new[]
                {
                    Placement("player", StageAuthoringEntityKind.Player, 1, string.Empty),
                    Placement("subject", kind, 2, presentationId),
                });
                fixture.Authoring.SetEntityIdMappings(new[]
                {
                    new StageAuthoringIdMapping { StableGuid = "player", EntityId = 1 },
                    new StageAuthoringIdMapping { StableGuid = "subject", EntityId = 2 },
                });

                if (kind == StageAuthoringEntityKind.Enemy)
                {
                    SetStageSpawns(
                        fixture.Gameplay,
                        enemySpawns: new[] { Spawn(2, StageSpawnKind.Enemy) },
                        boxSpawns: Array.Empty<StageSpawnDefinition>(),
                        wallSpawns: Array.Empty<StageSpawnDefinition>());
                    fixture.SetEnemyBindings(string.IsNullOrWhiteSpace(presentationId)
                        ? Array.Empty<EnemyPresentationBinding>()
                        : new[] { new EnemyPresentationBinding { EntityId = 2, PresentationId = presentationId } });
                }
                else if (kind == StageAuthoringEntityKind.Box || kind == StageAuthoringEntityKind.Wall)
                {
                    SetStageSpawns(
                        fixture.Gameplay,
                        enemySpawns: Array.Empty<StageSpawnDefinition>(),
                        boxSpawns: kind == StageAuthoringEntityKind.Box
                            ? new[] { Spawn(2, StageSpawnKind.Box) }
                            : Array.Empty<StageSpawnDefinition>(),
                        wallSpawns: kind == StageAuthoringEntityKind.Wall
                            ? new[] { Spawn(2, StageSpawnKind.Wall) }
                            : Array.Empty<StageSpawnDefinition>());
                    fixture.SetStaticBindings(string.IsNullOrWhiteSpace(presentationId)
                        ? Array.Empty<StaticEntityPresentationBinding>()
                        : new[] { new StaticEntityPresentationBinding { EntityId = 2, PresentationId = presentationId } });
                }
                else
                {
                    SetStageSpawns(
                        fixture.Gameplay,
                        enemySpawns: Array.Empty<StageSpawnDefinition>(),
                        boxSpawns: Array.Empty<StageSpawnDefinition>(),
                        wallSpawns: Array.Empty<StageSpawnDefinition>());
                }

                return fixture;
            }

            public static PresentationCatalogFixture CreateBindingOnly(
                string[] enemyCatalogIds,
                string[] staticCatalogIds,
                bool assignViewPrefabs = true)
            {
                var fixture = CreateBase(enemyCatalogIds, staticCatalogIds, assignViewPrefabs);
                fixture.Entry.AssignAuthoringDefinition(null);
                SetStageSpawns(
                    fixture.Gameplay,
                    enemySpawns: new[] { Spawn(2, StageSpawnKind.Enemy) },
                    boxSpawns: new[] { Spawn(3, StageSpawnKind.Box) },
                    wallSpawns: Array.Empty<StageSpawnDefinition>());
                fixture.SetEnemyBindings(new[] { new EnemyPresentationBinding { EntityId = 2, PresentationId = "enemy-view" } });
                fixture.SetStaticBindings(new[] { new StaticEntityPresentationBinding { EntityId = 3, PresentationId = "box-view" } });
                return fixture;
            }

            public StageValidationReport Validate()
            {
                return new StageCatalogValidator().ValidateEntries(
                    new[] { Entry },
                    aliasTable: null,
                    new StageCatalogValidationOptions { Timing = StageValidationTiming.TestOrCi });
            }

            public void SetEnemyBindings(EnemyPresentationBinding[] bindings)
            {
                var serializedObject = new SerializedObject(Presentation);
                var property = serializedObject.FindProperty("enemyPresentationBindings");
                property.arraySize = bindings.Length;
                for (var i = 0; i < bindings.Length; i++)
                {
                    var element = property.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("EntityId").intValue = bindings[i].EntityId;
                    element.FindPropertyRelative("PresentationId").stringValue = bindings[i].PresentationId;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetStaticBindings(StaticEntityPresentationBinding[] bindings)
            {
                var serializedObject = new SerializedObject(Presentation);
                var property = serializedObject.FindProperty("staticEntityPresentationBindings");
                property.arraySize = bindings.Length;
                for (var i = 0; i < bindings.Length; i++)
                {
                    var element = property.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("EntityId").intValue = bindings[i].EntityId;
                    element.FindPropertyRelative("PresentationId").stringValue = bindings[i].PresentationId;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetTileFeatureBindings(TileFeaturePresentationBinding[] bindings)
            {
                var serializedObject = new SerializedObject(Presentation);
                var property = serializedObject.FindProperty("tileFeaturePresentationBindings");
                property.arraySize = bindings.Length;
                for (var i = 0; i < bindings.Length; i++)
                {
                    var element = property.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("TileId").intValue = bindings[i].TileId;
                    element.FindPropertyRelative("VisualPrefab").objectReferenceValue = bindings[i].VisualPrefab;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetGameplayTileFeatures(StageTileFeatureDefinition[] tileFeatures)
            {
                var serializedObject = new SerializedObject(Gameplay);
                var property = serializedObject.FindProperty("tileFeatures");
                property.arraySize = tileFeatures.Length;
                for (var i = 0; i < tileFeatures.Length; i++)
                {
                    var element = property.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("TileId").intValue = tileFeatures[i].TileId;
                    var cell = element.FindPropertyRelative("Cell");
                    cell.FindPropertyRelative("face").intValue = (int)tileFeatures[i].Cell.face;
                    cell.FindPropertyRelative("x").intValue = tileFeatures[i].Cell.x;
                    cell.FindPropertyRelative("y").intValue = tileFeatures[i].Cell.y;
                    element.FindPropertyRelative("Kind").intValue = (int)tileFeatures[i].Kind;
                    element.FindPropertyRelative("ActivationRule").intValue = (int)tileFeatures[i].ActivationRule;
                    element.FindPropertyRelative("Direction").intValue = (int)tileFeatures[i].Direction;
                    element.FindPropertyRelative("BoxSelector").intValue = (int)tileFeatures[i].BoxSelector;
                    element.FindPropertyRelative("BoundEntityId").intValue = tileFeatures[i].BoundEntityId;
                    element.FindPropertyRelative("PresentationKey").stringValue = tileFeatures[i].PresentationKey ?? string.Empty;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            public bool HasEnemySpawnPresentationIdProperty()
            {
                var serializedObject = new SerializedObject(Gameplay);
                return serializedObject
                    .FindProperty("enemySpawns")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("PresentationId") != null;
            }

            public void SetEnemyVfxProfile(string presentationId, VfxProfileAsset profile)
            {
                var serializedObject = new SerializedObject(EnemyCatalog);
                var entries = serializedObject.FindProperty("entries");
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var element = entries.GetArrayElementAtIndex(i);
                    if (EnemyPresentationCatalogResolver.NormalizePresentationId(
                            element.FindPropertyRelative("PresentationId").stringValue) != presentationId)
                    {
                        continue;
                    }

                    element.FindPropertyRelative("VfxProfileAsset").objectReferenceValue = profile;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }

                Assert.Fail($"Enemy presentation id '{presentationId}' was not found.");
            }

            public void Dispose()
            {
                for (var i = 0; i < ownedObjects.Length; i++)
                {
                    if (ownedObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                    }
                }
            }

            private static PresentationCatalogFixture CreateBase(
                string[] enemyCatalogIds,
                string[] staticCatalogIds,
                bool assignViewPrefabs = true)
            {
                var entry = ScriptableObject.CreateInstance<StageContentEntry>();
                var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
                var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
                var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
                var viewPrefabs = new[]
                {
                    CreateViewPrefab("PresentationCatalogValidation_EnemyViewPrefab"),
                    CreateViewPrefab("PresentationCatalogValidation_StaticViewPrefab"),
                };
                var enemyCatalog = enemyCatalogIds != null
                    ? CreateEnemyCatalog(enemyCatalogIds, assignViewPrefabs ? viewPrefabs[0] : null)
                    : null;
                var staticCatalog = staticCatalogIds != null
                    ? CreateStaticCatalog(staticCatalogIds, assignViewPrefabs ? viewPrefabs[1] : null)
                    : null;

                entry.name = "presentation-catalog-validation_Entry";
                authoring.name = "presentation-catalog-validation_Authoring";
                gameplay.name = "presentation-catalog-validation";
                presentation.name = "presentation-catalog-validation_Presentation";
                entry.AssignStageId(StageId.CreateOrThrow("presentation-catalog-validation"));
                entry.AssignAuthoringDefinition(authoring);
                entry.AssignGameplayDefinition(gameplay);
                entry.AssignPresentationDefinition(presentation);
                authoring.AssignGeneratedDefinitions(gameplay, presentation);
                authoring.SetOwnerMetadata(entry, string.Empty);
                presentation.SetOwnerMetadata(entry, string.Empty);
                authoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(4, 4),
                    InitialBottomFace = FaceId.Floor,
                });
                authoring.SetObjective(StageObjectiveAuthoring.CreateDefault());
                SetStageSpawns(
                    gameplay,
                    enemySpawns: Array.Empty<StageSpawnDefinition>(),
                    boxSpawns: Array.Empty<StageSpawnDefinition>(),
                    wallSpawns: Array.Empty<StageSpawnDefinition>());
                SetPresentationCatalogs(presentation, enemyCatalog, staticCatalog);
                return new PresentationCatalogFixture(
                    entry,
                    authoring,
                    gameplay,
                    presentation,
                    enemyCatalog,
                    staticCatalog,
                    viewPrefabs);
            }

            private static StagePlacedEntityAuthoring Placement(
                string stableGuid,
                StageAuthoringEntityKind kind,
                int entityId,
                string presentationId)
            {
                return new StagePlacedEntityAuthoring
                {
                    StableGuid = stableGuid,
                    DisplayName = stableGuid,
                    Kind = kind,
                    Cell = new SurfaceCell(FaceId.Floor, entityId, 0),
                    Facing = Direction.Right,
                    Hp = 1,
                    BoxCapabilities = BoxCapabilities.Push,
                    EnemyAiMode = kind == StageAuthoringEntityKind.Enemy ? EnemyAiMode.Patrol : EnemyAiMode.None,
                    PresentationId = presentationId,
                };
            }

            private static StageSpawnDefinition Spawn(int entityId, StageSpawnKind kind)
            {
                return new StageSpawnDefinition
                {
                    EntityId = entityId,
                    Kind = kind,
                    Cell = new SurfaceCell(FaceId.Floor, entityId, 0),
                    Facing = Direction.Right,
                    Hp = 1,
                    BoxCapabilities = BoxCapabilities.Push,
                    EnemyAiMode = kind == StageSpawnKind.Enemy ? EnemyAiMode.Patrol : EnemyAiMode.None,
                };
            }

            private static void SetStageSpawns(
                StageDefinition gameplay,
                StageSpawnDefinition[] enemySpawns,
                StageSpawnDefinition[] boxSpawns,
                StageSpawnDefinition[] wallSpawns)
            {
                var serializedObject = new SerializedObject(gameplay);
                var board = serializedObject.FindProperty("board");
                board.FindPropertyRelative("MinInclusive").vector2IntValue = new Vector2Int(0, 0);
                board.FindPropertyRelative("MaxInclusive").vector2IntValue = new Vector2Int(4, 4);
                board.FindPropertyRelative("InitialBottomFace").intValue = (int)FaceId.Floor;
                WriteSpawns(serializedObject.FindProperty("playerSpawns"), new[] { Spawn(1, StageSpawnKind.Player) });
                WriteSpawns(serializedObject.FindProperty("enemySpawns"), enemySpawns);
                WriteSpawns(serializedObject.FindProperty("boxSpawns"), boxSpawns);
                WriteSpawns(serializedObject.FindProperty("wallSpawns"), wallSpawns);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            private static void WriteSpawns(SerializedProperty property, StageSpawnDefinition[] spawns)
            {
                property.arraySize = spawns.Length;
                for (var i = 0; i < spawns.Length; i++)
                {
                    var element = property.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("EntityId").intValue = spawns[i].EntityId;
                    element.FindPropertyRelative("Kind").intValue = (int)spawns[i].Kind;
                    var cell = element.FindPropertyRelative("Cell");
                    cell.FindPropertyRelative("face").intValue = (int)spawns[i].Cell.face;
                    cell.FindPropertyRelative("x").intValue = spawns[i].Cell.x;
                    cell.FindPropertyRelative("y").intValue = spawns[i].Cell.y;
                    element.FindPropertyRelative("Facing").intValue = (int)spawns[i].Facing;
                    element.FindPropertyRelative("Hp").intValue = spawns[i].Hp;
                    element.FindPropertyRelative("BoxCapabilities").intValue = (int)spawns[i].BoxCapabilities;
                    element.FindPropertyRelative("EnemyAiMode").intValue = (int)spawns[i].EnemyAiMode;
                }
            }

            private static EnemyPresentationCatalog CreateEnemyCatalog(string[] ids, GameplayEntityView viewPrefab)
            {
                var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
                var serializedObject = new SerializedObject(catalog);
                var entries = serializedObject.FindProperty("entries");
                entries.arraySize = ids.Length;
                for (var i = 0; i < ids.Length; i++)
                {
                    var element = entries.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("PresentationId").stringValue = ids[i];
                    element.FindPropertyRelative("ViewPrefab").objectReferenceValue = viewPrefab;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                return catalog;
            }

            private static StaticEntityPresentationCatalog CreateStaticCatalog(string[] ids, GameplayEntityView viewPrefab)
            {
                var catalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();
                var serializedObject = new SerializedObject(catalog);
                var entries = serializedObject.FindProperty("entries");
                entries.arraySize = ids.Length;
                for (var i = 0; i < ids.Length; i++)
                {
                    var element = entries.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("PresentationId").stringValue = ids[i];
                    element.FindPropertyRelative("ViewPrefab").objectReferenceValue = viewPrefab;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                return catalog;
            }

            private static void SetPresentationCatalogs(
                StagePresentationDefinition presentation,
                EnemyPresentationCatalog enemyCatalog,
                StaticEntityPresentationCatalog staticCatalog)
            {
                var serializedObject = new SerializedObject(presentation);
                serializedObject.FindProperty("enemyPresentationCatalog").objectReferenceValue = enemyCatalog;
                serializedObject.FindProperty("staticEntityPresentationCatalog").objectReferenceValue = staticCatalog;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            private static GameplayEntityView CreateViewPrefab(string name)
            {
                return new GameObject(name).AddComponent<GameplayEntityView>();
            }
        }
    }
}
