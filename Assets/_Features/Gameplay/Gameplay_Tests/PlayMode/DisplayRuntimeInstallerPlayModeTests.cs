using System.Reflection;
using Game.Shared.Display;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class DisplayRuntimeInstallerPlayModeTests
    {
        [Test]
        [Category("Full")]
        public void DisplayRuntimeInstaller_AwakeAndInstall_AreIdempotent_WithoutBootSave()
        {
            var rootObject = new GameObject("DisplayRuntimeInstaller_AwakeAndInstall_AreIdempotent_WithoutBootSave");
            try
            {
                var store = new RecordingDisplaySettingsStore(
                    hasSavedSnapshot: true,
                    savedSnapshot: new DisplaySettingsSnapshot(1600, 900, DisplayWindowMode.FullScreenWindow, 60, 1));
                var gateway = new FakeDisplayRuntimeGateway(
                    current: new DisplaySettingsSnapshot(1280, 720, DisplayWindowMode.Windowed, 60, 1),
                    supported: new[]
                    {
                        CreateResolution(1280, 720),
                        CreateResolution(1600, 900),
                    });
                var service = new DisplaySettingsService(store, gateway);
                var cursorGateway = new FakeCursorConfinementRuntimeGateway(
                    DisplayWindowMode.Windowed,
                    CursorLockMode.None);
                var cursorPolicy = new FullscreenCursorConfinementPolicy(cursorGateway);
                var installer = rootObject.AddComponent<DisplayRuntimeInstaller>();
                installer.SetDisplaySettingsServiceForTesting(service);
                installer.SetFullscreenCursorConfinementPolicyForTesting(cursorPolicy);

                InvokePrivateMethod(installer, "Awake");
                installer.Install();

                Assert.That(installer.HasBootApplied, Is.True);
                Assert.That(gateway.ApplyCallCount, Is.EqualTo(1));
                Assert.That(store.SaveCallCount, Is.Zero);
                Assert.That(cursorGateway.SetLockStateCallCount, Is.Zero);

                cursorGateway.WindowMode = DisplayWindowMode.FullScreenWindow;
                InvokePrivateMethod(installer, "Update");
                Assert.That(cursorGateway.LockState, Is.EqualTo(CursorLockMode.Confined));

                InvokePrivateMethod(installer, "OnApplicationFocus", false);
                Assert.That(cursorGateway.LockState, Is.EqualTo(CursorLockMode.None));

                InvokePrivateMethod(installer, "OnApplicationFocus", true);
                Assert.That(cursorGateway.LockState, Is.EqualTo(CursorLockMode.Confined));

                InvokePrivateMethod(installer, "OnApplicationPause", true);
                Assert.That(cursorGateway.LockState, Is.EqualTo(CursorLockMode.None));

                cursorGateway.IsApplicationFocused = true;
                InvokePrivateMethod(installer, "OnApplicationPause", false);
                Assert.That(cursorGateway.LockState, Is.EqualTo(CursorLockMode.Confined));

                cursorGateway.WindowMode = DisplayWindowMode.Windowed;
                InvokePrivateMethod(installer, "Update");
                Assert.That(cursorGateway.LockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(cursorGateway.SetLockStateCallCount, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }

        private static Resolution CreateResolution(int width, int height)
        {
            var boxed = (object)new Resolution();
            SetResolutionMember(ref boxed, "width", width);
            SetResolutionMember(ref boxed, "height", height);
            TrySetResolutionMember(ref boxed, "refreshRate", 60);
            return (Resolution)boxed;
        }

        private static void SetResolutionMember(ref object boxedResolution, string memberName, int value)
        {
            if (TrySetResolutionMember(ref boxedResolution, memberName, value))
            {
                return;
            }

            throw new System.InvalidOperationException($"Unable to set Resolution member '{memberName}'.");
        }

        private static bool TrySetResolutionMember(ref object boxedResolution, string memberName, int value)
        {
            var field = typeof(Resolution).GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(boxedResolution, value);
                return true;
            }

            var property = typeof(Resolution).GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(boxedResolution, value, null);
                return true;
            }

            return false;
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

            public FakeDisplayRuntimeGateway(DisplaySettingsSnapshot current, Resolution[] supported)
            {
                CurrentSnapshot = current;
                this.supported = supported;
            }

            public int ApplyCallCount { get; private set; }

            public DisplaySettingsSnapshot CurrentSnapshot { get; private set; }

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
                CurrentSnapshot = snapshot;
            }
        }

        private sealed class FakeCursorConfinementRuntimeGateway : ICursorConfinementRuntimeGateway
        {
            public FakeCursorConfinementRuntimeGateway(
                DisplayWindowMode windowMode,
                CursorLockMode lockState)
            {
                WindowMode = windowMode;
                LockState = lockState;
            }

            public bool SupportsConfinement => true;

            public bool IsApplicationFocused { get; set; } = true;

            public DisplayWindowMode WindowMode { get; set; }

            public CursorLockMode LockState { get; private set; }

            public int SetLockStateCallCount { get; private set; }

            public DisplayWindowMode ReadCurrentWindowMode()
            {
                return WindowMode;
            }

            public CursorLockMode ReadCurrentLockState()
            {
                return LockState;
            }

            public void SetLockState(CursorLockMode lockState)
            {
                SetLockStateCallCount++;
                LockState = lockState;
            }
        }
    }
}
