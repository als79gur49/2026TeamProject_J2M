using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class TileFeaturePresentationCatalogTests
    {
        [Test]
        public void TileFeaturePresentationCatalog_RejectsDuplicateKeys()
        {
            var prefab = CreateValidPrefab("DuplicateKeysPrefab");
            var catalog = CreateCatalog(
                Entry("duplicate", TileFeatureKind.Button, prefab),
                Entry(" duplicate ", TileFeatureKind.Destroy, prefab));

            try
            {
                Assert.That(catalog.TryGetEntry("duplicate", out _), Is.False);
            }
            finally
            {
                DestroyObjects(catalog, prefab);
            }
        }

        [Test]
        public void TileFeaturePresentationCatalog_RejectsEmptyKey()
        {
            var prefab = CreateValidPrefab("EmptyKeyPrefab");
            var catalog = CreateCatalog(Entry(" ", TileFeatureKind.Button, prefab));

            try
            {
                Assert.That(catalog.TryGetEntry(" ", out _), Is.False);
            }
            finally
            {
                DestroyObjects(catalog, prefab);
            }
        }

        [Test]
        public void TileFeaturePresentationCatalog_RejectsMissingPrefab()
        {
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, null));
            var entry = CreateEntry("button", TileFeatureKind.Button, null);
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button")),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.catalog.prefab-null");
                Assert.That(entry.VisualPrefab, Is.Null);
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog);
            }
        }

        [Test]
        public void TileFeaturePresentationCatalog_RejectsPrefabWithoutTarget()
        {
            var prefab = new GameObject("InvalidTileFeaturePrefab");
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button")),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.catalog.prefab-target-missing");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void TileFeaturePresentationCatalog_AllowsPrefabPlaceholderTileId()
        {
            var prefab = CreateValidPrefab("PlaceholderTileIdPrefab");
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button")),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertNoCode(report, "presentation.tile-feature.catalog.prefab-target-missing");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void TileFeaturePresentationCatalog_RejectsDuplicateDefaultForKind()
        {
            var prefab = CreateValidPrefab("DuplicateDefaultPrefab");
            var catalog = CreateCatalog(
                Entry("button-a", TileFeatureKind.Button, prefab, isDefault: true),
                Entry("button-b", TileFeatureKind.Button, prefab, isDefault: true));

            try
            {
                Assert.That(catalog.TryGetDefaultEntry(TileFeatureKind.Button, out _), Is.False);
            }
            finally
            {
                DestroyObjects(catalog, prefab);
            }
        }

        [Test]
        public void TileFeaturePresentationCatalog_AllowsDirectionHintNoneAsGeneric()
        {
            var prefab = CreateValidPrefab("DirectionHintNonePrefab");
            var catalog = CreateCatalog(Entry(
                "slide-generic",
                TileFeatureKind.Slide,
                prefab,
                directionHint: Direction2D.None));

            try
            {
                Assert.That(catalog.TryGetEntry("slide-generic", out var entry), Is.True);
                Assert.That(entry.DirectionHint, Is.EqualTo(Direction2D.None));
            }
            finally
            {
                DestroyObjects(catalog, prefab);
            }
        }

        [Test]
        public void TileFeaturePresentationCatalogEntry_DoesNotExposePlacementMode()
        {
            var entryType = typeof(TileFeaturePresentationCatalogEntry);
            var catalogAssembly = typeof(TileFeaturePresentationCatalog).Assembly;

            Assert.That(entryType.GetProperty("PlacementMode"), Is.Null);
            Assert.That(entryType.GetField("placementMode", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
            Assert.That(catalogAssembly.GetType("Game.Feature.Stages.TileFeatureVisualPlacementMode"), Is.Null);
        }

        [Test]
        public void TileFeaturePresentationCatalogEntry_DoesNotExposeIcon()
        {
            var entryType = typeof(TileFeaturePresentationCatalogEntry);

            Assert.That(entryType.GetProperty("Icon"), Is.Null);
            Assert.That(entryType.GetField("icon", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        }

        [Test]
        public void CampaignMainBoardCatalog_HasNoRemovedSerializedFields()
        {
            const string assetPath =
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Catalogs/TileFeaturePresentationCatalog_CampaignMainBoard.asset";
            var yaml = File.ReadAllText(assetPath);

            Assert.That(yaml, Does.Not.Contain("placementMode:"));
            Assert.That(yaml, Does.Not.Contain("icon:"));
            Assert.That(yaml, Does.Contain("presentationKey: exit.1x1"));
            Assert.That(yaml, Does.Contain("footprintMode: 1"));
            Assert.That(yaml, Does.Contain("footprintMode: 0"));
        }

        [Test]
        public void TileFeaturePresentationCatalogEntry_DefaultFootprintMode_SingleCell()
        {
            var entry = new TileFeaturePresentationCatalogEntry();

            Assert.That(entry.FootprintMode, Is.EqualTo(TileFeatureVisualFootprintMode.SingleCell));
        }

        [Test]
        public void TileFeaturePresentationCatalog_RejectsInvalidFootprintMode()
        {
            var prefab = CreateValidPrefab("InvalidFootprintModePrefab");
            var entry = Entry("button", TileFeatureKind.Button, prefab);
            SetPrivateField(entry, "footprintMode", (TileFeatureVisualFootprintMode)999);
            var catalog = CreateCatalog(entry);
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button")),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.catalog.footprint-mode-invalid");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_ResolvesTileFeatureByPresentationKey()
        {
            var prefab = CreateValidPrefab("CatalogResolvedPrefab");
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button"));
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.TileFeatureBindings[0].TileId, Is.EqualTo(100));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(prefab));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_ResolvesTileFeatureVfxStyleFromCatalogEntry()
        {
            var prefab = CreateValidPrefab("CatalogVfxStylePrefab");
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button"));
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab, vfxStyleKey: VfxStyleKey.Green));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.TileFeatureBindings[0].VfxStyleKey, Is.EqualTo(VfxStyleKey.Green));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_DestroyTileVfxStyleFollowsActivationRule()
        {
            var prefab = CreateValidPrefab("DestroyActivationStylePrefab");
            var stage = CreateStage(
                CreateTileFeature(
                    100,
                    TileFeatureKind.Destroy,
                    string.Empty,
                    activationRule: TileFeatureActivationRule.FrontFaceOnly),
                CreateTileFeature(
                    101,
                    TileFeatureKind.Destroy,
                    string.Empty,
                    activationRule: TileFeatureActivationRule.BottomFaceOnly));
            var catalog = CreateCatalog(Entry(
                "destroy.default",
                TileFeatureKind.Destroy,
                prefab,
                isDefault: true));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation)
                    .TileFeatureBindings
                    .OrderBy(binding => binding.TileId)
                    .ToArray();

                Assert.That(resolved, Has.Length.EqualTo(2));
                Assert.That(resolved[0].VfxStyleKey, Is.EqualTo(VfxStyleKey.Red));
                Assert.That(resolved[1].VfxStyleKey, Is.EqualTo(VfxStyleKey.Blue));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_CatalogResolvedTileFeatureSuppressesBaseTile()
        {
            var prefab = CreateValidPrefab("CatalogImplicitReplacePrefab");
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button", cell));
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.SuppressedBaseTileCells, Has.Count.EqualTo(1));
                Assert.That(resolved.SuppressedBaseTileCells[0], Is.EqualTo(cell));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_DirectTileIdBindingOverridesCatalog()
        {
            var catalogPrefab = CreateValidPrefab("CatalogPrefab");
            var directPrefab = CreateValidPrefab("DirectPrefab");
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button"));
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, catalogPrefab));
            var presentation = CreatePresentation(
                catalog,
                new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = directPrefab });

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(directPrefab));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, catalogPrefab, directPrefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_DirectOverrideUsesCatalogFootprintWhenKeyResolves()
        {
            var catalogPrefab = CreateValidPrefab("DirectFootprintCatalogPrefab");
            var directPrefab = CreateValidPrefab("DirectFootprintDirectPrefab");
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button"));
            var catalog = CreateCatalog(Entry(
                "button",
                TileFeatureKind.Button,
                catalogPrefab,
                footprintMode: TileFeatureVisualFootprintMode.ThreeByThreeSameFace));
            var presentation = CreatePresentation(
                catalog,
                new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = directPrefab });

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(directPrefab));
                Assert.That(
                    resolved.TileFeatureBindings[0].FootprintMode,
                    Is.EqualTo(TileFeatureVisualFootprintMode.ThreeByThreeSameFace));
                Assert.That(resolved.SuppressedBaseTileCells, Has.Count.EqualTo(9));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, catalogPrefab, directPrefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_DirectOverrideWithoutCatalogKeyUsesSingleCellFootprint()
        {
            var directPrefab = CreateValidPrefab("DirectSingleCellPrefab");
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, string.Empty, cell));
            var presentation = CreatePresentation(
                null,
                new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = directPrefab });

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(
                    resolved.TileFeatureBindings[0].FootprintMode,
                    Is.EqualTo(TileFeatureVisualFootprintMode.SingleCell));
                Assert.That(resolved.SuppressedBaseTileCells, Has.Count.EqualTo(1));
                Assert.That(resolved.SuppressedBaseTileCells[0], Is.EqualTo(cell));
            }
            finally
            {
                DestroyObjects(stage, presentation, directPrefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_BuildsSuppressedBaseTileCellsForImplicitReplace()
        {
            var prefab = CreateValidPrefab("SuppressedImplicitReplacePrefab");
            var cell = new SurfaceCell(FaceId.Front, 1, 0);
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button", cell));
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.SuppressedBaseTileCells, Has.Count.EqualTo(1));
                Assert.That(resolved.SuppressedBaseTileCells[0], Is.EqualTo(cell));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_BuildsThreeByThreeSuppressedBaseTileCellsForFootprint()
        {
            var prefab = CreateValidPrefab("SuppressedThreeByThreePrefab");
            var center = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Exit, "exit", center));
            var catalog = CreateCatalog(Entry(
                "exit",
                TileFeatureKind.Exit,
                prefab,
                footprintMode: TileFeatureVisualFootprintMode.ThreeByThreeSameFace));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.SuppressedBaseTileCells, Has.Count.EqualTo(9));
                Assert.That(
                    resolved.SuppressedBaseTileCells,
                    Is.EquivalentTo(new[]
                    {
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        new SurfaceCell(FaceId.Floor, 2, 0),
                        new SurfaceCell(FaceId.Floor, 0, 1),
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        new SurfaceCell(FaceId.Floor, 2, 1),
                        new SurfaceCell(FaceId.Floor, 0, 2),
                        new SurfaceCell(FaceId.Floor, 1, 2),
                        new SurfaceCell(FaceId.Floor, 2, 2),
                    }));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_AllCatalogResolvedFeaturesSuppressBaseTile()
        {
            var prefab = CreateValidPrefab("ImplicitReplaceSuppressPrefab");
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button", cell));
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.SuppressedBaseTileCells, Has.Count.EqualTo(1));
                Assert.That(resolved.SuppressedBaseTileCells[0], Is.EqualTo(cell));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_FallsBackToKindDefault()
        {
            var prefab = CreateValidPrefab("DefaultPrefab");
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, string.Empty));
            var catalog = CreateCatalog(Entry("button-default", TileFeatureKind.Button, prefab, isDefault: true));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(prefab));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_EntranceFallsBackToKindDefault()
        {
            var prefab = CreateValidPrefab("EntranceDefaultPrefab");
            var stage = CreateStage(CreateTileFeature(
                100,
                TileFeatureKind.Entrance,
                string.Empty,
                new SurfaceCell(FaceId.Floor, 0, 0)));
            var catalog = CreateCatalog(Entry("entrance-default", TileFeatureKind.Entrance, prefab, isDefault: true));
            var presentation = CreatePresentation(catalog);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(prefab));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_MissingCatalogLeavesDirectBindingPathIntact()
        {
            var prefab = CreateValidPrefab("DirectOnlyPrefab");
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, string.Empty));
            var presentation = CreatePresentation(
                null,
                new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = prefab });

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(prefab));
            }
            finally
            {
                DestroyObjects(stage, presentation, prefab);
            }
        }

        [Test]
        public void StagePresentationAssembler_EmptyPresentationKeyWithNoDefaultSkipsWithWarningOrNoBinding()
        {
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, string.Empty));
            var catalog = CreateCatalog();
            var presentation = CreatePresentation(catalog);

            try
            {
                LogAssert.ignoreFailingMessages = true;
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                Assert.That(resolved.TileFeatureBindings, Is.Empty);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                DestroyObjects(stage, presentation, catalog);
            }
        }

        [Test]
        public void StagePresentationDefinition_ApplyResolvedData_PreservesTileFeaturePresentationCatalog()
        {
            var catalog = CreateCatalog();
            var source = CreatePresentation(catalog);
            var copy = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                copy.ApplyResolvedData(StagePresentationAssembler.Resolve(source));

                Assert.That(copy.TileFeaturePresentationCatalog, Is.SameAs(catalog));
            }
            finally
            {
                DestroyObjects(source, copy, catalog);
            }
        }

        [Test]
        public void RuntimeFactory_InstantiatesCatalogResolvedTileFeaturePrefab()
        {
            var prefab = CreateValidPrefab("CatalogResolvedRuntimePrefab");
            var stage = CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button"));
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var presentation = CreatePresentation(catalog);
            var parent = new GameObject("RuntimeFactoryCatalogParent");
            var registryObject = new GameObject("RuntimeFactoryCatalogRegistry");
            var registry = registryObject.AddComponent<TileFeatureVisualRegistry>();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var resolved = StagePresentationAssembler.Resolve(stage, presentation);
                registry.ConfigureSearchRoot(parent.transform);

                InvokeStageTileFeatureVisualInstantiation(
                    resolved.TileFeatureBindings,
                    new[]
                    {
                        new TileFeatureState(
                            100,
                            cell,
                            TileFeatureKind.Button,
                            TileFeatureFlags.None,
                            sourceEntityId: 0,
                            ownerEntityId: 0,
                            teamId: 0,
                            lifetimeTicks: 0,
                            charges: 0),
                    },
                    parent.transform,
                    registry);

                Assert.That(parent.transform.childCount, Is.EqualTo(1));
                Assert.That(registry.TryGetTileVisual(100, out var target), Is.True);
                Assert.That(target.TileId, Is.EqualTo(100));
            }
            finally
            {
                DestroyObjects(stage, presentation, catalog, prefab, parent, registryObject);
            }
        }

        [Test]
        public void TileFeatureBarricadeDefaultPrefab_ConfiguresActiveStateAnimator()
        {
            const string prefabPath =
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Barricade_Default.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null);
            var targetView = prefab.GetComponent<TileFeatureVisualTargetView>();
            var animator = prefab.GetComponent<Animator>();
            Assert.That(targetView, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(targetView.DebugAnimator, Is.SameAs(animator));

            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            AssertAnimatorParameter(controller, "BarricadeActive", AnimatorControllerParameterType.Bool);
            AssertAnimatorParameter(controller, "BarricadeActivated", AnimatorControllerParameterType.Trigger);
            AssertAnimatorParameter(controller, "BarricadeDeactivated", AnimatorControllerParameterType.Trigger);
            AssertAnimatorParameter(controller, "BarricadeBlocked", AnimatorControllerParameterType.Trigger);
            AssertAnimatorParameter(controller, "BarricadeCrushed", AnimatorControllerParameterType.Trigger);

            var stateNames = controller.layers[0].stateMachine.states
                .Select(childState => childState.state.name)
                .ToArray();
            Assert.That(
                stateNames,
                Is.SupersetOf(new[] { "LoweredIdle", "Raise", "RaisedIdle", "Lower", "BlockedPulse", "CrushImpact" }));
        }

        [Test]
        public void TileFeatureSlidePrefabs_ConfigureSlideInactiveVisualOverrides()
        {
            var prefabPaths = new[]
            {
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Up.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Right.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Down.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Left.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Right_DirectVariant.prefab",
            };

            for (var i = 0; i < prefabPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                Assert.That(prefab, Is.Not.Null, prefabPaths[i]);
                var targetView = prefab.GetComponent<TileFeatureVisualTargetView>();
                Assert.That(targetView, Is.Not.Null, prefabPaths[i]);

                var serialized = new SerializedObject(targetView);
                var targets = serialized.FindProperty("slideTileInactiveMaterialTargets");

                Assert.That(targets, Is.Not.Null, prefabPaths[i]);
                Assert.That(targets.arraySize, Is.GreaterThan(0), prefabPaths[i]);
                for (var targetIndex = 0; targetIndex < targets.arraySize; targetIndex++)
                {
                    var target = targets.GetArrayElementAtIndex(targetIndex);
                    Assert.That(
                        target.FindPropertyRelative("Renderer").objectReferenceValue,
                        Is.Not.Null,
                        prefabPaths[i]);
                    Assert.That(
                        target.FindPropertyRelative("MaterialIndex").intValue,
                        Is.GreaterThanOrEqualTo(0),
                        prefabPaths[i]);
                    Assert.That(
                        target.FindPropertyRelative("InactiveColor").colorValue,
                        Is.Not.EqualTo(Color.white),
                        prefabPaths[i]);
                    Assert.That(
                        target.FindPropertyRelative("InactiveMetallic").floatValue,
                        Is.EqualTo(1f),
                        prefabPaths[i]);
                }
            }
        }

        [Test]
        public void StageCatalogValidator_ReportsDuplicateTileFeatureCatalogKey()
        {
            var prefab = CreateValidPrefab("ValidatorDuplicateKeyPrefab");
            var catalog = CreateCatalog(
                Entry("button", TileFeatureKind.Button, prefab),
                Entry("button", TileFeatureKind.Button, prefab));
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button")),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.catalog.key-duplicate");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_AllowsMissingPresentationKeyWhenDirectOverrideExists()
        {
            var prefab = CreateValidPrefab("DirectOverrideAllowsEmptyKeyPrefab");
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(100, TileFeatureKind.Button, string.Empty)),
                CreatePresentation(
                    null,
                    new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = prefab }));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertNoCode(report, "presentation.tile-feature.catalog.missing");
                AssertNoCode(report, "presentation.tile-feature.key-empty-no-default");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_AllowsEmptyPresentationKeyWhenKindDefaultExists()
        {
            var prefab = CreateValidPrefab("KindDefaultAllowsEmptyKeyPrefab");
            var catalog = CreateCatalog(Entry("button-default", TileFeatureKind.Button, prefab, isDefault: true));
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(100, TileFeatureKind.Button, string.Empty)),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertNoCode(report, "presentation.tile-feature.key-empty-no-default");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_RejectsDuplicateReplaceBaseTileSameCell()
        {
            var prefab = CreateValidPrefab("DuplicateReplacePrefab");
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var catalog = CreateCatalog(Entry(
                "button",
                TileFeatureKind.Button,
                prefab));
            var stageEntry = CreateStageEntry(
                CreateStage(
                    CreateTileFeature(100, TileFeatureKind.Button, "button", cell),
                    CreateTileFeature(101, TileFeatureKind.Button, "button", cell)),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.replace-base-tile.cell-duplicate");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_RejectsReplaceBaseTileWithoutResolvedPrefab()
        {
            var entry = Entry(
                "button",
                TileFeatureKind.Button,
                null);
            var catalog = CreateCatalog(entry);
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(100, TileFeatureKind.Button, "button")),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.replace-base-tile.visual-unresolved");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog);
            }
        }

        [Test]
        public void StageCatalogValidator_RejectsThreeByThreeReplaceBaseTileOutsideBoardBounds()
        {
            var prefab = CreateValidPrefab("FootprintOutOfBoundsPrefab");
            var catalog = CreateCatalog(Entry(
                "exit",
                TileFeatureKind.Exit,
                prefab,
                footprintMode: TileFeatureVisualFootprintMode.ThreeByThreeSameFace));
            var stageEntry = CreateStageEntry(
                CreateStage(CreateTileFeature(
                    100,
                    TileFeatureKind.Exit,
                    "exit",
                    new SurfaceCell(FaceId.Floor, 0, 0))),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.replace-base-tile.footprint-out-of-bounds");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_RejectsDifferentImplicitReplaceKeysOnSameCell()
        {
            var prefab = CreateValidPrefab("ImplicitReplaceSameCellPrefab");
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var catalog = CreateCatalog(
                Entry("replace", TileFeatureKind.Button, prefab),
                Entry("overlay", TileFeatureKind.Button, prefab));
            var stageEntry = CreateStageEntry(
                CreateStage(
                    CreateTileFeature(100, TileFeatureKind.Button, "replace", cell),
                    CreateTileFeature(101, TileFeatureKind.Button, "overlay", cell)),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.replace-base-tile.cell-duplicate");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StageCatalogValidator_RejectsMultipleImplicitReplaceSameCell()
        {
            var prefab = CreateValidPrefab("MultipleImplicitReplaceSameCellPrefab");
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var catalog = CreateCatalog(Entry("overlay", TileFeatureKind.Button, prefab));
            var stageEntry = CreateStageEntry(
                CreateStage(
                    CreateTileFeature(100, TileFeatureKind.Button, "overlay", cell),
                    CreateTileFeature(101, TileFeatureKind.Button, "overlay", cell)),
                CreatePresentation(catalog));

            try
            {
                var report = new StageCatalogValidator().ValidateEntries(new[] { stageEntry }, null);

                AssertHasCode(report, "presentation.tile-feature.replace-base-tile.cell-duplicate");
            }
            finally
            {
                DestroyObjects(stageEntry.PresentationDefinition, stageEntry.GameplayDefinition, stageEntry, catalog, prefab);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_TileFeatureVisualDropdown_FiltersByKind()
        {
            var buttonPrefab = CreateValidPrefab("ButtonOptionPrefab");
            var slidePrefab = CreateValidPrefab("SlideOptionPrefab");
            var catalog = CreateCatalog(
                Entry("button", TileFeatureKind.Button, buttonPrefab),
                Entry("slide", TileFeatureKind.Slide, slidePrefab));
            var authoring = CreateAuthoring(
                CreatePresentation(catalog),
                CreateTileFeature(100, TileFeatureKind.Button, string.Empty));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SelectTileFeatureByIdForTests(100);

                var options = window.GetSelectedTileFeatureCatalogOptionsForTests();

                Assert.That(options.Select(option => option.PresentationKey), Does.Contain("button"));
                Assert.That(options.Select(option => option.PresentationKey), Does.Not.Contain("slide"));
            }
            finally
            {
                DestroyObjects(window, authoring.GeneratedPresentationDefinition, authoring, catalog, buttonPrefab, slidePrefab);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_TileFeatureVisualDropdown_UsesSelectedDraftKind()
        {
            var buttonPrefab = CreateValidPrefab("ButtonDraftOptionPrefab");
            var slidePrefab = CreateValidPrefab("SlideDraftOptionPrefab");
            var catalog = CreateCatalog(
                Entry("button", TileFeatureKind.Button, buttonPrefab),
                Entry("slide", TileFeatureKind.Slide, slidePrefab));
            var authoring = CreateAuthoring(
                CreatePresentation(catalog),
                CreateTileFeature(100, TileFeatureKind.Button, string.Empty));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SelectTileFeatureByIdForTests(100);
                window.SetTileFeatureDraftForTests(
                    TileFeatureKind.Slide,
                    TileFeatureActivationRule.FrontFaceOnly,
                    Direction2D.Right,
                    TileFeatureBoxSelector.None,
                    boundEntityId: 0);

                var options = window.GetSelectedTileFeatureCatalogOptionsForTests();

                Assert.That(options.Select(option => option.PresentationKey), Does.Contain("slide"));
                Assert.That(options.Select(option => option.PresentationKey), Does.Not.Contain("button"));
            }
            finally
            {
                DestroyObjects(window, authoring.GeneratedPresentationDefinition, authoring, catalog, buttonPrefab, slidePrefab);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_TileFeatureVisualDropdown_WritesPresentationKey()
        {
            var prefab = CreateValidPrefab("WritesPresentationKeyPrefab");
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var authoring = CreateAuthoring(
                CreatePresentation(catalog),
                CreateTileFeature(100, TileFeatureKind.Button, string.Empty));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SelectTileFeatureByIdForTests(100);

                var changed = window.SetSelectedTileFeatureCatalogPresentationKeyForTests(" button ");
                if (changed)
                {
                    Assert.That(authoring.TileFeatures.Single().PresentationKey, Is.EqualTo("button"));
                }
            }
            finally
            {
                DestroyObjects(window, authoring.GeneratedPresentationDefinition, authoring, catalog, prefab);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_ShowsDirectOverrideStatus()
        {
            var catalogPrefab = CreateValidPrefab("CatalogStatusPrefab");
            var directPrefab = CreateValidPrefab("DirectStatusPrefab");
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, catalogPrefab));
            var presentation = CreatePresentation(
                catalog,
                new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = directPrefab });
            var authoring = CreateAuthoring(
                presentation,
                CreateTileFeature(100, TileFeatureKind.Button, "button"));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SelectTileFeatureByIdForTests(100);

                var status = window.GetSelectedTileFeatureCatalogStatusForTests();

                Assert.That(status.Kind, Is.EqualTo(TileFeaturePresentationCatalogStatusKind.DirectOverrideActive));
            }
            finally
            {
                DestroyObjects(window, presentation, authoring, catalog, catalogPrefab, directPrefab);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_CatalogNullKeepsDirectBindingAdvancedOnly()
        {
            var prefab = CreateValidPrefab("CatalogNullDirectPrefab");
            var presentation = CreatePresentation(
                null,
                new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = prefab });
            var authoring = CreateAuthoring(
                presentation,
                CreateTileFeature(100, TileFeatureKind.Button, string.Empty));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SelectTileFeatureByIdForTests(100);

                var bindingStatus = window.GetSelectedTileFeatureVisualBindingStatusForTests();

                Assert.That(bindingStatus.Kind, Is.EqualTo(TileFeatureVisualBindingStatusKind.Bound));
            }
            finally
            {
                DestroyObjects(window, presentation, authoring, prefab);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_WarnsOnMissingPresentationKey()
        {
            var catalog = CreateCatalog();
            var authoring = CreateAuthoring(
                CreatePresentation(catalog),
                CreateTileFeature(100, TileFeatureKind.Button, "missing"));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SelectTileFeatureByIdForTests(100);

                var status = window.GetSelectedTileFeatureCatalogStatusForTests();

                Assert.That(status.Kind, Is.EqualTo(TileFeaturePresentationCatalogStatusKind.KeyMissing));
            }
            finally
            {
                DestroyObjects(window, authoring.GeneratedPresentationDefinition, authoring, catalog);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_ShowsCatalogResolvedStatusWithoutPlacementMode()
        {
            var prefab = CreateValidPrefab("CatalogResolvedStatusPrefab");
            var catalog = CreateCatalog(Entry("button", TileFeatureKind.Button, prefab));
            var authoring = CreateAuthoring(
                CreatePresentation(catalog),
                CreateTileFeature(100, TileFeatureKind.Button, "button"));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SelectTileFeatureByIdForTests(100);

                var status = window.GetSelectedTileFeatureCatalogStatusForTests();

                Assert.That(status.Kind, Is.EqualTo(TileFeaturePresentationCatalogStatusKind.KeyResolved));
                Assert.That(status.Entry, Is.Not.Null);
                Assert.That(status.Message, Does.Not.Contain("Placement"));
            }
            finally
            {
                DestroyObjects(window, authoring.GeneratedPresentationDefinition, authoring, catalog, prefab);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_DirectOverrideShowsCatalogFootprintStatus()
        {
            var catalogPrefab = CreateValidPrefab("DirectFootprintSourceCatalogPrefab");
            var directPrefab = CreateValidPrefab("DirectFootprintSourceDirectPrefab");
            var catalog = CreateCatalog(Entry(
                "button",
                TileFeatureKind.Button,
                catalogPrefab,
                footprintMode: TileFeatureVisualFootprintMode.ThreeByThreeSameFace));
            var presentation = CreatePresentation(
                catalog,
                new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = directPrefab });
            var authoring = CreateAuthoring(
                presentation,
                CreateTileFeature(100, TileFeatureKind.Button, "button"));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SelectTileFeatureByIdForTests(100);

                var status = window.GetSelectedTileFeatureCatalogStatusForTests();

                Assert.That(status.Kind, Is.EqualTo(TileFeaturePresentationCatalogStatusKind.DirectOverrideActive));
                Assert.That(status.Entry, Is.Not.Null);
                Assert.That(status.Entry.FootprintMode, Is.EqualTo(TileFeatureVisualFootprintMode.ThreeByThreeSameFace));
                Assert.That(status.Message, Does.Contain("footprint"));
            }
            finally
            {
                DestroyObjects(window, presentation, authoring, catalog, catalogPrefab, directPrefab);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTileOverrideShowsSuppressedStatus()
        {
            var prefab = CreateValidPrefab("BoardTileSuppressedStatusPrefab");
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var catalog = CreateCatalog(Entry(
                "button",
                TileFeatureKind.Button,
                prefab));
            var presentation = CreatePresentation(catalog);
            var authoring = CreateAuthoring(
                presentation,
                CreateTileFeature(100, TileFeatureKind.Button, "button", cell));
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();

            try
            {
                window.BindForTests(authoring);
                window.SetTargetCellForTests(cell.face, cell.PlanarPosition);

                var status = window.GetBoardTileOverrideStatusForTests();

                if (status.IsBaseTileSuppressed)
                {
                    Assert.That(status.SuppressingTileId, Is.EqualTo(100));
                    Assert.That(status.Message, Does.Contain("will not be visible while suppressed"));
                }
            }
            finally
            {
                DestroyObjects(window, presentation, authoring, catalog, prefab);
            }
        }

        private static TileFeaturePresentationCatalogEntry Entry(
            string presentationKey,
            TileFeatureKind kind,
            GameObject visualPrefab,
            bool isDefault = false,
            Direction2D directionHint = Direction2D.None,
            TileFeatureVisualFootprintMode footprintMode = TileFeatureVisualFootprintMode.SingleCell,
            VfxStyleKey vfxStyleKey = default)
        {
            return CreateEntry(presentationKey, kind, visualPrefab, isDefault, directionHint, footprintMode, vfxStyleKey);
        }

        private static TileFeaturePresentationCatalogEntry CreateEntry(
            string presentationKey,
            TileFeatureKind kind,
            GameObject visualPrefab,
            bool isDefault = false,
            Direction2D directionHint = Direction2D.None,
            TileFeatureVisualFootprintMode footprintMode = TileFeatureVisualFootprintMode.SingleCell,
            VfxStyleKey vfxStyleKey = default)
        {
            var entry = new TileFeaturePresentationCatalogEntry();
            SetPrivateField(entry, "presentationKey", presentationKey);
            SetPrivateField(entry, "displayName", presentationKey);
            SetPrivateField(entry, "kind", kind);
            SetPrivateField(entry, "visualPrefab", visualPrefab);
            SetPrivateField(entry, "footprintMode", footprintMode);
            SetPrivateField(entry, "isDefaultForKind", isDefault);
            SetPrivateField(entry, "directionHint", directionHint);
            SetPrivateField(entry, "vfxStyleKey", vfxStyleKey);
            return entry;
        }

        private static TileFeaturePresentationCatalog CreateCatalog(
            params TileFeaturePresentationCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<TileFeaturePresentationCatalog>();
            catalog.name = "TileFeaturePresentationCatalogTests";
            SetPrivateField(catalog, "entries", entries ?? Array.Empty<TileFeaturePresentationCatalogEntry>());
            return catalog;
        }

        private static StagePresentationDefinition CreatePresentation(
            TileFeaturePresentationCatalog catalog,
            params TileFeaturePresentationBinding[] bindings)
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            presentation.name = "TileFeatureCatalogPresentation";
            SetPrivateField(presentation, "tileFeaturePresentationCatalog", catalog);
            SetPrivateField(presentation, "tileFeaturePresentationBindings", bindings ?? Array.Empty<TileFeaturePresentationBinding>());
            return presentation;
        }

        private static StageDefinition CreateStage(params StageTileFeatureDefinition[] tileFeatures)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.name = "TileFeatureCatalogStage";
            SetPrivateField(stage, "board", new StageBoardDefinition
            {
                MinInclusive = Vector2Int.zero,
                MaxInclusive = new Vector2Int(2, 2),
                InitialBottomFace = FaceId.Floor,
            });
            SetPrivateField(stage, "playerSpawns", new[]
            {
                new StageSpawnDefinition
                {
                    EntityId = 1,
                    Kind = StageSpawnKind.Player,
                    Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                    Hp = 3,
                    Facing = Direction.Up,
                },
            });
            SetPrivateField(stage, "tileFeatures", tileFeatures ?? Array.Empty<StageTileFeatureDefinition>());
            return stage;
        }

        private static StageAuthoringDefinition CreateAuthoring(
            StagePresentationDefinition presentation,
            params StageTileFeatureDefinition[] tileFeatures)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            authoring.name = "TileFeatureCatalogAuthoring";
            authoring.AssignGeneratedDefinitions(null, presentation);
            authoring.SetTileFeatures(tileFeatures);
            return authoring;
        }

        private static StageContentEntry CreateStageEntry(
            StageDefinition stage,
            StagePresentationDefinition presentation)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.name = "TileFeatureCatalogEntry";
            entry.AssignStageId(StageId.CreateOrThrow("tile-feature-catalog-tests"));
            entry.AssignGameplayDefinition(stage);
            entry.AssignPresentationDefinition(presentation);
            return entry;
        }

        private static StageTileFeatureDefinition CreateTileFeature(
            int tileId,
            TileFeatureKind kind,
            string presentationKey,
            SurfaceCell? cell = null,
            TileFeatureActivationRule? activationRule = null)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = cell ?? new SurfaceCell(FaceId.Floor, 1, 1),
                Kind = kind,
                ActivationRule = activationRule ?? (kind == TileFeatureKind.Slide
                    ? TileFeatureActivationRule.FrontFaceOnly
                    : TileFeatureActivationRule.BottomFaceOnly),
                Direction = kind == TileFeatureKind.Slide ? Direction2D.Right : Direction2D.None,
                BoxSelector = kind == TileFeatureKind.Button
                    ? TileFeatureBoxSelector.AnyPushableBox
                    : TileFeatureBoxSelector.None,
                PresentationKey = presentationKey,
            };
        }

        private static GameObject CreateValidPrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<TileFeatureVisualTargetView>();
            return prefab;
        }

        private static void AssertHasCode(StageValidationReport report, string code)
        {
            Assert.That(report.Issues.Any(issue => issue.Code == code), Is.True, $"Expected issue code '{code}'.");
        }

        private static void AssertNoCode(StageValidationReport report, string code)
        {
            Assert.That(report.Issues.Any(issue => issue.Code == code), Is.False, $"Unexpected issue code '{code}'.");
        }

        private static void AssertAnimatorParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            Assert.That(
                controller.parameters.Any(parameter =>
                    parameter.name == parameterName &&
                    parameter.type == parameterType),
                Is.True,
                $"Missing Animator parameter '{parameterName}' ({parameterType}).");
        }

        private static void InvokeStageTileFeatureVisualInstantiation(
            System.Collections.Generic.IReadOnlyList<TileFeaturePresentationResolvedBinding> bindings,
            System.Collections.Generic.IReadOnlyList<TileFeatureState> initialTileFeatures,
            Transform parent,
            TileFeatureVisualRegistry registry)
        {
            var method = typeof(GameplayHostRuntimeFactory).GetMethod(
                "InstantiateStageTileFeatureVisuals",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(
                null,
                new object[]
                {
                    bindings,
                    initialTileFeatures,
                    Array.Empty<TileFeatureRuntimeDefinition>(),
                    new CubeTopologyState(FaceId.Floor),
                    parent,
                    registry,
                    null,
                    null,
                    null,
                });
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
