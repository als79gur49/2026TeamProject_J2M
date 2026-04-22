using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Shared.Display;
using NUnit.Framework;
using UnityEngine;
using DisplayPreviewCountdownSnapshot = Game.Feature.UI.Application.DisplayPreviewCountdownSnapshot;

namespace Game.Feature.UI.Tests
{
    public sealed class DisplaySettingsRuntimeHardeningTests
    {
        [SetUp]
        public void SetUp()
        {
            DeleteDisplayPlayerPrefsKeys();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteDisplayPlayerPrefsKeys();
        }

        [Test]
        public void PlayerPrefsDisplaySettingsStore_RoundTripsSnapshot()
        {
            var store = new PlayerPrefsDisplaySettingsStore();
            var snapshot = new DisplaySettingsSnapshot(1920, 1080, DisplayWindowMode.FullScreenWindow, 144, 1);

            store.Save(snapshot);

            Assert.That(store.TryLoad(out var loaded), Is.True);
            Assert.That(loaded, Is.EqualTo(snapshot));
        }

        [Test]
        public void DisplaySettingsService_CatalogDedupe_ReturnsLargestVisibleModesFirst_AndUpgradesHighestRefresh()
        {
            var current = new DisplaySettingsSnapshot(1600, 900, DisplayWindowMode.Windowed, 60, 1);
            var catalog = DisplaySettingsService.BuildCatalog(
                current,
                new[]
                {
                    CreateResolution(1280, 720, 60),
                    CreateResolution(1920, 1080, 60),
                    CreateResolution(1280, 720, 144),
                    CreateResolution(1600, 900, 75),
                });

            Assert.That(catalog, Has.Length.EqualTo(3));
            Assert.That(catalog[0].VisibleLabel, Is.EqualTo("1920 x 1080"));
            Assert.That(catalog[1].VisibleLabel, Is.EqualTo("1600 x 900"));
            Assert.That(catalog[2].VisibleLabel, Is.EqualTo("1280 x 720"));
            Assert.That(catalog[2].PreferredRefreshRate, Is.EqualTo(144));
        }

        [Test]
        public void DisplaySettingsService_BootApply_NormalizesInvalidSavedResolution_WithoutSaving_AndRunsOnce()
        {
            var store = new RecordingDisplaySettingsStore(
                hasSavedSnapshot: true,
                savedSnapshot: new DisplaySettingsSnapshot(2560, 1440, DisplayWindowMode.FullScreenWindow, 60, 1));
            var gateway = new FakeDisplayRuntimeGateway(
                current: new DisplaySettingsSnapshot(1600, 900, DisplayWindowMode.Windowed, 60, 1),
                supported: new[]
                {
                    CreateResolution(1280, 720, 60),
                    CreateResolution(1600, 900, 75),
                });
            var service = new DisplaySettingsService(store, gateway);

            Assert.That(service.ApplyBootSettingsOnce(), Is.True);
            Assert.That(service.ApplyBootSettingsOnce(), Is.False);
            Assert.That(gateway.ApplyCallCount, Is.EqualTo(1));
            Assert.That(gateway.LastAppliedSnapshot.Width, Is.EqualTo(1600));
            Assert.That(gateway.LastAppliedSnapshot.Height, Is.EqualTo(900));
            Assert.That(gateway.LastAppliedSnapshot.WindowMode, Is.EqualTo(DisplayWindowMode.FullScreenWindow));
            Assert.That(store.SaveCallCount, Is.Zero);
            Assert.That(service.HasBootApplied, Is.True);
        }

        [Test]
        public void DisplaySettingsService_BootApply_SkipsRuntimeReapply_WhenNormalizedTargetAlreadyMatches()
        {
            var store = new RecordingDisplaySettingsStore(
                hasSavedSnapshot: true,
                savedSnapshot: new DisplaySettingsSnapshot(1600, 900, DisplayWindowMode.Windowed, 60, 1));
            var gateway = new FakeDisplayRuntimeGateway(
                current: new DisplaySettingsSnapshot(1600, 900, DisplayWindowMode.Windowed, 75, 1),
                supported: new[]
                {
                    CreateResolution(1280, 720, 60),
                    CreateResolution(1600, 900, 75),
                });
            var service = new DisplaySettingsService(store, gateway);

            Assert.That(service.ApplyBootSettingsOnce(), Is.True);
            Assert.That(gateway.ApplyCallCount, Is.Zero);
            Assert.That(store.SaveCallCount, Is.Zero);
        }

