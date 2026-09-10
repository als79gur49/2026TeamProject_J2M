using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Exhibition.RestartExperiment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Game.Exhibition.Tests.PlayMode
{
    public sealed class OverlayObservationPresentationTests
    {
        [UnityTest, Category("Full"), Timeout(120000)]
        public IEnumerator RealPanelRendersReportAndExitWithoutCaptureInputOrNativeLaunch()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Observation panel requires graphical Editor review. Batch skip does not validate rendering, scrolling, mouse, keyboard or exit interaction.");
            string directory = Path.Combine(@"D:\J2M\evidence\overlay-handoff-panel", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(directory);
            var fake = new Effects();
            var flow = new OverlayHandoffObservation(fake, OverlayObservationRole.OriginObserver);
            var panel = OverlayHandoffObservationPresentation.Attach(flow);
            int width = Screen.width, height = Screen.height;
            var mode = Screen.fullScreenMode;
            var cameraObject = new GameObject("Observation fake panel camera");
            cameraObject.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            try
            {
                Screen.SetResolution(640, 360, FullScreenMode.Windowed);
                yield return null;
                yield return Capture(directory, "ready");
                Assert.That(flow.CanReplace, Is.False, "A rendered panel is not external Overlay evidence.");
                var reported = flow.ReportAsync(OverlayVisibility.Opened);
                while (!reported.IsCompleted) yield return null;
                Assert.That(flow.CanReplace, Is.True);
                yield return Capture(directory, "report-stored");
                fake.Clock = 3600; flow.Tick();
                Assert.That(flow.CanReplace, Is.True);
                var closed = flow.CloseAsync();
                while (!closed.IsCompleted) yield return null;
                Assert.That(fake.Quits, Is.EqualTo(1));
                Assert.That(fake.Helpers, Is.Zero);
                File.WriteAllText(Path.Combine(directory, "review.json"), "{\"fakePanelCaptured\":true,\"steamOverlayTested\":false,\"manualInputVerified\":false}");
            }
            finally
            {
                UnityEngine.Object.Destroy(panel.gameObject); UnityEngine.Object.Destroy(cameraObject);
                Screen.SetResolution(width, height, mode);
            }
        }
        private static IEnumerator Capture(string directory, string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { Assert.That(texture, Is.Not.Null); File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG()); }
            finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
        }
        private sealed class Effects : IOverlayHandoffObservationRuntime
        {
            public double Clock;
            public int Helpers, Quits;
            public double Now => Clock;
            public string ObservationFailure => null;
            public bool NativeReady() => true;
            public bool ReceiptReady() => true;
            public Task PrepareAsync(Action guard) { guard(); return Task.CompletedTask; }
            public Task RevalidateAsync(Action guard) { guard(); return Task.CompletedTask; }
            public void Claim() { }
            public HandoffResult StartHelper(Action guard) { guard(); Helpers++; return HandoffResult.NotStarted; }
            public void Record(string stage, object value = null) { }
            public Task SaveObservationAsync(OverlayVisibility visibility) => Task.CompletedTask;
            public Task DelayAsync() => Task.CompletedTask;
            public void FinalSnapshot() { }
            public void Quit() { Quits++; }
        }
    }
}
