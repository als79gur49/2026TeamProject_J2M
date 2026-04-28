using System;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringGridPresentationDropdownTests
    {
        [Test]
        public void EnemyCatalogNull_ReportsWarning()
        {
            var presentation = CreatePresentation(enemyCatalog: null, staticCatalog: CreateStaticCatalog("box-a"));

            try
            {
                var model = StageAuthoringPresentationOptionModel.Build(
                    StageAuthoringEntityKind.Enemy,
                    string.Empty,
                    presentation);

                Assert.That(model.RawIds, Is.Empty);
                Assert.That(model.CatalogAssigned, Is.False);
                Assert.That(model.WarningMessage, Does.Contain("Enemy presentation catalog is not assigned."));
                Assert.That(model.PopupLabels, Has.Length.EqualTo(1));
                Assert.That(model.PopupLabels[0], Is.EqualTo(StageAuthoringPresentationOptionModel.NoneLabel));
            }
            finally
            {
                DestroyImmediate(presentation.StaticEntityPresentationCatalog);
                DestroyImmediate(presentation);
            }
        }

        [Test]
        public void EnemyCatalogEmpty_ReportsWarning()
        {
            var enemyCatalog = CreateEnemyCatalog();
            var presentation = CreatePresentation(enemyCatalog, staticCatalog: CreateStaticCatalog("box-a"));

            try
            {
                var model = StageAuthoringPresentationOptionModel.Build(
                    StageAuthoringEntityKind.Enemy,
                    string.Empty,
                    presentation);

                Assert.That(model.RawIds, Is.Empty);
                Assert.That(model.CatalogAssigned, Is.True);
                Assert.That(model.CatalogHasEntries, Is.False);
                Assert.That(model.WarningMessage, Does.Contain("Enemy presentation catalog has no entries."));
            }
            finally
            {
                DestroyImmediate(enemyCatalog);
                DestroyImmediate(presentation.StaticEntityPresentationCatalog);
                DestroyImmediate(presentation);
            }
        }

        [Test]
        public void StaticCatalogNull_ReportsWarning()
        {
            var presentation = CreatePresentation(CreateEnemyCatalog("slime-a"), staticCatalog: null);

            try
            {
                var model = StageAuthoringPresentationOptionModel.Build(
                    StageAuthoringEntityKind.Box,
                    string.Empty,
                    presentation);

                Assert.That(model.RawIds, Is.Empty);
                Assert.That(model.CatalogAssigned, Is.False);
                Assert.That(model.WarningMessage, Does.Contain("Static presentation catalog is not assigned."));
            }
            finally
            {
                DestroyImmediate(presentation.EnemyPresentationCatalog);
                DestroyImmediate(presentation);
            }
        }

        [Test]
        public void StaticCatalogEmpty_ReportsWarning()
        {
            var staticCatalog = CreateStaticCatalog();
            var presentation = CreatePresentation(CreateEnemyCatalog("slime-a"), staticCatalog);

            try
            {
                var model = StageAuthoringPresentationOptionModel.Build(
                    StageAuthoringEntityKind.Wall,
                    string.Empty,
                    presentation);

                Assert.That(model.RawIds, Is.Empty);
                Assert.That(model.CatalogAssigned, Is.True);
                Assert.That(model.CatalogHasEntries, Is.False);
                Assert.That(model.WarningMessage, Does.Contain("Static presentation catalog has no entries."));
            }
            finally
            {
                DestroyImmediate(presentation.EnemyPresentationCatalog);
                DestroyImmediate(staticCatalog);
                DestroyImmediate(presentation);
            }
        }

        [Test]
        public void PopupOptions_AlwaysIncludeNoneButWarningUsesRawCount()
        {
            var enemyCatalog = CreateEnemyCatalog();
            var presentation = CreatePresentation(enemyCatalog, staticCatalog: null);

            try
            {
                var model = StageAuthoringPresentationOptionModel.Build(
                    StageAuthoringEntityKind.Enemy,
                    string.Empty,
                    presentation);

                Assert.That(model.RawIds, Is.Empty);
                Assert.That(model.PopupLabels, Has.Length.EqualTo(1));
                Assert.That(model.PopupLabels[0], Is.EqualTo(StageAuthoringPresentationOptionModel.NoneLabel));
                Assert.That(model.WarningMessage, Does.Contain("Enemy presentation catalog has no entries."));
            }
            finally
            {
                DestroyImmediate(enemyCatalog);
                DestroyImmediate(presentation);
            }
        }

        [Test]
        public void ValidEnemyPresentationId_SelectsCorrectIndex()
        {
            var enemyCatalog = CreateEnemyCatalog("slime-a", "slime-b");
            var presentation = CreatePresentation(enemyCatalog, staticCatalog: null);

            try
            {
                var model = StageAuthoringPresentationOptionModel.Build(
                    StageAuthoringEntityKind.Enemy,
                    "slime-b",
                    presentation);

                Assert.That(model.SelectedPopupIndex, Is.EqualTo(2));
                Assert.That(model.SelectedIdMissing, Is.False);
                Assert.That(model.ResolvePresentationId(model.SelectedPopupIndex), Is.EqualTo("slime-b"));
            }
            finally
            {
                DestroyImmediate(enemyCatalog);
                DestroyImmediate(presentation);
            }
        }

        [Test]
        public void NoneSelection_WritesEmptyPresentationId()
        {
            var enemyCatalog = CreateEnemyCatalog("slime-a");
            var presentation = CreatePresentation(enemyCatalog, staticCatalog: null);

            try
            {
                var model = StageAuthoringPresentationOptionModel.Build(
                    StageAuthoringEntityKind.Enemy,
                    "slime-a",
                    presentation);

                Assert.That(model.ResolvePresentationId(0), Is.EqualTo(string.Empty));
            }
            finally
            {
                DestroyImmediate(enemyCatalog);
                DestroyImmediate(presentation);
            }
        }

        [Test]
        public void SelectedIdMissing_ReportsWarning()
        {
            var enemyCatalog = CreateEnemyCatalog("slime-a");
            var presentation = CreatePresentation(enemyCatalog, staticCatalog: null);

            try
            {
                var currentPresentationId = "missing-id";
                var model = StageAuthoringPresentationOptionModel.Build(
                    StageAuthoringEntityKind.Enemy,
                    currentPresentationId,
                    presentation);

                Assert.That(model.SelectedIdMissing, Is.True);
                Assert.That(model.SelectedPopupIndex, Is.EqualTo(0));
                Assert.That(
                    model.WarningMessage,
                    Does.Contain("Selected PresentationId 'missing-id' was not found in the enemy presentation catalog."));
                Assert.That(currentPresentationId, Is.EqualTo("missing-id"));
            }
            finally
            {
                DestroyImmediate(enemyCatalog);
                DestroyImmediate(presentation);
            }
        }

        [Test]
        public void PlayerPlacement_DoesNotRequirePresentationCatalog()
        {
            var model = StageAuthoringPresentationOptionModel.Build(
                StageAuthoringEntityKind.Player,
                "player-view",
                presentationDefinition: null);

            Assert.That(model.RawIds, Is.Empty);
            Assert.That(model.PopupLabels, Is.Empty);
            Assert.That(model.WarningMessages, Is.Empty);
            Assert.That(model.SelectedIdMissing, Is.False);
        }

        private static StagePresentationDefinition CreatePresentation(
            EnemyPresentationCatalog enemyCatalog,
            StaticEntityPresentationCatalog staticCatalog)
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var serializedObject = new SerializedObject(presentation);
            serializedObject.FindProperty("enemyPresentationCatalog").objectReferenceValue = enemyCatalog;
            serializedObject.FindProperty("staticEntityPresentationCatalog").objectReferenceValue = staticCatalog;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return presentation;
        }

        private static EnemyPresentationCatalog CreateEnemyCatalog(params string[] ids)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            var serializedObject = new SerializedObject(catalog);
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize = ids.Length;
            for (var i = 0; i < ids.Length; i++)
            {
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("PresentationId").stringValue = ids[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static StaticEntityPresentationCatalog CreateStaticCatalog(params string[] ids)
        {
            var catalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();
            var serializedObject = new SerializedObject(catalog);
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize = ids.Length;
            for (var i = 0; i < ids.Length; i++)
            {
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("PresentationId").stringValue = ids[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static void DestroyImmediate(UnityEngine.Object unityObject)
        {
            if (unityObject != null)
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }
    }
}
