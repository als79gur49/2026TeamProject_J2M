using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public enum ProviderSelectionMode
{
    DefaultWhenUnspecified = 0,
    ExternalLaunchArgumentRequired = 1,
}

public enum WindowsDistributionValidationFailure
{
    None = 0,
    ConfigurationMissing = 1,
    TargetIdMissing = 2,
    ExpectedProviderIdMissing = 3,
    InvalidArtifactContract = 4,
    LaunchArgumentContractMismatch = 5,
    RequiredArtifactMissing = 6,
    ForbiddenArtifactPresent = 7,
}

public sealed class WindowsDistributionTargetConfiguration
{
    private readonly IReadOnlyList<string> expectedLaunchArguments;
    private readonly IReadOnlyList<string> requiredArtifacts;
    private readonly IReadOnlyList<string> forbiddenArtifacts;

    public WindowsDistributionTargetConfiguration(
        string targetId,
        string artifactDirectoryName,
        ProviderSelectionMode providerSelectionMode,
        string expectedProviderId,
        IEnumerable<string> expectedLaunchArguments,
        IEnumerable<string> requiredArtifacts,
        IEnumerable<string> forbiddenArtifacts)
    {
        TargetId = targetId;
        ArtifactDirectoryName = artifactDirectoryName;
        ProviderSelectionMode = providerSelectionMode;
        ExpectedProviderId = expectedProviderId;
        this.expectedLaunchArguments = Array.AsReadOnly(Copy(expectedLaunchArguments));
        this.requiredArtifacts = Array.AsReadOnly(Copy(requiredArtifacts));
        this.forbiddenArtifacts = Array.AsReadOnly(Copy(forbiddenArtifacts));
    }

    public string TargetId { get; }
    public string ArtifactDirectoryName { get; }
    public ProviderSelectionMode ProviderSelectionMode { get; }
    public string ExpectedProviderId { get; }
    public IReadOnlyList<string> ExpectedLaunchArguments => expectedLaunchArguments;
    public IReadOnlyList<string> RequiredArtifacts => requiredArtifacts;
    public IReadOnlyList<string> ForbiddenArtifacts => forbiddenArtifacts;
    public string ExpectedStoreLaunch => string.Join(
        " ",
        new[] { WindowsDistributionTargetPolicy.ExecutableName }
            .Concat(expectedLaunchArguments));

    public string[] CopyExpectedLaunchArguments() => expectedLaunchArguments.ToArray();

    public string[] CopyRequiredArtifacts() => requiredArtifacts.ToArray();

    public string[] CopyForbiddenArtifacts() => forbiddenArtifacts.ToArray();

    private static string[] Copy(IEnumerable<string> values)
    {
        return values == null ? Array.Empty<string>() : values.ToArray();
    }
}

public static class WindowsDistributionTargetPolicy
{
    public const string DirectWindowsTargetId = "direct-windows";
    public const string SteamWindowsTargetId = "steam-windows";
    public const string LocalProviderId = "local";
    public const string SteamProviderId = "steam";
    public const string ExecutableName = "VectorQuake.exe";
    public const string ProviderSelectorArgument = "-j2mPlatformProvider";
    public const string SteamNativeArtifact = "steam_api64.dll";
    public const string SteamManagedBindingArtifact =
        "com.rlabrecque.steamworks.net.dll";
    public const string SteamAppIdArtifact = "steam_appid.txt";
    public const string ThirdPartyNoticesArtifact = "ThirdPartyNotices.txt";
    public static readonly IReadOnlyList<string> ThirdPartyNoticeRequiredMarkers =
        Array.AsReadOnly(new[]
        {
            "VectorQuake Third-Party Notices",
            "Unity UI Extensions",
            "Steamworks.NET (Steam distribution only)",
            "Valve Steamworks SDK Redistributable (Steam distribution only)",
            "Open Font Software",
            "SIL OPEN FONT LICENSE Version 1.1",
        });

    public static readonly WindowsDistributionTargetConfiguration DirectWindows =
        new WindowsDistributionTargetConfiguration(
            DirectWindowsTargetId,
            "DirectWindows",
            ProviderSelectionMode.DefaultWhenUnspecified,
            LocalProviderId,
            Array.Empty<string>(),
            new[] { ThirdPartyNoticesArtifact },
            new[]
            {
                SteamNativeArtifact,
                SteamManagedBindingArtifact,
                SteamAppIdArtifact,
            });

    public static readonly WindowsDistributionTargetConfiguration SteamWindows =
        new WindowsDistributionTargetConfiguration(
            SteamWindowsTargetId,
            "SteamWindows",
            ProviderSelectionMode.ExternalLaunchArgumentRequired,
            SteamProviderId,
            new[] { ProviderSelectorArgument, SteamProviderId },
            new[]
            {
                ThirdPartyNoticesArtifact,
                SteamNativeArtifact,
                SteamManagedBindingArtifact,
            },
            new[] { SteamAppIdArtifact });

    public static bool TryResolve(
        string targetId,
        out WindowsDistributionTargetConfiguration configuration)
    {
        if (string.Equals(targetId, DirectWindowsTargetId, StringComparison.Ordinal))
        {
            configuration = DirectWindows;
            return true;
        }

        if (string.Equals(targetId, SteamWindowsTargetId, StringComparison.Ordinal))
        {
            configuration = SteamWindows;
            return true;
        }

        configuration = null;
        return false;
    }

