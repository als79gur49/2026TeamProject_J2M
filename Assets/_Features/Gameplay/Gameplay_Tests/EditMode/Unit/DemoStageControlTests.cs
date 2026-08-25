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
        private string _activeSlotNamespace;
        private string _saveSlotNamespace;

        [SetUp]
        public void SetUp()
        {
            _saveSlotNamespace = $"{nameof(DemoStageControlTests)}.Save.{Guid.NewGuid():N}";
            _activeSlotNamespace = $"{nameof(DemoStageControlTests)}.Active.{Guid.NewGuid():N}";
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            if (!string.IsNullOrWhiteSpace(_saveSlotNamespace))
            {
                new TransientCampaignSaveSlotStore(_saveSlotNamespace).ClearAll();
            }

            if (!string.IsNullOrWhiteSpace(_activeSlotNamespace))
            {
                new TransientActiveSlotStorage(_activeSlotNamespace).ClearActiveSlot();
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
            var selected = CreateEntry("stage-1-1");
            var service = CreateService(new[] { first, selected }, out var saveStore, out var router);
            var directPlayContext = EditorDirectPlayContext.CreateCampaignTempSlot(
                first.StageId,
                CampaignSaveSlotPolicy.DefaultRemainingChances);
            EditorDirectPlayContextStore.SetCurrent(directPlayContext);

            var result = service.StartStage(selected.StageId);

            Assert.That(result.Success, Is.True);
            Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(selected.StageId));
            Assert.That(saveStore.LoadSlot(1).CurrentLevelGroupId, Is.EqualTo("level-1"));
            Assert.That(
                StageLaunchContextStore.TryGetCurrent(out _),
                Is.False,
                "The production router, not the Demo bridge, owns pending-less reload context registration.");
            Assert.That(router.Requests, Has.Count.EqualTo(1));
            Assert.That(router.Requests[0].StageId, Is.EqualTo(selected.StageId));
            Assert.That(
                router.Requests[0].TransitionIntent,
                Is.EqualTo(SceneTransitionIntent.DemoStageRelaunch));
            Assert.That(
                router.Requests[0].EditorDirectPlayContext,
                Is.EqualTo(directPlayContext.ForStage(selected.StageId)));
        }

        [Test]
        public void DemoStageControl_StartStage_AllowsLockedStageSelection_AndDoesNotCompletePreviousStages()
        {
            var first = CreateEntry("stage-0-1");
            var selected = CreateEntry("stage-0-2", initiallyAvailable: false);
            var service = CreateService(new[] { first, selected }, out var saveStore, out _);

            var result = service.StartStage(selected.StageId);

            Assert.That(result.Success, Is.True);
            Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(selected.StageId));
            Assert.That(saveStore.LoadSlot(1).State.StageClearProfile.Records, Is.Empty);
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
        [Category("Core")]
        public void DemoStageControl_GetStages_UsesCampaignSequenceOrder_AndExcludesCatalogExtras()
        {
            var first = CreateEntry("stage-0-1");
            var second = CreateEntry("stage-0-2", initiallyAvailable: false);
            var extra = CreateEntry("catalog-stage-b");
            var service = CreateService(new[] { second, extra, first }, out _, out _);

            var stages = service.GetStages();

            Assert.That(stages, Has.Count.EqualTo(2));
            Assert.That(stages[0].StageId, Is.EqualTo(first.StageId));
            Assert.That(stages[0].DisplayNameKey, Is.EqualTo("stage.stage-0-1.display_name"));
            Assert.That(stages[0].IsUnlocked, Is.True);
            Assert.That(stages[1].StageId, Is.EqualTo(second.StageId));
            Assert.That(stages[1].DisplayNameKey, Is.EqualTo("stage.stage-0-2.display_name"));
            Assert.That(stages[1].IsUnlocked, Is.False);
        }

        [Test]
        [Category("Core")]
        public void DemoStageControl_StartStage_RejectsCatalogStageOutsideCampaignSequence()
        {
            var first = CreateEntry("stage-0-1");
            var extra = CreateEntry("catalog-stage-b");
            var service = CreateService(new[] { first, extra }, out var saveStore, out var router);

            var result = service.StartStage(extra.StageId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("not part of the campaign sequence"));
            Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(first.StageId));
            Assert.That(router.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void DemoStageControlCampaignBridge_TrySetActiveStage_RejectsStageOutsideSequence()
        {
            var first = CreateEntry("stage-0-1");
            var extra = CreateEntry("catalog-stage-b");
            var saveStore = new TransientCampaignSaveSlotStore(_saveSlotNamespace);
            var activeSlotProvider = new ActiveSlotProvider(new TransientActiveSlotStorage(_activeSlotNamespace));
            activeSlotProvider.SetActiveSlot(1);
            saveStore.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                1,
                first.StageId,
                "level-0",
                CampaignSaveSlotPolicy.DefaultRemainingChances,
                string.Empty));
            var bridge = new DemoStageControlCampaignBridge(
                saveStore,
                saveStore,
                activeSlotProvider,
                LoadProductionSequenceResolver());

            var result = bridge.TrySetActiveStage(extra, out var message);

            Assert.That(result, Is.False);
            Assert.That(message, Does.Contain("not part of the campaign sequence"));
            Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(first.StageId));
        }

        [Test]
        public void DemoStageControl_ForceClear_EmitsTerminalClearOnce()
        {
            var tracker = new StageSessionTracker();
            tracker.Start(StageId.CreateOrThrow("stage-0-1"));

            Assert.That(tracker.TryEmitForcedClear(out var result), Is.True);
            Assert.That(result.StageId, Is.EqualTo(StageId.CreateOrThrow("stage-0-1")));
            Assert.That(result.FinalTickIndex, Is.Zero);
            Assert.That(tracker.CurrentState.IsTerminal, Is.True);
            Assert.That(tracker.CurrentState.TerminalReason, Is.EqualTo(StageTerminalReason.Cleared));
            Assert.That(tracker.TryEmitForcedClear(out _), Is.False);
        }

        [Test]
        public void DemoStageControl_ForceClear_UsesMinimalCompletionPipeline()
        {
            var entry = CreateEntry("stage-0-1");
            var runtime = new GameplayHostStageCompletionRuntime(entry);

            var readModel = runtime.ForceClearCurrentStage();

            Assert.That(readModel.StageId, Is.EqualTo(entry.StageId));
            Assert.That(readModel.FinalTickIndex, Is.Zero);
            Assert.That(readModel.ContinueRequest.IsValid, Is.True);
            Assert.That(readModel.RetryRequest.IsValid, Is.True);
        }

        [Test]
        public void DemoStageControl_ServiceDoesNotReferenceLegacyDeveloperCommandsOrSceneLoading()
        {
            var source = File.ReadAllText("Assets/_Features/DemoStageControl/Runtime/DemoStageControlService.cs");
            var removedCommandPrefix = "Debug" + "Command";

            Assert.That(source, Does.Not.Contain(removedCommandPrefix));
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
            Assert.That(source, Does.Not.Contain("TransientCampaignSaveSlotStore"));
            Assert.That(source, Does.Not.Contain("Stage" + "Completion" + "Profile" + "Snapshot"));
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
            out TransientCampaignSaveSlotStore saveStore,
            out RecordingStageLaunchRouter router,
            IDemoStageControlLaunchBridge launchBridge = null,
            CampaignStageSequenceResolver sequenceResolver = null)
        {
            if (sequenceResolver == null)
            {
                sequenceResolver = LoadProductionSequenceResolver();
            }

            var provider = new TestStageCatalogProvider(entries);
            saveStore = new TransientCampaignSaveSlotStore(_saveSlotNamespace);
            var activeSlotProvider = new ActiveSlotProvider(new TransientActiveSlotStorage(_activeSlotNamespace));
            activeSlotProvider.SetActiveSlot(1);
            saveStore.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                1,
                entries[0].StageId,
                "level-0",
                CampaignSaveSlotPolicy.DefaultRemainingChances,
                string.Empty));
            var campaignBridge = new DemoStageControlCampaignBridge(
                saveStore,
                saveStore,
                activeSlotProvider,
                sequenceResolver);
            router = new RecordingStageLaunchRouter();
            return new DemoStageControlService(
                DemoStageControlSettings.EnabledByDefault(),
                provider,
                sequenceResolver,
                campaignBridge,
                launchBridge ?? new DemoStageControlLaunchBridge(router, () => false),
                new RecordingCompletionBridge());
        }

        private StageContentEntry CreateEntry(
            string rawStageId,
            bool initiallyAvailable = true)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            _createdObjects.Add(entry);
            entry.AssignStageId(StageId.CreateOrThrow(rawStageId));
            entry.AssignInitialAvailability(initiallyAvailable);
            var presentationDefinition = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            _createdObjects.Add(presentationDefinition);
            SetPrivateField(presentationDefinition, "displayNameKey", StageDisplayNameKeys.ForStage(entry.StageId));
            entry.AssignPresentationDefinition(presentationDefinition);

            return entry;
        }

        private static CampaignStageSequenceResolver LoadProductionSequenceResolver()
        {
            return CampaignStageSequenceTestAsset.LoadProductionResolver();
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

    }
}
