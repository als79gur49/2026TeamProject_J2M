[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$UnityExe = "C:\Users\user\Desktop\6000.3.11f1\Editor\Unity.exe",
    [string]$OutputRoot = "C:\Users\user\Documents\VectorQuake-Release-Builds",
    [string]$BuildSourceRoot = "C:\VQBuildSources",
    [string]$PreparedBuildSourceRoot = "",
    [string]$RunId = ([DateTime]::UtcNow.ToString("yyyyMMddTHHmmssfffZ")),
    [ValidateSet("CanonicalStore", "BackendComparison")]
    [string]$BuildIntent = "CanonicalStore",
    [string]$Backend = "Mono",
    [string]$BackendComparisonId = "",
    [string]$DistributionTarget = "",
    [ValidateSet("InternalRc", "StoreDistributable")]
    [string]$PayloadAudience = "StoreDistributable",
    [string[]]$AllowUntrackedRoot = @(
        "TestLogs/CampaignLaunchOwnershipE2E",
        "TestLogs/MainReReview",
        "TestLogs/SceneLoadDiagnosticsQA",
        "TestLogs/UndoPreflight",
        "TestLogs/UndoPreflight-MainVfxDuplication"
    )
)

function Resolve-WindowsDistributionTargetPolicy {
    param([Parameter(Mandatory)][string]$TargetId)
    switch -CaseSensitive ($TargetId) {
        "direct-windows" {
            return [pscustomobject][ordered]@{
                TargetId = "direct-windows"
                ArtifactDirectoryName = "DirectWindows"
                ProviderSelectionMode = "DefaultWhenUnspecified"
                ExpectedProviderId = "local"
                ExpectedLaunchArguments = @()
                RequiredArtifacts = @()
                ForbiddenArtifacts = @(
                    "steam_api64.dll",
                    "com.rlabrecque.steamworks.net.dll",
                    "steam_appid.txt"
                )
                ExpectedStoreLaunch = "VectorQuake.exe"
            }
        }
        "steam-windows" {
            return [pscustomobject][ordered]@{
                TargetId = "steam-windows"
                ArtifactDirectoryName = "SteamWindows"
                ProviderSelectionMode = "ExternalLaunchArgumentRequired"
                ExpectedProviderId = "steam"
                ExpectedLaunchArguments = @("-j2mPlatformProvider", "steam")
                RequiredArtifacts = @(
                    "steam_api64.dll",
                    "com.rlabrecque.steamworks.net.dll"
                )
                ForbiddenArtifacts = @("steam_appid.txt")
                ExpectedStoreLaunch =
                    "VectorQuake.exe -j2mPlatformProvider steam"
            }
        }
        default { throw "Unsupported Windows distribution target: $TargetId" }
    }
}

function Resolve-StoreBackendPolicy {
    param(
        [string]$Value = "Mono",
        [ValidateSet("CanonicalStore", "BackendComparison")]
        [string]$Intent = "CanonicalStore",
        [ValidateSet("InternalRc", "StoreDistributable")]
        [string]$Audience = $(if ($Intent -ceq "CanonicalStore") {
            "StoreDistributable"
        } else {
            "InternalRc"
        })
    )
    if ($Intent -ceq "CanonicalStore" -and
        ($Value -cne "Mono" -or $Audience -cne "StoreDistributable")) {
        throw "CanonicalStore requires Mono and StoreDistributable."
    }
    if ($Intent -ceq "BackendComparison" -and $Audience -cne "InternalRc") {
        throw "BackendComparison artifacts require InternalRc."
    }
    switch -CaseSensitive ($Value) {
        "Mono" {
            $canonical = $Intent -ceq "CanonicalStore"
            return [pscustomobject][ordered]@{
                BuildIntent = $Intent
                Backend = "Mono"
                ScriptingBackend = "Mono2x"
                ManagedStrippingLevel = "Disabled"
                Il2CppCompilerConfiguration = "Release"
                ComparisonRole = if ($canonical) { "CanonicalStore" } else { "MonoControl" }
                Configuration = if ($canonical) {
                    "Windows-x64-Store-Mono-LogOn"
                } else {
                    "Windows-x64-NonDevelopment-Mono"
                }
                StoreConfigurationSchema = if ($canonical) { "1.0" } else {
                    "comparison-1.0"
                }
                StoreConfigurationId = if ($canonical) {
                    "windows-x64-store-mono-logon-v1"
                } else {
                    "not-canonical-backend-comparison"
                }
                PlayerLogEnabled = $true
                LogPolicyId = "local-player-log-no-auto-upload-v1"
                AutomaticLogUpload = $false
                PayloadAudience = $Audience
            }
        }
        "IL2CPP" {
            if ($Intent -cne "BackendComparison") {
                throw "IL2CPP is not a canonical Store configuration."
            }
            return [pscustomobject][ordered]@{
                BuildIntent = $Intent
                Backend = "IL2CPP"
                ScriptingBackend = "IL2CPP"
                ManagedStrippingLevel = "Minimal"
                Il2CppCompilerConfiguration = "Release"
                ComparisonRole = "IL2CPPCandidate"
                Configuration = "Windows-x64-NonDevelopment-IL2CPP"
                StoreConfigurationSchema = "comparison-1.0"
                StoreConfigurationId = "not-canonical-backend-comparison"
                PlayerLogEnabled = $true
                LogPolicyId = "local-player-log-no-auto-upload-v1"
                AutomaticLogUpload = $false
                PayloadAudience = $Audience
            }
        }
        default { throw "Unsupported Store backend: $Value" }
    }
}

$script:ReleaseExitCodes = [ordered]@{
    GitPreflightFailure = 100
    UnknownUntrackedFailure = 101
    ProcessGateFailure = 102
    OutputCollision = 103
    DetachedSourceFailure = 104
    UnityInvocationFailure = 105
    SourceDriftDetected = 106
    ManifestGenerationFailure = 107
    ManifestVerificationFailure = 108
    ControlFileConsistencyFailure = 109
    PromotionFailure = 110
    ArtifactQuarantined = 111
    WrapperInternalError = 112
    BuildEvidenceFailure = 113
    ArtifactProvenanceFailure = 114
    BuildSourcePathBudgetFailure = 115
    UnsupportedConfiguration = 116
}
try {
    $script:BackendPolicy = Resolve-StoreBackendPolicy $Backend $BuildIntent $PayloadAudience
    $script:DistributionPolicy =
        Resolve-WindowsDistributionTargetPolicy $DistributionTarget
} catch {
    if ($env:VECTORQUAKE_RELEASE_WRAPPER_TEST_MODE -eq "1") { throw }
    Write-Error "[configuration][$($script:ReleaseExitCodes.UnsupportedConfiguration)] $($_.Exception.Message)"
    exit $script:ReleaseExitCodes.UnsupportedConfiguration
}
$script:ConfigurationName = [string]$script:BackendPolicy.Configuration
$script:ExecutingWrapperSourcePath = $PSCommandPath
$script:ConfigurationPathName = Join-Path `
    ([string]$script:BackendPolicy.Configuration) `
    ([string]$script:DistributionPolicy.ArtifactDirectoryName)
$script:MetadataSchemaVersion = "4.0"
$script:ReportSummarySchemaVersion = "4.0"
$script:ReportDetailsSchemaVersion = "3.0"
$script:ConfigurationSummarySchemaVersion = "2.0"
$script:ProvenanceSchemaVersion = "4.0"
$script:MaxLegacyWindowsPathLength = 259
$script:CriticalImporterRelativePaths = @(
    (
        "Library\PackageCache\com.unity.collections@000000000000\" +
        "Unity.Collections.Tests\System.Runtime.CompilerServices.Unsafe\" +
        "System.Runtime.CompilerServices.Unsafe.dll"
    ),
    (
        "Library\PackageCache\com.unity.render-pipelines.universal@000000000000\" +
        "Runtime\RendererFeatures\SurfaceCacheGI\SurfaceCacheCore\" +
        "RestirCandidateTemporal.urtshader"
    ),
    (
        "Library\PackageCache\com.unity.render-pipelines.core@000000000000\" +
        "Editor\Lighting\ProbeVolume\RenderingLayerMask\" +
        "TraceRenderingLayerMask.urtshader"
    )
)
$script:ControlFileNames = @(
    "files.sha256",
    "files.sha256.sha256",
    "artifact-provenance.json",
    "SUCCESS.json"
)

function Get-ReleaseExitCodes { return $script:ReleaseExitCodes }

function Test-BuildSourcePathBudget {
    param([Parameter(Mandatory)][string]$DetachedSourcePath)
    return (Get-BuildSourceCriticalPathLength $DetachedSourcePath) -le
        $script:MaxLegacyWindowsPathLength
}

function Get-BuildSourceCriticalPathLength {
    param([Parameter(Mandatory)][string]$DetachedSourcePath)
    return @($script:CriticalImporterRelativePaths | ForEach-Object {
        [IO.Path]::GetFullPath((Join-Path $DetachedSourcePath $_)).Length
    } | Measure-Object -Maximum).Maximum
}

function Resolve-BuildSourcePlan {
    param(
        [Parameter(Mandatory)][string]$BuildSourceRoot,
        [string]$PreparedBuildSourceRoot = "",
        [Parameter(Mandatory)][string]$SourceSha,
        [Parameter(Mandatory)][string]$RunId
    )
    if (-not [string]::IsNullOrWhiteSpace($PreparedBuildSourceRoot)) {
        return [pscustomobject][ordered]@{
            Path = [IO.Path]::GetFullPath($PreparedBuildSourceRoot)
            RequiresCreation = $false
        }
    }
    return [pscustomobject][ordered]@{
        Path = Join-Path (Join-Path $BuildSourceRoot $SourceSha) $RunId
        RequiresCreation = $true
    }
}

function New-ReleaseEvidenceExpectation {
    param(
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][string]$ArtifactId,
        [Parameter(Mandatory)][string]$SourceSha,
        [Parameter(Mandatory)][string]$SourceTree,
        [Parameter(Mandatory)][string]$EntrySourcePath,
        [Parameter(Mandatory)][string]$PolicySourcePath,
        [Parameter(Mandatory)][string]$DetachedWrapperSourcePath,
        [Parameter(Mandatory)][string]$ExecutingWrapperSourcePath,
        [string]$Configuration = $script:ConfigurationName,
        [string]$Backend = $script:BackendPolicy.ScriptingBackend,
        [string]$BackendIdentity = $script:BackendPolicy.Backend,
        [string]$ManagedStrippingLevel = $script:BackendPolicy.ManagedStrippingLevel,
        [string]$Il2CppCompilerConfiguration =
            $script:BackendPolicy.Il2CppCompilerConfiguration,
        [string]$BackendComparisonId = "comparison",
        [string]$ComparisonRole = $script:BackendPolicy.ComparisonRole,
        [string]$BuildIntent = $script:BackendPolicy.BuildIntent,
        [string]$StoreConfigurationSchema =
            $script:BackendPolicy.StoreConfigurationSchema,
        [string]$StoreConfigurationId = $script:BackendPolicy.StoreConfigurationId,
        [bool]$PlayerLogEnabled = $script:BackendPolicy.PlayerLogEnabled,
        [string]$LogPolicyId = $script:BackendPolicy.LogPolicyId,
        [bool]$AutomaticLogUpload = $script:BackendPolicy.AutomaticLogUpload,
        [string]$PayloadAudience = $script:BackendPolicy.PayloadAudience,
        [string]$DistributionTargetId = $script:DistributionPolicy.TargetId,
        [string]$ProviderSelectionMode =
            $script:DistributionPolicy.ProviderSelectionMode,
        [string]$ExpectedProviderId = $script:DistributionPolicy.ExpectedProviderId,
        [string[]]$ExpectedLaunchArguments =
            @($script:DistributionPolicy.ExpectedLaunchArguments),
        [string[]]$RequiredArtifacts = @($script:DistributionPolicy.RequiredArtifacts),
        [string[]]$ForbiddenArtifacts = @($script:DistributionPolicy.ForbiddenArtifacts),
        [string]$ExpectedStoreLaunch = $script:DistributionPolicy.ExpectedStoreLaunch
    )
    foreach ($path in @(
        $EntrySourcePath,
        $PolicySourcePath,
        $DetachedWrapperSourcePath,
        $ExecutingWrapperSourcePath
    )) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Expected committed source file is missing: $path"
        }
    }
    $detachedWrapperHash = Get-Sha256 $DetachedWrapperSourcePath
    $executingWrapperHash = Get-Sha256 $ExecutingWrapperSourcePath
    if ($detachedWrapperHash -cne $executingWrapperHash) {
        throw "Executing wrapper bytes do not match the detached committed source."
    }
    return [pscustomobject][ordered]@{
        RunId = $RunId
        ArtifactId = $ArtifactId
        SourceSha = $SourceSha
        SourceTree = $SourceTree
        Configuration = $Configuration
        Backend = $BackendIdentity
        ScriptingBackend = $Backend
        ManagedStrippingLevel = $ManagedStrippingLevel
        Il2CppCompilerConfiguration = $Il2CppCompilerConfiguration
        BackendComparisonId = $BackendComparisonId
        ComparisonRole = $ComparisonRole
        BuildIntent = $BuildIntent
        StoreConfigurationSchema = $StoreConfigurationSchema
        StoreConfigurationId = $StoreConfigurationId
        PlayerLogEnabled = $PlayerLogEnabled
        LogPolicyId = $LogPolicyId
        AutomaticLogUpload = $AutomaticLogUpload
        PayloadAudience = $PayloadAudience
        DistributionTargetId = $DistributionTargetId
        ProviderSelectionMode = $ProviderSelectionMode
        ExpectedProviderId = $ExpectedProviderId
        ExpectedLaunchArguments = @($ExpectedLaunchArguments)
        RequiredArtifacts = @($RequiredArtifacts)
        ForbiddenArtifacts = @($ForbiddenArtifacts)
        ExpectedStoreLaunch = $ExpectedStoreLaunch
        EntrySourcePath = $EntrySourcePath
        PolicySourcePath = $PolicySourcePath
        DetachedWrapperSourcePath = $DetachedWrapperSourcePath
        ExecutingWrapperSourcePath = $ExecutingWrapperSourcePath
        EntrySourceSha256 = Get-Sha256 $EntrySourcePath
        PolicySourceSha256 = Get-Sha256 $PolicySourcePath
        WrapperSourceSha256 = $detachedWrapperHash
    }
}

function Get-NormalizedRelativePath {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Path)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [IO.Path]::GetFullPath($Path)
    if (-not $pathFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside root: $Path"
    }
    return $pathFull.Substring($rootFull.Length + 1).Replace('\', '/')
}

