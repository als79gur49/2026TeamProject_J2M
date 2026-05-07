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

            Assert.That(stageDefinitionSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(buildResultSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(buildResultSource, Does.Not.Contain("TileFeaturePresentationBinding"));
        }

        [Test]
        public void TileFeatureVisualBinding_RemainsStagePresentationDefinitionOwned()
        {
            var stagePresentationDefinitionSource =
                File.ReadAllText("Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs");

            Assert.That(stagePresentationDefinitionSource, Does.Contain("TileFeaturePresentationBinding"));
            Assert.That(stagePresentationDefinitionSource, Does.Contain("VisualPrefab"));
            Assert.That(stagePresentationDefinitionSource, Does.Not.Contain("TileFeatureAudio"));
        }

        private static bool RuntimeSourceContains(string text)
        {
            return Directory
                .GetFiles("Assets/_Features/Stages/Runtime", "*.cs", SearchOption.AllDirectories)
                .Any(path => File.ReadAllText(path).Contains(text, StringComparison.Ordinal));
        }
    }
}
