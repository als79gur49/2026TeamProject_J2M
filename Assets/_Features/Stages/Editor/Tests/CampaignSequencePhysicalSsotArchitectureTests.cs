using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSequencePhysicalSsotArchitectureTests
    {
        [Test]
        public void ProductionSource_HasNoCodeSequenceFactoryMirrorOrStaticResolver()
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null);

            var definitionPath = Path.Combine(
                projectRoot,
                "Assets/_Features/Stages/Runtime/Campaign/CampaignStageSequenceDefinition.cs");
            var definitionSource = File.ReadAllText(definitionPath);
            Assert.That(definitionSource, Does.Not.Contain("static readonly string[]"));
            Assert.That(definitionSource, Does.Not.Contain("display" + "Name"));
            Assert.That(definitionSource, Does.Not.Contain("entries = Create"));

            var forbiddenFactory = "CreateCanonicalRuntime" + "Instance";
            var forbiddenStaticResolver = "static CampaignStageSequence" + "Resolver";
            var forbiddenLazyResolver = "Lazy<CampaignStageSequence" + "Resolver>";
            var productionRoots = new[]
            {
                "Assets/_Features/Stages/Runtime",
                "Assets/_Features/Stages/Editor",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime",
                "Assets/_Features/UI/UI_Composition/Runtime",
                "Assets/_Features/UI/UI_Flow/Runtime",
            };

            foreach (var relativeRoot in productionRoots)
            {
                var root = Path.Combine(projectRoot, relativeRoot);
                foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (path.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
                    {
                        continue;
                    }

                    var source = File.ReadAllText(path);
                    Assert.That(source, Does.Not.Contain(forbiddenFactory), path);
                    Assert.That(source, Does.Not.Contain(forbiddenStaticResolver), path);
                    Assert.That(source, Does.Not.Contain(forbiddenLazyResolver), path);
                }
            }
        }
    }
}
