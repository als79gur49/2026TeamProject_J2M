using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Exhibition.RestartExperiment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Game.Exhibition.Tests.PlayMode
{
    public sealed class ResetOverlayPresentationTests
    {
        [UnityTest, Category("Full"), Timeout(120000)]
        public IEnumerator ActualPanelRendersRequiredReportAndScopeWithoutExternalEffects()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Interactive fake panel review remains required for small screens, keyboard and mouse. Batch skip is not visual acceptance.");
            var effects = new Effects();
            var flow = new ResetOverlayTrial(new ExhibitionResetCoordinator(effects, effects, effects), effects, ResetOverlayRole.Initiator);
            var panel = ResetOverlayTrialPresentation.Attach(flow, null);
            int width = Screen.width, height = Screen.height; var mode = Screen.fullScreenMode;
            try
            {
                Screen.SetResolution(640, 360, FullScreenMode.Windowed);
                yield return null; yield return null;
                Assert.That(flow.CanReport, Is.True); Assert.That(flow.CanRequest, Is.False);
                var saved = flow.ReportAsync("opened", Enumerable.Repeat("earned", 5).ToArray());
                while (!saved.IsCompleted) yield return null;
                yield return null;
                Assert.That(flow.ReportSaved, Is.True); Assert.That(flow.CanRequest, Is.False);
                flow.ScopeConfirmed = true;
                Assert.That(flow.CanRequest, Is.True);
                var close = flow.CloseAsync(); while (!close.IsCompleted) yield return null;
                Assert.That(effects.Quits, Is.EqualTo(1)); Assert.That(effects.Writes, Is.Zero);
            }
            finally { UnityEngine.Object.Destroy(panel.gameObject); Screen.SetResolution(width, height, mode); }
        }
        [UnityTest, Category("Full")]
        public IEnumerator ActualPanelPreparationFallbackAndCloseHaveNoLateEffects()
        {
            var effects = new Effects { Gate = new TaskCompletionSource<bool>() };
            var flow = new ResetOverlayTrial(new ExhibitionResetCoordinator(effects, effects, effects), effects, ResetOverlayRole.Initiator);
            var panel = ResetOverlayTrialPresentation.Attach(flow, null);
            try
            {
                yield return null;
                Assert.That(flow.IsBusy, Is.True);
                var closed = flow.CloseAsync(); effects.Gate.SetResult(true);
                while (!closed.IsCompleted) yield return null;
                Assert.That(effects.Prepares, Is.Zero); Assert.That(effects.Writes, Is.Zero); Assert.That(effects.Quits, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.Destroy(panel.gameObject); }
        }
        [UnityTest, Category("Full")]
        public IEnumerator ActualPanelKeyboardRequiresReportThenScopeAndPreservesOverlayShortcut()
        {
            var effects = new Effects();
            var flow = new ResetOverlayTrial(new ExhibitionResetCoordinator(effects, effects, effects), effects, ResetOverlayRole.Initiator);
            var panel = ResetOverlayTrialPresentation.Attach(flow, null);
            try
            {
                yield return null;
                Assert.That(panel.HandleKey(KeyCode.Tab, true), Is.False);
                panel.HandleKey(KeyCode.Tab); panel.HandleKey(KeyCode.DownArrow); // opened
                for (int i = 0; i < 5; i++) { panel.HandleKey(KeyCode.Tab); panel.HandleKey(KeyCode.DownArrow); }
                Assert.That(flow.ReportSaved, Is.False); Assert.That(flow.CanRequest, Is.False);
                panel.HandleKey(KeyCode.Tab); panel.HandleKey(KeyCode.Return);
                Assert.That(flow.ReportSaved, Is.True); Assert.That(flow.CanRequest, Is.False);
                panel.HandleKey(KeyCode.Tab); panel.HandleKey(KeyCode.Space);
                Assert.That(flow.CanRequest, Is.True);
                panel.HandleKey(KeyCode.Tab); panel.HandleKey(KeyCode.Tab); panel.HandleKey(KeyCode.Return);
                yield return null;
                Assert.That(effects.Quits, Is.EqualTo(1)); Assert.That(effects.Writes, Is.Zero);
            }
            finally { UnityEngine.Object.Destroy(panel.gameObject); }
        }
        private sealed class Effects : IResetOverlayTrialRuntime, IExhibitionResetJournal, IExhibitionSteamReset, IParticipantProgressReset
        {
            public int Writes, Quits, Prepares;
            public TaskCompletionSource<bool> Gate;
            public bool Available => true;
            public string CoreFailure => null;
            public string[] Targets => ResetOverlayWire.Names();
            public string BaselineSummary => string.Join("\n", Targets);
            public async Task WaitAvailableAsync(Action guard) { if (Gate != null) await Gate.Task; guard(); }
            public Task PrepareAsync(ResetRecord record, Action guard) { guard(); Prepares++; return Task.CompletedTask; }
            public Task RevalidateAsync(Action guard) => throw new InvalidOperationException("No visual-fixture handoff.");
            public void ValidateRecord(ResetRecord record, bool pending) => throw new InvalidOperationException("No visual-fixture reset.");
            public void Claim(string step) => throw new InvalidOperationException("No visual-fixture claim.");
            public void ConfirmScope() => throw new InvalidOperationException("No visual-fixture reset.");
            public Task BindPendingAsync(ResetRecord record, Action guard) => throw new InvalidOperationException();
            public Task VerifyResetAsync(ResetRecord record, Action guard) => throw new InvalidOperationException();
            public Task SaveReportAsync(string visibility, string[] judgments) => Task.CompletedTask;
            public HandoffResult StartHelper(Action guard, Action finalGuard) => throw new InvalidOperationException();
            public void Record(string stage) { }
            public void Failure(Exception error) { }
            public Task FinalSnapshotAsync() => Task.CompletedTask;
            public void Quit() => Quits++;
            public ResetRecord Load() => new ResetRecord { State = ResetRecord.Ready, OperationId = "0123456789abcdef0123456789abcdef", AppId = 123, SteamId = 456, MappingVersion = ExhibitionResetCoordinator.MappingVersion };
            public void Save(ResetRecord record) { Writes++; throw new InvalidOperationException(); }
            public ResetIdentity GetIdentity() => new ResetIdentity(123, 456);
            public Task ResetAsync(ResetIdentity identity) { Writes++; throw new InvalidOperationException(); }
            public void Reset() { Writes++; throw new InvalidOperationException(); }
        }
    }
}
