param([string]$PreserveSyntheticEvidenceRoot = "")

$ErrorActionPreference = "Stop"
$env:VECTORQUAKE_STEAMPIPE_DRYRUN_TEST_MODE = "1"
. (Join-Path $PSScriptRoot "..\Prepare-SteamPipeBuild.ps1")

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
        $script:Results += "FAIL $Name :: $($_.Exception.Message) :: $($_.ScriptStackTrace)"
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

function Assert-ThrowsContaining {
    param([scriptblock]$Body, [string]$Expected)
    try {
        & $Body
    } catch {
        if ($_.Exception.Message.IndexOf(
                $Expected,
                [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Expected '$Expected', got '$($_.Exception.Message)'."
        }
        return
    }
    throw "Expected '$Expected'."
}

function Write-Utf8File {
    param([string]$Path, [string]$Content)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Write-TestJson {
    param($Value, [string]$Path)
    $json = ($Value | ConvertTo-Json -Depth 12).Replace("`r`n", "`n") + "`n"
    Write-Utf8File $Path $json
}

function New-PromotedFixture {
    param(
        [string]$Root,
        [string]$Target = "steam-windows",
        [bool]$IncludeNative = $true,
        [bool]$IncludeManaged = $true,
        [bool]$IncludeSteamAppId = $false,
        [bool]$IncludeDenied = $false
    )

    $payload = Join-Path $Root "payload"
    $evidence = Join-Path $Root "evidence"
    [IO.Directory]::CreateDirectory($payload) | Out-Null
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    Write-Utf8File (Join-Path $payload "VectorQuake.exe") "exe"
    Write-Utf8File (Join-Path $payload "UnityPlayer.dll") "unity"
    Write-Utf8File (Join-Path $payload "VectorQuake_Data\globalgamemanagers") "managers"
    Write-Utf8File (Join-Path $payload "MonoBleedingEdge\etc\mono\config") "mono"
    if ($IncludeManaged) {
        Write-Utf8File `
            (Join-Path $payload "VectorQuake_Data\Managed\com.rlabrecque.steamworks.net.dll") `
            "managed"
    }
    if ($IncludeNative) {
        Write-Utf8File `
            (Join-Path $payload "VectorQuake_Data\Plugins\x86_64\steam_api64.dll") `
            "native"
    }
    if ($IncludeSteamAppId) {
        Write-Utf8File (Join-Path $payload "steam_appid.txt") "synthetic"
    }
    if ($IncludeDenied) {
        Write-Utf8File (Join-Path $payload "VectorQuake_Data\TestLogs\run.log") "denied"
    }

    $files = @(Get-ChildItem -LiteralPath $payload -File -Recurse |
        ForEach-Object {
            [ordered]@{
                relativePath = $_.FullName.Substring($payload.Length + 1).Replace('\', '/')
                size = $_.Length
                sha256 = Get-FileSha256 $_.FullName
            }
        } | Sort-Object { $_.relativePath })
    $totalBytes = [long]0
    foreach ($file in $files) {
        $totalBytes += [long]$file["size"]
    }
    $deniedCount = if ($IncludeDenied) { 1 } else { 0 }
    $provider = if ($Target -ceq "steam-windows") { "steam" } else { "local" }
    $arguments = if ($Target -ceq "steam-windows") {
        @("-j2mPlatformProvider", "steam")
    } else {
        @()
    }
    $manifest = [ordered]@{
        schemaVersion = "1.0"
        distributionTargetId = $Target
        sourceSha = "synthetic-source"
        sourceTree = "synthetic-tree"
        artifactId = "synthetic-artifact"
        runId = "synthetic-run"
        scriptingBackend = "Mono2x"
        expectedProviderId = $provider
        expectedLaunchArguments = $arguments
        deniedArtifactCount = $deniedCount
        fileCount = $files.Count
        totalBytes = $totalBytes
        files = $files
    }
    $manifestPath = Join-Path $evidence "distribution-manifest.json"
    Write-TestJson $manifest $manifestPath
    $success = [ordered]@{
        status = "SUCCESS"
        distributionTarget = $Target
        sourceSha = "synthetic-source"
        sourceTree = "synthetic-tree"
        scriptingBackend = "Mono2x"
        fileCount = $files.Count
        totalBytes = $totalBytes
        manifestSha256 = Get-FileSha256 $manifestPath
        sourceBuildImmutable = $true
        copyByteIdentity = $true
        promotedArtifactContractPassed = $true
        deniedArtifactCount = $deniedCount
    }
    Write-TestJson $success (Join-Path $evidence "SUCCESS.json")
    return $Root
}

function New-ValidArguments {
    param([string]$PromotedRoot, [string]$OutputRoot)
    return @{
        AppId = "900000001"
        DepotId = "900000002"
        PromotedSteamWindowsRoot = $PromotedRoot
        OutputRoot = $OutputRoot
        IdentityMode = "Synthetic"
        RepositoryRoot = $script:RepositoryRoot
        DryRun = $true
    }
}

function Assert-FinalOutputAbsent {
    param([string]$OutputRoot)
    Assert-True (-not (Test-Path -LiteralPath $OutputRoot))
    Assert-True (-not (Test-Path -LiteralPath `
        (Join-Path $OutputRoot "PRE_APPID_DRY_RUN_SUCCESS.json")))
}

$script:FixtureRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ("j2m-steampipe-tests-" + [Guid]::NewGuid().ToString("N"))
$script:RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
[IO.Directory]::CreateDirectory($script:FixtureRoot) | Out-Null

try {
    Invoke-Case "VDF writer and parser preserve quotes backslashes and canonical syntax" {
        $value = 'D:\Synthetic Folder\quoted"name'
        $node = New-VdfSection "Root" @((New-VdfPair "Path" $value))
        $text = ConvertTo-VdfText $node
        $parsed = ConvertFrom-VdfText $text
        Assert-Equal $value (Get-RequiredVdfPairValue $parsed "Path")
        Assert-True ($text.EndsWith("`n"))
        Assert-True (-not $text.Contains("`r"))
        Assert-ThrowsContaining { ConvertFrom-VdfText '"Root" { "A" "B"' } `
            "unbalanced braces"
    }

    Invoke-Case "VDF schema validator rejects missing duplicate and mismatched fields" {
        $contentRoot = 'D:\Synthetic promoted\payload'
        $buildOutput = 'D:\Synthetic evidence\build-output'
        $depotFile = 'depot_build_900000002.vdf'
        $valid = New-VdfSection "AppBuild" @(
            (New-VdfPair "AppID" "900000001"),
            (New-VdfPair "Desc" "SYNTHETIC_VALIDATION_ONLY"),
            (New-VdfPair "Preview" "1"),
            (New-VdfPair "ContentRoot" $contentRoot),
            (New-VdfPair "BuildOutput" $buildOutput),
            (New-VdfSection "Depots" @(
                (New-VdfPair "900000002" $depotFile)
            ))
        )
        $schemaArguments = @{
            ExpectedAppId = "900000001"
            ExpectedDepotId = "900000002"
            ExpectedDepotFile = $depotFile
            ExpectedContentRoot = $contentRoot
            ExpectedBuildOutput = $buildOutput
        }

        $missing = New-VdfSection "AppBuild" @(
            $valid.Entries | Where-Object { $_.Key -cne "AppID" })
        Assert-ThrowsContaining {
            Assert-AppVdfSchema -Root $missing @schemaArguments
        } "required pair AppID"

        $duplicate = New-VdfSection "AppBuild" @(
            @($valid.Entries) + (New-VdfPair "Preview" "1"))
        Assert-ThrowsContaining {
            Assert-AppVdfSchema -Root $duplicate @schemaArguments
        } "required pair Preview"

        $mismatch = New-VdfSection "AppBuild" @(
            $valid.Entries | ForEach-Object {
                if ($_.Key -ceq "Depots") {
                    New-VdfSection "Depots" @(
                        (New-VdfPair "900000002" "wrong-depot.vdf"))
                } else {
                    $_
                }
            })
        Assert-ThrowsContaining {
            Assert-AppVdfSchema -Root $mismatch @schemaArguments
        } "DEPOT_REFERENCE_MISMATCH"

        $depotMissingId = New-VdfSection "DepotBuild" @(
            (New-VdfSection "FileMapping" @(
                (New-VdfPair "LocalPath" "*"),
                (New-VdfPair "DepotPath" "."),
                (New-VdfPair "Recursive" "1")
            )),
            (New-VdfPair "FileExclusion" "steam_appid.txt")
        )
        Assert-ThrowsContaining {
            Assert-DepotVdfSchema -Root $depotMissingId `
                -ExpectedDepotId "900000002"
        } "required pair DepotID"
    }

    Invoke-Case "synthetic valid promoted artifact produces preview-only dry-run" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "valid-promoted")
        $output = Join-Path $script:FixtureRoot "valid-output"
        $arguments = New-ValidArguments $promoted $output
        $result = Invoke-PrepareSteamPipeBuild @arguments
        Assert-Equal "SYNTHETIC_VALIDATION_ONLY" $result.Classification
        Assert-True (Test-Path -LiteralPath $result.AppVdf -PathType Leaf)
        Assert-True (Test-Path -LiteralPath $result.DepotVdf -PathType Leaf)
        $app = Get-Content -LiteralPath $result.AppVdf -Raw
        Assert-True ($app.Contains('"Preview"    "1"'))
        Assert-True (-not $app.Contains("SetLive"))
        Assert-True ($app.Contains((ConvertTo-VdfQuoted (Join-Path $promoted "payload"))))
        Assert-True (-not $app.Contains((ConvertTo-VdfQuoted (Join-Path $promoted "evidence"))))
        $success = Get-Content `
            (Join-Path $output "PRE_APPID_DRY_RUN_SUCCESS.json") -Raw | ConvertFrom-Json
        Assert-Equal "NOT_UPLOADABLE" ([string]$success.uploadAuthority)
        Assert-Equal "NOT_ACTUAL_STEAM_IDENTITY" ([string]$success.identityAuthority)
        Assert-Equal "NO_STEAM_BACKEND_CONTACT" ([string]$success.backendContact)
        Assert-True (-not $success.networkOperation)
        Assert-True (-not $success.steamCmdExecuted)
    }

    Invoke-Case "same input and output policy produces deterministic VDF bytes" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "det-promoted")
        $output = Join-Path $script:FixtureRoot "det-output"
        $arguments = New-ValidArguments $promoted $output
        $first = Invoke-PrepareSteamPipeBuild @arguments
        $appFirst = [IO.File]::ReadAllBytes($first.AppVdf)
        $depotFirst = [IO.File]::ReadAllBytes($first.DepotVdf)
        $hashFirst = $first.VdfAggregateSha256
        Remove-Item -LiteralPath $output -Recurse -Force
        $second = Invoke-PrepareSteamPipeBuild @arguments
        Assert-True ([Linq.Enumerable]::SequenceEqual(
            [byte[]]$appFirst, [byte[]][IO.File]::ReadAllBytes($second.AppVdf)))
        Assert-True ([Linq.Enumerable]::SequenceEqual(
            [byte[]]$depotFirst, [byte[]][IO.File]::ReadAllBytes($second.DepotVdf)))
        Assert-Equal $hashFirst $second.VdfAggregateSha256
    }

    Invoke-Case "AppID invalid matrix fails closed" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "appid-promoted")
        foreach ($value in @("", "0", "-1", "not-a-number", "4294967296")) {
            $output = Join-Path $script:FixtureRoot ("appid-output-" + [Guid]::NewGuid().ToString("N"))
            $arguments = New-ValidArguments $promoted $output
            $arguments.AppId = $value
            Assert-ThrowsContaining { Invoke-PrepareSteamPipeBuild @arguments } "STEAMPIPE_ID_INVALID"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "DepotID invalid matrix fails closed" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "depotid-promoted")
        foreach ($value in @("", "0", "-1", "not-a-number", "4294967296")) {
            $output = Join-Path $script:FixtureRoot ("depotid-output-" + [Guid]::NewGuid().ToString("N"))
            $arguments = New-ValidArguments $promoted $output
            $arguments.DepotId = $value
            Assert-ThrowsContaining { Invoke-PrepareSteamPipeBuild @arguments } "STEAMPIPE_ID_INVALID"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "Actual mode rejects Spacewar AppID" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "actual-promoted")
        $output = Join-Path $script:FixtureRoot "actual-output"
        $arguments = New-ValidArguments $promoted $output
        $arguments.AppId = "480"
        $arguments.IdentityMode = "Actual"
        Assert-ThrowsContaining { Invoke-PrepareSteamPipeBuild @arguments } `
            "STEAMPIPE_SPACEWAR_APPID_REJECTED"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "raw Player input is rejected" {
        $raw = Join-Path $script:FixtureRoot "raw-player"
        Write-Utf8File (Join-Path $raw "VectorQuake.exe") "raw"
        $output = Join-Path $script:FixtureRoot "raw-output"
        $arguments = New-ValidArguments $raw $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PROMOTED_EVIDENCE_MISSING"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "DirectWindows promoted target is rejected" {
        $promoted = New-PromotedFixture `
            (Join-Path $script:FixtureRoot "direct-promoted") `
            -Target "direct-windows" -IncludeNative $false -IncludeManaged $false
        $output = Join-Path $script:FixtureRoot "direct-output"
        $arguments = New-ValidArguments $promoted $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_PROMOTED_TARGET_REJECTED"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "manifest mismatch is rejected" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "mismatch-promoted")
        Write-Utf8File (Join-Path $promoted "payload\VectorQuake.exe") "tampered"
        $output = Join-Path $script:FixtureRoot "mismatch-output"
        $arguments = New-ValidArguments $promoted $output
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STAGING_PROMOTED_MANIFEST_MISMATCH"
        Assert-FinalOutputAbsent $output
    }

    Invoke-Case "steam_appid and denied content are rejected" {
        foreach ($kind in @("appid", "denied")) {
            $promoted = New-PromotedFixture `
                (Join-Path $script:FixtureRoot ("forbidden-" + $kind)) `
                -IncludeSteamAppId ($kind -ceq "appid") `
                -IncludeDenied ($kind -ceq "denied")
            $output = Join-Path $script:FixtureRoot ("forbidden-output-" + $kind)
            $arguments = New-ValidArguments $promoted $output
            $expected = if ($kind -ceq "appid") {
                "STAGING_FORBIDDEN_ARTIFACT_PRESENT"
            } else {
                "STEAMPIPE_PROMOTED_EVIDENCE_INVALID"
            }
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } $expected
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "missing Steam native and managed bindings are rejected" {
        foreach ($kind in @("native", "managed")) {
            $promoted = New-PromotedFixture `
                (Join-Path $script:FixtureRoot ("missing-" + $kind)) `
                -IncludeNative ($kind -cne "native") `
                -IncludeManaged ($kind -cne "managed")
            $output = Join-Path $script:FixtureRoot ("missing-output-" + $kind)
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "STAGING_PROMOTED_ARTIFACT_CONTRACT_FAILED"
            Assert-FinalOutputAbsent $output
        }
    }

    Invoke-Case "repository output and content overlap are rejected" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "path-promoted")
        $repositoryOutput = Join-Path $script:RepositoryRoot "TestResults\forbidden-steampipe"
        $arguments = New-ValidArguments $promoted $repositoryOutput
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_REPOSITORY_OUTPUT_REJECTED"
        Assert-FinalOutputAbsent $repositoryOutput

        $repositoryAncestor = [IO.Path]::GetDirectoryName($script:RepositoryRoot)
        $arguments = New-ValidArguments $promoted $repositoryAncestor
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_REPOSITORY_OUTPUT_REJECTED"

        $overlap = Join-Path $promoted "payload\dry-run-output"
        $arguments = New-ValidArguments $promoted $overlap
        Assert-ThrowsContaining {
            Invoke-PrepareSteamPipeBuild @arguments
        } "STEAMPIPE_CONTENT_OUTPUT_OVERLAP_REJECTED"
        Assert-FinalOutputAbsent $overlap
    }

    Invoke-Case "SetLive execution and credential-like parameters are absent" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "surface-promoted")
        foreach ($parameter in @("SetLive", "ExecuteSteamCmd", "SteamUsername")) {
            $output = Join-Path $script:FixtureRoot ("surface-output-" + $parameter)
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                if ($parameter -ceq "SetLive") {
                    Invoke-PrepareSteamPipeBuild @arguments -SetLive "branch"
                } elseif ($parameter -ceq "ExecuteSteamCmd") {
                    Invoke-PrepareSteamPipeBuild @arguments -ExecuteSteamCmd
                } else {
                    Invoke-PrepareSteamPipeBuild @arguments -SteamUsername "account"
                }
            } $parameter
            Assert-FinalOutputAbsent $output
        }

        $malicious = ConvertFrom-VdfText @'
