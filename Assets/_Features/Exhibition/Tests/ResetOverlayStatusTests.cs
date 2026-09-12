using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Exhibition.RestartExperiment;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Exhibition.Tests
{
    public sealed class ResetOverlayStatusTests
    {
        [TestCase(ResetOverlayRole.Initiator, false)]
        [TestCase(ResetOverlayRole.ResetWorker, false)]
        [TestCase(ResetOverlayRole.FinalObserver, false)]
        [TestCase(ResetOverlayRole.Initiator, true)]
        [TestCase(ResetOverlayRole.ResetWorker, true)]
        [TestCase(ResetOverlayRole.FinalObserver, true)]
        public void TrialStatus_ActualInstallerDoesNotCreateProductRetryPopup(ResetOverlayRole role, bool failed)
        {
            var trial = new ResetOverlayTrial(null, null, role, failed ? new IOException("Trial failed") : null);
            using var menu = new MenuStatusHarness();
            menu.Refresh(trial);
            Assert.That(menu.Popups.PopupCount, Is.Zero);
            Assert.That(menu.Factory.Created, Is.Zero, "Trial status must not briefly open a product popup.");
            Assert.That(menu.ResetButton.gameObject.activeSelf, Is.False);
            Assert.That(trial.BlocksMenu, Is.True);
            Assert.That(menu.Installer.Controller, Is.Null, "Save module must remain unconstructed.");
            Assert.That(menu.Installer.HubController, Is.Null);
        }

        [TestCase(OverlayObservationRole.OriginObserver, false)]
        [TestCase(OverlayObservationRole.ReplacementObserver, false)]
        [TestCase(OverlayObservationRole.OriginObserver, true)]
        [TestCase(OverlayObservationRole.ReplacementObserver, true)]
        [Category("Integration")]
        public void ObservationPanelAndActualInstallerOwnStatusWithoutSaveModuleOrRetry(OverlayObservationRole role, bool failed)
        {
            var observation = new OverlayHandoffObservation(null, role, failed ? new IOException("observation failed") : null);
            var panel = OverlayHandoffObservationPresentation.Attach(observation);
            try
            {
                using var menu = new MenuStatusHarness();
                menu.Refresh(observation); menu.Refresh(observation);
                Assert.That(panel, Is.Not.Null);
                Assert.That(menu.Factory.Created, Is.Zero); Assert.That(menu.Popups.PopupCount, Is.Zero);
                Assert.That(menu.ResetButton.gameObject.activeSelf, Is.False);
                Assert.That(menu.Installer.Controller, Is.Null); Assert.That(menu.Installer.HubController, Is.Null);
                Assert.That(observation.BlocksMenu && observation.SuppressSaveSeedImport, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(panel.gameObject); }
        }

        [Test]
        public void TrialStatus_ExistingProductPopupClosesProgrammaticallyWithoutRetry()
        {
            using var menu = new MenuStatusHarness();
            var ordinary = new OrdinaryFailurePort();
            menu.Refresh(ordinary);
            Assert.That(menu.Popups.PopupCount, Is.EqualTo(1));
            var oldRuntime = menu.Factory.Last;
            PopupCompletion? completion = null;
            menu.Popups.PopupCompleted += row => completion = row.Completion;
            menu.Refresh(new ResetOverlayTrial(null, null, ResetOverlayRole.ResetWorker, new IOException("Trial failed")));
            Assert.That(menu.Popups.PopupCount, Is.Zero);
            Assert.That(completion.HasValue, Is.False, "Programmatic close must not publish a user completion.");
            Assert.That(oldRuntime.Disposed, Is.True);
            oldRuntime.Emit(PopupCompletionKind.Confirmed);
            oldRuntime.Emit(PopupCompletionKind.Cancelled);
            Assert.That(ordinary.Restarts, Is.Zero, "Closing a prior popup must not invoke its recovery callback.");
            Assert.That(menu.Factory.Created, Is.EqualTo(1));
        }

        [Test]
        public void TrialStatus_OrdinaryFailureRetainsEnabledRestartAndCallback()
        {
            using var menu = new MenuStatusHarness();
            var ordinary = new OrdinaryFailurePort();
            menu.Refresh(ordinary);
            Assert.That(menu.Popups.PopupCount, Is.EqualTo(1));
            var payload = (ConfirmPopupPayload)menu.Popups.TopPopup.Value.Payload;
            Assert.That(payload.ConfirmEnabled, Is.True);
            Assert.That(payload.CancelEnabled, Is.True);
            Assert.That(menu.ResetButton.gameObject.activeSelf, Is.True);
            menu.Factory.Last.Emit(PopupCompletionKind.Confirmed);
            Assert.That(ordinary.Restarts, Is.EqualTo(1));
            Assert.That(menu.Popups.PopupCount, Is.Zero);
        }

        private sealed class MenuStatusHarness : IDisposable
        {
            private readonly GameObject root;
            private readonly MainMenuScreenView view;
            public readonly MainMenuUiFlowInstaller Installer;
            public readonly PopupFactory Factory = new PopupFactory();
            public readonly PopupController Popups;
            public readonly Button ResetButton;
            public MenuStatusHarness()
            {
                root = new GameObject("Trial status installer fixture");
                root.SetActive(false);
                Installer = root.AddComponent<MainMenuUiFlowInstaller>();
                var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>("Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab");
                view = UnityEngine.Object.Instantiate(prefab, root.transform);
                Set("_mainMenuScreenView", view);
                Set("_localizedTextResolver", PackageFreeLocalizedTextResolver.CreateSettingsDefault());
                Popups = new PopupController(Factory);
                typeof(MainMenuUiFlowInstaller).GetProperty("PopupController").GetSetMethod(true).Invoke(Installer, new object[] { Popups });
                ResetButton = (Button)new SerializedObject(view).FindProperty("_participantResetButton").objectReferenceValue;
            }
            private void Set(string field, object value) => typeof(MainMenuUiFlowInstaller)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Installer, value);
            public void Refresh(IParticipantResetPort port)
            {
                Installer.ParticipantResetPort = port;
                typeof(MainMenuUiFlowInstaller).GetMethod("RefreshParticipantState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Installer, null);
            }
            public void Dispose()
            {
                Popups.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        private sealed class OrdinaryFailurePort : IParticipantResetPort
        {
            public event Action Changed { add { } remove { } }
            public bool BlocksMenu => true;
            public bool IsBusy => false;
            public bool CanRequest => false;
            public bool SuppressSaveSeedImport => true;
            public string Error => "Ordinary recovery failure";
            public int Restarts;
            public Task PrepareMenuAsync() => Task.CompletedTask;
            public void CompleteMenuInitialization() { }
            public void LeaveMenu() { }
            public void FailMenuInitialization(string reason) { }
            public void RequestReset() => Assert.Fail("Unexpected reset.");
            public void Restart() => Restarts++;
        }
        private sealed class PopupFactory : IPopupRuntimeFactory
        {
            public int Created;
            public PopupRuntime Last;
            public PopupRuntimeFactoryResult Create(PopupRequest request)
            {
                Created++; Last = new PopupRuntime();
                return new PopupRuntimeFactoryResult(new PopupPolicy(PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen, PopupBackAction.Cancel, PopupBackdropMode.Consume, true, true), Last);
            }
        }
        private sealed class PopupRuntime : IPopupRuntime
        {
            public event Action<PopupCompletionKind> CompletionRequested;
            public bool Disposed;
            public void Emit(PopupCompletionKind result) => CompletionRequested?.Invoke(result);
            public void SetIsTopmost(bool value) { }
            public void Dispose() => Disposed = true;
        }
    }
}
