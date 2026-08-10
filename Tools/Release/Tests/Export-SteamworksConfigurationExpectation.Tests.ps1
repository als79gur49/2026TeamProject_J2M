$ErrorActionPreference = "Stop"
$env:VECTORQUAKE_STEAMWORKS_EXPECTATION_TEST_MODE = "1"
. (Join-Path $PSScriptRoot "..\Export-SteamworksConfigurationExpectation.ps1")

$script:Passed = 0
$script:Failed = 0
$script:Results = @()

function Invoke-Case {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $script:Passed++
        $script:Results += "PASS $Name"
    } catch {
        $script:Failed++
        $script:Results += "FAIL $Name :: $($_.Exception.Message)"
    }
}

function Assert-True {
    param($Value, [string]$Message = "Expected true.")
    if (-not $Value) { throw $Message }
}

function Assert-Throws {
    param([scriptblock]$Body, [string]$ExpectedMessage)
    try {
        & $Body
    } catch {
        if ($_.Exception.Message -cne $ExpectedMessage) {
            throw "Expected '$ExpectedMessage', got '$($_.Exception.Message)'."
        }
        return
    }
    throw "Expected '$ExpectedMessage'."
}

Invoke-Case "path containment recognizes repository children" {
    $root = [IO.Path]::GetFullPath((Join-Path $env:TEMP "j2m-repository"))
    Assert-True (Test-PathIsSameOrUnder `
        -Candidate (Join-Path $root "Artifacts\expectation") `
        -Parent $root)
}

Invoke-Case "path containment permits private sibling output" {
    $root = [IO.Path]::GetFullPath((Join-Path $env:TEMP "j2m-repository"))
    $private = [IO.Path]::GetFullPath((Join-Path $env:TEMP "j2m-evidence"))
    Assert-True (-not (Test-PathIsSameOrUnder -Candidate $private -Parent $root))
}

Invoke-Case "clean worktree provenance is accepted" {
    Assert-RepositoryIsClean -Status ""
}

Invoke-Case "dirty worktree provenance is rejected" {
    Assert-Throws {
        Assert-RepositoryIsClean -Status " M Tools/Release/example.ps1"
    } "STEAMWORKS_EXPECTATION_CLEAN_WORKTREE_REQUIRED"
}

Invoke-Case "recorded revision must remain unchanged" {
    Assert-RepositoryRevisionUnchanged `
        -ExpectedHead "head-a" `
        -ExpectedTree "tree-a" `
        -ActualHead "head-a" `
        -ActualTree "tree-a"
    Assert-Throws {
        Assert-RepositoryRevisionUnchanged `
            -ExpectedHead "head-a" `
            -ExpectedTree "tree-a" `
            -ActualHead "head-b" `
            -ActualTree "tree-b"
    } "STEAMWORKS_EXPECTATION_SOURCE_REVISION_CHANGED"
}

Invoke-Case "only the known Unity font importer mutation is restorable" {
    $fixtureRoot = Join-Path $env:TEMP ("j2m-font-guard-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    try {
        $beforePath = Join-Path $fixtureRoot "before.asset"
        $knownPath = Join-Path $fixtureRoot "known.asset"
        $concurrentPath = Join-Path $fixtureRoot "concurrent.asset"
        $before = "  m_MipmapLimitGroupName:`n    - _ScaleRatioA: 1`n    - _ScaleRatioC: 1`n"
        $known = "  m_MipmapLimitGroupName: `n    - _ScaleRatioA: 0.9`n    - _ScaleRatioC: 0.73125`n"
        [IO.File]::WriteAllText($beforePath, $before)
        [IO.File]::WriteAllText($knownPath, $known)
        [IO.File]::WriteAllText($concurrentPath, $known + "user edit`n")
        Assert-True (Test-IsKnownUnityFontImporterMutation `
            -BeforePath $beforePath -AfterPath $knownPath)
        Assert-True (-not (Test-IsKnownUnityFontImporterMutation `
            -BeforePath $beforePath -AfterPath $concurrentPath))
    } finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Invoke-Case "exclusive repository lock blocks concurrent mutation and cleans up" {
    $lockPath = Join-Path $env:TEMP ("j2m-repository-" + [Guid]::NewGuid().ToString("N") + ".lock")
    $first = Enter-ExclusiveRepositoryLock -Path $lockPath
    try {
        Assert-Throws {
            Enter-ExclusiveRepositoryLock -Path $lockPath | Out-Null
        } "STEAMWORKS_EXPECTATION_REPOSITORY_BUSY"
    } finally {
        Exit-ExclusiveRepositoryLocks -Locks @($first)
    }
    Assert-True (-not (Test-Path -LiteralPath $lockPath))
}

Invoke-Case "repository provenance remains readable while mutation locks are held" {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
    $locks = Enter-RepositoryMutationLocks -Root $repositoryRoot
    try {
        $head = Invoke-RepositoryGit $repositoryRoot @("rev-parse", "HEAD")
        $tree = Invoke-RepositoryGit $repositoryRoot @("rev-parse", "HEAD^{tree}")
        Invoke-RepositoryGit $repositoryRoot @("status", "--short") | Out-Null
        Assert-True (-not [string]::IsNullOrWhiteSpace($head))
        Assert-True (-not [string]::IsNullOrWhiteSpace($tree))
    } finally {
        Exit-ExclusiveRepositoryLocks -Locks $locks
    }
}

Invoke-Case "production wrapper contains no identity or backend mutation surface" {
    $source = Get-Content -LiteralPath `
        (Join-Path $PSScriptRoot "..\Export-SteamworksConfigurationExpectation.ps1") -Raw
    foreach ($token in @(
        "steamcmd.exe", "+login", "+run_app_build", "SetLive",
        "SteamGuard", "password", "ACH_WIN_ONE_GAME")) {
        Assert-True (-not $source.Contains($token)) "Forbidden token: $token"
    }
}

$script:Results | ForEach-Object { Write-Host $_ }
Write-Host "TOTAL=$($script:Passed + $script:Failed) PASS=$script:Passed FAIL=$script:Failed"
if ($script:Failed -ne 0) { exit 1 }
exit 0
