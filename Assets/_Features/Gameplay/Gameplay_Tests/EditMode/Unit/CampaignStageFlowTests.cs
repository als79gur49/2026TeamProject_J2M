using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class CampaignStageFlowTests
    {
        [SetUp]
        public void SetUp()
        {
            StageSaveSlotTestReset.ClearDefaultPlayerPrefs();
        }

        [TearDown]
        public void TearDown()
        {
            StageSaveSlotTestReset.ClearDefaultPlayerPrefs();
        }

        [Test]
        [Category("Extended")]
        public void SequenceResolver_UsesCanonicalOrderDisplayNamesAndLevelGroups()
        {
            var resolver = CreateResolver();

            Assert.That(resolver.FirstStageId.Value, Is.EqualTo("stage-0-1"));
            Assert.That(resolver.FinalStageId.Value, Is.EqualTo("stage-4-2"));
            Assert.That(resolver.IsFinal(StageId.CreateOrThrow("stage-4-2")), Is.True);
            Assert.That(resolver.Contains(StageId.CreateOrThrow("stage-5-1")), Is.False);
            Assert.That(
                CampaignStageSequenceDefinition.IsRetiredCompletedStageId(StageId.CreateOrThrow("stage-5-1")),
                Is.True);
            Assert.That(resolver.GetNextOrNone(StageId.CreateOrThrow("stage-2-1")).Value, Is.EqualTo("stage-2-2"));
            Assert.That(resolver.GetFirstStageInLevelGroupOrNone("level-2").Value, Is.EqualTo("stage-2-1"));
            Assert.That(
                resolver.Entries.Select(entry => entry.StageId.Value).ToArray(),
                Is.EqualTo(CampaignStageSequenceDefinition.CanonicalStageIdValues));
            Assert.That(
                resolver.Entries.Select(entry => entry.DisplayName).ToArray(),
                Is.EqualTo(CampaignStageSequenceDefinition.CanonicalDisplayNames));
        }

        [Test]
        [Category("Extended")]
        public void SequenceValidator_ReportsCatalogMissingStageIds()
        {
            var definition = CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance();
            var report = new CampaignStageSequenceValidator().Validate(
                definition,
                new[] { CreateEntry("stage-0-1") },
                StageValidationTiming.TestOrCi);

            Assert.That(report.Issues.Any(issue => issue.Code == "campaign-sequence.catalog-missing"), Is.True);
            Assert.That(report.HasErrors, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_PersistsThreeSlotsIndependently_WithSaveVersion()
        {
            var key = CreatePrefsKey(nameof(SaveSlotStore_PersistsThreeSlotsIndependently_WithSaveVersion));
            var store = new SaveSlotStore(key);
            store.ClearAll();

            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = 2,
            });
            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 2,
                CurrentStageId = StageId.CreateOrThrow("stage-2-1"),
                CurrentLevelGroupId = "level-2",
                RemainingChances = 1,
                TotalDeaths = 4,
            });

            var reloaded = new SaveSlotStore(key).LoadAll();
            Assert.That(reloaded[0].CurrentStageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(reloaded[0].RemainingChances, Is.EqualTo(2));
            Assert.That(reloaded[1].CurrentStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(reloaded[1].TotalDeaths, Is.EqualTo(4));
            Assert.That(reloaded[2].IsEmpty, Is.True);

            var dto = JsonUtility.FromJson<SaveSlotStoreDto>(PlayerPrefs.GetString(key));
            Assert.That(dto.SchemaId, Is.EqualTo(SaveSlotStore.SchemaId));
            Assert.That(dto.SchemaVersion, Is.EqualTo(SaveSlotStore.SchemaVersion));
            Assert.That(dto.SaveVersion, Is.EqualTo(SaveSlotStore.SaveVersion));
        }

        [Test]
        [Category("Extended")]
        public void StageClearSavePayloadGuard_Empty_ReturnsEmpty()
        {
            var result = StageClearSavePayloadGuard.Inspect(" ");

            Assert.That(result.Status, Is.EqualTo(StageClearSavePayloadStatus.Empty));
            Assert.That(result.ShouldReset, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void StageClearSavePayloadGuard_CurrentSchema_ReturnsCurrent()
        {
            var result = StageClearSavePayloadGuard.Inspect(BuildCurrentSaveJson());

            Assert.That(result.Status, Is.EqualTo(StageClearSavePayloadStatus.Current));
            Assert.That(result.MatchedToken, Is.EqualTo(SaveSlotStore.SchemaId));
        }

        [Test]
        [Category("Extended")]
        public void StageClearSavePayloadGuard_LegacyToken_ReturnsLegacyRejected()
        {
            foreach (var legacyToken in StageClearSavePayloadGuard.LegacyTokens)
            {
                var result = StageClearSavePayloadGuard.Inspect("{\"" + legacyToken + "\":true}");

                Assert.That(result.Status, Is.EqualTo(StageClearSavePayloadStatus.LegacyRejected), legacyToken);
                Assert.That(result.MatchedToken, Is.EqualTo(legacyToken));
                Assert.That(result.ShouldReset, Is.True);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageClearSavePayloadGuard_InvalidJson_ReturnsInvalidRejected()
        {
            var result = StageClearSavePayloadGuard.Inspect(
                "{\"SchemaId\":\"StageClearSaveSlots\",\"SchemaVersion\":2,");

            Assert.That(result.Status, Is.EqualTo(StageClearSavePayloadStatus.InvalidRejected));
            Assert.That(result.ShouldReset, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void StageClearSavePayloadGuard_MissingSchemaMarker_ReturnsInvalidRejected()
        {
            var result = StageClearSavePayloadGuard.Inspect("{\"SaveVersion\":1,\"Slots\":[]}");

            Assert.That(result.Status, Is.EqualTo(StageClearSavePayloadStatus.InvalidRejected));
            Assert.That(result.MatchedToken, Is.EqualTo("SchemaId"));
        }

        [Test]
        [Category("Extended")]
        public void StageClearSavePayloadGuard_WrongSchemaVersion_ReturnsInvalidRejected()
        {
            var result = StageClearSavePayloadGuard.Inspect(
                "{\"SchemaId\":\"StageClearSaveSlots\",\"SchemaVersion\":1,\"SaveVersion\":1,\"Slots\":[]}");

            Assert.That(result.Status, Is.EqualTo(StageClearSavePayloadStatus.InvalidRejected));
            Assert.That(result.MatchedToken, Is.EqualTo("SchemaVersion"));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_CurrentKeyWithLegacyPayload_IsRejectedAndReset()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildLegacyPayloadJson());
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            var slots = store.LoadAll();

            Assert.That(slots.All(slot => slot.IsEmpty), Is.True);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.False);
            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.LegacyRejected));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_CurrentKeyWithLegacyPayload_IsNotExposed()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildLegacyPayloadJson());
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            var slot = store.LoadSlot(1);

            Assert.That(slot.CurrentStageId.IsValid, Is.False);
            Assert.That(slot.StageClearProfileSnapshot.ClearRecordsByStageId, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_CurrentKeyWithLegacyPayload_DoesNotMigrateToClearRecords()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildLegacyPayloadJson());
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            var slot = store.LoadSlot(1);

            Assert.That(slot.StageClearProfileSnapshot.ClearRecordsByStageId.ContainsKey(StageId.CreateOrThrow("stage-0-1")), Is.False);
            Assert.That(slot.StageClearProfileSnapshot.ClearRecordsByStageId, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_CurrentKeyWithLegacyPayload_DeletesCurrentAndActivePrefs()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildLegacyPayloadJson());
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 2);
            PlayerPrefs.Save();

            _ = new SaveSlotStore().LoadAll();

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_InvalidPayload_DoesNotCrashAndResets()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, "{\"SchemaId\":\"StageClearSaveSlots\",\"SchemaVersion\":2,");
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            Assert.DoesNotThrow(() => store.LoadAll());

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.False);
            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.InvalidRejected));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_InvalidPayload_IsNotExposed()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, "{\"SchemaId\":\"StageClearSaveSlots\",\"SchemaVersion\":3,\"Slots\":[]}");
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            var slots = store.LoadAll();

            Assert.That(slots.All(slot => slot.IsEmpty), Is.True);
            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.InvalidRejected));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_EmptyPayload_ReturnsEmptyDatabase()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, string.Empty);
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            var slots = store.LoadAll();

            Assert.That(slots.All(slot => slot.IsEmpty), Is.True);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.Empty));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_CurrentPayload_LoadsNormally()
        {
            var store = new SaveSlotStore();
            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });
            var reloaded = new SaveSlotStore();

            var slot = reloaded.LoadSlot(1);

            Assert.That(slot.CurrentStageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(reloaded.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.Current));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_LoadReport_RecordsLegacyRejection()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildLegacyPayloadJson());
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            _ = store.LoadAll();

            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.LegacyRejected));
            Assert.That(store.LastLoadReport.MatchedToken, Is.EqualTo("ProgressByStageId"));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_LoadReport_RecordsInvalidRejection()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, "{\"SchemaId\":\"Wrong\",\"SchemaVersion\":2}");
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            _ = store.LoadAll();

            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.InvalidRejected));
            Assert.That(store.LastLoadReport.MatchedToken, Is.EqualTo("SchemaId"));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_LoadReport_RecordsCurrentPayload()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildCurrentSaveJson());
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            _ = store.LoadAll();

            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.Current));
            Assert.That(store.LastLoadReport.MatchedToken, Is.EqualTo(SaveSlotStore.SchemaId));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_CustomKeyLegacyPayload_DoesNotResetProductionKeys()
        {
            var customKey = CreatePrefsKey(nameof(SaveSlotStore_CustomKeyLegacyPayload_DoesNotResetProductionKeys));
            var customActiveKey = customKey + ".active";
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildCurrentSaveJson());
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.SetString(customKey, BuildLegacyPayloadJson());
            PlayerPrefs.SetInt(customActiveKey, 2);
            PlayerPrefs.Save();
            var store = new SaveSlotStore(customKey, customActiveKey);

            _ = store.LoadAll();

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(customKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(customActiveKey), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_DirectPlayTempKeyLegacyPayload_DoesNotResetProductionKeys()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildCurrentSaveJson());
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.SetString(EditorDirectPlayContextStore.TempSaveSlotStoreKey, BuildLegacyPayloadJson());
            PlayerPrefs.SetInt(EditorDirectPlayContextStore.TempActiveSlotProviderKey, 1);
            PlayerPrefs.Save();
            var store = new SaveSlotStore(EditorDirectPlayContextStore.TempSaveSlotStoreKey);

            _ = store.LoadAll();

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempActiveSlotProviderKey), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_DirectPlayTempKeyInvalidPayload_ResetsOnlyTempKeys()
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildCurrentSaveJson());
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.SetString(EditorDirectPlayContextStore.TempSaveSlotStoreKey, "{\"SchemaId\":\"StageClearSaveSlots\"}");
            PlayerPrefs.SetInt(EditorDirectPlayContextStore.TempActiveSlotProviderKey, 1);
            PlayerPrefs.Save();
            var store = new SaveSlotStore(EditorDirectPlayContextStore.TempSaveSlotStoreKey);

            _ = store.LoadAll();

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempActiveSlotProviderKey), Is.False);
            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.InvalidRejected));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotStore_LegacyPayload_DoesNotMigrateToClearRecords()
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, BuildLegacyPayloadJson());
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();
            var store = new SaveSlotStore();

            var slot = store.LoadSlot(1);

            Assert.That(slot.IsEmpty, Is.True);
            Assert.That(slot.StageClearProfileSnapshot.ClearRecordsByStageId.ContainsKey(stageId), Is.False);
            Assert.That(slot.StageClearProfileSnapshot.ClearRecordsByStageId, Is.Empty);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.False);
            Assert.That(store.LastLoadReport.Status, Is.EqualTo(StageClearSavePayloadStatus.LegacyRejected));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotProfileDto_RoundTripsStageClearProfileSnapshot()
        {
            var stageId = StageId.CreateOrThrow("stage-3-1");
            var snapshot = new StageClearProfileSnapshot
            {
                Version = 7,
            };
            snapshot.ClearRecordsByStageId[stageId] = new PlayerStageClearRecord
            {
                StageId = stageId,
                HasAttempted = true,
                HasCleared = true,
                ClearCount = 2,
                ProcessedStageRunIds = new[] { "run-a" },
            };
            snapshot.ProcessedStageRunIds.Add("run-a");
            snapshot.ProcessedClearAttemptIds.Add("attempt-a");

            var roundTripped = SaveSlotDtoMapper.FromDto(SaveSlotDtoMapper.ToDto(snapshot));

            Assert.That(roundTripped.Version, Is.EqualTo(7));
            Assert.That(roundTripped.ClearRecordsByStageId[stageId].HasAttempted, Is.True);
            Assert.That(roundTripped.ClearRecordsByStageId[stageId].HasCleared, Is.True);
            Assert.That(roundTripped.ClearRecordsByStageId[stageId].ClearCount, Is.EqualTo(2));
            Assert.That(roundTripped.ClearRecordsByStageId[stageId].ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(roundTripped.ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(roundTripped.ProcessedClearAttemptIds, Does.Contain("attempt-a"));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotProfileDto_WritesOnlyClearProfileVocabulary()
        {
            var stageId = StageId.CreateOrThrow("stage-3-1");
            var slot = SaveSlotData.CreateEmpty(1);
            slot.StageClearProfileSnapshot.Version = 2;
            slot.StageClearProfileSnapshot.ClearRecordsByStageId[stageId] = new PlayerStageClearRecord
            {
                StageId = stageId,
                HasAttempted = true,
                HasCleared = true,
                ClearCount = 3,
            };
            slot.StageClearProfileSnapshot.ProcessedClearAttemptIds.Add("attempt-clear");

            var json = JsonUtility.ToJson(SaveSlotDtoMapper.ToDto(new[] { slot }));

            Assert.That(json, Does.Contain("StageClearProfileSnapshot"));
            Assert.That(json, Does.Contain("ClearRecordsByStageId"));
            Assert.That(json, Does.Contain("HasAttempted"));
            Assert.That(json, Does.Contain("ProcessedClearAttemptIds"));
            Assert.That(json, Does.Not.Contain("Stage" + "Completion" + "Profile" + "Snapshot"));
            Assert.That(json, Does.Not.Contain("Progress" + "By" + "StageId"));
            Assert.That(json, Does.Not.Contain("Has" + "Started"));
            Assert.That(json, Does.Not.Contain("Processed" + "Completion" + "AttemptIds"));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotProfileDto_DoesNotWriteLegacyCompletionProgressRewardFields()
        {
            var slot = SaveSlotData.CreateEmpty(1);
            var json = JsonUtility.ToJson(SaveSlotDtoMapper.ToDto(new[] { slot }));
            var legacyFields = new[]
            {
                "Stage" + "Completion" + "Profile" + "Snapshot",
                "Progress" + "By" + "StageId",
                "Processed" + "Completion" + "AttemptIds",
                "Consumed" + "Reward" + "RuleIds",
                "Applied" + "Reward" + "GrantIds",
                "Inventory" + "Balances",
                "Best" + "Score",
                "Best" + "Stars",
                "Best" + "RankId",
                "Completed" + "ChallengeIds",
            };

            foreach (var legacyField in legacyFields)
            {
                Assert.That(json, Does.Not.Contain(legacyField));
            }
        }

        [Test]
        [Category("Extended")]
        public void ActiveSlotStageClearProfileStore_UpdatesOnlyActiveSlot()
        {
            var saveKey = CreatePrefsKey(nameof(ActiveSlotStageClearProfileStore_UpdatesOnlyActiveSlot));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData { SlotNumber = 1, CurrentStageId = StageId.CreateOrThrow("stage-1-1") });
            saveStore.SaveSlot(new SaveSlotData { SlotNumber = 2, CurrentStageId = StageId.CreateOrThrow("stage-2-1") });
            activeSlotProvider.SetActiveSlot(2);

            var profileStore = new SaveSlotStageClearProfileStore(saveStore, activeSlotProvider);
            var snapshot = new StageClearProfileSnapshot();
            snapshot.ClearRecordsByStageId[StageId.CreateOrThrow("stage-2-1")] =
                new PlayerStageClearRecord
                {
                    StageId = StageId.CreateOrThrow("stage-2-1"),
                    HasAttempted = true,
                    HasCleared = true,
                    ClearCount = 1,
                };

            profileStore.Save(snapshot);

            Assert.That(saveStore.LoadSlot(1).StageClearProfileSnapshot.ClearRecordsByStageId, Is.Empty);
            var activeRecord = saveStore.LoadSlot(2)
                .StageClearProfileSnapshot
                .ClearRecordsByStageId[StageId.CreateOrThrow("stage-2-1")];
            Assert.That(activeRecord.HasCleared, Is.True);
            Assert.That(activeRecord.ClearCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Architecture_SaveProfileProductionSymbolsUseClearVocabulary()
        {
            var source = ReadSaveProfileProductionSources();
            var forbiddenSymbols = new[]
            {
                "Player" + "Stage" + "Progress",
                "Stage" + "Completion" + "Profile" + "Snapshot",
                "IStage" + "Completion" + "Profile" + "Store",
                "SaveSlot" + "Stage" + "Completion" + "Profile" + "Store",
            };

            foreach (var forbiddenSymbol in forbiddenSymbols)
            {
                Assert.That(source, Does.Not.Contain(forbiddenSymbol));
            }

            Assert.That(source, Does.Contain("PlayerStageClearRecord"));
            Assert.That(source, Does.Contain("StageClearProfileSnapshot"));
            Assert.That(source, Does.Contain("IStageClearProfileStore"));
            Assert.That(source, Does.Contain("SaveSlotStageClearProfileStore"));
        }

        [Test]
        [Category("Extended")]
        public void Architecture_SaveProfileProductionDtosUseClearFieldsOnly()
        {
            var source = ReadSaveProfileProductionSources();
            var forbiddenFields = new[]
            {
                "Progress" + "By" + "StageId",
                "Has" + "Started",
                "Processed" + "Completion" + "AttemptIds",
                "Consumed" + "Reward" + "RuleIds",
                "Applied" + "Reward" + "GrantIds",
                "Inventory" + "Balances",
                "Best" + "Score",
                "Best" + "Stars",
                "Best" + "RankId",
                "Completed" + "ChallengeIds",
            };

            foreach (var forbiddenField in forbiddenFields)
            {
                Assert.That(source, Does.Not.Contain(forbiddenField));
            }

            Assert.That(source, Does.Contain("ClearRecordsByStageId"));
            Assert.That(source, Does.Contain("HasAttempted"));
            Assert.That(source, Does.Contain("ProcessedClearAttemptIds"));
        }

        [Test]
        [Category("Full")]
        public void RetryChanceTracker_RoutesChancesThreeTwoOne()
        {
            var resolver = CreateResolver();
            var tracker = new StageRetryChanceTracker(resolver);
            var slot = new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                CurrentLevelGroupId = "level-2",
            };

            slot.RemainingChances = 3;
            var first = tracker.ResolveDeathRoute(slot);
            Assert.That(first.RouteKind, Is.EqualTo(StageRetryRouteKind.RetrySameStage));
            Assert.That(first.NextStageId.Value, Is.EqualTo("stage-2-2"));
            Assert.That(first.RemainingChances, Is.EqualTo(2));

            slot.RemainingChances = 2;
            var second = tracker.ResolveDeathRoute(slot);
            Assert.That(second.RouteKind, Is.EqualTo(StageRetryRouteKind.RetrySameStage));
            Assert.That(second.RemainingChances, Is.EqualTo(1));

            slot.RemainingChances = 1;
            var last = tracker.ResolveDeathRoute(slot);
            Assert.That(last.RouteKind, Is.EqualTo(StageRetryRouteKind.ReturnToLevelGroupFirstStage));
            Assert.That(last.NextStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(last.RemainingChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
        }

        [Test]
        [Category("Extended")]
        public void StageResultNavigationStore_StoresNextStageAndFinalCompletionPlans()
        {
            CampaignStageResultNavigationStore.Clear();
            var completedStageId = StageId.CreateOrThrow("stage-1-1");
            var nextStageId = StageId.CreateOrThrow("stage-2-1");

            CampaignStageResultNavigationStore.Set(
                new CampaignStageResultNavigationPlan(
                    completedStageId,
                    new StageNavigationRequest(nextStageId, StageNavigationKind.NextStage, "test"),
                    campaignCompleted: false));

            Assert.That(CampaignStageResultNavigationStore.TryGet(completedStageId, out var nextPlan), Is.True);
            Assert.That(nextPlan.NextStageRequest.StageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(nextPlan.CampaignCompleted, Is.False);

            CampaignStageResultNavigationStore.Set(
                new CampaignStageResultNavigationPlan(
                    StageId.CreateOrThrow("stage-4-2"),
                    StageNavigationRequest.None,
                    campaignCompleted: true));
            Assert.That(CampaignStageResultNavigationStore.TryGet(StageId.CreateOrThrow("stage-4-2"), out var finalPlan), Is.True);
            Assert.That(finalPlan.CampaignCompleted, Is.True);
            Assert.That(finalPlan.NextStageRequest.IsValid, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void StageClear_AdvancesAcrossLevelGroupsWithoutResettingRemainingChances()
        {
            var saveKey = CreatePrefsKey(nameof(StageClear_AdvancesAcrossLevelGroupsWithoutResettingRemainingChances));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-clear-host");
            var inputHostObject = new GameObject("campaign-clear-input");

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                    CurrentLevelGroupId = "level-1",
                    RemainingChances = 1,
                });
                activeSlotProvider.SetActiveSlot(1);

                var host = hostObject.AddComponent<GameplaySceneHost>();
                var inputHost = inputHostObject.AddComponent<GameplayInputHost>();
                SetPrivateField(inputHost, "_isInitialized", true);
                SetPrivateField(
                    host,
                    "_runtime",
                    new GameplayHostRuntimeContext(
                        null,
                        null,
                        null,
                        inputHost,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null));

                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    new FakeStageLaunchRouter());
                var method = typeof(CampaignGameplayFlowController).GetMethod(
                    "HandleStageClear",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);

                method.Invoke(controller, new object[] { null });

                var slot = saveStore.LoadSlot(1);
                Assert.That(slot.CurrentStageId.Value, Is.EqualTo("stage-2-1"));
                Assert.That(slot.CurrentLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(slot.RemainingChances, Is.EqualTo(1));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(inputHostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TerminalHold_BlocksGameplayTickWithoutSimulationPause()
        {
            var hostObject = new GameObject("input-host");
            var inputHost = hostObject.AddComponent<GameplayInputHost>();

            try
            {
                SetPrivateField(inputHost, "_isInitialized", true);
                SetPrivateField(inputHost, "_isTerminalHoldActive", true);
                SetPrivateField(inputHost, "_isSimulationPaused", false);

                Assert.That(inputHost.AdvanceTime(1f), Is.EqualTo(0));
                Assert.That(inputHost.RunSingleTick(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void RespawnProcessor_PlayerRespawnGateSuppressesSpawnBeforeWrite()
        {
            var source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.RespawnProcessor.cs"));
            var gateIndex = source.IndexOf("if (!allowRespawn)", StringComparison.Ordinal);
            var spawnIndex = source.IndexOf("writeContext.SpawnEntity(respawnEntity)", StringComparison.Ordinal);

            Assert.That(gateIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(source, Does.Contain("RespawnSuppressed|E="));
            Assert.That(gateIndex, Is.LessThan(spawnIndex));
        }

        [Test]
        [Category("Extended")]
        public void StageAudioRuntimeRequestSource_SubmitsStageGameplayThroughRouter()
        {
            var coordinator = new FakeBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var source = new StageAudioRuntimeRequestSource();
            var profile = ScriptableObject.CreateInstance<BgmProfile>();
            var audioData = new StageAudioResolvedData(
                new StageBgmResolvedSlot(StageBgmSlotMode.Profile, profile));

            source.Apply(audioData, router);

            Assert.That(coordinator.RequestCount, Is.EqualTo(1));
            Assert.That(coordinator.LastProfile, Is.SameAs(profile));
            Assert.That(router.ActiveRequest.HasValue, Is.True);
            Assert.That(router.ActiveRequest.Value.SourceKind, Is.EqualTo(BgmRequestSourceKind.StageGameplay));
            Assert.That(router.ActiveRequest.Value.Priority, Is.EqualTo(BgmRequestPriority.StageGameplay));
        }

        [Test]
        [Category("Extended")]
        public void StageAudioRuntimeRequestSource_NoneSubmitsStageGameplaySilence()
        {
            var coordinator = new FakeBgmFlowCoordinator();
            var router = new BgmRequestRouter(coordinator);
            var source = new StageAudioRuntimeRequestSource();

            source.Apply(StageAudioAssembler.EmptyResolvedData, router);

            Assert.That(coordinator.StopCount, Is.EqualTo(1));
            Assert.That(router.ActiveRequest.HasValue, Is.True);
            Assert.That(router.ActiveRequest.Value.SourceKind, Is.EqualTo(BgmRequestSourceKind.StageGameplay));
            Assert.That(router.ActiveRequest.Value.StopBgm, Is.True);
            Assert.That(router.ActiveRequest.Value.Priority, Is.EqualTo(BgmRequestPriority.StageGameplay));
        }

        [Test]
        [Category("Full")]
        public void StageVisualRuntimeAdapter_InstantiatesAndReplacesBackgroundPrefab()
        {
            var root = new GameObject("background-root");
            var firstPrefab = new GameObject("first-background");
            var secondPrefab = new GameObject("second-background");
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var adapter = new StageVisualRuntimeAdapter();

            try
            {
                SetPrivateField(presentation, "backgroundPrefab", firstPrefab);
                adapter.Apply(presentation, root.transform);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(adapter.CurrentBackgroundInstance.name, Is.EqualTo("first-background"));

                SetPrivateField(presentation, "backgroundPrefab", secondPrefab);
                adapter.Apply(presentation, root.transform);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(adapter.CurrentBackgroundInstance.name, Is.EqualTo("second-background"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(firstPrefab);
                UnityEngine.Object.DestroyImmediate(secondPrefab);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        [Category("Full")]
        public void StageVisualRuntimeAdapter_BackgroundPrefabBridgeAutoResolvesSingleSceneHost()
        {
            var hostObject = new GameObject("background-bridge-host");
            var root = new GameObject("background-root");
            var prefab = new GameObject("background-with-bridge");
            var bridgeTarget = new GameObject("bridge-target");
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var adapter = new StageVisualRuntimeAdapter();

            try
            {
                hostObject.AddComponent<GameplaySceneHost>();
                bridgeTarget.transform.SetParent(prefab.transform, worldPositionStays: false);
                var controller = prefab.AddComponent<TopologyVisualBridgeVisibilityController>();
                SetPrivateField(
                    controller,
                    "bindings",
                    new[]
                    {
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = bridgeTarget,
                            FirstFace = FaceId.Floor,
                            SecondFace = FaceId.Front,
                        },
                    });
                SetPrivateField(presentation, "backgroundPrefab", prefab);

                adapter.Apply(presentation, root.transform);

                var instantiatedController =
                    adapter.CurrentBackgroundInstance.GetComponent<TopologyVisualBridgeVisibilityController>();
                Assert.That(instantiatedController, Is.Not.Null);
                Assert.That(instantiatedController.SceneHost, Is.SameAs(hostObject.GetComponent<GameplaySceneHost>()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageBackedInstaller_ResolveStageBackgroundRoot_CreatesFallbackUnderInstaller()
        {
            var installerObject = new GameObject("installer-background-root");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();

                var resolvedRoot = (Transform)InvokeInstanceMethod(installer, "ResolveStageBackgroundRoot");

                Assert.That(resolvedRoot, Is.Not.Null);
                Assert.That(resolvedRoot.name, Is.EqualTo("StageBackgroundRoot"));
                Assert.That(resolvedRoot.parent, Is.EqualTo(installerObject.transform));
                Assert.That(ReadPrivateField<Transform>(installer, "stageBackgroundRoot"), Is.SameAs(resolvedRoot));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageBackedInstaller_ResolveStageBackgroundRoot_ReusesAuthoredChild()
        {
            var installerObject = new GameObject("installer-background-root");
            var authoredRoot = new GameObject("StageBackgroundRoot");
            authoredRoot.transform.SetParent(installerObject.transform, worldPositionStays: false);

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();

                var resolvedRoot = (Transform)InvokeInstanceMethod(installer, "ResolveStageBackgroundRoot");

                Assert.That(resolvedRoot, Is.SameAs(authoredRoot.transform));
                Assert.That(ReadPrivateField<Transform>(installer, "stageBackgroundRoot"), Is.SameAs(authoredRoot.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayInstaller_NoActiveSlot_DoesNotBindCampaignController_AndDoesNotDisableRespawn()
        {
            var activeKey = CreatePrefsKey(nameof(GameplayInstaller_NoActiveSlot_DoesNotBindCampaignController_AndDoesNotDisableRespawn));
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            try
            {
                activeSlotProvider.ClearActiveSlot();

                var activation = CampaignRuntimeActivationPolicy.Evaluate(
                    enableCampaignFlow: true,
                    activeSlotProvider,
                    EditorDirectPlayContext.None);

                Assert.That(activation.IsActive, Is.False);
                Assert.That(activation.HasActiveSlot, Is.False);
            }
            finally
            {
                activeSlotProvider.ClearActiveSlot();
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayInstaller_ActiveSlot_BindsCampaignController_AndDisablesRespawn()
        {
            var activeKey = CreatePrefsKey(nameof(GameplayInstaller_ActiveSlot_BindsCampaignController_AndDisablesRespawn));
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            try
            {
                activeSlotProvider.SetActiveSlot(1);

                var activation = CampaignRuntimeActivationPolicy.Evaluate(
                    enableCampaignFlow: true,
                    activeSlotProvider,
                    EditorDirectPlayContext.None);

                Assert.That(activation.IsActive, Is.True);
                Assert.That(activation.HasActiveSlot, Is.True);
            }
            finally
            {
                activeSlotProvider.ClearActiveSlot();
            }
        }

        [Test]
        [Category("Extended")]
        public void NonCampaignDirectPlay_SuppressesCampaign_EvenWithStaleActiveSlot()
        {
            var activeKey = CreatePrefsKey(nameof(NonCampaignDirectPlay_SuppressesCampaign_EvenWithStaleActiveSlot));
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            try
            {
                activeSlotProvider.SetActiveSlot(1);
                var context = EditorDirectPlayContext.CreateNonCampaign(StageId.CreateOrThrow("stage-0-1"));

                var activation = CampaignRuntimeActivationPolicy.Evaluate(
                    enableCampaignFlow: true,
                    activeSlotProvider,
                    context);

                Assert.That(activation.HasActiveSlot, Is.True);
                Assert.That(activation.IsSuppressedByEditorDirectPlay, Is.True);
                Assert.That(activation.IsActive, Is.False);
            }
            finally
            {
                activeSlotProvider.ClearActiveSlot();
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDirectPlay_TempSlotMode_EnablesCampaignRuntime()
        {
            var activeSlotProvider = new ActiveSlotProvider(EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            try
            {
                activeSlotProvider.ClearActiveSlot();
                activeSlotProvider.SetActiveSlot(1);
                var context = EditorDirectPlayContext.CreateCampaignTempSlot(
                    StageId.CreateOrThrow("stage-2-1"),
                    remainingChances: 2);

                var activation = CampaignRuntimeActivationPolicy.Evaluate(
                    enableCampaignFlow: true,
                    activeSlotProvider,
                    context);

                Assert.That(activation.IsActive, Is.True);
                Assert.That(context.SaveSlotStoreKey, Is.EqualTo(EditorDirectPlayContextStore.TempSaveSlotStoreKey));
                Assert.That(context.ActiveSlotProviderKey, Is.EqualTo(EditorDirectPlayContextStore.TempActiveSlotProviderKey));
            }
            finally
            {
                activeSlotProvider.ClearActiveSlot();
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDirectPlay_RemainingChances1_DeathPublishesLevelFailedRoute()
        {
            var tracker = new StageRetryChanceTracker(CreateResolver());
            var route = tracker.ResolveDeathRoute(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                CurrentLevelGroupId = "level-2",
                RemainingChances = 1,
            });

            Assert.That(route.RouteKind, Is.EqualTo(StageRetryRouteKind.ReturnToLevelGroupFirstStage));
            Assert.That(route.NextStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(route.RemainingChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_RetryNavigationLaunchesBeforeDeathRecoveryHold()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_RetryNavigationLaunchesBeforeDeathRecoveryHold));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-2",
                    RemainingChances = 2,
                });
                activeSlotProvider.SetActiveSlot(1);

                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
                Assert.That(router.LastRequest.StageId.Value, Is.EqualTo("stage-2-2"));
                Assert.That(router.LastRequest.TransitionHint.Kind, Is.EqualTo(StageTransitionKind.DeathRetryChanceLost));
                Assert.That(router.LastRequest.TransitionHint.HasChanceLostPayload, Is.True);
                Assert.That(router.LastRequest.TransitionHint.ChanceLostPayload.PreviousRemainingChances, Is.EqualTo(2));
                Assert.That(router.LastRequest.TransitionHint.ChanceLostPayload.CurrentRemainingChances, Is.EqualTo(1));
                Assert.That(router.LastRequest.TransitionHint.ChanceLostPayload.TotalChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(saveStore.LoadSlot(1).RemainingChances, Is.EqualTo(1));

                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(51) });
                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_RetryTerminalHold_NotifiesPresentationTerminalExtensions()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_RetryTerminalHold_NotifiesPresentationTerminalExtensions));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-terminal-vfx-host");

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-2",
                    RemainingChances = 2,
                });
                activeSlotProvider.SetActiveSlot(1);

                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var terminalExtension = new RecordingTerminalPresentationExtension();
                presenter.AttachPresentationExtension(terminalExtension);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3, presenter: presenter);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    new FakeStageLaunchRouter());

                GetHandleTickCompletedMethod().Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });

                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(terminalExtension.ApplyCount, Is.EqualTo(1));
                Assert.That(terminalExtension.LastReason, Is.EqualTo(GameplayStageTerminalPresentationReason.PlayerDeathRetry));
                Assert.That(terminalExtension.LastTickIndex, Is.EqualTo(50));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_LevelFailedWaitsForDeathRecoveryHold()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_LevelFailedWaitsForDeathRecoveryHold));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-level-failed-host");

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-2",
                    RemainingChances = 1,
                });
                activeSlotProvider.SetActiveSlot(1);

                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var presentationFeed = new GameplayHostPresentationFeed(host.InputHost, presenter);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    new FakeStageLaunchRouter());
                SetPrivateField(controller, "_presentationFeed", presentationFeed);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(52) });

                Assert.That(presentationFeed.CurrentLevelFailed, Is.Null);
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.False);

                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });
                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(54) });

                Assert.That(presentationFeed.CurrentLevelFailed, Is.Not.Null);
                Assert.That(presentationFeed.CurrentLevelFailed.RestartLevelRequest.StageId.Value, Is.EqualTo("stage-2-1"));
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                presentationFeed.Dispose();
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_LevelFailedTerminalHold_NotifiesPresentationTerminalExtensions()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_LevelFailedTerminalHold_NotifiesPresentationTerminalExtensions));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-level-failed-terminal-vfx-host");

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-2",
                    RemainingChances = 1,
                });
                activeSlotProvider.SetActiveSlot(1);

                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var terminalExtension = new RecordingTerminalPresentationExtension();
                presenter.AttachPresentationExtension(terminalExtension);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3, presenter: presenter);
                var presentationFeed = new GameplayHostPresentationFeed(host.InputHost, presenter);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    new FakeStageLaunchRouter());
                SetPrivateField(controller, "_presentationFeed", presentationFeed);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(presentationFeed.CurrentLevelFailed, Is.Not.Null);
                Assert.That(terminalExtension.ApplyCount, Is.EqualTo(1));
                Assert.That(terminalExtension.LastReason, Is.EqualTo(GameplayStageTerminalPresentationReason.LevelFailed));
                Assert.That(terminalExtension.LastTickIndex, Is.EqualTo(53));
                presentationFeed.Dispose();
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_LevelFailedDisplaysZeroChancesWhileSaveSlotIsRecovered()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_LevelFailedDisplaysZeroChancesWhileSaveSlotIsRecovered));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-level-failed-zero-chances-host");
            var chanceDisplayOverride = new CampaignChanceDisplayOverride();
            var chancesReadSource = new SaveSlotCampaignChancesReadSource(
                saveStore,
                activeSlotProvider,
                chanceDisplayOverride);

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 1);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });

                Assert.That(saveStore.LoadSlot(1).RemainingChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
                Assert.That(
                    chancesReadSource.TryReadChances(
                        out var remainingChances,
                        out var maxChances,
                        out var audioPolicy),
                    Is.True);
                Assert.That(remainingChances, Is.EqualTo(0));
                Assert.That(maxChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
                Assert.That(audioPolicy, Is.EqualTo(GameplayChanceAudioPolicy.SuppressChanceChangeCue));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_RetryLaunch_IgnoresLaterStageClear()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_RetryLaunch_IgnoresLaterStageClear));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-pending-retry-clear-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();
                var handleStageClearCommitted = GetHandleStageClearCommittedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                handleStageClearCommitted.Invoke(
                    controller,
                    new object[] { CreateEmptyTickResult(51), CreateMinimalStageCompletionReadModel("stage-2-2", tickIndex: 51) });

                var pendingSlot = saveStore.LoadSlot(1);
                Assert.That(pendingSlot.CurrentStageId.Value, Is.EqualTo("stage-2-2"));
                Assert.That(pendingSlot.CurrentLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(pendingSlot.RemainingChances, Is.EqualTo(1));
                Assert.That(pendingSlot.TotalDeaths, Is.EqualTo(1));
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.StageId.Value, Is.EqualTo("stage-2-2"));
                Assert.That(router.LastRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));

                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_PendingLevelFailed_IgnoresLaterStageClear()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_PendingLevelFailed_IgnoresLaterStageClear));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-pending-level-failed-clear-host");

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 1);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var presentationFeed = new GameplayHostPresentationFeed(host.InputHost, presenter);
                var router = new FakeStageLaunchRouter();
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                SetPrivateField(controller, "_presentationFeed", presentationFeed);
                var handleTickCompleted = GetHandleTickCompletedMethod();
                var handleStageClearCommitted = GetHandleStageClearCommittedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                handleStageClearCommitted.Invoke(
                    controller,
                    new object[] { CreateEmptyTickResult(51), CreateMinimalStageCompletionReadModel("stage-2-2", tickIndex: 51) });

                var pendingSlot = saveStore.LoadSlot(1);
                Assert.That(pendingSlot.CurrentStageId.Value, Is.EqualTo("stage-2-1"));
                Assert.That(pendingSlot.CurrentLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(pendingSlot.RemainingChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
                Assert.That(pendingSlot.TotalDeaths, Is.EqualTo(1));
                Assert.That(presentationFeed.CurrentLevelFailed, Is.Null);
                Assert.That(router.LaunchCount, Is.EqualTo(0));

                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(presentationFeed.CurrentLevelFailed, Is.Not.Null);
                Assert.That(presentationFeed.CurrentLevelFailed.RestartLevelRequest.StageId.Value, Is.EqualTo("stage-2-1"));
                Assert.That(router.LaunchCount, Is.EqualTo(0));
                presentationFeed.Dispose();
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeathAndClearSameTick_DeathWins_StageClearIgnored()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeathAndClearSameTick_DeathWins_StageClearIgnored));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-clear-same-tick-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();
                var handleStageClearCommitted = GetHandleStageClearCommittedMethod();
                var deathAndClearTick = CreateDeathTickResult(50, eligibleTick: 53);

                handleStageClearCommitted.Invoke(
                    controller,
                    new object[] { deathAndClearTick, CreateMinimalStageCompletionReadModel("stage-2-2", tickIndex: 50) });
                handleTickCompleted.Invoke(controller, new object[] { deathAndClearTick });

                var slot = saveStore.LoadSlot(1);
                Assert.That(slot.CurrentStageId.Value, Is.EqualTo("stage-2-2"));
                Assert.That(slot.RemainingChances, Is.EqualTo(1));
                Assert.That(slot.TotalDeaths, Is.EqualTo(1));

                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.StageId.Value, Is.EqualTo("stage-2-2"));

                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_DuplicateSameTickSignal_DoesNotDuplicateRouteOrSave()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_DuplicateSameTickSignal_DoesNotDuplicateRouteOrSave));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-duplicate-same-tick-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();
                var deathTick = CreateDeathTickResult(50, eligibleTick: 53);

                handleTickCompleted.Invoke(controller, new object[] { deathTick });
                handleTickCompleted.Invoke(controller, new object[] { deathTick });

                var slot = saveStore.LoadSlot(1);
                Assert.That(slot.RemainingChances, Is.EqualTo(1));
                Assert.That(slot.TotalDeaths, Is.EqualTo(1));
                Assert.That(router.LaunchCount, Is.EqualTo(1));

                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_DuplicateConsecutiveSignal_DoesNotDuplicateRouteOrSave()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_DuplicateConsecutiveSignal_DoesNotDuplicateRouteOrSave));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-duplicate-consecutive-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(51, eligibleTick: 54) });

                var slot = saveStore.LoadSlot(1);
                Assert.That(slot.RemainingChances, Is.EqualTo(1));
                Assert.That(slot.TotalDeaths, Is.EqualTo(1));
                Assert.That(router.LaunchCount, Is.EqualTo(1));

                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.StageId.Value, Is.EqualTo("stage-2-2"));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_DuplicateElapsedTick_DoesNotDuplicateLaunch()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_DuplicateElapsedTick_DoesNotDuplicateLaunch));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-duplicate-elapsed-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();
                var elapsedTick = CreateElapsedSuppressedTickResult(53);

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                handleTickCompleted.Invoke(controller, new object[] { elapsedTick });
                handleTickCompleted.Invoke(controller, new object[] { elapsedTick });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.StageId.Value, Is.EqualTo("stage-2-2"));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeathHold_PlayerInput_IsClearedAndDoesNotCreateGameplayCommand()
        {
            var hostObject = new GameObject("campaign-death-input-block-host");

            try
            {
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var inputHost = host.InputHost;
                SetPrivateField(inputHost, "_isPlayerRespawnDelayInputBlocked", true);

                inputHost.SetRawMoveInput(Vector2.right);
                inputHost.BufferPush();
                inputHost.BufferFlip();

                var command = (PlayerTickCommand)InvokeInstanceMethod(inputHost, "BuildPlayerCommand");

                Assert.That(ReadPrivateField<Vector2>(inputHost, "_sampledMoveInput"), Is.EqualTo(Vector2.zero));
                Assert.That(ReadPrivateField<bool>(inputHost, "_hasBufferedPush"), Is.False);
                Assert.That(ReadPrivateField<bool>(inputHost, "_hasBufferedFlip"), Is.False);
                Assert.That(command.MoveDirection, Is.EqualTo(Direction.None));
                Assert.That(command.PushPressed, Is.False);
                Assert.That(command.FlipPressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeathRetry_PauseResume_DoesNotDuplicateRoute()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeathRetry_PauseResume_DoesNotDuplicateRoute));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-pause-resume-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var pauseService = new GameplayHostPauseService(host.InputHost);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                pauseService.Pause();
                pauseService.Resume();

                var pendingSlot = saveStore.LoadSlot(1);
                Assert.That(pendingSlot.CurrentStageId.Value, Is.EqualTo("stage-2-2"));
                Assert.That(pendingSlot.RemainingChances, Is.EqualTo(1));
                Assert.That(router.LaunchCount, Is.EqualTo(1));

                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.StageId.Value, Is.EqualTo("stage-2-2"));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeathElapsed_EnterTerminalHold_BlocksFurtherTicks()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeathElapsed_EnterTerminalHold_BlocksFurtherTicks));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-terminal-hold-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                handleTickCompleted.Invoke(controller, new object[] { CreateEmptyTickResult(53) });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(host.InputHost.RunSingleTick(), Is.Null);
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeathElapsed_LaunchOccursAfterSuppressedElapsedResult()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeathElapsed_LaunchOccursAfterSuppressedElapsedResult));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-suppressed-elapsed-host");
            var router = new FakeStageLaunchRouter();

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    activeSlotProvider,
                    CreateResolver(),
                    router);
                var handleTickCompleted = GetHandleTickCompletedMethod();
                var elapsedTick = CreateElapsedSuppressedTickResult(53);
                var elapsedEventLog = elapsedTick.EventLog.ToList();

                Assert.That(
                    elapsedEventLog.IndexOf("PlayerRespawnDelayElapsed|E=10|StartTick=50|EligibleTick=53|Tick=53"),
                    Is.LessThan(elapsedEventLog.IndexOf("RespawnSuppressed|E=10|Reason=PolicyDisabled|Tick=53")));

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                handleTickCompleted.Invoke(controller, new object[] { elapsedTick });

                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.StageId.Value, Is.EqualTo("stage-2-2"));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeBootstrap_FailsIfCampaignActiveSlotStageAndLaunchStageMismatch()
        {
            var saveKey = CreatePrefsKey(nameof(RuntimeBootstrap_FailsIfCampaignActiveSlotStageAndLaunchStageMismatch));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var installerObject = new GameObject("installer");
            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                });
                activeSlotProvider.SetActiveSlot(1);
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                SetPrivateField(installer, "_saveSlotStore", saveStore);
                SetPrivateField(installer, "_activeSlotProvider", activeSlotProvider);

                var method = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                    "ValidateActiveSlotMatchesLaunchStage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(
                    installer,
                    new object[] { StageId.CreateOrThrow("stage-2-1") }));

                Assert.That(exception?.InnerException, Is.TypeOf<InvalidOperationException>());
                StringAssert.Contains("does not match launch stage", exception?.InnerException?.Message);
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DeathRoute_UsesSyncedLevelGroup_NotStaleSavedGroup()
        {
            var tracker = new StageRetryChanceTracker(CreateResolver());

            var route = tracker.ResolveDeathRoute(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                CurrentLevelGroupId = "level-5",
                RemainingChances = 1,
            });

            Assert.That(route.NextStageId.Value, Is.EqualTo("stage-2-1"));
        }

        [Test]
        [Category("Extended")]
        public void StageBackedInstaller_UsesSameRootStageLaunchRouterProviderBeforeFallback()
        {
            var root = new GameObject("stage-launch-router-provider-root");
            try
            {
                var provider = root.AddComponent<FakeStageLaunchRouterProvider>();
                var method = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                    "CreateStageLaunchRouter",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);

                var router = (IStageLaunchRouter)method.Invoke(null, new object[] { root, "UIAudioScene" });

                Assert.That(router, Is.SameAs(provider.Router));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayHost_DoesNotReferenceUiComposition()
        {
            var asmdefPath = Path.Combine(
                Application.dataPath,
                "_Features/Gameplay/Gameplay_Host/Gameplay.Host.asmdef");
            var asmdef = File.ReadAllText(asmdefPath);
            Assert.That(asmdef, Does.Not.Contain("Game.Feature.UI.Composition"));

            foreach (var path in Directory.GetFiles(
                         Path.Combine(Application.dataPath, "_Features/Gameplay/Gameplay_Host/Runtime"),
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("Game.Feature.UI.Composition"), path);
                Assert.That(source, Does.Not.Contain("SceneTransitionCoordinator"), path);
                Assert.That(source, Does.Not.Contain("CurrentSceneStageLaunchRouter"), path);
            }
        }

        private static CampaignStageSequenceResolver CreateResolver()
        {
            return new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
        }

        private static StageContentEntry CreateEntry(string stageId)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            SetPrivateField(entry, "stageId", StageId.CreateOrThrow(stageId));
            return entry;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var targetType = target.GetType();
            FieldInfo field = null;
            for (var type = targetType; type != null && field == null; type = type.BaseType)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            var targetType = target.GetType();
            FieldInfo field = null;
            for (var type = targetType; type != null && field == null; type = type.BaseType)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
        {
            var targetType = target.GetType();
            var argumentTypes = args.Select(arg => arg?.GetType() ?? typeof(object)).ToArray();
            MethodInfo method = null;
            for (var type = targetType; type != null && method == null; type = type.BaseType)
            {
                method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    argumentTypes,
                    null);
                method ??= type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName}");
            return method.Invoke(target, args);
        }

        private static void SeedSaveSlot(
            SaveSlotStore saveStore,
            ActiveSlotProvider activeSlotProvider,
            string stageId,
            string levelGroupId,
            int remainingChances)
        {
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = remainingChances,
            });
            activeSlotProvider.SetActiveSlot(1);
        }

        private static GameplaySceneHost CreateHostWithInput(
            GameObject hostObject,
            int playerEntityId,
            int respawnDelayTicks,
            GameplayTickViewPresenter presenter = null)
        {
            var host = hostObject.AddComponent<GameplaySceneHost>();
            var inputHost = hostObject.AddComponent<GameplayInputHost>();
            SetPrivateField(inputHost, "_isInitialized", true);
            SetPrivateField(inputHost, "_playerEntityId", playerEntityId);
            SetPrivateField(
                host,
                "_runtime",
                new GameplayHostRuntimeContext(
                    null,
                    null,
                    null,
                    inputHost,
                    presenter,
                    GameplayTimingProfile.CreateDefault(),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    respawnDelayTicks));
            return host;
        }

        private static MethodInfo GetHandleTickCompletedMethod()
        {
            var method = typeof(CampaignGameplayFlowController).GetMethod(
                "HandleTickCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static MethodInfo GetHandleStageClearCommittedMethod()
        {
            var method = typeof(CampaignGameplayFlowController).GetMethod(
                "HandleStageClearCommitted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static bool ReadInputHostTerminalHold(GameplayInputHost inputHost)
        {
            var field = typeof(GameplayInputHost).GetField(
                "_isTerminalHoldActive",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(inputHost);
        }

        private static TickResult CreateDeathTickResult(int tickIndex, int eligibleTick)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                new[]
                {
                    new TickPlayerDeathPresentationSignal(
                        10,
                        didDieThisTick: true,
                        sourceEntityId: 0,
                        fallbackFacing: Direction.Right,
                        resolvedDamageSourceAvailable: false,
                        damageAmountAtFatalHit: 1,
                        deathDirectionHintKind: DeathDirectionHintKind.FacingReverse),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                playerDeathHoldSignals: new[]
                {
                    new TickPlayerDeathHoldPresentationSignal(
                        10,
                        tickIndex,
                        eligibleTick,
                        eligibleTick - tickIndex,
                        startedThisTick: true),
                });

            return CreateTickResult(tickIndex, presentationData);
        }

        private static TickResult CreateEmptyTickResult(int tickIndex)
        {
            return CreateTickResult(tickIndex, TickPresentationData.Empty);
        }

        private static TickResult CreateElapsedSuppressedTickResult(int tickIndex)
        {
            return CreateTickResult(
                tickIndex,
                TickPresentationData.Empty,
                new[]
                {
                    "PlayerRespawnDelayElapsed|E=10|StartTick=50|EligibleTick=53|Tick=53",
                    "RespawnSuppressed|E=10|Reason=PolicyDisabled|Tick=53",
                });
        }

        private static TickResult CreateTickResult(int tickIndex, TickPresentationData presentationData)
        {
            return CreateTickResult(tickIndex, presentationData, Array.Empty<string>());
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            TickPresentationData presentationData,
            IEnumerable<string> eventLog)
        {
            var constructor = typeof(TickResult).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(int),
                    typeof(IEnumerable<TickPhase>),
                    typeof(IEnumerable<string>),
                    typeof(MovementPhaseResult),
                    typeof(AttackPhaseResult),
                    typeof(IEnumerable<EntityState>),
                    typeof(IEnumerable<string>),
                    typeof(CubeTopologyState),
                    typeof(TickPresentationData),
                    typeof(string),
                    typeof(TickTrace),
                    typeof(StageObjectiveTickResult),
                },
                null);
            Assert.That(constructor, Is.Not.Null);

            return (TickResult)constructor.Invoke(new object[]
            {
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                eventLog ?? Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective,
            });
        }

        private static MinimalStageCompletionReadModel CreateMinimalStageCompletionReadModel(string stageIdValue, int tickIndex)
        {
            var stageId = StageId.CreateOrThrow(stageIdValue);
            var result = new MinimalStageCompletionResult(
                stageId,
                new StageRunId($"campaign-test-run-{tickIndex}"),
                new StageCompletionAttemptId($"campaign-test-attempt-{tickIndex}"),
                StageTerminalReason.Cleared,
                wasCleared: true,
                finalTickIndex: tickIndex,
                new StageObjectiveProgressSnapshot(true, true, true, true, 1, 1),
                StageClearSource.Objective);

            return new MinimalStageCompletionReadModel(
                stageId,
                "Campaign Test Stage",
                result,
                new StageNavigationRequest(stageId, StageNavigationKind.Continue, "campaign-test-continue"),
                new StageNavigationRequest(stageId, StageNavigationKind.Retry, "campaign-test-retry"),
                StageNavigationRequest.None);
        }

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
        }

        private static string BuildCurrentSaveJson()
        {
            return JsonUtility.ToJson(SaveSlotDtoMapper.CreateEmptyDto());
        }

        private static string BuildLegacyPayloadJson()
        {
            return
                "{" +
                "\"SaveVersion\":1," +
                "\"Slots\":[{" +
                "\"SlotNumber\":1," +
                "\"CurrentStageId\":\"stage-0-1\"," +
                "\"ProgressByStageId\":[{" +
                "\"StageId\":\"stage-0-1\"," +
                "\"HasStarted\":true," +
                "\"HasCleared\":true," +
                "\"ClearCount\":99" +
                "}]" +
                "}]" +
                "}";
        }

        private static string ReadSaveProfileProductionSources()
        {
            var paths = new[]
            {
                "Assets/_Features/Stages/Runtime/ClearFlow/StageProgressAndCompletion.cs",
                "Assets/_Features/Stages/Runtime/Campaign/SaveSlotModels.cs",
                "Assets/_Features/DemoStageControl/Runtime/DemoStageControlBridges.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/UIAccess/GameplayHostStageCompletionRuntime.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/UIAccess/GameplayHostPresentationFeed.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs",
            };

            return StripStageClearSavePayloadGuardSource(string.Join(Environment.NewLine, paths.Select(File.ReadAllText)));
        }

        private static string StripStageClearSavePayloadGuardSource(string source)
        {
            const string startToken = "internal static class StageClearSavePayloadGuard";
            const string endToken = "internal readonly struct StageClearSavePrefsScope";
            var start = source.IndexOf(startToken, StringComparison.Ordinal);
            var end = source.IndexOf(endToken, StringComparison.Ordinal);
            if (start < 0 || end <= start)
            {
                return source;
            }

            return source.Remove(start, end - start);
        }

        private static class StageSaveSlotTestReset
        {
            public static void ClearDefaultPlayerPrefs()
            {
                PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacySaveSlotsKey);
                PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
                PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
                PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
                PlayerPrefs.DeleteKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey);
                PlayerPrefs.DeleteKey(EditorDirectPlayContextStore.TempActiveSlotProviderKey);
                PlayerPrefs.Save();
            }
        }

        private sealed class FakeBgmFlowCoordinator : IBgmFlowCoordinator
        {
            public int RequestCount { get; private set; }

            public int StopCount { get; private set; }

            public BgmProfile LastProfile { get; private set; }

            public void RequestSceneDefault(BgmProfile profile)
            {
                RequestCount++;
                LastProfile = profile;
            }

            public void StopCurrent()
            {
                StopCount++;
                LastProfile = null;
            }

            public BgmProfile GetCurrentProfile()
            {
                return LastProfile;
            }
        }

        private sealed class FakeStageLaunchRouter : IStageLaunchRouter
        {
            public StageNavigationRequest LastRequest { get; private set; } = StageNavigationRequest.None;

            public int LaunchCount { get; private set; }

            public void Launch(StageNavigationRequest request)
            {
                LaunchCount++;
                LastRequest = request;
            }
        }

        private sealed class FakeStageLaunchRouterProvider : MonoBehaviour, IStageLaunchRouterProvider
        {
            public FakeStageLaunchRouter Router { get; } = new();

            public bool TryCreateStageLaunchRouter(string currentSceneName, out IStageLaunchRouter router)
            {
                router = Router;
                return true;
            }
        }

        private sealed class RecordingTerminalPresentationExtension :
            IGameplayTickPresentationExtension,
            IGameplayStageTerminalPresentationExtension
        {
            public int ApplyCount { get; private set; }

            public GameplayStageTerminalPresentationReason LastReason { get; private set; }

            public int LastTickIndex { get; private set; }

            public void ResetSession()
            {
            }

            public void Present(in GameplayTickPresentationExtensionContext context)
            {
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void HardCleanup()
            {
            }

            public void ApplyStageTerminalPresentation(in GameplayStageTerminalPresentationContext context)
            {
                ApplyCount++;
                LastReason = context.Reason;
                LastTickIndex = context.TerminalTickResult?.TickIndex ?? -1;
            }
        }
    }
}
