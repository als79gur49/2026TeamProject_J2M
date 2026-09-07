using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Exhibition.Editor
{
    public sealed class ParticipantResetBuildPostprocessor : IPostprocessBuildWithReport
    {
        public const string HelperSource = "Assets/_Features/Exhibition/Tools/Exhibition-Relaunch.ps1";
        public int callbackOrder => 0;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            CopyHelper(Path.GetDirectoryName(report.summary.outputPath));
        }

        public static void CopyHelper(string outputDirectory)
        {
            if (!File.Exists(HelperSource)) throw new BuildFailedException("Participant restart helper is missing.");
            Directory.CreateDirectory(outputDirectory);
            File.Copy(HelperSource, Path.Combine(outputDirectory,
                WindowsDistributionTargetPolicy.ParticipantRestartArtifact), true);
        }
    }
}
