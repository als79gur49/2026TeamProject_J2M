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
        private const string GameplayTickPresentationCoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
        private const string GameplayHostRuntimeFactoryPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs";
        private const string GameplayLoopRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime";
        private const string StageDefinitionPath =
            "Assets/_Features/Stages/Runtime/StageDefinition.cs";
        private const string StageRuntimeBuildResultPath =
            "Assets/_Features/Stages/Runtime/StageRuntimeBuildResult.cs";
        private const string GameplayBoardStateRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_BoardState/Runtime";
        private static readonly string[] TileFeatureVisualRuntimePaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/ITileFeatureVisualRegistry.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualRegistry.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualTargetView.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualPresentationController.cs",
        };
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
        public void EntityType_DoesNotContainTileFeatureOrMoonBlock()
        {
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("TileFeature"));
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("MoonBlock"));
        }

        [Test]
        [Category("Core")]
        public void BoxCapabilities_DoNotContainMoonIdentity()
        {
            Assert.That(Enum.GetNames(typeof(BoxCapabilities)), Does.Not.Contain("Moon"));
            Assert.That(Enum.GetNames(typeof(BoxCapabilities)), Does.Not.Contain("MoonBlock"));
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
        public void TilePresentationCoordinatorRefresh_DoesNotReferenceAuthorityOrPlaybackTypes()
        {
            var source = File.ReadAllText(GetAbsolutePath(GameplayTickPresentationCoordinatorPath));
            var body = ExtractMethodBody(source, "private void RefreshTilePresentationRequests(");
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "PlayPlannedAudio",
                "PlayBlockBursts",
                "PlayHitEffect",
                "GameplayAudio",
                "GameplayVfx",
                "MonoBehaviour",
                "GameObject",
                "Prefab",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    body,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"Tile presentation request coordinator refresh must not reference authority, playback, or mutation token '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_DoesNotPlanTilePresentationRequests()
        {
            var absoluteDirectory = GetAbsolutePath(GameplayLoopRuntimePath);
            var pipelineFiles = Directory.GetFiles(absoluteDirectory, "TickPipeline*.cs", SearchOption.TopDirectoryOnly);
            Assert.That(pipelineFiles, Is.Not.Empty);

            for (var i = 0; i < pipelineFiles.Length; i++)
            {
                var source = File.ReadAllText(pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("TilePresentationRequestPlanner"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("TilePresentationRequest"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("TileFeatureVisual"), pipelineFiles[i]);
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVisualConsumerPath_DoesNotReferenceAuthorityOrPlaybackSurfaces()
        {
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "ProjectedWorld",
                "FinalizationBatch",
                "TickPipeline",
                "DeterminismHashBuilder",
                "TileEffectResolver",
                "TickPresentationData",
                "TileEvents",
                "GameplayAudio",
                "AudioMap",
                "ObjectiveStatusScreen",
                "Hud",
            };

            for (var pathIndex = 0; pathIndex < TileFeatureVisualRuntimePaths.Length; pathIndex++)
            {
                var source = File.ReadAllText(GetAbsolutePath(TileFeatureVisualRuntimePaths[pathIndex]));
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(forbiddenTokens[tokenIndex]),
                        $"{TileFeatureVisualRuntimePaths[pathIndex]} must not reference '{forbiddenTokens[tokenIndex]}'.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVisualTarget_DoesNotExposeGameplayMutationMethods()
        {
            var source = File.ReadAllText(GetAbsolutePath(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualTargetView.cs"));
            var forbiddenTokens = new[]
            {
                "AddTileFeature",
                "UpdateTileFeature",
                "RemoveTileFeature",
                "ApplyTo",
                "IWorldWriteContext",
                "IWorldStateMutationPort",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"Tile feature visual target must not expose gameplay mutation token '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVisualBinding_RemainsPresentationOwned()
        {
            var stageDefinitionSource = File.ReadAllText(GetAbsolutePath(StageDefinitionPath));
            var buildResultSource = File.ReadAllText(GetAbsolutePath(StageRuntimeBuildResultPath));

            Assert.That(stageDefinitionSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("GameObject"));
            Assert.That(buildResultSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(buildResultSource, Does.Not.Contain("TileFeaturePresentationResolvedBinding"));
            Assert.That(buildResultSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(buildResultSource, Does.Not.Contain("GameObject"));
        }

        [Test]
        [Category("Core")]
        public void GameplayBoardState_DoesNotReferenceStagePresentationDefinition()
        {
            var sources = Directory.GetFiles(GetAbsolutePath(GameplayBoardStateRuntimePath), "*.cs", SearchOption.TopDirectoryOnly);
            Assert.That(sources, Is.Not.Empty);

            for (var i = 0; i < sources.Length; i++)
            {
                var source = File.ReadAllText(sources[i]);
                Assert.That(source, Does.Not.Contain("StagePresentationDefinition"), sources[i]);
                Assert.That(source, Does.Not.Contain("TileFeaturePresentationBinding"), sources[i]);
                Assert.That(source, Does.Not.Contain("VisualPrefab"), sources[i]);
            }
        }

        [Test]
        [Category("Core")]
        public void StageTileFeatureVisualInstantiation_DoesNotReferenceAuthorityOrMutationTypes()
        {
            var source = File.ReadAllText(GetAbsolutePath(GameplayHostRuntimeFactoryPath));
            var body = ExtractMethodBody(source, "private static void InstantiateStageTileFeatureVisuals(");
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "TileEffectResolver",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    body,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"Stage TileFeature visual instantiation must not reference authority or mutation token '{forbiddenTokens[i]}'.");
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

        private static string ExtractMethodBody(string source, string signature)
        {
            var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), $"Missing method signature '{signature}'.");

            var bodyStart = source.IndexOf('{', signatureIndex);
            Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Missing method body for '{signature}'.");

            var depth = 0;
            for (var i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(bodyStart, i - bodyStart + 1);
                    }
                }
            }

            Assert.Fail($"Could not extract method body for '{signature}'.");
            return string.Empty;
        }
    }
}