        [Test]
        public void DisplaySettingsService_PreviewFlow_KeepsCommittedSeparate_FromPreviewAndSave()
        {
            var store = new RecordingDisplaySettingsStore(
                hasSavedSnapshot: false,
                savedSnapshot: default);
            var gateway = new FakeDisplayRuntimeGateway(
                current: new DisplaySettingsSnapshot(1920, 1080, DisplayWindowMode.Windowed, 60, 1),
                supported: new[]
                {
                    CreateResolution(1280, 720, 60),
                    CreateResolution(1920, 1080, 60),
                });
            var service = new DisplaySettingsService(store, gateway);

            Assert.That(service.BeginPreview(new DisplaySettingsSnapshot(1280, 720, DisplayWindowMode.FullScreenWindow, 60, 1)), Is.True);
            Assert.That(service.IsPreviewActive, Is.True);
            Assert.That(service.ReadCommittedDisplaySettings().Width, Is.EqualTo(1920));
            Assert.That(gateway.CurrentSnapshot.Width, Is.EqualTo(1280));
            Assert.That(store.SaveCallCount, Is.Zero);

            Assert.That(service.RevertPreview(), Is.True);
            Assert.That(service.IsPreviewActive, Is.False);
            Assert.That(gateway.CurrentSnapshot.Width, Is.EqualTo(1920));
            Assert.That(store.SaveCallCount, Is.Zero);

            Assert.That(service.BeginPreview(new DisplaySettingsSnapshot(1280, 720, DisplayWindowMode.FullScreenWindow, 60, 1)), Is.True);
            Assert.That(service.CommitPreview(), Is.True);
            Assert.That(service.IsPreviewActive, Is.False);
            Assert.That(service.ReadCommittedDisplaySettings().Width, Is.EqualTo(1280));
            Assert.That(service.ReadCommittedDisplaySettings().WindowMode, Is.EqualTo(DisplayWindowMode.FullScreenWindow));
            Assert.That(store.SaveCallCount, Is.EqualTo(1));
        }

