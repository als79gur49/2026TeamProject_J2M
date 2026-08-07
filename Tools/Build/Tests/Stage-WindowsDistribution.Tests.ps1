$ErrorActionPreference = "Stop"
$env:VECTORQUAKE_DISTRIBUTION_STAGER_TEST_MODE = "1"
. (Join-Path $PSScriptRoot "..\Stage-WindowsDistribution.ps1")

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

function Assert-Equal {
    param($Expected, $Actual, [string]$Message = "")
    if ($Expected -cne $Actual) {
        throw "Expected '$Expected', got '$Actual'. $Message"
    }
}

function Write-FixtureFile {
    param([string]$Root, [string]$RelativePath, [string]$Content)
    $path = Join-Path $Root $RelativePath
    New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($path, $Content, [Text.UTF8Encoding]::new($false))
}

function New-RawFixture {
    param([string]$Root)
    New-Item -ItemType Directory -Path $Root -Force | Out-Null
    Write-FixtureFile $Root "VectorQuake.exe" "exe"
    Write-FixtureFile $Root "UnityPlayer.dll" "unity"
    Write-FixtureFile $Root "VectorQuake_Data\globalgamemanagers" "managers"
    Write-FixtureFile $Root `
        "VectorQuake_Data\Managed\com.rlabrecque.steamworks.net.dll" "managed"
    Write-FixtureFile $Root `
        "VectorQuake_Data\Plugins\x86_64\steam_api64.dll" "native"
    Write-FixtureFile $Root "VectorQuake_Data\TestLogs\player.log" "denied"
    Write-FixtureFile $Root "steam_appid.txt" "480"
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ("j2m-stage-wrapper-" + [Guid]::NewGuid().ToString("N"))
$rawRoot = Join-Path $fixtureRoot "raw"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path

try {
    New-RawFixture $rawRoot

    Invoke-Case "wrapper stages SteamWindows with canonical C# contracts" {
        $output = Join-Path $fixtureRoot "steam"
        $result = Invoke-WindowsDistributionStaging `
            -SourceBuildRoot $rawRoot `
            -DistributionTarget "steam-windows" `
            -OutputRoot $output `
            -RepositoryRoot $repositoryRoot `
            -SourceSha "sha" `
            -SourceTree "tree" `
            -ArtifactId "artifact" `
            -RunId "steam-run"
        Assert-Equal 1 $result.SteamNativeCount
        Assert-Equal 1 $result.SteamManagedCount
        Assert-Equal 0 $result.SteamAppIdCount
        Assert-True (Test-Path -LiteralPath $result.ManifestPath -PathType Leaf)
        Assert-True (Test-Path -LiteralPath $result.SuccessPath -PathType Leaf)
    }

    Invoke-Case "wrapper stages DirectWindows without Steam dependencies" {
        $result = Invoke-WindowsDistributionStaging `
            -SourceBuildRoot $rawRoot `
            -DistributionTarget "direct-windows" `
            -OutputRoot (Join-Path $fixtureRoot "direct") `
            -RepositoryRoot $repositoryRoot `
            -RunId "direct-run"
        Assert-Equal 0 $result.SteamNativeCount
        Assert-Equal 0 $result.SteamManagedCount
        Assert-Equal 0 $result.SteamAppIdCount
        Assert-Equal 0 $result.DeniedArtifactCount
    }

    Invoke-Case "wrapper rejects unknown target" {
        $threw = $false
        try {
            Invoke-WindowsDistributionStaging `
                -SourceBuildRoot $rawRoot `
                -DistributionTarget "unknown" `
                -OutputRoot (Join-Path $fixtureRoot "unknown") `
                -RepositoryRoot $repositoryRoot | Out-Null
        } catch {
            $threw = $_.Exception.Message.Contains(
                "STAGING_UNKNOWN_DISTRIBUTION_TARGET")
        }
        Assert-True $threw
    }

    Invoke-Case "wrapper rejects missing target" {
        $threw = $false
        try {
            Invoke-WindowsDistributionStaging `
                -SourceBuildRoot $rawRoot `
                -DistributionTarget "" `
                -OutputRoot (Join-Path $fixtureRoot "missing") `
                -RepositoryRoot $repositoryRoot | Out-Null
        } catch {
            $threw = $true
        }
        Assert-True $threw
    }
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

$script:Results | ForEach-Object { Write-Host $_ }
Write-Host "TOTAL=$($script:Passed + $script:Failed) PASS=$script:Passed FAIL=$script:Failed"
if ($script:Failed -ne 0) { exit 1 }
exit 0
