using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class PendingLaunchSlotProviderTests
    {
        private const string PendingLaunchProviderSourcePath =
            "Assets/_Features/Stages/Runtime/Campaign/PendingLaunchSlotProvider.cs";
        private readonly string _activeSlotNamespace =
            $"pending-launch-slot-provider-tests-{Guid.NewGuid():N}";

        [SetUp]
        public void SetUp()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
            new TransientActiveSlotStorage(_activeSlotNamespace).ClearActiveSlot();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
            new TransientActiveSlotStorage(_activeSlotNamespace).ClearActiveSlot();
        }

        [Test]
        public void PendingBegin_DoesNotModifyPersistentActiveSlot()
        {
            var activeSlotProvider = new ActiveSlotProvider(
                new TransientActiveSlotStorage(_activeSlotNamespace));
            var store = CampaignLaunchHandoffSessionStore.Instance;

            Assert.That(
                store.TryBegin(
                    2,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "main-menu-continue",
                    out var handoff),
                Is.True);

            Assert.That(handoff.SlotNumber, Is.EqualTo(2));
            Assert.That(activeSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
        }

        [Test]
        public void FirstAcceptedRequest_WinsUntilMatchingTokenConsumesIt()
        {
            var store = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                store.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Continue,
                    "first",
                    out var first),
                Is.True);

            Assert.That(
                store.TryBegin(
                    3,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "second",
                    out var rejected),
                Is.False);
            Assert.That(rejected, Is.SameAs(first));
            Assert.That(store.TryClear(Guid.NewGuid()), Is.False);
            Assert.That(store.TryConsume(Guid.NewGuid(), out _), Is.False);
            Assert.That(store.TryPeek(out var stillPending), Is.True);
            Assert.That(stillPending, Is.SameAs(first));

            Assert.That(store.TryConsume(first.Token, out var consumed), Is.True);
            Assert.That(consumed, Is.SameAs(first));
            Assert.That(store.TryPeek(out _), Is.False);
        }

        [Test]
        public void NewApplicationSessionReset_DropsPendingHandoff()
        {
            var store = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                store.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Continue,
                    "session-reset",
                    out _),
                Is.True);

            CampaignLaunchHandoffSessionStore.ResetForTests();

            Assert.That(store.TryPeek(out _), Is.False);
        }

        [Test]
        public void PendingClear_DoesNotClearPersistentActiveSlot()
        {
            var activeSlotProvider = new ActiveSlotProvider(
                new TransientActiveSlotStorage(_activeSlotNamespace));
            activeSlotProvider.SetActiveSlot(3);
            var store = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                store.TryBegin(
                    2,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "clear-only-pending",
                    out var handoff),
                Is.True);

            Assert.That(store.TryClear(handoff.Token), Is.True);
            Assert.That(activeSlotProvider.TryGetActiveSlotNumber(out var activeSlotNumber), Is.True);
            Assert.That(activeSlotNumber, Is.EqualTo(3));
        }

        [Test]
        public void PendingOwner_IsSessionOnlyAndDoesNotReferenceProfileOrLocalStateStorage()
        {
            var source = File.ReadAllText(PendingLaunchProviderSourcePath);
            var writableProperties = typeof(CampaignLaunchHandoff)
                .GetProperties()
                .Where(property => property.CanWrite)
                .Select(property => property.Name)
                .ToArray();

            Assert.That(source, Does.Contain("RuntimeInitializeLoadType.SubsystemRegistration"));
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"));
            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(source, Does.Not.Contain("ICampaignLocalLaunchStateRepository"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs"));
            Assert.That(source, Does.Not.Contain("MonoBehaviour"));
            Assert.That(writableProperties, Is.Empty);
        }
    }
}
