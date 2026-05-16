using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public void StageRuntimeBuilder_DoesNotReferencePresentationBindingTypes()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Runtime/StageRuntimeBuilder.cs");
            var forbiddenTokens = new[]
            {
                "EnemyPresentationBinding",
                "StaticEntityPresentationBinding",
                "TileFeaturePresentationBinding",
                "VisualPrefab",
                "PresentationCatalog",
                "BackgroundPrefab",
                "PreviewSprite",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(
                    source.Contains(token, StringComparison.Ordinal),
                    Is.False,
                    $"StageRuntimeBuilder must remain gameplay-only and must not reference presentation token '{token}'.");
            }

            Assert.That(source, Does.Contain(nameof(EnemyAiProfileOverride)));
        }

        [Test]
        public void StageRuntimeBuilder_DoesNotReferenceGameplayHostNamespace()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Runtime/StageRuntimeBuilder.cs");
            var forbiddenTokens = new[]
            {
                "using Game.Feature.Gameplay.Host;",
                "Game.Feature.Gameplay.Host.",
                "EnemyPresentationBinding",
                "StaticEntityPresentationBinding",
                "TileFeaturePresentationBinding",
                "VisualPrefab",
                "PresentationCatalog",
                "BackgroundPrefab",
                "PreviewSprite",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(
                    source.Contains(token, StringComparison.Ordinal),
                    Is.False,
                    $"StageRuntimeBuilder must remain gameplay-only and must not reference host/presentation token '{token}'.");
            }

            Assert.That(source, Does.Contain(nameof(EnemyAiProfileOverride)));
        }

        [Test]
        public void EnemyAiProfileOverride_LivesInGameplayEntitiesNamespace()
        {
            Assert.That(
                typeof(EnemyAiProfileOverride).Namespace,
                Is.EqualTo("Game.Feature.Gameplay.Entities"));
        }

        [Test]
        public void StageRuntimeBuildResult_PublicSurface_IsGameplayOnly()
        {
            var type = typeof(StageRuntimeBuildResult);
            var forbiddenFragments = new[]
            {
                "Presentation",
                "Prefab",
                "View",
                "Binding",
                "Catalog",
                "Visual",
            };

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                AssertPublicNameIsGameplayOnly(property.Name, $"{type.Name} property", forbiddenFragments);
                AssertPublicTypeIsGameplayOnly(
                    property.PropertyType,
                    $"{type.Name}.{property.Name}",
                    forbiddenFragments);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                AssertPublicNameIsGameplayOnly(field.Name, $"{type.Name} field", forbiddenFragments);
                AssertPublicTypeIsGameplayOnly(field.FieldType, $"{type.Name}.{field.Name}", forbiddenFragments);
            }

            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    AssertPublicNameIsGameplayOnly(
                        parameter.Name,
                        $"{type.Name} constructor parameter",
                        forbiddenFragments);
                    AssertPublicTypeIsGameplayOnly(
                        parameter.ParameterType,
                        $"{type.Name} constructor parameter '{parameter.Name}'",
                        forbiddenFragments);
                }
            }

            foreach (var method in type.GetMethods(
                         BindingFlags.Public |
                         BindingFlags.Instance |
                         BindingFlags.Static |
                         BindingFlags.DeclaredOnly))
            {
                AssertPublicNameIsGameplayOnly(method.Name, $"{type.Name} method", forbiddenFragments);
                AssertPublicTypeIsGameplayOnly(method.ReturnType, $"{type.Name}.{method.Name} return", forbiddenFragments);
                foreach (var parameter in method.GetParameters())
                {
                    AssertPublicNameIsGameplayOnly(
                        parameter.Name,
                        $"{type.Name}.{method.Name} parameter",
                        forbiddenFragments);
                    AssertPublicTypeIsGameplayOnly(
                        parameter.ParameterType,
                        $"{type.Name}.{method.Name} parameter '{parameter.Name}'",
                        forbiddenFragments);
                }
            }

            foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public))
            {
                AssertPublicNameIsGameplayOnly(nestedType.Name, $"{type.Name} nested type", forbiddenFragments);
                AssertPublicTypeIsGameplayOnly(nestedType, $"{type.Name}.{nestedType.Name}", forbiddenFragments);
            }
        }

        [Test]
        public void StagePresentationAssembler_OwnsPresentationBindingNormalization()
        {
            const string assemblerPath = "Assets/_Features/Stages/Runtime/Presentation/StagePresentationAssemblers.cs";
            const string normalizerPath = "Assets/_Features/Stages/Runtime/Presentation/StagePresentationBindingNormalizer.cs";
            var builderSource = File.ReadAllText("Assets/_Features/Stages/Runtime/StageRuntimeBuilder.cs");
            var assemblerSource = File.ReadAllText(assemblerPath);
            var normalizerSource = File.ReadAllText(normalizerPath);
            var hostFactorySource =
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs");
            var stageInstallerSource =
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplayShowcaseInstallerBase.cs");

            Assert.That(typeof(StagePresentationBindingNormalizer).Namespace, Is.EqualTo(typeof(StagePresentationAssembler).Namespace));
            Assert.That(normalizerSource, Does.Contain("NormalizeEnemyBindings"));
            Assert.That(normalizerSource, Does.Contain("NormalizeStaticEntityBindings"));
            Assert.That(assemblerSource, Does.Contain("StagePresentationBindingNormalizer.NormalizeEnemyBindings"));
            Assert.That(assemblerSource, Does.Contain("StagePresentationBindingNormalizer.NormalizeStaticEntityBindings"));
            Assert.That(builderSource, Does.Not.Contain("PresentationBindingComparer"));
            Assert.That(builderSource, Does.Not.Contain("EnemyPresentationBindings.Sort"));
            Assert.That(builderSource, Does.Not.Contain("StaticEntityPresentationBindings.Sort"));
            Assert.That(hostFactorySource, Does.Not.Contain("EnemyPresentationBindings.Sort"));
            Assert.That(hostFactorySource, Does.Not.Contain("StaticEntityPresentationBindings.Sort"));
            Assert.That(hostFactorySource, Does.Not.Contain("OrderBy(binding"));
            Assert.That(stageInstallerSource, Does.Not.Contain("EnemyPresentationBindings.Sort"));
            Assert.That(stageInstallerSource, Does.Not.Contain("StaticEntityPresentationBindings.Sort"));
            Assert.That(stageInstallerSource, Does.Not.Contain("OrderBy(binding"));
        }

        [Test]
        public void StageRuntimeBuilder_DoesNotAcceptPresentationInputs()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Runtime/StageRuntimeBuilder.cs");

            Assert.That(source, Does.Not.Contain("Build(StagePresentationDefinition"));
            Assert.That(source, Does.Not.Contain("Build(StageDefinition gameplayDefinition, StagePresentationDefinition"));
            Assert.That(source, Does.Not.Contain("StagePresentationResolvedData"));
            Assert.That(source, Does.Not.Contain("StageSceneCompositionData"));
            Assert.That(source, Does.Not.Contain("StageSceneCompositionAssembler"));
        }

        [Test]
        public void StagePresentationBindingNormalizer_RemainsBriteNormalizerOnly()
        {
            var source =
                File.ReadAllText("Assets/_Features/Stages/Runtime/Presentation/StagePresentationBindingNormalizer.cs");
            var forbiddenTokens = new[]
            {
                "StageRuntimeBuilder",
                "StageRuntimeBuildResult",
                "GameplayHostRuntimeFactory",
                "StageBackedGameplayShowcaseInstallerBase",
                "AudioRuntime",
                "IAudioService",
                "BgmFlow",
                "StageCompletionReadModel",
                "StageNavigationRequest",
                "WorldState",
                "TickRunner",
                "PrefabUtility",
                "AssetDatabase",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(
                    source.Contains(token, StringComparison.Ordinal),
                    Is.False,
                    $"StagePresentationBindingNormalizer must stay B-lite and must not reference '{token}'.");
            }

            Assert.That(source, Does.Contain("NormalizeEnemyBindings"));
            Assert.That(source, Does.Contain("NormalizeStaticEntityBindings"));
            Assert.That(source, Does.Contain("CloneTileFeatureBindingsPreserveOrder"));
        }

        [Test]
        public void StageBackedHostComposition_PassesGameplayAndPresentationSeparately()
        {
            var installerSource =
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplayShowcaseInstallerBase.cs");
            var compositionSource =
                File.ReadAllText("Assets/_Features/Stages/Runtime/Presentation/StagePresentationAssemblers.cs");
            var hostFactorySource =
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs");
            var buildResultSource = File.ReadAllText("Assets/_Features/Stages/Runtime/StageRuntimeBuildResult.cs");

            Assert.That(installerSource, Does.Contain("var buildResult = StageRuntimeBuilder.Build"));
            Assert.That(installerSource, Does.Contain("var resolvedPresentation = StagePresentationAssembler.Resolve"));
            Assert.That(installerSource, Does.Contain("StageSceneCompositionAssembler.Compose(buildResult, resolvedPresentation)"));
            Assert.That(compositionSource, Does.Contain("new StageSceneCompositionData(gameplayBuildResult, presentationData)"));
            Assert.That(buildResultSource, Does.Not.Contain("EnemyPresentationBindings"));
            Assert.That(buildResultSource, Does.Not.Contain("StaticEntityPresentationBindings"));
            Assert.That(buildResultSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(hostFactorySource, Does.Not.Contain("EnemyPresentationBindings.Sort"));
            Assert.That(hostFactorySource, Does.Not.Contain("StaticEntityPresentationBindings.Sort"));
            Assert.That(hostFactorySource, Does.Not.Contain("OrderBy(binding"));
            Assert.That(installerSource, Does.Not.Contain("EnemyPresentationBindings.Sort"));
            Assert.That(installerSource, Does.Not.Contain("StaticEntityPresentationBindings.Sort"));
            Assert.That(installerSource, Does.Not.Contain("OrderBy(binding"));
        }

        [Test]
        public void StageResultUi_DoesNotDependOnStageRuntimeBuildResult()
        {
            var uiSources = Directory.GetFiles("Assets/_Features/UI", "*.cs", SearchOption.AllDirectories);
            var mapperSource =
                File.ReadAllText("Assets/_Features/UI/UI_Application/Runtime/StageCompletionPayloadMappers.cs");

            Assert.That(mapperSource, Does.Contain("StageCompletionReadModel"));
            foreach (var sourcePath in uiSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(
                    source.Contains("StageRuntimeBuildResult", StringComparison.Ordinal),
                    Is.False,
                    $"UI must consume stage result read models, not StageRuntimeBuildResult: {sourcePath}");
            }
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
        public void BoardTileOverride_DoesNotOwnBaseTileReplacement()
        {
            var replaceBaseTile = "Replace" + "BaseTile";
            var boardTileSources = new[]
            {
                "Assets/_Features/Stages/Runtime/Content/BoardTilePresentationCatalog.cs",
                "Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs",
            };

            foreach (var sourcePath in boardTileSources)
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
            var entityTypeTileFeature = "EntityType." + "TileFeature";
            var terrainFlagTileFeature = "Terrain" + "Flags." + "TileFeature";
            var boardTileTerrainFlag = "BoardTile" + "Terrain" + "Flags";
            var terrainFlagBoardTile = "Terrain" + "Flags" + "BoardTile";
            foreach (var sourcePath in allSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain(entityTypeTileFeature), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlagTileFeature), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileTerrainFlag), sourcePath);
                Assert.That(source, Does.Not.Contain(terrainFlagBoardTile), sourcePath);
            }
        }

        [Test]
        public void ReplaceBaseTile_DoesNotEnterStageDefinitionOrRuntimeBuildResult()
        {
            var forbiddenTokens = new[]
            {
                "Replace" + "BaseTile",
                "TileFeature" + "Visual" + "PlacementMode",
                "TileFeature" + "Visual" + "FootprintMode",
                "Placement" + "Mode",
            };
            var sourcePaths = new[]
            {
                "Assets/_Features/Stages/Runtime/StageDefinition.cs",
                "Assets/_Features/Stages/Runtime/StageRuntimeBuildResult.cs",
            };

            foreach (var sourcePath in sourcePaths)
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), sourcePath);
                }
            }
        }

        [Test]
        public void ReplaceBaseTile_DoesNotEnterBoardStateOrGameplayLoop()
        {
            var forbiddenTokens = new[]
            {
                "Replace" + "BaseTile",
                "TileFeature" + "Visual" + "PlacementMode",
                "TileFeature" + "Visual" + "FootprintMode",
            };
            var sourcePaths = Directory
                .GetFiles("Assets/_Features/Gameplay/Gameplay_Loop", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/_Features/Gameplay/Gameplay_BoardState", "*.cs", SearchOption.AllDirectories));

            foreach (var sourcePath in sourcePaths)
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), sourcePath);
                }
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

        private static void AssertPublicNameIsGameplayOnly(
            string name,
            string description,
            string[] forbiddenFragments)
        {
            foreach (var fragment in forbiddenFragments)
            {
                Assert.That(
                    (name ?? string.Empty).Contains(fragment, StringComparison.Ordinal),
                    Is.False,
                    $"{description} '{name}' must remain gameplay-only.");
            }
        }

        private static void AssertPublicTypeIsGameplayOnly(
            Type type,
            string description,
            string[] forbiddenFragments)
        {
            if (type == null || type == typeof(void))
            {
                return;
            }

            if (type.IsByRef || type.IsPointer || type.IsArray)
            {
                AssertPublicTypeIsGameplayOnly(type.GetElementType(), description, forbiddenFragments);
                return;
            }

            if (type == typeof(EnemyAiProfileOverride))
            {
                Assert.That(
                    type.Namespace,
                    Is.EqualTo("Game.Feature.Gameplay.Entities"),
                    $"{description} may expose EnemyAiProfileOverride only from the gameplay entities namespace.");
                return;
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    AssertPublicTypeIsGameplayOnly(argument, description, forbiddenFragments);
                }
            }

            var typeName = type.FullName ?? type.Name;
            foreach (var fragment in forbiddenFragments)
            {
                Assert.That(
                    typeName.Contains(fragment, StringComparison.Ordinal),
                    Is.False,
                    $"{description} must remain gameplay-only but exposes type '{typeName}'.");
            }
        }
    }
}
