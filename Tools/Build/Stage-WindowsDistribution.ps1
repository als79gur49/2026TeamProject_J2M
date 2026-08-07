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

function Import-WindowsDistributionStagerTypes {
    param([Parameter(Mandatory)][string]$Root)

    if ($null -ne ("WindowsDistributionStager" -as [type])) {
        return
    }

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

    $sourceIdentity = ($sourcePaths | ForEach-Object {
        (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    }) -join "-"
    $identityBytes = [Text.Encoding]::UTF8.GetBytes($sourceIdentity)
    $identityHash = [Security.Cryptography.SHA256]::Create()
    try {
        $cacheKey = -join ($identityHash.ComputeHash($identityBytes) |
            ForEach-Object { $_.ToString("x2") })
    } finally {
        $identityHash.Dispose()
    }

    $compileRoot = Join-Path ([IO.Path]::GetTempPath()) `
        (Join-Path "VectorQuakeDistributionStager" $cacheKey)
    $assemblyPath = Join-Path $compileRoot "bin\VectorQuake.DistributionStager.dll"
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        New-Item -ItemType Directory -Path $compileRoot -Force | Out-Null
        foreach ($sourcePath in $sourcePaths) {
            [IO.File]::Copy(
                $sourcePath,
                (Join-Path $compileRoot ([IO.Path]::GetFileName($sourcePath))),
                $true)
        }
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
        $buildOutput = @(& dotnet.exe build $projectPath `
            --configuration Release --output (Join-Path $compileRoot "bin") `
            --nologo --verbosity quiet 2>&1)
        if ($LASTEXITCODE -ne 0 -or
            -not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
            throw "STAGING_POLICY_COMPILE_FAILED: $($buildOutput -join [Environment]::NewLine)"
        }
    }

    [void][Reflection.Assembly]::LoadFrom($assemblyPath)
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
