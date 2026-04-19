using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Shared.Display;
using NUnit.Framework;
using UiDisplayWindowMode = Game.Feature.UI.Application.DisplayWindowMode;
using SharedDisplayWindowMode = Game.Shared.Display.DisplayWindowMode;

namespace Game.Feature.UI.Tests
{
    public sealed class DisplaySettingsBridgeTests
    {
        [Test]
        public void DisplayWindowMode_PublicSurface_RemainsVisibleScopeOnly()
        {
            Assert.That(Enum.GetNames(typeof(UiDisplayWindowMode)), Is.EqualTo(new[] { "Windowed", "FullScreenWindow" }));
        }

        [TestCase(UiDisplayWindowMode.Windowed, SharedDisplayWindowMode.Windowed)]
        [TestCase(UiDisplayWindowMode.FullScreenWindow, SharedDisplayWindowMode.FullScreenWindow)]
        public void UIDisplayWindowModeMapper_MapsVisibleModes_ToSingleSharedTruth(
            UiDisplayWindowMode source,
            SharedDisplayWindowMode expected)
        {
            Assert.That(UIDisplayWindowModeMapper.ToShared(source), Is.EqualTo(expected));
            Assert.That(UIDisplayWindowModeMapper.ToVisible(expected), Is.EqualTo(source));
        }

        [Test]
        public void DisplaySettingsPortAdapter_ReadAndWrite_UseBridgeOwnedMapping()
        {
            var service = new RecordingDisplaySettingsService(
                current: new DisplaySettingsSnapshot(1600, 900, SharedDisplayWindowMode.FullScreenWindow, 144, 1),
                committed: new DisplaySettingsSnapshot(1920, 1080, SharedDisplayWindowMode.Windowed, 60, 1),
                availableModes: new[]
                {
                    new DisplayModeOption(1920, 1080, 60, 1),
                    new DisplayModeOption(1600, 900, 120, 1),
                    new DisplayModeOption(1280, 720, 60, 1),
                });
            var adapter = new DisplaySettingsPortAdapter(service);

            var snapshot = adapter.Read();
            var started = adapter.BeginPreview(new DisplaySettingsPortPreviewRequest(1, UiDisplayWindowMode.FullScreenWindow));
            var committed = adapter.CommitPreview();
            var reverted = adapter.RevertPreview();

            Assert.That(snapshot.CommittedModeIndex, Is.EqualTo(0));
            Assert.That(snapshot.CurrentRuntimeResolutionLabel, Is.EqualTo("1600 x 900"));
            Assert.That(snapshot.CurrentRuntimeWindowMode, Is.EqualTo(UiDisplayWindowMode.FullScreenWindow));
            Assert.That(started, Is.True);
            Assert.That(service.LastPreviewSnapshot.Width, Is.EqualTo(1600));
            Assert.That(service.LastPreviewSnapshot.Height, Is.EqualTo(900));
            Assert.That(service.LastPreviewSnapshot.WindowMode, Is.EqualTo(SharedDisplayWindowMode.FullScreenWindow));
            Assert.That(committed, Is.True);
            Assert.That(reverted, Is.True);
            Assert.That(service.CommitPreviewCallCount, Is.EqualTo(1));
            Assert.That(service.RevertPreviewCallCount, Is.EqualTo(1));
        }

        [Test]
        public void DisplaySettingsPortAdapter_Source_UsesCentralMapper_InsteadOfInlineSharedWindowModeLiterals()
        {
            var sourcePath = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Features/UI/UI_Composition/Runtime/DisplaySettingsPortAdapter.cs");
            var source = File.ReadAllText(sourcePath);

            StringAssert.Contains("UIDisplayWindowModeMapper.ToVisible", source);
            StringAssert.Contains("UIDisplayWindowModeMapper.ToShared", source);
            Assert.That(source, Does.Not.Contain("SharedDisplayWindowMode.Windowed"));
            Assert.That(source, Does.Not.Contain("SharedDisplayWindowMode.FullScreenWindow"));
        }

        private sealed class RecordingDisplaySettingsService : IDisplaySettingsService
        {
            private readonly IReadOnlyList<DisplayModeOption> availableModes;
            private readonly DisplaySettingsSnapshot committed;
            private readonly DisplaySettingsSnapshot current;

            public RecordingDisplaySettingsService(
                DisplaySettingsSnapshot current,
                DisplaySettingsSnapshot committed,
                IReadOnlyList<DisplayModeOption> availableModes)
            {
                this.current = current;
                this.committed = committed;
                this.availableModes = availableModes;
            }

            public bool IsPreviewActive => false;

            public bool HasBootApplied => false;

            public int CommitPreviewCallCount { get; private set; }

            public int RevertPreviewCallCount { get; private set; }

            public DisplaySettingsSnapshot LastPreviewSnapshot { get; private set; }

            public DisplaySettingsSnapshot ReadCurrentDisplaySettings()
            {
                return current;
            }

            public DisplaySettingsSnapshot ReadCommittedDisplaySettings()
            {
                return committed;
            }

            public IReadOnlyList<DisplayModeOption> ReadAvailableDisplayModes()
            {
                return availableModes;
            }

            public bool ApplyBootSettingsOnce()
            {
                return true;
            }

            public bool BeginPreview(DisplaySettingsSnapshot snapshot)
            {
                LastPreviewSnapshot = snapshot;
                return true;
            }

            public bool CommitPreview()
            {
                CommitPreviewCallCount++;
                return true;
            }

            public bool RevertPreview()
            {
                RevertPreviewCallCount++;
                return true;
            }
        }
    }
}
