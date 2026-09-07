using System;
using System.IO;
using Game.Feature.UI.Composition.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Game.Exhibition.Tests
{
    public sealed class ParticipantMenuPreviewTests
    {
        [Test, Category("Full")]
        public void AuthoredMenuRendersLocalizedParticipantCommand()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Prefab preview requires UNITY_GRAPHICS=1.");
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-codexResultPath");
            if (index < 0) Assert.Ignore("Capture output must be assigned by the repository lane.");
            var result = TypographyPreviewScreenshotUtility.CaptureScreenshots(
                new[] { new TypographyPreviewScreenshotTarget("Main Menu", "MainMenu",
                    "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab") },
                new[] { "ko-KR", "en-US" },
                Path.Combine(Path.GetDirectoryName(args[index + 1]), "participant-menu-preview"));
            Assert.That(result.HasErrors, Is.False, "Prefab capture and localization validation must succeed.");
            Assert.That(result.Captures.Count, Is.EqualTo(2));
        }
    }
}
