using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringPresentationBindingCommandTests
    {
        [Test]
        public void SetBinding_CreatesNewTileIdPrefabBinding()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                    presentation,
                    authoring,
                    1,
                    prefab,
                    out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                Assert.That(presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(1));
                Assert.That(presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(prefab));
            });
        }

        [Test]
        public void SetBinding_ReplacesExistingBinding()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var replacement = CreateValidPrefab("ReplacementTileFeaturePrefab");
                try
                {
                    Assert.That(StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        1,
                        prefab,
                        out _), Is.True);

                    var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        1,
                        replacement,
                        out var error);

                    Assert.That(changed, Is.True, error);
                    Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                    Assert.That(presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(replacement));
                }
                finally
                {
                    Object.DestroyImmediate(replacement);
                }
            });
        }

        [Test]
        public void RemoveBinding_RemovesOnlySelectedTileId()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var other = CreateValidPrefab("OtherTileFeaturePrefab");
                try
                {
                    Assert.That(StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        1,
                        prefab,
                        out _), Is.True);
                    Assert.That(StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        2,
                        other,
                        out _), Is.True);

                    var removed = StageAuthoringPresentationBindingCommands.TryRemoveTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        1,
                        out var error);

                    Assert.That(removed, Is.True, error);
                    Assert.That(presentation.TileFeaturePresentationBindings.Select(binding => binding.TileId), Is.EqualTo(new[] { 2 }));
                    Assert.That(presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(other));
                }
                finally
                {
                    Object.DestroyImmediate(other);
                }
            });
        }

        [Test]
        public void SetBinding_RejectsNonPositiveTileId()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                    presentation,
                    authoring,
                    0,
                    prefab,
                    out _);

                Assert.That(changed, Is.False);
                Assert.That(presentation.TileFeaturePresentationBindings, Is.Empty);
            });
        }

        [Test]
        public void SetBinding_RejectsMissingSelectedTileFeature()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                    presentation,
                    authoring,
                    99,
                    prefab,
                    out _);

                Assert.That(changed, Is.False);
                Assert.That(presentation.TileFeaturePresentationBindings, Is.Empty);
            });
        }

        [Test]
        public void SetBinding_RejectsNullPrefab()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                    presentation,
                    authoring,
                    1,
                    null,
                    out _);

                Assert.That(changed, Is.False);
                Assert.That(presentation.TileFeaturePresentationBindings, Is.Empty);
            });
        }

        [Test]
        public void SetBinding_RejectsPrefabWithoutConfigurableTileFeatureVisualTarget()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var invalidPrefab = new GameObject("InvalidTileFeaturePrefab");
                try
                {
                    var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        1,
                        invalidPrefab,
                        out _);

                    Assert.That(changed, Is.False);
                    Assert.That(presentation.TileFeaturePresentationBindings, Is.Empty);
                }
                finally
                {
                    Object.DestroyImmediate(invalidPrefab);
                }
            });
        }

        [Test]
        public void SetBinding_AllowsPrefabPlaceholderTileIdMismatch()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var prefabTarget = prefab.GetComponent<TileFeatureVisualTargetView>();

                var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                    presentation,
                    authoring,
                    2,
                    prefab,
                    out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(prefabTarget.TileId, Is.Not.EqualTo(2));
                Assert.That(presentation.TileFeaturePresentationBindings.Single().TileId, Is.EqualTo(2));
            });
        }

        [Test]
        public void SetBinding_SortsBindingsByTileId()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                var other = CreateValidPrefab("OtherTileFeaturePrefab");
                try
                {
                    Assert.That(StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        2,
                        other,
                        out _), Is.True);
                    Assert.That(StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        1,
                        prefab,
                        out _), Is.True);

                    Assert.That(
                        presentation.TileFeaturePresentationBindings.Select(binding => binding.TileId).ToArray(),
                        Is.EqualTo(new[] { 1, 2 }));
                }
                finally
                {
                    Object.DestroyImmediate(other);
                }
            });
        }

        [Test]
        public void SetBinding_NormalizesDuplicateTileIdBindings()
        {
            WithFixture((authoring, presentation, prefab) =>
            {
                SetTileFeaturePresentationBindings(
                    presentation,
                    new[]
                    {
                        new TileFeaturePresentationBinding { TileId = 1, VisualPrefab = prefab },
                        new TileFeaturePresentationBinding { TileId = 1, VisualPrefab = prefab },
                    });

                var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                    presentation,
                    authoring,
                    1,
                    prefab,
                    out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                Assert.That(presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(1));
            });
        }

        [Test]
        public void SetBinding_DoesNotChangeStageDefinition()
        {
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            try
            {
                WithFixture((authoring, presentation, prefab) =>
                {
                    authoring.AssignGeneratedDefinitions(gameplay, presentation);

                    var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                        presentation,
                        authoring,
                        1,
                        prefab,
                        out var error);

                    Assert.That(changed, Is.True, error);
                    Assert.That(authoring.GeneratedGameplayDefinition, Is.SameAs(gameplay));
                    Assert.That(presentation.TileFeaturePresentationBindings.Single().VisualPrefab, Is.SameAs(prefab));
                });
            }
            finally
            {
                Object.DestroyImmediate(gameplay);
            }
        }

        private static void WithFixture(
            System.Action<StageAuthoringDefinition, StagePresentationDefinition, GameObject> action)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var prefab = CreateValidPrefab("ButtonTileFeaturePrefab");
            try
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1),
                    CreateFeature(2),
                });
                action(authoring, presentation, prefab);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(presentation);
                Object.DestroyImmediate(authoring);
            }
        }

        private static StageTileFeatureDefinition CreateFeature(int tileId)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = new SurfaceCell(FaceId.Floor, tileId - 1, 0),
                Kind = TileFeatureKind.Button,
                ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                Direction = Direction2D.None,
                BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
            };
        }

        private static GameObject CreateValidPrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<TileFeatureVisualTargetView>();
            return prefab;
        }

        private static void SetTileFeaturePresentationBindings(
            StagePresentationDefinition presentation,
            TileFeaturePresentationBinding[] bindings)
        {
            var serializedObject = new SerializedObject(presentation);
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
    }
}
