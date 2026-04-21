using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageLoadSourceModeArchitectureTests
    {
        [Test]
        public void StageLoadSourceMode_SwitchExistsOnlyInStrategyFactory()
        {
            var featuresRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features"));
            var matches = Directory
                .GetFiles(featuresRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("StageLoadSourceModeArchitectureTests.cs", StringComparison.Ordinal))
                .Where(path => !path.Contains("/Editor/", StringComparison.Ordinal) &&
                               !path.Contains("\\Editor\\", StringComparison.Ordinal))
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("StageLoadSourceMode") && source.Contains("switch");
                })
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            Assert.That(matches, Is.EquivalentTo(new[]
            {
                $"{featuresRoot.Replace('\\', '/')}/Stages/Runtime/Load/StageLoadStrategyFactory.cs",
            }));
        }
    }
}
