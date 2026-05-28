using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class DemoStageControlTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private string _activeSlotKey;
        private string _saveSlotKey;

        [SetUp]
        public void SetUp()
        {
            _saveSlotKey = $"{nameof(DemoStageControlTests)}.Save.{Guid.NewGuid():N}";
            _activeSlotKey = $"{nameof(DemoStageControlTests)}.Active.{Guid.NewGuid():N}";
            StageLaunchContextStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            if (!string.IsNullOrWhiteSpace(_saveSlotKey))
            {
                PlayerPrefs.DeleteKey(_saveSlotKey);
            }

            if (!string.IsNullOrWhiteSpace(_activeSlotKey))
            {
                PlayerPrefs.DeleteKey(_activeSlotKey);
            }

            for (var i = 0; i < _createdObjects.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void DemoStageControl_StartStage_RejectsUnknownStageId()
        {
            var first = CreateEntry("stage-0-1");
            var service = CreateService(new[] { first }, out _, out _);

            var result = service.StartStage(StageId.CreateOrThrow("stage-0-2"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("not found"));
        }

        [Test]
        public void DemoStageControl_StartStage_UpdatesCampaignActiveStage_AndWritesLaunchContext()
        {
            var first = CreateEntry("stage-0-1");
            var selected = CreateEntry("stage-0-2");
            var service = CreateService(new[] { first, selected }, out var saveStore, out var router);

            var result = service.StartStage(selected.StageId);

            Assert.That(result.Success, Is.True);
            Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(selected.StageId));
            Assert.That(StageLaunchContextStore.TryGetCurrent(out var launchStageId), Is.True);
            Assert.That(launchStageId, Is.EqualTo(selected.StageId));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
            Assert.That(router.Requests[0].StageId, Is.EqualTo(selected.StageId));
        }

        [Test]
        public void DemoStageControl_StartStage_AllowsLockedStageSelection_AndDoesNotCompletePreviousStages()
        {
            var first = CreateEntry("stage-0-1");
            var selected = CreateEntry("stage-0-2", CreateLockedProgressionDefinition());
            var service = CreateService(new[] { first, selected }, out var saveStore, out _);
            saveStore.UpdateSlot(
                1,
                slot => slot.StageCompletionProfileSnapshot.ProgressByStageId[first.StageId] =
                    PlayerStageProgress.CreateEmpty(first.StageId));

            var result = service.StartStage(selected.StageId);

            Assert.That(result.Success, Is.True);
            Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(selected.StageId));
            var previousProgress = saveStore.LoadSlot(1).StageCompletionProfileSnapshot.ProgressByStageId[first.StageId];
            Assert.That(previousProgress.HasCleared, Is.False);
        }

        [Test]
        public void DemoStageControl_StartStage_FailsWhenSceneTransitionInProgress()
        {
            var first = CreateEntry("stage-0-1");
            var router = new RecordingStageLaunchRouter();
            var service = CreateService(
                new[] { first },
                out _,
                out _,
                launchBridge: new DemoStageControlLaunchBridge(router, () => true));

            var result = service.StartStage(first.StageId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("transition"));
            Assert.That(router.Requests, Is.Empty);
        }

        [Test]
        public void DemoStageControl_ForceClear_EmitsTerminalClearOnce()
        {
            var tracker = new StageSessionTracker();
            tracker.Start(StageId.CreateOrThrow("stage-0-1"));

            Assert.That(tracker.TryEmitForcedClear(out var result), Is.True);
            Assert.That(result.WasCleared, Is.True);
            Assert.That(result.ClearSource, Is.EqualTo(StageClearSource.ForcedByDemoStageControl));
            Assert.That(tracker.TryEmitForcedClear(out _), Is.False);
        }

        [Test]
        public void DemoStageControl_ForceClear_UsesNormalCompletionPipeline_AndCommitsRewardAndProgress()
        {
            var entry = CreateEntry("stage-0-1", rewardDefinition: CreateRewardDefinition());
            var store = new InMemoryStageCompletionProfileStore();
            var runtime = new GameplayHostStageCompletionRuntime(entry, store);

            var readModel = runtime.ForceClearCurrentStage();

            Assert.That(readModel.ClearResult.WasCleared, Is.True);
            Assert.That(readModel.ClearResult.ClearSource, Is.EqualTo(StageClearSource.ForcedByDemoStageControl));
            Assert.That(readModel.RewardGrantResult.AnyGranted, Is.True);
            Assert.That(store.Snapshot.ProgressByStageId[entry.StageId].HasCleared, Is.True);
            Assert.That(store.Snapshot.ProgressByStageId[entry.StageId].ClearCount, Is.EqualTo(1));
            Assert.That(store.Snapshot.InventoryBalances["coin"], Is.EqualTo(2));
        }

        [Test]
        public void DemoStageControl_ServiceDoesNotReferenceLegacyDeveloperCommandsOrCompletionCommitter()
        {
            var source = File.ReadAllText("Assets/_Features/DemoStageControl/Runtime/DemoStageControlService.cs");
            var removedCommandPrefix = "Debug" + "Command";

            Assert.That(source, Does.Not.Contain(removedCommandPrefix));
            Assert.That(source, Does.Not.Contain("StageCompletionCommitter"));
            Assert.That(source, Does.Not.Contain("ObjectiveTracker"));
            Assert.That(source, Does.Not.Contain("SceneManager.LoadScene"));
        }

        [Test]
        public void DemoGameplayOverride_SetPlayerInvincible_UpdatesRuntimeOnlySnapshot()
        {
            var runtime = new DemoGameplayOverrideRuntime(DemoStageControlSettings.EnabledByDefault());

            var result = runtime.SetPlayerInvincible(true);

            Assert.That(result.Success, Is.True);
            Assert.That(runtime.GetSnapshot().PlayerInvincible, Is.True);
            Assert.That(runtime.GetOverrideStatus().LastOverrideMessage, Is.EqualTo("Player Invincible ON"));
        }

        [Test]
        public void DemoGameplayOverride_SettingsDisabled_RejectsPlayerInvincibleToggle()
        {
            var runtime = new DemoGameplayOverrideRuntime(new DemoStageControlSettings
            {
                Enabled = false,
            });

            var result = runtime.TogglePlayerInvincible();

            Assert.That(result.Success, Is.False);
            Assert.That(runtime.GetSnapshot().PlayerInvincible, Is.False);
        }

        [Test]
        public void DemoGameplayOverride_DoesNotUseLegacyDeveloperCommands()
        {
            var sources = string.Join(
                Environment.NewLine,
                File.ReadAllText("Assets/_Features/DemoStageControl/Runtime/DemoGameplayOverrides.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelModels.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelRuntime.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelView.cs"));
            var removedPopupName = "Debug" + "Commands";
            var removedAccessName = "Debug" + "CommandAccess";
            var removedBuildGateName = "Debug" + "CommandBuildGate";

            Assert.That(sources, Does.Not.Contain(removedPopupName));
            Assert.That(sources, Does.Not.Contain(removedAccessName));
            Assert.That(sources, Does.Not.Contain(removedBuildGateName));
        }

        [Test]
        public void DemoGameplayOverride_DoesNotModifySaveSchema()
        {
            var source = File.ReadAllText("Assets/_Features/DemoStageControl/Runtime/DemoGameplayOverrides.cs");

            Assert.That(source, Does.Not.Contain("SaveSlotData"));
            Assert.That(source, Does.Not.Contain("SaveSlotStore"));
            Assert.That(source, Does.Not.Contain("StageCompletionProfileSnapshot"));
        }

        [Test]
        public void DemoGameplayOverride_NoDirectHpMutationFromUI()
        {
            var sources = string.Join(
                Environment.NewLine,
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelModels.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelRuntime.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelView.cs"));

            Assert.That(sources, Does.Not.Contain(".hp"));
            Assert.That(sources, Does.Not.Contain("ApplyDamage"));
            Assert.That(sources, Does.Not.Contain("SetPlayerHp"));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_DoesNotDependOnEnemyProfileNames()
        {
            var sources = string.Join(
                Environment.NewLine,
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"),
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/AttackPhaseResult.cs"),
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerDamageState.cs"));

            Assert.That(sources, Does.Not.Contain("JPeter"));
            Assert.That(sources, Does.Not.Contain("Nebulous"));
            Assert.That(sources, Does.Not.Contain("Nebuolus"));
            Assert.That(sources, Does.Not.Contain("DrSaturn"));
            Assert.That(sources, Does.Not.Contain("Saturn"));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_DoesNotImplementEnemiesIgnorePlayer()
        {
            var sources = string.Join(
                Environment.NewLine,
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"),
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs"),
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyTargetSelector.cs"));

            Assert.That(sources, Does.Not.Contain("EnemiesIgnorePlayer"));
            Assert.That(sources, Does.Not.Contain("IgnorePlayer"));
        }

        [Test]
        [Category("Core")]
        public void AudioRuntime_DoesNotAddGameplaySpecificSuppressRules()
        {
            var sources = string.Join(
                Environment.NewLine,
                File.ReadAllText("Assets/_Shared/Audio/Runtime/AudioPlaybackService.cs"),
                File.ReadAllText("Assets/_Shared/Audio/Runtime/AudioManager.cs"));

            Assert.That(sources, Does.Not.Contain("PlayerInvincible"));
            Assert.That(sources, Does.Not.Contain("DamageRejectReason"));
            Assert.That(sources, Does.Not.Contain("PassiveContact"));
        }

        private DemoStageControlService CreateService(
            IReadOnlyList<StageContentEntry> entries,
            out SaveSlotStore saveStore,
            out RecordingStageLaunchRouter router,
            IDemoStageControlLaunchBridge launchBridge = null)
        {
            var provider = new TestStageCatalogProvider(entries);
            saveStore = new SaveSlotStore(_saveSlotKey);
            var activeSlotProvider = new ActiveSlotProvider(_activeSlotKey);
            activeSlotProvider.SetActiveSlot(1);
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = entries[0].StageId,
                CurrentLevelGroupId = "level-0",
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                StageCompletionProfileSnapshot = new StageCompletionProfileSnapshot(),
            });
            var campaignBridge = new DemoStageControlCampaignBridge(
                saveStore,
                activeSlotProvider,
                new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()));
            router = new RecordingStageLaunchRouter();
            return new DemoStageControlService(
                DemoStageControlSettings.EnabledByDefault(),
                provider,
                campaignBridge,
                launchBridge ?? new DemoStageControlLaunchBridge(router, () => false),
                new RecordingCompletionBridge());
        }

        private StageContentEntry CreateEntry(
            string rawStageId,
            StageProgressionDefinition progressionDefinition = null,
            StageRewardDefinition rewardDefinition = null)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            _createdObjects.Add(entry);
            entry.AssignStageId(StageId.CreateOrThrow(rawStageId));
            entry.AssignProgressionDefinition(progressionDefinition);
            entry.AssignRewardDefinition(rewardDefinition);
            return entry;
        }

        private StageProgressionDefinition CreateLockedProgressionDefinition()
        {
            var definition = ScriptableObject.CreateInstance<StageProgressionDefinition>();
            _createdObjects.Add(definition);
            return definition;
        }

        private StageRewardDefinition CreateRewardDefinition()
        {
            var definition = ScriptableObject.CreateInstance<StageRewardDefinition>();
            _createdObjects.Add(definition);
            var rule = new StageRewardRuleDefinition
            {
                TriggerKind = StageRewardTriggerKind.Clear,
                GrantOnce = true,
                Rewards = new[]
                {
                    new RewardEntry
                    {
                        RewardId = "coin",
                        Amount = 2,
                    },
                },
            };
            rule.SetRuleId("first-clear");
            rule.SetDeprecatedRuleIds(Array.Empty<string>());
            SetPrivateField(definition, "rules", new[] { rule });
            return definition;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class TestStageCatalogProvider : IStageCatalogProvider
        {
            private readonly IReadOnlyList<StageContentEntry> _entries;

            public TestStageCatalogProvider(IReadOnlyList<StageContentEntry> entries)
            {
                _entries = entries;
            }

            public IReadOnlyList<StageContentEntry> LoadEntries()
            {
                return _entries;
            }

            public StageIdAliasTable AliasTable => null;
        }

        private sealed class RecordingStageLaunchRouter : IStageLaunchRouter
        {
            private readonly List<StageNavigationRequest> _requests = new();

            public IReadOnlyList<StageNavigationRequest> Requests => _requests;

            public void Launch(StageNavigationRequest request)
            {
                _requests.Add(request);
            }
        }

        private sealed class RecordingCompletionBridge : IDemoStageControlCompletionBridge
        {
            public bool IsCompletionInProgress { get; set; }

            public DemoStageControlResult ForceClearCurrentStage()
            {
                return DemoStageControlResult.Ok("forced");
            }
        }

        private sealed class InMemoryStageCompletionProfileStore : IStageCompletionProfileStore
        {
            public StageCompletionProfileSnapshot Snapshot { get; private set; } = new();

            public StageCompletionProfileSnapshot Load()
            {
                return Snapshot.Clone();
            }

            public void Save(StageCompletionProfileSnapshot snapshot)
            {
                Snapshot = snapshot?.Clone() ?? new StageCompletionProfileSnapshot();
            }
        }
    }
}
