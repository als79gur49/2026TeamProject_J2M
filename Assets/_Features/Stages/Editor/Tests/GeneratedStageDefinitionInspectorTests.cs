using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class GeneratedStageDefinitionInspectorTests
    {
        private const string CampaignStageRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/Levels/level-01/Stages";
        private const string TemporaryRoot = "Assets/__GeneratedStageDefinitionInspectorTests";

        private static readonly string[] ExpectedStageNames =
        {
            "legacy-stage-5-1",
            "stage-0-1",
            "stage-0-2",
            "stage-1-1",
            "stage-2-1",
            "stage-2-2",
            "stage-3-1",
            "stage-3-2",
            "stage-4-1",
            "stage-4-2",
        };

        [SetUp]
        public void SetUp()
        {
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = null;
            if (AssetDatabase.IsValidFolder(TemporaryRoot))
            {
                AssetDatabase.DeleteAsset(TemporaryRoot);
            }

            StageGeneratedDefinitionOwnershipIndex.ResetForTests();
        }

        [Test]
        public void RepeatedResolve_DoesNotRescanAssetDatabase()
        {
            EnsureTemporaryRoot();
            var stage = CreateAsset<StageDefinition>($"{TemporaryRoot}/target.asset");
            CreateOwnerAsset($"{TemporaryRoot}/owner.asset", stage);
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(stage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(stage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(1));
        }

        [Test]
        public void ProjectChange_InvalidatesOwnershipIndex()
        {
            EnsureTemporaryRoot();
            var stage = CreateAsset<StageDefinition>($"{TemporaryRoot}/target.asset");
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(stage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
            CreateOwnerAsset($"{TemporaryRoot}/owner.asset", stage);
            StageGeneratedDefinitionOwnershipIndex.NotifyProjectChangedForTests();

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(stage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(2));
        }

        [Test]
        public void AssetDelete_InvalidatesOwnershipIndex()
        {
            EnsureTemporaryRoot();
            var stage = CreateAsset<StageDefinition>($"{TemporaryRoot}/target.asset");
            var ownerPath = $"{TemporaryRoot}/owner.asset";
            CreateOwnerAsset(ownerPath, stage);
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();
            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(stage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));

            Assert.That(AssetDatabase.DeleteAsset(ownerPath), Is.True);

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(stage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
        }

        [Test]
        public void SavedOwnershipReferenceChange_InvalidatesOwnershipIndex()
        {
            EnsureTemporaryRoot();
            var firstStage = CreateAsset<StageDefinition>($"{TemporaryRoot}/first.asset");
            var secondStage = CreateAsset<StageDefinition>($"{TemporaryRoot}/second.asset");
            var owner = CreateOwnerAsset($"{TemporaryRoot}/owner.asset", firstStage);
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();
            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(firstStage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));

            owner.AssignGeneratedDefinitions(secondStage, null);
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssetIfDirty(owner);

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(firstStage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(secondStage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
        }

        [Test]
        public void UnsavedGeneratedGameplayDefinitionChange_InvalidatesOwnershipIndex()
        {
            EnsureTemporaryRoot();
            var previousStage = CreateAsset<StageDefinition>($"{TemporaryRoot}/previous.asset");
            var newStage = CreateAsset<StageDefinition>($"{TemporaryRoot}/new.asset");
            var owner = CreateOwnerAsset($"{TemporaryRoot}/owner.asset", previousStage);
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(previousStage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(1));

            var serialized = new SerializedObject(owner);
            var previousReference = owner.GeneratedGameplayDefinition;
            serialized.FindProperty("generatedGameplayDefinition").objectReferenceValue = newStage;
            Assert.That(
                StageAuthoringDefinitionEditor.ApplyModifiedPropertiesAndInvalidateOwnership(
                    serialized,
                    previousReference),
                Is.True);
            Assert.That(EditorUtility.IsDirty(owner), Is.True);

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(previousStage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(newStage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(2));

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(previousStage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(newStage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(2));
        }

        [Test]
        public void UnrelatedAuthoringPropertyChange_DoesNotInvalidateOwnershipIndex()
        {
            EnsureTemporaryRoot();
            var stage = CreateAsset<StageDefinition>($"{TemporaryRoot}/target.asset");
            var owner = CreateOwnerAsset($"{TemporaryRoot}/owner.asset", stage);
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(stage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(1));

            var serialized = new SerializedObject(owner);
            serialized.FindProperty("enforceGeneratedSync").boolValue = !owner.EnforceGeneratedSync;
            Assert.That(
                StageAuthoringDefinitionEditor.ApplyModifiedPropertiesAndInvalidateOwnership(
                    serialized,
                    owner.GeneratedGameplayDefinition),
                Is.True);

            Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(stage).Kind,
                Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(1));
        }

        [Test]
        public void UndoRedoGeneratedReferenceChange_RefreshesOwnership()
        {
            EnsureTemporaryRoot();
            var previousStage = CreateAsset<StageDefinition>($"{TemporaryRoot}/previous.asset");
            var newStage = CreateAsset<StageDefinition>($"{TemporaryRoot}/new.asset");
            var owner = CreateOwnerAsset($"{TemporaryRoot}/owner.asset", previousStage);
            UnityEditor.Editor editor = null;
            try
            {
                Undo.RecordObject(owner, "Change generated gameplay definition");
                owner.AssignGeneratedDefinitions(newStage, null);
                EditorUtility.SetDirty(owner);
                Undo.FlushUndoRecordObjects();
                editor = UnityEditor.Editor.CreateEditor(owner);
                StageGeneratedDefinitionOwnershipIndex.ResetForTests();

                Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(newStage).Kind,
                    Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
                Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(1));

                Undo.PerformUndo();

                Assert.That(owner.GeneratedGameplayDefinition, Is.SameAs(previousStage));
                Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(previousStage).Kind,
                    Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
                Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(newStage).Kind,
                    Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
                Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(2));

                Undo.PerformRedo();

                Assert.That(owner.GeneratedGameplayDefinition, Is.SameAs(newStage));
                Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(previousStage).Kind,
                    Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
                Assert.That(StageGeneratedDefinitionOwnershipResolver.Resolve(newStage).Kind,
                    Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
                Assert.That(StageGeneratedDefinitionOwnershipIndex.BuildInvocationCountForTests, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
                Undo.ClearUndo(owner);
            }
        }

        [Test]
        public void GeneratedOwnership_RemainsCorrectAfterRebuild()
        {
            EnsureTemporaryRoot();
            var stage = CreateAsset<StageDefinition>($"{TemporaryRoot}/target.asset");
            var owner = CreateOwnerAsset($"{TemporaryRoot}/owner.asset", stage);
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();

            var initial = StageGeneratedDefinitionOwnershipResolver.Resolve(stage);
            StageGeneratedDefinitionOwnershipIndex.NotifyProjectChangedForTests();
            var rebuilt = StageGeneratedDefinitionOwnershipResolver.Resolve(stage);

            Assert.That(initial.Kind, Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(rebuilt.Kind, Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(rebuilt.Owner, Is.SameAs(owner));
        }

        [Test]
        public void AmbiguousOwnership_RemainsAmbiguous()
        {
            EnsureTemporaryRoot();
            var stage = CreateAsset<StageDefinition>($"{TemporaryRoot}/target.asset");
            CreateOwnerAsset($"{TemporaryRoot}/owner-b.asset", stage);
            CreateOwnerAsset($"{TemporaryRoot}/owner-a.asset", stage);
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();

            var ownership = StageGeneratedDefinitionOwnershipResolver.Resolve(stage);

            Assert.That(ownership.Kind, Is.EqualTo(StageDefinitionOwnershipKind.Ambiguous));
            CollectionAssert.AreEqual(
                new[] { $"{TemporaryRoot}/owner-a.asset", $"{TemporaryRoot}/owner-b.asset" },
                ownership.Owners.Select(owner => owner.AssetPath));
        }

        [Test]
        public void Standalone_RemainsStandalone()
        {
            EnsureTemporaryRoot();
            var stage = CreateAsset<StageDefinition>($"{TemporaryRoot}/target.asset");
            StageGeneratedDefinitionOwnershipIndex.ResetForTests();

            var ownership = StageGeneratedDefinitionOwnershipResolver.Resolve(stage);

            Assert.That(ownership.Kind, Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
            Assert.That(ownership.Owners, Is.Empty);
        }

        [Test]
        public void CampaignInventory_HasTenExplicitSingleOwnerGeneratedDefinitions()
        {
            var stageGuids = AssetDatabase.FindAssets("t:StageDefinition", new[] { CampaignStageRoot });
            var stagePaths = stageGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".asset", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(stagePaths, Has.Length.EqualTo(10));
            CollectionAssert.AreEquivalent(
                ExpectedStageNames.Select(StagePath),
                stagePaths);

            var ownerships = stagePaths
                .Select(AssetDatabase.LoadAssetAtPath<StageDefinition>)
                .Select(StageGeneratedDefinitionOwnershipResolver.Resolve)
                .ToArray();
            Assert.That(
                ownerships.Count(ownership => ownership.Kind == StageDefinitionOwnershipKind.GeneratedOwned),
                Is.EqualTo(10));
            Assert.That(
                ownerships.Count(ownership => ownership.Kind == StageDefinitionOwnershipKind.Standalone),
                Is.Zero);
            Assert.That(
                ownerships.Count(ownership => ownership.Kind == StageDefinitionOwnershipKind.Ambiguous),
                Is.Zero);

            foreach (var ownership in ownerships)
            {
                Assert.That(ownership.Owners.Count, Is.EqualTo(1), ownership.TargetPath);
                Assert.That(ownership.Owner.GeneratedGameplayDefinition, Is.SameAs(ownership.Target));
                Assert.That(ownership.TargetGuid, Is.Not.Empty);
                Assert.That(ownership.Owners[0].AssetGuid, Is.Not.Empty);
                Assert.That(
                    ownership.Owners[0].AssetPath,
                    Is.EqualTo(AuthoringPath(ownership.Target.name)));
            }
        }

        [Test]
        public void Resolver_ClassifiesStandaloneGeneratedAndAmbiguous_WithDeterministicOwners()
        {
            EnsureTemporaryRoot();
            var stage = CreateAsset<StageDefinition>($"{TemporaryRoot}/target.asset");

            var standalone = StageGeneratedDefinitionOwnershipResolver.Resolve(stage);
            Assert.That(standalone.Kind, Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
            Assert.That(standalone.Owners, Is.Empty);

            var ownerB = CreateOwnerAsset($"{TemporaryRoot}/owner-b.asset", stage);
            var generated = StageGeneratedDefinitionOwnershipResolver.Resolve(stage);
            Assert.That(generated.Kind, Is.EqualTo(StageDefinitionOwnershipKind.GeneratedOwned));
            Assert.That(generated.Owner, Is.SameAs(ownerB));

            var ownerA = CreateOwnerAsset($"{TemporaryRoot}/owner-a.asset", stage);
            var ambiguous = StageGeneratedDefinitionOwnershipResolver.Resolve(stage);
            Assert.That(ambiguous.Kind, Is.EqualTo(StageDefinitionOwnershipKind.Ambiguous));
            Assert.That(ambiguous.Owners.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(
                new[]
                {
                    $"{TemporaryRoot}/owner-a.asset",
                    $"{TemporaryRoot}/owner-b.asset",
                },
                ambiguous.Owners.Select(owner => owner.AssetPath));
            Assert.That(ambiguous.Owners[0].Authoring, Is.SameAs(ownerA));
            Assert.That(ambiguous.Owner, Is.Null);
        }

        [Test]
        public void Resolver_NullTarget_IsNullSafeStandalone()
        {
            var ownership = StageGeneratedDefinitionOwnershipResolver.Resolve(null);

            Assert.That(ownership.Kind, Is.EqualTo(StageDefinitionOwnershipKind.Standalone));
            Assert.That(ownership.Target, Is.Null);
            Assert.That(ownership.Owners, Is.Empty);
        }

        [Test]
        public void Policy_PreservesStandaloneEditingAndLocksGeneratedAndAmbiguous()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var ownerA = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var ownerB = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                var standalone = Ownership(stage);
                var generated = Ownership(stage, Owner(ownerA, "Assets/owner-a.asset"));
                var ambiguous = Ownership(
                    stage,
                    Owner(ownerA, "Assets/owner-a.asset"),
                    Owner(ownerB, "Assets/owner-b.asset"));

                Assert.That(StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { generated }), Is.False);
                Assert.That(StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { standalone }), Is.True);
                Assert.That(StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { ambiguous }), Is.False);
                Assert.That(StageDefinitionInspectorPolicy.CanUseCanonicalActions(new[] { generated }), Is.True);
                Assert.That(StageDefinitionInspectorPolicy.CanUseCanonicalActions(new[] { standalone }), Is.False);
                Assert.That(StageDefinitionInspectorPolicy.CanUseCanonicalActions(new[] { ambiguous }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerB);
                UnityEngine.Object.DestroyImmediate(ownerA);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void Policy_MultiSelectionCannotBypassGeneratedOrAmbiguousLock()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var ownerA = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var ownerB = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                var standalone = Ownership(stage);
                var generated = Ownership(stage, Owner(ownerA, "Assets/owner-a.asset"));
                var ambiguous = Ownership(
                    stage,
                    Owner(ownerA, "Assets/owner-a.asset"),
                    Owner(ownerB, "Assets/owner-b.asset"));

                Assert.That(
                    StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { standalone, standalone }),
                    Is.True);
                Assert.That(
                    StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { standalone, generated }),
                    Is.False);
                Assert.That(
                    StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { standalone, ambiguous }),
                    Is.False);
                Assert.That(
                    StageDefinitionInspectorPolicy.CanUseCanonicalActions(new[] { generated, standalone }),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerB);
                UnityEngine.Object.DestroyImmediate(ownerA);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void GeneratedGameplayCompanion_IsEditableWithoutUnlockingGeneratedFields()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var ownerA = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var ownerB = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                var standalone = Ownership(stage);
                var generated = Ownership(stage, Owner(ownerA, "Assets/owner-a.asset"));
                var ambiguous = Ownership(
                    stage,
                    Owner(ownerA, "Assets/owner-a.asset"),
                    Owner(ownerB, "Assets/owner-b.asset"));

                Assert.That(StageDefinitionInspectorPolicy.CanEditGameplayCompanion(new[] { generated }), Is.True);
                Assert.That(StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { generated }), Is.False);
                Assert.That(StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { standalone }), Is.True);
                Assert.That(StageDefinitionInspectorPolicy.CanEditGameplayCompanion(new[] { ambiguous }), Is.False);
                Assert.That(
                    StageDefinitionInspectorPolicy.CanEditGameplayCompanion(new[] { generated, standalone }),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerB);
                UnityEngine.Object.DestroyImmediate(ownerA);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void OpenAuthoring_SelectsExactSingleOwnerWithoutMutation()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var owner = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                var ownership = Ownership(stage, Owner(owner, "Assets/owner.asset"));
                var ownerBefore = EditorJsonUtility.ToJson(owner);
                var stageBefore = EditorJsonUtility.ToJson(stage);

                Assert.That(StageDefinitionInspectorActions.OpenAuthoring(ownership), Is.True);

                Assert.That(Selection.activeObject, Is.SameAs(owner));
                Assert.That(EditorJsonUtility.ToJson(owner), Is.EqualTo(ownerBefore));
                Assert.That(EditorJsonUtility.ToJson(stage), Is.EqualTo(stageBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void OpenAuthoring_IsUnavailableForStandaloneAndAmbiguousOwnership()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var ownerA = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var ownerB = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                Assert.That(StageDefinitionInspectorActions.OpenAuthoring(Ownership(stage)), Is.False);
                Assert.That(
                    StageDefinitionInspectorActions.OpenAuthoring(
                        Ownership(stage, Owner(ownerA, "a"), Owner(ownerB, "b"))),
                    Is.False);
                Assert.That(Selection.activeObject, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerB);
                UnityEngine.Object.DestroyImmediate(ownerA);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void ValidateAuthoring_UsesDryRunAndDoesNotMutatePair()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));
                var authoringBefore = EditorJsonUtility.ToJson(fixture.Authoring);
                var gameplayBefore = EditorJsonUtility.ToJson(fixture.Gameplay);
                var presentationBefore = EditorJsonUtility.ToJson(fixture.Presentation);

                var report = StageDefinitionInspectorActions.ValidateAuthoring(ownership);

                Assert.That(report, Is.Not.Null);
                Assert.That(report.HasErrors, Is.False, StageAuthoringTestFixture.FormatGenerationIssues(report));
                Assert.That(EditorJsonUtility.ToJson(fixture.Authoring), Is.EqualTo(authoringBefore));
                Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.EqualTo(gameplayBefore));
                Assert.That(EditorJsonUtility.ToJson(fixture.Presentation), Is.EqualTo(presentationBefore));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void InitialOpen_InvalidAuthoring_IsNotInSync()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var mappings = fixture.Authoring.EntityIdMappings.ToList();
                mappings.Add(new StageAuthoringIdMapping
                {
                    StableGuid = string.Empty,
                    EntityId = 999,
                    LastKnownDisplayName = "invalid",
                });
                fixture.Authoring.SetEntityIdMappings(mappings);
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));

                Assert.That(
                    StageDefinitionInspectorActions.ResolveSyncStatus(ownership),
                    Is.EqualTo(StageDefinitionGeneratedSyncStatus.InvalidAuthoring));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void DuplicateOrInvalidEntityIdMapping_ReportsInvalidAuthoring()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var mappings = fixture.Authoring.EntityIdMappings.ToArray();
                mappings[1].EntityId = mappings[0].EntityId;
                fixture.Authoring.SetEntityIdMappings(mappings);
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));

                var report = StageDefinitionInspectorActions.ValidateAuthoring(ownership);

                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "authoring.entity-id-mapping.id-duplicate"), Is.True);
                Assert.That(
                    StageDefinitionInspectorActions.ResolveSyncStatus(ownership),
                    Is.EqualTo(StageDefinitionGeneratedSyncStatus.InvalidAuthoring));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void ValidAuthoringAndMatchingOutputs_ReportsInSync()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));

                Assert.That(
                    StageDefinitionInspectorActions.ResolveSyncStatus(ownership),
                    Is.EqualTo(StageDefinitionGeneratedSyncStatus.InSync));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void PresentationOnlyDrift_ReportsGenerateRequired()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var serialized = new SerializedObject(fixture.Presentation);
                var bindings = serialized.FindProperty("enemyPresentationBindings");
                Assert.That(bindings.arraySize, Is.GreaterThan(0));
                bindings.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("PresentationId")
                    .stringValue = "stale-enemy-view";
                Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));

                Assert.That(
                    StageDefinitionInspectorActions.ResolveSyncStatus(ownership),
                    Is.EqualTo(StageDefinitionGeneratedSyncStatus.GenerateRequired));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void GameplayAndPresentationInSync_ReportsInSync()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));

                Assert.That(
                    StageDefinitionInspectorActions.ResolveSyncStatus(ownership),
                    Is.EqualTo(StageDefinitionGeneratedSyncStatus.InSync));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void GameplayDrift_StillReportsGenerateRequired()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var changedBoard = fixture.Authoring.Board;
                changedBoard.MaxInclusive = new Vector2Int(5, 5);
                fixture.Authoring.SetBoard(changedBoard);
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));

                Assert.That(
                    StageDefinitionInspectorActions.ResolveSyncStatus(ownership),
                    Is.EqualTo(StageDefinitionGeneratedSyncStatus.GenerateRequired));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void GenerateFromAuthoring_PreservesEnemyUnitArchetypeCatalog()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            var catalog = ScriptableObject.CreateInstance<EnemyUnitArchetypeCatalog>();
            try
            {
                var serialized = new SerializedObject(fixture.Gameplay);
                serialized.FindProperty("enemyUnitArchetypeCatalog").objectReferenceValue = catalog;
                Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));

                var report = StageDefinitionInspectorActions.GenerateFromAuthoring(ownership);

                Assert.That(report.HasErrors, Is.False, StageAuthoringTestFixture.FormatGenerationIssues(report));
                Assert.That(fixture.Gameplay.EnemyUnitArchetypeCatalog, Is.SameAs(catalog));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                fixture.Destroy();
            }
        }

        [Test]
        public void GenerateFromAuthoring_UsesCanonicalWriterRestoresParityAndIsIdempotent()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            EnsureTemporaryRoot();
            var sentinel = CreateAsset<TestSentinelAsset>($"{TemporaryRoot}/sentinel.asset");
            sentinel.SetValue("persisted");
            EditorUtility.SetDirty(sentinel);
            AssetDatabase.SaveAssetIfDirty(sentinel);
            var sentinelPath = Path.GetFullPath($"{TemporaryRoot}/sentinel.asset");
            var sentinelBytesBefore = File.ReadAllBytes(sentinelPath);
            try
            {
                sentinel.SetValue("dirty-only");
                EditorUtility.SetDirty(sentinel);
                var changedBoard = fixture.Authoring.Board;
                changedBoard.MaxInclusive = new Vector2Int(5, 5);
                fixture.Authoring.SetBoard(changedBoard);
                var ownership = Ownership(fixture.Gameplay, Owner(fixture.Authoring, "authoring"));
                Assert.That(
                    StageDefinitionInspectorActions.ResolveSyncStatus(ownership),
                    Is.EqualTo(StageDefinitionGeneratedSyncStatus.GenerateRequired));

                var firstReport = StageDefinitionInspectorActions.GenerateFromAuthoring(ownership);
                Assert.That(firstReport.HasErrors, Is.False, StageAuthoringTestFixture.FormatGenerationIssues(firstReport));
                Assert.That(
                    StageDefinitionInspectorActions.ResolveSyncStatus(ownership),
                    Is.EqualTo(StageDefinitionGeneratedSyncStatus.InSync));
                var gameplayAfterFirst = EditorJsonUtility.ToJson(fixture.Gameplay);
                var presentationAfterFirst = EditorJsonUtility.ToJson(fixture.Presentation);

                var secondReport = StageDefinitionInspectorActions.GenerateFromAuthoring(ownership);
                Assert.That(secondReport.HasErrors, Is.False, StageAuthoringTestFixture.FormatGenerationIssues(secondReport));
                Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.EqualTo(gameplayAfterFirst));
                Assert.That(EditorJsonUtility.ToJson(fixture.Presentation), Is.EqualTo(presentationAfterFirst));
                CollectionAssert.AreEqual(sentinelBytesBefore, File.ReadAllBytes(sentinelPath));
                Assert.That(EditorUtility.IsDirty(sentinel), Is.True);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void CanonicalActions_RejectStandaloneAndAmbiguousOwnership()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var ownerA = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var ownerB = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                Assert.That(StageDefinitionInspectorActions.ValidateAuthoring(Ownership(stage)), Is.Null);
                Assert.That(StageDefinitionInspectorActions.GenerateFromAuthoring(Ownership(stage)), Is.Null);
                var ambiguous = Ownership(stage, Owner(ownerA, "a"), Owner(ownerB, "b"));
                Assert.That(StageDefinitionInspectorActions.ValidateAuthoring(ambiguous), Is.Null);
                Assert.That(StageDefinitionInspectorActions.GenerateFromAuthoring(ambiguous), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerB);
                UnityEngine.Object.DestroyImmediate(ownerA);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void StandaloneSerializedEditingAndUndoRedoRemainAvailable()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            try
            {
                Assert.That(
                    StageDefinitionInspectorPolicy.IsSelectionEditable(new[] { Ownership(stage) }),
                    Is.True);
                var original = stage.Board.MinInclusive;
                Undo.RecordObject(stage, "Edit standalone StageDefinition");
                var serialized = new SerializedObject(stage);
                serialized.FindProperty("board").FindPropertyRelative("MinInclusive").vector2IntValue =
                    new Vector2Int(2, 3);
                Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                Assert.That(stage.Board.MinInclusive, Is.EqualTo(new Vector2Int(2, 3)));

                Undo.PerformUndo();
                Assert.That(stage.Board.MinInclusive, Is.EqualTo(original));
                Undo.PerformRedo();
                Assert.That(stage.Board.MinInclusive, Is.EqualTo(new Vector2Int(2, 3)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void UnityResolvesStageDefinitionCustomEditor()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            UnityEditor.Editor editor = null;
            try
            {
                editor = UnityEditor.Editor.CreateEditor(stage);
                Assert.That(editor, Is.TypeOf<StageDefinitionEditor>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void InspectorArchitectureGuard_UsesDisabledDefaultInspectorAndExplicitReferencesOnly()
        {
            var inspectorSource = File.ReadAllText(
                "Assets/_Features/Stages/Editor/Authoring/StageDefinitionEditor.cs");
            var resolverSource = File.ReadAllText(
                "Assets/_Features/Stages/Editor/Authoring/StageGeneratedDefinitionOwnershipResolver.cs");
            var writerSource = File.ReadAllText(
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringGeneratedAssetWriter.cs");

            Assert.That(inspectorSource, Does.Contain("[CustomEditor(typeof(StageDefinition))]"));
            Assert.That(inspectorSource, Does.Contain("using (new EditorGUI.DisabledScope(true))"));
            Assert.That(inspectorSource, Does.Contain("DrawReadOnlyDefaultInspector();"));
            Assert.That(Count(inspectorSource, "DrawDefaultInspector();"), Is.EqualTo(2));
            Assert.That(resolverSource, Does.Contain("authoring.GeneratedGameplayDefinition"));
            Assert.That(resolverSource, Does.Not.Contain("GetFileName"));
            Assert.That(resolverSource, Does.Not.Contain("_generated"));
            Assert.That(resolverSource, Does.Not.Contain("StageId"));
            Assert.That(inspectorSource, Does.Not.Contain("AssetDatabase.SaveAssets"));
            Assert.That(inspectorSource, Does.Contain("StageAuthoringGenerateOptions.DryRunValidation"));
            Assert.That(inspectorSource, Does.Contain("StageAuthoringGenerateOptions.WriteAll"));
            Assert.That(inspectorSource, Does.Contain("DrawPropertiesExcluding(serializedObject, \"enemyUnitArchetypeCatalog\")"));
            Assert.That(inspectorSource, Does.Contain("serializedObject.FindProperty(\"enemyUnitArchetypeCatalog\")"));
            Assert.That(resolverSource, Does.Contain("EditorApplication.projectChanged += Invalidate"));
            Assert.That(resolverSource, Does.Contain("StageGeneratedDefinitionOwnershipIndex.GetOwners(target)"));
            var authoringInspectorSource = File.ReadAllText(
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringDefinitionEditor.cs");
            Assert.That(authoringInspectorSource, Does.Contain("ApplyModifiedPropertiesAndInvalidateOwnership"));
            Assert.That(authoringInspectorSource, Does.Contain("Undo.undoRedoPerformed += HandleOwnershipUndoRedo"));
            Assert.That(writerSource, Does.Contain("StageAuthoringGenerationSaveSet.SaveTouchedAssets(plan, options);"));
            Assert.That(writerSource, Does.Not.Contain("AssetDatabase.SaveAssets"));
            Assert.That(writerSource, Does.Not.Contain("enemyUnitArchetypeCatalog"));
        }

        private static StageGeneratedDefinitionOwnership Ownership(
            StageDefinition target,
            params StageGeneratedDefinitionOwner[] owners)
        {
            return new StageGeneratedDefinitionOwnership(target, "target", "target-guid", owners);
        }

        private static StageGeneratedDefinitionOwner Owner(
            StageAuthoringDefinition owner,
            string path)
        {
            return new StageGeneratedDefinitionOwner(owner, path, $"{path}-guid");
        }

        private static T CreateAsset<T>(string path)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static StageAuthoringDefinition CreateOwnerAsset(
            string path,
            StageDefinition stage)
        {
            var owner = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            owner.AssignGeneratedDefinitions(stage, null);
            AssetDatabase.CreateAsset(owner, path);
            AssetDatabase.SaveAssetIfDirty(owner);
            return owner;
        }

        private static void EnsureTemporaryRoot()
        {
            if (!AssetDatabase.IsValidFolder(TemporaryRoot))
            {
                AssetDatabase.CreateFolder("Assets", "__GeneratedStageDefinitionInspectorTests");
            }
        }

        private static int Count(string source, string token)
        {
            return source.Split(new[] { token }, StringSplitOptions.None).Length - 1;
        }

        private static string StagePath(string stageName)
        {
            return $"{CampaignStageRoot}/{stageName}/{stageName}.asset";
        }

        private static string AuthoringPath(string stageName)
        {
            return $"{CampaignStageRoot}/{stageName}/{stageName}_Authoring.asset";
        }
    }
}
