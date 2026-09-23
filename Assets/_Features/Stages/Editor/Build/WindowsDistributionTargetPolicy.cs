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
    public const string SystemIoHashingArtifact = "System.IO.Hashing.dll";
    public const string UnsafeArtifact =
        "System.Runtime.CompilerServices.Unsafe.dll";
    public const string ParticipantRestartArtifact = "Exhibition-Relaunch.ps1";
    public static readonly IReadOnlyList<string> CompletedResetHelperArtifacts = Array.AsReadOnly(new[]
    {
        "Restart-Experiment.ps1",
        "RestartExperiment.cs",
        "RestartExperimentWindows.cs",
        "RestartExperimentNativeProbe.cs",
    });
    public const string ThirdPartyNoticesArtifact = "ThirdPartyNotices.txt";
    public const string UnityPlayerThirdPartyNoticesArtifact =
        "UnityPlayerThirdPartyNotices.pdf";
    public const int UnityPlayerThirdPartyNoticesSize = 132262;
    public const string UnityPlayerThirdPartyNoticesSha256 =
        "7bed0e6f6646552f9262903b62863c693074ac89ccf6291ead7e876033a29623";
    private const string UnityCompanionLicenseUrl =
        "https://unity.com/legal/licenses/unity-companion-license";
    private const int UnityCompanionLicenseUrlOccurrenceCount = 18;
    public static readonly IReadOnlyList<string> ThirdPartyNoticeRequiredMarkers =
        Array.AsReadOnly(new[]
        {
            "VectorQuake Third-Party Notices",
            "PART I - Required License and Legal Notices",
            "Unity Player Runtime Third-Party Notices",
            "Unity UI Extensions",
            "Platform-Conditional Required Notices",
            "Steamworks.NET (Steam distribution only)",
            "Valve Steamworks SDK Redistributable (Steam distribution only)",
            "Unity Package Notices",
            "Open Font Software",
            "SIL OPEN FONT LICENSE Version 1.1",
            "PART II - Licensed Third-Party Asset Disclosure",
            "Commercial Third-Party Assets",
            "Ovani Sound Audio Assets",
        });
    public static readonly IReadOnlyList<string> ThirdPartyNoticeRequiredFragments =
        Array.AsReadOnly(new[]
        {
            "This part contains notices, license texts, copyright statements, and " +
            "other\nlegal disclosures required for components distributed with " +
            "this product.",
            "This part identifies the commercially licensed third-party assets " +
            "currently\nconfirmed for this product.",
            "The providers retain copyright and ownership; the\nindividual or legal " +
            "entity responsible for this product has acquired the rights\ngranted " +
            "under the applicable licenses to use the assets in, and distribute " +
            "them\nas part of, this product. This disclosure does not transfer or " +
            "sublicense the\nunderlying assets to recipients.",
            "provided separately in UnityPlayerThirdPartyNotices.pdf.",
            "Product: Unity Player\nPlatform: Windows\n" +
            "Scripting backend: Mono\nVersion: 6000.3.11f1",
            "Player_Windows_Mono_6000_3_11f1_b7ab078964.pdf",
            "License: BSD 3-Clause",
            "Source: https://github.com/Unity-UI-Extensions/com.unity.uiextensions",
            "License: MIT",
            "Source: https://github.com/rlabrecque/Steamworks.NET",
            "Copyright (c) 2013-2022 Riley Labrecque",
            "Component: steam_api64.dll",
            "Provider: Valve Corporation",
            "not licensed under the Steamworks.NET MIT License reproduced above.",
            "DOTween Pro\nProvider: Demigiant / Daniele Giardini",
            "AllSky - 220+ Sky / Skybox Set\nProvider: rpgwhitelock\n" +
            "Source: https://assetstore.unity.com/packages/2d/textures-materials/sky/" +
            "allsky-220-sky-skybox-set-10109",
            "INTERFACE - Sci-Fi Soldier HUD\nProvider: Synty Studios",
            "Casual & Mobile Sound FX Pack Vol. 3",
            "Mutated Beings Sound FX Pack",
            "Runtime packages whose bundled third-party notices are reproduced below:",
            "com.unity.cinemachine 3.1.6",
            "com.unity.nuget.newtonsoft-json 3.2.2",
            "com.unity.localization 1.5.12",
            "com.unity.visualscripting 1.9.10",
            "com.unity.render-pipelines.universal 17.3.0",
            "com.unity.render-pipelines.core 17.3.0",
            "com.unity.mathematics 1.3.3",
            "Copyright (c) 2023 Unity Technologies ApS",
            "com.unity.burst 1.8.28",
            "Runtime packages licensed under the Unity Companion License without a " +
            "separate\nbundled third-party notice in the installed package:",
            "com.unity.collections 2.6.2\n" +
            "Copyright (c) 2024 Unity Technologies",
            "com.unity.addressables 2.9.1\n" +
            "Copyright (c) 2020 Unity Technologies ApS",
            "com.unity.ai.navigation 2.0.11\n" +
            "Copyright (c) 2016 Unity Technologies ApS",
            "com.unity.inputsystem 1.19.0\n" +
            "Copyright (c) 2024 Unity Technologies",
            "com.unity.shadergraph 17.3.0\n" +
            "Copyright (c) 2020 Unity Technologies ApS",
            "com.unity.splines 2.8.3\n" +
            "Copyright (c) 2024 Unity Technologies ApS",
            "com.unity.timeline 1.8.11\n" +
            "Copyright (c) 2023 Unity Technologies",
            "com.unity.ugui 2.0.0\n" +
            "Copyright (c) 2015-2020 Unity Technologies ApS",
            "com.unity.render-pipelines.universal-config 17.0.3\n" +
            "Copyright (c) 2020 Unity Technologies ApS",
            "com.unity.profiling.core 1.0.3\n" +
            "Copyright (c) 2020 Unity Technologies ApS",
            "com.unity.scriptablebuildpipeline 2.6.1\n" +
            "Copyright (c) 2020 Unity Technologies ApS",
            "----- BEGIN NOTICE: unity-companion-license-v1.4 -----",
            "5. Notices & Third-Party Rights. This License, including the copyright " +
            "notice",
            "# [Clipper](http://www.angusj.com/delphi/clipper.php)",
            "Component Name: Newtonsoft.Json\n\nLicense Type: MIT\n\nThe MIT License (MIT)",
            "Component Name: **SmartFormat**",
            "Component Name: AQN Parser",
            "Component Name: Full Serializer",
            "Component Name: Ensure.That",
            "Component Name: NCalc",
            "Component Name: Antlr 3 Runtime",
            "Component Name: FXAA3_11.h (renamed to FXAA3_11.hlsl)",
            "Component Name: RadeonRays 4.1",
            "Component Name: Sobol sampler",
            "Component Name: LLVM",
            "Component Name: mimalloc",
            "Orbitron\n\nCopyright 2018 The Orbitron Project Authors",
            "with Reserved Font Name: \"Orbitron\"",
            "Exo 2.0\n\nStyles included: Regular, SemiBold",
            "with Reserved Font Name 'Exo'",
            "Saira Condensed\n\nStyle included in the current build: SemiBold",
            "reserved font name \"Saira\".",
            "Noto Sans JP / Noto Sans SC\n\n" +
            "Styles included: JP Regular, JP Bold, SC Regular, SC Bold",
            "Copyright 2014-2021 Adobe (http://www.adobe.com/).\n" +
            "Noto is a trademark of Google Inc.",
            "Source: https://github.com/notofonts/noto-cjk",
            "static TextMesh Pro SDF rendering assets generated from\n" +
            "the listed font software.",
            "KBO Dia Gothic\n---------------\n\nStyles included: Light, Medium",
            "Copyright and intellectual property owner: Korea Baseball Organization (KBO)",
            "Commercial use and software embedding are permitted within the official scope.",
            "CI / BI use is not permitted.",
            "The font itself may not be sold. The supplied distribution form must be kept;",
            "do not edit the TTF files or redistribute a modified or adapted font.",
            "official terms also restrict illegal sites, false or exaggerated advertising,",
            "printed materials and advertising materials (including online advertising)",
            "made with the font for KBO promotion, and the user may request otherwise.",
            "On 2026-09-23 KST, the project release\nowner confirmed that the required " +
            "KBO/KBOP clearance",
            "The private rights\nevidence ledger, rather than this public summary, " +
            "is the authority",
            "This confirmation does not permit standalone font sales, source TTF\n" +
            "modification, CI / BI use",
            "No explicit end-credit or license-file bundling requirement was identified in",
            "https://www.koreabaseball.com/Reference/etc/KboFont.aspx",
            "Official KBO Dia Gothic License Guide Ver.2 (PDF):",
            "kbop@koreabaseball.or.kr",
            "Liberation Sans\n\nDigitized data copyright (c) 2010 Google Corporation",
            "Copyright (c) 2012 Red Hat, Inc.",
            "with Reserved Font Name Liberation.",
        });
    public static readonly IReadOnlyList<string> ThirdPartyNoticeForbiddenFragments =
        Array.AsReadOnly(new[]
        {
            "CI / BI use is permitted.",
            "KBO has approved TextMesh Pro SDF",
            "TextMesh Pro SDF distribution is explicitly approved",
            "TextMesh Pro SDF distribution is permitted by KBO",
        });

    private static readonly ThirdPartyNoticeBodyContract[] ThirdPartyNoticeBodyContracts =
    {
        new ThirdPartyNoticeBodyContract(
            "unity-ui-extensions-bsd-3-clause",
            "c6a4a8ed2a82b50bb6c71da4211ab64903d7c752e8c91b149a2364165aa717de"),
        new ThirdPartyNoticeBodyContract(
            "steamworks-net-mit",
            "5760a1a32c5b06c462ecacda9162d6987ae8fa3738f522e55dcd31120a190e4c"),
        new ThirdPartyNoticeBodyContract(
            "unity-companion-license-v1.4",
            "2e27959750ed5f01d00e1b82e520ca2e9d0115570f4dcc8adf0534fe3d06bd4a"),
        new ThirdPartyNoticeBodyContract(
            "upm-cinemachine-3.1.6",
            "678229d5dd2445ce43fc6946384bd1d1b6fdfc9c536685a6cc9aac36163758c8"),
        new ThirdPartyNoticeBodyContract(
            "upm-newtonsoft-json-3.2.2",
            "3adf7770d103fc1c5f9830d41003d961ed2e52efa2f6f76c59275e744df70f26"),
        new ThirdPartyNoticeBodyContract(
            "upm-localization-smartformat-1.5.12",
            "67223356b8edd8ba7607c5096dfa235ff8e8ddd821fd37666df93928863962b2"),
        new ThirdPartyNoticeBodyContract(
            "upm-visualscripting-runtime-1.9.10",
            "b127bcfedf86708096ff926af6fd40d3025f762e561eaf029cdae20969ba043f"),
        new ThirdPartyNoticeBodyContract(
            "upm-urp-fxaa-17.3.0",
            "383daf405e3a8569b3e591b09970ba040abd5491615fe17868205f05627e138a"),
        new ThirdPartyNoticeBodyContract(
            "upm-render-pipelines-core-runtime-17.3.0",
            "6a43a4582d54e450f53f7596e068a3dac8486e2c94b04839139c548e3725d7f4"),
        new ThirdPartyNoticeBodyContract(
            "upm-mathematics-noise-1.3.3",
            "f18920bdf3c091dee9a97bcad9a93dd2d2a8343aaf558db46f4bc4f82f3c20b0"),
        new ThirdPartyNoticeBodyContract(
            "upm-burst-1.8.28",
            "496c579fc20d6bfcb4e19cf0eafd85eb20dc2d3a93e545f11bcfec79aca76ea0"),
        new ThirdPartyNoticeBodyContract(
            "open-font-license-1.1",
            "6f9807a7127177a76fae2209e11f4b90d01a50cbfdb3dfe5704c2dcb2c68a3d1"),
    };

    public static readonly WindowsDistributionTargetConfiguration DirectWindows =
        new WindowsDistributionTargetConfiguration(
            DirectWindowsTargetId,
            "DirectWindows",
            ProviderSelectionMode.DefaultWhenUnspecified,
            LocalProviderId,
            Array.Empty<string>(),
            new[]
            {
                ParticipantRestartArtifact,
                "Restart-Experiment.ps1",
                "RestartExperiment.cs",
                "RestartExperimentWindows.cs",
                "RestartExperimentNativeProbe.cs",
                ThirdPartyNoticesArtifact,
                UnityPlayerThirdPartyNoticesArtifact,
            },
            new[]
            {
                SteamNativeArtifact,
                SteamManagedBindingArtifact,
                SteamAppIdArtifact,
                SystemIoHashingArtifact,
                UnsafeArtifact,
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
                ParticipantRestartArtifact,
                "Restart-Experiment.ps1",
                "RestartExperiment.cs",
                "RestartExperimentWindows.cs",
                "RestartExperimentNativeProbe.cs",
                ThirdPartyNoticesArtifact,
                UnityPlayerThirdPartyNoticesArtifact,
                SteamNativeArtifact,
                SteamManagedBindingArtifact,
                "Game.Exhibition.Application.dll",
                "Game.Exhibition.Integration.dll",
            },
            new[]
            {
                SteamAppIdArtifact,
                SystemIoHashingArtifact,
                UnsafeArtifact,
            });

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

        var publicNoticeArtifacts = new[]
        {
            ParticipantRestartArtifact,
            ThirdPartyNoticesArtifact,
            UnityPlayerThirdPartyNoticesArtifact,
        };
        if (publicNoticeArtifacts.Any(publicNotice =>
                configuration.RequiredArtifacts.Contains(
                    publicNotice, StringComparer.OrdinalIgnoreCase) &&
                !artifactPaths.Any(path => IsCanonicalPublicNoticePath(
                    path, publicNotice))))
        {
            return WindowsDistributionValidationFailure.RequiredArtifactMissing;
        }

        if (configuration.RequiredArtifacts.Any(required =>
                !publicNoticeArtifacts.Contains(
                    required, StringComparer.OrdinalIgnoreCase) &&
                !fileNames.Contains(required)))
        {
            return WindowsDistributionValidationFailure.RequiredArtifactMissing;
        }

        foreach (var helper in CompletedResetHelperArtifacts)
            if (configuration.RequiredArtifacts.Contains(helper) &&
                !artifactPaths.Any(path => string.Equals(path, "RestartExperiment/" + helper, StringComparison.Ordinal) ||
                    string.Equals(path, "payload/RestartExperiment/" + helper, StringComparison.Ordinal)))
                return WindowsDistributionValidationFailure.RequiredArtifactMissing;

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
        if (ThirdPartyNoticeForbiddenFragments.Any(
                fragment => CountOrdinalOccurrences(normalized, fragment) != 0))
        {
            return false;
        }
        if (CountOrdinalOccurrences(normalized, UnityCompanionLicenseUrl) !=
            UnityCompanionLicenseUrlOccurrenceCount)
        {
            return false;
        }

        foreach (var contract in ThirdPartyNoticeBodyContracts)
        {
            var startMarker =
                "----- BEGIN NOTICE: " + contract.Name + " -----\n";
            var endMarker =
                "\n----- END NOTICE: " + contract.Name + " -----";
            if (!TryGetUniqueOrdinalIndex(normalized, startMarker, out var startIndex) ||
                !TryGetUniqueOrdinalIndex(normalized, endMarker, out var endIndex))
            {
                return false;
            }

            startIndex += startMarker.Length;
            if (endIndex < startIndex)
            {
                return false;
            }
            var body = normalized.Substring(
                startIndex,
                endIndex - startIndex);
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

    public static bool HasValidUnityPlayerThirdPartyNoticeContent(byte[] content)
    {
        if (content == null ||
            content.Length != UnityPlayerThirdPartyNoticesSize ||
            content.Length < 5 ||
            !string.Equals(
                Encoding.ASCII.GetString(content, 0, 5),
                "%PDF-",
                StringComparison.Ordinal))
        {
            return false;
        }

        using (var algorithm = SHA256.Create())
        {
            var hash = algorithm.ComputeHash(content);
            var actual = BitConverter.ToString(hash)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            return string.Equals(
                actual,
                UnityPlayerThirdPartyNoticesSha256,
                StringComparison.Ordinal);
        }
    }

    private static bool IsCanonicalPublicNoticePath(string path, string artifact)
    {
        return string.Equals(
                   path,
                   artifact,
                   StringComparison.Ordinal) ||
               string.Equals(
                   path,
                   "payload/" + artifact,
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
            string expectedSha256)
        {
            Name = name;
            ExpectedSha256 = expectedSha256;
        }

        public string Name { get; }
        public string ExpectedSha256 { get; }
    }
}
