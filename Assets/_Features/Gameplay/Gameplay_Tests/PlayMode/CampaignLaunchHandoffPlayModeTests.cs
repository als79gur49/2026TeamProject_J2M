using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class CampaignLaunchHandoffPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
            StageLaunchContextStore.Clear();
            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            GameplayEntryTransitionVisualSnapshotRegistry.ResetForTests();
            SceneTransitionCoordinator.SetSceneLoaderForTests(null);
            SceneTransitionCoordinator.BindUiAudioPortForCurrentScene(null);
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
        }

        [TearDown]
        public void TearDown()
        {
            var playback = TerminalTransitionRegistry.Current;
            if (playback != null)
            {
                TerminalTransitionRegistry.Clear(playback);
                playback.Dispose();
            }

            var coordinators = UnityEngine.Object.FindObjectsByType<SceneTransitionCoordinator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < coordinators.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(coordinators[i].gameObject);
            }

            var uiRoots = UnityEngine.Object.FindObjectsByType<GameplayUiCanvasRootView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < uiRoots.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(uiRoots[i].gameObject);
            }

            var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < eventSystems.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(eventSystems[i].gameObject);
            }

            SceneTransitionCoordinator.SetSceneLoaderForTests(null);
            SceneTransitionCoordinator.BindUiAudioPortForCurrentScene(null);
            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            GameplayEntryTransitionVisualSnapshotRegistry.ResetForTests();
            StageLaunchContextStore.Clear();
            CampaignLaunchHandoffSessionStore.ResetForTests();
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PendingHandoff_SurvivesRequiredCrossSceneLifetime()
        {
            var originalScene = SceneManager.GetActiveScene();
            var firstScene = SceneManager.CreateScene("CampaignHandoffSmoke_First");
            var secondScene = default(Scene);
            SceneManager.SetActiveScene(firstScene);
            var store = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                store.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Continue,
                    "playmode-cross-scene",
                    out var accepted),
                Is.True);

            secondScene = SceneManager.CreateScene("CampaignHandoffSmoke_Second");
            SceneManager.SetActiveScene(secondScene);
            yield return null;
            var unloadFirst = SceneManager.UnloadSceneAsync(firstScene);
            if (unloadFirst != null)
            {
                yield return unloadFirst;
            }

            var survived = store.TryPeek(out var afterSceneChange) &&
                           object.ReferenceEquals(afterSceneChange, accepted);

            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
            }

            var unloadSecond = SceneManager.UnloadSceneAsync(secondScene);
            if (unloadSecond != null)
            {
                yield return unloadSecond;
            }

            Assert.That(survived, Is.True);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator SubsystemRegistrationReset_DoesNotRestorePendingHandoff()
        {
            var store = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                store.TryBegin(
                    2,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "playmode-session-reset",
                    out _),
                Is.True);
            yield return null;

            CampaignLaunchHandoffSessionStore.ResetForTests();
            yield return null;

            Assert.That(store.TryPeek(out _), Is.False);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalCoordinator_LoadFirst_TimeScaleZero_MultiSlotAndDestinationReadyGate()
        {
            var load = new ControlledSceneLoadOperation(progress: 0.9f);
            var audio = new RecordingUiAudioPort();
            using var iris = TerminalIrisHarness.Create();
            var terminalToken = iris.Token;
            SceneTransitionCoordinator.SetSceneLoaderForTests(_ => load);
            var coordinator = SceneTransitionCoordinator.Instance;
            coordinator.BindUiAudioPort(audio);
            Time.timeScale = 0f;

            var request = CreateChanceLostRequest(
                terminalToken,
                previousRemainingChances: 3,
                currentRemainingChances: 1);
            Assert.That(
                coordinator.TryStartStageTransition(request, "TerminalTest_LoadFirst"),
                Is.True);
            Assert.That(
                coordinator.TryStartStageTransition(request, "TerminalTest_Duplicate"),
                Is.False);

            yield return WaitUntilRealtime(
                () => load.AllowSceneActivation,
                8f,
                "Load-first transition never joined real Chance Lost completion.");

            Assert.That(audio.Played, Is.EqualTo(new[] { UiAudioCueId.ChanceLoss }));
            Assert.That(load.IsDone, Is.True);
            Assert.That(TerminalSessionRegistry.IsActive, Is.True);
            Assert.That(
                TerminalSessionRegistry.Current.Phase,
                Is.EqualTo(TerminalSessionPhase.WaitingDestinationReady));
            Assert.That(TerminalDestinationReadiness.IsReady(terminalToken), Is.False);

            yield return null;
            Assert.That(TerminalSessionRegistry.IsActive, Is.True);
            Assert.That(
                TerminalDestinationReadiness.Signal(CreateCurrentReadinessSignal(
                    new TerminalSessionToken(
                        terminalToken.AuthorityGeneration + 1,
                        terminalToken.Sequence))),
                Is.False);
            Assert.That(TerminalSessionRegistry.IsActive, Is.True);
            var destinationGeneration =
                TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                    9701,
                    "FakeDestination701");
            Assert.That(
                TerminalDestinationReadiness.Signal(CreateCurrentReadinessSignal(terminalToken)),
                Is.True);
            CompleteControlledSceneEntry(destinationGeneration);

            yield return WaitUntilRealtime(
                () => !TerminalSessionRegistry.IsActive,
                3f,
                "Persistent reveal did not complete after the correlated destination readiness signal.");

            Assert.That(coordinator.IsTransitionInProgress, Is.False);
            Assert.That(TerminalSessionRegistry.Current.Phase, Is.EqualTo(TerminalSessionPhase.Completed));
            Assert.That(TerminalDestinationReadiness.IsReady(terminalToken), Is.False);
            var nextClaim = TerminalSessionRegistry.Authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Defeat,
                TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                TerminalDestinationKind.ReloadedGameplay));
            Assert.That(nextClaim.Accepted, Is.True);
            Assert.That(nextClaim.Token, Is.Not.EqualTo(terminalToken));
            Assert.That(TerminalDestinationReadiness.IsReady(nextClaim.Token), Is.False);
            Assert.That(
                TerminalSessionRegistry.TryAdvance(
                    nextClaim.Token,
                    TerminalSessionPhase.Revealing),
                Is.True);
            Assert.That(TerminalSessionRegistry.TryComplete(nextClaim.Token), Is.True);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalCoordinator_ContentFirst_HoldsBlackUntilAsyncLoadReady()
        {
            var load = new ControlledSceneLoadOperation(progress: 0.1f);
            using var iris = TerminalIrisHarness.Create();
            var terminalToken = iris.Token;
            SceneTransitionCoordinator.SetSceneLoaderForTests(_ => load);
            var coordinator = SceneTransitionCoordinator.Instance;
            Time.timeScale = 0f;

            Assert.That(
                coordinator.TryStartStageTransition(
                    CreateChanceLostRequest(terminalToken, 2, 1),
                    "TerminalTest_ContentFirst"),
                Is.True);

            yield return WaitUntilRealtime(
                () => UnityEngine.Object.FindFirstObjectByType<ChanceLostOverlayContentView>()?.IsCompleted == true,
                8f,
                "Chance Lost content did not complete under Time.timeScale=0.");

            Assert.That(load.AllowSceneActivation, Is.False);
            Assert.That(coordinator.IsTransitionInProgress, Is.True);
            Assert.That(TerminalSessionRegistry.IsActive, Is.True);

            load.Progress = 0.9f;
            yield return WaitUntilRealtime(
                () => load.AllowSceneActivation,
                2f,
                "Content-first transition did not activate after async load became ready.");
            var destinationGeneration =
                TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                    9702,
                    "FakeDestination702");
            Assert.That(
                TerminalDestinationReadiness.Signal(CreateCurrentReadinessSignal(terminalToken)),
                Is.True);
            CompleteControlledSceneEntry(destinationGeneration);
            yield return WaitUntilRealtime(
                () => !TerminalSessionRegistry.IsActive,
                3f,
                "Content-first reveal did not complete.");

            Assert.That(coordinator.IsTransitionInProgress, Is.False);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalCoordinator_ContentCancellation_EntersFailedHoldingCoverAndRejectsSecondLoad()
        {
            var loadCount = 0;
            var load = new ControlledSceneLoadOperation(progress: 0.9f);
            using var iris = TerminalIrisHarness.Create();
            var terminalToken = iris.Token;
            SceneTransitionCoordinator.SetSceneLoaderForTests(_ =>
            {
                loadCount++;
                return load;
            });
            var coordinator = SceneTransitionCoordinator.Instance;

            var request = CreateChanceLostRequest(terminalToken, 2, 1);
            Assert.That(
                coordinator.TryStartStageTransition(request, "TerminalTest_Cancel"),
                Is.True);
            yield return WaitUntilRealtime(
                () => UnityEngine.Object.FindFirstObjectByType<ChanceLostOverlayContentView>() != null,
                3f,
                "Chance Lost content was not mounted.");

            LogAssert.Expect(
                LogType.Error,
                new Regex("Terminal scene transition 1 entered FailedHoldingCover.*token="));
            UnityEngine.Object.FindFirstObjectByType<ChanceLostOverlayContentView>().Hide();
            yield return WaitUntilRealtime(
                () => TerminalSessionRegistry.Current.Phase ==
                      TerminalSessionPhase.FailedHoldingCover,
                2f,
                "Cancelled content did not enter FailedHoldingCover.");

            Assert.That(load.AllowSceneActivation, Is.False);
            Assert.That(coordinator.IsTransitionInProgress, Is.True);
            Assert.That(
                coordinator.TryStartStageTransition(request, "TerminalTest_CancelDuplicate"),
                Is.False);
            Assert.That(loadCount, Is.EqualTo(1));

            var shell = UnityEngine.Object.FindFirstObjectByType<SceneTransitionOverlayShellView>();
            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.gameObject.activeInHierarchy, Is.True);
            Assert.That(shell.PersistentCoverOpacityForTests, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator ChanceLostProductionView_TwoSlotsRunsAllEighteenShardsAndSettlesExactlyOnce()
        {
            var prefab = Resources.Load<ChanceLostOverlayContentView>(
                "UI/Transitions/Contents/ChanceLostOverlayContent");
            Assert.That(prefab, Is.Not.Null);
            var view = UnityEngine.Object.Instantiate(prefab);
            var completedCount = 0;
            var cancelledCount = 0;
            view.Completed += () => completedCount++;
            view.Cancelled += () => cancelledCount++;
            try
            {
                view.Bind(new SceneTransitionOverlayModel(
                    StageTransitionKind.DeathRetryChanceLost,
                    TransitionOverlayKind.ChanceLost,
                    "Chance Lost",
                    "Retrying.",
                    blockInput: true,
                    showProgress: true,
                    progress01: 0f,
                    hasChanceLost: true,
                    previousRemainingChances: 3,
                    currentRemainingChances: 1,
                    totalChances: 3,
                    deathCount: 1));

                var lostSlotShardStates = new List<ShardEndpointObservation>();
                var slots = view.ResolvedChanceSlotsForTests;
                for (var slotIndex = 1; slotIndex < 3; slotIndex++)
                {
                    var tweenRoot = slots[slotIndex].Find("LostChanceTweenRoot");
                    Assert.That(tweenRoot, Is.Not.Null);
                    var images = tweenRoot.GetComponentsInChildren<Image>(includeInactive: true);
                    for (var imageIndex = 0; imageIndex < images.Length; imageIndex++)
                    {
                        if (!images[imageIndex].name.StartsWith(
                                "CrackShard",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        lostSlotShardStates.Add(new ShardEndpointObservation(
                            images[imageIndex],
                            images[imageIndex].rectTransform.anchoredPosition,
                            images[imageIndex].rectTransform.localRotation,
                            images[imageIndex].rectTransform.localScale));
                    }
                }

                Assert.That(lostSlotShardStates, Has.Count.EqualTo(18));
                Time.timeScale = 0f;
                view.Show();
                var duration = view.RootSequenceDurationSecondsForTests;
                var settle = view.PostShatterSettleDurationSecondsForTests;
                var shardDeadline = Time.realtimeSinceStartup + 6f;
                while (view.RootSequencePositionSecondsForTests < duration - settle &&
                       Time.realtimeSinceStartup < shardDeadline)
                {
                    yield return null;
                }

                Assert.That(
                    view.RootSequencePositionSecondsForTests,
                    Is.GreaterThanOrEqualTo(duration - settle - 0.01f));
                Assert.That(view.IsCompleted, Is.False, "Root completion fired before the authored settle interval.");
                Assert.That(completedCount, Is.Zero);
                for (var i = 0; i < lostSlotShardStates.Count; i++)
                {
                    var shard = lostSlotShardStates[i];
                    Assert.That(shard.Image.color.a, Is.LessThanOrEqualTo(0.01f), $"shard {i} alpha");
                    Assert.That(
                        Vector2.Distance(shard.Image.rectTransform.anchoredPosition, shard.StartPosition),
                        Is.GreaterThan(1f),
                        $"shard {i} position");
                    Assert.That(
                        Quaternion.Angle(shard.Image.rectTransform.localRotation, shard.StartRotation),
                        Is.GreaterThan(1f),
                        $"shard {i} rotation");
                    Assert.That(
                        shard.Image.rectTransform.localScale.magnitude,
                        Is.LessThan(shard.StartScale.magnitude),
                        $"shard {i} scale");
                }

                var completionDeadline = Time.realtimeSinceStartup + 2f;
                while (!view.IsCompleted &&
                       Time.realtimeSinceStartup < completionDeadline)
                {
                    yield return null;
                }

                Assert.That(view.IsCompleted, Is.True);
                Assert.That(view.IsCancelled, Is.False);
                Assert.That(completedCount, Is.EqualTo(1));
                Assert.That(cancelledCount, Is.Zero);
                yield return null;
                Assert.That(completedCount, Is.EqualTo(1));
            }
            finally
            {
                Time.timeScale = 1f;
                if (view != null)
                {
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
                }
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalDropdownCleanup_PlayMode_RemovesHighSortingLiveListInClaimFrame()
        {
            const long claimId = 704;
            var tmpDropdownType = Type.GetType(
                "TMPro.TMP_Dropdown, Unity.TextMeshPro");
            Assert.That(tmpDropdownType, Is.Not.Null);
            var installerObject = new GameObject("TerminalDropdownCleanupInstaller");
            var dropdownObject = new GameObject(
                "ResolutionDropdown",
                new[]
                {
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup),
                    tmpDropdownType,
                });
            var liveList = new GameObject(
                "Dropdown List",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup),
                typeof(Image));
            var blocker = new GameObject(
                "Blocker",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup),
                typeof(Image));
            var eventSystemObject = new GameObject(
                "TerminalDropdownEventSystem",
                typeof(EventSystem));
            try
            {
                var installer = installerObject.AddComponent<GameplayUiFlowInstaller>();
                var dropdown = dropdownObject.GetComponent(tmpDropdownType);
                var isExpandedProperty = tmpDropdownType.GetProperty("IsExpanded");
                Assert.That(isExpandedProperty, Is.Not.Null);
                liveList.GetComponent<Canvas>().overrideSorting = true;
                liveList.GetComponent<Canvas>().sortingOrder = 50000;
                blocker.GetComponent<Canvas>().overrideSorting = true;
                blocker.GetComponent<Canvas>().sortingOrder = 49999;
                tmpDropdownType
                    .GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(dropdown, liveList);
                tmpDropdownType
                    .GetField("m_Blocker", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(dropdown, blocker);
                var eventSystem = eventSystemObject.GetComponent<EventSystem>();
                eventSystem.SetSelectedGameObject(dropdownObject);

                Assert.That((bool)isExpandedProperty.GetValue(dropdown), Is.True);
                Assert.That(liveList.activeInHierarchy, Is.True);
                Assert.That(blocker.activeInHierarchy, Is.True);
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.CurrentSceneGeneration > 0
                    ? authority.CurrentSceneGeneration
                    : authority.RegisterSceneBootstrap(
                        (int)claimId,
                        "dropdown-cleanup-test");
                var claim = authority.TryClaim(new TerminalClaimRequest(
                    TerminalTransitionKind.Defeat,
                    sourceGeneration,
                    TerminalDestinationKind.ReloadedGameplay));
                Assert.That(claim.Accepted, Is.True);

                Assert.That((bool)isExpandedProperty.GetValue(dropdown), Is.False);
                Assert.That(liveList == null || !liveList.activeInHierarchy, Is.True);
                Assert.That(blocker == null || !blocker.activeInHierarchy, Is.True);
                Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
                yield return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
                UnityEngine.Object.DestroyImmediate(dropdownObject);
                if (liveList != null)
                {
                    UnityEngine.Object.DestroyImmediate(liveList);
                }

                if (blocker != null)
                {
                    UnityEngine.Object.DestroyImmediate(blocker);
                }

                UnityEngine.Object.DestroyImmediate(eventSystemObject);
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator TerminalRenderEvidence_FourAspectRatios_CapturesNoGapLifecycleFrames()
        {
            if (Array.Exists(
                    Environment.GetCommandLineArgs(),
                    argument => string.Equals(
                        argument,
                        "-nographics",
                        StringComparison.OrdinalIgnoreCase)))
            {
                Assert.Ignore(
                    "Terminal render evidence requires UNITY_GRAPHICS=1 ./run_tests.sh core --filter TerminalRenderEvidence_.");
                yield break;
            }

            // URP emits non-fatal transient RenderTexture.Create diagnostics for internal
            // camera-stack targets in Windows batchmode. The explicit target creation,
            // pixel assertions, and PNG count below remain the evidence gate.
            LogAssert.ignoreFailingMessages = true;
            TerminalDestinationReadiness.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            var outputDirectory = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "TestLogs",
                "TerminalIris",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(outputDirectory);
            var resolutions = new[]
            {
                new Vector2Int(1920, 1080),
                new Vector2Int(1920, 1200),
                new Vector2Int(2560, 1080),
                new Vector2Int(3440, 1440),
            };

            for (var resolutionIndex = 0; resolutionIndex < resolutions.Length; resolutionIndex++)
            {
                var resolution = resolutions[resolutionIndex];
                var suffix = $"{resolution.x}x{resolution.y}";
                var cameraObject = new GameObject($"TerminalEvidenceCamera-{suffix}");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.red;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                var target = new RenderTexture(resolution.x, resolution.y, 24);
                Assert.That(target.Create(), Is.True, suffix);
                camera.targetTexture = target;

                var uiPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
                var uiRoot = UnityEngine.Object.Instantiate(uiPrefab);
                var rootView = uiRoot.GetComponent<GameplayUiCanvasRootView>();
                rootView.EnsureHierarchy();
                ConfigureCanvasForEvidence(uiRoot.GetComponent<Canvas>(), camera);
                var port = new GameplayTerminalTransitionPort(
                    rootView.TerminalIrisOverlayView,
                    rootView.RequireTerminalIrisMotionProfile().CreateResolver(),
                    focusTargetSource: null);
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(
                    9800 + resolutionIndex,
                    $"TerminalRenderEvidence-{suffix}");
                var claim = authority.TryClaim(new TerminalClaimRequest(
                    TerminalTransitionKind.Defeat,
                    sourceGeneration,
                    TerminalDestinationKind.ReloadedGameplay));
                Assert.That(claim.Accepted, Is.True);
                Assert.That(
                    port.TryBegin(
                        new TerminalTransitionRequest(
                            TerminalTransitionKind.Defeat,
                            focusEntityId: 1,
                            claim.Token,
                            TerminalTransitionDestinationMode.SceneHandoff),
                        out var irisPlayback),
                    Is.True);

                port.Tick(irisPlayback.Preset.FocusDuration * 0.5f);
                CaptureRenderTexture(camera, target, outputDirectory, $"01-iris-focus-{suffix}.png");

                port.Tick(irisPlayback.Preset.BlackAt);
                Assert.That(irisPlayback.State, Is.EqualTo(TerminalTransitionState.Black));
                CaptureRenderTexture(camera, target, outputDirectory, $"02-iris-black-{suffix}.png", Color.black);

                var shellPrefab = Resources.Load<SceneTransitionOverlayShellView>(
                    "UI/Transitions/SceneTransitionOverlayShell");
                var shell = UnityEngine.Object.Instantiate(shellPrefab);
                ConfigureCanvasForEvidence(shell.GetComponent<Canvas>(), camera);
                shell.RequestOpaqueTakeover(Color.black);
                CaptureRenderTexture(camera, target, outputDirectory, $"03-persistent-first-render-{suffix}.png", Color.black);
                yield return null;
                CaptureRenderTexture(camera, target, outputDirectory, $"03b-persistent-next-render-{suffix}.png", Color.black);
                Assert.That(
                    shell.HasRenderedOpaqueFrame,
                    Is.False,
                    "The RenderTexture evidence Canvas is intentionally ScreenSpaceCamera and must not satisfy production overlay acknowledgement.");
                Assert.That(irisPlayback.CompleteHandoff(claim.Token), Is.True);
                CaptureRenderTexture(camera, target, outputDirectory, $"04-iris-removed-persistent-black-{suffix}.png", Color.black);

                var contentPrefab = Resources.Load<ChanceLostOverlayContentView>(
                    "UI/Transitions/Contents/ChanceLostOverlayContent");
                var content = (ChanceLostOverlayContentView)shell.MountContent(contentPrefab);
                shell.ShowContent(
                    new SceneTransitionOverlayModel(
                        StageTransitionKind.DeathRetryChanceLost,
                        TransitionOverlayKind.ChanceLost,
                        "Chance Lost",
                        "Retrying.",
                        blockInput: true,
                        showProgress: true,
                        progress01: 0f,
                        hasChanceLost: true,
                        previousRemainingChances: 2,
                        currentRemainingChances: 1,
                        totalChances: 3,
                        deathCount: 1,
                        terminalClaimId: claim.Token.Sequence),
                    content);
                CaptureRenderTexture(
                    camera,
                    target,
                    outputDirectory,
                    $"05-chance-lost-start-{suffix}.png",
                    Color.black,
                    allowTerminalContent: true);

                var duration = content.RootSequenceDurationSecondsForTests;
                var settle = content.PostShatterSettleDurationSecondsForTests;
                content.GotoRootSequenceForTests(Mathf.Max(0f, duration - settle - 0.001f));
                Assert.That(content.IsCompleted, Is.False);
                CaptureRenderTexture(
                    camera,
                    target,
                    outputDirectory,
                    $"06-last-ninth-shard-{suffix}.png",
                    Color.black,
                    allowTerminalContent: true);
                content.GotoRootSequenceForTests(Mathf.Max(0f, duration - settle * 0.5f));
                Assert.That(content.IsCompleted, Is.False);
                CaptureRenderTexture(
                    camera,
                    target,
                    outputDirectory,
                    $"07-post-shatter-settle-{suffix}.png",
                    Color.black,
                    allowTerminalContent: true);
                content.GotoRootSequenceForTests(duration);
                Assert.That(content.IsCompleted, Is.True);

                shell.HideVisual();
                shell.SetPersistentCoverOpacity(1f);
                CaptureRenderTexture(camera, target, outputDirectory, $"08-before-activation-black-{suffix}.png", Color.black);

                var entryIris = rootView.TerminalIrisOverlayView;
                var entryPreset = rootView.RequireTerminalIrisMotionProfile()
                    .CreateResolver()
                    .ResolveStageEntryOpen();
                var entryCenter = new Vector2(0.5f, 0.5f);
                entryIris.ConfigureTransitionColor(Color.black);
                entryIris.Show();
                entryIris.ApplyClosedEntry(entryCenter, entryPreset);
                var beforeReleasePixels = CaptureRenderTexture(
                    camera,
                    target,
                    outputDirectory,
                    $"09-destination-closed-entry-before-release-{suffix}.png",
                    Color.black);
                shell.SetPersistentCoverOpacity(0f);
                var afterReleasePixels = CaptureRenderTexture(
                    camera,
                    target,
                    outputDirectory,
                    $"10-destination-closed-entry-after-release-{suffix}.png",
                    Color.black);
                AssertAndWriteEntryHandoffContinuity(
                    beforeReleasePixels,
                    afterReleasePixels,
                    outputDirectory,
                    suffix);

                var fullyRevealedRadius = entryIris.CalculateFullyRevealedRadius(
                    entryCenter,
                    entryPreset.FullOpenMargin);
                entryIris.ApplyEntryRadius(
                    fullyRevealedRadius * 0.02f,
                    entryPreset.FinalClosedOvershootPixels * 0.98f);
                CaptureRenderTexture(
                    camera,
                    target,
                    outputDirectory,
                    $"11-entry-opening-first-visible-{suffix}.png");
                entryIris.ApplyEntryRadius(fullyRevealedRadius, 0f);
                CaptureRenderTexture(
                    camera,
                    target,
                    outputDirectory,
                    $"12-entry-opening-completed-{suffix}.png",
                    Color.red);

                content.Hide();
                Assert.That(content.Playback.Outcome, Is.EqualTo(TransitionContentPlaybackOutcome.Completed));
                port.Dispose();
                UnityEngine.Object.DestroyImmediate(shell.gameObject);
                UnityEngine.Object.DestroyImmediate(uiRoot);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                TerminalDestinationReadiness.ResetForTests();
                TerminalSessionRegistry.ResetForTests();
                yield return null;
            }

            Assert.That(
                Directory.GetFiles(outputDirectory, "*.png", SearchOption.TopDirectoryOnly),
                Has.Length.EqualTo(resolutions.Length * 13));
            File.WriteAllText(
                Path.Combine(outputDirectory, "manifest.txt"),
                $"UTC={DateTime.UtcNow:O}\nFramesPerResolution=13\n" +
                "Lifecycle=source-close,opaque-hold,destination-closed-entry,cover-release,entry-opening\n" +
                "Resolutions=1920x1080,1920x1200,2560x1080,3440x1440\n" +
                "SameColorChannelTolerance=0/255\n" +
                "TransparentPixelTolerance=0\n" +
                "UnexpectedChromaShiftTolerance=0\n");
            UnityEngine.Debug.Log($"Terminal Iris render evidence: {outputDirectory}");
            LogAssert.ignoreFailingMessages = false;
        }

        private static StageNavigationRequest CreateChanceLostRequest(
            TerminalSessionToken terminalToken,
            int previousRemainingChances,
            int currentRemainingChances)
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            var hint = StageTransitionHint.ForChanceLost(
                    new StageTransitionChanceLostPayload(
                        previousRemainingChances,
                        currentRemainingChances,
                        3,
                        stageId,
                        stageId,
                        deathCount: 1,
                        source: "campaign-death-retry",
                        title: "Chance Lost",
                        message: "Retrying."))
                .WithTerminalClaim(terminalToken);
            return new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "campaign-death-retry",
                hint,
                SceneTransitionIntent.DeathRetry);
        }

        private static DestinationReadinessSignal CreateCurrentReadinessSignal(
            TerminalSessionToken token)
        {
            var session = TerminalSessionRegistry.Current;
            return new DestinationReadinessSignal(
                token,
                session.TransitionId,
                session.SourceSceneGeneration,
                session.DestinationSceneGeneration,
                session.DestinationKind,
                TerminalSessionPhase.WaitingDestinationReady,
                TerminalDestinationProvenance.ReloadedGameplayBootstrap,
                DestinationReadinessOutcome.Ready);
        }

        private static void CompleteControlledSceneEntry(long destinationSceneGeneration)
        {
            var session = SceneEntryPresentationRegistry.Current;
            Assert.That(session.IsActive, Is.True);
            Assert.That(
                session.Phase,
                Is.EqualTo(SceneEntryPresentationPhase.Loading));
            Assert.That(
                SceneEntryPresentationRegistry.TryRegisterDestinationScene(
                    session.Token,
                    destinationSceneGeneration),
                Is.True);

            var cameraObject = new GameObject("ControlledDestinationOutputCamera", typeof(Camera));
            try
            {
                var ready = new SceneEntryRuntimeReady(
                    session.Token,
                    session.TransitionId,
                    destinationSceneGeneration,
                    playerEntityId: 1,
                    cameraObject.GetComponent<Camera>(),
                    SceneEntryRuntimeReadyProvenance.ProductionGameplayBootstrap);
                Assert.That(
                    SceneEntryPresentationRegistry.CanAcceptRuntimeReady(ready),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        session.Token,
                        SceneEntryPresentationPhase.EntryIrisClosed),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        session.Token,
                        SceneEntryPresentationPhase.Opening),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryComplete(session.Token),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static IEnumerator WaitUntilRealtime(
            Func<bool> predicate,
            float timeoutSeconds,
            string failureMessage)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, failureMessage);
        }

        private static void ConfigureCanvasForEvidence(Canvas canvas, Camera camera)
        {
            Assert.That(canvas, Is.Not.Null);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
        }

        private static Color32[] CaptureRenderTexture(
            Camera camera,
            RenderTexture target,
            string outputDirectory,
            string fileName,
            Color? expectedCorner = null,
            bool allowTerminalContent = false)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(outputDirectory, fileName), texture.EncodeToPNG());
            if (expectedCorner.HasValue)
            {
                var expected = expectedCorner.Value;
                const int gridStride = 16;
                var exposedPixelCount = 0;
                var sampledPixelCount = 0;
                for (var y = 0; y < target.height; y += gridStride)
                {
                    for (var x = 0; x < target.width; x += gridStride)
                    {
                        var actual = texture.GetPixel(x, y);
                        sampledPixelCount++;
                        if (!IsAllowedTerminalContentPixel(x, y, target, allowTerminalContent) &&
                            (Mathf.Abs(actual.r - expected.r) > 0.02f ||
                            Mathf.Abs(actual.g - expected.g) > 0.02f ||
                            Mathf.Abs(actual.b - expected.b) > 0.02f))
                        {
                            exposedPixelCount++;
                        }
                    }
                }

                var representativePoints = new[]
                {
                    new Vector2Int(2, 2),
                    new Vector2Int(target.width - 3, 2),
                    new Vector2Int(2, target.height - 3),
                    new Vector2Int(target.width - 3, target.height - 3),
                    new Vector2Int(target.width / 2, target.height / 2),
                    new Vector2Int(target.width / 2, 2),
                    new Vector2Int(target.width / 2, target.height - 3),
                    new Vector2Int(2, target.height / 2),
                    new Vector2Int(target.width - 3, target.height / 2),
                    new Vector2Int(target.width / 4, target.height / 3),
                    new Vector2Int(target.width * 3 / 4, target.height * 2 / 3),
                };
                for (var i = 0; i < representativePoints.Length; i++)
                {
                    var point = representativePoints[i];
                    var actual = texture.GetPixel(point.x, point.y);
                    sampledPixelCount++;
                    if (!IsAllowedTerminalContentPixel(
                            point.x,
                            point.y,
                            target,
                            allowTerminalContent) &&
                        (Mathf.Abs(actual.r - expected.r) > 0.02f ||
                        Mathf.Abs(actual.g - expected.g) > 0.02f ||
                        Mathf.Abs(actual.b - expected.b) > 0.02f))
                    {
                        exposedPixelCount++;
                    }
                }

                Assert.That(
                    exposedPixelCount,
                    Is.Zero,
                    $"{fileName}: gameplay exposure pixel count must be zero across {sampledPixelCount} screen-wide samples.");
            }

            var pixels = texture.GetPixels32();
            UnityEngine.Object.DestroyImmediate(texture);
            RenderTexture.active = previous;
            return pixels;
        }

        private static void AssertAndWriteEntryHandoffContinuity(
            Color32[] before,
            Color32[] after,
            string outputDirectory,
            string suffix)
        {
            Assert.That(after, Has.Length.EqualTo(before.Length));
            long transparentPixelCount = 0;
            long changedPixelCount = 0;
            long unexpectedChromaShiftCount = 0;
            long luminanceDeltaSum = 0;
            var maxChannelDelta = 0;
            var luminanceHistogram = new long[256];
            for (var i = 0; i < before.Length; i++)
            {
                var left = before[i];
                var right = after[i];
                if (left.a < byte.MaxValue || right.a < byte.MaxValue)
                {
                    transparentPixelCount++;
                }

                var redDelta = Mathf.Abs(left.r - right.r);
                var greenDelta = Mathf.Abs(left.g - right.g);
                var blueDelta = Mathf.Abs(left.b - right.b);
                var alphaDelta = Mathf.Abs(left.a - right.a);
                var pixelMaxDelta = Mathf.Max(
                    Mathf.Max(redDelta, greenDelta),
                    Mathf.Max(blueDelta, alphaDelta));
                maxChannelDelta = Mathf.Max(maxChannelDelta, pixelMaxDelta);
                if (pixelMaxDelta > 1)
                {
                    changedPixelCount++;
                }

                var leftLuminance = Mathf.RoundToInt(
                    left.r * 0.2126f +
                    left.g * 0.7152f +
                    left.b * 0.0722f);
                var rightLuminance = Mathf.RoundToInt(
                    right.r * 0.2126f +
                    right.g * 0.7152f +
                    right.b * 0.0722f);
                var luminanceDelta = Mathf.Clamp(
                    Mathf.Abs(leftLuminance - rightLuminance),
                    0,
                    255);
                luminanceDeltaSum += luminanceDelta;
                luminanceHistogram[luminanceDelta]++;

                var rgbMax = Mathf.Max(redDelta, Mathf.Max(greenDelta, blueDelta));
                var rgbMin = Mathf.Min(redDelta, Mathf.Min(greenDelta, blueDelta));
                if (rgbMax - rgbMin > 1)
                {
                    unexpectedChromaShiftCount++;
                }
            }

            var p99Target = (long)Math.Ceiling(before.Length * 0.99d);
            long cumulative = 0;
            var p99LuminanceDelta = 0;
            for (var delta = 0; delta < luminanceHistogram.Length; delta++)
            {
                cumulative += luminanceHistogram[delta];
                if (cumulative >= p99Target)
                {
                    p99LuminanceDelta = delta;
                    break;
                }
            }

            var meanLuminanceDelta = before.Length > 0
                ? luminanceDeltaSum / (double)before.Length
                : 0d;
            Assert.That(transparentPixelCount, Is.Zero);
            Assert.That(changedPixelCount, Is.Zero);
            Assert.That(maxChannelDelta, Is.Zero);
            Assert.That(meanLuminanceDelta, Is.Zero);
            Assert.That(p99LuminanceDelta, Is.Zero);
            Assert.That(unexpectedChromaShiftCount, Is.Zero);
            File.WriteAllText(
                Path.Combine(outputDirectory, $"entry-handoff-continuity-{suffix}.txt"),
                $"transparent_pixel_count={transparentPixelCount}\n" +
                $"changed_pixel_count={changedPixelCount}\n" +
                $"max_channel_delta={maxChannelDelta}\n" +
                $"mean_luminance_delta={meanLuminanceDelta:F6}\n" +
                $"p99_luminance_delta={p99LuminanceDelta}\n" +
                $"unexpected_chroma_shift_count={unexpectedChromaShiftCount}\n");
        }

        private static bool IsAllowedTerminalContentPixel(
            int x,
            int y,
            RenderTexture target,
            bool allowTerminalContent)
        {
            if (!allowTerminalContent)
            {
                return false;
            }

            var normalizedX = target.width > 0 ? (float)x / target.width : 0f;
            var normalizedY = target.height > 0 ? (float)y / target.height : 0f;
            var chanceLostContent =
                normalizedX >= 0.07f && normalizedX <= 0.93f &&
                normalizedY >= 0.32f && normalizedY <= 0.95f;
            var loadingIndicator =
                normalizedX >= 0.70f &&
                normalizedY <= 0.17f;
            return chanceLostContent || loadingIndicator;
        }

        private sealed class ControlledSceneLoadOperation : ISceneTransitionLoadOperation
        {
            private bool _allowSceneActivation;

            public ControlledSceneLoadOperation(float progress)
            {
                Progress = progress;
            }

            public float Progress { get; set; }

            public bool IsDone { get; private set; }

            public bool AllowSceneActivation
            {
                get => _allowSceneActivation;
                set
                {
                    _allowSceneActivation = value;
                    if (value)
                    {
                        IsDone = true;
                    }
                }
            }
        }

        private sealed class RecordingUiAudioPort : IUiAudioPort
        {
            public readonly List<UiAudioCueId> Played = new();

            public void Play(UiAudioCueId cueId)
            {
                Played.Add(cueId);
            }
        }

        private readonly struct ShardEndpointObservation
        {
            public ShardEndpointObservation(
                Image image,
                Vector2 startPosition,
                Quaternion startRotation,
                Vector3 startScale)
            {
                Image = image;
                StartPosition = startPosition;
                StartRotation = startRotation;
                StartScale = startScale;
            }

            public Image Image { get; }

            public Vector2 StartPosition { get; }

            public Quaternion StartRotation { get; }

            public Vector3 StartScale { get; }
        }

        private sealed class TerminalIrisHarness : IDisposable
        {
            private static int _nextSceneHandle = 8800;
            private readonly GameObject _root;
            private readonly GameplayTerminalTransitionPort _port;

            private TerminalIrisHarness(
                GameObject root,
                GameplayTerminalTransitionPort port)
            {
                _root = root;
                _port = port;
            }

            public TerminalSessionToken Token { get; private set; }

            public static TerminalIrisHarness Create()
            {
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(
                    ++_nextSceneHandle,
                    "TerminalHarnessSource");
                var claim = authority.TryClaim(new TerminalClaimRequest(
                    TerminalTransitionKind.Defeat,
                    sourceGeneration,
                    TerminalDestinationKind.ReloadedGameplay));
                Assert.That(claim.Accepted, Is.True);
                var prefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
                Assert.That(prefab, Is.Not.Null);
                var root = UnityEngine.Object.Instantiate(prefab);
                var rootView = root.GetComponent<GameplayUiCanvasRootView>();
                rootView.EnsureHierarchy();
                var port = new GameplayTerminalTransitionPort(
                    rootView.TerminalIrisOverlayView,
                    rootView.RequireTerminalIrisMotionProfile().CreateResolver(),
                    focusTargetSource: null);
                Assert.That(
                    port.TryBegin(
                        new TerminalTransitionRequest(
                            TerminalTransitionKind.Defeat,
                            focusEntityId: 1,
                            claim.Token,
                            TerminalTransitionDestinationMode.SceneHandoff),
                        out var playback),
                    Is.True);
                port.Tick(playback.Preset.BlackAt);
                Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Black));
                return new TerminalIrisHarness(root, port)
                {
                    Token = claim.Token,
                };
            }

            public void Dispose()
            {
                _port.Dispose();
                if (_root != null)
                {
                    UnityEngine.Object.DestroyImmediate(_root);
                }
            }
        }
    }
}
