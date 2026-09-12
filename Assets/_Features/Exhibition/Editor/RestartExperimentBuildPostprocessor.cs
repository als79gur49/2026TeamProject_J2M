using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Exhibition.Editor
{
    public sealed class RestartExperimentBuildPostprocessor : IPostprocessBuildWithReport
    {
        public const string Define = "J2M_PARTICIPANT_RESTART_EXPERIMENT";
        public int callbackOrder => 1;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            bool enabled = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';').Contains(Define);
            CopyTools(Path.GetDirectoryName(report.summary.outputPath), enabled);
        }
        public static void CopyTools(string outputDirectory, bool enabled)
        {
            var destination = Path.Combine(outputDirectory, "RestartExperiment");
            Directory.CreateDirectory(destination);
            foreach (string name in new[] { "RestartExperiment.cs", "RestartExperimentWindows.cs", "RestartExperimentNativeProbe.cs" })
                File.Copy(Path.Combine("Assets/_Features/Exhibition/Integration", name), Path.Combine(destination, name), true);
            File.Copy("Assets/_Features/Exhibition/Tools/Restart-Experiment.ps1", Path.Combine(destination, "Restart-Experiment.ps1"), true);
            if (!enabled)
            {
                if (File.Exists(Path.Combine(destination, "ObservationV3Wire.cs")) || File.Exists(Path.Combine(destination, "prerequisites.example.json")))
                    throw new BuildFailedException("Stale restart experiment artifacts: use a fresh output directory.");
                return;
            }
            foreach (string name in new[] { "ObservationV3Wire.cs", "ObservationV3Handoff.cs", "ObservationV3Pipe.cs", "ObservationV3RuntimeWire.cs", "ObservationV3RuntimeProtocol.cs","ObservationV3Admission.cs","ObservationV3JournalReader.cs","ObservationV3Session.cs", "ObservationV3WindowsEnvironment.cs", "ObservationV3WindowsHost.cs" })
                File.Copy(Path.Combine("Assets/_Features/Exhibition/Integration", name), Path.Combine(destination, name), true);
            File.WriteAllText(Path.Combine(destination, "prerequisites.example.json"),
                "{\n  \"SteamExeSha256\": \"\",\n  \"DllSha256\": \"8de54d32508e216c9135b8bf025749243d44e404c1c22a8e5fe35acecabe7a9c\",\n" +
                "  \"ShutdownCommandVerified\": false,\n  \"GateAEvidence\": \"\",\n  \"ProbeContractReviewed\": false,\n" +
                "  \"FailedInitExitReviewed\": false,\n  \"CallbackPumpReviewed\": false,\n  \"GateBEvidence\": \"\"\n}\n");
        }
    }
}
