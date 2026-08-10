[CmdletBinding()]
param(
    [string]$SourceBuildRoot = "",
    [string]$DistributionTarget = "",
    [string]$OutputRoot = "",
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$SourceSha = "",
    [string]$SourceTree = "",
    [string]$ArtifactId = "",
    [string]$RunId = ([DateTime]::UtcNow.ToString("yyyyMMddTHHmmssfffZ"))
)

$ErrorActionPreference = "Stop"

function Get-WindowsDistributionDriveType {
    param([Parameter(Mandatory)][string]$Path)

    $pathRoot = [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($Path))
    try {
        return ([IO.DriveInfo]::new($pathRoot)).DriveType
    } catch {
        throw "STAGING_POLICY_VALIDATOR_DRIVE_TYPE_UNAVAILABLE: $Path"
    }
}

function Assert-WindowsDistributionLocalValidatorPath {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        -not [IO.Path]::IsPathRooted($Path)) {
        throw "STAGING_POLICY_VALIDATOR_PATH_INVALID: $Name"
    }

    $normalized = $Path.Replace('\', '/')
    if ($normalized.StartsWith('//', [StringComparison]::Ordinal)) {
        throw "STAGING_POLICY_VALIDATOR_NETWORK_PATH_REJECTED: $Name"
    }

    $driveType = Get-WindowsDistributionDriveType -Path $Path
    if ($driveType -notin @(
            [IO.DriveType]::Fixed,
            [IO.DriveType]::Removable,
            [IO.DriveType]::Ram)) {
        throw "STAGING_POLICY_VALIDATOR_NETWORK_PATH_REJECTED: $Name"
    }

    $ancestors = @()
    $current = [IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        $ancestors += $current
        $trimmed = $current.TrimEnd('\', '/')
        $parent = [IO.Path]::GetDirectoryName($trimmed)
        if ([string]::IsNullOrWhiteSpace($parent) -or
            $parent -ceq $current) {
            break
        }
        $current = $parent
    }
    [array]::Reverse($ancestors)
    foreach ($ancestor in $ancestors) {
        if (-not (Test-Path -LiteralPath $ancestor)) {
            break
        }
        $item = Get-Item -LiteralPath $ancestor -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "STAGING_POLICY_VALIDATOR_REPARSE_PATH_REJECTED: $ancestor"
        }
    }
}

