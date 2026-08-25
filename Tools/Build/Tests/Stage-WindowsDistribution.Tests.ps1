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

function Get-ValidThirdPartyNoticeFixture {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
    return [IO.File]::ReadAllText(
        (Join-Path $repositoryRoot "ThirdPartyNotices.txt"),
        [Text.UTF8Encoding]::new($false, $true))
}

function Get-ValidUnityPlayerThirdPartyNoticeFixture {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
    return [IO.File]::ReadAllBytes(
        (Join-Path $repositoryRoot "UnityPlayerThirdPartyNotices.pdf"))
}

function New-RawFixture {
    param([string]$Root)
    New-Item -ItemType Directory -Path $Root -Force | Out-Null
    Write-FixtureFile $Root "VectorQuake.exe" "exe"
    Write-FixtureFile $Root "ThirdPartyNotices.txt" `
        (Get-ValidThirdPartyNoticeFixture)
    [IO.File]::WriteAllBytes(
        (Join-Path $Root "UnityPlayerThirdPartyNotices.pdf"),
        (Get-ValidUnityPlayerThirdPartyNoticeFixture))
    Write-FixtureFile $Root "UnityPlayer.dll" "unity"
    Write-FixtureFile $Root "VectorQuake_Data\globalgamemanagers" "managers"
    Write-FixtureFile $Root "VectorQuake_Data\Managed\Game.dll" "game-managed"
    Write-FixtureFile $Root "VectorQuake_Data\ScriptingAssemblies.json" `
        '{"names":["Game.dll"]}'
    Write-FixtureFile $Root "MonoBleedingEdge\etc\mono\config" "mono-runtime"
    Write-FixtureFile $Root `
        "VectorQuake_Data\Managed\com.rlabrecque.steamworks.net.dll" "managed"
    Write-FixtureFile $Root `
        "VectorQuake_Data\Plugins\x86_64\steam_api64.dll" "native"
    Write-FixtureFile $Root "VectorQuake_Data\TestLogs\player.log" "denied"
    Write-FixtureFile $Root "steam_appid.txt" "480"
    Write-FixtureFile $Root "build-metadata.json" `
        '{"sourceSha":"metadata-sha","sourceTree":"metadata-tree","artifactId":"metadata-artifact","scriptingBackend":"Mono2x"}'
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ("j2m-stage-wrapper-" + [Guid]::NewGuid().ToString("N"))
$rawRoot = Join-Path $fixtureRoot "raw"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path

try {
    New-RawFixture $rawRoot

    Invoke-Case "wrapper stages SteamWindows with canonical C# contracts" {
        $output = Join-Path $fixtureRoot "steam"
        $statuses = [Collections.Generic.Queue[string]]::new()
        $statuses.Enqueue("stable")
        $statuses.Enqueue("stable")
        $statusProvider = { param([string]$Root) $statuses.Dequeue() }.GetNewClosure()
        $result = Invoke-WindowsDistributionStaging `
            -SourceBuildRoot $rawRoot `
            -DistributionTarget "steam-windows" `
            -OutputRoot $output `
            -RepositoryRoot $repositoryRoot `
            -SourceSha "sha" `
            -SourceTree "tree" `
            -ArtifactId "artifact" `
            -RunId "steam-run" `
            -RepositoryStatusProvider $statusProvider
        Assert-Equal 1 $result.SteamNativeCount
        Assert-Equal 1 $result.SteamManagedCount
        Assert-Equal 0 $result.SteamAppIdCount
        Assert-True (Test-Path -LiteralPath (
            Join-Path $result.PayloadRoot "ThirdPartyNotices.txt") -PathType Leaf)
        Assert-True (Test-Path -LiteralPath (
            Join-Path $result.PayloadRoot `
                "UnityPlayerThirdPartyNotices.pdf") -PathType Leaf)
        Assert-True (Test-Path -LiteralPath $result.ManifestPath -PathType Leaf)
        Assert-True (Test-Path -LiteralPath $result.SuccessPath -PathType Leaf)
        Assert-True ((Get-Content -LiteralPath $result.SuccessPath -Raw).Contains(
            '"scriptingBackend": "Mono2x"'))
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
        Assert-True (Test-Path -LiteralPath (
            Join-Path $result.PayloadRoot "ThirdPartyNotices.txt") -PathType Leaf)
        Assert-True (Test-Path -LiteralPath (
            Join-Path $result.PayloadRoot `
                "UnityPlayerThirdPartyNotices.pdf") -PathType Leaf)
    }

    Invoke-Case "repository drift fails before final promotion" {
        $output = Join-Path $fixtureRoot "drift"
        $statuses = [Collections.Generic.Queue[string]]::new()
        $statuses.Enqueue("initial")
        $statuses.Enqueue("changed")
        $statusProvider = { param([string]$Root) $statuses.Dequeue() }.GetNewClosure()
        $threw = $false
        try {
            Invoke-WindowsDistributionStaging `
                -SourceBuildRoot $rawRoot `
                -DistributionTarget "direct-windows" `
                -OutputRoot $output `
                -RepositoryRoot $repositoryRoot `
                -RunId "drift-run" `
                -RepositoryStatusProvider $statusProvider | Out-Null
        } catch {
            $threw = $_.Exception.Message.Contains("STAGING_REPOSITORY_MUTATED")
        }
        Assert-True $threw
        Assert-True (-not (Test-Path -LiteralPath $output))
        Assert-True (-not (Test-Path -LiteralPath `
            (Join-Path $output "evidence\SUCCESS.json") -PathType Leaf))
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

    Invoke-Case "wrapper rejects an empty public notice" {
        $invalidRawRoot = Join-Path $fixtureRoot "invalid-notice-raw"
        New-RawFixture $invalidRawRoot
        Write-FixtureFile $invalidRawRoot "ThirdPartyNotices.txt" ""
        $output = Join-Path $fixtureRoot "invalid-notice-output"
        $threw = $false
        try {
            Invoke-WindowsDistributionStaging `
                -SourceBuildRoot $invalidRawRoot `
                -DistributionTarget "direct-windows" `
                -OutputRoot $output `
                -RepositoryRoot $repositoryRoot | Out-Null
        } catch {
            $threw = $_.Exception.Message.Contains(
                "STAGING_PUBLIC_NOTICE_INVALID")
        }
        Assert-True $threw
        Assert-True (-not (Test-Path -LiteralPath $output))
    }

    Invoke-Case "wrapper rejects a mutated canonical license body" {
        $invalidRawRoot = Join-Path $fixtureRoot "mutated-notice-raw"
        New-RawFixture $invalidRawRoot
        $noticePath = Join-Path $invalidRawRoot "ThirdPartyNotices.txt"
        $content = [IO.File]::ReadAllText($noticePath).Replace(
            "development of collaborative font projects",
            "development of font projects")
        [IO.File]::WriteAllText(
            $noticePath, $content, [Text.UTF8Encoding]::new($false))
        $output = Join-Path $fixtureRoot "mutated-notice-output"
        $threw = $false
        try {
            Invoke-WindowsDistributionStaging `
                -SourceBuildRoot $invalidRawRoot `
                -DistributionTarget "direct-windows" `
                -OutputRoot $output `
                -RepositoryRoot $repositoryRoot | Out-Null
        } catch {
            $threw = $_.Exception.Message.Contains(
                "STAGING_PUBLIC_NOTICE_INVALID")
        }
        Assert-True $threw
        Assert-True (-not (Test-Path -LiteralPath $output))
    }

    Invoke-Case "wrapper rejects a missing Unity Player notice" {
        $invalidRawRoot = Join-Path $fixtureRoot "missing-unity-notice-raw"
        New-RawFixture $invalidRawRoot
        [IO.File]::Delete((Join-Path `
            $invalidRawRoot "UnityPlayerThirdPartyNotices.pdf"))
        $output = Join-Path $fixtureRoot "missing-unity-notice-output"
        $threw = $false
        try {
            Invoke-WindowsDistributionStaging `
                -SourceBuildRoot $invalidRawRoot `
                -DistributionTarget "direct-windows" `
                -OutputRoot $output `
                -RepositoryRoot $repositoryRoot | Out-Null
        } catch {
            $threw = $_.Exception.Message.Contains(
                "STAGING_REQUIRED_PUBLIC_NOTICE_MISSING")
        }
        Assert-True $threw
        Assert-True (-not (Test-Path -LiteralPath $output))
    }

    Invoke-Case "wrapper rejects a mutated Unity Player notice" {
        $invalidRawRoot = Join-Path $fixtureRoot "mutated-unity-notice-raw"
        New-RawFixture $invalidRawRoot
        $noticePath = Join-Path `
            $invalidRawRoot "UnityPlayerThirdPartyNotices.pdf"
        $content = [IO.File]::ReadAllBytes($noticePath)
        $content[100] = $content[100] -bxor 1
        [IO.File]::WriteAllBytes($noticePath, $content)
        $output = Join-Path $fixtureRoot "mutated-unity-notice-output"
        $threw = $false
        try {
            Invoke-WindowsDistributionStaging `
                -SourceBuildRoot $invalidRawRoot `
                -DistributionTarget "direct-windows" `
                -OutputRoot $output `
                -RepositoryRoot $repositoryRoot | Out-Null
        } catch {
            $threw = $_.Exception.Message.Contains(
                "STAGING_PUBLIC_NOTICE_INVALID")
        }
        Assert-True $threw
        Assert-True (-not (Test-Path -LiteralPath $output))
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
