using System;
using System.Globalization;
using System.Reflection;
using Game.Shared.Display;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class DisplayRefreshRateReflectionTests
    {
        [Test]
        public void TryCreateRefreshRateRatio_CoercesRuntimeMemberTypes()
        {
            var helperType = typeof(DisplaySettingsService).Assembly.GetType("Game.Shared.Display.DisplayRefreshRateReflection");
            Assert.That(helperType, Is.Not.Null);

            var tryCreateMethod = helperType.GetMethod(
                "TryCreateRefreshRateRatio",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(tryCreateMethod, Is.Not.Null);

            var arguments = new object[] { 144, 1, null };
            var created = (bool)tryCreateMethod.Invoke(null, arguments);

            Assert.That(created, Is.True);
            Assert.That(arguments[2], Is.Not.Null);
            Assert.That(ReadRatioMember(arguments[2], "numerator"), Is.EqualTo(144));
            Assert.That(ReadRatioMember(arguments[2], "denominator"), Is.EqualTo(1));
        }

        private static int ReadRatioMember(object ratio, string memberName)
        {
            var ratioType = ratio.GetType();
            var field = ratioType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return Convert.ToInt32(field.GetValue(ratio), CultureInfo.InvariantCulture);
            }

            var property = ratioType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return Convert.ToInt32(property.GetValue(ratio, null), CultureInfo.InvariantCulture);
            }

            throw new InvalidOperationException($"Unable to read refresh rate ratio member '{memberName}'.");
        }
    }
}