function Assert-WindowsDistributionStagerIdentity {
    param(
        [Parameter(Mandatory)][type]$StagerType,
        [Parameter(Mandatory)][string]$ExpectedIdentity
    )

    $identityType = $StagerType.Assembly.GetType(
        "WindowsDistributionStagerCompiledIdentity",
        $false,
        $false)
    $identityField = if ($null -eq $identityType) {
        $null
    } else {
        $identityType.GetField(
            "SourceIdentitySha256",
            [Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static)
    }
    $actualIdentity = if ($null -eq $identityField) {
        ""
    } else {
        [string]$identityField.GetRawConstantValue()
    }
    if ($actualIdentity -cne $ExpectedIdentity) {
        throw "STAGING_POLICY_LOADED_IDENTITY_MISMATCH: expected $ExpectedIdentity, got $actualIdentity"
    }
}

function Import-WindowsDistributionStagerTypes {
    param(
        [Parameter(Mandatory)][string]$Root,
        [switch]$OfflineOnly,
        [string]$CacheRoot = ""
    )

    $sourcePaths = @(
        "Assets\_Features\Stages\Editor\Build\WindowsDistributionTargetPolicy.cs",
        "Assets\_Features\Stages\Editor\Build\SteamPipeStagingSanitizerPolicy.cs",
        "Assets\_Features\Stages\Editor\Build\WindowsDistributionStager.cs"
    ) | ForEach-Object { Join-Path $Root $_ }

    foreach ($sourcePath in $sourcePaths) {
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "STAGING_POLICY_SOURCE_MISSING: $sourcePath"
        }
    }

    $sourceIdentity = "typed-validator-v2-" + (($sourcePaths | ForEach-Object {
        (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    }) -join "-")
    $identityBytes = [Text.Encoding]::UTF8.GetBytes($sourceIdentity)
    $identityHash = [Security.Cryptography.SHA256]::Create()
    try {
        $cacheKey = -join ($identityHash.ComputeHash($identityBytes) |
            ForEach-Object { $_.ToString("x2") })
    } finally {
        $identityHash.Dispose()
    }

    $compileCacheRoot = if ([string]::IsNullOrWhiteSpace($CacheRoot)) {
        Join-Path ([IO.Path]::GetTempPath()) "VectorQuakeDistributionStager"
    } else {
        [IO.Path]::GetFullPath($CacheRoot)
    }
    $compileRoot = Join-Path $compileCacheRoot $cacheKey
    Assert-WindowsDistributionLocalValidatorPath `
        -Path $compileRoot `
        -Name "compile cache"

    $loadedStagerType = "WindowsDistributionStager" -as [type]
    if ($null -ne $loadedStagerType) {
        Assert-WindowsDistributionStagerIdentity `
            -StagerType $loadedStagerType `
            -ExpectedIdentity $cacheKey
        Assert-WindowsDistributionLocalValidatorPath `
            -Path ([string]$loadedStagerType.Assembly.Location) `
            -Name "loaded validator assembly"
        return
    }

    $assemblyPath = Join-Path $compileRoot "bin\VectorQuake.DistributionStager.dll"
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        New-Item -ItemType Directory -Path $compileRoot -Force | Out-Null
        Assert-WindowsDistributionLocalValidatorPath `
            -Path $compileRoot `
            -Name "compile cache"
        foreach ($sourcePath in $sourcePaths) {
            [IO.File]::Copy(
                $sourcePath,
                (Join-Path $compileRoot ([IO.Path]::GetFileName($sourcePath))),
                $true)
        }
        $compiledIdentityPath = Join-Path $compileRoot `
            "WindowsDistributionStagerCompiledIdentity.cs"
        $compiledIdentity = @"
public static class WindowsDistributionStagerCompiledIdentity
{
    public const string SourceIdentitySha256 = "$cacheKey";
}
"@
        [IO.File]::WriteAllText(
            $compiledIdentityPath,
            $compiledIdentity,
            [Text.UTF8Encoding]::new($false))
        $projectPath = Join-Path $compileRoot "VectorQuake.DistributionStager.csproj"
        $project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>VectorQuake.DistributionStager</AssemblyName>
  </PropertyGroup>
</Project>
'@
        [IO.File]::WriteAllText(
            $projectPath, $project, [Text.UTF8Encoding]::new($false))
        if ($OfflineOnly) {
            $offlineSource = Join-Path $compileRoot "offline-package-source"
            New-Item -ItemType Directory -Path $offlineSource -Force | Out-Null
            $nugetConfigPath = Join-Path $compileRoot "NuGet.Offline.Config"
            $offlineSourceXml = [Security.SecurityElement]::Escape($offlineSource)
            $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="offline" value="$offlineSourceXml" />
  </packageSources>
</configuration>
"@
            [IO.File]::WriteAllText(
                $nugetConfigPath, $nugetConfig, [Text.UTF8Encoding]::new($false))
            $previousTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
            $previousFirstTime = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
            $previousWorkloadUpdate = `
                $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE
            try {
                $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
                $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
                $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = "1"
                $restoreOutput = @(& dotnet.exe restore $projectPath `
                    --configfile $nugetConfigPath --no-cache `
                    -p:NuGetAudit=false --nologo --verbosity quiet 2>&1)
                if ($LASTEXITCODE -ne 0) {
                    throw "STAGING_POLICY_OFFLINE_RESTORE_FAILED: $($restoreOutput -join [Environment]::NewLine)"
                }
                $buildOutput = @(& dotnet.exe build $projectPath `
                    --configuration Release `
                    --output (Join-Path $compileRoot "bin") `
                    --no-restore --nologo --verbosity quiet 2>&1)
            } finally {
                $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousTelemetry
                $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $previousFirstTime
                $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = `
                    $previousWorkloadUpdate
            }
        } else {
            $buildOutput = @(& dotnet.exe build $projectPath `
                --configuration Release --output (Join-Path $compileRoot "bin") `
                --nologo --verbosity quiet 2>&1)
        }
        if ($LASTEXITCODE -ne 0 -or
            -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
            throw "STAGING_POLICY_COMPILE_FAILED: $($buildOutput -join [Environment]::NewLine)"
        }
    }

    [void][Reflection.Assembly]::LoadFrom($assemblyPath)
    $loadedStagerType = "WindowsDistributionStager" -as [type]
    if ($null -eq $loadedStagerType) {
        throw "STAGING_POLICY_COMPILE_FAILED: validator type was not loaded."
    }
    Assert-WindowsDistributionStagerIdentity `
        -StagerType $loadedStagerType `
        -ExpectedIdentity $cacheKey
}

function Get-StagingSourceIdentity {
    param(
        [Parameter(Mandatory)][string]$Root,
        [string]$RequestedSourceSha,
        [string]$RequestedSourceTree,
        [string]$RequestedArtifactId
    )

    $identity = [ordered]@{
        SourceSha = $RequestedSourceSha
        SourceTree = $RequestedSourceTree
        ArtifactId = $RequestedArtifactId
        ScriptingBackend = ""
    }
    $metadataPath = Join-Path $Root "build-metadata.json"
    if (Test-Path -LiteralPath $metadataPath -PathType Leaf) {
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($identity.SourceSha)) {
            $identity.SourceSha = [string]$metadata.sourceSha
        }
        if ([string]::IsNullOrWhiteSpace($identity.SourceTree)) {
            $identity.SourceTree = [string]$metadata.sourceTree
        }
        if ([string]::IsNullOrWhiteSpace($identity.ArtifactId)) {
            $identity.ArtifactId = [string]$metadata.artifactId
        }
        $identity.ScriptingBackend = [string]$metadata.scriptingBackend
    }

    return [pscustomobject]$identity
}

