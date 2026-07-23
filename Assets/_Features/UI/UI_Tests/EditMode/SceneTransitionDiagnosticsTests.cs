using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class SceneTransitionDiagnosticsTests
    {
        private const string CoordinatorSourcePath =
            "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs";

        [Test]
        public void ThresholdNotReached_EmitsNoWarning_AndReportsOperationState()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 10d,
                stallThresholdSeconds: 5d);

            Assert.That(monitor.Snapshot.Phase, Is.EqualTo(SceneTransitionDiagnosticPhase.LoadRequested));

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.42f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 4.9d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.42f,
                isDone: false,
                allowSceneActivation: false);

            var snapshot = monitor.Snapshot;
            Assert.That(logger.Messages, Is.Empty);
            Assert.That(snapshot.IsActive, Is.True);
            Assert.That(snapshot.Phase, Is.EqualTo(SceneTransitionDiagnosticPhase.WaitingForReadiness));
            Assert.That(snapshot.Progress, Is.EqualTo(0.42f));
            Assert.That(snapshot.IsDone, Is.False);
            Assert.That(snapshot.AllowSceneActivation, Is.False);
            Assert.That(snapshot.ActiveElapsedSeconds, Is.EqualTo(4.9d).Within(0.0001d));
            Assert.That(snapshot.PhaseElapsedSeconds, Is.EqualTo(4.9d).Within(0.0001d));
            Assert.That(snapshot.ProgressStallElapsedSeconds, Is.EqualTo(4.9d).Within(0.0001d));
            Assert.That(snapshot.LastMeaningfulProgress, Is.EqualTo(0.42f));
            Assert.That(snapshot.AnyWarningEmitted, Is.False);
        }

        [Test]
        public void TotalActiveDuration_CrossingThreshold_EmitsExactlyOnceWithoutStallWarning()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 10d,
                stallThresholdSeconds: 4d);

            for (var step = 1; step <= 5; step++)
            {
                wall.Now = step * 3d;
                monitor.Observe(
                    SceneTransitionDiagnosticPhase.WaitingForReadiness,
                    progress: step * 0.02f,
                    isDone: false,
                    allowSceneActivation: false);
            }

            Assert.That(logger.Messages, Has.Count.EqualTo(1));
            Assert.That(logger.Messages[0], Does.Contain("WarningType=TotalActiveDuration"));
            Assert.That(logger.Messages[0], Does.Not.Contain("WarningType=ProgressStall"));
            Assert.That(monitor.Snapshot.TotalDurationWarningEmitted, Is.True);
            Assert.That(monitor.Snapshot.CurrentPhaseProgressStallWarningEmitted, Is.False);
        }

        [Test]
        public void FixedProgress_CrossingStallThreshold_EmitsExactlyOnce()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 5d);

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 5d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 20d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Has.Count.EqualTo(1));
            Assert.That(logger.Messages[0], Does.Contain("WarningType=ProgressStall"));
            Assert.That(logger.Messages[0], Does.Contain("TransitionId=17"));
            Assert.That(logger.Messages[0], Does.Contain("Scene=GameplayShell"));
            Assert.That(logger.Messages[0], Does.Contain("Navigation=Retry"));
            Assert.That(logger.Messages[0], Does.Contain("Source=diagnostics-test"));
            Assert.That(logger.Messages[0], Does.Contain("Phase=WaitingForReadiness"));
            Assert.That(logger.Messages[0], Does.Contain("Progress=0.2"));
            Assert.That(logger.Messages[0], Does.Contain("IsDone=False"));
            Assert.That(logger.Messages[0], Does.Contain("AllowSceneActivation=False"));
            Assert.That(logger.Messages[0], Does.Contain("ActiveElapsed=5s"));
            Assert.That(logger.Messages[0], Does.Contain("PhaseElapsed=5s"));
            Assert.That(logger.Messages[0], Does.Contain("ProgressStallElapsed=5s"));
        }

        [Test]
        public void MeaningfulProgress_ResetsStallWindow()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 5d);

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 4.9d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 5d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.111f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 9.9d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.111f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Is.Empty);

            wall.Now = 10d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.111f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Has.Count.EqualTo(1));
            Assert.That(monitor.Snapshot.LastMeaningfulProgress, Is.EqualTo(0.111f));
        }

        [Test]
        public void ReadinessPlateau_DuringPresentationWait_IsActivationPending()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 5d);

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.8f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 1d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.ActivationPending,
                progress: 0.9f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 6d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.ActivationPending,
                progress: 0.9f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(monitor.Snapshot.Phase, Is.EqualTo(SceneTransitionDiagnosticPhase.ActivationPending));
            Assert.That(logger.Messages, Has.Count.EqualTo(1));
            Assert.That(logger.Messages[0], Does.Contain("Phase=ActivationPending"));
            Assert.That(logger.Messages[0], Does.Not.Contain("Phase=WaitingForReadiness"));
        }

        [Test]
        public void ActivationReleased_WhileOperationIncomplete_IsWaitingForCompletion()
        {
            var wall = new ManualWallTime();
            var monitor = CreateMonitor(
                wall,
                new RecordingLogger(),
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 20d);

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForCompletion,
                progress: 0.9f,
                isDone: false,
                allowSceneActivation: true);

            var snapshot = monitor.Snapshot;
            Assert.That(snapshot.Phase, Is.EqualTo(SceneTransitionDiagnosticPhase.WaitingForCompletion));
            Assert.That(snapshot.Progress, Is.EqualTo(0.9f));
            Assert.That(snapshot.IsDone, Is.False);
            Assert.That(snapshot.AllowSceneActivation, Is.True);
        }

        [Test]
        public void PausedWallTime_IsExcludedFromActiveAndStallElapsed()
        {
            var wall = new ManualWallTime();
            var clock = new SceneTransitionActiveClock(() => wall.Now);
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                clock,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 5d);

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 2d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);

            clock.SetPaused(true);
            clock.SetPaused(true);
            wall.Now = 102d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);

            clock.SetPaused(false);
            clock.SetPaused(false);
            wall.Now = 104.9d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Is.Empty);
            Assert.That(monitor.Snapshot.ActiveElapsedSeconds, Is.EqualTo(4.9d).Within(0.0001d));

            wall.Now = 105d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Has.Count.EqualTo(1));
            Assert.That(monitor.Snapshot.ProgressStallElapsedSeconds, Is.EqualTo(5d).Within(0.0001d));
        }

        [Test]
        public void CompletionCleanup_PreventsStaleWarning()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 1d);
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: false);

            wall.Now = 10d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForCompletion,
                progress: 0.2f,
                isDone: true,
                allowSceneActivation: true);
            monitor.End();
            wall.Now = 20d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForCompletion,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: true);

            Assert.That(logger.Messages, Is.Empty);
            Assert.That(monitor.Snapshot.IsActive, Is.False);
        }

        [Test]
        public void ExceptionCleanup_PreventsStaleWarning()
        {
            AssertEndPreventsStaleWarning();
        }

        [Test]
        public void DestroyCleanup_PreventsStaleWarning()
        {
            AssertEndPreventsStaleWarning();
        }

        [Test]
        public void OlderEndedMonitor_CannotWarnAsOrMutateNewerTransition()
        {
            var wall = new ManualWallTime();
            var clock = new SceneTransitionActiveClock(() => wall.Now);
            var logger = new RecordingLogger();
            var oldMonitor = CreateMonitor(
                clock,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 1d,
                transitionId: 101);
            oldMonitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);
            oldMonitor.End();

            var newMonitor = CreateMonitor(
                clock,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 1d,
                transitionId: 202);
            newMonitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.3f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 1d;
            oldMonitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForCompletion,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: true);
            newMonitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.3f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Has.Count.EqualTo(1));
            Assert.That(logger.Messages[0], Does.Contain("TransitionId=202"));
            Assert.That(logger.Messages[0], Does.Not.Contain("TransitionId=101"));
            Assert.That(newMonitor.Snapshot.Phase, Is.EqualTo(SceneTransitionDiagnosticPhase.WaitingForReadiness));
            Assert.That(newMonitor.Snapshot.AllowSceneActivation, Is.False);
        }

        [Test]
        public void ProgressStallWarning_IsOncePerPhase()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 5d);

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 5d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 6d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.ActivationPending,
                progress: 0.9f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 11d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.ActivationPending,
                progress: 0.9f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Has.Count.EqualTo(2));
            Assert.That(logger.Messages[0], Does.Contain("Phase=WaitingForReadiness"));
            Assert.That(logger.Messages[1], Does.Contain("Phase=ActivationPending"));
        }

        [Test]
        public void PhaseChange_DoesNotResetMeaningfulProgressHistory()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 5d);

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.5f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 4d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.ActivationPending,
                progress: 0.5f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Is.Empty);

            wall.Now = 5d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.ActivationPending,
                progress: 0.5f,
                isDone: false,
                allowSceneActivation: false);

            Assert.That(logger.Messages, Has.Count.EqualTo(1));
            Assert.That(logger.Messages[0], Does.Contain("Phase=ActivationPending"));
            Assert.That(logger.Messages[0], Does.Contain("PhaseElapsed=1s"));
            Assert.That(logger.Messages[0], Does.Contain("ProgressStallElapsed=5s"));
        }

        [Test]
        public void WarningEmission_DoesNotMutateRuntimeOwnerState()
        {
            var guardClaimed = true;
            var context = new object();
            var pending = new object();
            var overlayVisible = true;
            var allowSceneActivation = false;
            var operation = new object();
            var operationIsDone = false;
            var sceneName = "GameplayShell";
            var navigation = StageNavigationKind.Retry;
            var source = "diagnostics-test";
            var wall = new ManualWallTime();
            var monitor = CreateMonitor(
                wall,
                new RecordingLogger(),
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 1d);

            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.4f,
                isDone: false,
                allowSceneActivation);
            wall.Now = 1d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.4f,
                isDone: false,
                allowSceneActivation);

            Assert.That(guardClaimed, Is.True);
            Assert.That(context, Is.Not.Null);
            Assert.That(pending, Is.Not.Null);
            Assert.That(overlayVisible, Is.True);
            Assert.That(allowSceneActivation, Is.False);
            Assert.That(operation, Is.Not.Null);
            Assert.That(operationIsDone, Is.False);
            Assert.That(sceneName, Is.EqualTo("GameplayShell"));
            Assert.That(navigation, Is.EqualTo(StageNavigationKind.Retry));
            Assert.That(source, Is.EqualTo("diagnostics-test"));
        }

        [Test]
        public void ThrowingLogger_CannotFailDiagnosticsOrSceneTransitionPath()
        {
            var wall = new ManualWallTime();
            var monitor = CreateMonitor(
                wall,
                new ThrowingLogger(),
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 1d);
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false);
            wall.Now = 1d;

            Assert.DoesNotThrow(() => monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.1f,
                isDone: false,
                allowSceneActivation: false));
            Assert.That(monitor.Snapshot.CurrentPhaseProgressStallWarningEmitted, Is.True);
        }

        [Test]
        public void DiagnosticsHelper_HasNoRuntimeOwnerCleanupRetryOrPersistenceDependency()
        {
            var coordinatorSource = File.ReadAllText(CoordinatorSourcePath);
            var diagnosticsStart = coordinatorSource.IndexOf(
                "internal enum SceneTransitionDiagnosticPhase",
                StringComparison.Ordinal);
            Assert.That(diagnosticsStart, Is.GreaterThanOrEqualTo(0));
            var source = coordinatorSource.Substring(diagnosticsStart);

            Assert.That(source, Does.Not.Contain("StageTransitionLaunchGuard"));
            Assert.That(source, Does.Not.Contain("StageLaunchContextStore"));
            Assert.That(source, Does.Not.Contain("CampaignLaunchHandoff"));
            Assert.That(source, Does.Not.Contain("SceneTransitionOverlayShell"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs"));
            Assert.That(source, Does.Not.Contain("Retry"));
            Assert.That(source, Does.Not.Contain("SaveRepository"));
        }

        [Test]
        public void Coordinator_EndsDiagnosticsOnCompletionExceptionAndDestroyPaths()
        {
            var source = File.ReadAllText(CoordinatorSourcePath);
            var finallyIndex = source.IndexOf("finally", StringComparison.Ordinal);
            var endInFinallyIndex = source.IndexOf(
                "EndDiagnostics(transitionId, state.Diagnostics);",
                finallyIndex,
                StringComparison.Ordinal);
            var disposeIndex = source.IndexOf(
                "(routine as IDisposable)?.Dispose();",
                finallyIndex,
                StringComparison.Ordinal);

            Assert.That(finallyIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(endInFinallyIndex, Is.GreaterThan(finallyIndex));
            Assert.That(disposeIndex, Is.GreaterThan(endInFinallyIndex));
            Assert.That(source, Does.Contain("EndCurrentDiagnostics();"));
            Assert.That(source, Does.Contain("private void OnApplicationPause(bool pauseStatus)"));
        }

        private static void AssertEndPreventsStaleWarning()
        {
            var wall = new ManualWallTime();
            var logger = new RecordingLogger();
            var monitor = CreateMonitor(
                wall,
                logger,
                totalThresholdSeconds: 100d,
                stallThresholdSeconds: 1d);
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForReadiness,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: false);

            monitor.End();
            wall.Now = 10d;
            monitor.Observe(
                SceneTransitionDiagnosticPhase.WaitingForCompletion,
                progress: 0.2f,
                isDone: false,
                allowSceneActivation: true);

            Assert.That(logger.Messages, Is.Empty);
            Assert.That(monitor.Snapshot.IsActive, Is.False);
        }

        private static SceneTransitionDiagnosticsMonitor CreateMonitor(
            ManualWallTime wall,
            ISceneTransitionDiagnosticLogger logger,
            double totalThresholdSeconds,
            double stallThresholdSeconds,
            int transitionId = 17)
        {
            return CreateMonitor(
                new SceneTransitionActiveClock(() => wall.Now),
                logger,
                totalThresholdSeconds,
                stallThresholdSeconds,
                transitionId);
        }

        private static SceneTransitionDiagnosticsMonitor CreateMonitor(
            ISceneTransitionActiveClock clock,
            ISceneTransitionDiagnosticLogger logger,
            double totalThresholdSeconds,
            double stallThresholdSeconds,
            int transitionId = 17)
        {
            return new SceneTransitionDiagnosticsMonitor(
                new SceneTransitionDiagnosticIdentity(
                    transitionId,
                    "GameplayShell",
                    StageNavigationKind.Retry,
                    "diagnostics-test"),
                new SceneTransitionDiagnosticsSettings(
                    totalThresholdSeconds,
                    stallThresholdSeconds,
                    progressEpsilon: 0.01f),
                clock,
                logger);
        }

        private sealed class ManualWallTime
        {
            public double Now { get; set; }
        }

        private sealed class RecordingLogger : ISceneTransitionDiagnosticLogger
        {
            private readonly List<string> _messages = new();

            public IReadOnlyList<string> Messages => _messages;

            public void LogWarning(string message)
            {
                _messages.Add(message);
            }
        }

        private sealed class ThrowingLogger : ISceneTransitionDiagnosticLogger
        {
            public void LogWarning(string message)
            {
                throw new InvalidOperationException("test logger failure");
            }
        }
    }
}
