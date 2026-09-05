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

function Convert-GitPathToNative {
    param([Parameter(Mandatory)][string]$Path)

    if ($Path.StartsWith("/mnt/", [StringComparison]::Ordinal)) {
        $output = @(& wsl.exe -e wslpath -w $Path 2>&1)
        if ($LASTEXITCODE -ne 0 -or $output.Count -eq 0) {
            throw "STEAMWORKS_EXPECTATION_GIT_LOCK_PATH_FAILED: $($output -join ' ')"
        }
        return [string]$output[0]
    }

    return [IO.Path]::GetFullPath($Path)
}

function Invoke-RepositoryGit {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0)
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

    if ($AllowedExitCodes -notcontains $LASTEXITCODE) {
        throw "STEAMWORKS_EXPECTATION_GIT_FAILED: $($output -join ' ')"
    }

    return ($output -join "`n").Trim()
}

function Get-HeadLockGitPath {
    param([AllowEmptyString()][string]$HeadReference)

    if ([string]::IsNullOrWhiteSpace($HeadReference)) {
        return "HEAD.lock"
    }

    return $HeadReference + ".lock"
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

function Assert-RepositoryRevisionUnchanged {
    param(
        [Parameter(Mandatory)][string]$ExpectedHead,
        [Parameter(Mandatory)][string]$ExpectedTree,
        [Parameter(Mandatory)][string]$ActualHead,
        [Parameter(Mandatory)][string]$ActualTree
    )

    if ($ActualHead -cne $ExpectedHead -or $ActualTree -cne $ExpectedTree) {
        throw "STEAMWORKS_EXPECTATION_SOURCE_REVISION_CHANGED"
    }
}

function Test-FontAssetBytesUnchanged {
    param(
        [Parameter(Mandatory)][string]$BeforePath,
        [Parameter(Mandatory)][string]$AfterPath
    )

    if (-not (Test-Path -LiteralPath $AfterPath -PathType Leaf)) {
        return $false
    }
    return (Get-FileHash -LiteralPath $BeforePath -Algorithm SHA256).Hash -ceq `
        (Get-FileHash -LiteralPath $AfterPath -Algorithm SHA256).Hash
}

function Enter-ExclusiveRepositoryLock {
    param([Parameter(Mandatory)][string]$Path)

    try {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) |
            Out-Null
        return [IO.File]::Open(
            $Path,
            [IO.FileMode]::CreateNew,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
    } catch {
        throw "STEAMWORKS_EXPECTATION_REPOSITORY_BUSY"
    }
}

function Exit-ExclusiveRepositoryLocks {
    param([object[]]$Locks)

    $lockArray = @($Locks)
    for ($index = $lockArray.Count - 1; $index -ge 0; $index--) {
        $lock = $lockArray[$index]
        if ($null -eq $lock) { continue }
        $path = $lock.Name
        $lock.Dispose()
        Remove-Item -LiteralPath $path -Force
    }
}

function Enter-SharedReadFileLocks {
    param([Parameter(Mandatory)][string[]]$Paths)

    $locks = @()
    try {
        foreach ($path in $Paths) {
            $locks += [IO.File]::Open(
                $path,
                [IO.FileMode]::Open,
                [IO.FileAccess]::Read,
                [IO.FileShare]::Read)
        }
        return $locks
    } catch {
        Exit-SharedReadFileLocks -Locks $locks
        throw "STEAMWORKS_EXPECTATION_SOURCE_LOCK_FAILED: $path"
    }
}

function Exit-SharedReadFileLocks {
    param([object[]]$Locks)

    foreach ($lock in @($Locks)) {
        if ($null -ne $lock) { $lock.Dispose() }
    }
}

function Enter-TrackedSourceReadLocks {
    param(
        [Parameter(Mandatory)][string]$Root,
        [string[]]$ExcludedRelativePaths = @()
    )

    $excluded = @{}
    foreach ($relativePath in $ExcludedRelativePaths) {
        $key = $relativePath.Replace('\', '/').TrimStart('/').ToLowerInvariant()
        $excluded[$key] = $true
    }

    $paths = @()
    $tracked = Invoke-RepositoryGit $Root @(
        "-c", "core.quotePath=false", "ls-files")
    foreach ($relativePath in ($tracked -split "`n")) {
        if ([string]::IsNullOrWhiteSpace($relativePath)) { continue }
        $key = $relativePath.Replace('\', '/').TrimStart('/').ToLowerInvariant()
        if ($excluded.ContainsKey($key)) { continue }

        $fullPath = Join-Path $Root $relativePath
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $paths += $fullPath
        }
    }

    return Enter-SharedReadFileLocks -Paths $paths
}

