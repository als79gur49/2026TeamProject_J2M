using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Exhibition.Editor
{
    public sealed class RestartExperimentBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            CopyTools(Path.GetDirectoryName(report.summary.outputPath));
        }
        public static void CopyTools(string outputDirectory)
        {
            var destination = Path.Combine(outputDirectory, "RestartExperiment");
            Directory.CreateDirectory(destination);
            var names = new[] { "RestartExperiment.cs", "RestartExperimentWindows.cs", "RestartExperimentNativeProbe.cs", "Restart-Experiment.ps1" };
            foreach (var file in Directory.GetFiles(destination))
                if (System.Array.IndexOf(names, Path.GetFileName(file)) < 0)
                    throw new BuildFailedException("Unexpected restart helper artifact: use a fresh output directory.");
            foreach (string name in names)
            {
                var source = name.EndsWith(".ps1") ? "Assets/_Features/Exhibition/Tools" : "Assets/_Features/Exhibition/Integration";
                File.Copy(Path.Combine(source, name), Path.Combine(destination, name), true);
            }
        }
    }
}
