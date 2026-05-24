using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Game.Feature.UI.Tests
{
    public sealed class SlotCinematicFlowTests
    {
        [Test]
        public void CinematicStageLaunchRouter_IntroPlaysOncePerSlot_AndPreservesRequest()
        {
            var keys = TestKeys.Create(nameof(CinematicStageLaunchRouter_IntroPlaysOncePerSlot_AndPreservesRequest));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var activeSlotProvider = new ActiveSlotProvider(keys.ActiveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(1, stageId));
                activeSlotProvider.SetActiveSlot(1);
                var inner = new RecordingStageLaunchRouter();
                var player = new ManualSlotCinematicPlayer { HasIntroClipValue = true };
                var router = new CinematicStageLaunchRouter(inner, saveStore, activeSlotProvider, player);
                var request = new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.Continue,
                    "main-menu-new-game",
                    StageTransitionHint.ForKindWithMinimum(StageTransitionKind.MainToGameplay, 0.25f));

                router.Launch(request);

                Assert.That(player.PlayIntroCallCount, Is.EqualTo(1));
                Assert.That(inner.Requests, Is.Empty);

                player.CompleteIntro();

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.True);
                Assert.That(inner.Requests, Has.Count.EqualTo(1));
                AssertRequestsEqual(request, inner.Requests[0]);

                router.Launch(request);

                Assert.That(player.PlayIntroCallCount, Is.EqualTo(1));
                Assert.That(inner.Requests, Has.Count.EqualTo(2));
                AssertRequestsEqual(request, inner.Requests[1]);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicStageLaunchRouter_MissingIntroClip_DelegatesImmediately()
        {
            var keys = TestKeys.Create(nameof(CinematicStageLaunchRouter_MissingIntroClip_DelegatesImmediately));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var activeSlotProvider = new ActiveSlotProvider(keys.ActiveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(CreateSlot(1, stageId));
                activeSlotProvider.SetActiveSlot(1);
                var inner = new RecordingStageLaunchRouter();
                var player = new ManualSlotCinematicPlayer { HasIntroClipValue = false };
                var router = new CinematicStageLaunchRouter(inner, saveStore, activeSlotProvider, player);
                var request = new StageNavigationRequest(stageId, StageNavigationKind.Continue, "test");

                router.Launch(request);

                Assert.That(player.PlayIntroCallCount, Is.Zero);
                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(inner.Requests, Has.Count.EqualTo(1));
                AssertRequestsEqual(request, inner.Requests[0]);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicMainMenuReturnRouter_OutroPlaysOnceOnlyForFinalClearMainReturn()
        {
            var keys = TestKeys.Create(nameof(CinematicMainMenuReturnRouter_OutroPlaysOnceOnlyForFinalClearMainReturn));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var activeSlotProvider = new ActiveSlotProvider(keys.ActiveKey);
                saveStore.SaveSlot(CreateSlot(1, StageId.CreateOrThrow("stage-4-1"), campaignCompleted: true));
                activeSlotProvider.SetActiveSlot(1);
                var inner = new RecordingMainMenuReturnRouter();
                var player = new ManualSlotCinematicPlayer { HasOutroClipValue = true };
                var router = new CinematicMainMenuReturnRouter(
                    inner,
                    saveStore,
                    activeSlotProvider,
                    player,
                    () => true);

                router.ReturnToMainMenu();

                Assert.That(player.PlayOutroCallCount, Is.EqualTo(1));
                Assert.That(inner.ReturnCallCount, Is.Zero);

                player.CompleteOutro();

                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.True);
                Assert.That(inner.ReturnCallCount, Is.EqualTo(1));

                router.ReturnToMainMenu();

                Assert.That(player.PlayOutroCallCount, Is.EqualTo(1));
                Assert.That(inner.ReturnCallCount, Is.EqualTo(2));
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicMainMenuReturnRouter_LevelFailedMainReturn_BypassesOutro()
        {
            var keys = TestKeys.Create(nameof(CinematicMainMenuReturnRouter_LevelFailedMainReturn_BypassesOutro));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var activeSlotProvider = new ActiveSlotProvider(keys.ActiveKey);
                saveStore.SaveSlot(CreateSlot(1, StageId.CreateOrThrow("stage-0-1")));
                activeSlotProvider.SetActiveSlot(1);
                var inner = new RecordingMainMenuReturnRouter();
                var player = new ManualSlotCinematicPlayer { HasOutroClipValue = true };
                var router = new CinematicMainMenuReturnRouter(
                    inner,
                    saveStore,
                    activeSlotProvider,
                    player,
                    () => false);

                router.ReturnToMainMenu();

                Assert.That(player.PlayOutroCallCount, Is.Zero);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.False);
                Assert.That(inner.ReturnCallCount, Is.EqualTo(1));
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void SaveSlotStore_CinematicFlags_RoundTripAndResetWithNewGameAndDelete()
        {
            var keys = TestKeys.Create(nameof(SaveSlotStore_CinematicFlags_RoundTripAndResetWithNewGameAndDelete));
            try
            {
                var saveStore = new SaveSlotStore(keys.SaveKey);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = stageId,
                    IntroPlayed = true,
                    OutroPlayed = true,
                });

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.True);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.True);

                saveStore.SaveSlot(CreateSlot(1, stageId));

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.False);

                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = stageId,
                    IntroPlayed = true,
                    OutroPlayed = true,
                });
                saveStore.DeleteSlot(1);

                Assert.That(saveStore.LoadSlot(1).IntroPlayed, Is.False);
                Assert.That(saveStore.LoadSlot(1).OutroPlayed, Is.False);
                Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.True);
            }
            finally
            {
                keys.Clear();
            }
        }

        [Test]
        public void CinematicVideoOverlayView_UsesAudioSourceOutput_AndBlocksLowerInput()
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/3DM/0516.mp4");
            Assert.That(clip, Is.Not.Null, "0516.mp4 must remain importable as a VideoClip.");
            var root = new GameObject(nameof(CinematicVideoOverlayView_UsesAudioSourceOutput_AndBlocksLowerInput), typeof(RectTransform));
            try
            {
                var view = root.AddComponent<CinematicVideoOverlayView>();
                var completionCount = 0;

                view.Play(
                    clip,
                    new SlotCinematicPlaybackOptions(true, SlotCinematicAspectPolicy.FitInside, 320, 180),
                    _ => completionCount++);

                Assert.That(view.ConfiguredAudioOutputMode, Is.EqualTo(VideoAudioOutputMode.AudioSource));
                Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);

                view.CompleteForTesting(CinematicPlaybackCompletionKind.Completed);
                view.CompleteForTesting(CinematicPlaybackCompletionKind.Skipped);

                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CinematicSources_DoNotUseDirectVideoOutputOrUiSfxDispatch()
        {
            var overlaySource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/CinematicVideoOverlayView.cs");
            var focusSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/CinematicAudioFocusController.cs");
            var gameplayInstallerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");

            Assert.That(overlaySource, Does.Not.Contain("VideoAudioOutputMode.Direct"));
            Assert.That(focusSource, Does.Not.Contain("IUiAudioPort"));
            Assert.That(focusSource, Does.Not.Contain("AudioChannel.Bgm"));
            Assert.That(focusSource, Does.Not.Contain("AudioChannel.Sfx"));
            Assert.That(focusSource, Does.Not.Contain("AudioChannel.Ui"));
            Assert.That(focusSource, Does.Not.Contain("new BgmFlowCoordinator"));
            Assert.That(gameplayInstallerSource, Does.Not.Contain("GlobalAudioFlowRoot"));
            Assert.That(gameplayInstallerSource, Does.Not.Contain("GlobalAudioFlowBootstrap"));
            Assert.That(gameplayInstallerSource, Does.Not.Contain("IBgmFlowCoordinator"));
        }

        private static SaveSlotData CreateSlot(
            int slotNumber,
            StageId stageId,
            bool campaignCompleted = false)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "test-group",
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                CampaignCompleted = campaignCompleted,
            };
        }

        private static void AssertRequestsEqual(StageNavigationRequest expected, StageNavigationRequest actual)
        {
            Assert.That(actual.StageId, Is.EqualTo(expected.StageId));
            Assert.That(actual.NavigationKind, Is.EqualTo(expected.NavigationKind));
            Assert.That(actual.Source, Is.EqualTo(expected.Source));
            Assert.That(actual.TransitionHint.Kind, Is.EqualTo(expected.TransitionHint.Kind));
            Assert.That(actual.TransitionHint.HasMinimumVisibleSecondsOverride, Is.EqualTo(expected.TransitionHint.HasMinimumVisibleSecondsOverride));
            Assert.That(actual.TransitionHint.MinimumVisibleSecondsOverride, Is.EqualTo(expected.TransitionHint.MinimumVisibleSecondsOverride));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }

        private readonly struct TestKeys
        {
            private TestKeys(string saveKey, string activeKey)
            {
                SaveKey = saveKey;
                ActiveKey = activeKey;
            }

            public string SaveKey { get; }

            public string ActiveKey { get; }

            public static TestKeys Create(string suffix)
            {
                var prefix = "Game.Feature.UI.Tests.SlotCinematic." + suffix + "." + Guid.NewGuid().ToString("N");
                return new TestKeys(prefix + ".Save", prefix + ".Active");
            }

            public void Clear()
            {
                PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.DeleteKey(ActiveKey);
                PlayerPrefs.Save();
            }
        }

        private sealed class ManualSlotCinematicPlayer : ISlotCinematicPlayer
        {
            private Action _introCompletion;
            private Action _outroCompletion;

            public bool HasIntroClipValue { get; set; }

            public bool HasOutroClipValue { get; set; }

            public bool HasIntroClip => HasIntroClipValue;

            public bool HasOutroClip => HasOutroClipValue;

            public bool IsPlaying { get; private set; }

            public int PlayIntroCallCount { get; private set; }

            public int PlayOutroCallCount { get; private set; }

            public void PlayIntro(Action completion)
            {
                PlayIntroCallCount++;
                IsPlaying = true;
                _introCompletion = completion;
            }

            public void PlayOutro(Action completion)
            {
                PlayOutroCallCount++;
                IsPlaying = true;
                _outroCompletion = completion;
            }

            public void RequestSkip()
            {
                CompleteIntro();
                CompleteOutro();
            }

            public void CompleteIntro()
            {
                var completion = _introCompletion;
                _introCompletion = null;
                IsPlaying = false;
                completion?.Invoke();
            }

            public void CompleteOutro()
            {
                var completion = _outroCompletion;
                _outroCompletion = null;
                IsPlaying = false;
                completion?.Invoke();
            }
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

        private sealed class RecordingMainMenuReturnRouter : IMainMenuReturnRouter
        {
            public int ReturnCallCount { get; private set; }

            public void ReturnToMainMenu()
            {
                ReturnCallCount++;
            }
        }
    }
}
