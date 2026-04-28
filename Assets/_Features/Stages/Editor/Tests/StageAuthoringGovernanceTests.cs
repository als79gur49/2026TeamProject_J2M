using System.Linq;
using Game.Feature.Gameplay.BoardState;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringGovernanceTests
    {
        [Test]
        public void GovernanceDetectsGeneratedDrift()
        {
            var fixture = CreateSyncedEntry(enforceGeneratedSync: true);

            try
            {
                SetFirstPlayerHp(fixture.Gameplay, 2);
                var report = new StageCatalogValidator().ValidateEntries(
                    new[] { fixture.Entry },
                    aliasTable: null,
                    new StageCatalogValidationOptions
                    {
                        Timing = StageValidationTiming.TestOrCi,
                    });

                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Severity == StageValidationSeverity.Error &&
                        issue.Code == "authoring.generated-output-mismatch"),
                    Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void StageWithoutAuthoringDefinitionStillValid()
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                entry.name = "legacy-stage_Entry";
                entry.AssignStageId(StageId.CreateOrThrow("legacy-stage"));
                entry.AssignGameplayDefinition(gameplay);
                entry.AssignPresentationDefinition(presentation);
                presentation.SetOwnerMetadata(entry, string.Empty);
                SetMinimalStage(gameplay);

                var report = new StageCatalogValidator().ValidateEntries(
                    new[] { entry },
                    aliasTable: null,
                    new StageCatalogValidationOptions
                    {
                        Timing = StageValidationTiming.TestOrCi,
                    });

                Assert.That(report.HasErrors, Is.False);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.missing"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entry);
                UnityEngine.Object.DestroyImmediate(gameplay);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        private static GovernanceFixture CreateSyncedEntry(bool enforceGeneratedSync)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            var gameplay = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            entry.name = "governance-stage_Entry";
            gameplay.name = "governance-stage";
            presentation.name = "governance-stage_Presentation";
            authoring.name = "governance-stage_Authoring";
            entry.AssignStageId(StageId.CreateOrThrow("governance-stage"));
            entry.AssignGameplayDefinition(gameplay);
            entry.AssignPresentationDefinition(presentation);
            entry.AssignAuthoringDefinition(authoring);
            authoring.SetOwnerMetadata(entry, string.Empty);
            presentation.SetOwnerMetadata(entry, string.Empty);
            authoring.AssignGeneratedDefinitions(gameplay, presentation);
            authoring.SetBoard(new StageBoardDefinition
            {
                MinInclusive = new Vector2Int(0, 0),
                MaxInclusive = new Vector2Int(2, 2),
                InitialBottomFace = FaceId.Floor,
            });
            authoring.SetPlacements(new[]
            {
                new StagePlacedEntityAuthoring
                {
                    StableGuid = "player",
                    DisplayName = "Player",
                    Kind = StageAuthoringEntityKind.Player,
                    Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                    Facing = Direction.Right,
                    Hp = 1,
                },
            });
            authoring.SetEnforceGeneratedSync(enforceGeneratedSync);
            var generationReport = StageAuthoringGenerator.Generate(authoring, StageAuthoringGenerateOptions.WriteAll);
            Assert.That(generationReport.HasErrors, Is.False);
            return new GovernanceFixture(entry, authoring, gameplay, presentation);
        }

        private static void SetMinimalStage(StageDefinition gameplay)
        {
            var serializedObject = new SerializedObject(gameplay);
            var board = serializedObject.FindProperty("board");
            board.FindPropertyRelative("MinInclusive").vector2IntValue = new Vector2Int(0, 0);
            board.FindPropertyRelative("MaxInclusive").vector2IntValue = new Vector2Int(2, 2);
            board.FindPropertyRelative("InitialBottomFace").intValue = (int)FaceId.Floor;
            var players = serializedObject.FindProperty("playerSpawns");
            players.arraySize = 1;
            var player = players.GetArrayElementAtIndex(0);
            player.FindPropertyRelative("EntityId").intValue = 1;
            player.FindPropertyRelative("Kind").intValue = (int)StageSpawnKind.Player;
            var cell = player.FindPropertyRelative("Cell");
            cell.FindPropertyRelative("face").intValue = (int)FaceId.Floor;
            cell.FindPropertyRelative("x").intValue = 0;
            cell.FindPropertyRelative("y").intValue = 0;
            player.FindPropertyRelative("Facing").intValue = (int)Direction.Right;
            player.FindPropertyRelative("Hp").intValue = 1;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFirstPlayerHp(StageDefinition gameplay, int hp)
        {
            var serializedObject = new SerializedObject(gameplay);
            serializedObject
                .FindProperty("playerSpawns")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("Hp")
                .intValue = hp;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class GovernanceFixture
        {
            private readonly UnityEngine.Object[] ownedObjects;

            public GovernanceFixture(
                StageContentEntry entry,
                StageAuthoringDefinition authoring,
                StageDefinition gameplay,
                StagePresentationDefinition presentation)
            {
                Entry = entry;
                Authoring = authoring;
                Gameplay = gameplay;
                ownedObjects = new UnityEngine.Object[] { entry, authoring, gameplay, presentation };
            }

            public StageContentEntry Entry { get; }

            public StageAuthoringDefinition Authoring { get; }

            public StageDefinition Gameplay { get; }

            public void Destroy()
            {
                for (var i = 0; i < ownedObjects.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }
        }
    }
}
