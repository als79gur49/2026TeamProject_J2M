using System;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class CampaignProfileReadinessReportCommand
    {
        [MenuItem("Tools/Stages/Diagnostics/Write Campaign Profile Readiness Report")]
        public static void WriteDefaultReportFromMenu()
        {
            var outputPath = WriteDefaultReport();
            Debug.Log($"Campaign profile readiness report written: {outputPath}");
        }

        public static void WriteDefaultReportFromCommandLine()
        {
            try
            {
                var outputPath = WriteDefaultReport();
                Debug.Log($"Campaign profile readiness report written: {outputPath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static string WriteDefaultReport()
        {
            var report = new CampaignProfileReadinessReportBuilder().Build(
                new CampaignProfileReadinessReportOptions(Application.persistentDataPath));
            return CampaignProfileReadinessReportWriter.Write(report);
        }
    }
}
