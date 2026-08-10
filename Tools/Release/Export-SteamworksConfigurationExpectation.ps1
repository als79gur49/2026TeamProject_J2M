[CmdletBinding()]
param(
    [string]$OutputRoot = "",
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$UnityPath = "C:\Users\user\Desktop\6000.3.11f1\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"

function Test-PathIsSameOrUnder {
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$Parent
    )

    $candidateFull = [IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/') + '\'
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + '\'
    return $candidateFull.StartsWith(
        $parentFull,
        [StringComparison]::OrdinalIgnoreCase)
}

function Convert-RepositoryPathToWsl {
    param([Parameter(Mandatory)][string]$Root)

    $output = @(& wsl.exe -e wslpath -u $Root 2>&1)
    if ($LASTEXITCODE -ne 0 -or $output.Count -eq 0) {
        throw "STEAMWORKS_EXPECTATION_WSL_PATH_FAILED: $($output -join ' ')"
    }

    return [string]$output[0]
}

function Invoke-RepositoryGit {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $gitMarker = Join-Path $Root ".git"
    $usesWslGitDir = (Test-Path -LiteralPath $gitMarker -PathType Leaf) -and
        ((Get-Content -LiteralPath $gitMarker -Raw) -match '^gitdir: /mnt/')
    if ($usesWslGitDir) {
        $wslRoot = Convert-RepositoryPathToWsl $Root
        $output = @(& wsl.exe -e git -C $wslRoot @Arguments 2>&1)
    } else {
        $output = @(& git -C $Root @Arguments 2>&1)
    }

    if ($LASTEXITCODE -ne 0) {
        throw "STEAMWORKS_EXPECTATION_GIT_FAILED: $($output -join ' ')"
    }

    return ($output -join "`n").Trim()
}

function Wait-ExpectationOutputFile {
    param([Parameter(Mandatory)][string]$Path)

    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            return $true
        }
        Start-Sleep -Milliseconds 100
    }

    return (Test-Path -LiteralPath $Path -PathType Leaf)
}

function Assert-RepositoryIsClean {
    param([AllowEmptyString()][string]$Status)

    if (-not [string]::IsNullOrWhiteSpace($Status)) {
        throw "STEAMWORKS_EXPECTATION_CLEAN_WORKTREE_REQUIRED"
    }
}

function Invoke-SteamworksConfigurationExpectationExport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$OutputRoot,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$UnityPath
    )

    if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
        throw "STEAMWORKS_EXPECTATION_OUTPUT_REQUIRED"
    }

    $repositoryFull = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $unityFull = (Resolve-Path -LiteralPath $UnityPath).Path
    $outputFull = [IO.Path]::GetFullPath($OutputRoot)
    if (Test-PathIsSameOrUnder -Candidate $outputFull -Parent $repositoryFull) {
        throw "STEAMWORKS_EXPECTATION_REPOSITORY_OUTPUT_FORBIDDEN"
    }

    $sourceHead = Invoke-RepositoryGit $repositoryFull @("rev-parse", "HEAD")
    $sourceTree = Invoke-RepositoryGit $repositoryFull @("rev-parse", "HEAD^{tree}")
    $statusBefore = Invoke-RepositoryGit $repositoryFull @("status", "--short")
    Assert-RepositoryIsClean -Status $statusBefore

    New-Item -ItemType Directory -Path $outputFull -Force | Out-Null
    $logPath = Join-Path $outputFull "unity-export.log"
    $guardRoot = Join-Path ([IO.Path]::GetTempPath()) `
        ("j2m-steamworks-expectation-guard-" + [Guid]::NewGuid().ToString("N"))
    $guardedRelativePaths = @(
        "Assets\_Shared\UI\Fonts\ClimateCrisisKR-2000 SDF.asset",
        "Assets\_Shared\UI\Fonts\NanumGothic SDF.asset"
    )
    New-Item -ItemType Directory -Path $guardRoot -Force | Out-Null
    foreach ($relativePath in $guardedRelativePaths) {
        $sourcePath = Join-Path $repositoryFull $relativePath
        $snapshotPath = Join-Path $guardRoot $relativePath
        New-Item -ItemType Directory -Path (Split-Path $snapshotPath -Parent) `
            -Force | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $snapshotPath -Force
    }

    $arguments = @(
        "-batchmode",
        "-nographics",
        "-projectPath", $repositoryFull,
        "-logFile", $logPath,
        "-executeMethod",
        "Game.Release.Steamworks.Editor.SteamworksConfigurationExpectationCli.ExportFromCommandLine",
        "-steamworksExpectationOutputDirectory", $outputFull,
        "-steamworksExpectationSourceHead", $sourceHead,
        "-steamworksExpectationSourceTree", $sourceTree
    )

    $unityExitCode = -1
    $guardedMutationRestored = $false
    try {
        & $unityFull @arguments
        $unityExitCode = $LASTEXITCODE
    } finally {
        foreach ($relativePath in $guardedRelativePaths) {
            $sourcePath = Join-Path $repositoryFull $relativePath
            $snapshotPath = Join-Path $guardRoot $relativePath
            $beforeHash = (Get-FileHash -LiteralPath $snapshotPath -Algorithm SHA256).Hash
            $afterHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
            if ($afterHash -cne $beforeHash) {
                Copy-Item -LiteralPath $snapshotPath -Destination $sourcePath -Force
                $guardedMutationRestored = $true
            }
        }
        Remove-Item -LiteralPath $guardRoot -Recurse -Force
    }

    if ($unityExitCode -ne 0) {
        throw "STEAMWORKS_EXPECTATION_UNITY_FAILED: exit=$unityExitCode log=$logPath"
    }

    $reportPath = Join-Path $outputFull "steamworks-expected-configuration.json"
    $hashPath = Join-Path $outputFull "steamworks-expected-configuration.sha256"
    if (-not (Wait-ExpectationOutputFile $reportPath) -or
        -not (Wait-ExpectationOutputFile $hashPath)) {
        throw "STEAMWORKS_EXPECTATION_OUTPUT_MISSING"
    }

    $statusAfter = Invoke-RepositoryGit $repositoryFull @("status", "--short")
    if ($statusAfter -cne $statusBefore) {
        throw "STEAMWORKS_EXPECTATION_SOURCE_MUTATED"
    }

    $hash = ((Get-Content -LiteralPath $hashPath -Raw).Trim() -split '\s+')[0]
    return [pscustomobject][ordered]@{
        ReportPath = $reportPath
        HashPath = $hashPath
        Sha256 = $hash
        SourceHead = $sourceHead
        SourceTree = $sourceTree
        SourceMutation = $false
        GuardedMutationRestored = $guardedMutationRestored
    }
}

if ($env:VECTORQUAKE_STEAMWORKS_EXPECTATION_TEST_MODE -ne "1") {
    $result = Invoke-SteamworksConfigurationExpectationExport `
        -OutputRoot $OutputRoot `
        -RepositoryRoot $RepositoryRoot `
        -UnityPath $UnityPath
    $result | Format-List
}
