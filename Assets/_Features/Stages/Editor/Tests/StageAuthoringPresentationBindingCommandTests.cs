using System.Linq;
using System.Reflection;
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
                Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(2));
                Assert.That(
                    presentation.TileFeaturePresentationBindings.Single(binding => binding.TileId == 1).VisualPrefab,
                    Is.SameAs(prefab));
                Assert.That(
                    presentation.TileFeaturePresentationBindings.Single(binding => binding.TileId == 2).VisualPrefab,
                    Is.Null);
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
                    Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(2));
                    Assert.That(
                        presentation.TileFeaturePresentationBindings.Single(binding => binding.TileId == 1).VisualPrefab,
                        Is.SameAs(replacement));
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
                    Assert.That(
                        presentation.TileFeaturePresentationBindings.Select(binding => binding.TileId).ToArray(),
                        Is.EqualTo(new[] { 1, 2 }));
                    Assert.That(
                        presentation.TileFeaturePresentationBindings.Single(binding => binding.TileId == 1).VisualPrefab,
                        Is.Null);
                    Assert.That(
                        presentation.TileFeaturePresentationBindings.Single(binding => binding.TileId == 2).VisualPrefab,
                        Is.SameAs(other));
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
                Assert.That(
                    presentation.TileFeaturePresentationBindings.Single(binding => binding.VisualPrefab != null).TileId,
                    Is.EqualTo(2));
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
                Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(2));
                Assert.That(
                    presentation.TileFeaturePresentationBindings.Single(binding => binding.TileId == 1).VisualPrefab,
                    Is.SameAs(prefab));
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
                    Assert.That(
                        presentation.TileFeaturePresentationBindings.Single(binding => binding.TileId == 1).VisualPrefab,
                        Is.SameAs(prefab));
                });
            }
            finally
            {
                Object.DestroyImmediate(gameplay);
            }
        }

        [Test]
        public void SetBoardTilePaintOverride_ReplacesExistingCellPaint()
        {
            WithBoardTileFixture((authoring, presentation) =>
            {
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);

                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    presentation,
                    authoring,
                    cell,
                    "grass",
                    out var firstError), Is.True, firstError);
                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    presentation,
                    authoring,
                    cell,
                    "danger",
                    out var secondError), Is.True, secondError);

                Assert.That(presentation.BoardTilePaintOverrides.Count, Is.EqualTo(1));
                Assert.That(presentation.BoardTilePaintOverrides[0].Cell, Is.EqualTo(cell));
                Assert.That(presentation.BoardTilePaintOverrides[0].StyleKey, Is.EqualTo("danger"));
                Assert.That(EditorUtility.IsDirty(presentation), Is.True);
            });
        }

        [Test]
        public void ClearBoardTilePaintOverride_RemovesOnlySelectedCell()
        {
            WithBoardTileFixture((authoring, presentation) =>
            {
                var first = new SurfaceCell(FaceId.Floor, 0, 0);
                var second = new SurfaceCell(FaceId.Floor, 1, 0);
                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    presentation,
                    authoring,
                    first,
                    "grass",
                    out _), Is.True);
                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    presentation,
                    authoring,
                    second,
                    "danger",
                    out _), Is.True);

                var cleared = StageAuthoringPresentationBindingCommands.TryClearBoardTilePaintOverride(
                    presentation,
                    authoring,
                    first,
                    out var error);

                Assert.That(cleared, Is.True, error);
                Assert.That(presentation.BoardTilePaintOverrides.Count, Is.EqualTo(1));
                Assert.That(presentation.BoardTilePaintOverrides[0].Cell, Is.EqualTo(second));
                Assert.That(presentation.BoardTilePaintOverrides[0].StyleKey, Is.EqualTo("danger"));
            });
        }

        [Test]
        public void BoardTilePaintCommands_RejectInvalidCellsAndKeys()
        {
            WithBoardTileFixture((authoring, presentation) =>
            {
                var invalidFace = new SurfaceCell((FaceId)999, 0, 0);
                var outsideBounds = new SurfaceCell(FaceId.Floor, 9, 9);

                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    presentation,
                    authoring,
                    invalidFace,
                    "grass",
                    out _), Is.False);
                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    presentation,
                    authoring,
                    outsideBounds,
                    "grass",
                    out _), Is.False);
                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    presentation,
                    authoring,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    " ",
                    out _), Is.False);
                Assert.That(presentation.BoardTilePaintOverrides, Is.Empty);
            });
        }

        [Test]
        public void BoardTilePaintCommands_RejectMissingCatalogAndMissingCatalogKey()
        {
            WithBoardTileFixture((authoring, presentation) =>
            {
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);

                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    presentation,
                    authoring,
                    cell,
                    "missing-style",
                    out _), Is.False);
                Assert.That(presentation.BoardTilePaintOverrides, Is.Empty);
            });

            var missingCatalogAuthoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var missingCatalogPresentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            try
            {
                missingCatalogAuthoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = Vector2Int.zero,
                    MaxInclusive = Vector2Int.one,
                    InitialBottomFace = FaceId.Floor,
                });

                Assert.That(StageAuthoringPresentationBindingCommands.TrySetBoardTilePaintOverride(
                    missingCatalogPresentation,
                    missingCatalogAuthoring,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    "grass",
                    out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(missingCatalogPresentation);
                Object.DestroyImmediate(missingCatalogAuthoring);
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

        private static void WithBoardTileFixture(
            System.Action<StageAuthoringDefinition, StagePresentationDefinition> action)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var styleCatalog = CreateStyleCatalog(
                StyleEntry("grass", "Grass", Color.green),
                StyleEntry("danger", "Danger", Color.red));
            var profile = CreateBoardPresentationProfile(styleCatalog);
            try
            {
                authoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = Vector2Int.zero,
                    MaxInclusive = new Vector2Int(2, 2),
                    InitialBottomFace = FaceId.Floor,
                });
                SetPrivateField(presentation, "boardPresentationProfile", profile);
                action(authoring, presentation);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(styleCatalog);
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

        private static BoardTileStyleCatalogEntry StyleEntry(
            string styleKey,
            string displayName,
            Color tint)
        {
            var entry = new BoardTileStyleCatalogEntry();
            SetPrivateField(entry, "styleKey", styleKey);
            SetPrivateField(entry, "displayName", displayName);
            SetPrivateField(entry, "tint", tint);
            return entry;
        }

        private static BoardTileStyleCatalog CreateStyleCatalog(params BoardTileStyleCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<BoardTileStyleCatalog>();
            catalog.name = "StageAuthoringPresentationBindingCommandTests_StyleCatalog";
            SetPrivateField(catalog, "entries", entries ?? System.Array.Empty<BoardTileStyleCatalogEntry>());
            return catalog;
        }

        private static BoardPresentationProfile CreateBoardPresentationProfile(BoardTileStyleCatalog styleCatalog)
        {
            var profile = ScriptableObject.CreateInstance<BoardPresentationProfile>();
            profile.name = "StageAuthoringPresentationBindingCommandTests_BoardProfile";
            SetPrivateField(profile, "defaultBoardTileStyleCatalog", styleCatalog);
            return profile;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
