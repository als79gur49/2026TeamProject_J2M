using System;
using System.Collections.Generic;
using System.IO;
using Game.Exhibition.Integration;
using Game.Platform.Steam;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    [Category("Full")]
    public sealed class ParticipantResetDiagnosticsTests
    {
        [Test]
        public void DefaultCompositionDoesNotReadJournalOrCreateNativeDiagnosticsInBatch()
        {
            var reads = 0;
            Assert.That(ParticipantResetDiagnostics.Create(
                () => { reads++; return null; },
                () => { reads++; return null; }), Is.Null);
            Assert.That(reads, Is.Zero);
        }

        [Test]
        public void VerifiedReusesReadbackAndLaterSamplesRecordChangesOncePerStage()
        {
            var initial = new ResetRecord { State = ResetRecord.Pending, OperationId = "operation", AppId = 123, SteamId = 456 };
            var current = initial.Copy();
            var observations = new List<ParticipantResetDiagnostics.Observation>();
            var reads = 0;
            bool Read(string name, out bool earned) { reads++; earned = name == "B"; return true; }
            var diagnostics = new ParticipantResetDiagnostics(() => initial, () => current,
                () => new ResetIdentity(123, 456), Read, new[] { "A", "B" }, observations.Add);
            diagnostics.SteamResetVerified(new[] { "A", "B" }, SteamCallbackResult.Ok);
            Assert.That(reads, Is.Zero);
            Assert.That(observations[0].achievements[1].achieved, Is.False);
            Assert.That(observations[0].storeResult, Is.EqualTo("Ok"));
            current.State = ResetRecord.Ready;
            diagnostics.Capture(ParticipantResetDiagnosticStage.BeforeServices);
            diagnostics.Capture(ParticipantResetDiagnosticStage.MenuReady);
            diagnostics.Capture(ParticipantResetDiagnosticStage.MenuReady);
            Assert.That(reads, Is.EqualTo(4));
            Assert.That(observations.Count, Is.EqualTo(3));
            Assert.That(observations[2].achievements[1].achieved, Is.True);
            Assert.That(observations[2].initialJournalState, Is.EqualTo(ResetRecord.Pending));
            Assert.That(observations[2].journalState, Is.EqualTo(ResetRecord.Ready));
            Assert.That(observations[2].identityMatches, Is.True);
            Assert.That(observations[2].sessionId, Is.EqualTo(observations[0].sessionId));
        }

        [Test]
        public void UnavailableIdentitySkipsNativeReadsAndRecordsUnknown()
        {
            ParticipantResetDiagnostics.Observation observed = null;
            bool Read(string name, out bool earned) { earned = false; throw new AssertionException("native query"); }
            var diagnostics = new ParticipantResetDiagnostics(() => null,
                () => throw new IOException("journal unreadable"),
                () => throw new InvalidOperationException("Steam unavailable"), Read, new[] { "A" }, row => observed = row);
            diagnostics.Capture(ParticipantResetDiagnosticStage.MenuReady);
            Assert.That(observed.identityReadSucceeded, Is.False);
            Assert.That(observed.identityError, Is.EqualTo("Steam unavailable"));
            Assert.That(observed.journalState, Is.EqualTo("Unreadable"));
            Assert.That(observed.achievements[0].querySucceeded, Is.False);
            Assert.That(observed.achievements[0].error, Is.Not.Empty);
        }

        [Test]
        public void QueryFailureIsNotReportedAsSuccessfulLockedAchievement()
        {
            ParticipantResetDiagnostics.Observation observed = null;
            bool Read(string name, out bool earned)
            {
                earned = false;
                if (name == "B") throw new IOException("read error");
                return false;
            }
            var diagnostics = new ParticipantResetDiagnostics(() => null, () => null,
                () => new ResetIdentity(123, 456), Read, new[] { "A", "B" }, row => observed = row);
            diagnostics.Capture(ParticipantResetDiagnosticStage.MenuReady);
            foreach (var item in observed.achievements)
            {
                Assert.That(item.querySucceeded, Is.False);
                Assert.That(item.error, Is.Not.Empty);
            }
            Assert.That(observed.hasRecordedIdentity, Is.False);
            Assert.That(observed.identityMatches, Is.False);
        }

        [Test]
        public void WriterFailureAndReentryCannotEscapeBoundaryOrDuplicateSample()
        {
            var writes = 0;
            ParticipantResetDiagnostics diagnostics = null;
            bool Read(string name, out bool earned) { earned = false; return true; }
            diagnostics = new ParticipantResetDiagnostics(() => null, () => null,
                () => new ResetIdentity(123, 456), Read, new[] { "A" }, row =>
                {
                    writes++;
                    diagnostics.Capture(ParticipantResetDiagnosticStage.MenuReady);
                    throw new IOException("disk full");
                });
            Assert.DoesNotThrow(() => ParticipantResetDiagnosticBoundary.Capture(diagnostics, ParticipantResetDiagnosticStage.MenuReady));
            ParticipantResetDiagnosticBoundary.Capture(diagnostics, ParticipantResetDiagnosticStage.MenuReady);
            Assert.That(writes, Is.EqualTo(1));
        }
    }
}
