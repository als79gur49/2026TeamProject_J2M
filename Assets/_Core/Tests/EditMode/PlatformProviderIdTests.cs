using System;
using Game.Platform.Runtime;
using NUnit.Framework;

namespace Game.Platform.Tests.EditMode
{
    public sealed class PlatformProviderIdTests
    {
        [TestCase("local")]
        [TestCase("provider2")]
        [TestCase("com.vendor.store")]
        [TestCase("store_name")]
        [TestCase("store-name")]
        public void Constructor_AcceptsCanonicalLowercaseAsciiIds(string value)
        {
            var providerId = new PlatformProviderId(value);

            Assert.That(providerId.IsValid, Is.True);
            Assert.That(providerId.Value, Is.EqualTo(value));
        }

        [Test]
        public void Constructor_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new PlatformProviderId(null));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        [TestCase("Steam")]
        [TestCase("STEAM")]
        [TestCase(" steam")]
        [TestCase("steam ")]
        [TestCase("스팀")]
        [TestCase("steam/provider")]
        [TestCase("steam:provider")]
        public void Constructor_RejectsNonCanonicalIds(string value)
        {
            Assert.Throws<ArgumentException>(() => new PlatformProviderId(value));
        }

        [Test]
        public void EqualityAndHashCode_UseOrdinalMeaning()
        {
            var first = new PlatformProviderId("com.vendor.store");
            var second = new PlatformProviderId("com.vendor.store");
            var different = new PlatformProviderId("com.vendor-store");

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
        }

        [Test]
        public void DefaultValue_IsNotAValidProviderId()
        {
            var providerId = default(PlatformProviderId);

            Assert.That(providerId.IsValid, Is.False);
            Assert.That(providerId, Is.Not.EqualTo(PlatformProviderId.Local));
            Assert.That(providerId.ToString(), Is.EqualTo("<invalid>"));
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = providerId.Value;
            });
        }
    }
}
