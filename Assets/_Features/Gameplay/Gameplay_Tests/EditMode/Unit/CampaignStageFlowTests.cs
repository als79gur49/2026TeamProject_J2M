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
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Stages;
using Game.Product.Achievements;
using Game.Product.Achievements.CampaignIntegration;
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
            TerminalSessionRegistry.ResetForTests();
            StageSaveSlotTestReset.ClearDefaultPlayerPrefs();
        }

        [TearDown]
        public void TearDown()
        {
            TerminalSessionRegistry.ResetForTests();
            StageSaveSlotTestReset.ClearDefaultPlayerPrefs();
        }

        [Test]
        [Category("Extended")]
        public void SequenceResolver_UsesCanonicalOrderAndLevelGroups()
        {
            var resolver = CreateResolver();

            Assert.That(resolver.FirstStageId.Value, Is.EqualTo("stage-0-1"));
            Assert.That(resolver.FinalStageId.Value, Is.EqualTo("stage-4-3"));
            Assert.That(resolver.IsFinal(StageId.CreateOrThrow("stage-4-3")), Is.True);
            Assert.That(resolver.Contains(StageId.CreateOrThrow("stage-5-1")), Is.False);
            Assert.That(
                RetiredCampaignSaveCompatibilityPolicy.IsRetiredCompletedStageId(StageId.CreateOrThrow("stage-5-1")),
                Is.True);
            Assert.That(resolver.GetNextOrNone(StageId.CreateOrThrow("stage-2-1")).Value, Is.EqualTo("stage-2-2"));
            Assert.That(resolver.GetFirstStageInLevelGroupOrNone("level-2").Value, Is.EqualTo("stage-2-1"));
            Assert.That(resolver.GetNextOrNone(StageId.CreateOrThrow("stage-0-2")).Value, Is.EqualTo("stage-0-3"));
            Assert.That(resolver.GetNextOrNone(StageId.CreateOrThrow("stage-0-3")).Value, Is.EqualTo("stage-1-1"));
            Assert.That(resolver.GetNextOrNone(StageId.CreateOrThrow("stage-1-1")).Value, Is.EqualTo("stage-1-2"));
            Assert.That(resolver.GetNextOrNone(StageId.CreateOrThrow("stage-2-2")).Value, Is.EqualTo("stage-3-1"));
            Assert.That(resolver.GetNextOrNone(StageId.CreateOrThrow("stage-3-3")).Value, Is.EqualTo("stage-4-1"));
            Assert.That(resolver.GetNextOrNone(StageId.CreateOrThrow("stage-4-2")).Value, Is.EqualTo("stage-4-3"));
            Assert.That(resolver.Entries.Count, Is.GreaterThan(0));
        }

        [Test]
        [Category("Extended")]
        public void SequenceValidator_ReportsCatalogMissingStageIds()
        {
            var definition = CampaignStageSequenceTestAsset.LoadProductionDefinition();
            var aliasTable = ScriptableObject.CreateInstance<StageIdAliasTable>();
            try
            {
                var report = new CampaignStageSequenceValidator().ValidateAuthoritativeAsset(
                    definition,
                    new[] { CreateEntry("stage-0-1") },
                    aliasTable,
                    StageValidationTiming.TestOrCi);

                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Code == "campaign-sequence.authoritative.catalog-missing"),
                    Is.True);
                Assert.That(report.HasErrors, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aliasTable);
            }
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
        public void SaveSlotStore_FacadePlayerPrefsBackend_PreservesLoadDeleteAndClearBehavior()
        {
            var key = CreatePrefsKey(nameof(SaveSlotStore_FacadePlayerPrefsBackend_PreservesLoadDeleteAndClearBehavior));
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
            });

            var reloaded = new SaveSlotStore(key);
            var loaded = reloaded.LoadAll();
            Assert.That(loaded[0].CurrentStageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(loaded[1].CurrentStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(PlayerPrefs.HasKey(key), Is.True);

            reloaded.DeleteSlot(1);
            var afterDelete = new SaveSlotStore(key).LoadAll();
            Assert.That(afterDelete[0].IsEmpty, Is.True);
            Assert.That(afterDelete[1].CurrentStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(PlayerPrefs.HasKey(key), Is.True);

            reloaded.ClearAll();

            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            Assert.That(new SaveSlotStore(key).LoadAll().All(slot => slot.IsEmpty), Is.True);
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
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.EqualTo(1));
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
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.EqualTo(2));
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
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.EqualTo(1));
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
        public void RunningSlotStageClearProfileStore_LoadSave_StaysOnRunningSlotAfterActiveSlotChanges()
        {
            var saveKey = CreatePrefsKey(nameof(RunningSlotStageClearProfileStore_LoadSave_StaysOnRunningSlotAfterActiveSlotChanges));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData { SlotNumber = 1, CurrentStageId = StageId.CreateOrThrow("stage-1-1") });
            saveStore.SaveSlot(new SaveSlotData { SlotNumber = 2, CurrentStageId = StageId.CreateOrThrow("stage-2-1") });
            activeSlotProvider.SetActiveSlot(2);

            var profileStore = new SaveSlotStageClearProfileStore(saveStore, new CampaignRunningSlotContext(2));
            activeSlotProvider.SetActiveSlot(1);
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
            var loadedSnapshot = profileStore.Load();
            var activeRecord = loadedSnapshot
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

        [TestCase("stage-0-3", "level-0", "stage-1-1", "level-1")]
        [TestCase("stage-1-2", "level-1", "stage-2-1", "level-2")]
        [TestCase("stage-2-2", "level-2", "stage-3-1", "level-3")]
        [TestCase("stage-3-3", "level-3", "stage-4-1", "level-4")]
        [Category("Extended")]
        public void StageClear_AdvancesAcrossLevelGroupsRestoringRemainingChancesAndKeepingCurrentSceneDisplayStable(
            string completedStageId,
            string completedLevelGroupId,
            string expectedNextStageId,
            string expectedNextLevelGroupId)
        {
            AssertStageClearChancePolicy(
                nameof(StageClear_AdvancesAcrossLevelGroupsRestoringRemainingChancesAndKeepingCurrentSceneDisplayStable) +
                completedStageId,
                completedStageId,
                completedLevelGroupId,
                expectedNextStageId,
                expectedNextLevelGroupId,
                expectedSavedChances: SaveSlotStore.DefaultRemainingChances,
                expectedDisplayedChances: 1,
                expectedAudioPolicy: GameplayChanceAudioPolicy.SuppressChanceChangeCue);
        }

        [TestCase("stage-0-1", "level-0", "stage-0-2", "level-0")]
        [TestCase("stage-0-2", "level-0", "stage-0-3", "level-0")]
        [TestCase("stage-1-1", "level-1", "stage-1-2", "level-1")]
        [TestCase("stage-2-1", "level-2", "stage-2-2", "level-2")]
        [TestCase("stage-3-1", "level-3", "stage-3-2", "level-3")]
        [TestCase("stage-3-2", "level-3", "stage-3-3", "level-3")]
        [TestCase("stage-4-1", "level-4", "stage-4-2", "level-4")]
        [TestCase("stage-4-2", "level-4", "stage-4-3", "level-4")]
        [Category("Extended")]
        public void StageClear_AdvancesWithinLevelGroupPreservingRemainingChances(
            string completedStageId,
            string completedLevelGroupId,
            string expectedNextStageId,
            string expectedNextLevelGroupId)
        {
            AssertStageClearChancePolicy(
                nameof(StageClear_AdvancesWithinLevelGroupPreservingRemainingChances) + completedStageId,
                completedStageId,
                completedLevelGroupId,
                expectedNextStageId,
                expectedNextLevelGroupId,
                expectedSavedChances: 1,
                expectedDisplayedChances: 1,
                expectedAudioPolicy: GameplayChanceAudioPolicy.Default);
        }

        [Test]
        [Category("Extended")]
        public void DivergentSequence_ClearAlignsResultNextSavedCursorAndRetryGroupFirst()
        {
            var saveKey = CreatePrefsKey(nameof(DivergentSequence_ClearAlignsResultNextSavedCursorAndRetryGroupFirst));
            var saveStore = new SaveSlotStore(saveKey);
            var hostObject = new GameObject("divergent-sequence-clear-host");
            var resolver = CreateDivergentResolver(out var definition);
            try
            {
                saveStore.ClearAll();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("fixture-a"),
                    CurrentLevelGroupId = "group-a",
                    RemainingChances = 2,
                });
                var readModel = MinimalStageCompletionReadModelBuilder.Build(
                    entry: null,
                    clearResult: CreateClearResult("fixture-a"),
                    sequenceResolver: resolver);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    resolver,
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());

                GetHandleStageClearMethod().Invoke(controller, new object[] { null, readModel });

                Assert.That(readModel.NextStageRequest.StageId, Is.EqualTo(StageId.CreateOrThrow("fixture-c")));
                Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-c")));
                Assert.That(saveStore.LoadSlot(1).CurrentLevelGroupId, Is.EqualTo("group-b"));

                var retryRoute = new StageRetryChanceTracker(resolver).ResolveDeathRoute(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("fixture-b"),
                    CurrentLevelGroupId = "group-b",
                    RemainingChances = 1,
                });
                Assert.That(retryRoute.NextStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-c")));
                Assert.That(retryRoute.RouteKind, Is.EqualTo(StageRetryRouteKind.ReturnToLevelGroupFirstStage));
            }
            finally
            {
                saveStore.ClearAll();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Extended")]
        public void DivergentSequence_FinalClearAlignsReadModelSaveAndTerminalDestination()
        {
            var saveKey = CreatePrefsKey(nameof(DivergentSequence_FinalClearAlignsReadModelSaveAndTerminalDestination));
            var saveStore = new SaveSlotStore(saveKey);
            var hostObject = new GameObject("divergent-sequence-final-host");
            var resolver = CreateDivergentResolver(out var definition);
            try
            {
                saveStore.ClearAll();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("fixture-b"),
                    CurrentLevelGroupId = "group-b",
                    RemainingChances = 2,
                });
                var readModel = MinimalStageCompletionReadModelBuilder.Build(
                    entry: null,
                    clearResult: CreateClearResult("fixture-b"),
                    sequenceResolver: resolver);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    resolver,
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());

                GetHandleStageClearMethod().Invoke(controller, new object[] { null, readModel });

                Assert.That(readModel.NextStageRequest.IsValid, Is.False);
                Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-b")));
                Assert.That(saveStore.LoadSlot(1).CampaignCompleted, Is.True);
                Assert.That(
                    TerminalSessionRegistry.Current.DestinationKind,
                    Is.EqualTo(TerminalDestinationKind.SameSceneGameClear));
            }
            finally
            {
                saveStore.ClearAll();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Extended")]
        public void NormalFinalObjectiveClear_CommitsCampaignCompletedAndReceiptInOneSlotUpdate()
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-4-3"));
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew,
                () => store.SaveCount == 1);
            var hostObject = new GameObject("normal-final-receipt-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel(
                        "stage-4-3",
                        tickIndex: 101));

                var saved = store.LoadSlot(1);
                Assert.That(store.UpdateCount, Is.EqualTo(1));
                Assert.That(store.SaveCount, Is.EqualTo(1));
                Assert.That(saved.CampaignCompleted, Is.True);
                Assert.That(saved.NormalCampaignCompletionReceipt, Is.Not.Null);
                Assert.That(saved.NormalCampaignCompletionReceipt.Version, Is.EqualTo(2));
                Assert.That(saved.NormalCampaignCompletionReceipt.CompletedStageId, Is.EqualTo("stage-4-3"));
                Assert.That(saved.NormalCampaignCompletionReceipt.StageRunId, Is.Empty);
                Assert.That(saved.NormalCampaignCompletionReceipt.ClearSource, Is.EqualTo(-1));
                Assert.That(store.LastMutationObservedCampaignCompleted, Is.True);
                Assert.That(store.LastMutationObservedReceipt, Is.True);
                Assert.That(earningSink.EarnCount, Is.EqualTo(1));
                Assert.That(earningSink.SaveWasCommittedAtEarn, Is.True);
                Assert.That(
                    earningSink.LastAchievementId,
                    Is.EqualTo(GameAchievementIds.NormalCampaignComplete));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void NormalStage1_2Clear_CommitsAttemptRecordBeforeEarningBothStageAchievements()
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-1-2"));
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew,
                () => store.SaveCount == 1);
            var hostObject = new GameObject("normal-stage-1-2-achievement-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    stageAchievementIntegration:
                        new CampaignStageAchievementIntegration(earningSink));

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel(
                        "stage-1-2",
                        tickIndex: 110));

                var records = store.LoadSlot(1).NormalStagePerformanceRecords;
                Assert.That(records, Has.Length.EqualTo(1));
                Assert.That(records[0].StageId.Value, Is.EqualTo("stage-1-2"));
                Assert.That(records[0].BestCombinedPushFlipUses, Is.Zero);
                Assert.That(earningSink.EarnCount, Is.EqualTo(2));
                Assert.That(earningSink.SaveWasCommittedAtEarn, Is.True);
                Assert.That(
                    earningSink.LastAchievementId,
                    Is.EqualTo(GameAchievementIds.CampaignStage1_2PushFlipWithin25));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [TestCase(24, 2)]
        [TestCase(25, 2)]
        [TestCase(26, 1)]
        [Category("Core")]
        public void NormalStage1_2Clear_ProductionFeedPersistsExactPushFlipBoundaryBeforeEarning(
            int combinedPushFlipUses,
            int expectedEarnCount)
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-1-2"));
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew,
                () => store.SaveCount == 1);
            var hostObject = new GameObject(
                $"normal-stage-1-2-production-feed-{combinedPushFlipUses}");
            var entry = CreateEntry("stage-1-2");
            var presentationDefinition = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(
                presentationDefinition,
                "displayNameKey",
                StageDisplayNameKeys.ForStage(entry.StageId));
            entry.AssignPresentationDefinition(presentationDefinition);
            CampaignGameplayFlowController controller = null;
            GameplayHostUiAccessContext uiAccess = null;

            try
            {
                var resolver = CreateResolver();
                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var host = CreateHostWithInput(
                    hostObject,
                    playerEntityId: 10,
                    respawnDelayTicks: 3,
                    presenter);
                var feed = new GameplayHostPresentationFeed(
                    host.InputHost,
                    presenter,
                    entry,
                    campaignStageSequenceResolver: resolver);
                uiAccess = new GameplayHostUiAccessContext(
                    new NoOpGameplayCommandGateway(),
                    new NoOpGameplayQueryFacade(),
                    feed,
                    new NoOpGameplayPauseService(),
                    campaignStageSequenceResolver: resolver);
                AttachUiAccess(host, presenter, uiAccess, respawnDelayTicks: 3);
                controller = new CampaignGameplayFlowController(
                    host,
                    store,
                    new CampaignRunningSlotContext(1),
                    resolver,
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort(),
                    campaignStageAchievementIntegration:
                        new CampaignStageAchievementIntegration(earningSink));
                controller.Bind();

                for (var useIndex = 1; useIndex <= combinedPushFlipUses; useIndex++)
                {
                    RaiseInputHostTickCompleted(
                        host.InputHost,
                        CreateCountedPushFlipTickResult(
                            tickIndex: useIndex,
                            actionSequence: useIndex,
                            actionKind: useIndex % 2 == 0
                                ? PlayerActionKind.Flip
                                : PlayerActionKind.Push,
                            resolutionKind: useIndex % 2 == 0
                                ? TickPlayerActionResolutionKind.Impact
                                : TickPlayerActionResolutionKind.Success,
                            objectiveCleared: useIndex == combinedPushFlipUses));
                }

                var records = store.LoadSlot(1).NormalStagePerformanceRecords;
                Assert.That(records, Has.Length.EqualTo(1));
                Assert.That(records[0].StageId.Value, Is.EqualTo("stage-1-2"));
                Assert.That(records[0].BestCombinedPushFlipUses, Is.EqualTo(combinedPushFlipUses));
                Assert.That(store.SaveCount, Is.EqualTo(1));
                Assert.That(earningSink.SaveWasCommittedAtEarn, Is.True);
                Assert.That(earningSink.EarnCount, Is.EqualTo(expectedEarnCount));
                Assert.That(
                    earningSink.EarnedAchievementIds,
                    Does.Contain(GameAchievementIds.CampaignStage1_2Clear));
                Assert.That(
                    earningSink.EarnedAchievementIds.Contains(
                        GameAchievementIds.CampaignStage1_2PushFlipWithin25),
                    Is.EqualTo(combinedPushFlipUses <= 25));
            }
            finally
            {
                controller?.Dispose();
                uiAccess?.Dispose();
                UnityEngine.Object.DestroyImmediate(presentationDefinition);
                UnityEngine.Object.DestroyImmediate(entry);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void FinalClear_SaveFailureLeavesNoDurableCampaignCompletionOrReceipt()
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-4-3"));
            store.ThrowOnUpdate = true;
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew);
            var hostObject = new GameObject("failed-final-receipt-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                var exception = Assert.Throws<TargetInvocationException>(() =>
                    InvokeStageClear(
                        controller,
                        CreateMinimalStageCompletionReadModel("stage-4-3", tickIndex: 102)));

                Assert.That(exception?.InnerException, Is.TypeOf<IOException>());
                Assert.That(store.UpdateCount, Is.EqualTo(1));
                Assert.That(store.SaveCount, Is.Zero);
                Assert.That(store.LoadSlot(1).CampaignCompleted, Is.False);
                Assert.That(store.LoadSlot(1).NormalCampaignCompletionReceipt, Is.Null);
                Assert.That(earningSink.EarnCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void FinalForcedClear_PreservesCampaignProgressionWithoutNormalReceipt()
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-4-3"));
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew);
            var hostObject = new GameObject("forced-final-receipt-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel(
                        "stage-4-3",
                        tickIndex: 103),
                    hasObjectiveClear: false);

                Assert.That(store.LoadSlot(1).CampaignCompleted, Is.True);
                Assert.That(store.LoadSlot(1).NormalCampaignCompletionReceipt, Is.Null);
                Assert.That(store.LoadSlot(1).NormalStagePerformanceRecords, Is.Empty);
                Assert.That(earningSink.EarnCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [TestCase(EditorDirectPlayMode.NonCampaign)]
        [TestCase(EditorDirectPlayMode.CampaignTempSlot)]
        [TestCase(EditorDirectPlayMode.CampaignProductionSlot)]
        [Category("Extended")]
        public void FinalClear_EveryDirectPlayModeCreatesNoReceipt(EditorDirectPlayMode mode)
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-4-3"));
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew);
            var hostObject = new GameObject("direct-play-final-receipt-host");
            try
            {
                var context = new EditorDirectPlayContext(
                    mode,
                    StageId.CreateOrThrow("stage-4-3"),
                    string.Empty,
                    string.Empty,
                    3,
                    suppressCampaignFlow: false);
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    editorDirectPlayContext: context,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel("stage-4-3", tickIndex: 104));

                Assert.That(store.LoadSlot(1).CampaignCompleted, Is.True);
                Assert.That(store.LoadSlot(1).NormalCampaignCompletionReceipt, Is.Null);
                Assert.That(store.LoadSlot(1).NormalStagePerformanceRecords, Is.Empty);
                Assert.That(earningSink.EarnCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void FinalClear_ExistingReceiptIsPreservedWithoutDuplicateWrite()
        {
            var existing = new NormalCampaignCompletionReceipt
            {
                Version = 1,
                CompletedStageId = "stage-4-3",
                StageRunId = "original-run",
                ClearSource = 0,
            };
            var slot = CreateCampaignSlot(1, "stage-4-3");
            slot.CampaignCompleted = true;
            slot.HasNormalCampaignCompletionReceipt = true;
            slot.NormalCampaignCompletionReceipt = existing;
            var store = RecordingCampaignSaveSlotStore.WithSlot(slot);
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew);
            var hostObject = new GameObject("existing-final-receipt-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel(
                        "stage-4-3",
                        tickIndex: 105));

                Assert.That(store.UpdateCount, Is.EqualTo(1));
                Assert.That(store.SaveCount, Is.EqualTo(1));
                Assert.That(
                    store.LoadSlot(1).NormalCampaignCompletionReceipt.StageRunId,
                    Is.EqualTo("original-run"));
                Assert.That(earningSink.EarnCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void FinalClear_InvalidExistingReceiptIsNotSilentlyReplaced()
        {
            var slot = CreateCampaignSlot(1, "stage-4-3");
            slot.HasNormalCampaignCompletionReceipt = true;
            slot.NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
            {
                Version = 99,
                CompletedStageId = "stage-4-3",
                StageRunId = "invalid-version-run",
                ClearSource = 0,
            };
            var store = RecordingCampaignSaveSlotStore.WithSlot(slot);
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew);
            var hostObject = new GameObject("invalid-existing-final-receipt-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel("stage-4-3", tickIndex: 106));

                Assert.That(store.LoadSlot(1).NormalCampaignCompletionReceipt.Version, Is.EqualTo(99));
                Assert.That(
                    store.LoadSlot(1).NormalCampaignCompletionReceipt.StageRunId,
                    Is.EqualTo("invalid-version-run"));
                Assert.That(earningSink.EarnCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void FinalClear_PresentNullReceiptIsNotSilentlyReplaced()
        {
            var slot = CreateCampaignSlot(1, "stage-4-3");
            slot.HasNormalCampaignCompletionReceipt = true;
            slot.NormalCampaignCompletionReceipt = null;
            var store = RecordingCampaignSaveSlotStore.WithSlot(slot);
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew);
            var hostObject = new GameObject("present-null-final-receipt-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel("stage-4-3", tickIndex: 107));

                Assert.That(store.LoadSlot(1).HasNormalCampaignCompletionReceipt, Is.True);
                Assert.That(store.LoadSlot(1).NormalCampaignCompletionReceipt, Is.Null);
                Assert.That(earningSink.EarnCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [TestCase(AchievementEarnResult.PersistenceFailed)]
        [TestCase(AchievementEarnResult.UnavailableState)]
        [TestCase(AchievementEarnResult.InvalidAchievement)]
        [Category("Extended")]
        public void FinalClear_ProductFailureResult_DoesNotBlockCommittedCampaignOrTerminal(
            AchievementEarnResult earnResult)
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-4-3"));
            var earningSink = new RecordingProductAchievementEarningSink(earnResult);
            var hostObject = new GameObject("product-failure-final-clear-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                Assert.DoesNotThrow(() => InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel(
                        "stage-4-3",
                        tickIndex: 108)));

                Assert.That(store.LoadSlot(1).CampaignCompleted, Is.True);
                Assert.That(store.LoadSlot(1).NormalCampaignCompletionReceipt, Is.Not.Null);
                Assert.That(earningSink.EarnCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void FinalClear_UnexpectedIntegrationException_DoesNotBlockCommittedCampaignOrTerminal()
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-4-3"));
            var hostObject = new GameObject("integration-exception-final-clear-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration: new ThrowingAchievementIntegration());

                Assert.DoesNotThrow(() => InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel(
                        "stage-4-3",
                        tickIndex: 109)));

                Assert.That(store.LoadSlot(1).CampaignCompleted, Is.True);
                Assert.That(store.LoadSlot(1).NormalCampaignCompletionReceipt, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void NonFinalObjectiveClear_ProductEarnIsZero()
        {
            var store = RecordingCampaignSaveSlotStore.WithSlot(
                CreateCampaignSlot(1, "stage-4-2"));
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew);
            var hostObject = new GameObject("non-final-achievement-host");
            try
            {
                var controller = CreateReceiptController(
                    hostObject,
                    store,
                    slotNumber: 1,
                    achievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel("stage-4-2", tickIndex: 110));

                Assert.That(store.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-4-3"));
                Assert.That(earningSink.EarnCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void FinalClear_WritesOnlyRunningSlotReceipt()
        {
            var first = CreateCampaignSlot(1, "stage-4-3");
            first.CampaignCompleted = true;
            first.NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
            {
                Version = 1,
                CompletedStageId = "stage-4-3",
                StageRunId = "slot-one-run",
                ClearSource = 0,
            };
            var store = new RecordingCampaignSaveSlotStore(
                first,
                CreateCampaignSlot(2, "stage-4-3"));
            var hostObject = new GameObject("other-slot-final-receipt-host");
            try
            {
                var controller = CreateReceiptController(hostObject, store, slotNumber: 2);

                InvokeStageClear(
                    controller,
                    CreateMinimalStageCompletionReadModel(
                        "stage-4-3",
                        tickIndex: 107));

                Assert.That(store.LastUpdatedSlotNumber, Is.EqualTo(2));
                Assert.That(store.LoadSlot(1).NormalCampaignCompletionReceipt.StageRunId, Is.EqualTo("slot-one-run"));
                Assert.That(store.LoadSlot(2).NormalCampaignCompletionReceipt.Version, Is.EqualTo(2));
                Assert.That(store.LoadSlot(2).NormalCampaignCompletionReceipt.StageRunId, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignBootstrap_MissingTerminalPortFailsFastAndControllerRejectsNull()
        {
            var owner = new GameObject("campaign-missing-terminal-port");
            var hostObject = new GameObject("campaign-null-terminal-controller");
            var saveKey = CreatePrefsKey(nameof(CampaignBootstrap_MissingTerminalPortFailsFastAndControllerRejectsNull));
            var saveStore = new SaveSlotStore(saveKey);
            try
            {
                var createPort = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                    "CreateTerminalTransitionPort",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(createPort, Is.Not.Null);
                var bootstrapException = Assert.Throws<TargetInvocationException>(
                    () => createPort.Invoke(null, new object[] { owner }));
                Assert.That(bootstrapException?.InnerException, Is.TypeOf<InvalidOperationException>());
                StringAssert.Contains(
                    "requires a co-located ITerminalTransitionPortProvider",
                    bootstrapException?.InnerException?.Message);

                var host = hostObject.AddComponent<GameplaySceneHost>();
                Assert.Throws<ArgumentNullException>(() =>
                    new CampaignGameplayFlowController(
                        host,
                        saveStore,
                        new CampaignRunningSlotContext(1),
                        CreateResolver(),
                        new FakeStageLaunchRouter(),
                        chanceDisplayOverride: null,
                        terminalTransitionPort: null));
            }
            finally
            {
                saveStore.ClearAll();
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignClear_UsesRunningSlotAfterActiveSlotChanges()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignClear_UsesRunningSlotAfterActiveSlotChanges));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-clear-running-slot-host");

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                    CurrentLevelGroupId = "level-1",
                    RemainingChances = 2,
                });
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 2,
                    CurrentStageId = StageId.CreateOrThrow("stage-3-1"),
                    CurrentLevelGroupId = "level-3",
                    RemainingChances = 1,
                });
                activeSlotProvider.SetActiveSlot(1);

                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
                activeSlotProvider.SetActiveSlot(2);

                var method = typeof(CampaignGameplayFlowController).GetMethod(
                    "HandleStageClear",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                method.Invoke(
                    controller,
                    new object[]
                    {
                        null,
                        CreateMinimalStageCompletionReadModel("stage-1-1", tickIndex: 10),
                    });

                Assert.That(saveStore.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-1-2"));
                Assert.That(saveStore.LoadSlot(2).CurrentStageId.Value, Is.EqualTo("stage-3-1"));
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
        public void CampaignDeath_UsesRunningSlotAfterActiveSlotChanges()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_UsesRunningSlotAfterActiveSlotChanges));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-running-slot-host");

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
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 2,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                    CurrentLevelGroupId = "level-1",
                    RemainingChances = 3,
                });
                activeSlotProvider.SetActiveSlot(1);

                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
                activeSlotProvider.SetActiveSlot(2);

                GetHandleTickCompletedMethod().Invoke(
                    controller,
                    new object[] { CreateDeathTickResult(50, eligibleTick: 53) });

                Assert.That(saveStore.LoadSlot(1).RemainingChances, Is.EqualTo(1));
                Assert.That(saveStore.LoadSlot(1).TotalDeaths, Is.EqualTo(1));
                Assert.That(saveStore.LoadSlot(2).RemainingChances, Is.EqualTo(3));
                Assert.That(saveStore.LoadSlot(2).TotalDeaths, Is.EqualTo(0));
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
        public void SaveSlotCampaignChancesReadSource_UsesRunningSlotAfterActiveSlotChanges()
        {
            var saveKey = CreatePrefsKey(nameof(SaveSlotCampaignChancesReadSource_UsesRunningSlotAfterActiveSlotChanges));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);

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
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 2,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                    CurrentLevelGroupId = "level-1",
                    RemainingChances = 1,
                });
                activeSlotProvider.SetActiveSlot(1);
                var source = new SaveSlotCampaignChancesReadSource(
                    saveStore,
                    new CampaignRunningSlotContext(1));
                activeSlotProvider.SetActiveSlot(2);

                var read = source.TryReadChances(
                    out var remainingChances,
                    out var maxChances,
                    out _);

                Assert.That(read, Is.True);
                Assert.That(remainingChances, Is.EqualTo(2));
                Assert.That(maxChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
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

        [TestCase(3, 0, 2)]
        [TestCase(3, 4, 2)]
        [TestCase(2, 0, 1)]
        [Category("Extended")]
        public void CampaignDeath_RetryableMutationPublishesHudAudioSuppression(
            int remainingBefore,
            int totalDeathsBefore,
            int remainingAfter)
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_RetryableMutationPublishesHudAudioSuppression));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-chance-audio-policy-host");
            var router = new FakeStageLaunchRouter();
            var chanceDisplayOverride = new CampaignChanceDisplayOverride();
            var runningSlotContext = new CampaignRunningSlotContext(1);
            var chancesReadSource = new SaveSlotCampaignChancesReadSource(
                saveStore,
                runningSlotContext,
                chanceDisplayOverride);

            try
            {
                TerminalSessionRegistry.ResetForTests();
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-2",
                    RemainingChances = remainingBefore,
                    TotalDeaths = totalDeathsBefore,
                });
                activeSlotProvider.SetActiveSlot(1);

                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    runningSlotContext,
                    CreateResolver(),
                    router,
                    chanceDisplayOverride,
                    new FakeTerminalTransitionPort());

                GetHandleTickCompletedMethod().Invoke(
                    controller,
                    new object[] { CreateDeathTickResult(50, eligibleTick: 53) });

                Assert.That(
                    chancesReadSource.TryReadChances(
                        out var observedRemaining,
                        out var observedMaximum,
                        out var audioPolicy),
                    Is.True);
                Assert.That(observedRemaining, Is.EqualTo(remainingAfter));
                Assert.That(observedMaximum, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
                Assert.That(audioPolicy, Is.EqualTo(GameplayChanceAudioPolicy.SuppressChanceChangeCue));
                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.TransitionHint.Kind, Is.EqualTo(StageTransitionKind.DeathRetryChanceLost));
                Assert.That(router.LastRequest.TransitionHint.ChanceLostPayload.PreviousRemainingChances, Is.EqualTo(remainingBefore));
                Assert.That(router.LastRequest.TransitionHint.ChanceLostPayload.CurrentRemainingChances, Is.EqualTo(remainingAfter));
                Assert.That(saveStore.LoadSlot(1).TotalDeaths, Is.EqualTo(totalDeathsBefore + 1));
            }
            finally
            {
                TerminalSessionRegistry.ResetForTests();
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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());

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
        public void CampaignDeath_LevelFailedClaimsTerminalImmediatelyWithoutEligibleTick()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_LevelFailedClaimsTerminalImmediatelyWithoutEligibleTick));
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
                var terminalPort = new FakeTerminalTransitionPort();
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: terminalPort);
                SetPrivateField(controller, "_presentationFeed", presentationFeed);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });

                Assert.That(terminalPort.Current, Is.Not.Null);
                Assert.That(presentationFeed.CurrentLevelFailed, Is.Null);
                terminalPort.Current.Advance(terminalPort.Current.Preset.BlackAt);

                Assert.That(presentationFeed.CurrentLevelFailed, Is.Not.Null);
                Assert.That(presentationFeed.CurrentLevelFailed.RestartLevelRequest.StageId.Value, Is.EqualTo("stage-2-1"));
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(host.InputHost.RunSingleTick(), Is.Null);
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
        public void CampaignDeath_LevelFailedPublishesOnlyAfterDefeatIrisBlackReached()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_LevelFailedPublishesOnlyAfterDefeatIrisBlackReached));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-level-failed-iris-host");

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 1);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var presentationFeed = new GameplayHostPresentationFeed(host.InputHost, presenter);
                var terminalPort = new FakeTerminalTransitionPort();
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: terminalPort);
                SetPrivateField(controller, "_presentationFeed", presentationFeed);

                GetHandleTickCompletedMethod().Invoke(
                    controller,
                    new object[] { CreateDeathTickResult(50, eligibleTick: 999) });

                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(presentationFeed.CurrentLevelFailed, Is.Null);
                Assert.That(terminalPort.Current.Request.Kind, Is.EqualTo(TerminalTransitionKind.Defeat));

                terminalPort.Current.Advance(terminalPort.Current.Preset.BlackAt);

                Assert.That(presentationFeed.CurrentLevelFailed, Is.Not.Null);
                Assert.That(
                    presentationFeed.CurrentLevelFailed.Reason,
                    Is.EqualTo(GameplayLevelFailureReason.ChancesExhausted));
                Assert.That(presentationFeed.CurrentLevelFailed.RestartLevelRequest.StageId.Value, Is.EqualTo("stage-2-1"));
                Assert.That(
                    presentationFeed.CurrentLevelFailed.RestartLevelRequest.NavigationKind,
                    Is.EqualTo(StageNavigationKind.Retry));
                Assert.That(
                    presentationFeed.CurrentLevelFailed.RestartLevelRequest.TransitionHint.Kind,
                    Is.EqualTo(StageTransitionKind.LevelFailedRestart));
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(terminalPort.Current.State, Is.EqualTo(TerminalTransitionState.Black));
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
                var terminalPort = new FakeTerminalTransitionPort();
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: terminalPort);
                SetPrivateField(controller, "_presentationFeed", presentationFeed);
                var handleTickCompleted = GetHandleTickCompletedMethod();

                handleTickCompleted.Invoke(controller, new object[] { CreateDeathTickResult(50, eligibleTick: 53) });
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.True);
                Assert.That(presentationFeed.CurrentLevelFailed, Is.Null);
                terminalPort.Current.Advance(terminalPort.Current.Preset.BlackAt);
                Assert.That(presentationFeed.CurrentLevelFailed, Is.Not.Null);
                Assert.That(terminalExtension.ApplyCount, Is.EqualTo(1));
                Assert.That(terminalExtension.LastReason, Is.EqualTo(GameplayStageTerminalPresentationReason.LevelFailed));
                Assert.That(terminalExtension.LastTickIndex, Is.EqualTo(50));
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
                new CampaignRunningSlotContext(1),
                chanceDisplayOverride);

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 1);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride,
                    new FakeTerminalTransitionPort());
                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var presentationFeed = new GameplayHostPresentationFeed(host.InputHost, presenter);
                SetPrivateField(controller, "_presentationFeed", presentationFeed);
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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
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
        [Category("Core")]
        public void CampaignDeath_IrisSetupThrow_ReleasesExactHoldAndUsesNonIrisRetryFallback()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_IrisSetupThrow_ReleasesExactHoldAndUsesNonIrisRetryFallback));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-death-iris-setup-fallback-host");
            var router = new FakeStageLaunchRouter();
            var setupException = new InvalidOperationException("defeat iris setup failed");

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort(setupException));
                var handler = (Action<TickResult>)Delegate.CreateDelegate(
                    typeof(Action<TickResult>),
                    controller,
                    GetHandleTickCompletedMethod());

                var thrown = Assert.Throws<InvalidOperationException>(
                    () => handler(CreateDeathTickResult(50, eligibleTick: 53)));

                Assert.That(thrown, Is.SameAs(setupException));
                Assert.That(TerminalSessionRegistry.IsActive, Is.False);
                Assert.That(TerminalSessionRegistry.Current.Phase, Is.EqualTo(TerminalSessionPhase.FailedBeforeCover));
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.False);
                Assert.That(router.LaunchCount, Is.EqualTo(1));
                Assert.That(router.LastRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));
                Assert.That(router.LastRequest.TransitionHint.HasTerminalClaim, Is.False);
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CampaignLevelFailed_IrisSetupThrow_ReleasesExactHoldAndPublishesFallback()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignLevelFailed_IrisSetupThrow_ReleasesExactHoldAndPublishesFallback));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-level-failed-iris-setup-fallback-host");
            var setupException = new InvalidOperationException("level-failed iris setup failed");

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 1);
                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var host = CreateHostWithInput(
                    hostObject,
                    playerEntityId: 10,
                    respawnDelayTicks: 3,
                    presenter);
                var feed = new GameplayHostPresentationFeed(host.InputHost, presenter);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort(setupException));
                SetPrivateField(controller, "_presentationFeed", feed);
                var handler = (Action<TickResult>)Delegate.CreateDelegate(
                    typeof(Action<TickResult>),
                    controller,
                    GetHandleTickCompletedMethod());

                var thrown = Assert.Throws<InvalidOperationException>(
                    () => handler(CreateDeathTickResult(50, eligibleTick: 53)));

                Assert.That(thrown, Is.SameAs(setupException));
                Assert.That(TerminalSessionRegistry.IsActive, Is.False);
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.False);
                Assert.That(feed.CurrentLevelFailed, Is.Not.Null);
                Assert.That(
                    feed.CurrentLevelFailed.RestartLevelRequest.StageId.Value,
                    Is.EqualTo("stage-2-1"));
                feed.Dispose();
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void CampaignVictory_IrisSetupThrow_ReleasesGateAndExactHoldWithCommittedOutcome()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignVictory_IrisSetupThrow_ReleasesGateAndExactHoldWithCommittedOutcome));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-victory-iris-setup-fallback-host");
            var setupException = new InvalidOperationException("victory iris setup failed");
            var entry = CreateEntry("stage-1-1");
            var presentationDefinition = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(
                presentationDefinition,
                "displayNameKey",
                StageDisplayNameKeys.ForStage(entry.StageId));
            entry.AssignPresentationDefinition(presentationDefinition);

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-1-1", "level-1", remainingChances: 3);
                var presenter = hostObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var host = CreateHostWithInput(
                    hostObject,
                    playerEntityId: 10,
                    respawnDelayTicks: 3,
                    presenter);
                var feed = new GameplayHostPresentationFeed(host.InputHost, presenter, entry);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort(setupException));
                SetPrivateField(controller, "_presentationFeed", feed);
                var arbiter = ReadPrivateField<TerminalArbitrationOwner>(controller, "_terminalArbiter");
                feed.ConfigureTerminalArbiter(arbiter);
                var acceptedHandler = (Action<TerminalClaimAcceptedContext>)
                    Delegate.CreateDelegate(
                        typeof(Action<TerminalClaimAcceptedContext>),
                        controller,
                        typeof(CampaignGameplayFlowController).GetMethod(
                            "HandleTerminalClaimAccepted",
                            BindingFlags.Instance | BindingFlags.NonPublic));
                feed.TerminalClaimAccepted += acceptedHandler;
                var frames = new List<GameplayPresentationFrame>();
                feed.FramePublished += frames.Add;

                var thrown = Assert.Throws<InvalidOperationException>(
                    () => feed.ForceClearCurrentStage());

                Assert.That(thrown, Is.SameAs(setupException));
                Assert.That(TerminalSessionRegistry.IsActive, Is.False);
                Assert.That(ReadInputHostTerminalHold(host.InputHost), Is.False);
                Assert.That(feed.CurrentMinimalStageCompletion, Is.Not.Null);
                Assert.That(feed.HasPendingStageClearPresentation, Is.False);
                Assert.That(frames, Has.Count.EqualTo(1));
                Assert.That(frames[0].StageEvent.HasValue, Is.True);
                Assert.That(
                    frames[0].StageEvent.Value.EventKind,
                    Is.EqualTo(GameplayStageEventKind.Cleared));
                Assert.That(saveStore.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-1-2"));
                feed.TerminalClaimAccepted -= acceptedHandler;
                feed.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presentationDefinition);
                UnityEngine.Object.DestroyImmediate(entry);
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayInputHost_ExactTerminalHoldRelease_PreservesNewerOwner()
        {
            var hostObject = new GameObject("terminal-hold-newer-owner-host");
            try
            {
                var inputHost = hostObject.AddComponent<GameplayInputHost>();
                SetPrivateField(inputHost, "_isInitialized", true);
                var oldToken = new TerminalSessionToken(81, 1);
                var newerToken = new TerminalSessionToken(81, 2);

                inputHost.EnterTerminalHold(newerToken);

                Assert.That(inputHost.TryExitTerminalHold(oldToken), Is.False);
                Assert.That(ReadInputHostTerminalHold(inputHost), Is.True);
                Assert.That(inputHost.TryExitTerminalHold(newerToken), Is.True);
                Assert.That(ReadInputHostTerminalHold(inputHost), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CampaignDeath_ClaimedLevelFailed_IgnoresLaterStageClear()
        {
            var saveKey = CreatePrefsKey(nameof(CampaignDeath_ClaimedLevelFailed_IgnoresLaterStageClear));
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
                var terminalPort = new FakeTerminalTransitionPort();
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: terminalPort);
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

                terminalPort.Current.Advance(terminalPort.Current.Preset.BlackAt);
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
            var earningSink = new RecordingProductAchievementEarningSink(
                AchievementEarnResult.EarnedNew);

            try
            {
                SeedSaveSlot(saveStore, activeSlotProvider, "stage-2-2", "level-2", remainingChances: 2);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort(),
                    normalCampaignCompletionAchievementIntegration:
                        new NormalCampaignCompletionAchievementIntegration(earningSink));
                var handleTickCompleted = GetHandleTickCompletedMethod();
                var handleStageClearCommitted = GetHandleStageClearCommittedMethod();
                var deathAndClearTick = CreateDeathTickResult(
                    50,
                    eligibleTick: 53,
                    objectiveCleared: true);

                handleStageClearCommitted.Invoke(
                    controller,
                    new object[] { deathAndClearTick, CreateMinimalStageCompletionReadModel("stage-2-2", tickIndex: 50) });
                handleTickCompleted.Invoke(controller, new object[] { deathAndClearTick });

                var slot = saveStore.LoadSlot(1);
                Assert.That(slot.CurrentStageId.Value, Is.EqualTo("stage-2-2"));
                Assert.That(slot.RemainingChances, Is.EqualTo(1));
                Assert.That(slot.TotalDeaths, Is.EqualTo(1));
                Assert.That(slot.NormalCampaignCompletionReceipt, Is.Null);
                Assert.That(earningSink.EarnCount, Is.Zero);

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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
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
                    new CampaignRunningSlotContext(1),
                    CreateResolver(),
                    router,
                    chanceDisplayOverride: null,
                    terminalTransitionPort: new FakeTerminalTransitionPort());
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
                StageLaunchContextStore.SetCurrent(StageId.CreateOrThrow("stage-2-1"));

                var method = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                    "ValidateCommittedActiveSlotMatchesLaunchStage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(
                    installer,
                    new object[] { StageId.CreateOrThrow("stage-2-1") }));

                Assert.That(exception?.InnerException, Is.TypeOf<InvalidOperationException>());
                StringAssert.Contains("does not match resolved launch stage", exception?.InnerException?.Message);
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                StageLaunchContextStore.Clear();
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
            return CampaignStageSequenceTestAsset.LoadProductionResolver();
        }

        private static CampaignStageSequenceResolver CreateDivergentResolver(
            out CampaignStageSequenceDefinition definition)
        {
            definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            definition.SetEntries(new[]
            {
                CreateSequenceEntry("fixture-a", "group-a"),
                CreateSequenceEntry("fixture-c", "group-b"),
                CreateSequenceEntry("fixture-b", "group-b"),
            });
            return new CampaignStageSequenceResolver(definition);
        }

        private static CampaignStageSequenceEntry CreateSequenceEntry(
            string stageId,
            string levelGroupId)
        {
            var entry = new CampaignStageSequenceEntry();
            entry.Set(
                StageId.CreateOrThrow(stageId),
                levelGroupId);
            return entry;
        }

        private static StageClearResult CreateClearResult(string stageId)
        {
            return new StageClearResult(
                StageId.CreateOrThrow(stageId),
                finalTickIndex: 1);
        }

        private static MethodInfo GetHandleStageClearMethod()
        {
            var method = typeof(CampaignGameplayFlowController).GetMethod(
                "HandleStageClear",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static CampaignGameplayFlowController CreateReceiptController(
            GameObject hostObject,
            ICampaignSaveSlotStore store,
            int slotNumber,
            EditorDirectPlayContext? editorDirectPlayContext = null,
            INormalCampaignCompletionAchievementIntegration achievementIntegration = null,
            ICampaignStageAchievementIntegration stageAchievementIntegration = null)
        {
            var host = CreateHostWithInput(
                hostObject,
                playerEntityId: 10,
                respawnDelayTicks: 3);
            return new CampaignGameplayFlowController(
                host,
                store,
                new CampaignRunningSlotContext(slotNumber),
                CreateResolver(),
                new FakeStageLaunchRouter(),
                chanceDisplayOverride: null,
                terminalTransitionPort: new FakeTerminalTransitionPort(),
                editorDirectPlayContext: editorDirectPlayContext,
                normalCampaignCompletionAchievementIntegration: achievementIntegration,
                campaignStageAchievementIntegration: stageAchievementIntegration);
        }

        private static void InvokeStageClear(
            CampaignGameplayFlowController controller,
            MinimalStageCompletionReadModel readModel,
            bool hasObjectiveClear = true)
        {
            var method = typeof(CampaignGameplayFlowController).GetMethod(
                "HandleStageClear",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(
                controller,
                new object[]
                {
                    hasObjectiveClear
                        ? CreateObjectiveClearTickResult(readModel.FinalTickIndex)
                        : null,
                    readModel,
                });
        }

        private static SaveSlotData CreateCampaignSlot(
            int slotNumber,
            string stageId)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = "level-4",
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
            };
        }

        private static void AssertStageClearChancePolicy(
            string testKey,
            string completedStageId,
            string completedLevelGroupId,
            string expectedNextStageId,
            string expectedNextLevelGroupId,
            int expectedSavedChances,
            int expectedDisplayedChances,
            GameplayChanceAudioPolicy expectedAudioPolicy)
        {
            var saveKey = CreatePrefsKey(testKey);
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            var hostObject = new GameObject("campaign-clear-chance-policy-host");

            try
            {
                SeedSaveSlot(
                    saveStore,
                    activeSlotProvider,
                    completedStageId,
                    completedLevelGroupId,
                    remainingChances: 1);
                var host = CreateHostWithInput(hostObject, playerEntityId: 10, respawnDelayTicks: 3);
                var runningSlotContext = new CampaignRunningSlotContext(1);
                var chanceDisplayOverride = new CampaignChanceDisplayOverride();
                var chancesReadSource = new SaveSlotCampaignChancesReadSource(
                    saveStore,
                    runningSlotContext,
                    chanceDisplayOverride);
                var controller = new CampaignGameplayFlowController(
                    host,
                    saveStore,
                    runningSlotContext,
                    CreateResolver(),
                    new FakeStageLaunchRouter(),
                    chanceDisplayOverride,
                    new FakeTerminalTransitionPort());
                var method = typeof(CampaignGameplayFlowController).GetMethod(
                    "HandleStageClear",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);

                method.Invoke(
                    controller,
                    new object[]
                    {
                        null,
                        CreateMinimalStageCompletionReadModel(completedStageId, tickIndex: 10),
                    });

                var slot = saveStore.LoadSlot(1);
                Assert.That(slot.CurrentStageId.Value, Is.EqualTo(expectedNextStageId));
                Assert.That(slot.CurrentLevelGroupId, Is.EqualTo(expectedNextLevelGroupId));
                Assert.That(slot.RemainingChances, Is.EqualTo(expectedSavedChances));
                Assert.That(slot.NormalCampaignCompletionReceipt, Is.Null);
                Assert.That(
                    chancesReadSource.TryReadChances(
                        out var displayedChances,
                        out var maxChances,
                        out var audioPolicy),
                    Is.True);
                Assert.That(displayedChances, Is.EqualTo(expectedDisplayedChances));
                Assert.That(maxChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
                Assert.That(audioPolicy, Is.EqualTo(expectedAudioPolicy));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
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
                    null,
                    respawnDelayTicks));
            return host;
        }

        private static void AttachUiAccess(
            GameplaySceneHost host,
            GameplayTickViewPresenter presenter,
            GameplayHostUiAccessContext uiAccess,
            int respawnDelayTicks)
        {
            SetPrivateField(
                host,
                "_runtime",
                new GameplayHostRuntimeContext(
                    null,
                    null,
                    null,
                    host.InputHost,
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
                    uiAccess,
                    respawnDelayTicks));
        }

        private static void RaiseInputHostTickCompleted(
            GameplayInputHost inputHost,
            TickResult result)
        {
            var handlers = ReadPrivateField<Action<TickResult>>(inputHost, "TickCompleted");
            Assert.That(handlers, Is.Not.Null, "Production presentation feed must subscribe to TickCompleted.");
            handlers(result);
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

        private static TickResult CreateDeathTickResult(
            int tickIndex,
            int eligibleTick,
            bool objectiveCleared = false)
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

            return CreateTickResult(
                tickIndex,
                presentationData,
                Array.Empty<string>(),
                objectiveCleared
                    ? CreateClearedObjectiveResult()
                    : StageObjectiveTickResult.NoObjective);
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
            IEnumerable<string> eventLog,
            StageObjectiveTickResult objectiveResult = null)
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
                objectiveResult ?? StageObjectiveTickResult.NoObjective,
            });
        }

        private static MinimalStageCompletionReadModel CreateMinimalStageCompletionReadModel(
            string stageIdValue,
            int tickIndex)
        {
            var stageId = StageId.CreateOrThrow(stageIdValue);
            return new MinimalStageCompletionReadModel(
                stageId,
                tickIndex,
                new StageNavigationRequest(stageId, StageNavigationKind.Continue, "campaign-test-continue"),
                new StageNavigationRequest(stageId, StageNavigationKind.Retry, "campaign-test-retry"),
                StageNavigationRequest.None);
        }

        private static TickResult CreateObjectiveClearTickResult(int tickIndex)
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
                TickTrace.Empty,
                CreateClearedObjectiveResult());
        }

        private static TickResult CreateCountedPushFlipTickResult(
            int tickIndex,
            int actionSequence,
            PlayerActionKind actionKind,
            TickPlayerActionResolutionKind resolutionKind,
            bool objectiveCleared)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: actionKind,
                        activeActionSequence: actionSequence,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: resolutionKind),
                },
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>());

            return CreateTickResult(
                tickIndex,
                presentationData,
                Array.Empty<string>(),
                objectiveCleared
                    ? CreateClearedObjectiveResult()
                    : StageObjectiveTickResult.NoObjective);
        }

        private static StageObjectiveTickResult CreateClearedObjectiveResult()
        {
            return new StageObjectiveTickResult(
                hasObjective: true,
                goalReached: true,
                allConditionsSatisfied: true,
                clearedThisTick: true,
                isCleared: true,
                Array.Empty<StageConditionStatus>());
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

        private sealed class RecordingCampaignSaveSlotStore : ICampaignSaveSlotStore
        {
            private readonly SaveSlotData[] _slots =
            {
                SaveSlotData.CreateEmpty(1),
                SaveSlotData.CreateEmpty(2),
                SaveSlotData.CreateEmpty(3),
            };

            public RecordingCampaignSaveSlotStore(params SaveSlotData[] slots)
            {
                foreach (var slot in slots ?? Array.Empty<SaveSlotData>())
                {
                    if (slot != null && SaveSlotStore.IsValidSlotNumber(slot.SlotNumber))
                    {
                        _slots[slot.SlotNumber - 1] = slot.Clone();
                    }
                }
            }

            public string DiagnosticsKey => nameof(RecordingCampaignSaveSlotStore);

            public CampaignSaveLoadReport LastCampaignLoadReport { get; private set; } =
                CampaignSaveLoadReport.Loaded(
                    "Recording store initialized.",
                    nameof(RecordingCampaignSaveSlotStore));

            public int UpdateCount { get; private set; }

            public int SaveCount { get; private set; }

            public int LastUpdatedSlotNumber { get; private set; }

            public bool LastMutationObservedCampaignCompleted { get; private set; }

            public bool LastMutationObservedReceipt { get; private set; }

            public bool ThrowOnUpdate { get; set; }

            public static RecordingCampaignSaveSlotStore WithSlot(SaveSlotData slot)
            {
                return new RecordingCampaignSaveSlotStore(slot);
            }

            public SaveSlotData[] LoadAll()
            {
                return _slots.Select(slot => slot.Clone()).ToArray();
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                return new CampaignSaveLoadResult(LoadAll(), LastCampaignLoadReport);
            }

            public SaveSlotData LoadSlot(int slotNumber)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                return _slots[slotNumber - 1].Clone();
            }

            public void SaveSlot(SaveSlotData slot)
            {
                if (slot == null)
                {
                    throw new ArgumentNullException(nameof(slot));
                }

                SaveSlotStore.ThrowIfInvalidSlotNumber(slot.SlotNumber);
                _slots[slot.SlotNumber - 1] = slot.Clone();
                SaveCount++;
            }

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt)
            {
                var slot = SaveSlotData.CreateNewGame(
                    slotNumber,
                    sequenceResolver,
                    lastPlayedAt);
                SaveSlot(slot);
                return slot.Clone();
            }

            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                if (mutation == null)
                {
                    throw new ArgumentNullException(nameof(mutation));
                }

                UpdateCount++;
                LastUpdatedSlotNumber = slotNumber;
                var candidate = LoadSlot(slotNumber);
                mutation(candidate);
                LastMutationObservedCampaignCompleted = candidate.CampaignCompleted;
                LastMutationObservedReceipt =
                    candidate.NormalCampaignCompletionReceipt != null;
                if (ThrowOnUpdate)
                {
                    throw new IOException("Simulated atomic save failure.");
                }

                SaveSlot(candidate);
            }

            public void DeleteSlot(int slotNumber)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                _slots[slotNumber - 1] = SaveSlotData.CreateEmpty(slotNumber);
            }

            public void ClearAll()
            {
                for (var i = 0; i < _slots.Length; i++)
                {
                    _slots[i] = SaveSlotData.CreateEmpty(i + 1);
                }
            }
        }

        private sealed class RecordingProductAchievementEarningSink :
            IProductAchievementEarningSink
        {
            private readonly AchievementEarnResult _result;
            private readonly Func<bool> _saveCommittedProbe;
            private readonly Exception _exception;

            public RecordingProductAchievementEarningSink(
                AchievementEarnResult result,
                Func<bool> saveCommittedProbe = null,
                Exception exception = null)
            {
                _result = result;
                _saveCommittedProbe = saveCommittedProbe;
                _exception = exception;
            }

            public int EarnCount { get; private set; }

            public GameAchievementId LastAchievementId { get; private set; }

            public bool SaveWasCommittedAtEarn { get; private set; }

            public List<GameAchievementId> EarnedAchievementIds { get; } = new();

            public AchievementEarnResult Earn(GameAchievementId achievementId)
            {
                EarnCount++;
                LastAchievementId = achievementId;
                EarnedAchievementIds.Add(achievementId);
                SaveWasCommittedAtEarn = _saveCommittedProbe?.Invoke() ?? true;
                if (_exception != null)
                {
                    throw _exception;
                }

                return _result;
            }
        }

        private sealed class NoOpGameplayCommandGateway : IGameplayCommandGateway
        {
            public GameplayCommandAcceptance SetHeldMoveDirection(GameplayUiDirection direction)
            {
                return GameplayCommandAcceptance.Accept();
            }

            public GameplayCommandAcceptance ClearHeldMoveDirection()
            {
                return GameplayCommandAcceptance.Accept();
            }
        }

        private sealed class NoOpGameplayQueryFacade : IGameplayQueryFacade
        {
            public IGameplaySessionQuery Session => null;

            public IGameplayStageQuery Stage => null;

            public IGameplayPlayerHudQuery PlayerHud => null;

            public IGameplayObjectiveQuery Objectives => null;

            public IGameplaySurfaceButtonRemainderQuery SurfaceButtonRemainders => null;
        }

        private sealed class NoOpGameplayPauseService : IGameplayPauseService
        {
            public event Action<bool> PauseChanged;

            public bool IsPaused { get; private set; }

            public void Pause()
            {
                SetPaused(true);
            }

            public void Resume()
            {
                SetPaused(false);
            }

            public void Toggle()
            {
                SetPaused(!IsPaused);
            }

            private void SetPaused(bool isPaused)
            {
                if (IsPaused == isPaused)
                {
                    return;
                }

                IsPaused = isPaused;
                PauseChanged?.Invoke(IsPaused);
            }
        }

        private sealed class ThrowingAchievementIntegration :
            INormalCampaignCompletionAchievementIntegration
        {
            public NormalCampaignCompletionAchievementResult TryEarnAfterCommittedCompletion(
                NormalCampaignCompletionFact completion,
                CampaignStageSequenceResolver sequenceResolver,
                SaveSlotData committedSlot)
            {
                throw new InvalidOperationException("simulated integration failure");
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

        [Test]
        [Category("Core")]
        public void DefeatCameraHandoff_PreservesLethalDuringOpenHoldAndResetsExactlyOnceAtIrisClosing()
        {
            var preset = TerminalIrisTestPresetFactory.CreateDefeat();
            var playback = new TerminalTransitionPlayback(preset);
            var request = new TerminalTransitionRequest(
                TerminalTransitionKind.Defeat,
                focusEntityId: 10,
                claimId: 72,
                destinationMode: TerminalTransitionDestinationMode.SceneHandoff);
            Assert.That(
                playback.TryBegin(
                    request,
                    new TerminalFocusTarget(Vector2.one * 0.5f, 0.12f, false)),
                Is.True);
            var resetCount = 0;

            CampaignGameplayFlowController.BindDefeatCameraHandoff(
                playback,
                request.Token,
                () => resetCount++);

            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Focusing));
            Assert.That(resetCount, Is.Zero);
            playback.Advance(preset.FocusDuration);
            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Holding));
            Assert.That(resetCount, Is.Zero);
            playback.Advance(preset.HoldDuration);
            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Closing));
            Assert.That(resetCount, Is.EqualTo(1));
            playback.Advance(preset.CloseDuration);
            Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Black));
            Assert.That(resetCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void DefeatCameraHandoff_MissingPlaybackResetsImmediately()
        {
            var resetCount = 0;
            CampaignGameplayFlowController.BindDefeatCameraHandoff(
                playback: null,
                token: default,
                completeHandoff: () => resetCount++);

            Assert.That(resetCount, Is.EqualTo(1));
        }

        private sealed class FakeTerminalTransitionPort : ITerminalTransitionPort
        {
            private readonly Exception _setupException;

            internal FakeTerminalTransitionPort(Exception setupException = null)
            {
                _setupException = setupException;
            }

            public TerminalTransitionPlayback Current { get; private set; }

            public bool TryBegin(
                TerminalTransitionRequest request,
                out TerminalTransitionPlayback playback)
            {
                if (_setupException != null)
                {
                    var authority = TerminalSessionRegistry.Authority;
                    if (authority.IsActive &&
                        authority.ActiveToken == request.Token &&
                        authority.Phase == TerminalSessionPhase.Claimed)
                    {
                        authority.TryAdvancePhase(request.Token, TerminalSessionPhase.Iris);
                    }

                    authority.TryAbortIrisSetup(
                        request.Token,
                        new TerminalFailure("TestIrisSetup", _setupException.Message));
                    playback = null;
                    throw _setupException;
                }

                if (Current != null && !Current.IsTerminal)
                {
                    playback = Current;
                    return false;
                }

                var preset = request.Kind == TerminalTransitionKind.Victory
                    ? TerminalIrisTestPresetFactory.CreateVictory()
                    : TerminalIrisTestPresetFactory.CreateDefeat();
                Current = new TerminalTransitionPlayback(preset);
                Current.TryBegin(
                    request,
                    new TerminalFocusTarget(
                        new Vector2(0.5f, 0.5f),
                        0.15f,
                        isFallback: false));
                playback = Current;
                return true;
            }
        }

        private static class TerminalIrisTestPresetFactory
        {
            internal static TerminalIrisRuntimePreset CreateVictory()
            {
                return new TerminalIrisRuntimePreset(
                    0.24f,
                    0.34f,
                    0.42f,
                    new Vector2(0.5f, 0.5f),
                    0.22f,
                    0.12f,
                    0.035f,
                    4f,
                    CreateEdge(new Color(1f, 0.84f, 0.28f, 0f)),
                    TerminalIrisEasing.Linear,
                    TerminalIrisEasing.Linear);
            }

            internal static TerminalIrisRuntimePreset CreateDefeat()
            {
                var reveal = new TerminalIrisRuntimeOpenPreset(
                    0f,
                    0.3f,
                    0.01f,
                    4f,
                    CreateEdge(new Color(0.86f, 0.12f, 0.1f, 0f)),
                    TerminalIrisEasing.Linear);
                return new TerminalIrisRuntimePreset(
                    0.18f,
                    0.2f,
                    0.32f,
                    new Vector2(0.5f, 0.5f),
                    0.2f,
                    0.11f,
                    0.035f,
                    4f,
                    CreateEdge(new Color(0.86f, 0.12f, 0.1f, 0f)),
                    TerminalIrisEasing.Linear,
                    TerminalIrisEasing.Linear,
                    reveal);
            }

            private static TerminalIrisRuntimeEdgeSettings CreateEdge(Color rimColor)
            {
                return new TerminalIrisRuntimeEdgeSettings(
                    0.82f,
                    0.82f,
                    1f,
                    0f,
                    0.85f,
                    3f,
                    rimColor);
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
