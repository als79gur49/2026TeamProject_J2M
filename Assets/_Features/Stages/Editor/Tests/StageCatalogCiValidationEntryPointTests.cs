using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCatalogCiValidationEntryPointTests
    {
        [Test]
        public void Run_WritesGovernanceAndAliasUsageValidationSections()
        {
            var result = StageCatalogCiValidationEntryPoint.Run();
            var reportPath = Path.Combine("Temp", "StageCatalogValidation", "stage-catalog-validation.md");

            Assert.That(result, Is.EqualTo(0));
            Assert.That(File.Exists(reportPath), Is.True);

            var reportText = File.ReadAllText(reportPath);
            Assert.That(reportText, Does.Contain("## Authoring Sync Issues"));
            Assert.That(reportText, Does.Contain("## Known Warning Governance Issues"));
            Assert.That(reportText, Does.Contain("## Alias Governance Issues"));
            Assert.That(reportText, Does.Contain("## Alias Usage Issues"));
        }
    }
}
