using System;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class CampaignChanceDisplayScopeTests
    {
        [Test]
        public void LastDeathRestorationIsHiddenUntilZeroOverrideSelected()
        {
            var store = Store(1);
            var display = new CampaignChanceDisplayOverride();
            using var source = new SaveSlotCampaignChancesReadSource(store, new CampaignRunningSlotContext(1), display);
            AssertChance(source, 1);
            using (var scope = display.BeginUpdate())
            {
                Seed(store, 3);
                Assert.That(store.LoadSlot(1).RemainingChances, Is.EqualTo(3));
                AssertChance(source, 1);
                display.Set(0, 3, GameplayChanceAudioPolicy.SuppressChanceChangeCue);
                AssertChance(source, 1);
                scope.Complete();
                AssertChance(source, 0);
            }
            AssertChance(source, 0);
            store.ClearAll();
        }

        [Test]
        public void ColdScopeUsesMutationBeforeState_AbortReleasesFrozenValue()
        {
            var store = Store(2);
            var display = new CampaignChanceDisplayOverride();
            using var source = new SaveSlotCampaignChancesReadSource(store, new CampaignRunningSlotContext(1), display);
            using (var scope = display.BeginUpdate())
            {
                scope.ObserveBeforeMutation(store.LoadSlot(1).State);
                Seed(store, 3);
                AssertChance(source, 2);
            }
            AssertChance(source, 3);
            store.ClearAll();
        }

        [Test]
        public void ActiveOverrideWinsOverBeforeMutationCapture_AndPolicyHasRevision()
        {
            var store = Store(3);
            var display = new CampaignChanceDisplayOverride();
            display.Set(0, 3, GameplayChanceAudioPolicy.SuppressChanceChangeCue);
            using var source = new SaveSlotCampaignChancesReadSource(store, new CampaignRunningSlotContext(1), display);
            using (var scope = display.BeginUpdate())
            {
                scope.ObserveBeforeMutation(store.LoadSlot(1).State);
                AssertChance(source, 0);
            }
            source.TryGetRevision(out var before);
            display.Set(0, 3, GameplayChanceAudioPolicy.Default);
            source.TryGetRevision(out var after);
            Assert.That(after, Is.GreaterThan(before));
            display.Clear();
            AssertChance(source, 3);
            store.ClearAll();
        }

        [Test]
        public void BeginOnUninitializedMissingSlotDoesNotRead_ColdFailureIsNotHidden()
        {
            var store = new TransientCampaignSaveSlotStore(Guid.NewGuid().ToString("N"));
            var display = new CampaignChanceDisplayOverride();
            using var source = new SaveSlotCampaignChancesReadSource(store, new CampaignRunningSlotContext(1), display);
            var revision = source.Session.Revision;
            using (display.BeginUpdate())
            {
                Assert.That(source.Session.Revision, Is.EqualTo(revision));
                Assert.Throws<InvalidOperationException>(() => AssertChance(source, 3));
            }
            Seed(store, 3);
            AssertChance(source, 3);
            store.ClearAll();
        }

        private static TransientCampaignSaveSlotStore Store(int remaining)
        {
            var store = new TransientCampaignSaveSlotStore(Guid.NewGuid().ToString("N"));
            Seed(store, remaining);
            return store;
        }
        private static void Seed(TransientCampaignSaveSlotStore store, int remaining) => store.ImportSlotSeed(
            new CampaignSlotSeedImportRequest(1, StageId.CreateOrThrow("stage-1-1"), "level-1", remaining, string.Empty));
        private static void AssertChance(SaveSlotCampaignChancesReadSource source, int expected)
        {
            Assert.That(source.TryReadChances(out var remaining, out var maximum, out _), Is.True);
            Assert.That(remaining, Is.EqualTo(expected));
            Assert.That(maximum, Is.EqualTo(3));
        }
    }
}
