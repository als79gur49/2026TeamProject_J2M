using System;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringArchitectureBoundaryTests
    {
        [Test]
        public void RuntimeAssembly_DoesNotReferenceUnityEditor()
        {
            var referenced = typeof(StageDefinition).Assembly.GetReferencedAssemblies();
            Assert.That(referenced.Any(assembly => assembly.Name == "UnityEditor"), Is.False);
        }

        [Test]
        public void EditorProviderRegistration_DoesNotAddUnityEditorReferenceToRuntime()
        {
            var validatorSource = File.ReadAllText("Assets/_Features/Stages/Runtime/Validation/StageCatalogValidator.cs");
            var validationTypeSource = File.ReadAllText("Assets/_Features/Stages/Runtime/Validation/StageValidationTypes.cs");

            Assert.That(validatorSource.Contains("UnityEditor", StringComparison.Ordinal), Is.False);
            Assert.That(validatorSource.Contains("AssetDatabase", StringComparison.Ordinal), Is.False);
            Assert.That(validationTypeSource.Contains("UnityEditor", StringComparison.Ordinal), Is.False);
            Assert.That(validationTypeSource.Contains("AssetDatabase", StringComparison.Ordinal), Is.False);

            var runtimeAuthoringValidationSources = Directory.GetFiles(
                "Assets/_Features/Stages/Runtime/Authoring/Validation",
                "*.cs",
                SearchOption.TopDirectoryOnly);
            foreach (var sourcePath in runtimeAuthoringValidationSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source.Contains("UnityEditor", StringComparison.Ordinal), Is.False, sourcePath);
                Assert.That(source.Contains("AssetDatabase", StringComparison.Ordinal), Is.False, sourcePath);
                Assert.That(source.Contains("Undo", StringComparison.Ordinal), Is.False, sourcePath);
                Assert.That(source.Contains("EditorWindow", StringComparison.Ordinal), Is.False, sourcePath);
                Assert.That(source.Contains("MenuItem", StringComparison.Ordinal), Is.False, sourcePath);
            }
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceStageEditorAuthoringNamespace()
        {
            var referenced = typeof(StageDefinition).Assembly.GetReferencedAssemblies();
            Assert.That(referenced.Any(assembly => assembly.Name == "Game.Feature.Stages.Editor"), Is.False);
            Assert.That(RuntimeSourceContains("Game.Feature.Stages.Editor"), Is.False);
        }

        [Test]
        public void RuntimeKindRegistry_DoesNotReferenceEditorOrColorTypes()
        {
            var registrySources = new[]
            {
                "Assets/_Features/Stages/Runtime/Authoring/StageAuthoringPresentationLane.cs",
                "Assets/_Features/Stages/Runtime/Authoring/StageAuthoringKindDescriptor.cs",
                "Assets/_Features/Stages/Runtime/Authoring/StageAuthoringKindRegistry.cs",
            };

            foreach (var sourcePath in registrySources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source.Contains("UnityEditor", StringComparison.Ordinal), Is.False, sourcePath);
                Assert.That(source.Contains("UnityEngine.Color", StringComparison.Ordinal), Is.False, sourcePath);
                Assert.That(source.Contains("Game.Feature.Stages.Editor", StringComparison.Ordinal), Is.False, sourcePath);
            }
        }

        [Test]
        public void StageCatalogValidator_DoesNotUseEditorGenerator()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Runtime/Validation/StageCatalogValidator.cs");
            Assert.That(source.Contains("StageAuthoringGenerator", StringComparison.Ordinal), Is.False);
        }

        [Test]
        public void EditorGenerator_IsEditorAssemblyOnly()
        {
            Assert.That(
                typeof(StageAuthoringGenerator).Assembly.GetName().Name,
                Is.EqualTo("Game.Feature.Stages.Editor"));
        }

        [Test]
        public void RuntimeResolver_DoesNotConsumeAuthoringDefinition()
        {
            var builder = File.ReadAllText("Assets/_Features/Stages/Runtime/StageRuntimeBuilder.cs");
            var resolver = File.ReadAllText("Assets/_Features/Stages/Runtime/Load/StageRuntimeContentResolver.cs");
            Assert.That(builder.Contains("AuthoringDefinition", StringComparison.Ordinal), Is.False);
            Assert.That(resolver.Contains("AuthoringDefinition", StringComparison.Ordinal), Is.False);
        }

        [Test]
        public void OverlayFeature_NotModeledAsEntityKind()
        {
            const string forbiddenName = "Tile" + "Feature";
            Assert.That(Enum.GetNames(typeof(StageAuthoringEntityKind)), Does.Not.Contain(forbiddenName));
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain(forbiddenName));
        }

        [Test]
        public void OverlayFeature_CellFieldUsesSurfaceCell()
        {
            var field = typeof(StageTileFeatureDefinition).GetField(nameof(StageTileFeatureDefinition.Cell));

            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(SurfaceCell)));
        }

        [Test]
        public void StageDefinitionAndRuntimeBuildResult_DoNotContainTileFeatureVisualPrefab()
        {
            var stageDefinitionSource = File.ReadAllText("Assets/_Features/Stages/Runtime/StageDefinition.cs");
            var buildResultSource = File.ReadAllText("Assets/_Features/Stages/Runtime/StageRuntimeBuildResult.cs");
            var authoringDefinitionSource = File.ReadAllText("Assets/_Features/Stages/Runtime/Authoring/StageAuthoringDefinition.cs");

            Assert.That(stageDefinitionSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("TileFeaturePresentationCatalog"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(buildResultSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(buildResultSource, Does.Not.Contain("TileFeaturePresentationCatalog"));
            Assert.That(buildResultSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(authoringDefinitionSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(authoringDefinitionSource, Does.Not.Contain("TileFeaturePresentationCatalog"));
        }

        [Test]
        public void BoardTileCatalog_DoesNotEnterGameplayDefinitions()
        {
            var stageDefinitionSource = File.ReadAllText("Assets/_Features/Stages/Runtime/StageDefinition.cs");
            var buildResultSource = File.ReadAllText("Assets/_Features/Stages/Runtime/StageRuntimeBuildResult.cs");

            Assert.That(stageDefinitionSource, Does.Not.Contain("BoardTilePresentationCatalog"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("BoardTilePresentationOverride"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("BoardTileVisualRole"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("BoardTilePresentationCatalogEntry"));
            Assert.That(buildResultSource, Does.Not.Contain("BoardTilePresentationCatalog"));
            Assert.That(buildResultSource, Does.Not.Contain("BoardTilePresentationOverride"));
            Assert.That(buildResultSource, Does.Not.Contain("BoardTileVisualRole"));
            Assert.That(buildResultSource, Does.Not.Contain("BoardTilePresentationCatalogEntry"));
        }

        [Test]
        public void BoardTileOverride_RemainsPresentationOwned()
        {
            var stagePresentationDefinitionSource =
                File.ReadAllText("Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs");
            var tileFeatureCatalogSource =
                File.ReadAllText("Assets/_Features/Stages/Runtime/Content/TileFeaturePresentationCatalog.cs");

            Assert.That(stagePresentationDefinitionSource, Does.Contain("BoardTilePresentationOverride"));
            Assert.That(stagePresentationDefinitionSource, Does.Contain("BoardTilePresentationCatalog"));
            Assert.That(tileFeatureCatalogSource, Does.Not.Contain("BoardTilePresentationOverride"));
        }

        [Test]
        public void TileFeatureVisualBinding_RemainsStagePresentationDefinitionOwned()
        {
            var stagePresentationDefinitionSource =
                File.ReadAllText("Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs");

            Assert.That(stagePresentationDefinitionSource, Does.Contain("TileFeaturePresentationBinding"));
            Assert.That(stagePresentationDefinitionSource, Does.Contain("TileFeaturePresentationCatalog"));
            Assert.That(stagePresentationDefinitionSource, Does.Contain("VisualPrefab"));
            Assert.That(stagePresentationDefinitionSource, Does.Not.Contain("TileFeatureAudio"));
        }

        [Test]
        public void TileFeatureCatalog_DoesNotEnterGameplayLoopOrBoardState()
        {
            var gameplayLoopSources = Directory.GetFiles(
                "Assets/_Features/Gameplay/Gameplay_Loop",
                "*.cs",
                SearchOption.AllDirectories);
            var boardStateSources = Directory.GetFiles(
                "Assets/_Features/Gameplay/Gameplay_BoardState",
                "*.cs",
                SearchOption.AllDirectories);

            foreach (var sourcePath in gameplayLoopSources.Concat(boardStateSources))
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("TileFeaturePresentationCatalog"), sourcePath);
            }
        }

        [Test]
        public void BoardTileCatalog_DoesNotEnterGameplayLoopOrBoardState()
        {
            var gameplayLoopSources = Directory.GetFiles(
                "Assets/_Features/Gameplay/Gameplay_Loop",
                "*.cs",
                SearchOption.AllDirectories);
            var boardStateSources = Directory.GetFiles(
                "Assets/_Features/Gameplay/Gameplay_BoardState",
                "*.cs",
                SearchOption.AllDirectories);

            foreach (var sourcePath in gameplayLoopSources.Concat(boardStateSources))
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("BoardTilePresentationCatalog"), sourcePath);
                Assert.That(source, Does.Not.Contain("BoardTilePresentationOverride"), sourcePath);
            }
        }

        [Test]
        public void BoardTileOverride_DoesNotOpenBaseTileReplacement()
        {
            var allSources = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
            var replaceBaseTile = "Replace" + "BaseTile";
            foreach (var sourcePath in allSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain(replaceBaseTile), sourcePath);
            }
        }

        [Test]
        public void TerrainFlags_DoNotGainBoardVisualSemantics()
        {
            var allSources = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
            var terrainFlags = "Terrain" + "Flags";
            var boardTile = "Board" + "Tile";
            foreach (var sourcePath in allSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlags + ".Visual"), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlags + ".Board"), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlags + ".Tile"), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlags + ".Presentation"), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTile + terrainFlags), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlags + boardTile), sourcePath);
            }
        }

        [Test]
        public void BoardTileCatalog_DoesNotOpenForbiddenTileFeatureOrTerrainPolicy()
        {
            var tileFeatureCatalogSource =
                File.ReadAllText("Assets/_Features/Stages/Runtime/Content/TileFeaturePresentationCatalog.cs");
            Assert.That(tileFeatureCatalogSource, Does.Not.Contain("BoardTilePresentationCatalog"));

            var allSources = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
            var replaceBaseTile = "Replace" + "BaseTile";
            var tfvMode = "TileFeature" + "Visual" + "PlacementMode";
            var vPlacementMode = "Visual" + "PlacementMode";
            var entityTypeTileFeature = "EntityType." + "TileFeature";
            var terrainFlagTileFeature = "Terrain" + "Flags." + "TileFeature";
            var boardTileTerrainFlag = "BoardTile" + "Terrain" + "Flags";
            var terrainFlagBoardTile = "Terrain" + "Flags" + "BoardTile";
            foreach (var sourcePath in allSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain(replaceBaseTile), sourcePath);
                Assert.That(source, Does.Not.Contain(tfvMode), sourcePath);
                Assert.That(source, Does.Not.Contain(vPlacementMode), sourcePath);
                Assert.That(source, Does.Not.Contain(entityTypeTileFeature), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlagTileFeature), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileTerrainFlag), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlagBoardTile), sourcePath);
            }
        }

        [Test]
        public void TileFeatureCatalog_DoesNotOpenForbiddenRuntimeSurfaces()
        {
            var allSources = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
            var vector2 = "Vector2" + "Int";
            var tileFeaturePresentation = "TileFeature" + "Presentation";
            var pKey = "Presentation" + "Key";
            var entityType = "Entity" + "Type";
            var terrainFlags = "Terrain" + "Flags";
            var tileFeature = "Tile" + "Feature";
            foreach (var sourcePath in allSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain(vector2 + " " + tileFeaturePresentation), sourcePath);
                Assert.That(source, Does.Not.Contain(vector2 + " " + pKey), sourcePath);
                Assert.That(source, Does.Not.Contain(pKey + " " + vector2), sourcePath);
                Assert.That(source, Does.Not.Contain(entityType + "." + tileFeature), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlags + "." + tileFeature), sourcePath);
            }
        }

        [Test]
        public void TileFeatureVisualBindingEditor_DoesNotReferenceRuntimeGameplayMutationSurfaces()
        {
            var commandSource =
                File.ReadAllText("Assets/_Features/Stages/Editor/Authoring/StageAuthoringPresentationBindingCommands.cs");

            Assert.That(commandSource, Does.Contain("StagePresentationDefinition"));
            Assert.That(commandSource, Does.Not.Contain("StageRuntimeBuildResult"));
            Assert.That(commandSource, Does.Not.Contain("TickPipeline"));
            Assert.That(commandSource, Does.Not.Contain("WorldState"));
            Assert.That(commandSource, Does.Not.Contain("TileEffect"));
            Assert.That(commandSource, Does.Not.Contain("TileFeatureAudio"));
            Assert.That(commandSource, Does.Not.Contain("SetTileFeatures"));
        }

        [Test]
        public void ExitGoalHelperEditor_DoesNotReferenceRuntimeGameplayMutationSurfaces()
        {
            var commandSource =
                File.ReadAllText("Assets/_Features/Stages/Editor/Authoring/StageAuthoringExitGoalHelperCommands.cs");

            Assert.That(commandSource, Does.Contain("SetZones"));
            Assert.That(commandSource, Does.Not.Contain("StageRuntimeBuildResult"));
            Assert.That(commandSource, Does.Not.Contain("TickPipeline"));
            Assert.That(commandSource, Does.Not.Contain("WorldState"));
            Assert.That(commandSource, Does.Not.Contain("TileEffect"));
            Assert.That(commandSource, Does.Not.Contain("FinalizationBatch"));
            Assert.That(commandSource, Does.Not.Contain("ProjectedWorld"));
            Assert.That(commandSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(commandSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(commandSource, Does.Not.Contain("SetTileFeatures"));
            Assert.That(commandSource, Does.Not.Contain("StageObjectiveTracker"));
            Assert.That(commandSource, Does.Not.Contain("StageRuntimeBuilder"));
            Assert.That(commandSource, Does.Not.Contain("3x3"));
        }

        private static bool RuntimeSourceContains(string text)
        {
            return Directory
                .GetFiles("Assets/_Features/Stages/Runtime", "*.cs", SearchOption.AllDirectories)
                .Any(path => File.ReadAllText(path).Contains(text, StringComparison.Ordinal));
        }
    }
}
