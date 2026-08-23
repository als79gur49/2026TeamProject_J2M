using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
    public static readonly IReadOnlyList<string> ThirdPartyNoticeRequiredFragments =
        Array.AsReadOnly(new[]
        {
            "License: BSD 3-Clause",
            "Source: https://github.com/Unity-UI-Extensions/com.unity.uiextensions",
            "Copyright (c) 2019",
            "License: MIT",
            "Source: https://github.com/rlabrecque/Steamworks.NET",
            "Copyright (c) 2013-2022 Riley Labrecque",
            "Component: steam_api64.dll",
            "Provider: Valve Corporation",
            "not licensed under the Steamworks.NET MIT License reproduced above.",
            "Orbitron\n\nCopyright 2018 The Orbitron Project Authors",
            "with Reserved Font Name: \"Orbitron\"",
            "Exo 2.0\n\nStyles included: Regular, SemiBold",
            "with Reserved Font Name 'Exo'",
            "Saira Condensed\n\nStyle included in the current build: SemiBold",
            "reserved font name \"Saira\".",
            "Climate Crisis KR\n\nStyles included: 2000, 2019",
            "Copyright 2022, NohType with Reserved Font Name \"Climate Crisis\"",
            "Liberation Sans\n\nDigitized data copyright (c) 2010 Google Corporation",
            "Copyright (c) 2012 Red Hat, Inc.",
            "with Reserved Font Name Liberation.",
        });

    private static readonly ThirdPartyNoticeBodyContract[] ThirdPartyNoticeBodyContracts =
    {
        new ThirdPartyNoticeBodyContract(
            "BSD-3-Clause",
            "Copyright (c) 2019",
            "SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.",
            "c6a4a8ed2a82b50bb6c71da4211ab64903d7c752e8c91b149a2364165aa717de"),
        new ThirdPartyNoticeBodyContract(
            "MIT",
            "The MIT License (MIT)",
            "THE SOFTWARE.",
            "5760a1a32c5b06c462ecacda9162d6987ae8fa3738f522e55dcd31120a190e4c"),
        new ThirdPartyNoticeBodyContract(
            "OFL-1.1",
            "SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007",
            "OTHER DEALINGS IN THE FONT SOFTWARE.",
            "a79eba37ac2bb75da2e17e0854aeeeebaa7e4a49fb120e63162f333df394831d"),
    };

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

    public static bool HasValidThirdPartyNoticeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var normalized = NormalizeLineEndings(content);
        var previousIndex = -1;
        foreach (var marker in ThirdPartyNoticeRequiredMarkers)
        {
            if (!TryGetUniqueOrdinalIndex(normalized, marker, out var markerIndex) ||
                markerIndex <= previousIndex)
            {
                return false;
            }

            previousIndex = markerIndex;
        }

        if (ThirdPartyNoticeRequiredFragments.Any(
                fragment => CountOrdinalOccurrences(normalized, fragment) != 1))
        {
            return false;
        }

        foreach (var contract in ThirdPartyNoticeBodyContracts)
        {
            if (!TryGetUniqueOrdinalIndex(normalized, contract.StartMarker, out var startIndex) ||
                !TryGetUniqueOrdinalIndex(normalized, contract.EndMarker, out var endIndex) ||
                endIndex < startIndex)
            {
                return false;
            }

            var body = normalized.Substring(
                startIndex,
                endIndex + contract.EndMarker.Length - startIndex);
            if (!string.Equals(
                    ComputeUtf8Sha256(body),
                    contract.ExpectedSha256,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
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

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static bool TryGetUniqueOrdinalIndex(
        string content,
        string value,
        out int index)
    {
        index = content.IndexOf(value, StringComparison.Ordinal);
        return index >= 0 &&
               content.IndexOf(
                   value,
                   index + value.Length,
                   StringComparison.Ordinal) < 0;
    }

    private static int CountOrdinalOccurrences(string content, string value)
    {
        var count = 0;
        var startIndex = 0;
        while (startIndex <= content.Length - value.Length)
        {
            var index = content.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            startIndex = index + value.Length;
        }

        return count;
    }

    private static string ComputeUtf8Sha256(string value)
    {
        using (var algorithm = SHA256.Create())
        {
            var hash = algorithm.ComputeHash(new UTF8Encoding(false).GetBytes(value));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
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

    private sealed class ThirdPartyNoticeBodyContract
    {
        public ThirdPartyNoticeBodyContract(
            string name,
            string startMarker,
            string endMarker,
            string expectedSha256)
        {
            Name = name;
            StartMarker = startMarker;
            EndMarker = endMarker;
            ExpectedSha256 = expectedSha256;
        }

        public string Name { get; }
        public string StartMarker { get; }
        public string EndMarker { get; }
        public string ExpectedSha256 { get; }
    }
}
