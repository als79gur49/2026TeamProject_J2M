using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringPresentationPreviewTests
    {
        [Test]
        public void PresentationPreview_StaticPlacement_ResolvesCatalogAndPrefab()
        {
            using var fixture = PreviewFixture.CreateStatic("box_showcase", withViewPrefab: true);

            var model = StageAuthoringPresentationPreviewResolver.Resolve(
                fixture.Authoring,
                fixture.Placement,
                fixture.Presentation);

            Assert.That(model.RequiresPresentation, Is.True);
            Assert.That(model.PresentationKindLabel, Is.EqualTo("Static"));
            Assert.That(model.PresentationId, Is.EqualTo("box_showcase"));
            Assert.That(model.CatalogAsset, Is.EqualTo(fixture.StaticCatalog));
            Assert.That(model.ViewPrefab, Is.EqualTo(fixture.StaticViewPrefab));
            Assert.That(model.EntryFound, Is.True);
            Assert.That(model.StatusLabel, Is.EqualTo("Resolved."));
        }

        [Test]
        public void PresentationPreview_EnemyPlacement_ResolvesCatalogAndPrefab()
        {
            using var fixture = PreviewFixture.CreateEnemy("slime_showcase", withViewPrefab: true);

            var model = StageAuthoringPresentationPreviewResolver.Resolve(
                fixture.Authoring,
                fixture.Placement,
                fixture.Presentation);

            Assert.That(model.PresentationKindLabel, Is.EqualTo("Enemy"));
            Assert.That(model.CatalogAsset, Is.EqualTo(fixture.EnemyCatalog));
            Assert.That(model.ViewPrefab, Is.EqualTo(fixture.EnemyViewPrefab));
            Assert.That(model.EntryFound, Is.True);
        }

        [Test]
        public void PresentationPreview_PlayerPlacement_NoPresentationRequired()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            try
            {
                var placement = Placement(StageAuthoringEntityKind.Player, "player", string.Empty);

                var model = StageAuthoringPresentationPreviewResolver.Resolve(authoring, placement, presentation);

                Assert.That(model.RequiresPresentation, Is.False);
                Assert.That(model.StatusLabel, Is.EqualTo("No presentation required."));
            }
            finally
            {
                Object.DestroyImmediate(presentation);
                Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void PresentationPreview_MissingPresentationId_ShowsWarning()
        {
            using var fixture = PreviewFixture.CreateStatic("box_showcase", withViewPrefab: true);
            fixture.Placement.PresentationId = "missing";

            var model = StageAuthoringPresentationPreviewResolver.Resolve(
                fixture.Authoring,
                fixture.Placement,
                fixture.Presentation);

            Assert.That(model.EntryFound, Is.False);
            Assert.That(model.StatusLabel, Does.Contain("was not found"));
            Assert.That(model.StatusMessageType, Is.EqualTo(MessageType.Warning));
        }

        [Test]
        public void PresentationPreview_NullViewPrefab_ShowsError()
        {
            using var fixture = PreviewFixture.CreateStatic("box_showcase", withViewPrefab: false);

            var model = StageAuthoringPresentationPreviewResolver.Resolve(
                fixture.Authoring,
                fixture.Placement,
                fixture.Presentation);

            Assert.That(model.EntryFound, Is.True);
            Assert.That(model.ViewPrefab, Is.Null);
            Assert.That(model.StatusLabel, Does.Contain("non-null ViewPrefab"));
            Assert.That(model.StatusMessageType, Is.EqualTo(MessageType.Error));
        }

        [Test]
        public void PresentationPreview_DoesNotModifyPlacementOrCatalog()
        {
            using var fixture = PreviewFixture.CreateStatic("box_showcase", withViewPrefab: true);
            var originalPresentationId = fixture.Placement.PresentationId;
            var originalEntries = fixture.StaticCatalog.Entries.Length;

            StageAuthoringPresentationPreviewResolver.Resolve(
                fixture.Authoring,
                fixture.Placement,
                fixture.Presentation);

            Assert.That(fixture.Placement.PresentationId, Is.EqualTo(originalPresentationId));
            Assert.That(fixture.StaticCatalog.Entries.Length, Is.EqualTo(originalEntries));
            Assert.That(fixture.StaticCatalog.Entries[0].ViewPrefab, Is.EqualTo(fixture.StaticViewPrefab));
        }

        private static StagePlacedEntityAuthoring Placement(
            StageAuthoringEntityKind kind,
            string stableGuid,
            string presentationId)
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = stableGuid,
                DisplayName = stableGuid,
                Kind = kind,
                Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                Facing = Direction.Right,
                Hp = 1,
                BoxCapabilities = BoxCapabilities.Push,
                EnemyAiMode = kind == StageAuthoringEntityKind.Enemy ? EnemyAiMode.Patrol : EnemyAiMode.None,
                PresentationId = presentationId,
            };
        }

        private sealed class PreviewFixture : System.IDisposable
        {
            private PreviewFixture(
                StageAuthoringDefinition authoring,
                StagePresentationDefinition presentation,
                StagePlacedEntityAuthoring placement,
                EnemyPresentationCatalog enemyCatalog,
                StaticEntityPresentationCatalog staticCatalog,
                GameplayEntityView enemyViewPrefab,
                GameplayEntityView staticViewPrefab)
            {
                Authoring = authoring;
                Presentation = presentation;
                Placement = placement;
                EnemyCatalog = enemyCatalog;
                StaticCatalog = staticCatalog;
                EnemyViewPrefab = enemyViewPrefab;
                StaticViewPrefab = staticViewPrefab;
            }

            public StageAuthoringDefinition Authoring { get; }

            public StagePresentationDefinition Presentation { get; }

            public StagePlacedEntityAuthoring Placement { get; }

            public EnemyPresentationCatalog EnemyCatalog { get; }

            public StaticEntityPresentationCatalog StaticCatalog { get; }

            public GameplayEntityView EnemyViewPrefab { get; }

            public GameplayEntityView StaticViewPrefab { get; }

            public static PreviewFixture CreateStatic(string presentationId, bool withViewPrefab)
            {
                var staticView = withViewPrefab ? CreateViewPrefab("StaticView") : null;
                var staticCatalog = CreateStaticCatalog(presentationId, staticView);
                return Create(
                    Placement(StageAuthoringEntityKind.Box, "box", presentationId),
                    enemyCatalog: null,
                    staticCatalog,
                    enemyViewPrefab: null,
                    staticView);
            }

            public static PreviewFixture CreateEnemy(string presentationId, bool withViewPrefab)
            {
                var enemyView = withViewPrefab ? CreateViewPrefab("EnemyView") : null;
                var enemyCatalog = CreateEnemyCatalog(presentationId, enemyView);
                return Create(
                    Placement(StageAuthoringEntityKind.Enemy, "enemy", presentationId),
                    enemyCatalog,
                    staticCatalog: null,
                    enemyView,
                    staticViewPrefab: null);
            }

            public void Dispose()
            {
                Destroy(EnemyViewPrefab != null ? EnemyViewPrefab.gameObject : null);
                Destroy(StaticViewPrefab != null ? StaticViewPrefab.gameObject : null);
                Destroy(EnemyCatalog);
                Destroy(StaticCatalog);
                Destroy(Presentation);
                Destroy(Authoring);
            }

            private static PreviewFixture Create(
                StagePlacedEntityAuthoring placement,
                EnemyPresentationCatalog enemyCatalog,
                StaticEntityPresentationCatalog staticCatalog,
                GameplayEntityView enemyViewPrefab,
                GameplayEntityView staticViewPrefab)
            {
                var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
                var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
                SetPresentationCatalogs(presentation, enemyCatalog, staticCatalog);
                authoring.SetPlacements(new[] { placement });
                authoring.AssignGeneratedDefinitions(null, presentation);
                return new PreviewFixture(
                    authoring,
                    presentation,
                    placement,
                    enemyCatalog,
                    staticCatalog,
                    enemyViewPrefab,
                    staticViewPrefab);
            }

            private static EnemyPresentationCatalog CreateEnemyCatalog(string id, GameplayEntityView viewPrefab)
            {
                var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
                var serializedObject = new SerializedObject(catalog);
                var entries = serializedObject.FindProperty("entries");
                entries.arraySize = 1;
                var element = entries.GetArrayElementAtIndex(0);
                element.FindPropertyRelative("PresentationId").stringValue = id;
                element.FindPropertyRelative("ViewPrefab").objectReferenceValue = viewPrefab;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                return catalog;
            }

            private static StaticEntityPresentationCatalog CreateStaticCatalog(string id, GameplayEntityView viewPrefab)
            {
                var catalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();
                var serializedObject = new SerializedObject(catalog);
                var entries = serializedObject.FindProperty("entries");
                entries.arraySize = 1;
                var element = entries.GetArrayElementAtIndex(0);
                element.FindPropertyRelative("PresentationId").stringValue = id;
                element.FindPropertyRelative("ViewPrefab").objectReferenceValue = viewPrefab;
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

            private static void Destroy(Object value)
            {
                if (value != null)
                {
                    Object.DestroyImmediate(value);
                }
            }
        }
    }
}