function Test-ApprovedUntrackedPaths {
    param([string[]]$Paths, [string[]]$AllowedRoots)
    foreach ($pathValue in @($Paths)) {
        $normalized = $pathValue.Replace('\', '/').TrimEnd('/')
        $accepted = $false
        foreach ($root in @($AllowedRoots)) {
            $allowed = $root.Replace('\', '/').TrimEnd('/')
            if ($normalized -eq $allowed -or
                $normalized.StartsWith($allowed + '/', [StringComparison]::Ordinal)) {
                $accepted = $true
                break
            }
        }
        if (-not $accepted) { return $false }
    }
    return $true
}

function Test-GitState {
    param(
        [Parameter(Mandatory)]$Snapshot,
        [string[]]$AllowedRoots = @(),
        [switch]$RequireNoUntracked
    )
    if (@($Snapshot.Tracked).Count -ne 0 -or @($Snapshot.Staged).Count -ne 0) {
        return $false
    }
    if ($RequireNoUntracked) { return @($Snapshot.Untracked).Count -eq 0 }
    return Test-ApprovedUntrackedPaths -Paths @($Snapshot.Untracked) -AllowedRoots $AllowedRoots
}

function Test-ReleaseProcessGate {
    param(
        [object[]]$Processes,
        [string[]]$RepositoryFamilyPaths,
        [Nullable[int]]$AllowedUnityPid = $null
    )
    return (Get-ReleaseProcessGateResult -Processes $Processes `
        -RepositoryFamilyPaths $RepositoryFamilyPaths `
        -AllowedUnityPid $AllowedUnityPid).Allowed
}

function Get-ReleaseProcessGateResult {
    param(
        [object[]]$Processes,
        [string[]]$RepositoryFamilyPaths,
        [Nullable[int]]$AllowedUnityPid = $null
    )
    $byId = @{}
    foreach ($process in @($Processes)) { $byId[[int]$process.ProcessId] = $process }
    $rejected = @()
    $accepted = @()
    foreach ($process in @($Processes)) {
        $name = [string]$process.Name
        if ($name -ieq "VectorQuake.exe") {
            $rejected += [pscustomobject][ordered]@{
                processId = [int]$process.ProcessId
                parentProcessId = [int]$process.ParentProcessId
                name = $name
                commandLine = [string]$process.CommandLine
                reason = "VectorQuakePlayerRunning"
            }
            continue
        }
        if ($name -ieq "Unity.exe") {
            if ($null -ne $AllowedUnityPid -and
                [int]$process.ProcessId -eq [int]$AllowedUnityPid) {
                $accepted += [pscustomobject][ordered]@{
                    processId = [int]$process.ProcessId
                    parentProcessId = [int]$process.ParentProcessId
                    name = $name
                    commandLine = [string]$process.CommandLine
                    attribution = "AllowedBuildUnity"
                }
                continue
            }
            $command = [string]$process.CommandLine
            $attributed = $false
            foreach ($familyPath in @($RepositoryFamilyPaths)) {
                if ($command.IndexOf($familyPath, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $attributed = $true
                    break
                }
            }
            # Repository-family and unattributed Unity are both fail-closed.
            $reason = if ($attributed) {
                "RepositoryFamilyUnity"
            } elseif ([string]::IsNullOrWhiteSpace($command)) {
                "UnattributedUnity"
            } else {
                "UnexpectedUnity"
            }
            $rejected += [pscustomobject][ordered]@{
                processId = [int]$process.ProcessId
                parentProcessId = [int]$process.ParentProcessId
                name = $name
                commandLine = $command
                reason = $reason
            }
            continue
        }
        if ($name -ieq "UnityCrashHandler64.exe") {
            $parentId = [int]$process.ParentProcessId
            if (-not $byId.ContainsKey($parentId)) {
                $rejected += [pscustomobject][ordered]@{
                    processId = [int]$process.ProcessId
                    parentProcessId = $parentId
                    name = $name
                    commandLine = [string]$process.CommandLine
                    reason = "CrashHandlerParentMissing"
                }
                continue
            }
            $parent = $byId[$parentId]
            if ([string]$parent.Name -ieq "Unity.exe") {
                if ($null -ne $AllowedUnityPid -and
                    $parentId -eq [int]$AllowedUnityPid) {
                    # Unity's own CrashHandler is an expected direct child of the
                    # one explicitly allowed build process.
                    $accepted += [pscustomobject][ordered]@{
                        processId = [int]$process.ProcessId
                        parentProcessId = $parentId
                        name = $name
                        commandLine = [string]$process.CommandLine
                        attribution = "AllowedBuildCrashHandler"
                    }
                    continue
                }
                $parentCommand = [string]$parent.CommandLine
                foreach ($familyPath in @($RepositoryFamilyPaths)) {
                    if ($parentCommand.IndexOf(
                            $familyPath, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                        $rejected += [pscustomobject][ordered]@{
                            processId = [int]$process.ProcessId
                            parentProcessId = $parentId
                            name = $name
                            commandLine = [string]$process.CommandLine
                            parentName = [string]$parent.Name
                            parentCommandLine = $parentCommand
                            reason = "RepositoryFamilyCrashHandler"
                        }
                        break
                    }
                }
            } elseif ([string]::IsNullOrWhiteSpace([string]$parent.Name)) {
                $rejected += [pscustomobject][ordered]@{
                    processId = [int]$process.ProcessId
                    parentProcessId = $parentId
                    name = $name
                    commandLine = [string]$process.CommandLine
                    reason = "CrashHandlerParentUnattributed"
                }
            } else {
                $accepted += [pscustomobject][ordered]@{
                    processId = [int]$process.ProcessId
                    parentProcessId = $parentId
                    name = $name
                    commandLine = [string]$process.CommandLine
                    parentName = [string]$parent.Name
                    attribution = "ClearlyAttributedOtherProduct"
                }
            }
        }
    }
    return [pscustomobject]@{
        Allowed = $rejected.Count -eq 0
        AcceptedProcesses = @($accepted)
        RejectedProcesses = @($rejected)
    }
}

function Write-ProcessGateDiagnostics {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)]$GateResult,
        [Nullable[int]]$AllowedUnityPid = $null
    )
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    [ordered]@{
        schemaVersion = $script:MetadataSchemaVersion
        phase = $Phase
        timestampUtc = [DateTime]::UtcNow.ToString("o")
        allowedUnityPid = if ($null -ne $AllowedUnityPid) {
            [int]$AllowedUnityPid
        } else { $null }
        allowed = [bool]$GateResult.Allowed
        acceptedProcesses = @($GateResult.AcceptedProcesses)
        rejectedProcesses = @($GateResult.RejectedProcesses)
    } | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-PrivateJson {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Value)
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    $Value | ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-ImmutablePowerShellTestEvidence {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$SourceSha,
        [Parameter(Mandatory)][string]$SourceTree,
        [Parameter(Mandatory)][bool]$SourceClean,
        [Parameter(Mandatory)][bool]$CommittedBytesMatched,
        [Parameter(Mandatory)][string]$WrapperPath,
        [Parameter(Mandatory)][string]$TestScriptPath,
        [Parameter(Mandatory)][string]$CommandLine,
        [Parameter(Mandatory)][string]$StartedUtc,
        [Parameter(Mandatory)][string]$CompletedUtc,
        [Parameter(Mandatory)][string]$PowerShellVersion,
        [Parameter(Mandatory)][int]$Selected,
        [Parameter(Mandatory)][int]$Passed,
        [Parameter(Mandatory)][int]$Failed,
        [int]$Skipped = 0,
        [string]$StoreConfigurationSchema = "1.0",
        [string]$StoreConfigurationId = "windows-x64-store-mono-logon-v1",
        [string]$BuildIntent = "CanonicalStore",
        [string]$Backend = "Mono",
        [string]$LogPolicyId = "local-player-log-no-auto-upload-v1",
        [bool]$PlayerLogEnabled = $true,
        [bool]$AutomaticLogUpload = $false,
        [string]$PayloadAudience = "StoreDistributable",
        [string[]]$Results = @()
    )
    $hashPath = "$Path.sha256"
    if ((Test-Path -LiteralPath $Path) -or (Test-Path -LiteralPath $hashPath)) {
        throw "Immutable PowerShell test evidence path already exists: $Path"
    }
    if (-not $SourceClean -or -not $CommittedBytesMatched) {
        throw "PowerShell test evidence requires exact clean committed wrapper and test bytes."
    }
    $record = [ordered]@{
        schemaVersion = "1.0"
        evidenceType = "WindowsReleasePowerShellTests"
        sourceSha = $SourceSha
        sourceTree = $SourceTree
        sourceClean = $SourceClean
        committedBytesMatched = $CommittedBytesMatched
        wrapperFile = "Tools/Build/Build-WindowsRelease.ps1"
        wrapperSha256 = Get-Sha256 $WrapperPath
        testScriptFile = "Tools/Build/Tests/Build-WindowsRelease.Tests.ps1"
        testScriptSha256 = Get-Sha256 $TestScriptPath
        commandLine = $CommandLine
        powershellVersion = $PowerShellVersion
        selected = $Selected
        passed = $Passed
        failed = $Failed
        skipped = $Skipped
        storeConfigurationSchema = $StoreConfigurationSchema
        storeConfigurationId = $StoreConfigurationId
        buildIntent = $BuildIntent
        backend = $Backend
        playerLogEnabled = $PlayerLogEnabled
        logPolicyId = $LogPolicyId
        automaticLogUpload = $AutomaticLogUpload
        payloadAudience = $PayloadAudience
        resultStatus = if ($Failed -eq 0) { "Passed" } else { "Failed" }
        startedUtc = $StartedUtc
        completedUtc = $CompletedUtc
        results = @($Results)
    }
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(
        ($record | ConvertTo-Json -Depth 8) + "`n")
    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None,
        4096,
        [IO.FileOptions]::WriteThrough)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    } finally {
        $stream.Dispose()
    }
    $selfHash = Get-Sha256 $Path
    $hashBytes = [Text.UTF8Encoding]::new($false).GetBytes(
        "$selfHash  $([IO.Path]::GetFileName($Path))`n")
    $hashStream = [IO.FileStream]::new(
        $hashPath,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None,
        4096,
        [IO.FileOptions]::WriteThrough)
    try {
        $hashStream.Write($hashBytes, 0, $hashBytes.Length)
        $hashStream.Flush($true)
    } finally {
        $hashStream.Dispose()
    }
    return [pscustomobject]@{
        Path = $Path
        Sha256Path = $hashPath
        Sha256 = $selfHash
    }
}

function Write-WrapperLog {
    param([string]$Path, [string]$Stage, [string]$Message)
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    Add-Content -LiteralPath $Path -Encoding UTF8 -Value (
        "{0} [{1}] {2}" -f [DateTime]::UtcNow.ToString("o"), $Stage, $Message)
}