function Get-RepositoryStatusSnapshot {
    param([Parameter(Mandatory)][string]$Root)
    $gitMarker = Join-Path $Root ".git"
    $usesWslGitDir = (Test-Path -LiteralPath $gitMarker -PathType Leaf) -and
        ((Get-Content -LiteralPath $gitMarker -Raw) -match '^gitdir: /mnt/')
    if ($usesWslGitDir) {
        $wslPathOutput = @(& wsl.exe -e wslpath -u $Root 2>&1)
        $wslPathExitCode = $LASTEXITCODE
        $wslRoot = [string]($wslPathOutput | Select-Object -First 1)
        if ($wslPathExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($wslRoot)) {
            throw "STAGING_GIT_STATUS_FAILED: could not resolve WSL repository path (exit=$wslPathExitCode, output=$($wslPathOutput -join ' '))."
        }
        $lines = @(& wsl.exe -e git -C $wslRoot status --short 2>&1)
    } else {
        $lines = @(& git -C $Root status --short 2>&1)
    }
    if ($LASTEXITCODE -ne 0) {
        throw "STAGING_GIT_STATUS_FAILED: $($lines -join ' ')"
    }
    return ($lines -join "`n")
}

function Invoke-WindowsDistributionStaging {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourceBuildRoot,
        [Parameter(Mandatory)][string]$DistributionTarget,
        [Parameter(Mandatory)][string]$OutputRoot,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [string]$SourceSha = "",
        [string]$SourceTree = "",
        [string]$ArtifactId = "",
        [string]$RunId = ([DateTime]::UtcNow.ToString("yyyyMMddTHHmmssfffZ")),
        [scriptblock]$RepositoryStatusProvider = {
            param([string]$Root)
            Get-RepositoryStatusSnapshot $Root
        }
    )

    if ([string]::IsNullOrWhiteSpace($SourceBuildRoot) -or
        [string]::IsNullOrWhiteSpace($DistributionTarget) -or
        [string]::IsNullOrWhiteSpace($OutputRoot)) {
        throw "STAGING_INVALID_ARGUMENT: SourceBuildRoot, DistributionTarget, and OutputRoot are required."
    }

    $repositoryFull = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $sourceFull = (Resolve-Path -LiteralPath $SourceBuildRoot).Path
    $outputFull = [IO.Path]::GetFullPath($OutputRoot)
    $statusBefore = & $RepositoryStatusProvider $repositoryFull

    Import-WindowsDistributionStagerTypes $repositoryFull
    $identity = Get-StagingSourceIdentity `
        -Root $sourceFull `
        -RequestedSourceSha $SourceSha `
        -RequestedSourceTree $SourceTree `
        -RequestedArtifactId $ArtifactId

    $request = New-Object WindowsDistributionStagingRequest
    $request.SourceBuildRoot = $sourceFull
    $request.DistributionTargetId = $DistributionTarget
    $request.OutputRoot = $outputFull
    $request.RepositoryRoot = $repositoryFull
    $request.SourceSha = $identity.SourceSha
    $request.SourceTree = $identity.SourceTree
    $request.ArtifactId = $identity.ArtifactId
    $request.RunId = $RunId
    $request.ScriptingBackend = $identity.ScriptingBackend

    $prePromotionValidation = {
        $statusAfter = & $RepositoryStatusProvider $repositoryFull
        if ($statusBefore -cne $statusAfter) {
            throw "STAGING_REPOSITORY_MUTATED: repository status changed during staging."
        }
    }.GetNewClosure()
    $request.PrePromotionValidation = [Action]$prePromotionValidation

    $result = [WindowsDistributionStager]::Stage($request)
    return $result
}

if ($env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE -ne "1") {
    try {
        $result = Invoke-WindowsDistributionStaging `
            -SourceBuildRoot $SourceBuildRoot `
            -DistributionTarget $DistributionTarget `
            -OutputRoot $OutputRoot `
            -RepositoryRoot $RepositoryRoot `
            -SourceSha $SourceSha `
            -SourceTree $SourceTree `
            -ArtifactId $ArtifactId `
            -RunId $RunId

        Write-Host "WINDOWS_DISTRIBUTION_STAGING_PASS"
        Write-Host "DistributionTarget=$($result.DistributionTargetId)"
        Write-Host "PayloadRoot=$($result.PayloadRoot)"
        Write-Host "EvidenceRoot=$($result.EvidenceRoot)"
        Write-Host "FileCount=$($result.FileCount)"
        Write-Host "TotalBytes=$($result.TotalBytes)"
        Write-Host "ManifestSha256=$($result.ManifestSha256)"
        Write-Host "DeniedArtifactCount=$($result.DeniedArtifactCount)"
        Write-Host "SteamNativeCount=$($result.SteamNativeCount)"
        Write-Host "SteamManagedCount=$($result.SteamManagedCount)"
        Write-Host "SteamAppIdCount=$($result.SteamAppIdCount)"
        exit 0
    } catch {
        Write-Error $_.Exception.Message
        exit 121
    }
}