        [Test]
        public void DisplayPreviewSessionHost_ConfirmAndScreenTransition_ClearSessionOnce()
        {
            var rootObject = new GameObject("DisplayPreviewSessionHost_ConfirmAndScreenTransition_ClearSessionOnce");
            try
            {
                double now = 0d;
                var relay = rootObject.AddComponent<DisplayPreviewTimeoutRelay>();
                relay.SetTimeProviderForTesting(() => now);
                var runtimeFactory = new TestPopupRuntimeFactory();
                var popupController = new PopupController(runtimeFactory);
                var host = new DisplayPreviewSessionHost(popupController, relay);
                var confirmCount = 0;
                var cancelCount = 0;

                Assert.That(host.TryOpen(
                    new ConfirmPopupPayload("Confirm", "Body", "Keep", "Revert", false),
                    () => confirmCount++,
                    () => cancelCount++), Is.True);

                runtimeFactory.LastRuntime.Trigger(PopupCompletionKind.Confirmed);
                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(cancelCount, Is.EqualTo(0));
                Assert.That(host.HasActiveSession, Is.False);
                Assert.That(relay.IsArmed, Is.False);
                Assert.That(host.CancelActivePreview(), Is.False);

                Assert.That(host.TryOpen(
                    new ConfirmPopupPayload("Confirm", "Body", "Keep", "Revert", false),
                    () => confirmCount++,
                    () => cancelCount++), Is.True);

                popupController.CloseAll(PopupCloseReason.ScreenTransition);
                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(cancelCount, Is.EqualTo(1));
                Assert.That(host.HasActiveSession, Is.False);
                Assert.That(relay.IsArmed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DisplayPreviewSessionHost_TimeoutAndCancel_AreIdempotent_AcrossSessions()
        {
            var rootObject = new GameObject("DisplayPreviewSessionHost_TimeoutAndCancel_AreIdempotent_AcrossSessions");
            try
            {
                double now = 0d;
                var relay = rootObject.AddComponent<DisplayPreviewTimeoutRelay>();
                relay.SetTimeProviderForTesting(() => now);
                var runtimeFactory = new TestPopupRuntimeFactory();
                var popupController = new PopupController(runtimeFactory);
                var host = new DisplayPreviewSessionHost(popupController, relay);
                var cancelCount = 0;

                host.TryOpen(
                    new ConfirmPopupPayload("Confirm", "Body", "Keep", "Revert", false),
                    () => { },
                    () => cancelCount++);

                now = 20d;
                InvokePrivateMethod(relay, "Update");
                InvokePrivateMethod(relay, "Update");

                Assert.That(cancelCount, Is.EqualTo(1));
                Assert.That(host.HasActiveSession, Is.False);
                Assert.That(relay.IsArmed, Is.False);

                host.TryOpen(
                    new ConfirmPopupPayload("Confirm", "Body", "Keep", "Revert", false),
                    () => { },
                    () => cancelCount++);
                Assert.That(host.CancelActivePreview(), Is.True);
                Assert.That(host.CancelActivePreview(), Is.False);
                Assert.That(cancelCount, Is.EqualTo(2));
                Assert.That(host.HasActiveSession, Is.False);
                Assert.That(relay.IsArmed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DisplayPreviewSessionHost_ExposesSingleTimeoutSource_AndSeedsCountdownAfterSuccessfulOpen()
        {
            var rootObject = new GameObject("DisplayPreviewSessionHost_ExposesSingleTimeoutSource");
            try
            {
                double now = 0d;
                var relay = rootObject.AddComponent<DisplayPreviewTimeoutRelay>();
                relay.SetTimeProviderForTesting(() => now);
                var runtimeFactory = new TestPopupRuntimeFactory();
                var popupController = new PopupController(runtimeFactory);
                var host = new DisplayPreviewSessionHost(popupController, relay, 21d);
                var snapshots = new List<DisplayPreviewCountdownSnapshot>();
                host.CountdownChanged += snapshots.Add;

                Assert.That(host.PreviewTimeoutSeconds, Is.EqualTo(21d));
                Assert.That(host.TryOpen(
                    new ConfirmPopupPayload("Confirm", "Body", "Keep", "Revert", false),
                    () => { },
                    () => { }), Is.True);

                Assert.That(snapshots, Has.Count.EqualTo(1));
                Assert.That(snapshots[0].IsActive, Is.True);
                Assert.That(snapshots[0].RemainingSeconds, Is.EqualTo(21));
                Assert.That(snapshots[0].TotalSeconds, Is.EqualTo(21));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DisplayPreviewTimeoutRelay_EmitsOnlyOnWholeSecondBucketChanges_AndClearsOnce()
        {
            var rootObject = new GameObject("DisplayPreviewTimeoutRelay_WholeSecondBuckets");
            try
            {
                double now = 0d;
                var relay = rootObject.AddComponent<DisplayPreviewTimeoutRelay>();
                relay.SetTimeProviderForTesting(() => now);
                var snapshots = new List<DisplayPreviewCountdownSnapshot>();
                relay.CountdownChanged += snapshots.Add;

                relay.Arm(15d, () => { });

                now = 0.8d;
                InvokePrivateMethod(relay, "Update");
                Assert.That(snapshots, Is.Empty);

                now = 1.1d;
                InvokePrivateMethod(relay, "Update");
                Assert.That(snapshots, Has.Count.EqualTo(1));
                Assert.That(snapshots[0].RemainingSeconds, Is.EqualTo(14));
                Assert.That((float)snapshots[0].RemainingSeconds / snapshots[0].TotalSeconds, Is.EqualTo(14f / 15f).Within(0.0001f));

                now = 1.8d;
                InvokePrivateMethod(relay, "Update");
                Assert.That(snapshots, Has.Count.EqualTo(1));

                relay.Cancel();
                Assert.That(snapshots, Has.Count.EqualTo(2));
                Assert.That(snapshots[1].IsActive, Is.False);

                relay.Cancel();
                Assert.That(snapshots, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static void DeleteDisplayPlayerPrefsKeys()
        {
            PlayerPrefs.DeleteKey("settings.display.width");
            PlayerPrefs.DeleteKey("settings.display.height");
            PlayerPrefs.DeleteKey("settings.display.windowMode");
            PlayerPrefs.DeleteKey("settings.display.refreshNumerator");
            PlayerPrefs.DeleteKey("settings.display.refreshDenominator");
        }

        private static Resolution CreateResolution(int width, int height, int refreshRate)
        {
            var boxed = (object)new Resolution();
            SetResolutionMember(ref boxed, "width", width);
            SetResolutionMember(ref boxed, "height", height);

            if (!TrySetRefreshRateRatio(ref boxed, refreshRate))
            {
                SetResolutionMember(ref boxed, "refreshRate", refreshRate);
            }

            return (Resolution)boxed;
        }

        private static bool TrySetRefreshRateRatio(ref object boxedResolution, int refreshRate)
        {
            var property = typeof(Resolution).GetProperty("refreshRateRatio", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite)
            {
                return false;
            }

            var ratio = Activator.CreateInstance(property.PropertyType);
            SetMember(property.PropertyType, ref ratio, "numerator", refreshRate);
            SetMember(property.PropertyType, ref ratio, "denominator", 1);
            property.SetValue(boxedResolution, ratio, null);
            return true;
        }

        private static void SetResolutionMember(ref object boxedResolution, string memberName, int value)
        {
            SetMember(typeof(Resolution), ref boxedResolution, memberName, value);
        }

        private static void SetMember(Type declaringType, ref object boxedValue, string memberName, int value)
        {
            var field = declaringType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(boxedValue, ConvertToMemberType(field.FieldType, value));
                return;
            }

            var property = declaringType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(boxedValue, ConvertToMemberType(property.PropertyType, value), null);
                return;
            }

            throw new InvalidOperationException($"Unable to set Resolution member '{memberName}'.");
        }

        private static object ConvertToMemberType(Type memberType, int value)
        {
            var targetType = Nullable.GetUnderlyingType(memberType) ?? memberType;
            if (targetType == typeof(int))
            {
                return value;
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private sealed class RecordingDisplaySettingsStore : IDisplaySettingsStore
        {
            private readonly bool hasSavedSnapshot;
            private readonly DisplaySettingsSnapshot savedSnapshot;

            public RecordingDisplaySettingsStore(
                bool hasSavedSnapshot,
                DisplaySettingsSnapshot savedSnapshot)
            {
                this.hasSavedSnapshot = hasSavedSnapshot;
                this.savedSnapshot = savedSnapshot;
            }

            public int SaveCallCount { get; private set; }

            public bool TryLoad(out DisplaySettingsSnapshot snapshot)
            {
                snapshot = savedSnapshot;
                return hasSavedSnapshot;
            }

            public void Save(DisplaySettingsSnapshot snapshot)
            {
                SaveCallCount++;
            }
        }

        private sealed class FakeDisplayRuntimeGateway : IDisplayRuntimeGateway
        {
            private readonly Resolution[] supported;

            public FakeDisplayRuntimeGateway(
                DisplaySettingsSnapshot current,
                Resolution[] supported)
            {
                CurrentSnapshot = current;
                this.supported = supported;
            }

            public int ApplyCallCount { get; private set; }

            public DisplaySettingsSnapshot CurrentSnapshot { get; private set; }

            public DisplaySettingsSnapshot LastAppliedSnapshot { get; private set; }

            public DisplaySettingsSnapshot ReadCurrentDisplaySettings()
            {
                return CurrentSnapshot;
            }

            public Resolution[] ReadSupportedResolutions()
            {
                return supported;
            }

            public void ApplyDisplaySettings(DisplaySettingsSnapshot snapshot)
            {
                ApplyCallCount++;
                LastAppliedSnapshot = snapshot;
                CurrentSnapshot = snapshot;
            }
        }

        private sealed class TestPopupRuntimeFactory : IPopupRuntimeFactory
        {
            public TestPopupRuntime LastRuntime { get; private set; }

            public PopupRuntimeFactoryResult Create(PopupRequest request)
            {
                LastRuntime = new TestPopupRuntime();
                return new PopupRuntimeFactoryResult(
                    new PopupPolicy(
                        PopupPolicyClass.ModalBlocking,
                        PopupLifetimeScope.CurrentScreen,
                        PopupBackAction.Cancel,
                        PopupBackdropMode.Consume,
                        showsDim: true,
                        blocksLowerLayers: true),
                    LastRuntime);
            }
        }

        private sealed class TestPopupRuntime : IPopupRuntime
        {
            public event Action<PopupCompletionKind> CompletionRequested;

            public void Trigger(PopupCompletionKind completionKind)
            {
                CompletionRequested?.Invoke(completionKind);
            }

            public void Dispose()
            {
            }

            public void SetIsTopmost(bool isTopmost)
            {
            }
        }
    }
}
