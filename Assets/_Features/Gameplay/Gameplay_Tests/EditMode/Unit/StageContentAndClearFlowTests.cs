using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StageContentAndClearFlowTests
    {
        [Test]
        public void StageIdNormalizer_NormalizesWhitespaceUnderscoreAndSlash_IntoCanonicalLowerKebabCase()
        {
            Assert.That(StageIdNormalizer.Normalize(" Boss_Stage/01 "), Is.EqualTo("boss-stage-01"));
            Assert.That(StageId.TryCreate(" Boss_Stage/01 ", out var stageId), Is.True);
            Assert.That(stageId.Value, Is.EqualTo("boss-stage-01"));
            Assert.That(StageIdNormalizer.TryNormalize("boss@stage", out _, out _), Is.False);
        }

        [Test]
        public void StageCatalogResolver_ResolvesDeprecatedAlias_ToCurrentStageId()
        {
            var entry = CreateEntry("tutorial-stage");
            var aliasTable = ScriptableObject.CreateInstance<StageIdAliasTable>();
            aliasTable.SetEntries(new[]
            {
                new StageIdAliasEntry
                {
                    DeprecatedStageId = " Tutorial Stage ",
                    CurrentStageId = entry.StageId,
                },
            });

            var resolver = new StageCatalogResolver(new TestStageCatalogProvider(new[] { entry }, aliasTable));

            Assert.That(resolver.TryResolve("Tutorial Stage", out var resolvedEntry), Is.True);
            Assert.That(resolvedEntry, Is.SameAs(entry));
        }

        [Test]
        public void StageCatalogResolver_ResolveOrThrow_ByCanonicalStageId_ReturnsEntry()
        {
            var entry = CreateEntry("catalog-stage-a");
            var resolver = new StageCatalogResolver(new TestStageCatalogProvider(new[] { entry }, aliasTable: null));

            var resolved = resolver.ResolveOrThrow(StageId.CreateOrThrow("catalog-stage-a"));

            Assert.That(resolved, Is.SameAs(entry));
        }

        [Test]
        public void StageRuntimeContentResolver_CatalogMode_ResolvesLaunchContextOnly()
        {
            StageLaunchContextStore.Clear();
            try
            {
                var selectedEntry = CreateEntry("catalog-stage-b");
                var provider = CreateCatalogProvider(new[] { CreateEntry("catalog-stage-a"), selectedEntry }, aliasTable: null);
                var resolver = new StageRuntimeContentResolver();
                StageLaunchContextStore.SetCurrent(selectedEntry.StageId);

                var resolved = resolver.Resolve(
                    StageLoadRequest.CreateLaunchContextOnly(
                        provider,
                        sceneName: "StageRuntimeContentResolverTests"));

                Assert.That(resolved.Entry, Is.SameAs(selectedEntry));
                Assert.That(resolved.UsedLaunchContext, Is.True);
            }
            finally
            {
                StageLaunchContextStore.Clear();
            }
        }

        [Test]
        public void StageRuntimeContentResolver_CatalogMode_ThrowsWhenLaunchContextIsMissing()
        {
            StageLaunchContextStore.Clear();
            var entry = CreateEntry("catalog-stage-a");
            var provider = CreateCatalogProvider(new[] { entry }, aliasTable: null);
            var resolver = new StageRuntimeContentResolver();

            var exception = Assert.Throws<InvalidOperationException>(
                () => resolver.Resolve(
                    StageLoadRequest.CreateLaunchContextOnly(
                        provider,
                        sceneName: "StageRuntimeContentResolverTests")));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("Tools/Stages/Direct Play/Launch Stage", exception.Message);
        }

        [Test]
        public void StageCatalogValidator_DetectsDuplicateStageIds_AndSharedPresentationReuse()
        {
            var sharedPresentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var entryA = CreateEntry("shared-stage", presentationDefinition: sharedPresentation);
            var entryB = CreateEntry("shared-stage", presentationDefinition: sharedPresentation);
            sharedPresentation.SetOwnerMetadata(entryA, string.Empty);

            var validator = new StageCatalogValidator();
            var report = validator.ValidateEntries(
                new[] { entryA, entryB },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    RequirePresentationDefinition = true,
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "stage-id.duplicate"), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == "companion.presentation.reused"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_MissingAudioCompanion_ReportsErrorWhenRequired()
        {
            var entry = CreateEntry("stage-a");
            entry.AssignAudioDefinition(null);

            var validator = new StageCatalogValidator();
            var report = validator.ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    RequirePresentationDefinition = true,
                    RequireAudioDefinition = true,
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "companion.audio.null"), Is.True);
        }

        [Test]
        public void StageContentEntry_DoesNotExposeRewardProgressionClearEvaluationCompanions()
        {
            var memberNames = typeof(StageContentEntry)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(memberNames, Has.No.Member("ClearEvaluationDefinition"));
            Assert.That(memberNames, Has.No.Member("RewardDefinition"));
            Assert.That(memberNames, Has.No.Member("ProgressionDefinition"));
            Assert.That(memberNames, Has.No.Member("AssignClearEvaluationDefinition"));
            Assert.That(memberNames, Has.No.Member("AssignRewardDefinition"));
            Assert.That(memberNames, Has.No.Member("AssignProgressionDefinition"));
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_WithValidPrefab_Passes()
        {
            var prefab = CreateTileFeatureVisualPrefab("ButtonTileVisualPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 100, prefab);

            var report = ValidateSingleEntry(entry);

            Assert.That(
                report.Issues.Any(issue => issue.Code.StartsWith("presentation.tile-feature", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsNonPositiveTileId()
        {
            var prefab = CreateTileFeatureVisualPrefab("ButtonTileVisualPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 0, prefab);

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.tile-id-non-positive"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsDuplicateTileId()
        {
            var prefab = CreateTileFeatureVisualPrefab("ButtonTileVisualPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(
                gameplayTileId: 100,
                bindings: new[]
                {
                    new TileFeaturePresentationBinding
                    {
                        TileId = 100,
                        VisualPrefab = prefab,
                    },
                    new TileFeaturePresentationBinding
                    {
                        TileId = 100,
                        VisualPrefab = prefab,
                    },
                });

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.tile-id-duplicate"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsMissingGameplayTileFeature()
        {
            var prefab = CreateTileFeatureVisualPrefab("ButtonTileVisualPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 404, prefab);

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.tile-id-missing"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_TileFeaturePresentationSelection_AllowsNullOptionalDirectOverride()
        {
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 100, visualPrefab: null);

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.prefab-null"), Is.False);
        }

        [Test]
        public void StageCatalogValidator_TileFeatureVisualBinding_RejectsPrefabWithoutTarget()
        {
            var prefab = new GameObject("MissingTargetPrefab");
            var entry = CreateEntryWithTileFeatureVisualBinding(100, 100, prefab);

            var report = ValidateSingleEntry(entry);

            Assert.That(report.Issues.Any(issue => issue.Code == "presentation.tile-feature.prefab-target-missing"), Is.True);
        }

        [Test]
        public void StageCatalogValidator_InvalidGameplayDefinition_ReportsError()
        {
            var entry = CreateEntry("invalid-objective");
            var invalidStage = CreateMinimalStageDefinition("invalid-objective");
            SetPrivateField(
                invalidStage,
                "objective",
                new StageObjectiveAuthoring
                {
                    CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                    ConditionEntries = Array.Empty<StageObjectiveConditionEntry>(),
                });
            entry.AssignGameplayDefinition(invalidStage);

            var validator = new StageCatalogValidator();
            var report = validator.ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "gameplay.definition.invalid"), Is.True);
            Assert.That(
                report.Issues.Any(issue => issue.Message.Contains("requires at least one required condition")),
                Is.True);
        }

        [Test]
        public void CampaignStageObjectiveAudit_FlagsDisabledObjectiveInProductionStages()
        {
            var entry = CreateEntry("stage-2-2");
            entry.AssignCampaignParticipation(CampaignParticipation.Campaign);

            var report = new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "objective.production-disabled"), Is.True);
        }

        [Test]
        public void ObjectiveConditionEntry_MissingAuthoringLabel_WarnsOrErrors()
        {
            var entry = CreateEntry("stage-2-2");
            entry.AssignCampaignParticipation(CampaignParticipation.Campaign);
            var condition = CreatePlayerAtExitCondition();
            AssignExitObjective(
                entry.GameplayDefinition,
                condition,
                authoringLabel: string.Empty);

            var report = new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "objective.authoring-label-missing"), Is.True);

            UnityEngine.Object.DestroyImmediate(condition);
        }

        [Test]
        public void ObjectiveConditionEntry_ValidAuthoringLabel_DoesNotReportMissingLabel()
        {
            var entry = CreateEntry("stage-2-2");
            entry.AssignCampaignParticipation(CampaignParticipation.Campaign);
            var condition = CreatePlayerAtExitCondition();
            AssignExitObjective(
                entry.GameplayDefinition,
                condition,
                authoringLabel: "Reach the exit zone");

            var report = new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(
                report.Issues.Any(issue => issue.Code == "objective.authoring-label-missing"),
                Is.False);

            UnityEngine.Object.DestroyImmediate(condition);
        }

        [Test]
        public void ObjectiveStableConditionId_Duplicate_WarnsOrErrors()
        {
            var entry = CreateEntry("stage-2-2");
            var condition = CreatePlayerAtExitCondition();
            AssignExitObjective(
                entry.GameplayDefinition,
                condition,
                authoringLabel: "Reach the exit zone",
                duplicateStableId: true);

            var report = new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(
                report.Issues.Any(issue =>
                    issue.Code == "gameplay.definition.invalid" &&
                    issue.Message.Contains("duplicate condition stable id")),
                Is.True);

            UnityEngine.Object.DestroyImmediate(condition);
        }

        [Test]
        public void StageSessionTracker_FreshInstancesStartIndependentLifecyclesAndAllowTerminalCompletion()
        {
            var firstTracker = new StageSessionTracker();
            var stageId = StageId.CreateOrThrow("session-stage");
            var firstState = firstTracker.Start(stageId);
            var tickResult = new TickResult(3, new[] { TickPhase.Plan, TickPhase.Resolve }, Array.Empty<string>());

            Assert.That(firstState.StageId, Is.EqualTo(stageId));
            Assert.That(firstState.CurrentTickIndex, Is.EqualTo(0));
            Assert.That(firstState.IsTerminal, Is.False);
            Assert.That(firstState.TerminalReason, Is.EqualTo(StageTerminalReason.None));

            firstTracker.Advance(tickResult, forcedTerminalReason: StageTerminalReason.Cleared);

            Assert.That(firstTracker.TryCreateClearResult(out var clearResult), Is.True);
            Assert.That(clearResult.StageId, Is.EqualTo(stageId));
            Assert.That(clearResult.FinalTickIndex, Is.EqualTo(3));
            Assert.That(firstTracker.TryCreateClearResult(out _), Is.False);

            var secondTracker = new StageSessionTracker();
            var secondState = secondTracker.Start(stageId);

            Assert.That(secondState.StageId, Is.EqualTo(stageId));
            Assert.That(secondState.CurrentTickIndex, Is.EqualTo(0));
            Assert.That(secondState.IsTerminal, Is.False);
            Assert.That(secondState.TerminalReason, Is.EqualTo(StageTerminalReason.None));

            var secondTickResult = new TickResult(
                8,
                new[] { TickPhase.Plan, TickPhase.Resolve },
                Array.Empty<string>());
            secondTracker.Advance(secondTickResult, forcedTerminalReason: StageTerminalReason.Cleared);

            Assert.That(secondTracker.TryCreateClearResult(out var secondClearResult), Is.True);
            Assert.That(secondClearResult.StageId, Is.EqualTo(stageId));
            Assert.That(secondClearResult.FinalTickIndex, Is.EqualTo(8));
            Assert.That(secondTracker.TryCreateClearResult(out _), Is.False);
        }

        [Test]
        public void StageClear_EmitsMinimalCompletionOnce()
        {
            var tracker = new StageSessionTracker();
            var stageId = StageId.CreateOrThrow("minimal-completion-stage");
            tracker.Start(stageId);
            var tickResult = new TickResult(7, new[] { TickPhase.Plan, TickPhase.Resolve }, Array.Empty<string>());

            tracker.Advance(tickResult, forcedTerminalReason: StageTerminalReason.Cleared);

            Assert.That(tracker.TryCreateClearResult(out var clearResult), Is.True);
            var readModel = MinimalStageCompletionReadModelBuilder.Build(entry: null, clearResult);
            Assert.That(readModel.StageId, Is.EqualTo(stageId));
            Assert.That(readModel.FinalTickIndex, Is.EqualTo(7));
            Assert.That(readModel.ContinueRequest.IsValid, Is.True);
            Assert.That(readModel.RetryRequest.IsValid, Is.True);
            Assert.That(tracker.TryCreateClearResult(out _), Is.False);
        }

        [Test]
        public void StageSessionTracker_ObjectiveIsCleared_DrivesClearEligibility()
        {
            var tracker = new StageSessionTracker();
            var stageId = StageId.CreateOrThrow("objective-clear-stage");
            tracker.Start(stageId);

            tracker.Advance(CreateObjectiveTickResult(tickIndex: 4, isCleared: false));

            Assert.That(tracker.CurrentState.IsTerminal, Is.False);
            Assert.That(tracker.TryCreateClearResult(out _), Is.False);

            tracker.Advance(CreateObjectiveTickResult(tickIndex: 5, isCleared: true));

            Assert.That(tracker.CurrentState.IsTerminal, Is.True);
            Assert.That(
                tracker.CurrentState.TerminalReason,
                Is.EqualTo(StageTerminalReason.Cleared));
            Assert.That(tracker.TryCreateClearResult(out var clearResult), Is.True);
            Assert.That(clearResult.StageId, Is.EqualTo(stageId));
            Assert.That(clearResult.FinalTickIndex, Is.EqualTo(5));
        }

        [Test]
        public void StageClear_CatalogEntryWithEmptyDisplayNameKey_Throws()
        {
            var entry = CreateEntry("strict-completion-stage");
            var tracker = new StageSessionTracker();
            tracker.Start(entry.StageId);
            var tickResult = new TickResult(1, new[] { TickPhase.Plan, TickPhase.Resolve }, Array.Empty<string>());
            tracker.Advance(tickResult, forcedTerminalReason: StageTerminalReason.Cleared);
            Assert.That(tracker.TryCreateClearResult(out var clearResult), Is.True);

            Assert.Throws<InvalidOperationException>(
                () => MinimalStageCompletionReadModelBuilder.Build(entry, clearResult));
        }

        [Test]
        public void StageSessionState_ExposesOnlyLifecycleAndTerminalState()
        {
            var propertyNames = typeof(StageSessionState)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            Assert.That(
                propertyNames,
                Is.EquivalentTo(new[]
                {
                    "StageId",
                    "CurrentTickIndex",
                    "IsTerminal",
                    "TerminalReason",
                }));
            Assert.That(
                typeof(StageSessionState).Assembly.GetType(
                    "Game.Feature.Stages.StageObjectiveProgress" + "Snapshot"),
                Is.Null);
            Assert.That(
                typeof(StageSessionTracker).GetMethod(
                    "Reset",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Null);
        }

        [Test]
        public void PresentationBarrierKey_ExposesOnlyNoneAndButtonActivatedContract()
        {
            Assert.That(
                typeof(PresentationBarrierKey).GetMethod(
                    "StageClear",
                    BindingFlags.Static | BindingFlags.Public),
                Is.Null);
            Assert.That(
                typeof(PresentationBarrierKey).GetMethod(
                    "EntityExit",
                    BindingFlags.Static | BindingFlags.Public),
                Is.Null);
            Assert.That(
                typeof(PresentationBarrierKey).GetMethod(
                    "ObjectiveCondition",
                    BindingFlags.Static | BindingFlags.Public),
                Is.Null);
            Assert.That(
                typeof(PresentationBarrierKey).GetMethod(
                    nameof(PresentationBarrierKey.None),
                    Type.EmptyTypes),
                Is.Not.Null);
            Assert.That(
                typeof(PresentationBarrierKey).GetMethod(
                    nameof(PresentationBarrierKey.ButtonActivated),
                    new[] { typeof(int) }),
                Is.Not.Null);
            Assert.That(
                Enum.GetNames(typeof(PresentationBarrierKind)),
                Is.EquivalentTo(new[]
                {
                    nameof(PresentationBarrierKind.None),
                    nameof(PresentationBarrierKind.ButtonActivated),
                }));
            Assert.That((int)PresentationBarrierKind.None, Is.EqualTo(0));
            Assert.That((int)PresentationBarrierKind.ButtonActivated, Is.EqualTo(1));
        }

        [Test]
        public void StageConditionAssets_DoNotImplementMutableRuntimeInterfaces_Directly()
        {
            var conditionAssetTypes = typeof(StageConditionAsset).Assembly
                .GetTypes()
                .Where(type => typeof(StageConditionAsset).IsAssignableFrom(type) && !type.IsAbstract)
                .ToArray();

            Assert.That(conditionAssetTypes, Is.Not.Empty);
            for (var i = 0; i < conditionAssetTypes.Length; i++)
            {
                Assert.That(typeof(IStageConditionRuntime).IsAssignableFrom(conditionAssetTypes[i]), Is.False, conditionAssetTypes[i].FullName);
            }
        }

        private static StageContentEntry CreateEntry(
            string rawStageId,
            StagePresentationDefinition presentationDefinition = null,
            StageAudioDefinition audioDefinition = null)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            var stageId = StageId.CreateOrThrow(rawStageId);
            entry.AssignStageId(stageId);
            entry.name = $"{stageId.Value}_Entry";
            entry.AssignGameplayDefinition(CreateMinimalStageDefinition(stageId.Value));

            presentationDefinition ??= ScriptableObject.CreateInstance<StagePresentationDefinition>();
            audioDefinition ??= ScriptableObject.CreateInstance<StageAudioDefinition>();

            var ownerGuid = Guid.NewGuid().ToString("N");
            presentationDefinition.SetOwnerMetadata(entry, ownerGuid);
            audioDefinition.SetOwnerMetadata(entry, ownerGuid);

            entry.AssignPresentationDefinition(presentationDefinition);
            entry.AssignAudioDefinition(audioDefinition);
            return entry;
        }

        private static TickResult CreateObjectiveTickResult(int tickIndex, bool isCleared)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                TickPresentationData.Empty,
                string.Empty,
                Game.Feature.Gameplay.Debug.TickTrace.Empty,
                new StageObjectiveTickResult(
                    hasObjective: true,
                    goalReached: isCleared,
                    allConditionsSatisfied: isCleared,
                    clearedThisTick: isCleared,
                    isCleared,
                    Array.Empty<StageConditionStatus>()));
        }

        private static StageDefinition CreateMinimalStageDefinition(string stageName)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.name = stageName;
            SetPrivateField(
                stage,
                "board",
                new StageBoardDefinition
                {
                    MinInclusive = Vector2Int.zero,
                    MaxInclusive = new Vector2Int(2, 2),
                    InitialBottomFace = FaceId.Floor,
                });
            SetPrivateField(
                stage,
                "playerSpawns",
                new[]
                {
                    new StageSpawnDefinition
                    {
                        EntityId = 1,
                        Kind = StageSpawnKind.Player,
                        Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                        Facing = Direction.Right,
                        Hp = 3,
                    },
                });
            SetPrivateField(stage, "boxSpawns", Array.Empty<StageSpawnDefinition>());
            SetPrivateField(stage, "enemySpawns", Array.Empty<StageSpawnDefinition>());
            SetPrivateField(stage, "wallSpawns", Array.Empty<StageSpawnDefinition>());
            return stage;
        }

        private static StageContentEntry CreateEntryWithTileFeatureVisualBinding(
            int gameplayTileId,
            int bindingTileId,
            GameObject visualPrefab)
        {
            return CreateEntryWithTileFeatureVisualBinding(
                gameplayTileId,
                new[]
                {
                    new TileFeaturePresentationBinding
                    {
                        TileId = bindingTileId,
                        VisualPrefab = visualPrefab,
                    },
                });
        }

        private static StageContentEntry CreateEntryWithTileFeatureVisualBinding(
            int gameplayTileId,
            TileFeaturePresentationBinding[] bindings)
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentation, "tileFeaturePresentationBindings", bindings);
            var entry = CreateEntry("stage-a", presentationDefinition: presentation);
            SetPrivateField(
                entry.GameplayDefinition,
                "tileFeatures",
                new[]
                {
                    new StageTileFeatureDefinition
                    {
                        TileId = gameplayTileId,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 1),
                        Kind = TileFeatureKind.Button,
                        ActivationRule = TileFeatureActivationRule.ActiveFaceOnly,
                    },
                });
            return entry;
        }

        private static GameObject CreateTileFeatureVisualPrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<TileFeatureVisualTargetView>();
            return prefab;
        }

        private static StageValidationReport ValidateSingleEntry(StageContentEntry entry)
        {
            return new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    RequirePresentationDefinition = true,
                    RequireAudioDefinition = true,
                    Timing = StageValidationTiming.TestOrCi,
                });
        }

        private static PlayerAtAnyZoneConditionAsset CreatePlayerAtExitCondition()
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            SetPrivateField(condition, "zoneIds", new[] { "exit" });
            SetPrivateField(condition, "requireAlive", true);
            return condition;
        }

        private static void AssignExitObjective(
            StageDefinition stage,
            StageConditionAsset condition,
            string authoringLabel,
            bool duplicateStableId = false)
        {
            SetPrivateField(
                stage,
                "zones",
                new[]
                {
                    new StageZoneDefinition
                    {
                        ZoneId = "exit",
                        FaceId = FaceId.Floor,
                        Regions = new[]
                        {
                            new StageZoneRegionDefinition
                            {
                                MinInclusive = new Vector2Int(1, 1),
                                MaxInclusive = new Vector2Int(1, 1),
                            },
                        },
                    },
                });

            var entries = duplicateStableId
                ? new[]
                {
                    CreateObjectiveConditionEntry(condition, authoringLabel),
                    CreateObjectiveConditionEntry(condition, authoringLabel),
                }
                : new[]
                {
                    CreateObjectiveConditionEntry(condition, authoringLabel),
                };
            SetPrivateField(
                stage,
                "objective",
                new StageObjectiveAuthoring
                {
                    CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                    ConditionEntries = entries,
                });
        }

        private static StageObjectiveConditionEntry CreateObjectiveConditionEntry(
            StageConditionAsset condition,
            string authoringLabel)
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = true,
                Role = StageObjectiveConditionRole.PrimaryGoal,
                StableConditionId = "primary-goal",
                AuthoringLabel = authoringLabel,
                SortOrder = 0,
            };
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static ScriptableObjectStageCatalogProvider CreateCatalogProvider(
            IReadOnlyList<StageContentEntry> entries,
            StageIdAliasTable aliasTable)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            catalog.SetEntries(entries as StageContentEntry[] ?? new List<StageContentEntry>(entries).ToArray());
            catalog.AssignStageIdAliasTable(aliasTable);

            var provider = ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
            provider.AssignCatalog(catalog);
            return provider;
        }

        private sealed class TestStageCatalogProvider : IStageCatalogProvider
        {
            private readonly IReadOnlyList<StageContentEntry> entries;

            public TestStageCatalogProvider(IReadOnlyList<StageContentEntry> entries, StageIdAliasTable aliasTable)
            {
                this.entries = entries;
                AliasTable = aliasTable;
            }

            public IReadOnlyList<StageContentEntry> LoadEntries()
            {
                return entries;
            }

            public StageIdAliasTable AliasTable { get; }
        }

    }
}
