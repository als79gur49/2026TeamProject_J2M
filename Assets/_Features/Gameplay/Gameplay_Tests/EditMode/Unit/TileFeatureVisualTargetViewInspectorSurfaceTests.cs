using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualTargetViewInspectorSurfaceTests
    {
        private const string ProductionTileFeaturePrefabDirectory =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs";

        [Test]
        [Category("Extended")]
        public void TileFeatureVisualTargetView_SerializedSurface_HasNoFeatureSpecificStringUnityEventOrParticleFields()
        {
            var serializedFields = typeof(TileFeatureVisualTargetView)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(IsUnitySerializedField)
                .ToArray();

            var fieldNames = serializedFields.Select(field => field.Name).ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "tileId",
                    "cell",
                    "presentationRoot",
                    "visualRoot",
                    "primaryRenderer",
                    "iconRoot",
                    "labelRoot",
                    "authoredBindings",
                },
                fieldNames);

            Assert.That(serializedFields.Any(field => field.FieldType == typeof(string)), Is.False);
            Assert.That(serializedFields.Any(field => typeof(UnityEventBase).IsAssignableFrom(field.FieldType)), Is.False);
            Assert.That(serializedFields.Any(field => typeof(ParticleSystem).IsAssignableFrom(field.FieldType)), Is.False);
            Assert.That(serializedFields.Any(field => typeof(Animator).IsAssignableFrom(field.FieldType)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureVisualRuntime_DoesNotDeclareRemovedFeatureSpecificVisualInterfaces()
        {
            var forbiddenInterfaces = new[]
            {
                "IDestroyTileVisualTarget",
                "IDestroyTileActivatedVisualTarget",
                "IDestroyTileDeactivatedVisualTarget",
                "IDestroyTileActiveStateVisualTarget",
                "ITileFeatureActiveStateVisualTarget",
                "ISlideTileVisualTarget",
                "IBarricadeBlockedVisualTarget",
                "IBarricadeCrushedVisualTarget",
                "IBarricadeActivatedVisualTarget",
                "IBarricadeDeactivatedVisualTarget",
                "IBarricadeActiveStateVisualTarget",
                "IExitOpenedVisualTarget",
                "IExitEnteredVisualTarget",
                "IExitOpenStateVisualTarget",
                "IMoonBlockGeneratedVisualTarget",
                "IMoonBlockGeneratorBlockedVisualTarget",
            };
            var registrySource = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/ITileFeatureVisualRegistry.cs");

            foreach (var forbiddenInterface in forbiddenInterfaces)
            {
                Assert.That(registrySource, Does.Not.Contain(forbiddenInterface), forbiddenInterface);
            }

            Assert.That(typeof(ITileFeatureVisualCueSink).IsAssignableFrom(typeof(TileFeatureVisualTargetView)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureVisualTargetView_ExposesTargetAnchorApiWithoutFlatteningSurfaceCell()
        {
            var root = new GameObject(nameof(TileFeatureVisualTargetView_ExposesTargetAnchorApiWithoutFlatteningSurfaceCell));

            try
            {
                var target = root.AddComponent<TileFeatureVisualTargetView>();
                var cell = new SurfaceCell(FaceId.Ceiling, 2, 3);
                target.Configure(42, cell);

                Assert.That(target.TileId, Is.EqualTo(42));
                Assert.That(target.Cell, Is.EqualTo(cell));
                Assert.That(target.PresentationRoot, Is.EqualTo(root.transform));
                Assert.That(target.TryGetSlot(TileFeatureVisualSlotId.Root, out var rootSlot), Is.True);
                Assert.That(rootSlot, Is.EqualTo(root.transform));
                Assert.That(target.TryGetSlot(TileFeatureVisualSlotId.FeatureAnchor0, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureVisualTargetView_DoesNotDeclareFeatureSpecificPlayMethods()
        {
            var forbiddenMethods = typeof(TileFeatureVisualTargetView)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => method.Name.StartsWith("Play", StringComparison.Ordinal) ||
                                 method.Name.StartsWith("SetBarricade", StringComparison.Ordinal) ||
                                 method.Name.StartsWith("SetDestroy", StringComparison.Ordinal) ||
                                 method.Name.StartsWith("SetExit", StringComparison.Ordinal) ||
                                 method.Name.StartsWith("SetTileFeatureActive", StringComparison.Ordinal))
                .Select(method => method.Name)
                .ToArray();

            Assert.That(forbiddenMethods, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureVisualTargetView_NoRemovedTargetViewSurface()
        {
            var targetType = typeof(TileFeatureVisualTargetView);
            var declaredMembers = targetType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(member => member.Name)
                .ToArray();
            var forbiddenNames = new[]
            {
                "PlayButton",
                "PlayDestroy",
                "PlaySlide",
                "PlayBarricade",
                "PlayExit",
                "PlayMoon",
            };

            foreach (var forbiddenName in forbiddenNames)
            {
                Assert.That(
                    declaredMembers.Where(memberName => memberName.StartsWith(forbiddenName, StringComparison.Ordinal)).ToArray(),
                    Is.Empty,
                    forbiddenName);
            }

            var serializedFields = targetType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(IsUnitySerializedField)
                .ToArray();
            Assert.That(serializedFields.Any(field => field.FieldType == typeof(string)), Is.False);
            Assert.That(serializedFields.Any(field => typeof(UnityEventBase).IsAssignableFrom(field.FieldType)), Is.False);
            Assert.That(serializedFields.Any(field => typeof(ParticleSystem).IsAssignableFrom(field.FieldType)), Is.False);
        }

        [Test]
        [Category("Full")]
        public void ProductionTileFeaturePrefabs_DoNotSerializeRemovedVisualTargetResidue()
        {
            var prefabPaths = Directory.GetFiles(
                    ProductionTileFeaturePrefabDirectory,
                    "TileFeature_*.prefab",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(path => path)
                .ToArray();
            var forbiddenTokens = new[]
            {
                "buttonActivatedTriggerName",
                "destroyTileTriggeredTriggerName",
                "slideTileRedirectedTriggerName",
                "barricadeBlockedTriggerName",
                "exitOpenedTriggerName",
                "moonBlockGeneratedTriggerName",
                "buttonActivatedParticles",
                "destroyTileTriggeredParticles",
                "slideTileRedirectedParticles",
                "barricadeBlockedParticles",
                "exitOpenedParticles",
                "moonBlockGeneratedParticles",
                "m_PersistentCalls:",
                "destroyTileMaterialRenderers",
                "destroyTileInactiveMaterialTargets",
                "slideTileInactiveMaterialTargets",
            };

            Assert.That(prefabPaths, Has.Length.EqualTo(14));
            foreach (var prefabPath in prefabPaths)
            {
                var yaml = File.ReadAllText(prefabPath);
                foreach (var forbiddenToken in forbiddenTokens)
                {
                    Assert.That(yaml, Does.Not.Contain(forbiddenToken), $"{prefabPath}:{forbiddenToken}");
                }
            }
        }

        private static bool IsUnitySerializedField(FieldInfo field)
        {
            return !field.IsStatic &&
                   (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null) &&
                   field.GetCustomAttribute<NonSerializedAttribute>() == null;
        }
    }
}
