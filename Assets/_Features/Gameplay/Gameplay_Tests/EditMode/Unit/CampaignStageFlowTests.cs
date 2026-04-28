using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Shared.AudioContracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class CampaignStageFlowTests
    {
        [Test]
        [Category("Extended")]
        public void SequenceResolver_UsesCanonicalOrderDisplayNamesAndLevelGroups()
        {
            var resolver = CreateResolver();

            Assert.That(resolver.FirstStageId.Value, Is.EqualTo("stage-0-1"));
            Assert.That(resolver.FinalStageId.Value, Is.EqualTo("stage-5-1"));
            Assert.That(resolver.IsFinal(StageId.CreateOrThrow("stage-5-1")), Is.True);
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
            Assert.That(dto.SaveVersion, Is.EqualTo(SaveSlotStore.SaveVersion));
        }

        [Test]
        [Category("Extended")]
        public void SaveSlotProfileDto_RoundTripsCompletionSnapshot()
        {
            var stageId = StageId.CreateOrThrow("stage-3-1");
            var snapshot = new StageCompletionProfileSnapshot
            {
                Version = 7,
            };
            snapshot.InventoryBalances["coin"] = 12;
            snapshot.ProgressByStageId[stageId] = new PlayerStageProgress
            {
                StageId = stageId,
                HasCleared = true,
                ClearCount = 2,
                BestScore = 900,
                CompletedChallengeIds = new[] { "challenge-a" },
            };
            snapshot.ProcessedStageRunIds.Add("run-a");
            snapshot.ProcessedCompletionAttemptIds.Add("attempt-a");
            snapshot.AppliedRewardGrantIds.Add("reward-a");

            var roundTripped = SaveSlotDtoMapper.FromDto(SaveSlotDtoMapper.ToDto(snapshot));

            Assert.That(roundTripped.Version, Is.EqualTo(7));
            Assert.That(roundTripped.InventoryBalances["coin"], Is.EqualTo(12));
            Assert.That(roundTripped.ProgressByStageId[stageId].ClearCount, Is.EqualTo(2));
            Assert.That(roundTripped.ProcessedStageRunIds, Does.Contain("run-a"));
            Assert.That(roundTripped.ProcessedCompletionAttemptIds, Does.Contain("attempt-a"));
            Assert.That(roundTripped.AppliedRewardGrantIds, Does.Contain("reward-a"));
        }

        [Test]
        [Category("Extended")]
        public void ActiveSlotCompletionProfileStore_CommitsOnlyActiveSlot()
        {
            var saveKey = CreatePrefsKey(nameof(ActiveSlotCompletionProfileStore_CommitsOnlyActiveSlot));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData { SlotNumber = 1, CurrentStageId = StageId.CreateOrThrow("stage-1-1") });
            saveStore.SaveSlot(new SaveSlotData { SlotNumber = 2, CurrentStageId = StageId.CreateOrThrow("stage-2-1") });
            activeSlotProvider.SetActiveSlot(2);

            var profileStore = new SaveSlotStageCompletionProfileStore(saveStore, activeSlotProvider);
            var snapshot = new StageCompletionProfileSnapshot();
            snapshot.ProgressByStageId[StageId.CreateOrThrow("stage-2-1")] =
                new PlayerStageProgress
                {
                    StageId = StageId.CreateOrThrow("stage-2-1"),
                    HasCleared = true,
                    ClearCount = 1,
                };

            profileStore.Save(snapshot);

            Assert.That(saveStore.LoadSlot(1).StageCompletionProfileSnapshot.ProgressByStageId, Is.Empty);
            Assert.That(
                saveStore.LoadSlot(2).StageCompletionProfileSnapshot.ProgressByStageId.ContainsKey(StageId.CreateOrThrow("stage-2-1")),
                Is.True);
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
                    StageId.CreateOrThrow("stage-5-1"),
                    StageNavigationRequest.None,
                    campaignCompleted: true));
            Assert.That(CampaignStageResultNavigationStore.TryGet(StageId.CreateOrThrow("stage-5-1"), out var finalPlan), Is.True);
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
                "_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"));
            var gateIndex = source.IndexOf("if (!allowRespawn)", StringComparison.Ordinal);
            var spawnIndex = source.IndexOf("writeContext.SpawnEntity(respawnEntity)", StringComparison.Ordinal);

            Assert.That(gateIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(source, Does.Contain("RespawnSuppressed|E="));
            Assert.That(gateIndex, Is.LessThan(spawnIndex));
        }

        [Test]
        [Category("Extended")]
        public void StagePresentationRuntimeAdapter_BgmNoOpValidAndUnknownPolicies()
        {
            var coordinator = new FakeBgmFlowCoordinator();
            var adapter = new StagePresentationRuntimeAdapter(coordinator);
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var catalog = CreateBgmCatalog("valid", out var profile);

            adapter.Apply(presentation, null, catalog);
            Assert.That(coordinator.RequestCount, Is.EqualTo(0));

            SetPrivateField(presentation, "bgmReference", new StageBgmReference("valid"));
            adapter.Apply(presentation, null, catalog);
            Assert.That(coordinator.RequestCount, Is.EqualTo(1));
            Assert.That(coordinator.LastProfile, Is.SameAs(profile));

            SetPrivateField(presentation, "bgmReference", new StageBgmReference("unknown"));
            LogAssert.Expect(LogType.Warning, "Stage BGM key 'unknown' was not found in the stage BGM profile catalog.");
            adapter.Apply(presentation, null, catalog);
            Assert.That(coordinator.RequestCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Full")]
        public void StagePresentationRuntimeAdapter_InstantiatesAndReplacesBackgroundPrefab()
        {
            var root = new GameObject("background-root");
            var firstPrefab = new GameObject("first-background");
            var secondPrefab = new GameObject("second-background");
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var adapter = new StagePresentationRuntimeAdapter();

            try
            {
                SetPrivateField(presentation, "backgroundPrefab", firstPrefab);
                adapter.Apply(presentation, root.transform, null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(adapter.CurrentBackgroundInstance.name, Is.EqualTo("first-background"));

                SetPrivateField(presentation, "backgroundPrefab", secondPrefab);
                adapter.Apply(presentation, root.transform, null);
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
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                SetPrivateField(installer, "_saveSlotStore", saveStore);
                SetPrivateField(installer, "_activeSlotProvider", activeSlotProvider);

                var method = typeof(StageBackedGameplayShowcaseInstallerBase).GetMethod(
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

        private static StageBgmProfileCatalog CreateBgmCatalog(string key, out BgmProfile profile)
        {
            profile = ScriptableObject.CreateInstance<BgmProfile>();
            var entry = new StageBgmProfileCatalogEntry();
            SetPrivateField(entry, "key", key);
            SetPrivateField(entry, "profile", profile);
            var catalog = ScriptableObject.CreateInstance<StageBgmProfileCatalog>();
            SetPrivateField(catalog, "entries", new[] { entry });
            return catalog;
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

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
        }

        private sealed class FakeBgmFlowCoordinator : IBgmFlowCoordinator
        {
            public int RequestCount { get; private set; }

            public BgmProfile LastProfile { get; private set; }

            public void RequestSceneDefault(BgmProfile profile)
            {
                RequestCount++;
                LastProfile = profile;
            }

            public void StopCurrent()
            {
            }

            public BgmProfile GetCurrentProfile()
            {
                return LastProfile;
            }
        }

        private sealed class FakeStageLaunchRouter : IStageLaunchRouter
        {
            public StageNavigationRequest LastRequest { get; private set; } = StageNavigationRequest.None;

            public void Launch(StageNavigationRequest request)
            {
                LastRequest = request;
            }
        }
    }
}
