using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Feature.UI.Composition.Editor
{
    public enum TypographyValidationSeverity
    {
        Info,
        Warning,
        Error,
    }

    public sealed class TypographyValidationIssue
    {
        public TypographyValidationIssue(
            TypographyValidationSeverity severity,
            string target,
            string message)
        {
            Severity = severity;
            Target = target ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public TypographyValidationSeverity Severity { get; }

        public string Target { get; }

        public string Message { get; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Target)
                ? $"[{Severity}] {Message}"
                : $"[{Severity}] {Target}: {Message}";
        }
    }

    public sealed class TypographyValidationReport
    {
        private readonly List<TypographyValidationIssue> issues = new();

        public TypographyValidationReport(string title)
        {
            Title = title ?? "Typography Validation";
        }

        public string Title { get; }

        public IReadOnlyList<TypographyValidationIssue> Issues => issues;

        public bool HasErrors => issues.Any(issue => issue.Severity == TypographyValidationSeverity.Error);

        public int ErrorCount => issues.Count(issue => issue.Severity == TypographyValidationSeverity.Error);

        public int WarningCount => issues.Count(issue => issue.Severity == TypographyValidationSeverity.Warning);

        public void AddInfo(string target, string message)
        {
            issues.Add(new TypographyValidationIssue(TypographyValidationSeverity.Info, target, message));
        }

        public void AddWarning(string target, string message)
        {
            issues.Add(new TypographyValidationIssue(TypographyValidationSeverity.Warning, target, message));
        }

        public void AddError(string target, string message)
        {
            issues.Add(new TypographyValidationIssue(TypographyValidationSeverity.Error, target, message));
        }

        public void Merge(TypographyValidationReport other)
        {
            if (other == null)
            {
                return;
            }

            issues.AddRange(other.Issues);
        }

        public void LogToConsole()
        {
            var lines = new List<string>
            {
                $"{Title}: {ErrorCount} error(s), {WarningCount} warning(s)",
                "| Severity | Target | Message |",
                "|---|---|---|",
            };
            lines.AddRange(issues.Select(issue =>
                $"| {issue.Severity} | {Escape(issue.Target)} | {Escape(issue.Message)} |"));

            var message = string.Join(Environment.NewLine, lines);
            if (HasErrors)
            {
                Debug.LogError(message);
            }
            else if (WarningCount > 0)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal);
        }
    }
}
