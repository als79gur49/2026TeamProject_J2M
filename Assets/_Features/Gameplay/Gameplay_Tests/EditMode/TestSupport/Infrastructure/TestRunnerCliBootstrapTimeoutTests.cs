using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    [Category("Full")]
    public sealed class TestRunnerCliBootstrapTimeoutTests
    {
        [TestCase("1", true, 1)]
        [TestCase("285", true, 285)]
        [TestCase("900", true, 900)]
        [TestCase("2147483632", true, 2147483632)]
        [TestCase(null, false, 0)]
        [TestCase("", false, 0)]
        [TestCase("0", false, 0)]
        [TestCase("-1", false, 0)]
        [TestCase("+1", false, 0)]
        [TestCase("01", false, 0)]
        [TestCase(" 900", false, 0)]
        [TestCase("900 ", false, 0)]
        [TestCase("1.5", false, 0)]
        [TestCase("abc", false, 0)]
        [TestCase("2147483633", false, 0)]
        [TestCase("999999999999999999999", false, 0)]
        public void WatchdogBudget_AcceptsOnlyRunnerCompatibleIntegers(string value, bool accepted, int seconds)
        {
            var bootstrap = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("TestRunnerCliBootstrap"))
                .FirstOrDefault(type => type != null);
            Assert.That(bootstrap, Is.Not.Null);
            var parser = bootstrap.GetMethod("TryParseWatchdogTimeout", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(parser, Is.Not.Null);
            var arguments = new object[] { value, 0 };
            Assert.That((bool)parser.Invoke(null, arguments), Is.EqualTo(accepted));
            if (accepted)
                Assert.That(arguments[1], Is.EqualTo(seconds));
        }
    }
}
