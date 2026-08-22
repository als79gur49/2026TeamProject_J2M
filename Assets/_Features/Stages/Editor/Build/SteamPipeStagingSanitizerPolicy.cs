using System;

public static class SteamPipeStagingSanitizerPolicy
{
    private static readonly string[] DeniedDirectoryNames =
    {
        "TestLogs",
        "TestResult",
        "TestResults",
        "Logs",
        "ProfilerCaptures",
        "Library",
        "UserSettings",
        "obj",
        "Temp",
        ".git",
        ".github",
        ".vs",
        "Assets",
        "Packages",
        "ProjectSettings",
        "Docs",
    };

    public static bool IsDeniedContent(string candidate)
    {
        var value = Normalize(candidate);
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var fileName = GetFileName(value);
        if (EqualsOrdinalIgnoreCase(fileName, "steam_appid.txt") ||
            EqualsOrdinalIgnoreCase(fileName, "CampaignProfileReadiness.md") ||
            EqualsOrdinalIgnoreCase(fileName, "campaign-save-seed.json") ||
            HasDeniedExtension(fileName))
        {
            return true;
        }

        if (IsSaveOrLocalStateArtifact(value))
        {
            return true;
        }

        for (var index = 0; index < DeniedDirectoryNames.Length; index++)
        {
            if (ContainsDirectory(value, DeniedDirectoryNames[index]))
            {
                return true;
            }
        }

        return IsUnderSteamPipeGeneratedDirectory(value, "cache") ||
               IsUnderSteamPipeGeneratedDirectory(value, "output") ||
               IsUnderSteamPipeGeneratedDirectory(value, "login");
    }

    public static string Normalize(string candidate)
    {
        return (candidate ?? string.Empty).Replace('\\', '/').Trim('/');
    }

    private static bool IsSaveOrLocalStateArtifact(string value)
    {
        var lower = value.ToLowerInvariant();
        return EndsWithPath(lower, "saves/profile.json") ||
               EndsWithPath(lower, "saves/profile.json.bak") ||
               IsProfileTemporaryPath(lower) ||
               ContainsPath(lower, "saves/profile.json.corrupt.") ||
               ContainsPath(lower, "saves/profile.json.rejected.") ||
               ContainsPath(lower, "saves/profile.json.bak.rejected.") ||
               EndsWithPath(lower, "saves/profile.reset.pending.json") ||
               EndsWithPath(lower, "saves/profile.reset.pending.json.bak") ||
               EndsWithPath(lower, "settings/local-settings.json") ||
               EndsWithPath(lower, "saves/local-launch-state.json") ||
               EndsWithPath(lower, "saves/editor-direct-play.json") ||
               EndsWithPath(lower, "saves/direct-play-temp.json");
    }

    private static bool IsProfileTemporaryPath(string lower)
    {
        var marker = "/saves/profile.";
        var index = ("/" + lower).LastIndexOf(marker, StringComparison.Ordinal);
        return index >= 0 && lower.EndsWith(".tmp", StringComparison.Ordinal);
    }

    private static bool HasDeniedExtension(string fileName)
    {
        return fileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderSteamPipeGeneratedDirectory(string value, string leaf)
    {
        var segments = value.Split('/');
        for (var index = 0; index + 2 < segments.Length; index++)
        {
            if (EqualsOrdinalIgnoreCase(segments[index], "Tools") &&
                EqualsOrdinalIgnoreCase(segments[index + 1], "SteamPipe"))
            {
                for (var nested = index + 2; nested < segments.Length; nested++)
                {
                    if (EqualsOrdinalIgnoreCase(segments[nested], leaf))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool ContainsDirectory(string value, string directoryName)
    {
        var segments = value.Split('/');
        for (var index = 0; index < segments.Length; index++)
        {
            if (EqualsOrdinalIgnoreCase(segments[index], directoryName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EndsWithPath(string value, string suffix)
    {
        return string.Equals(value, suffix, StringComparison.Ordinal) ||
               value.EndsWith("/" + suffix, StringComparison.Ordinal);
    }

    private static bool ContainsPath(string value, string fragment)
    {
        return ("/" + value).IndexOf("/" + fragment, StringComparison.Ordinal) >= 0;
    }

    private static string GetFileName(string value)
    {
        var separator = value.LastIndexOf('/');
        return separator < 0 ? value : value.Substring(separator + 1);
    }

    private static bool EqualsOrdinalIgnoreCase(string first, string second)
    {
        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }
}
