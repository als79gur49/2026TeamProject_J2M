using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Popups;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Exhibition.Tests.PlayMode
{
    public sealed class ParticipantResetMenuPlayModeTests
    {
        private sealed class PendingPort : IParticipantResetPort
        {
            public event Action Changed;
            public bool BlocksMenu => true;
            public bool IsBusy { get; private set; } = true;
            public bool CanRequest => false;
            public bool SuppressSaveSeedImport => true;
            public string Error { get; private set; }
            public int Restarts;
            private readonly TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>();
            public Task PrepareMenuAsync() => gate.Task;
            public void CompleteMenuInitialization() => Assert.Fail("Menu completed before recovery.");
            public void LeaveMenu() { }
            public void FailMenuInitialization(string reason) => Assert.Fail(reason);
            public void RequestReset() => Assert.Fail("A second reset was requested.");
            public void Restart() { Restarts++; }
            public void Fail()
            {
                IsBusy = false;
                Error = "Steam 연결을 확인한 뒤 다시 실행해 주세요.";
                Changed?.Invoke();
                gate.SetResult(true);
            }
        }

        [UnityTest, Category("Full")]
        public IEnumerator PendingMenuOwnsCommonShellAndErrorRetryReceivesPointer()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Pointer and screenshot evidence requires UNITY_GRAPHICS=1.");
            var port = new PendingPort();
            var oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            var oldInput = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
            Mouse mouse = null;
            ParticipantResetMenuAccess.Register(port);
            CampaignSaveCompositionProvider.SuspendProductionAccess();
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            try
            {
                yield return SceneManager.LoadSceneAsync(SceneUtility.GetScenePathByBuildIndex(0), LoadSceneMode.Single);
                yield return null;
                var installer = UnityEngine.Object.FindFirstObjectByType<MainMenuUiFlowInstaller>();
                Assert.That(installer, Is.Not.Null);
                Assert.That(installer.Controller, Is.Null);
                Assert.That(installer.HubController, Is.Null);
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(1));
                Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                port.Fail();
                yield return null;
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(installer.Controller, Is.Null);
                Assert.That(installer.PopupController.HandleBackRequested(), Is.True);
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(1));
                var popup = UnityEngine.Object.FindFirstObjectByType<ConfirmPopupView>();
                Assert.That(popup.BodyText, Does.Contain("Steam"));
                var retry = popup.GetComponentsInChildren<Button>().Single(b => b.name == "ConfirmButton");
                Assert.That(retry.interactable, Is.True);
                Canvas.ForceUpdateCanvases();
                var point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)retry.transform).TransformPoint(
                    ((RectTransform)retry.transform).rect.center));
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                Assert.That(hits.Any(hit => hit.gameObject == retry.gameObject || hit.gameObject.transform.IsChildOf(retry.transform)), Is.True,
                    "Retry raycast at " + point + ": " + string.Join(",", hits.Select(hit => hit.gameObject.name)));
                mouse = InputSystem.AddDevice<Mouse>();
                InputSystem.EnableDevice(mouse);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                yield return null;
                Assert.That(port.Restarts, Is.EqualTo(1), "Common EventSystem must deliver the actual pointer click.");
                Assert.That(installer.Controller, Is.Null);
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackground;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = oldInput;
#endif
                ParticipantResetMenuAccess.Register(null);
                CampaignSaveCompositionProvider.ReleaseProductionAccess();
            }
        }
    }
}