function Convert-ToExtendedLengthPath {
    param([Parameter(Mandatory)][string]$Path)
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith('\\?\', [StringComparison]::Ordinal)) { return $full }
    if ($full.StartsWith('\\', [StringComparison]::Ordinal)) {
        return '\\?\UNC\' + $full.Substring(2)
    }
    return '\\?\' + $full
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)
    $stream = $null
    $algorithm = $null
    try {
        $stream = [IO.File]::OpenRead((Convert-ToExtendedLengthPath -Path $Path))
        $algorithm = [Security.Cryptography.SHA256]::Create()
        $bytes = $algorithm.ComputeHash($stream)
        return ([BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant())
    } finally {
        if ($null -ne $algorithm) { $algorithm.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

function Test-ExtendedLengthFileExists {
    param([Parameter(Mandatory)][string]$Path)
    return [IO.File]::Exists((Convert-ToExtendedLengthPath -Path $Path))
}

function Test-JsonProperty {
    param([Parameter(Mandatory)]$Value, [Parameter(Mandatory)][string]$Name)
    return $null -ne $Value.PSObject.Properties[$Name]
}

function Test-OrdinalArrayEqual {
    param($Actual, $Expected)
    $actualValues = @($Actual)
    $expectedValues = @($Expected)
    if ($actualValues.Count -ne $expectedValues.Count) { return $false }
    for ($index = 0; $index -lt $actualValues.Count; $index++) {
        if ([string]$actualValues[$index] -cne [string]$expectedValues[$index]) {
            return $false
        }
    }
    return $true
}

function New-BuildEvidenceResult {
    param([bool]$Allowed, [string]$Reason, $Metadata = $null,
        $Summary = $null, $Details = $null)
    return [pscustomobject]@{
        Allowed = $Allowed
        Reason = $Reason
        Metadata = $Metadata
        Summary = $Summary
        Details = $Details
    }
}

function Test-BuildEvidence {
    param(
        [Parameter(Mandatory)][string]$MetadataPath,
        [Parameter(Mandatory)][string]$SummaryPath,
        [Parameter(Mandatory)][string]$DetailsPath,
        $ExpectedIdentity = $null
    )
    foreach ($item in @(
        @{ Path = $MetadataPath; Reason = "MetadataMissing" },
        @{ Path = $SummaryPath; Reason = "ReportSummaryMissing" },
        @{ Path = $DetailsPath; Reason = "ReportDetailsMissing" }
    )) {
        if (-not (Test-Path -LiteralPath $item.Path -PathType Leaf)) {
            return New-BuildEvidenceResult $false $item.Reason
        }
    }

    try {
        $metadata = Get-Content -LiteralPath $MetadataPath -Raw | ConvertFrom-Json
        $summary = Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
        $details = Get-Content -LiteralPath $DetailsPath -Raw | ConvertFrom-Json
    } catch {
        return New-BuildEvidenceResult $false "EvidenceJsonInvalid"
    }

    if ($metadata.schemaVersion -cne $script:MetadataSchemaVersion -or
        $summary.schemaVersion -cne $script:ReportSummarySchemaVersion -or
        $details.schemaVersion -cne $script:ReportDetailsSchemaVersion) {
        return New-BuildEvidenceResult $false "EvidenceSchemaMismatch" `
            $metadata $summary $details
    }
    foreach ($required in @(
        @{ Value = $metadata; Name = "runId"; Reason = "MetadataIdentityMissing" },
        @{ Value = $metadata; Name = "artifactId"; Reason = "MetadataIdentityMissing" },
        @{ Value = $metadata; Name = "sourceSha"; Reason = "MetadataIdentityMissing" },
        @{ Value = $metadata; Name = "sourceTree"; Reason = "MetadataIdentityMissing" },
        @{ Value = $metadata; Name = "configuration"; Reason = "MetadataIdentityMissing" },
        @{ Value = $metadata; Name = "buildTarget"; Reason = "MetadataTargetMissing" },
        @{ Value = $metadata; Name = "architecture";
            Reason = "MetadataArchitectureMissing" },
        @{ Value = $metadata; Name = "buildIntent"; Reason = "MetadataIntentMissing" },
        @{ Value = $metadata; Name = "storeConfigurationSchema";
            Reason = "MetadataStoreSchemaMissing" },
        @{ Value = $metadata; Name = "storeConfigurationId";
            Reason = "MetadataStoreConfigurationIdMissing" },
        @{ Value = $metadata; Name = "backend"; Reason = "MetadataBackendMissing" },
        @{ Value = $metadata; Name = "scriptingBackend";
            Reason = "MetadataScriptingBackendMissing" },
        @{ Value = $metadata; Name = "managedStrippingLevel";
            Reason = "MetadataStrippingMissing" },
        @{ Value = $metadata; Name = "il2cppCompilerConfiguration";
            Reason = "MetadataIl2CppCompilerMissing" },
        @{ Value = $metadata; Name = "backendComparisonId";
            Reason = "MetadataComparisonIdMissing" },
        @{ Value = $metadata; Name = "comparisonRole";
            Reason = "MetadataComparisonRoleMissing" },
        @{ Value = $metadata; Name = "playerLogEnabled";
            Reason = "MetadataPlayerLogMissing" },
        @{ Value = $metadata; Name = "logPolicyId";
            Reason = "MetadataLogPolicyMissing" },
        @{ Value = $metadata; Name = "automaticLogUpload";
            Reason = "MetadataAutomaticLogUploadMissing" },
        @{ Value = $metadata; Name = "payloadAudience";
            Reason = "MetadataPayloadAudienceMissing" },
        @{ Value = $metadata; Name = "distributionTargetId";
            Reason = "MetadataDistributionTargetMissing" },
        @{ Value = $metadata; Name = "providerSelectionMode";
            Reason = "MetadataProviderSelectionModeMissing" },
        @{ Value = $metadata; Name = "expectedProviderId";
            Reason = "MetadataExpectedProviderMissing" },
        @{ Value = $metadata; Name = "expectedLaunchArguments";
            Reason = "MetadataExpectedLaunchArgumentsMissing" },
        @{ Value = $metadata; Name = "requiredArtifacts";
            Reason = "MetadataRequiredArtifactsMissing" },
        @{ Value = $metadata; Name = "forbiddenArtifacts";
            Reason = "MetadataForbiddenArtifactsMissing" },
        @{ Value = $metadata; Name = "expectedStoreLaunch";
            Reason = "MetadataExpectedStoreLaunchMissing" },
        @{ Value = $metadata; Name = "development";
            Reason = "MetadataBuildFlagMissing" },
        @{ Value = $metadata; Name = "connectWithProfiler";
            Reason = "MetadataBuildFlagMissing" },
        @{ Value = $metadata; Name = "deepProfiling";
            Reason = "MetadataBuildFlagMissing" },
        @{ Value = $metadata; Name = "allowDebugging";
            Reason = "MetadataBuildFlagMissing" },
        @{ Value = $metadata; Name = "scriptDebugging";
            Reason = "MetadataBuildFlagMissing" },
        @{ Value = $metadata; Name = "waitForPlayerConnection";
            Reason = "MetadataBuildFlagMissing" },
        @{ Value = $metadata; Name = "waitForDebugger";
            Reason = "MetadataBuildFlagMissing" },
        @{ Value = $metadata; Name = "forceAssertions";
            Reason = "MetadataBuildFlagMissing" },
        @{ Value = $metadata; Name = "effectiveScenes";
            Reason = "MetadataScenesMissing" },
        @{ Value = $metadata; Name = "buildResult"; Reason = "MetadataResultMissing" },
        @{ Value = $metadata; Name = "errorCount"; Reason = "MetadataErrorCountMissing" },
        @{ Value = $metadata; Name = "warningCount"; Reason = "MetadataWarningCountMissing" },
        @{ Value = $metadata; Name = "zeroErrorGatePassed"; Reason = "ZeroErrorGateMissing" },
        @{ Value = $metadata; Name = "metadataReportCountMatched";
            Reason = "MetadataCountGateMissing" },
        @{ Value = $metadata; Name = "structuredErrorCountMatched";
            Reason = "StructuredCountGateMissing" },
        @{ Value = $summary; Name = "result"; Reason = "ReportResultMissing" },
        @{ Value = $summary; Name = "runId"; Reason = "ReportIdentityMissing" },
        @{ Value = $summary; Name = "artifactId"; Reason = "ReportIdentityMissing" },
        @{ Value = $summary; Name = "sourceSha"; Reason = "ReportIdentityMissing" },
        @{ Value = $summary; Name = "sourceTree"; Reason = "ReportIdentityMissing" },
        @{ Value = $summary; Name = "configuration"; Reason = "ReportIdentityMissing" },
        @{ Value = $summary; Name = "buildIntent"; Reason = "ReportIntentMissing" },
        @{ Value = $summary; Name = "storeConfigurationSchema";
            Reason = "ReportStoreSchemaMissing" },
        @{ Value = $summary; Name = "storeConfigurationId";
            Reason = "ReportStoreConfigurationIdMissing" },
        @{ Value = $summary; Name = "backend"; Reason = "ReportBackendMissing" },
        @{ Value = $summary; Name = "scriptingBackend";
            Reason = "ReportScriptingBackendMissing" },
        @{ Value = $summary; Name = "managedStrippingLevel";
            Reason = "ReportStrippingMissing" },
        @{ Value = $summary; Name = "playerLogEnabled";
            Reason = "ReportPlayerLogMissing" },
        @{ Value = $summary; Name = "logPolicyId";
            Reason = "ReportLogPolicyMissing" },
        @{ Value = $summary; Name = "automaticLogUpload";
            Reason = "ReportAutomaticLogUploadMissing" },
        @{ Value = $summary; Name = "payloadAudience";
            Reason = "ReportPayloadAudienceMissing" },
        @{ Value = $summary; Name = "distributionTargetId";
            Reason = "ReportDistributionTargetMissing" },
        @{ Value = $summary; Name = "providerSelectionMode";
            Reason = "ReportProviderSelectionModeMissing" },
        @{ Value = $summary; Name = "expectedProviderId";
            Reason = "ReportExpectedProviderMissing" },
        @{ Value = $summary; Name = "expectedLaunchArguments";
            Reason = "ReportExpectedLaunchArgumentsMissing" },
        @{ Value = $summary; Name = "requiredArtifacts";
            Reason = "ReportRequiredArtifactsMissing" },
        @{ Value = $summary; Name = "forbiddenArtifacts";
            Reason = "ReportForbiddenArtifactsMissing" },
        @{ Value = $summary; Name = "expectedStoreLaunch";
            Reason = "ReportExpectedStoreLaunchMissing" },
        @{ Value = $summary; Name = "backendComparisonId";
            Reason = "ReportComparisonIdMissing" },
        @{ Value = $summary; Name = "comparisonRole";
            Reason = "ReportComparisonRoleMissing" },
        @{ Value = $summary; Name = "totalErrors"; Reason = "ReportErrorCountMissing" },
        @{ Value = $summary; Name = "totalWarnings"; Reason = "ReportWarningCountMissing" },
        @{ Value = $summary; Name = "errorRecordCount"; Reason = "SummaryRecordCountMissing" },
        @{ Value = $summary; Name = "warningRecordCount";
            Reason = "SummaryWarningRecordCountMissing" },
        @{ Value = $summary; Name = "detailsFile"; Reason = "DetailsReferenceMissing" },
        @{ Value = $summary; Name = "detailsSha256"; Reason = "DetailsHashMissing" },
        @{ Value = $details; Name = "runId"; Reason = "DetailsIdentityMissing" },
        @{ Value = $details; Name = "artifactId"; Reason = "DetailsIdentityMissing" },
        @{ Value = $details; Name = "sourceSha"; Reason = "DetailsIdentityMissing" },
        @{ Value = $details; Name = "sourceTree"; Reason = "DetailsIdentityMissing" },
        @{ Value = $details; Name = "configuration"; Reason = "DetailsIdentityMissing" },
        @{ Value = $details; Name = "buildIntent"; Reason = "DetailsIntentMissing" },
        @{ Value = $details; Name = "storeConfigurationSchema";
            Reason = "DetailsStoreSchemaMissing" },
        @{ Value = $details; Name = "storeConfigurationId";
            Reason = "DetailsStoreConfigurationIdMissing" },
        @{ Value = $details; Name = "backend"; Reason = "DetailsBackendMissing" },
        @{ Value = $details; Name = "scriptingBackend";
            Reason = "DetailsScriptingBackendMissing" },
        @{ Value = $details; Name = "managedStrippingLevel";
            Reason = "DetailsStrippingMissing" },
        @{ Value = $details; Name = "playerLogEnabled";
            Reason = "DetailsPlayerLogMissing" },
        @{ Value = $details; Name = "logPolicyId";
            Reason = "DetailsLogPolicyMissing" },
        @{ Value = $details; Name = "automaticLogUpload";
            Reason = "DetailsAutomaticLogUploadMissing" },
        @{ Value = $details; Name = "payloadAudience";
            Reason = "DetailsPayloadAudienceMissing" },
        @{ Value = $details; Name = "distributionTargetId";
            Reason = "DetailsDistributionTargetMissing" },
        @{ Value = $details; Name = "providerSelectionMode";
            Reason = "DetailsProviderSelectionModeMissing" },
        @{ Value = $details; Name = "expectedProviderId";
            Reason = "DetailsExpectedProviderMissing" },
        @{ Value = $details; Name = "expectedLaunchArguments";
            Reason = "DetailsExpectedLaunchArgumentsMissing" },
        @{ Value = $details; Name = "requiredArtifacts";
            Reason = "DetailsRequiredArtifactsMissing" },
        @{ Value = $details; Name = "forbiddenArtifacts";
            Reason = "DetailsForbiddenArtifactsMissing" },
        @{ Value = $details; Name = "expectedStoreLaunch";
            Reason = "DetailsExpectedStoreLaunchMissing" },
        @{ Value = $details; Name = "backendComparisonId";
            Reason = "DetailsComparisonIdMissing" },
        @{ Value = $details; Name = "comparisonRole";
            Reason = "DetailsComparisonRoleMissing" },
        @{ Value = $details; Name = "totalErrors"; Reason = "DetailsTotalErrorsMissing" },
        @{ Value = $details; Name = "totalWarnings"; Reason = "DetailsTotalWarningsMissing" },
        @{ Value = $details; Name = "errorRecordCount"; Reason = "DetailsRecordCountMissing" },
        @{ Value = $details; Name = "warningRecordCount";
            Reason = "DetailsWarningRecordCountMissing" },
        @{ Value = $details; Name = "steps"; Reason = "DetailsStepsMissing" }
    )) {
        if (-not (Test-JsonProperty $required.Value $required.Name)) {
            return New-BuildEvidenceResult $false $required.Reason `
                $metadata $summary $details
        }
    }
    if ([string]$metadata.buildTarget -cne "StandaloneWindows64" -or
        [string]$metadata.architecture -cne "x86_64" -or
        [bool]$metadata.development -or
        [bool]$metadata.connectWithProfiler -or
        [bool]$metadata.deepProfiling -or
        [bool]$metadata.allowDebugging -or
        [bool]$metadata.scriptDebugging -or
        [bool]$metadata.waitForPlayerConnection -or
        [bool]$metadata.waitForDebugger -or
        [bool]$metadata.forceAssertions -or
        @($metadata.effectiveScenes).Count -ne 2 -or
        [string]$metadata.effectiveScenes[0] -cne
            "Assets/Scenes/MainMenuScene.unity" -or
        [string]$metadata.effectiveScenes[1] -cne
            "Assets/Scenes/UIAudioScene.unity") {
        return New-BuildEvidenceResult $false "StoreBuildFlagsOrScenesMismatch" `
            $metadata $summary $details
    }
    if ($null -ne $ExpectedIdentity) {
        foreach ($binding in @(
            @{ Value = $metadata; Name = "runId"; Expected = $ExpectedIdentity.RunId },
            @{ Value = $metadata; Name = "artifactId"; Expected = $ExpectedIdentity.ArtifactId },
            @{ Value = $metadata; Name = "sourceSha"; Expected = $ExpectedIdentity.SourceSha },
            @{ Value = $metadata; Name = "sourceTree"; Expected = $ExpectedIdentity.SourceTree },
            @{ Value = $metadata; Name = "configuration";
                Expected = $ExpectedIdentity.Configuration },
            @{ Value = $metadata; Name = "buildIntent";
                Expected = $ExpectedIdentity.BuildIntent },
            @{ Value = $metadata; Name = "storeConfigurationSchema";
                Expected = $ExpectedIdentity.StoreConfigurationSchema },
            @{ Value = $metadata; Name = "storeConfigurationId";
                Expected = $ExpectedIdentity.StoreConfigurationId },
            @{ Value = $metadata; Name = "backend"; Expected = $ExpectedIdentity.Backend },
            @{ Value = $metadata; Name = "scriptingBackend";
                Expected = $ExpectedIdentity.ScriptingBackend },
            @{ Value = $metadata; Name = "managedStrippingLevel";
                Expected = $ExpectedIdentity.ManagedStrippingLevel },
            @{ Value = $metadata; Name = "il2cppCompilerConfiguration";
                Expected = $ExpectedIdentity.Il2CppCompilerConfiguration },
            @{ Value = $metadata; Name = "backendComparisonId";
                Expected = $ExpectedIdentity.BackendComparisonId },
            @{ Value = $metadata; Name = "comparisonRole";
                Expected = $ExpectedIdentity.ComparisonRole },
            @{ Value = $metadata; Name = "playerLogEnabled";
                Expected = $ExpectedIdentity.PlayerLogEnabled },
            @{ Value = $metadata; Name = "logPolicyId";
                Expected = $ExpectedIdentity.LogPolicyId },
            @{ Value = $metadata; Name = "automaticLogUpload";
                Expected = $ExpectedIdentity.AutomaticLogUpload },
            @{ Value = $metadata; Name = "payloadAudience";
                Expected = $ExpectedIdentity.PayloadAudience },
            @{ Value = $metadata; Name = "distributionTargetId";
                Expected = $ExpectedIdentity.DistributionTargetId },
            @{ Value = $metadata; Name = "providerSelectionMode";
                Expected = $ExpectedIdentity.ProviderSelectionMode },
            @{ Value = $metadata; Name = "expectedProviderId";
                Expected = $ExpectedIdentity.ExpectedProviderId },
            @{ Value = $metadata; Name = "expectedStoreLaunch";
                Expected = $ExpectedIdentity.ExpectedStoreLaunch },
            @{ Value = $summary; Name = "runId"; Expected = $ExpectedIdentity.RunId },
            @{ Value = $summary; Name = "artifactId"; Expected = $ExpectedIdentity.ArtifactId },
            @{ Value = $summary; Name = "sourceSha"; Expected = $ExpectedIdentity.SourceSha },
            @{ Value = $summary; Name = "sourceTree"; Expected = $ExpectedIdentity.SourceTree },
            @{ Value = $summary; Name = "configuration";
                Expected = $ExpectedIdentity.Configuration },
            @{ Value = $summary; Name = "buildIntent";
                Expected = $ExpectedIdentity.BuildIntent },
            @{ Value = $summary; Name = "storeConfigurationSchema";
                Expected = $ExpectedIdentity.StoreConfigurationSchema },
            @{ Value = $summary; Name = "storeConfigurationId";
                Expected = $ExpectedIdentity.StoreConfigurationId },
            @{ Value = $summary; Name = "backend"; Expected = $ExpectedIdentity.Backend },
            @{ Value = $summary; Name = "scriptingBackend";
                Expected = $ExpectedIdentity.ScriptingBackend },
            @{ Value = $summary; Name = "managedStrippingLevel";
                Expected = $ExpectedIdentity.ManagedStrippingLevel },
            @{ Value = $summary; Name = "playerLogEnabled";
                Expected = $ExpectedIdentity.PlayerLogEnabled },
            @{ Value = $summary; Name = "logPolicyId";
                Expected = $ExpectedIdentity.LogPolicyId },
            @{ Value = $summary; Name = "automaticLogUpload";
                Expected = $ExpectedIdentity.AutomaticLogUpload },
            @{ Value = $summary; Name = "payloadAudience";
                Expected = $ExpectedIdentity.PayloadAudience },
            @{ Value = $summary; Name = "distributionTargetId";
                Expected = $ExpectedIdentity.DistributionTargetId },
            @{ Value = $summary; Name = "providerSelectionMode";
                Expected = $ExpectedIdentity.ProviderSelectionMode },
            @{ Value = $summary; Name = "expectedProviderId";
                Expected = $ExpectedIdentity.ExpectedProviderId },
            @{ Value = $summary; Name = "expectedStoreLaunch";
                Expected = $ExpectedIdentity.ExpectedStoreLaunch },
            @{ Value = $summary; Name = "backendComparisonId";
                Expected = $ExpectedIdentity.BackendComparisonId },
            @{ Value = $summary; Name = "comparisonRole";
                Expected = $ExpectedIdentity.ComparisonRole },
            @{ Value = $details; Name = "runId"; Expected = $ExpectedIdentity.RunId },
            @{ Value = $details; Name = "artifactId"; Expected = $ExpectedIdentity.ArtifactId },
            @{ Value = $details; Name = "sourceSha"; Expected = $ExpectedIdentity.SourceSha },
            @{ Value = $details; Name = "sourceTree"; Expected = $ExpectedIdentity.SourceTree },
            @{ Value = $details; Name = "configuration";
                Expected = $ExpectedIdentity.Configuration },
            @{ Value = $details; Name = "buildIntent";
                Expected = $ExpectedIdentity.BuildIntent },
            @{ Value = $details; Name = "storeConfigurationSchema";
                Expected = $ExpectedIdentity.StoreConfigurationSchema },
            @{ Value = $details; Name = "storeConfigurationId";
                Expected = $ExpectedIdentity.StoreConfigurationId },
            @{ Value = $details; Name = "backend"; Expected = $ExpectedIdentity.Backend },
            @{ Value = $details; Name = "scriptingBackend";
                Expected = $ExpectedIdentity.ScriptingBackend },
            @{ Value = $details; Name = "managedStrippingLevel";
                Expected = $ExpectedIdentity.ManagedStrippingLevel },
            @{ Value = $details; Name = "playerLogEnabled";
                Expected = $ExpectedIdentity.PlayerLogEnabled },
            @{ Value = $details; Name = "logPolicyId";
                Expected = $ExpectedIdentity.LogPolicyId },
            @{ Value = $details; Name = "automaticLogUpload";
                Expected = $ExpectedIdentity.AutomaticLogUpload },
            @{ Value = $details; Name = "payloadAudience";
                Expected = $ExpectedIdentity.PayloadAudience },
            @{ Value = $details; Name = "distributionTargetId";
                Expected = $ExpectedIdentity.DistributionTargetId },
            @{ Value = $details; Name = "providerSelectionMode";
                Expected = $ExpectedIdentity.ProviderSelectionMode },
            @{ Value = $details; Name = "expectedProviderId";
                Expected = $ExpectedIdentity.ExpectedProviderId },
            @{ Value = $details; Name = "expectedStoreLaunch";
                Expected = $ExpectedIdentity.ExpectedStoreLaunch },
            @{ Value = $details; Name = "backendComparisonId";
                Expected = $ExpectedIdentity.BackendComparisonId },
            @{ Value = $details; Name = "comparisonRole";
                Expected = $ExpectedIdentity.ComparisonRole }
        )) {
            if (-not (Test-JsonProperty $binding.Value $binding.Name) -or
                [string]$binding.Value.($binding.Name) -cne [string]$binding.Expected) {
                return New-BuildEvidenceResult $false "EvidenceIdentityMismatch" `
                    $metadata $summary $details
            }
        }
        foreach ($carrier in @($metadata, $summary, $details)) {
            if (-not (Test-OrdinalArrayEqual $carrier.expectedLaunchArguments `
                    $ExpectedIdentity.ExpectedLaunchArguments) -or
                -not (Test-OrdinalArrayEqual $carrier.requiredArtifacts `
                    $ExpectedIdentity.RequiredArtifacts) -or
                -not (Test-OrdinalArrayEqual $carrier.forbiddenArtifacts `
                    $ExpectedIdentity.ForbiddenArtifacts)) {
                return New-BuildEvidenceResult $false "EvidenceIdentityMismatch" `
                    $metadata $summary $details
            }
        }
    }
    if ([string]$summary.detailsFile -cne [IO.Path]::GetFileName($DetailsPath)) {
        return New-BuildEvidenceResult $false "DetailsReferenceMismatch" `
            $metadata $summary $details
    }
    if ([string]$summary.detailsSha256 -cne (Get-Sha256 $DetailsPath)) {
        return New-BuildEvidenceResult $false "DetailsHashMismatch" `
            $metadata $summary $details
    }
    if ([string]$metadata.buildResult -cne "Succeeded" -or
        [string]$summary.result -cne "Succeeded" -or
        [string]$details.result -cne "Succeeded") {
        return New-BuildEvidenceResult $false "BuildResultNotSucceeded" `
            $metadata $summary $details
    }
    if (-not $metadata.zeroErrorGatePassed -or
        -not $metadata.metadataReportCountMatched -or
        -not $metadata.structuredErrorCountMatched) {
        return New-BuildEvidenceResult $false "MetadataGateNotPassed" `
            $metadata $summary $details
    }

    $metadataErrors = [int]$metadata.errorCount
    $metadataWarnings = [int]$metadata.warningCount
    $reportErrors = [int]$summary.totalErrors
    $reportWarnings = [int]$summary.totalWarnings
    $detailsTotalErrors = [int]$details.totalErrors
    $detailsTotalWarnings = [int]$details.totalWarnings
    $summaryRecords = [int]$summary.errorRecordCount
    $summaryWarningRecords = [int]$summary.warningRecordCount
    $detailsRecords = [int]$details.errorRecordCount
    $detailsWarningRecords = [int]$details.warningRecordCount
    if ($metadataErrors -ne $reportErrors -or
        $metadataWarnings -ne $reportWarnings) {
        return New-BuildEvidenceResult $false "MetadataReportCountMismatch" `
            $metadata $summary $details
    }
    if ($detailsTotalErrors -ne $reportErrors -or
        $summaryRecords -ne $reportErrors -or
        $detailsRecords -ne $reportErrors -or
        $detailsTotalWarnings -ne $reportWarnings -or
        $summaryWarningRecords -ne $reportWarnings -or
        $detailsWarningRecords -ne $reportWarnings) {
        return New-BuildEvidenceResult $false "StructuredErrorCountMismatch" `
            $metadata $summary $details
    }
    if ($reportErrors -ne 0) {
        return New-BuildEvidenceResult $false "NonzeroBuildErrors" `
            $metadata $summary $details
    }
    return New-BuildEvidenceResult $true "Accepted" $metadata $summary $details
}

function Get-BuildEvidenceGateDecision {
    param([Parameter(Mandatory)]$EvidenceResult)
    return [pscustomobject]@{
        CreateSuccess = [bool]$EvidenceResult.Allowed
        Promote = [bool]$EvidenceResult.Allowed
        Quarantine = -not [bool]$EvidenceResult.Allowed
        Reason = [string]$EvidenceResult.Reason
    }
}

function Test-ConfigurationSummary {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Expectation
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    try { $value = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { return $false }
    foreach ($name in @(
        "schemaVersion", "storeConfigurationSchema", "storeConfigurationId", "buildIntent",
        "configuration", "backend", "scriptingBackend", "managedStrippingLevel",
        "playerLogEnabled", "logPolicyId", "automaticLogUpload", "payloadAudience",
        "distributionTargetId", "providerSelectionMode", "expectedProviderId",
        "expectedLaunchArguments", "requiredArtifacts", "forbiddenArtifacts",
        "expectedStoreLaunch",
        "development", "connectWithProfiler", "deepProfiling", "allowDebugging",
        "waitForPlayerConnection", "forceEnableAssertions", "scenes"
    )) {
        if (-not (Test-JsonProperty $value $name)) { return $false }
    }
    return $value.schemaVersion -ceq $script:ConfigurationSummarySchemaVersion -and
        $value.storeConfigurationSchema -ceq
            $Expectation.StoreConfigurationSchema -and
        $value.storeConfigurationId -ceq $Expectation.StoreConfigurationId -and
        $value.buildIntent -ceq $Expectation.BuildIntent -and
        $value.configuration -ceq $Expectation.Configuration -and
        $value.backend -ceq $Expectation.Backend -and
        $value.scriptingBackend -ceq $Expectation.ScriptingBackend -and
        $value.managedStrippingLevel -ceq $Expectation.ManagedStrippingLevel -and
        [bool]$value.playerLogEnabled -eq $Expectation.PlayerLogEnabled -and
        $value.logPolicyId -ceq $Expectation.LogPolicyId -and
        [bool]$value.automaticLogUpload -eq $Expectation.AutomaticLogUpload -and
        $value.payloadAudience -ceq $Expectation.PayloadAudience -and
        $value.distributionTargetId -ceq $Expectation.DistributionTargetId -and
        $value.providerSelectionMode -ceq $Expectation.ProviderSelectionMode -and
        $value.expectedProviderId -ceq $Expectation.ExpectedProviderId -and
        (Test-OrdinalArrayEqual $value.expectedLaunchArguments `
            $Expectation.ExpectedLaunchArguments) -and
        (Test-OrdinalArrayEqual $value.requiredArtifacts `
            $Expectation.RequiredArtifacts) -and
        (Test-OrdinalArrayEqual $value.forbiddenArtifacts `
            $Expectation.ForbiddenArtifacts) -and
        $value.expectedStoreLaunch -ceq $Expectation.ExpectedStoreLaunch -and
        -not [bool]$value.development -and
        -not [bool]$value.connectWithProfiler -and
        -not [bool]$value.deepProfiling -and
        -not [bool]$value.allowDebugging -and
        -not [bool]$value.waitForPlayerConnection -and
        -not [bool]$value.forceEnableAssertions -and
        @($value.scenes).Count -eq 2 -and
        [string]$value.scenes[0] -ceq "Assets/Scenes/MainMenuScene.unity" -and
        [string]$value.scenes[1] -ceq "Assets/Scenes/UIAudioScene.unity"
}

function Get-PayloadFiles {
    param([Parameter(Mandatory)][string]$PayloadRoot)
    $items = @(Get-ChildItem -LiteralPath $PayloadRoot -File -Recurse |
        Where-Object { $script:ControlFileNames -notcontains $_.Name } |
        ForEach-Object {
            [pscustomobject]@{
                FullName = $_.FullName
                RelativePath = Get-NormalizedRelativePath -Root $PayloadRoot -Path $_.FullName
            }
        })
    $paths = [string[]]@($items | ForEach-Object { $_.RelativePath })
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $byPath = @{}
    foreach ($item in $items) { $byPath[$item.RelativePath] = $item }
    return @($paths | ForEach-Object { $byPath[$_] })
}

function Get-StoreExcludedPayloadDirectories {
    param([Parameter(Mandatory)][string]$PayloadRoot)
    return @(Get-ChildItem -LiteralPath $PayloadRoot -Directory -Recurse |
        Where-Object {
            $_.Name -like "*_BurstDebugInformation_DoNotShip" -or
            $_.Name -like "*_BackUpThisFolder_ButDontShipItWithYourGame"
        } |
        Sort-Object FullName)
}

function Test-PrivateSupportEvidencePath {
    param([Parameter(Mandatory)][string]$Path)
    $normalized = $Path.Replace('/', '\')
    return $normalized -match '(?i)(?:^|\\)\.private(?:\\|$)' -or
        $normalized -match '(?i)(?:^|\\)VectorQuake-QA-Telemetry(?:\\|$)'
}

function Test-StorePayloadPrivacy {
    param([Parameter(Mandatory)][string]$PayloadRoot)
    if (@(Get-StoreExcludedPayloadDirectories $PayloadRoot).Count -ne 0) {
        return $false
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $PayloadRoot -File -Recurse)) {
        if ($file.Name -ieq "Player.log" -or $file.Name -ieq "Player-prev.log") {
            return $false
        }
    }
    $textExtensions = @(
        ".cfg", ".config", ".ini", ".json", ".log", ".manifest",
        ".sha256", ".txt", ".xml", ".yaml", ".yml"
    )
    $absolutePrivatePathPattern =
        '(?im)(?:(?<![a-z0-9+.-])[a-z]:[\\/][^\s"''<>|]+|' +
        '\\\\[^\\\s]+\\[^\\\s]+|/(?:home|users)/[^\s"''<>|]+|' +
        '/mnt/[a-z]/users/[^\s"''<>|]+)'
    foreach ($file in @(Get-ChildItem -LiteralPath $PayloadRoot -File -Recurse)) {
        if ($textExtensions -notcontains $file.Extension.ToLowerInvariant()) { continue }
        try {
            $content = Get-Content -LiteralPath $file.FullName -Raw
        } catch {
            return $false
        }
        if ($content -match $absolutePrivatePathPattern) { return $false }
    }
    return $true
}

function Prepare-PayloadForAudience {
    param(
        [Parameter(Mandatory)][string]$PayloadRoot,
        [ValidateSet("InternalRc", "StoreDistributable")]
        [string]$Audience = "InternalRc"
    )
    if ($Audience -eq "InternalRc") {
        return [pscustomobject]@{
            Audience = $Audience
            ExcludedRelativePaths = @()
            PrivacyGatePassed = Test-StorePayloadPrivacy $PayloadRoot
        }
    }
    $excluded = @(
        Get-StoreExcludedPayloadDirectories $PayloadRoot |
            ForEach-Object {
                Get-NormalizedRelativePath -Root $PayloadRoot -Path $_.FullName
            }
    )
    foreach ($relativePath in $excluded) {
        $candidate = Join-Path $PayloadRoot $relativePath.Replace('/', '\')
        Remove-Item -LiteralPath $candidate -Recurse -Force
    }
    if (-not (Test-StorePayloadPrivacy $PayloadRoot)) {
        throw "Store payload privacy gate rejected DoNotShip content or an absolute private path."
    }
    return [pscustomobject]@{
        Audience = $Audience
        ExcludedRelativePaths = @($excluded)
        PrivacyGatePassed = $true
    }
}

function New-PayloadManifest {
    param([Parameter(Mandatory)][string]$PayloadRoot)
    $manifestPath = Join-Path $PayloadRoot "files.sha256"
    $lines = @(Get-PayloadFiles -PayloadRoot $PayloadRoot | ForEach-Object {
        "$(Get-Sha256 -Path $_.FullName)  $($_.RelativePath)"
    })
    [IO.File]::WriteAllLines(
        $manifestPath, $lines, [Text.UTF8Encoding]::new($false))
    $selfHash = Get-Sha256 -Path $manifestPath
    [IO.File]::WriteAllText(
        (Join-Path $PayloadRoot "files.sha256.sha256"),
        "$selfHash  files.sha256`n",
        [Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{
        Path = $manifestPath
        Sha256 = $selfHash
        FileCount = $lines.Count
    }
}

function Read-PayloadManifest {
    param([Parameter(Mandatory)][string]$ManifestPath)
    $entries = @()
    foreach ($line in @(Get-Content -LiteralPath $ManifestPath)) {
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') {
            throw "Malformed manifest line: $line"
        }
        $entries += [pscustomobject]@{
            Hash = $Matches[1].ToLowerInvariant()
            RelativePath = $Matches[2]
        }
    }
    return $entries
}

function Test-PayloadManifest {
    param([Parameter(Mandatory)][string]$PayloadRoot)
    $manifestPath = Join-Path $PayloadRoot "files.sha256"
    $selfPath = Join-Path $PayloadRoot "files.sha256.sha256"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $selfPath -PathType Leaf)) { return $false }
    try { $entries = @(Read-PayloadManifest -ManifestPath $manifestPath) }
    catch { return $false }
    $actual = @(Get-PayloadFiles -PayloadRoot $PayloadRoot)
    $expectedPaths = @($entries | ForEach-Object { $_.RelativePath })
    $actualPaths = @($actual | ForEach-Object { $_.RelativePath })
    if ((Compare-Object $expectedPaths $actualPaths).Count -ne 0) { return $false }
    $ordinalSorted = [string[]]@($expectedPaths)
    [Array]::Sort($ordinalSorted, [StringComparer]::Ordinal)
    if (($expectedPaths -join "`n") -cne ($ordinalSorted -join "`n")) { return $false }
    foreach ($entry in $entries) {
        $candidate = Join-Path $PayloadRoot $entry.RelativePath.Replace('/', '\')
        if (-not (Test-ExtendedLengthFileExists -Path $candidate)) { return $false }
        if ((Get-Sha256 -Path $candidate) -cne $entry.Hash) { return $false }
    }
    $selfLine = (Get-Content -LiteralPath $selfPath -Raw).Trim()
    return $selfLine -ceq "$(Get-Sha256 -Path $manifestPath)  files.sha256"
}

function New-ArtifactProvenance {
    param(
        [Parameter(Mandatory)][string]$ArtifactRoot,
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][string]$ArtifactId,
        [Parameter(Mandatory)][string]$SourceSha,
        [Parameter(Mandatory)][string]$SourceTree,
        [Parameter(Mandatory)][string]$BuildMetadataPath,
        [Parameter(Mandatory)][string]$ConfigurationSummaryPath,
        [Parameter(Mandatory)][string]$BuildReportSummaryPath,
        [Parameter(Mandatory)][string]$BuildReportDetailsPath,
        [Parameter(Mandatory)]$Manifest,
        [Parameter(Mandatory)]$BuildEvidence,
        [Parameter(Mandatory)]$PayloadPolicy,
        [Parameter(Mandatory)][string]$EntrySourceSha256,
        [Parameter(Mandatory)][string]$PolicySourceSha256,
        [Parameter(Mandatory)][string]$WrapperSourceSha256
    )
    $provenance = [ordered]@{
        schemaVersion = $script:ProvenanceSchemaVersion
        runId = $RunId
        artifactId = $ArtifactId
        sourceSha = $SourceSha
        sourceTree = $SourceTree
        configuration = $script:ConfigurationName
        buildIntent = [string]$BuildEvidence.Metadata.buildIntent
        storeConfigurationSchema =
            [string]$BuildEvidence.Metadata.storeConfigurationSchema
        storeConfigurationId = [string]$BuildEvidence.Metadata.storeConfigurationId
        backend = [string]$BuildEvidence.Metadata.backend
        scriptingBackend = [string]$BuildEvidence.Metadata.scriptingBackend
        managedStrippingLevel =
            [string]$BuildEvidence.Metadata.managedStrippingLevel
        il2cppCompilerConfiguration =
            [string]$BuildEvidence.Metadata.il2cppCompilerConfiguration
        nativeCompilerIdentity =
            [string]$BuildEvidence.Metadata.nativeCompilerIdentity
        windowsSdkIdentity = [string]$BuildEvidence.Metadata.windowsSdkIdentity
        backendComparisonId = [string]$BuildEvidence.Metadata.backendComparisonId
        comparisonRole = [string]$BuildEvidence.Metadata.comparisonRole
        playerLogEnabled = [bool]$BuildEvidence.Metadata.playerLogEnabled
        logPolicyId = [string]$BuildEvidence.Metadata.logPolicyId
        automaticLogUpload = [bool]$BuildEvidence.Metadata.automaticLogUpload
        buildResult = [string]$BuildEvidence.Summary.result
        totalErrors = [int]$BuildEvidence.Summary.totalErrors
        totalWarnings = [int]$BuildEvidence.Summary.totalWarnings
        errorRecordCount = [int]$BuildEvidence.Details.errorRecordCount
        warningRecordCount = [int]$BuildEvidence.Details.warningRecordCount
        buildMetadataFile = Get-NormalizedRelativePath $ArtifactRoot $BuildMetadataPath
        buildMetadataSha256 = Get-Sha256 $BuildMetadataPath
        configurationSummaryFile =
            Get-NormalizedRelativePath $ArtifactRoot $ConfigurationSummaryPath
        configurationSummarySha256 = Get-Sha256 $ConfigurationSummaryPath
        buildReportSummaryFile =
            Get-NormalizedRelativePath $ArtifactRoot $BuildReportSummaryPath
        buildReportSummarySha256 = Get-Sha256 $BuildReportSummaryPath
        buildReportDetailsFile = [IO.Path]::GetFileName($BuildReportDetailsPath)
        buildReportDetailsSha256 = Get-Sha256 $BuildReportDetailsPath
        payloadManifestFile = "files.sha256"
        payloadManifestSha256 = [string]$Manifest.Sha256
        payloadFileCount = [int]$Manifest.FileCount
        payloadAudience = [string]$PayloadPolicy.Audience
        distributionTargetId = [string]$BuildEvidence.Metadata.distributionTargetId
        providerSelectionMode = [string]$BuildEvidence.Metadata.providerSelectionMode
        expectedProviderId = [string]$BuildEvidence.Metadata.expectedProviderId
        expectedLaunchArguments = @($BuildEvidence.Metadata.expectedLaunchArguments)
        requiredArtifacts = @($BuildEvidence.Metadata.requiredArtifacts)
        forbiddenArtifacts = @($BuildEvidence.Metadata.forbiddenArtifacts)
        expectedStoreLaunch = [string]$BuildEvidence.Metadata.expectedStoreLaunch
        excludedPayloadPaths = @($PayloadPolicy.ExcludedRelativePaths)
        payloadPrivacyGatePassed = [bool]$PayloadPolicy.PrivacyGatePassed
        zeroErrorGatePassed = $true
        metadataReportCountMatched = $true
        structuredErrorCountMatched = $true
        entrySourceSha256 = $EntrySourceSha256
        policySourceSha256 = $PolicySourceSha256
        wrapperSourceSha256 = $WrapperSourceSha256
        createdUtc = [DateTime]::UtcNow.ToString("o")
    }
    $path = Join-Path $ArtifactRoot "artifact-provenance.json"
    $provenance | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $path -Encoding UTF8
    return [pscustomobject]$provenance
}

function Test-ArtifactProvenance {
    param(
        [Parameter(Mandatory)][string]$ArtifactRoot,
        [Parameter(Mandatory)][string]$BuildReportDetailsPath,
        [Parameter(Mandatory)]$Expectation
    )
    $path = Join-Path $ArtifactRoot "artifact-provenance.json"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
    try { $value = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
    catch { return $false }
    foreach ($name in @(
        "runId", "artifactId", "sourceSha", "sourceTree", "configuration",
        "buildIntent", "storeConfigurationSchema", "storeConfigurationId",
        "backend", "scriptingBackend", "managedStrippingLevel",
        "il2cppCompilerConfiguration", "playerLogEnabled", "logPolicyId",
        "automaticLogUpload",
        "nativeCompilerIdentity", "windowsSdkIdentity", "backendComparisonId",
        "comparisonRole",
        "buildResult", "totalErrors", "totalWarnings", "errorRecordCount",
        "warningRecordCount", "buildMetadataFile", "buildMetadataSha256",
        "configurationSummaryFile", "configurationSummarySha256",
        "buildReportSummaryFile", "buildReportSummarySha256",
        "buildReportDetailsFile", "buildReportDetailsSha256",
        "payloadManifestFile", "payloadManifestSha256", "payloadFileCount",
        "payloadAudience", "distributionTargetId", "providerSelectionMode",
        "expectedProviderId", "expectedLaunchArguments", "requiredArtifacts",
        "forbiddenArtifacts", "expectedStoreLaunch", "payloadPrivacyGatePassed",
        "entrySourceSha256",
        "policySourceSha256", "wrapperSourceSha256"
    )) {
        if (-not (Test-JsonProperty $value $name)) { return $false }
    }
    if ($value.schemaVersion -cne $script:ProvenanceSchemaVersion -or
        $value.runId -cne $Expectation.RunId -or
        $value.artifactId -cne $Expectation.ArtifactId -or
        $value.sourceSha -cne $Expectation.SourceSha -or
        $value.sourceTree -cne $Expectation.SourceTree -or
        $value.configuration -cne $Expectation.Configuration -or
        $value.buildIntent -cne $Expectation.BuildIntent -or
        $value.storeConfigurationSchema -cne
            $Expectation.StoreConfigurationSchema -or
        $value.storeConfigurationId -cne $Expectation.StoreConfigurationId -or
        $value.backend -cne $Expectation.Backend -or
        $value.scriptingBackend -cne $Expectation.ScriptingBackend -or
        $value.managedStrippingLevel -cne $Expectation.ManagedStrippingLevel -or
        $value.il2cppCompilerConfiguration -cne
            $Expectation.Il2CppCompilerConfiguration -or
        $value.backendComparisonId -cne $Expectation.BackendComparisonId -or
        $value.comparisonRole -cne $Expectation.ComparisonRole -or
        [bool]$value.playerLogEnabled -ne $Expectation.PlayerLogEnabled -or
        $value.logPolicyId -cne $Expectation.LogPolicyId -or
        [bool]$value.automaticLogUpload -ne $Expectation.AutomaticLogUpload -or
        $value.payloadAudience -cne $Expectation.PayloadAudience -or
        $value.distributionTargetId -cne $Expectation.DistributionTargetId -or
        $value.providerSelectionMode -cne $Expectation.ProviderSelectionMode -or
        $value.expectedProviderId -cne $Expectation.ExpectedProviderId -or
        -not (Test-OrdinalArrayEqual $value.expectedLaunchArguments `
            $Expectation.ExpectedLaunchArguments) -or
        -not (Test-OrdinalArrayEqual $value.requiredArtifacts `
            $Expectation.RequiredArtifacts) -or
        -not (Test-OrdinalArrayEqual $value.forbiddenArtifacts `
            $Expectation.ForbiddenArtifacts) -or
        $value.expectedStoreLaunch -cne $Expectation.ExpectedStoreLaunch -or
        [string]$value.buildResult -cne "Succeeded" -or
        -not $value.zeroErrorGatePassed -or
        -not $value.metadataReportCountMatched -or
        -not $value.structuredErrorCountMatched -or
        ([string]$value.payloadAudience -ceq "StoreDistributable" -and
            -not $value.payloadPrivacyGatePassed)) { return $false }
    foreach ($binding in @(
        @{ File = [string]$value.buildMetadataFile
            Hash = [string]$value.buildMetadataSha256 },
        @{ File = [string]$value.configurationSummaryFile
            Hash = [string]$value.configurationSummarySha256 },
        @{ File = [string]$value.buildReportSummaryFile
            Hash = [string]$value.buildReportSummarySha256 },
        @{ File = [string]$value.payloadManifestFile
            Hash = [string]$value.payloadManifestSha256 }
    )) {
        $candidate = Join-Path $ArtifactRoot $binding.File.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf) -or
            (Get-Sha256 $candidate) -cne $binding.Hash) { return $false }
    }
    if (-not (Test-Path -LiteralPath $BuildReportDetailsPath -PathType Leaf) -or
        [IO.Path]::GetFileName($BuildReportDetailsPath) -cne
            [string]$value.buildReportDetailsFile -or
        (Get-Sha256 $BuildReportDetailsPath) -cne
            [string]$value.buildReportDetailsSha256) { return $false }

    try {
        $metadataPath = Join-Path $ArtifactRoot `
            ([string]$value.buildMetadataFile).Replace('/', '\')
        $summaryPath = Join-Path $ArtifactRoot `
            ([string]$value.buildReportSummaryFile).Replace('/', '\')
        $evidence = Test-BuildEvidence -MetadataPath $metadataPath `
            -SummaryPath $summaryPath -DetailsPath $BuildReportDetailsPath `
            -ExpectedIdentity $Expectation
    } catch {
        return $false
    }
    if (-not $evidence.Allowed) { return $false }
    $configurationSummaryPath = Join-Path $ArtifactRoot `
        ([string]$value.configurationSummaryFile).Replace('/', '\')
    if (-not (Test-ConfigurationSummary -Path $configurationSummaryPath `
            -Expectation $Expectation)) { return $false }
    if ([int]$value.totalErrors -ne [int]$evidence.Metadata.errorCount -or
        [int]$value.totalWarnings -ne [int]$evidence.Metadata.warningCount -or
        [int]$value.errorRecordCount -ne [int]$evidence.Details.errorRecordCount -or
        [int]$value.warningRecordCount -ne
            [int]$evidence.Details.warningRecordCount) { return $false }

    foreach ($sourceBinding in @(
        @{ Path = $Expectation.EntrySourcePath
            Expected = $Expectation.EntrySourceSha256
            Metadata = [string]$evidence.Metadata.entrySourceSha256
            Provenance = [string]$value.entrySourceSha256 },
        @{ Path = $Expectation.PolicySourcePath
            Expected = $Expectation.PolicySourceSha256
            Metadata = [string]$evidence.Metadata.policySourceSha256
            Provenance = [string]$value.policySourceSha256 },
        @{ Path = $Expectation.DetachedWrapperSourcePath
            Expected = $Expectation.WrapperSourceSha256
            Metadata = [string]$evidence.Metadata.wrapperSourceSha256
            Provenance = [string]$value.wrapperSourceSha256 },
        @{ Path = $Expectation.ExecutingWrapperSourcePath
            Expected = $Expectation.WrapperSourceSha256
            Metadata = [string]$evidence.Metadata.wrapperSourceSha256
            Provenance = [string]$value.wrapperSourceSha256 }
    )) {
        if (-not (Test-Path -LiteralPath $sourceBinding.Path -PathType Leaf) -or
            (Get-Sha256 $sourceBinding.Path) -cne $sourceBinding.Expected -or
            $sourceBinding.Metadata -cne $sourceBinding.Expected -or
            $sourceBinding.Provenance -cne $sourceBinding.Expected) { return $false }
    }
    try {
        $manifestEntries = @(Read-PayloadManifest `
            (Join-Path $ArtifactRoot ([string]$value.payloadManifestFile)))
    } catch { return $false }
    $privacyAllowed = [string]$value.payloadAudience -cne "StoreDistributable" -or
        (Test-StorePayloadPrivacy $ArtifactRoot)
    return $privacyAllowed -and
        [int]$value.payloadFileCount -eq $manifestEntries.Count -and
        (Test-PayloadManifest $ArtifactRoot)
}

function New-SuccessControl {
    param(
        [Parameter(Mandatory)][string]$PayloadRoot,
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][string]$ArtifactId,
        [Parameter(Mandatory)][string]$SourceSha,
        [Parameter(Mandatory)][string]$SourceTree,
        [Parameter(Mandatory)]$Manifest,
        [Parameter(Mandatory)]$Provenance,
        [Parameter(Mandatory)]$BuildEvidence,
        [Parameter(Mandatory)]$PayloadPolicy
    )
    $control = [ordered]@{
        schemaVersion = $script:MetadataSchemaVersion
        runId = $RunId
        artifactId = $ArtifactId
        sourceSha = $SourceSha
        sourceTree = $SourceTree
        configuration = $script:ConfigurationName
        buildIntent = [string]$BuildEvidence.Metadata.buildIntent
        storeConfigurationSchema =
            [string]$BuildEvidence.Metadata.storeConfigurationSchema
        storeConfigurationId = [string]$BuildEvidence.Metadata.storeConfigurationId
        backend = [string]$BuildEvidence.Metadata.backend
        scriptingBackend = [string]$BuildEvidence.Metadata.scriptingBackend
        managedStrippingLevel =
            [string]$BuildEvidence.Metadata.managedStrippingLevel
        il2cppCompilerConfiguration =
            [string]$BuildEvidence.Metadata.il2cppCompilerConfiguration
        backendComparisonId = [string]$BuildEvidence.Metadata.backendComparisonId
        comparisonRole = [string]$BuildEvidence.Metadata.comparisonRole
        playerLogEnabled = [bool]$BuildEvidence.Metadata.playerLogEnabled
        logPolicyId = [string]$BuildEvidence.Metadata.logPolicyId
        automaticLogUpload = [bool]$BuildEvidence.Metadata.automaticLogUpload
        buildResult = [string]$BuildEvidence.Summary.result
        totalErrors = [int]$BuildEvidence.Summary.totalErrors
        totalWarnings = [int]$BuildEvidence.Summary.totalWarnings
        errorRecordCount = [int]$BuildEvidence.Details.errorRecordCount
        warningRecordCount = [int]$BuildEvidence.Details.warningRecordCount
        payloadManifestFile = "files.sha256"
        payloadManifestSha256 = $Manifest.Sha256
        payloadFileCount = [int]$Manifest.FileCount
        payloadAudience = [string]$PayloadPolicy.Audience
        distributionTargetId = [string]$BuildEvidence.Metadata.distributionTargetId
        providerSelectionMode = [string]$BuildEvidence.Metadata.providerSelectionMode
        expectedProviderId = [string]$BuildEvidence.Metadata.expectedProviderId
        expectedLaunchArguments = @($BuildEvidence.Metadata.expectedLaunchArguments)
        requiredArtifacts = @($BuildEvidence.Metadata.requiredArtifacts)
        forbiddenArtifacts = @($BuildEvidence.Metadata.forbiddenArtifacts)
        expectedStoreLaunch = [string]$BuildEvidence.Metadata.expectedStoreLaunch
        payloadPrivacyGatePassed = [bool]$PayloadPolicy.PrivacyGatePassed
        artifactProvenanceFile = "artifact-provenance.json"
        artifactProvenanceSha256 =
            Get-Sha256 (Join-Path $PayloadRoot "artifact-provenance.json")
        promotionReady = $true
        createdUtc = [DateTime]::UtcNow.ToString("o")
    }
    $control | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $PayloadRoot "SUCCESS.json") -Encoding UTF8
    return $control
}

function Test-SuccessControl {
    param(
        [Parameter(Mandatory)][string]$PayloadRoot,
        [Parameter(Mandatory)][string]$BuildReportDetailsPath,
        [Parameter(Mandatory)]$Expectation
    )
    $successPath = Join-Path $PayloadRoot "SUCCESS.json"
    if (-not (Test-Path -LiteralPath $successPath -PathType Leaf)) { return $false }
    try { $success = Get-Content -LiteralPath $successPath -Raw | ConvertFrom-Json }
    catch { return $false }
    foreach ($name in @(
        "runId", "artifactId", "sourceSha", "sourceTree", "configuration",
        "buildIntent", "storeConfigurationSchema", "storeConfigurationId",
        "backend", "scriptingBackend", "managedStrippingLevel",
        "il2cppCompilerConfiguration", "playerLogEnabled", "logPolicyId",
        "automaticLogUpload",
        "backendComparisonId", "comparisonRole",
        "buildResult", "totalErrors", "totalWarnings", "errorRecordCount",
        "warningRecordCount", "payloadManifestFile", "payloadManifestSha256",
        "payloadFileCount", "payloadAudience", "payloadPrivacyGatePassed",
        "distributionTargetId", "providerSelectionMode", "expectedProviderId",
        "expectedLaunchArguments", "requiredArtifacts", "forbiddenArtifacts",
        "expectedStoreLaunch",
        "artifactProvenanceFile", "artifactProvenanceSha256", "promotionReady"
    )) {
        if (-not (Test-JsonProperty $success $name)) { return $false }
    }
    if (-not (Test-JsonProperty $success "payloadManifestFile") -or
        (Test-JsonProperty $success "payloadManifest")) { return $false }
    $manifestPath = Join-Path $PayloadRoot ([string]$success.payloadManifestFile)
    $provenancePath = Join-Path $PayloadRoot ([string]$success.artifactProvenanceFile)
    if ($success.schemaVersion -cne $script:MetadataSchemaVersion -or
        -not $success.promotionReady -or
        $success.runId -cne $Expectation.RunId -or
        $success.artifactId -cne $Expectation.ArtifactId -or
        $success.sourceSha -cne $Expectation.SourceSha -or
        $success.sourceTree -cne $Expectation.SourceTree -or
        $success.configuration -cne $Expectation.Configuration -or
        $success.buildIntent -cne $Expectation.BuildIntent -or
        $success.storeConfigurationSchema -cne
            $Expectation.StoreConfigurationSchema -or
        $success.storeConfigurationId -cne $Expectation.StoreConfigurationId -or
        $success.backend -cne $Expectation.Backend -or
        $success.scriptingBackend -cne $Expectation.ScriptingBackend -or
        $success.managedStrippingLevel -cne $Expectation.ManagedStrippingLevel -or
        $success.il2cppCompilerConfiguration -cne
            $Expectation.Il2CppCompilerConfiguration -or
        $success.backendComparisonId -cne $Expectation.BackendComparisonId -or
        $success.comparisonRole -cne $Expectation.ComparisonRole -or
        [bool]$success.playerLogEnabled -ne $Expectation.PlayerLogEnabled -or
        $success.logPolicyId -cne $Expectation.LogPolicyId -or
        [bool]$success.automaticLogUpload -ne $Expectation.AutomaticLogUpload -or
        $success.payloadAudience -cne $Expectation.PayloadAudience -or
        $success.distributionTargetId -cne $Expectation.DistributionTargetId -or
        $success.providerSelectionMode -cne $Expectation.ProviderSelectionMode -or
        $success.expectedProviderId -cne $Expectation.ExpectedProviderId -or
        -not (Test-OrdinalArrayEqual $success.expectedLaunchArguments `
            $Expectation.ExpectedLaunchArguments) -or
        -not (Test-OrdinalArrayEqual $success.requiredArtifacts `
            $Expectation.RequiredArtifacts) -or
        -not (Test-OrdinalArrayEqual $success.forbiddenArtifacts `
            $Expectation.ForbiddenArtifacts) -or
        $success.expectedStoreLaunch -cne $Expectation.ExpectedStoreLaunch -or
        [string]$success.buildResult -cne "Succeeded" -or
        ([string]$success.payloadAudience -ceq "StoreDistributable" -and
            -not $success.payloadPrivacyGatePassed) -or
        -not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) { return $false }
    try { $entries = @(Read-PayloadManifest -ManifestPath $manifestPath) }
    catch { return $false }
    try { $provenance = Get-Content $provenancePath -Raw | ConvertFrom-Json }
    catch { return $false }
    $crossBindingsMatch =
        $success.runId -ceq $provenance.runId -and
        $success.artifactId -ceq $provenance.artifactId -and
        $success.sourceSha -ceq $provenance.sourceSha -and
        $success.sourceTree -ceq $provenance.sourceTree -and
        $success.configuration -ceq $provenance.configuration -and
        $success.buildIntent -ceq $provenance.buildIntent -and
        $success.storeConfigurationSchema -ceq
            $provenance.storeConfigurationSchema -and
        $success.storeConfigurationId -ceq $provenance.storeConfigurationId -and
        $success.backend -ceq $provenance.backend -and
        $success.scriptingBackend -ceq $provenance.scriptingBackend -and
        $success.managedStrippingLevel -ceq $provenance.managedStrippingLevel -and
        $success.il2cppCompilerConfiguration -ceq
            $provenance.il2cppCompilerConfiguration -and
        $success.backendComparisonId -ceq $provenance.backendComparisonId -and
        $success.comparisonRole -ceq $provenance.comparisonRole -and
        [bool]$success.playerLogEnabled -eq [bool]$provenance.playerLogEnabled -and
        $success.logPolicyId -ceq $provenance.logPolicyId -and
        [bool]$success.automaticLogUpload -eq
            [bool]$provenance.automaticLogUpload -and
        $success.buildResult -ceq $provenance.buildResult -and
        [int]$success.totalErrors -eq [int]$provenance.totalErrors -and
        [int]$success.totalWarnings -eq [int]$provenance.totalWarnings -and
        [int]$success.errorRecordCount -eq [int]$provenance.errorRecordCount -and
        [int]$success.warningRecordCount -eq
            [int]$provenance.warningRecordCount -and
        $success.payloadAudience -ceq $provenance.payloadAudience -and
        $success.distributionTargetId -ceq $provenance.distributionTargetId -and
        $success.providerSelectionMode -ceq $provenance.providerSelectionMode -and
        $success.expectedProviderId -ceq $provenance.expectedProviderId -and
        (Test-OrdinalArrayEqual $success.expectedLaunchArguments `
            $provenance.expectedLaunchArguments) -and
        (Test-OrdinalArrayEqual $success.requiredArtifacts `
            $provenance.requiredArtifacts) -and
        (Test-OrdinalArrayEqual $success.forbiddenArtifacts `
            $provenance.forbiddenArtifacts) -and
        $success.expectedStoreLaunch -ceq $provenance.expectedStoreLaunch
    return $crossBindingsMatch -and
        $success.payloadManifestSha256 -ceq (Get-Sha256 -Path $manifestPath) -and
        [int]$success.payloadFileCount -eq $entries.Count -and
        $success.artifactProvenanceSha256 -ceq (Get-Sha256 $provenancePath) -and
        (Test-PayloadManifest -PayloadRoot $PayloadRoot) -and
        (Test-ArtifactProvenance -ArtifactRoot $PayloadRoot `
            -BuildReportDetailsPath $BuildReportDetailsPath -Expectation $Expectation)
}

function Test-SnapshotEquality {
    param([Parameter(Mandatory)]$Before, [Parameter(Mandatory)]$After)
    return (ConvertTo-Json $Before -Depth 8 -Compress) -ceq
        (ConvertTo-Json $After -Depth 8 -Compress)
}

function Assert-OutputPlan {
    param([string]$StagingPath, [string]$FinalPath, [string]$DetachedPath)
    if ((Test-Path -LiteralPath $StagingPath) -or
        (Test-Path -LiteralPath $FinalPath) -or
        (Test-Path -LiteralPath $DetachedPath)) {
        return $false
    }
    return [IO.Path]::GetPathRoot($StagingPath) -eq [IO.Path]::GetPathRoot($FinalPath)
}

function Convert-UnityExitCode {
    param([int]$UnityExitCode)
    if ($UnityExitCode -eq 0) { return 0 }
    return $script:ReleaseExitCodes.UnityInvocationFailure
}

function Get-GitCommandArguments {
    param([string]$Root, [string[]]$Arguments, [switch]$DisableAutoCrlf)
    $configuration = @("-c", "core.longpaths=true")
    if ($DisableAutoCrlf) {
        $configuration += @(
            "-c", "core.autocrlf=false",
            "-c", "core.eol=lf"
        )
    }
    return $configuration + @("-C", $Root) + @($Arguments)
}

function Test-WslGitWorktreeMarker {
    param([Parameter(Mandatory)][string]$Root)
    $marker = Join-Path $Root ".git"
    return (Test-Path -LiteralPath $marker -PathType Leaf) -and
        ((Get-Content -LiteralPath $marker -Raw) -match '^gitdir: /mnt/')
}

function Convert-ToWslPath {
    param([Parameter(Mandatory)][string]$WindowsPath)
    $output = @(& wsl.exe -e wslpath -u $WindowsPath 2>&1)
    $exitCode = $LASTEXITCODE
    $resolved = [string]($output | Select-Object -First 1)
    if ($exitCode -ne 0 -or [string]::IsNullOrWhiteSpace($resolved)) {
        throw "wslpath failed for '$WindowsPath': $($output -join ' ')"
    }
    return $resolved.Trim()
}

function Convert-GitWorktreePathToWindows {
    param([Parameter(Mandatory)][string]$Path)
    if ($Path -match '^/mnt/([A-Za-z])(?:/(.*))?$') {
        $drive = $Matches[1].ToUpperInvariant()
        $tail = [string]$Matches[2]
        if ([string]::IsNullOrWhiteSpace($tail)) { return "${drive}:\" }
        return "${drive}:\$($tail.Replace('/', '\'))"
    }
    return $Path.Replace('/', '\')
}

function Invoke-GitText {
    param([string]$Root, [string[]]$Arguments, [switch]$DisableAutoCrlf)
    $usesWslGit = Test-WslGitWorktreeMarker -Root $Root
    $gitRoot = if ($usesWslGit) { Convert-ToWslPath -WindowsPath $Root } else { $Root }
    $gitArguments = @(Get-GitCommandArguments -Root $gitRoot -Arguments $Arguments `
        -DisableAutoCrlf:$DisableAutoCrlf)
    $output = if ($usesWslGit) {
        & wsl.exe -e git @gitArguments 2>&1
    } else {
        & git @gitArguments 2>&1
    }
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $output" }
    return ($output -join "`n").Trim()
}

function ConvertFrom-GitPathOutput {
    param([object[]]$Output)
    return @($Output | ForEach-Object {
        ([string]$_).Split([char]0)
    } | Where-Object { $_ } |
        ForEach-Object { ([string]$_).Replace('\', '/') })
}

function Invoke-GitPathList {
    param([string]$Root, [string[]]$Arguments, [switch]$DisableAutoCrlf)
    $usesWslGit = Test-WslGitWorktreeMarker -Root $Root
    $gitRoot = if ($usesWslGit) { Convert-ToWslPath -WindowsPath $Root } else { $Root }
    $gitArguments = @(Get-GitCommandArguments -Root $gitRoot -Arguments $Arguments `
        -DisableAutoCrlf:$DisableAutoCrlf)
    $output = if ($usesWslGit) {
        & wsl.exe -e git @gitArguments 2>$null
    } else {
        & git @gitArguments 2>$null
    }
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
    return @(ConvertFrom-GitPathOutput -Output @($output))
}

function Get-TrackedPathsAtSourceRevision {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$SourceRevision,
        [switch]$Detached
    )
    if ([string]::IsNullOrWhiteSpace($SourceRevision)) {
        throw "Source revision is required for tracked-path ownership."
    }
    return @(Invoke-GitPathList -Root $Root -DisableAutoCrlf:$Detached `
        -Arguments @("ls-tree", "-r", "--name-only", "-z", $SourceRevision))
}

function Get-GitChangeClassification {
    param(
        [string[]]$StatusLines,
        [string[]]$TrackedContentPaths,
        [string[]]$StagedContentPaths
    )
    return [pscustomobject]@{
        Tracked = @($TrackedContentPaths)
        Staged = @($StagedContentPaths)
        Untracked = @($StatusLines | Where-Object { $_ -match '^\?\?' } |
            ForEach-Object { $_.Substring(3).Replace('\', '/') })
    }
}

function Get-GitSnapshot {
    param(
        [Parameter(Mandatory)][string]$Root,
        [string[]]$CanaryPaths = @(),
        [switch]$Detached
    )
    $status = @(Invoke-GitPathList -Root $Root -DisableAutoCrlf:$Detached `
        -Arguments @("status", "--porcelain=v1", "-z", "-uall"))
    # Unity can rewrite a file byte-for-byte and leave only its stat data changed.
    # Status reports that as `.M`; content diffs are the authoritative dirty gate.
    $trackedContent = @(Invoke-GitPathList -Root $Root -DisableAutoCrlf:$Detached `
        -Arguments @("diff", "--name-only", "--"))
    $stagedContent = @(Invoke-GitPathList -Root $Root -DisableAutoCrlf:$Detached `
        -Arguments @("diff", "--cached", "--name-only", "--"))
    $changes = Get-GitChangeClassification -StatusLines $status `
        -TrackedContentPaths $trackedContent -StagedContentPaths $stagedContent
    $canaries = [ordered]@{}
    foreach ($relative in $CanaryPaths) {
        $candidate = Join-Path $Root $relative.Replace('/', '\')
        $canaries[$relative] = if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            Get-Sha256 -Path $candidate
        } else { "<missing>" }
    }
    $originMain = Invoke-GitText -Root $Root -DisableAutoCrlf:$Detached `
        -Arguments @("rev-parse", "origin/main")
    $counts = (Invoke-GitText -Root $Root -DisableAutoCrlf:$Detached -Arguments @(
        "rev-list", "--left-right", "--count", "origin/main...HEAD"
    )) -split '\s+'
    return [ordered]@{
        head = Invoke-GitText -Root $Root -DisableAutoCrlf:$Detached `
            -Arguments @("rev-parse", "HEAD")
        tree = Invoke-GitText -Root $Root -DisableAutoCrlf:$Detached `
            -Arguments @("rev-parse", "HEAD^{tree}")
        branch = if ($Detached) { "(detached)" } else {
            Invoke-GitText -Root $Root -Arguments @("rev-parse", "--abbrev-ref", "HEAD")
        }
        detached = [bool]$Detached
        originMain = $originMain
        behind = [int]$counts[0]
        ahead = [int]$counts[1]
        tracked = @($changes.Tracked)
        staged = @($changes.Staged)
        untracked = @($changes.Untracked)
        canaries = $canaries
    }
}

function Test-EstablishedAddressablesResidueSet {
    param(
        [string[]]$UntrackedPaths,
        [string[]]$WindowsFiles = @()
    )
    $allowedUntracked = @(
        "Assets/AddressableAssetsData/ProfileDataSourceSettings.asset",
        "Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta",
        "Assets/AddressableAssetsData/Windows.meta",
        "Assets/AddressableAssetsData/link.xml",
        "Assets/AddressableAssetsData/link.xml.meta"
    )
    $allowedWindowsFiles = @(
        "Assets/AddressableAssetsData/Windows/addressables_content_state.bin",
        "Assets/AddressableAssetsData/Windows/addressables_content_state.bin.meta"
    )
    return @($UntrackedPaths | Where-Object {
        $allowedUntracked -cnotcontains $_
    }).Count -eq 0 -and @($WindowsFiles | Where-Object {
        $allowedWindowsFiles -cnotcontains $_
    }).Count -eq 0
}

function Remove-EstablishedAddressablesResidue {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)]$Snapshot,
        [Parameter(Mandatory)][string[]]$TrackedPathsAtSourceRevision
    )
    $windowsRoot = Join-Path $Root "Assets\AddressableAssetsData\Windows"
    $windowsFiles = if (Test-Path -LiteralPath $windowsRoot -PathType Container) {
        @(Get-ChildItem -LiteralPath $windowsRoot -File -Recurse -Force |
            ForEach-Object {
                $_.FullName.Substring($Root.Length).TrimStart('\').Replace('\', '/')
            })
    } else { @() }
    $hasTrackedChanges = @($Snapshot.Tracked).Count -ne 0
    $hasStagedChanges = @($Snapshot.Staged).Count -ne 0
    $establishedResidueSet = Test-EstablishedAddressablesResidueSet `
        -UntrackedPaths @($Snapshot.Untracked) -WindowsFiles @($windowsFiles)
    $recognized = -not $hasTrackedChanges -and -not $hasStagedChanges -and
        [bool]$establishedResidueSet
    $removed = @()
    $decisions = @()
    $prunedDirectories = @()
    $candidates = @(
        "Assets/AddressableAssetsData/Windows.meta",
        "Assets/AddressableAssetsData/ProfileDataSourceSettings.asset",
        "Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta",
        "Assets/AddressableAssetsData/link.xml",
        "Assets/AddressableAssetsData/link.xml.meta",
        "Assets/AddressableAssetsData/Windows/addressables_content_state.bin",
        "Assets/AddressableAssetsData/Windows/addressables_content_state.bin.meta"
    )
    foreach ($relative in $candidates) {
        $path = Join-Path $Root $relative.Replace('/', '\')
        $trackedAtSourceRevision = @($TrackedPathsAtSourceRevision) -ccontains $relative
        $wasPresent = Test-Path -LiteralPath $path -PathType Leaf
        $action = "None"
        $reason = "NotPresent"
        if ($trackedAtSourceRevision) {
            $action = if ($wasPresent) { "Preserved" } else { "NoAction" }
            $reason = "TrackedSource"
        } elseif (-not $recognized) {
            $action = "NoAction"
            $reason = "ResidueSetNotRecognized"
        } elseif ($wasPresent) {
            Remove-Item -LiteralPath $path -Force
            $removed += $relative
            $action = "Removed"
            $reason = "UntrackedEstablishedGeneratedResidue"
        }
        if ($trackedAtSourceRevision -or $wasPresent) {
            $decisions += [pscustomobject][ordered]@{
                path = $relative
                candidateKind = "KnownGeneratedResidueCandidate"
                trackedAtSourceRevision = [bool]$trackedAtSourceRevision
                action = $action
                reason = $reason
            }
        }
    }
    if ($recognized -and (Test-Path -LiteralPath $windowsRoot -PathType Container) -and
        $null -eq (Get-ChildItem -LiteralPath $windowsRoot -Force |
            Select-Object -First 1)) {
        Remove-Item -LiteralPath $windowsRoot -Force
        $prunedDirectories += "Assets/AddressableAssetsData/Windows"
    }
    return [pscustomobject][ordered]@{
        recognized = [bool]$recognized
        detectedUntrackedPaths = @($Snapshot.Untracked)
        detectedWindowsFiles = @($windowsFiles)
        removedPaths = @($removed)
        prunedDirectories = @($prunedDirectories)
        decisions = @($decisions)
    }
}

function Get-RepositoryFamilyPaths {
    param([string]$Root)
    $lines = @(Invoke-GitPathList -Root $Root `
        -Arguments @("worktree", "list", "--porcelain"))
    return @($lines | Where-Object { $_ -like "worktree *" } |
        ForEach-Object {
            Convert-GitWorktreePathToWindows -Path $_.Substring(9)
        })
}

function Write-FailureEvidence {
    param([string]$Path, [string]$Stage, [int]$ExitCode, [string]$SourceSha,
        [string]$RunId, [string]$PrivateLogPath, [string]$PrivateDiagnosticsPath = "",
        [Nullable[int]]$UnityExitCode = $null, [string]$BuildEvidenceReason = "")
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    [ordered]@{
        failureStage = $Stage
        exitCode = $ExitCode
        sourceSha = $SourceSha
        runId = $RunId
        privateLogPath = $PrivateLogPath
        privateDiagnosticsPath = $PrivateDiagnosticsPath
        unityExitCode = if ($null -ne $UnityExitCode) { [int]$UnityExitCode } else { $null }
        buildEvidenceReason = $BuildEvidenceReason
        timestampUtc = [DateTime]::UtcNow.ToString("o")
        deployable = $false
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Path "FAILURE.json") -Encoding UTF8
}

function Get-WindowsNativeToolchainIdentity {
    param([string]$Backend = $script:BackendPolicy.Backend)
    if ($Backend -cne "IL2CPP") {
        return [pscustomobject]@{
            NativeCompilerIdentity = "NotApplicable-Mono"
            WindowsSdkIdentity = "NotApplicable-Mono"
        }
    }

    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
        throw "Visual Studio locator is missing: $vswhere"
    }
    $installation = (& $vswhere -latest -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -property installationPath | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($installation)) {
        throw "MSVC x64 C++ toolchain is not installed."
    }
    $vcRoot = Join-Path $installation "VC\Tools\MSVC"
    $vc = Get-ChildItem -LiteralPath $vcRoot -Directory |
        Sort-Object Name -Descending | Select-Object -First 1
    $cl = Join-Path $vc.FullName "bin\Hostx64\x64\cl.exe"
    $link = Join-Path $vc.FullName "bin\Hostx64\x64\link.exe"
    if (-not (Test-Path -LiteralPath $cl -PathType Leaf) -or
        -not (Test-Path -LiteralPath $link -PathType Leaf)) {
        throw "MSVC x64 compiler/linker executable is missing."
    }

    $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $sdk = Get-ChildItem -LiteralPath $sdkRoot -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        Where-Object {
            Test-Path -LiteralPath (Join-Path $_.FullName "x64\rc.exe") -PathType Leaf
        } |
        Select-Object -First 1
    if ($null -eq $sdk) {
        throw "Windows SDK x64 tools are not installed."
    }

    return [pscustomobject]@{
        NativeCompilerIdentity = "MSVC-$($vc.Name)-x64"
        WindowsSdkIdentity = "WindowsSDK-$($sdk.Name)-x64"
    }
}

function Invoke-WindowsReleasePipeline {
    [CmdletBinding()]
    param(
        [string]$RepositoryRoot,
        [string]$UnityExe,
        [string]$OutputRoot,
        [string]$BuildSourceRoot,
        [string]$PreparedBuildSourceRoot = "",
        [string]$RunId,
        [ValidateSet("CanonicalStore", "BackendComparison")]
        [string]$BuildIntent = $script:BackendPolicy.BuildIntent,
        [string]$Backend = $script:BackendPolicy.Backend,
        [string]$BackendComparisonId = "",
        [string]$DistributionTarget = $script:DistributionPolicy.TargetId,
        [ValidateSet("InternalRc", "StoreDistributable")]
        [string]$PayloadAudience = $script:BackendPolicy.PayloadAudience,
        [string[]]$AllowUntrackedRoot
    )
    $stage = "preflight"
    $sourceSha = ""
    $staging = ""
    $final = ""
    $privateLog = ""
    $processDiagnosticsPath = ""
    $wrapperLog = ""
    $unityExitCode = $null
    $buildEvidenceReason = ""
    $exitCode = $script:ReleaseExitCodes.WrapperInternalError
    try {
        $backendPolicy = Resolve-StoreBackendPolicy $Backend $BuildIntent $PayloadAudience
        $distributionPolicy =
            Resolve-WindowsDistributionTargetPolicy $DistributionTarget
        if ($backendPolicy.Configuration -cne $script:ConfigurationName) {
            $exitCode = $script:ReleaseExitCodes.UnsupportedConfiguration
            throw "Invocation backend does not match the loaded wrapper policy."
        }
        if ($distributionPolicy.TargetId -cne $script:DistributionPolicy.TargetId) {
            $exitCode = $script:ReleaseExitCodes.UnsupportedConfiguration
            throw "Invocation distribution target does not match the loaded wrapper policy."
        }
        if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
            throw "Unity executable not found: $UnityExe"
        }
        $sourceSha = Invoke-GitText -Root $RepositoryRoot -Arguments @("rev-parse", "HEAD")
        $sourceTree = Invoke-GitText -Root $RepositoryRoot -Arguments @("rev-parse", "HEAD^{tree}")
        $comparisonId = if ([string]::IsNullOrWhiteSpace($BackendComparisonId)) {
            $sourceSha
        } else {
            $BackendComparisonId
        }
        if ($comparisonId -notmatch '^[A-Za-z0-9._-]+$') {
            $exitCode = $script:ReleaseExitCodes.UnsupportedConfiguration
            throw "Backend comparison id contains unsupported characters."
        }
        $artifactId = "$sourceSha-$RunId"
        $parent = Join-Path (Join-Path $OutputRoot $sourceSha) $script:ConfigurationPathName
        $privateRoot = Join-Path $parent ".private\$RunId"
        $privateLog = Join-Path $privateRoot "UnityEditor.log"
        $wrapperLog = Join-Path $privateRoot "wrapper.log"
        $processDiagnosticsPath = Join-Path $privateRoot "process-preflight.json"
        New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
        Write-WrapperLog $wrapperLog $stage "Pipeline started for $sourceSha."
        $canaries = @(
            "ProjectSettings/ProjectVersion.txt",
            "ProjectSettings/ProjectSettings.asset",
            "ProjectSettings/EditorBuildSettings.asset",
            "Assets/Settings/Build Profiles/Windows 1.asset",
            "Packages/manifest.json",
            "Packages/packages-lock.json",
            "Assets/_Features/Stages/Editor/Build/WindowsReleaseBuildCli.cs",
            "Assets/_Features/Stages/Editor/Build/WindowsReleaseBuildPolicy.cs",
            "Assets/_Features/Stages/Editor/Build/WindowsDistributionTargetPolicy.cs",
            "Tools/Build/Build-WindowsRelease.ps1"
        )
        $invocationPre = Get-GitSnapshot -Root $RepositoryRoot -CanaryPaths $canaries
        Write-PrivateJson (Join-Path $privateRoot "invocation-source-pre.json") `
            $invocationPre
        if (-not (Test-GitState -Snapshot $invocationPre -AllowedRoots $AllowUntrackedRoot)) {
            $exitCode = if (@($invocationPre.Tracked).Count -or @($invocationPre.Staged).Count) {
                $script:ReleaseExitCodes.GitPreflightFailure
            } else { $script:ReleaseExitCodes.UnknownUntrackedFailure }
            throw "Invocation worktree clean gate failed."
        }
        $family = Get-RepositoryFamilyPaths -Root $RepositoryRoot
        # Keep the complete snapshot so CrashHandler parent attribution is possible.
        $processes = @(Get-CimInstance Win32_Process)
        $preflightProcessGate = Get-ReleaseProcessGateResult `
            -Processes $processes -RepositoryFamilyPaths $family
        Write-ProcessGateDiagnostics -Path $processDiagnosticsPath -Phase "preflight" `
            -GateResult $preflightProcessGate
        if (-not $preflightProcessGate.Allowed) {
            $exitCode = $script:ReleaseExitCodes.ProcessGateFailure
            throw "Repository process gate failed."
        }

        $staging = Join-Path $parent ".staging-$RunId"
        $final = Join-Path $parent $RunId
        $failed = Join-Path (Join-Path $parent "failed") $RunId
        $buildSourcePlan = Resolve-BuildSourcePlan `
            -BuildSourceRoot $BuildSourceRoot `
            -PreparedBuildSourceRoot $PreparedBuildSourceRoot `
            -SourceSha $sourceSha `
            -RunId $RunId
        $detached = [string]$buildSourcePlan.Path
        if (-not (Test-BuildSourcePathBudget $detached)) {
            $exitCode = $script:ReleaseExitCodes.BuildSourcePathBudgetFailure
            throw "Detached source path exceeds the URP importer path budget."
        }
        if ((Test-Path -LiteralPath $staging) -or (Test-Path -LiteralPath $final) -or
            [IO.Path]::GetPathRoot($staging) -ne [IO.Path]::GetPathRoot($final)) {
            $exitCode = $script:ReleaseExitCodes.OutputCollision
            throw "Output collision or cross-volume promotion plan."
        }
        if ($buildSourcePlan.RequiresCreation -and (Test-Path -LiteralPath $detached)) {
            $exitCode = $script:ReleaseExitCodes.DetachedSourceFailure
            throw "Detached source collision."
        }
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        $stage = "detached-source"
        if ($buildSourcePlan.RequiresCreation) {
            New-Item -ItemType Directory -Path (Split-Path $detached -Parent) -Force |
                Out-Null
            try {
                Invoke-GitText -Root $RepositoryRoot -Arguments @(
                    "worktree", "add", "--detach", $detached, $sourceSha
                ) -DisableAutoCrlf | Out-Null
            } catch {
                $exitCode = $script:ReleaseExitCodes.DetachedSourceFailure
                throw "Detached worktree creation failed: $($_.Exception.Message)"
            }
        } elseif (-not (Test-Path -LiteralPath $detached -PathType Container)) {
            $exitCode = $script:ReleaseExitCodes.DetachedSourceFailure
            throw "Prepared detached source does not exist."
        }
        $buildPre = Get-GitSnapshot -Root $detached -CanaryPaths $canaries -Detached
        Write-PrivateJson (Join-Path $privateRoot "build-source-pre.json") $buildPre
        if (-not (Test-GitState -Snapshot $buildPre -RequireNoUntracked)) {
            $exitCode = $script:ReleaseExitCodes.DetachedSourceFailure
            throw "Detached source is not clean."
        }
        if ([string]$buildPre.head -cne $sourceSha -or
            [string]$buildPre.tree -cne $sourceTree) {
            $exitCode = $script:ReleaseExitCodes.DetachedSourceFailure
            throw "Detached source HEAD/tree identity does not match the invocation revision."
        }
        $trackedPathsAtSourceRevision = @(Get-TrackedPathsAtSourceRevision `
            -Root $detached -SourceRevision $sourceSha -Detached)
        $entrySourcePath = Join-Path $detached `
            "Assets\_Features\Stages\Editor\Build\WindowsReleaseBuildCli.cs"
        $policySourcePath = Join-Path $detached `
            "Assets\_Features\Stages\Editor\Build\WindowsReleaseBuildPolicy.cs"
        $detachedWrapperSourcePath = Join-Path $detached `
            "Tools\Build\Build-WindowsRelease.ps1"
        $expectation = New-ReleaseEvidenceExpectation -RunId $RunId `
            -ArtifactId $artifactId -SourceSha $sourceSha -SourceTree $sourceTree `
            -EntrySourcePath $entrySourcePath -PolicySourcePath $policySourcePath `
            -DetachedWrapperSourcePath $detachedWrapperSourcePath `
            -ExecutingWrapperSourcePath $script:ExecutingWrapperSourcePath `
            -Backend $backendPolicy.ScriptingBackend `
            -BackendIdentity $backendPolicy.Backend `
            -ManagedStrippingLevel $backendPolicy.ManagedStrippingLevel `
            -Il2CppCompilerConfiguration $backendPolicy.Il2CppCompilerConfiguration `
            -BackendComparisonId $comparisonId `
            -ComparisonRole $backendPolicy.ComparisonRole `
            -BuildIntent $backendPolicy.BuildIntent `
            -StoreConfigurationSchema $backendPolicy.StoreConfigurationSchema `
            -StoreConfigurationId $backendPolicy.StoreConfigurationId `
            -PlayerLogEnabled $backendPolicy.PlayerLogEnabled `
            -LogPolicyId $backendPolicy.LogPolicyId `
            -AutomaticLogUpload $backendPolicy.AutomaticLogUpload `
            -PayloadAudience $backendPolicy.PayloadAudience `
            -DistributionTargetId $distributionPolicy.TargetId `
            -ProviderSelectionMode $distributionPolicy.ProviderSelectionMode `
            -ExpectedProviderId $distributionPolicy.ExpectedProviderId `
            -ExpectedLaunchArguments $distributionPolicy.ExpectedLaunchArguments `
            -RequiredArtifacts $distributionPolicy.RequiredArtifacts `
            -ForbiddenArtifacts $distributionPolicy.ForbiddenArtifacts `
            -ExpectedStoreLaunch $distributionPolicy.ExpectedStoreLaunch

        $payload = Join-Path $staging "payload"
        New-Item -ItemType Directory -Path $payload -Force | Out-Null
        New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
        $metadataPath = Join-Path $payload "build-metadata.json"
        $reportPath = Join-Path $payload "build-report-summary.json"
        $reportDetailsPath = Join-Path $privateRoot "build-report-details.json"
        $settingsTransactionPath = Join-Path $privateRoot "settings-transaction.json"
        $exePath = Join-Path $payload "VectorQuake.exe"

        $stage = "unity"
        $unityArguments = @(
            "-batchmode", "-nographics", "-buildTarget", "Win64",
            "-projectPath", $detached,
            "-executeMethod", "WindowsReleaseBuildCli.BuildWindowsX64NonDevelopment",
            "-releaseOutputPath", $exePath,
            "-releaseRunId", $RunId,
            "-releaseArtifactId", $artifactId,
            "-releaseSourceSha", $sourceSha,
            "-releaseSourceTree", $sourceTree,
            "-releaseBuildIntent", $BuildIntent,
            "-releaseBackend", $Backend,
            "-releaseBackendComparisonId", $comparisonId,
            "-releasePayloadAudience", $PayloadAudience,
            "-releaseDistributionTarget", $DistributionTarget,
            "-releaseIntermediateMetadataPath", $metadataPath,
            "-releaseBuildReportPath", $reportPath,
            "-releaseBuildReportDetailsPath", $reportDetailsPath,
            "-releaseSettingsTransactionPath", $settingsTransactionPath,
            "-logFile", $privateLog
        )
        $unityProcess = Start-Process -FilePath $UnityExe -ArgumentList $unityArguments -PassThru
        $processesAfterStart = @(Get-CimInstance Win32_Process)
        $postStartProcessGate = Get-ReleaseProcessGateResult `
            -Processes $processesAfterStart -RepositoryFamilyPaths $family `
            -AllowedUnityPid $unityProcess.Id
        $processDiagnosticsPath = Join-Path $privateRoot "process-poststart.json"
        Write-ProcessGateDiagnostics -Path $processDiagnosticsPath -Phase "post-unity-start" `
            -GateResult $postStartProcessGate -AllowedUnityPid $unityProcess.Id
        if (-not $postStartProcessGate.Allowed) {
            try { Stop-Process -Id $unityProcess.Id -Force -ErrorAction SilentlyContinue } catch {}
            $exitCode = $script:ReleaseExitCodes.ProcessGateFailure
            throw "Process gate changed after Unity start."
        }
        $unityProcess.WaitForExit()
        $unityExitCode = [int]$unityProcess.ExitCode
        Write-WrapperLog $wrapperLog "unity" "Unity exited with code $unityExitCode."
        $postBuildProcessGate = Get-ReleaseProcessGateResult `
            -Processes @(Get-CimInstance Win32_Process) -RepositoryFamilyPaths $family
        $processDiagnosticsPath = Join-Path $privateRoot "process-postbuild.json"
        Write-ProcessGateDiagnostics -Path $processDiagnosticsPath -Phase "post-build" `
            -GateResult $postBuildProcessGate
        if (-not $postBuildProcessGate.Allowed) {
            $exitCode = $script:ReleaseExitCodes.ProcessGateFailure
            throw "Process gate changed after Unity exit."
        }

        $stage = "build-evidence"
        $buildEvidence = Test-BuildEvidence -MetadataPath $metadataPath `
            -SummaryPath $reportPath -DetailsPath $reportDetailsPath `
            -ExpectedIdentity $expectation
        $buildEvidenceReason = [string]$buildEvidence.Reason
        $decision = Get-BuildEvidenceGateDecision $buildEvidence
        if (-not $decision.CreateSuccess -or -not $decision.Promote) {
            $exitCode = $script:ReleaseExitCodes.BuildEvidenceFailure
            throw "Build evidence rejected: $buildEvidenceReason."
        }
        if ($unityExitCode -ne 0) {
            $exitCode = $script:ReleaseExitCodes.UnityInvocationFailure
            throw "Unity returned exit code $unityExitCode."
        }

        $stage = "drift"
        $preCleanupBuildSnapshot = Get-GitSnapshot -Root $detached `
            -CanaryPaths $canaries -Detached
        $addressablesCleanup = Remove-EstablishedAddressablesResidue `
            -Root $detached -Snapshot $preCleanupBuildSnapshot `
            -TrackedPathsAtSourceRevision $trackedPathsAtSourceRevision
        Write-PrivateJson (Join-Path $privateRoot "addressables-residue-cleanup.json") `
            $addressablesCleanup
        $invocationPost = Get-GitSnapshot -Root $RepositoryRoot -CanaryPaths $canaries
        $buildPost = Get-GitSnapshot -Root $detached -CanaryPaths $canaries -Detached
        Write-PrivateJson (Join-Path $privateRoot "invocation-source-post.json") `
            $invocationPost
        Write-PrivateJson (Join-Path $privateRoot "build-source-post.json") $buildPost
        if (-not (Test-SnapshotEquality $invocationPre $invocationPost) -or
            -not (Test-SnapshotEquality $buildPre $buildPost)) {
            $exitCode = $script:ReleaseExitCodes.SourceDriftDetected
            throw "Source or configuration drift detected."
        }

        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        $metadata.sourceSha = $sourceSha
        $metadata.sourceTree = $sourceTree
        $metadata.branch = [string]$invocationPre.branch
        $metadata.headDetached = $true
        $metadata.originMainSha = [string]$invocationPre.originMain
        $metadata.ahead = [int]$invocationPre.ahead
        $metadata.behind = [int]$invocationPre.behind
        $metadata.sourceDirty = $false
        $metadata.entrySourceSha256 = $expectation.EntrySourceSha256
        $metadata.policySourceSha256 = $expectation.PolicySourceSha256
        $metadata.wrapperSourceSha256 = $expectation.WrapperSourceSha256
        $toolchain = Get-WindowsNativeToolchainIdentity $Backend
        $metadata.nativeCompilerIdentity = $toolchain.NativeCompilerIdentity
        $metadata.windowsSdkIdentity = $toolchain.WindowsSdkIdentity
        $metadata | ConvertTo-Json -Depth 10 |
            Set-Content -LiteralPath $metadataPath -Encoding UTF8
        $buildEvidence = Test-BuildEvidence -MetadataPath $metadataPath `
            -SummaryPath $reportPath -DetailsPath $reportDetailsPath `
            -ExpectedIdentity $expectation
        if (-not $buildEvidence.Allowed) {
            $exitCode = $script:ReleaseExitCodes.BuildEvidenceFailure
            $buildEvidenceReason = [string]$buildEvidence.Reason
            throw "Enriched build evidence rejected: $buildEvidenceReason."
        }
        $configurationSummaryPath = Join-Path $payload "configuration-summary.json"
        [ordered]@{
            schemaVersion = $script:ConfigurationSummarySchemaVersion
            storeConfigurationSchema = $backendPolicy.StoreConfigurationSchema
            storeConfigurationId = $backendPolicy.StoreConfigurationId
            buildIntent = $backendPolicy.BuildIntent
            configuration = $script:ConfigurationName
            backend = $backendPolicy.Backend
            scriptingBackend = $backendPolicy.ScriptingBackend
            managedStrippingLevel = $backendPolicy.ManagedStrippingLevel
            il2cppCompilerConfiguration =
                $backendPolicy.Il2CppCompilerConfiguration
            backendComparisonId = $comparisonId
            comparisonRole = $backendPolicy.ComparisonRole
            development = $false
            connectWithProfiler = $false
            deepProfiling = $false
            allowDebugging = $false
            waitForPlayerConnection = $false
            forceEnableAssertions = $false
            playerLogEnabled = $backendPolicy.PlayerLogEnabled
            logPolicyId = $backendPolicy.LogPolicyId
            automaticLogUpload = $backendPolicy.AutomaticLogUpload
            stackTracePolicy = "ScriptOnly"
            payloadAudience = $backendPolicy.PayloadAudience
            distributionTargetId = $distributionPolicy.TargetId
            providerSelectionMode = $distributionPolicy.ProviderSelectionMode
            expectedProviderId = $distributionPolicy.ExpectedProviderId
            expectedLaunchArguments = @($distributionPolicy.ExpectedLaunchArguments)
            requiredArtifacts = @($distributionPolicy.RequiredArtifacts)
            forbiddenArtifacts = @($distributionPolicy.ForbiddenArtifacts)
            expectedStoreLaunch = $distributionPolicy.ExpectedStoreLaunch
            scenes = @(
                "Assets/Scenes/MainMenuScene.unity",
                "Assets/Scenes/UIAudioScene.unity"
            )
        } | ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath $configurationSummaryPath -Encoding UTF8
        if (-not (Test-ConfigurationSummary -Path $configurationSummaryPath `
                -Expectation $expectation)) {
            $exitCode = $script:ReleaseExitCodes.ControlFileConsistencyFailure
            throw "Configuration summary consistency failed."
        }

        $stage = "payload-policy"
        $payloadPolicy = Prepare-PayloadForAudience -PayloadRoot $staging `
            -Audience $PayloadAudience
        $stage = "manifest"
        $manifest = New-PayloadManifest -PayloadRoot $staging
        if (-not (Test-PayloadManifest -PayloadRoot $staging)) {
            $exitCode = $script:ReleaseExitCodes.ManifestVerificationFailure
            throw "Payload manifest verification failed."
        }
        $stage = "artifact-provenance"
        $provenance = New-ArtifactProvenance -ArtifactRoot $staging `
            -RunId $RunId -ArtifactId $artifactId -SourceSha $sourceSha `
            -SourceTree $sourceTree -BuildMetadataPath $metadataPath `
            -ConfigurationSummaryPath $configurationSummaryPath `
            -BuildReportSummaryPath $reportPath `
            -BuildReportDetailsPath $reportDetailsPath -Manifest $manifest `
            -BuildEvidence $buildEvidence -PayloadPolicy $payloadPolicy `
            -EntrySourceSha256 $metadata.entrySourceSha256 `
            -PolicySourceSha256 $metadata.policySourceSha256 `
            -WrapperSourceSha256 $metadata.wrapperSourceSha256
        if (-not (Test-ArtifactProvenance -ArtifactRoot $staging `
                -BuildReportDetailsPath $reportDetailsPath `
                -Expectation $expectation)) {
            $exitCode = $script:ReleaseExitCodes.ArtifactProvenanceFailure
            throw "Artifact provenance binding failed."
        }
        $stage = "success-control"
        New-SuccessControl -PayloadRoot $staging -RunId $RunId -ArtifactId $artifactId `
            -SourceSha $sourceSha -SourceTree $sourceTree -Manifest $manifest `
            -Provenance $provenance -BuildEvidence $buildEvidence `
            -PayloadPolicy $payloadPolicy | Out-Null
        if (-not (Test-SuccessControl -PayloadRoot $staging `
                -BuildReportDetailsPath $reportDetailsPath `
                -Expectation $expectation)) {
            $exitCode = $script:ReleaseExitCodes.ControlFileConsistencyFailure
            throw "SUCCESS control consistency failed."
        }

        $stage = "promotion"
        if (Test-Path -LiteralPath $final) {
            $exitCode = $script:ReleaseExitCodes.OutputCollision
            throw "Final artifact path already exists."
        }
        Move-Item -LiteralPath $staging -Destination $final
        if (-not (Test-SuccessControl -PayloadRoot $final `
                -BuildReportDetailsPath $reportDetailsPath `
                -Expectation $expectation) -or
            -not (Test-ArtifactProvenance -ArtifactRoot $final `
                -BuildReportDetailsPath $reportDetailsPath `
                -Expectation $expectation)) {
            $exitCode = $script:ReleaseExitCodes.PromotionFailure
            throw "Final verification after promotion failed."
        }
        Write-Host (
            "WINDOWS_X64_NONDEVELOPMENT_$($Backend.ToUpperInvariant())_" +
            "$($DistributionTarget.ToUpperInvariant())_BUILD_PASS")
        Write-Host "FinalArtifact=$final"
        Write-WrapperLog $wrapperLog "complete" "Artifact promoted and reverified."
        return 0
    }
    catch {
        if ($exitCode -eq $script:ReleaseExitCodes.WrapperInternalError -and
            $stage -eq "preflight") {
            $exitCode = $script:ReleaseExitCodes.GitPreflightFailure
        }
        Write-Error "[$stage][$exitCode] $($_.Exception.Message)"
        Write-WrapperLog $wrapperLog $stage `
            "Pipeline failed with wrapper code ${exitCode}: $($_.Exception.Message)"
        $quarantineCandidate = if (-not [string]::IsNullOrWhiteSpace($staging) -and
            (Test-Path -LiteralPath $staging)) { $staging } elseif (
            $stage -eq "promotion" -and
            -not [string]::IsNullOrWhiteSpace($final) -and
            (Test-Path -LiteralPath $final)) { $final } else { "" }
        if (-not [string]::IsNullOrWhiteSpace($quarantineCandidate)) {
            try {
                $failedPath = Join-Path (Join-Path (Split-Path $staging -Parent) "failed") $RunId
                if (-not (Test-Path -LiteralPath $failedPath)) {
                    Write-FailureEvidence -Path $quarantineCandidate -Stage $stage -ExitCode $exitCode `
                        -SourceSha $sourceSha -RunId $RunId -PrivateLogPath $privateLog `
                        -PrivateDiagnosticsPath $processDiagnosticsPath `
                        -UnityExitCode $unityExitCode `
                        -BuildEvidenceReason $buildEvidenceReason
                    New-Item -ItemType Directory -Path (Split-Path $failedPath -Parent) `
                        -Force | Out-Null
                    Move-Item -LiteralPath $quarantineCandidate -Destination $failedPath
                }
            } catch {
                Write-Warning "Failure quarantine could not be completed: $($_.Exception.Message)"
            }
        }
        return $exitCode
    }
}

if ($env:VECTORQUAKE_RELEASE_WRAPPER_TEST_MODE -ne "1") {
    $pipelineExit = Invoke-WindowsReleasePipeline `
        -RepositoryRoot $RepositoryRoot `
        -UnityExe $UnityExe `
        -OutputRoot $OutputRoot `
        -BuildSourceRoot $BuildSourceRoot `
        -PreparedBuildSourceRoot $PreparedBuildSourceRoot `
        -RunId $RunId `
        -BuildIntent $BuildIntent `
        -Backend $Backend `
        -BackendComparisonId $BackendComparisonId `
        -DistributionTarget $DistributionTarget `
        -PayloadAudience $PayloadAudience `
        -AllowUntrackedRoot $AllowUntrackedRoot
    exit $pipelineExit
}
