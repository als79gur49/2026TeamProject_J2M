using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignRunningSlotContextTests
    {
        [Test]
        public void RunningSlotContext_RequiresValidSaveSlotNumber()
        {
            var context = new CampaignRunningSlotContext(2);

            Assert.That(context.SlotNumber, Is.EqualTo(2));
            Assert.That(context.ToString(), Does.Contain("2"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CampaignRunningSlotContext(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CampaignRunningSlotContext(4));
        }

        [Test]
        public void RunningSlotContext_IsSeparateFromLastPlayedProfileMetadata()
        {
            var document = new CampaignProfileDocument
            {
                LastPlayedSlotNumber = 3,
            };
            var runningSlotContext = new CampaignRunningSlotContext(1);

            Assert.That(document.LastPlayedSlotNumber, Is.EqualTo(3));
            Assert.That(runningSlotContext.SlotNumber, Is.EqualTo(1));
            Assert.That(runningSlotContext.SlotNumber, Is.Not.EqualTo(document.LastPlayedSlotNumber));
        }
    }
}