"AppBuild"
{
    "AppID" "900000001"
    "SetLive" "branch"
}
'@
        Assert-ThrowsContaining { Assert-VdfForbiddenSurfaceZero $malicious } `
            "STEAMPIPE_VDF_FORBIDDEN_KEY"
    }

    Invoke-Case "failure cleans preparing directory and leaves no final marker" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "atomic-fail-promoted")
        $output = Join-Path $script:FixtureRoot "atomic-fail-output"
        $original = (Get-Command Write-DeterministicJson).ScriptBlock
        try {
            Set-Item Function:\Write-DeterministicJson {
                throw "SYNTHETIC_WRITE_FAILURE"
            }
            $arguments = New-ValidArguments $promoted $output
            Assert-ThrowsContaining {
                Invoke-PrepareSteamPipeBuild @arguments
            } "SYNTHETIC_WRITE_FAILURE"
        } finally {
            Set-Item Function:\Write-DeterministicJson $original
        }
        Assert-FinalOutputAbsent $output
        $preparing = @(Get-ChildItem -LiteralPath $script:FixtureRoot -Directory |
            Where-Object { $_.Name.StartsWith(".preparing-atomic-fail-output-") })
        Assert-Equal 0 $preparing.Count
    }

    Invoke-Case "success atomically promotes only final output" {
        $promoted = New-PromotedFixture (Join-Path $script:FixtureRoot "atomic-promoted")
        $output = Join-Path $script:FixtureRoot "atomic-output"
        $arguments = New-ValidArguments $promoted $output
        $result = Invoke-PrepareSteamPipeBuild @arguments
        Assert-True (Test-Path -LiteralPath $result.OutputRoot -PathType Container)
        Assert-True (Test-Path -LiteralPath `
            (Join-Path $output "PRE_APPID_DRY_RUN_SUCCESS.json") -PathType Leaf)
        $preparing = @(Get-ChildItem -LiteralPath $script:FixtureRoot -Directory |
            Where-Object { $_.Name.StartsWith(".preparing-atomic-output-") })
        Assert-Equal 0 $preparing.Count
    }

    Invoke-Case "production source has no process network or identity hardcode path" {
        $source = Get-Content -LiteralPath `
            (Join-Path $PSScriptRoot "..\Prepare-SteamPipeBuild.ps1") -Raw
        foreach ($token in @(
            "Start-Process steamcmd", "& steamcmd", "+login", "+run_app_build",
            "Invoke-WebRequest", "Invoke-RestMethod", "System.Net.WebClient")) {
            Assert-True (-not $source.Contains($token)) "Forbidden token: $token"
        }
        Assert-True (-not $source.Contains('AppId = "480"'))
        Assert-True ($source -notmatch '\[string\]\$DepotId\s*=\s*"[1-9]')
    }

    if ($script:Failed -eq 0 -and
        -not [string]::IsNullOrWhiteSpace($PreserveSyntheticEvidenceRoot)) {
        if (Test-Path -LiteralPath $PreserveSyntheticEvidenceRoot) {
            throw "Synthetic evidence root must be fresh: $PreserveSyntheticEvidenceRoot"
        }
        $promoted = New-PromotedFixture `
            (Join-Path $PreserveSyntheticEvidenceRoot "synthetic-promoted-fixture")
        $output = Join-Path $PreserveSyntheticEvidenceRoot "dry-run"
        $arguments = New-ValidArguments $promoted $output
        $result = Invoke-PrepareSteamPipeBuild @arguments
        Write-Host "SYNTHETIC_EVIDENCE_ROOT=$PreserveSyntheticEvidenceRoot"
        Write-Host "SYNTHETIC_VDF_AGGREGATE_SHA256=$($result.VdfAggregateSha256)"
    }
} finally {
    if (Test-Path -LiteralPath $script:FixtureRoot -PathType Container) {
        Remove-Item -LiteralPath $script:FixtureRoot -Recurse -Force
    }
}

$script:Results | ForEach-Object { Write-Host $_ }
Write-Host "TOTAL=$($script:Passed + $script:Failed) PASS=$script:Passed FAIL=$script:Failed"
if ($script:Failed -ne 0) { exit 1 }
exit 0
