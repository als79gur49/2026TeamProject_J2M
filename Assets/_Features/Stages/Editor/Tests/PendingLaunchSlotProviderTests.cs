using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class PendingLaunchSlotProviderTests
    {
        private const string PendingLaunchProviderSourcePath =
            "Assets/_Features/Stages/Runtime/Campaign/PendingLaunchSlotProvider.cs";
        private readonly string _activeSlotKey = $"pending-launch-slot-provider-tests-{System.Guid.NewGuid():N}";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_activeSlotKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void PendingLaunchSlotProvider_WrapsExistingActiveSlotProviderBehavior()
        {
            var activeSlotProvider = new ActiveSlotProvider(_activeSlotKey);
            IPendingLaunchSlotProvider pendingLaunchSlotProvider =
                new ActiveSlotProviderPendingLaunchAdapter(activeSlotProvider);

            Assert.That(pendingLaunchSlotProvider.TryGetPendingLaunchSlot(out _), Is.False);

            pendingLaunchSlotProvider.SetPendingLaunchSlot(2);

            Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(2));
            Assert.That(PlayerPrefs.GetInt(_activeSlotKey), Is.EqualTo(2));
            Assert.That(pendingLaunchSlotProvider.TryGetPendingLaunchSlot(out var pendingSlot), Is.True);
            Assert.That(pendingSlot, Is.EqualTo(2));
            Assert.That(pendingLaunchSlotProvider.IsPendingLaunchSlot(2), Is.True);
            Assert.That(pendingLaunchSlotProvider.IsPendingLaunchSlot(1), Is.False);

            pendingLaunchSlotProvider.ClearPendingLaunchSlot();

            Assert.That(activeSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
            Assert.That(PlayerPrefs.HasKey(_activeSlotKey), Is.False);
        }

        [Test]
        public void PendingLaunchSlotProvider_SeesLegacyActiveSlotProviderChanges()
        {
            var activeSlotProvider = new ActiveSlotProvider(_activeSlotKey);
            IPendingLaunchSlotProvider pendingLaunchSlotProvider =
                new ActiveSlotProviderPendingLaunchAdapter(activeSlotProvider);

            activeSlotProvider.SetActiveSlot(3);

            Assert.That(pendingLaunchSlotProvider.TryGetPendingLaunchSlot(out var pendingSlot), Is.True);
            Assert.That(pendingSlot, Is.EqualTo(3));
            Assert.That(pendingLaunchSlotProvider.IsPendingLaunchSlot(3), Is.True);
        }

        [Test]
        public void PendingLaunchSlotProvider_DoesNotReferenceCampaignProfileDocument()
        {
            var source = File.ReadAllText(PendingLaunchProviderSourcePath);

            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"));
            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(source, Does.Not.Contain("ICampaignProfileRepository"));
            Assert.That(source, Does.Not.Contain("profile.json"));
        }
    }
}