    public static WindowsDistributionValidationFailure ValidateConfiguration(
        WindowsDistributionTargetConfiguration configuration)
    {
        if (configuration == null)
        {
            return WindowsDistributionValidationFailure.ConfigurationMissing;
        }

        if (string.IsNullOrWhiteSpace(configuration.TargetId) ||
            string.IsNullOrWhiteSpace(configuration.ArtifactDirectoryName))
        {
            return WindowsDistributionValidationFailure.TargetIdMissing;
        }

        if (string.IsNullOrWhiteSpace(configuration.ExpectedProviderId))
        {
            return WindowsDistributionValidationFailure.ExpectedProviderIdMissing;
        }

        if (!ValidTokens(configuration.ExpectedLaunchArguments) ||
            !ValidTokens(configuration.RequiredArtifacts) ||
            !ValidTokens(configuration.ForbiddenArtifacts) ||
            configuration.RequiredArtifacts.Any(required =>
                configuration.ForbiddenArtifacts.Contains(
                    required, StringComparer.OrdinalIgnoreCase)))
        {
            return WindowsDistributionValidationFailure.InvalidArtifactContract;
        }

        if (string.Equals(configuration.TargetId, DirectWindowsTargetId,
                StringComparison.Ordinal))
        {
            return configuration.ProviderSelectionMode ==
                       ProviderSelectionMode.DefaultWhenUnspecified &&
                   string.Equals(configuration.ExpectedProviderId, LocalProviderId,
                       StringComparison.Ordinal) &&
                   configuration.ExpectedLaunchArguments.Count == 0
                ? WindowsDistributionValidationFailure.None
                : WindowsDistributionValidationFailure.LaunchArgumentContractMismatch;
        }

        if (string.Equals(configuration.TargetId, SteamWindowsTargetId,
                StringComparison.Ordinal))
        {
            return configuration.ProviderSelectionMode ==
                       ProviderSelectionMode.ExternalLaunchArgumentRequired &&
                   string.Equals(configuration.ExpectedProviderId, SteamProviderId,
                       StringComparison.Ordinal) &&
                   SequenceEqual(
                       configuration.ExpectedLaunchArguments,
                       new[] { ProviderSelectorArgument, SteamProviderId })
                ? WindowsDistributionValidationFailure.None
                : WindowsDistributionValidationFailure.LaunchArgumentContractMismatch;
        }

        return WindowsDistributionValidationFailure.None;
    }

    public static WindowsDistributionValidationFailure ValidateLaunchArguments(
        WindowsDistributionTargetConfiguration configuration,
        IEnumerable<string> launchArguments)
    {
        var configurationFailure = ValidateConfiguration(configuration);
        if (configurationFailure != WindowsDistributionValidationFailure.None)
        {
            return configurationFailure;
        }

        return SequenceEqual(
                configuration.ExpectedLaunchArguments,
                launchArguments == null ? Array.Empty<string>() : launchArguments.ToArray())
            ? WindowsDistributionValidationFailure.None
            : WindowsDistributionValidationFailure.LaunchArgumentContractMismatch;
    }

    public static WindowsDistributionValidationFailure ValidatePromotedArtifactInventory(
        WindowsDistributionTargetConfiguration configuration,
        IEnumerable<string> normalizedArtifactPaths)
    {
        var configurationFailure = ValidateConfiguration(configuration);
        if (configurationFailure != WindowsDistributionValidationFailure.None)
        {
            return configurationFailure;
        }

        var artifactPaths = (normalizedArtifactPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Replace('\\', '/'))
            .ToArray();
        var fileNames = new HashSet<string>(
            artifactPaths.Select(Path.GetFileName),
            StringComparer.OrdinalIgnoreCase);

        var requiresPublicNotice = configuration.RequiredArtifacts.Contains(
            ThirdPartyNoticesArtifact, StringComparer.OrdinalIgnoreCase);
        if (requiresPublicNotice && !artifactPaths.Any(IsCanonicalPublicNoticePath))
        {
            return WindowsDistributionValidationFailure.RequiredArtifactMissing;
        }

        if (configuration.RequiredArtifacts.Any(required =>
                !string.Equals(
                    required,
                    ThirdPartyNoticesArtifact,
                    StringComparison.OrdinalIgnoreCase) &&
                !fileNames.Contains(required)))
        {
            return WindowsDistributionValidationFailure.RequiredArtifactMissing;
        }

        if (configuration.ForbiddenArtifacts.Any(fileNames.Contains))
        {
            return WindowsDistributionValidationFailure.ForbiddenArtifactPresent;
        }

        return WindowsDistributionValidationFailure.None;
    }

    private static bool IsCanonicalPublicNoticePath(string path)
    {
        return string.Equals(
                   path,
                   ThirdPartyNoticesArtifact,
                   StringComparison.Ordinal) ||
               string.Equals(
                   path,
                   "payload/" + ThirdPartyNoticesArtifact,
                   StringComparison.Ordinal);
    }

    private static bool ValidTokens(IEnumerable<string> values)
    {
        return values != null && values.All(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool SequenceEqual(
        IEnumerable<string> first,
        IEnumerable<string> second)
    {
        return first.SequenceEqual(second, StringComparer.Ordinal);
    }
}
