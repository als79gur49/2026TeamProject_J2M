using Game.Platform.Runtime;
using System;

namespace Game.Platform.Tests.PlayMode
{
    internal static class PlatformRuntimeTestBootstrap
    {
        internal static IDisposable BeginBootstrapSuppression()
        {
            return PlatformRuntimeBootstrap.BeginAutomaticBootstrapSuppressionForTests();
        }
    }
}
