using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringGeneratedBindingPreviewTests
    {
        [Test]
        public void GeneratedBindingPreview_StaticBindingSynced()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Box,
                "box_showcase",
                mappedEntityId: 198);
            fixture.SetStaticBinding(198, "box_showcase");

            var model = Resolve(fixture);

            Assert.That(model.BindingKindLabel, Is.EqualTo("Static"));
            Assert.That(model.EntityId, Is.EqualTo(198));
            Assert.That(model.IsSynced, Is.True);
            Assert.That(model.StatusLabel, Is.EqualTo("Synced."));
        }

        [Test]
        public void GeneratedBindingPreview_StaticBindingMissing()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Box,
                "box_showcase",
                mappedEntityId: 198);

            var model = Resolve(fixture);

            Assert.That(model.IsMissing, Is.True);
            Assert.That(model.StatusLabel, Is.EqualTo("Missing."));
        }

        [Test]
        public void GeneratedBindingPreview_StaticBindingDrifted()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Wall,
                "wall_showcase",
                mappedEntityId: 207);
            fixture.SetStaticBinding(207, "other_wall");

            var model = Resolve(fixture);

            Assert.That(model.IsDrifted, Is.True);
            Assert.That(model.StatusLabel, Is.EqualTo("Drifted."));
        }

        [Test]
        public void GeneratedBindingPreview_EnemyBindingSynced()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Enemy,
                "slime_showcase",
                mappedEntityId: 301);
            fixture.SetEnemyBinding(301, "slime_showcase");

            var model = Resolve(fixture);

            Assert.That(model.BindingKindLabel, Is.EqualTo("Enemy"));
            Assert.That(model.IsSynced, Is.True);
        }

        [Test]
        public void GeneratedBindingPreview_WrongKindBinding()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Box,
                "box_showcase",
                mappedEntityId: 198);
            fixture.SetEnemyBinding(198, "enemy_showcase");

            var model = Resolve(fixture);

            Assert.That(model.WrongKind, Is.True);
            Assert.That(model.StatusLabel, Is.EqualTo("Wrong Kind."));
        }

        [Test]
        public void GeneratedBindingPreview_PlayerNoBindingRequired()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Player,
                string.Empty,
                mappedEntityId: 1);

            var model = Resolve(fixture);

            Assert.That(model.RequiresBinding, Is.False);
            Assert.That(model.BindingKindLabel, Is.EqualTo("None"));
        }

        [Test]
        public void GeneratedBindingPreview_UsesMappedEntityId()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Box,
                "box_showcase",
                mappedEntityId: 243);

            var model = Resolve(fixture);

            Assert.That(model.HasMappedEntityId, Is.True);
            Assert.That(model.IsPreviewEntityId, Is.False);
            Assert.That(model.EntityId, Is.EqualTo(243));
        }

        [Test]
        public void GeneratedBindingPreview_UsesPreviewEntityIdWithoutMutatingMapping()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Box,
                "box_showcase",
                mappedEntityId: 0);

            var model = Resolve(fixture);

            Assert.That(model.HasMappedEntityId, Is.True);
            Assert.That(model.IsPreviewEntityId, Is.True);
            Assert.That(model.EntityId, Is.GreaterThan(0));
            Assert.That(fixture.Authoring.EntityIdMappings, Is.Empty);
        }

        [Test]
        public void GeneratedBindingPreview_DoesNotModifyPresentationBindings()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Box,
                "box_showcase",
                mappedEntityId: 198);
            fixture.SetStaticBinding(198, "other");
            var originalBinding = fixture.Presentation.StaticEntityPresentationBindings.Single();

            Resolve(fixture);

            Assert.That(fixture.Presentation.StaticEntityPresentationBindings.Single().PresentationId, Is.EqualTo(originalBinding.PresentationId));
            Assert.That(fixture.Presentation.StaticEntityPresentationBindings.Single().EntityId, Is.EqualTo(originalBinding.EntityId));
        }

        [Test]
        public void GeneratedGameplayPreview_IncludesFacing()
        {
            using var fixture = BindingFixture.Create(
                StageAuthoringEntityKind.Box,
                "box_showcase",
                mappedEntityId: 198,
                facing: Direction.Left);

            var model = Resolve(fixture);

            Assert.That(model.Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 3)));
            Assert.That(model.Facing, Is.EqualTo(Direction.Left));
        }

        private static StageAuthoringGeneratedBindingPreviewModel Resolve(BindingFixture fixture)
        {
            return StageAuthoringGeneratedBindingPreviewResolver.Resolve(
                fixture.Authoring,
                fixture.Placement,
                fixture.Presentation);
        }

        private sealed class BindingFixture : System.IDisposable
        {
            private BindingFixture(
                StageAuthoringDefinition authoring,
                StagePresentationDefinition presentation,
                StagePlacedEntityAuthoring placement)
            {
                Authoring = authoring;
                Presentation = presentation;
                Placement = placement;
            }

            public StageAuthoringDefinition Authoring { get; }

            public StagePresentationDefinition Presentation { get; }

            public StagePlacedEntityAuthoring Placement { get; }

            public static BindingFixture Create(
                StageAuthoringEntityKind kind,
                string presentationId,
                int mappedEntityId,
                Direction facing = Direction.Right)
            {
                var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
                var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
                var placement = new StagePlacedEntityAuthoring
                {
                    StableGuid = "placement-a",
                    DisplayName = "placement-a",
                    Kind = kind,
                    Cell = new SurfaceCell(FaceId.Floor, 2, 3),
                    Facing = facing,
                    Hp = 1,
                    BoxCapabilities = BoxCapabilities.Push,
                    EnemyAiMode = kind == StageAuthoringEntityKind.Enemy ? EnemyAiMode.Patrol : EnemyAiMode.None,
                    PresentationId = presentationId,
                };
                authoring.SetPlacements(new[] { placement });
                if (mappedEntityId > 0)
                {
                    authoring.SetEntityIdMappings(new[]
                    {
                        new StageAuthoringIdMapping
                        {
                            StableGuid = placement.StableGuid,
                            EntityId = mappedEntityId,
                        },
                    });
                }

                authoring.AssignGeneratedDefinitions(null, presentation);
                return new BindingFixture(authoring, presentation, placement);
            }

            public void SetEnemyBinding(int entityId, string presentationId)
            {
                var serializedObject = new SerializedObject(Presentation);
                var bindings = serializedObject.FindProperty("enemyPresentationBindings");
                bindings.arraySize = 1;
                var element = bindings.GetArrayElementAtIndex(0);
                element.FindPropertyRelative("EntityId").intValue = entityId;
                element.FindPropertyRelative("PresentationId").stringValue = presentationId;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetStaticBinding(int entityId, string presentationId)
            {
                var serializedObject = new SerializedObject(Presentation);
                var bindings = serializedObject.FindProperty("staticEntityPresentationBindings");
                bindings.arraySize = 1;
                var element = bindings.GetArrayElementAtIndex(0);
                element.FindPropertyRelative("EntityId").intValue = entityId;
                element.FindPropertyRelative("PresentationId").stringValue = presentationId;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Presentation);
                Object.DestroyImmediate(Authoring);
            }
        }
    }
}