function Enter-RepositoryMutationLocks {
    param([Parameter(Mandatory)][string]$Root)

    $headReference = Invoke-RepositoryGit `
        -Root $Root `
        -Arguments @("symbolic-ref", "-q", "HEAD") `
        -AllowedExitCodes @(0, 1)
    $indexLockPath = Invoke-RepositoryGit $Root @(
        "rev-parse", "--path-format=absolute", "--git-path", "index.lock")
    $headLockPath = Invoke-RepositoryGit $Root @(
        "rev-parse", "--path-format=absolute", "--git-path", `
        (Get-HeadLockGitPath -HeadReference $headReference))
    $locks = @()
    try {
        $locks += Enter-ExclusiveRepositoryLock `
            -Path (Convert-GitPathToNative $indexLockPath)
        $locks += Enter-ExclusiveRepositoryLock `
            -Path (Convert-GitPathToNative $headLockPath)
        return $locks
    } catch {
        Exit-ExclusiveRepositoryLocks -Locks $locks
        throw
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

    $statusProbe = Invoke-RepositoryGit $repositoryFull @("status", "--short")
    Assert-RepositoryIsClean -Status $statusProbe
    $guardedRelativePaths = @(
        "Assets\_Shared\UI\Fonts\KBODiaGothic-Medium SDF.asset",
        "Assets\_Shared\UI\Fonts\KBODiaGothic-Light SDF.asset"
    )
    $repositoryLocks = Enter-RepositoryMutationLocks -Root $repositoryFull
    $sourceReadLocks = @()
    try {
        $sourceReadLocks = Enter-TrackedSourceReadLocks `
            -Root $repositoryFull `
            -ExcludedRelativePaths $guardedRelativePaths
        $sourceHead = Invoke-RepositoryGit $repositoryFull @("rev-parse", "HEAD")
        $sourceTree = Invoke-RepositoryGit $repositoryFull @("rev-parse", "HEAD^{tree}")
        $statusBefore = Invoke-RepositoryGit $repositoryFull @("status", "--short")
        Assert-RepositoryIsClean -Status $statusBefore

    New-Item -ItemType Directory -Path $outputFull -Force | Out-Null
    $logPath = Join-Path $outputFull "unity-export.log"
    $guardRoot = Join-Path ([IO.Path]::GetTempPath()) `
        ("j2m-steamworks-expectation-guard-" + [Guid]::NewGuid().ToString("N"))
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
    $unexpectedGuardedMutation = ""
    try {
        & $unityFull @arguments
        $unityExitCode = $LASTEXITCODE
    } finally {
        foreach ($relativePath in $guardedRelativePaths) {
            $sourcePath = Join-Path $repositoryFull $relativePath
            $snapshotPath = Join-Path $guardRoot $relativePath
            if (-not (Test-FontAssetBytesUnchanged `
                    -BeforePath $snapshotPath `
                    -AfterPath $sourcePath)) {
                $unexpectedGuardedMutation = $sourcePath
            }
        }
        Remove-Item -LiteralPath $guardRoot -Recurse -Force
    }

    if (-not [string]::IsNullOrWhiteSpace($unexpectedGuardedMutation)) {
        throw "STEAMWORKS_EXPECTATION_UNEXPECTED_GUARDED_MUTATION: $unexpectedGuardedMutation"
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
    $sourceHeadAfter = Invoke-RepositoryGit $repositoryFull @("rev-parse", "HEAD")
    $sourceTreeAfter = Invoke-RepositoryGit $repositoryFull @("rev-parse", "HEAD^{tree}")
    Assert-RepositoryRevisionUnchanged `
        -ExpectedHead $sourceHead `
        -ExpectedTree $sourceTree `
        -ActualHead $sourceHeadAfter `
        -ActualTree $sourceTreeAfter

    $hash = ((Get-Content -LiteralPath $hashPath -Raw).Trim() -split '\s+')[0]
        $result = [pscustomobject][ordered]@{
            ReportPath = $reportPath
            HashPath = $hashPath
            Sha256 = $hash
            SourceHead = $sourceHead
            SourceTree = $sourceTree
            SourceMutation = $false
            GuardedMutationRestored = $guardedMutationRestored
        }
    } finally {
        Exit-SharedReadFileLocks -Locks $sourceReadLocks
        Exit-ExclusiveRepositoryLocks -Locks $repositoryLocks
    }

    return $result
}

if ($env:VECTORQUAKE_STEAMWORKS_EXPECTATION_TEST_MODE -ne "1") {
    $result = Invoke-SteamworksConfigurationExpectationExport `
        -OutputRoot $OutputRoot `
        -RepositoryRoot $RepositoryRoot `
        -UnityPath $UnityPath
    $result | Format-List
}
