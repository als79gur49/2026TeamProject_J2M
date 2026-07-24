param(
    [string]$EvidencePath = $env:VECTORQUAKE_RELEASE_TEST_EVIDENCE_PATH
)

$script:TestStartedUtc = [DateTime]::UtcNow.ToString("o")
$script:TestCommandLine = [Environment]::CommandLine
$ErrorActionPreference = "Stop"
$env:VECTORQUAKE_RELEASE_WRAPPER_TEST_MODE = "1"
. (Join-Path $PSScriptRoot "..\Build-WindowsRelease.ps1")

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

function Assert-True { param($Value, [string]$Message = "Expected true.")
    if (-not $Value) { throw $Message }
}
function Assert-False { param($Value, [string]$Message = "Expected false.")
    if ($Value) { throw $Message }
}
function Assert-Equal { param($Expected, $Actual, [string]$Message = "")
    if ($Expected -cne $Actual) { throw "Expected '$Expected', got '$Actual'. $Message" }
}
function New-State {
    param([string[]]$Tracked = @(), [string[]]$Staged = @(), [string[]]$Untracked = @())
    return [pscustomobject]@{ Tracked = $Tracked; Staged = $Staged; Untracked = $Untracked }
}
function New-Process {
    param([int]$Id, [int]$Parent, [string]$Name, [string]$Command)
    return [pscustomobject]@{
        ProcessId = $Id; ParentProcessId = $Parent; Name = $Name; CommandLine = $Command
    }
}
function Write-JsonFixture {
    param([string]$Path, $Value)
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    $Value | ConvertTo-Json -Depth 20 |
        Set-Content -LiteralPath $Path -Encoding UTF8
}
function New-ZeroErrorEvidenceFixture {
    param(
        [string]$Root,
        [string]$RunId = "run",
        [string]$ArtifactId = "artifact",
        [string]$SourceSha = "sha",
        [string]$SourceTree = "tree",
        [string]$Configuration = "Windows-x64-NonDevelopment-Mono-RC"
    )
    $metadataPath = Join-Path $Root "payload\build-metadata.json"
    $summaryPath = Join-Path $Root "payload\build-report-summary.json"
    $detailsPath = Join-Path $Root "private\build-report-details.json"
    Write-JsonFixture $detailsPath ([ordered]@{
        schemaVersion = "1.0"
        runId = $RunId
        artifactId = $ArtifactId
        sourceSha = $SourceSha
        sourceTree = $SourceTree
        result = "Succeeded"
        totalErrors = 0
        totalWarnings = 0
        errorRecordCount = 0
        warningRecordCount = 0
        steps = @()
    })
    Write-JsonFixture $metadataPath ([ordered]@{
        schemaVersion = "2.0"
        runId = $RunId
        artifactId = $ArtifactId
        sourceSha = $SourceSha
        sourceTree = $SourceTree
        configuration = $Configuration
        entrySourceSha256 = ("1" * 64)
        policySourceSha256 = ("2" * 64)
        wrapperSourceSha256 = ("3" * 64)
        buildResult = "Succeeded"
        errorCount = 0
        warningCount = 0
        zeroErrorGatePassed = $true
        metadataReportCountMatched = $true
        structuredErrorCountMatched = $true
    })
    Write-JsonFixture $summaryPath ([ordered]@{
        schemaVersion = "2.0"
        runId = $RunId
        artifactId = $ArtifactId
        sourceSha = $SourceSha
        sourceTree = $SourceTree
        configuration = $Configuration
        result = "Succeeded"
        totalErrors = 0
        totalWarnings = 0
        detailsFile = "build-report-details.json"
        detailsSha256 = Get-Sha256 $detailsPath
        errorRecordCount = 0
        warningRecordCount = 0
        distinctErrorMessageHashes = @()
    })
    return [pscustomobject]@{
        MetadataPath = $metadataPath
        SummaryPath = $summaryPath
        DetailsPath = $detailsPath
    }
}

$approved = @(
    "TestLogs/CampaignLaunchOwnershipE2E",
    "TestLogs/MainReReview",
    "TestLogs/SceneLoadDiagnosticsQA",
    "TestLogs/UndoPreflight",
    "TestLogs/UndoPreflight-MainVfxDuplication"
)

Invoke-Case "tracked diff rejection" {
    Assert-False (Test-GitState (New-State -Tracked @("a.cs")) $approved)
}
Invoke-Case "staged diff rejection" {
    Assert-False (Test-GitState (New-State -Staged @("a.cs")) $approved)
}
Invoke-Case "approved exact TestLogs roots accepted" {
    Assert-True (Test-GitState (New-State -Untracked @(
        "TestLogs/MainReReview/a.log",
        "TestLogs/UndoPreflight"
    )) $approved)
}
Invoke-Case "new TestLogs root rejected" {
    Assert-False (Test-GitState (New-State -Untracked @("TestLogs/NewRoot/a")) $approved)
}
Invoke-Case "unknown untracked rejected" {
    Assert-False (Test-GitState (New-State -Untracked @("unknown.txt")) $approved)
}
Invoke-Case "Assets untracked rejected" {
    Assert-False (Test-GitState (New-State -Untracked @("Assets/rogue.cs")) $approved)
}
Invoke-Case "worktree-family Unity rejected" {
    Assert-False (Test-ReleaseProcessGate @(
        (New-Process 10 1 "Unity.exe" "-projectPath C:\repo")
    ) @("C:\repo"))
}
Invoke-Case "VectorQuake rejected" {
    Assert-False (Test-ReleaseProcessGate @(
        (New-Process 11 1 "VectorQuake.exe" "")
    ) @("C:\repo"))
}
Invoke-Case "unrelated attributed CrashHandler accepted" {
    Assert-True (Test-ReleaseProcessGate @(
        (New-Process 20 1 "OtherProduct.exe" "C:\other\Product.exe"),
        (New-Process 21 20 "UnityCrashHandler64.exe" "")
    ) @("C:\repo"))
}
Invoke-Case "orphan CrashHandler rejected" {
    Assert-False (Test-ReleaseProcessGate @(
        (New-Process 21 999 "UnityCrashHandler64.exe" "")
    ) @("C:\repo"))
}
Invoke-Case "allowed build Unity CrashHandler accepted" {
    Assert-True (Test-ReleaseProcessGate @(
        (New-Process 30 1 "Unity.exe" "-projectPath C:\detached"),
        (New-Process 31 30 "UnityCrashHandler64.exe" "")
    ) @("C:\repo") 30)
}
Invoke-Case "repository CrashHandler outside allowed build rejected" {
    $result = Get-ReleaseProcessGateResult @(
        (New-Process 40 1 "Unity.exe" "-projectPath C:\repo"),
        (New-Process 41 40 "UnityCrashHandler64.exe" "")
    ) @("C:\repo") 99
    Assert-False $result.Allowed
    Assert-True (@($result.RejectedProcesses |
        Where-Object { $_.reason -eq "RepositoryFamilyCrashHandler" }).Count -eq 1)
}
Invoke-Case "git command uses process-local longpaths" {
    $arguments = @(Get-GitCommandArguments "C:\repo" @("worktree", "list"))
    Assert-Equal "-c" $arguments[0]
    Assert-Equal "core.longpaths=true" $arguments[1]
    Assert-Equal "-C" $arguments[2]
    Assert-Equal "C:\repo" $arguments[3]
    Assert-Equal "worktree" $arguments[4]
}
Invoke-Case "detached git command fixes LF checkout process-locally" {
    $arguments = @(Get-GitCommandArguments "C:\repo" @("status") -DisableAutoCrlf)
    Assert-Equal "-c" $arguments[0]
    Assert-Equal "core.longpaths=true" $arguments[1]
    Assert-Equal "-c" $arguments[2]
    Assert-Equal "core.autocrlf=false" $arguments[3]
    Assert-Equal "-c" $arguments[4]
    Assert-Equal "core.eol=lf" $arguments[5]
    Assert-Equal "-C" $arguments[6]
    Assert-Equal "C:\repo" $arguments[7]
    Assert-Equal "status" $arguments[8]
}
Invoke-Case "multiple porcelain lines remain independently fail-closed" {
    $changes = Get-GitChangeClassification @(
        "?? TestLogs/MainReReview/approved.txt",
        "?? Assets/rogue.cs"
    ) @() @()
    Assert-Equal 2 (@($changes.Untracked).Count)
    Assert-Equal "TestLogs/MainReReview/approved.txt" $changes.Untracked[0]
    Assert-Equal "Assets/rogue.cs" $changes.Untracked[1]
    Assert-False (Test-GitState $changes $approved)
}
Invoke-Case "nul porcelain preserves approved path with spaces" {
    $records = @(ConvertFrom-GitPathOutput @(
        "?? TestLogs/MainReReview/unity default resources`0?? Assets/rogue.cs`0"
    ))
    $changes = Get-GitChangeClassification $records @() @()
    Assert-Equal 2 (@($changes.Untracked).Count)
    Assert-Equal "TestLogs/MainReReview/unity default resources" $changes.Untracked[0]
    Assert-Equal "Assets/rogue.cs" $changes.Untracked[1]
    Assert-False (Test-GitState $changes $approved)
}
Invoke-Case "timestamp-only tracked status is not content drift" {
    $changes = Get-GitChangeClassification @(" M ProjectSettings/ProjectSettings.asset") `
        @() @()
    Assert-Equal 0 (@($changes.Tracked).Count)
    Assert-Equal 0 (@($changes.Staged).Count)
}
Invoke-Case "content diff remains fail-closed" {
    $changes = Get-GitChangeClassification @(" M ProjectSettings/ProjectSettings.asset") `
        @("ProjectSettings/ProjectSettings.asset") @("Tools/Build/a.ps1")
    Assert-Equal "ProjectSettings/ProjectSettings.asset" $changes.Tracked[0]
    Assert-Equal "Tools/Build/a.ps1" $changes.Staged[0]
}
Invoke-Case "short detached source root satisfies URP importer path budget" {
    Assert-True (Test-BuildSourcePathBudget `
        "C:\VQBuildSources\$("a" * 40)\20260724T132225125Z")
}
Invoke-Case "legacy detached source root exceeds URP importer path budget" {
    $legacyRoot = "C:\Users\user\Documents\VectorQuake-Release-BuildSources"
    Assert-False (Test-BuildSourcePathBudget `
        "$legacyRoot\$("a" * 40)\20260724T132225125Z")
}
Invoke-Case "default detached source root is short and deterministic" {
    Assert-Equal "C:\VQBuildSources" $BuildSourceRoot
}
Invoke-Case "critical path length 259 is accepted" {
    $candidate = "C:\x"
    while ((Get-BuildSourceCriticalPathLength $candidate) -lt 259) {
        $candidate += "x"
    }
    Assert-Equal 259 (Get-BuildSourceCriticalPathLength $candidate)
    Assert-True (Test-BuildSourcePathBudget $candidate)
}
Invoke-Case "critical path length 260 is rejected" {
    $candidate = "C:\x"
    while ((Get-BuildSourceCriticalPathLength $candidate) -lt 260) {
        $candidate += "x"
    }
    Assert-Equal 260 (Get-BuildSourceCriticalPathLength $candidate)
    Assert-False (Test-BuildSourcePathBudget $candidate)
}
Invoke-Case "Unicode and space detached path uses deterministic character budget" {
    $unicodeSegment = -join @(
        [char]0xB9B4,
        [char]0xB9AC,
        [char]0xC2A4
    )
    $candidate = "C:\VQ Build Sources\$unicodeSegment\$("a" * 40)\20260724T140105269Z"
    Assert-True (Test-BuildSourcePathBudget $candidate)
    Assert-Equal (Get-BuildSourceCriticalPathLength $candidate) `
        (Get-BuildSourceCriticalPathLength $candidate)
}
Invoke-Case "SHA and RunId variations are evaluated from the complete path" {
    $first = "C:\VQBuildSources\$("a" * 40)\20260724T140105269Z"
    $second = "C:\VQBuildSources\$("f" * 40)\20301231T235959999Z"
    Assert-Equal (Get-BuildSourceCriticalPathLength $first) `
        (Get-BuildSourceCriticalPathLength $second)
    Assert-True (Test-BuildSourcePathBudget $first)
    Assert-True (Test-BuildSourcePathBudget $second)
}
Invoke-Case "oversized RunId is rejected from the complete path" {
    $candidate = "C:\VQBuildSources\$("a" * 40)\$("r" * 180)"
    Assert-False (Test-BuildSourcePathBudget $candidate)
}
Invoke-Case "longest known critical importer suffix owns the path budget" {
    $candidate = "C:\VQBuildSources\$("a" * 40)\20260724T140105269Z"
    $lengths = @($script:CriticalImporterRelativePaths | ForEach-Object {
        [IO.Path]::GetFullPath((Join-Path $candidate $_)).Length
    })
    Assert-Equal (($lengths | Measure-Object -Maximum).Maximum) `
        (Get-BuildSourceCriticalPathLength $candidate)
    Assert-True (($lengths | Sort-Object -Unique).Count -gt 1)
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ("vq-release-tests-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $stage = Join-Path $temp "stage"
    $final = Join-Path $temp "final"
    $detached = Join-Path $temp "source"
    New-Item -ItemType Directory -Path $stage | Out-Null
    Invoke-Case "destination collision rejected" {
        Assert-False (Assert-OutputPlan $stage $final $detached)
    }
    Remove-Item -LiteralPath $stage
    New-Item -ItemType Directory -Path $detached | Out-Null
    Invoke-Case "detached source collision rejected" {
        Assert-False (Assert-OutputPlan $stage $final $detached)
    }
    Remove-Item -LiteralPath $detached
    Invoke-Case "detached source dirty rejection" {
        Assert-False (Test-GitState (New-State -Untracked @("rogue")) -RequireNoUntracked)
    }
    Invoke-Case "dual-worktree snapshot equality" {
        $a = [ordered]@{ head = "a"; tree = "b"; canaries = [ordered]@{ x = "1" } }
        $b = [ordered]@{ head = "a"; tree = "b"; canaries = [ordered]@{ x = "1" } }
        Assert-True (Test-SnapshotEquality $a $b)
    }
    Invoke-Case "config canary drift detection" {
        $a = [ordered]@{ head = "a"; canaries = [ordered]@{ x = "1" } }
        $b = [ordered]@{ head = "a"; canaries = [ordered]@{ x = "2" } }
        Assert-False (Test-SnapshotEquality $a $b)
    }
    Invoke-Case "wrapper hash included" {
        $wrapper = Join-Path $PSScriptRoot "..\Build-WindowsRelease.ps1"
        Assert-True ((Get-Sha256 $wrapper) -match '^[0-9a-f]{64}$')
    }
    Invoke-Case "PowerShell test evidence is revision-bound and immutable" {
        $evidencePath = Join-Path $temp "test-evidence\powershell-tests.json"
        $wrapper = Join-Path $PSScriptRoot "..\Build-WindowsRelease.ps1"
        $written = Write-ImmutablePowerShellTestEvidence -Path $evidencePath `
            -SourceSha ("a" * 40) -SourceTree ("b" * 40) -SourceClean $true `
            -CommittedBytesMatched $true `
            -WrapperPath $wrapper -TestScriptPath $PSCommandPath `
            -CommandLine "powershell.exe -File test.ps1" `
            -StartedUtc "2026-07-25T00:00:00.0000000Z" `
            -CompletedUtc "2026-07-25T00:00:01.0000000Z" `
            -PowerShellVersion $PSVersionTable.PSVersion.ToString() `
            -Selected 2 -Passed 2 -Failed 0 -Results @("PASS one", "PASS two")
        $record = Get-Content $written.Path -Raw | ConvertFrom-Json
        Assert-Equal ("a" * 40) $record.sourceSha
        Assert-Equal ("b" * 40) $record.sourceTree
        Assert-Equal 2 ([int]$record.selected)
        Assert-Equal "Passed" $record.resultStatus
        Assert-Equal (Get-Sha256 $wrapper) $record.wrapperSha256
        Assert-Equal (Get-Sha256 $PSCommandPath) $record.testScriptSha256
        Assert-Equal "$(Get-Sha256 $written.Path)  powershell-tests.json" `
            (Get-Content $written.Sha256Path -Raw).Trim()
        $threw = $false
        try {
            Write-ImmutablePowerShellTestEvidence -Path $evidencePath `
                -SourceSha ("a" * 40) -SourceTree ("b" * 40) -SourceClean $true `
                -CommittedBytesMatched $true `
                -WrapperPath $wrapper -TestScriptPath $PSCommandPath `
                -CommandLine "powershell.exe -File test.ps1" `
                -StartedUtc "2026-07-25T00:00:00.0000000Z" `
                -CompletedUtc "2026-07-25T00:00:01.0000000Z" `
                -PowerShellVersion $PSVersionTable.PSVersion.ToString() `
                -Selected 2 -Passed 2 -Failed 0 | Out-Null
        } catch {
            $threw = $true
        }
        Assert-True $threw
        $uncommittedPath = Join-Path $temp "test-evidence\uncommitted.json"
        $threw = $false
        try {
            Write-ImmutablePowerShellTestEvidence -Path $uncommittedPath `
                -SourceSha ("a" * 40) -SourceTree ("b" * 40) -SourceClean $false `
                -CommittedBytesMatched $false `
                -WrapperPath $wrapper -TestScriptPath $PSCommandPath `
                -CommandLine "powershell.exe -File test.ps1" `
                -StartedUtc "2026-07-25T00:00:00.0000000Z" `
                -CompletedUtc "2026-07-25T00:00:01.0000000Z" `
                -PowerShellVersion $PSVersionTable.PSVersion.ToString() `
                -Selected 2 -Passed 2 -Failed 0 | Out-Null
        } catch {
            $threw = $true
        }
        Assert-True $threw
        Assert-False (Test-Path $uncommittedPath)
    }
    Invoke-Case "rejected process diagnostics are private and reasoned" {
        $diagnostics = Join-Path $temp "private\process-gate-rejection.json"
        $result = Get-ReleaseProcessGateResult @(
            (New-Process 50 1 "VectorQuake.exe" "C:\game\VectorQuake.exe")
        ) @("C:\repo")
        Write-ProcessGateDiagnostics $diagnostics "preflight" $result
        $record = Get-Content -LiteralPath $diagnostics -Raw | ConvertFrom-Json
        Assert-False $record.allowed
        Assert-Equal "preflight" $record.phase
        Assert-Equal "VectorQuakePlayerRunning" $record.rejectedProcesses[0].reason
    }

    $evidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "evidence")
    Invoke-Case "Succeeded plus zero errors is accepted" {
        $result = Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath
        Assert-True $result.Allowed
        Assert-Equal "Accepted" $result.Reason
    }
    Invoke-Case "build evidence identity mismatch is rejected" {
        $expectedIdentity = [pscustomobject]@{
            RunId = "run"
            ArtifactId = "artifact"
            SourceSha = "sha"
            SourceTree = "tree"
            Configuration = "Windows-x64-NonDevelopment-Mono-RC"
        }
        $summary = Get-Content $evidence.SummaryPath -Raw | ConvertFrom-Json
        $summary.runId = "wrong"
        Write-JsonFixture $evidence.SummaryPath $summary
        $result = Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath -ExpectedIdentity $expectedIdentity
        Assert-False $result.Allowed
        Assert-Equal "EvidenceIdentityMismatch" $result.Reason
        $summary.runId = "run"
        Write-JsonFixture $evidence.SummaryPath $summary
    }
    Invoke-Case "Succeeded plus one error is rejected" {
        $metadata = Get-Content $evidence.MetadataPath -Raw | ConvertFrom-Json
        $summary = Get-Content $evidence.SummaryPath -Raw | ConvertFrom-Json
        $details = Get-Content $evidence.DetailsPath -Raw | ConvertFrom-Json
        $metadata.errorCount = 1
        $summary.totalErrors = 1
        $summary.errorRecordCount = 1
        $details.totalErrors = 1
        $details.errorRecordCount = 1
        Write-JsonFixture $evidence.MetadataPath $metadata
        Write-JsonFixture $evidence.DetailsPath $details
        $summary.detailsSha256 = Get-Sha256 $evidence.DetailsPath
        Write-JsonFixture $evidence.SummaryPath $summary
        $result = Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath
        Assert-False $result.Allowed
        Assert-Equal "NonzeroBuildErrors" $result.Reason
    }
    $evidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "evidence-count-mismatch")
    Invoke-Case "metadata report count mismatch is rejected" {
        $metadata = Get-Content $evidence.MetadataPath -Raw | ConvertFrom-Json
        $metadata.errorCount = 1
        Write-JsonFixture $evidence.MetadataPath $metadata
        $result = Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath
        Assert-False $result.Allowed
        Assert-Equal "MetadataReportCountMismatch" $result.Reason
    }
    $evidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "warning-count-mismatch")
    Invoke-Case "metadata report warning count mismatch is rejected" {
        $metadata = Get-Content $evidence.MetadataPath -Raw | ConvertFrom-Json
        $metadata.warningCount = 1
        Write-JsonFixture $evidence.MetadataPath $metadata
        $result = Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath
        Assert-False $result.Allowed
        Assert-Equal "MetadataReportCountMismatch" $result.Reason
    }
    $evidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "missing-metadata-count")
    Invoke-Case "metadata missing errorCount is rejected" {
        $metadata = Get-Content $evidence.MetadataPath -Raw | ConvertFrom-Json
        $metadata.PSObject.Properties.Remove("errorCount")
        Write-JsonFixture $evidence.MetadataPath $metadata
        Assert-False (Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath).Allowed
    }
    $evidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "missing-report-count")
    Invoke-Case "report missing totalErrors is rejected" {
        $summary = Get-Content $evidence.SummaryPath -Raw | ConvertFrom-Json
        $summary.PSObject.Properties.Remove("totalErrors")
        Write-JsonFixture $evidence.SummaryPath $summary
        Assert-False (Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath).Allowed
    }
    $evidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "missing-details")
    Invoke-Case "details file missing is rejected" {
        Remove-Item -LiteralPath $evidence.DetailsPath
        Assert-False (Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath).Allowed
    }
    $evidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "details-hash")
    Invoke-Case "details hash mismatch is rejected" {
        Add-Content -LiteralPath $evidence.DetailsPath -Value " "
        $result = Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath
        Assert-False $result.Allowed
        Assert-Equal "DetailsHashMismatch" $result.Reason
    }
    $evidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "structured-count")
    Invoke-Case "structured record count mismatch is rejected" {
        $details = Get-Content $evidence.DetailsPath -Raw | ConvertFrom-Json
        $summary = Get-Content $evidence.SummaryPath -Raw | ConvertFrom-Json
        $details.errorRecordCount = 1
        Write-JsonFixture $evidence.DetailsPath $details
        $summary.detailsSha256 = Get-Sha256 $evidence.DetailsPath
        Write-JsonFixture $evidence.SummaryPath $summary
        $result = Test-BuildEvidence $evidence.MetadataPath $evidence.SummaryPath `
            $evidence.DetailsPath
        Assert-False $result.Allowed
        Assert-Equal "StructuredErrorCountMismatch" $result.Reason
    }
    Invoke-Case "zero-error gate decision blocks success and promotion" {
        $decision = Get-BuildEvidenceGateDecision ([pscustomobject]@{
            Allowed = $false
            Reason = "NonzeroBuildErrors"
        })
        Assert-False $decision.CreateSuccess
        Assert-False $decision.Promote
        Assert-True $decision.Quarantine
    }
    Invoke-Case "zero-error gate failure leaves SUCCESS absent" {
        $rejectedStage = Join-Path $temp "rejected-stage"
        New-Item -ItemType Directory -Path $rejectedStage | Out-Null
        $decision = Get-BuildEvidenceGateDecision ([pscustomobject]@{
            Allowed = $false; Reason = "NonzeroBuildErrors"
        })
        if ($decision.CreateSuccess) {
            Set-Content -LiteralPath (Join-Path $rejectedStage "SUCCESS.json") -Value "{}"
        }
        Assert-False (Test-Path -LiteralPath (Join-Path $rejectedStage "SUCCESS.json"))
    }
    Invoke-Case "zero-error gate failure leaves final promotion absent" {
        $rejectedFinal = Join-Path $temp "rejected-final"
        $decision = Get-BuildEvidenceGateDecision ([pscustomobject]@{
            Allowed = $false; Reason = "NonzeroBuildErrors"
        })
        Assert-False $decision.Promote
        Assert-False (Test-Path -LiteralPath $rejectedFinal)
    }
    Invoke-Case "zero-error gate failure selects quarantine" {
        $decision = Get-BuildEvidenceGateDecision ([pscustomobject]@{
            Allowed = $false; Reason = "NonzeroBuildErrors"
        })
        Assert-True $decision.Quarantine
        Assert-Equal "NonzeroBuildErrors" $decision.Reason
    }

    $payload = Join-Path $temp "payload"
    New-Item -ItemType Directory -Path (Join-Path $payload "Data") -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $payload "z.txt") -Value "z" -NoNewline
    Set-Content -LiteralPath (Join-Path $payload "A.txt") -Value "a" -NoNewline
    Set-Content -LiteralPath (Join-Path $payload "Data\b.txt") -Value "b" -NoNewline
    $manifest = New-PayloadManifest $payload
    Invoke-Case "manifest ordinal relative ordering" {
        $paths = @(Read-PayloadManifest $manifest.Path | ForEach-Object { $_.RelativePath })
        Assert-Equal "A.txt" $paths[0]
        Assert-Equal "Data/b.txt" $paths[1]
        Assert-Equal "z.txt" $paths[2]
    }
    Invoke-Case "manifest control-file exclusion" {
        $paths = @(Read-PayloadManifest $manifest.Path | ForEach-Object { $_.RelativePath })
        Assert-False ($paths -contains "files.sha256")
        Assert-False ($paths -contains "files.sha256.sha256")
    }
    Invoke-Case "missing payload detection" {
        $missingRoot = Join-Path $temp "missing"
        Copy-Item $payload $missingRoot -Recurse
        Remove-Item -LiteralPath (Join-Path $missingRoot "z.txt")
        Assert-False (Test-PayloadManifest $missingRoot)
    }
    Invoke-Case "extra payload detection" {
        $extraRoot = Join-Path $temp "extra"
        Copy-Item $payload $extraRoot -Recurse
        Set-Content -LiteralPath (Join-Path $extraRoot "extra.txt") -Value "extra"
        Assert-False (Test-PayloadManifest $extraRoot)
    }
    Invoke-Case "hash mismatch detection" {
        $mismatchRoot = Join-Path $temp "mismatch"
        Copy-Item $payload $mismatchRoot -Recurse
        Set-Content -LiteralPath (Join-Path $mismatchRoot "z.txt") -Value "changed"
        Assert-False (Test-PayloadManifest $mismatchRoot)
    }
    Invoke-Case "manifest self-hash" {
        Assert-True (Test-PayloadManifest $payload)
        Assert-Equal $manifest.Sha256 (Get-Sha256 $manifest.Path)
    }
    Invoke-Case "internal RC preserves Burst DoNotShip diagnostics" {
        $internalRoot = Join-Path $temp "internal-rc"
        $debugRoot = Join-Path $internalRoot `
            "payload\VectorQuake_BurstDebugInformation_DoNotShip\Data"
        New-Item -ItemType Directory -Path $debugRoot -Force | Out-Null
        Set-Content (Join-Path $debugRoot "lib_burst_generated.txt") `
            "C:\Users\operator\private\source.cs"
        $policy = Prepare-PayloadForAudience $internalRoot "InternalRc"
        Assert-True (Test-Path $debugRoot)
        Assert-Equal 0 (@($policy.ExcludedRelativePaths).Count)
        Assert-False $policy.PrivacyGatePassed
    }
    Invoke-Case "Store payload excludes Burst DoNotShip diagnostics" {
        $storeRoot = Join-Path $temp "store-exclusion"
        $debugRoot = Join-Path $storeRoot `
            "payload\VectorQuake_BurstDebugInformation_DoNotShip\Data"
        New-Item -ItemType Directory -Path $debugRoot -Force | Out-Null
        Set-Content (Join-Path $debugRoot "lib_burst_generated.txt") `
            "C:\Users\operator\private\source.cs"
        Set-Content (Join-Path $storeRoot "payload\readme.txt") "shareable"
        $policy = Prepare-PayloadForAudience $storeRoot "StoreDistributable"
        Assert-False (Test-Path $debugRoot)
        Assert-Equal 1 (@($policy.ExcludedRelativePaths).Count)
        Assert-True (Test-StorePayloadPrivacy $storeRoot)
        $storeManifest = New-PayloadManifest $storeRoot
        $paths = @(Read-PayloadManifest $storeManifest.Path |
            ForEach-Object { $_.RelativePath })
        Assert-False (@($paths | Where-Object {
            $_ -like "*_BurstDebugInformation_DoNotShip/*"
        }).Count -ne 0)
    }
    Invoke-Case "Store payload rejects absolute private path outside exclusion" {
        $storeRoot = Join-Path $temp "store-private-path"
        New-Item -ItemType Directory -Path $storeRoot -Force | Out-Null
        Set-Content (Join-Path $storeRoot "diagnostics.txt") `
            "compiled from C:\Users\operator\Desktop\project\source.cs"
        $threw = $false
        try {
            Prepare-PayloadForAudience $storeRoot "StoreDistributable" | Out-Null
        } catch {
            $threw = $true
        }
        Assert-True $threw
    }
    Invoke-Case "Store payload rejects detached source absolute path" {
        $storeRoot = Join-Path $temp "store-detached-path"
        New-Item -ItemType Directory -Path $storeRoot -Force | Out-Null
        Set-Content (Join-Path $storeRoot "diagnostics.txt") `
            "compiled from C:\VQBuildSources\sha\run\source.cs"
        $threw = $false
        try {
            Prepare-PayloadForAudience $storeRoot "StoreDistributable" | Out-Null
        } catch {
            $threw = $true
        }
        Assert-True $threw
    }
    Invoke-Case "Store payload permits URI schemes in shipped runtime data" {
        $storeRoot = Join-Path $temp "store-runtime-uri"
        New-Item -ItemType Directory -Path $storeRoot -Force | Out-Null
        Set-Content (Join-Path $storeRoot "browscap.ini") `
            "crawler=http://www.example.com/bot"
        $policy = Prepare-PayloadForAudience $storeRoot "StoreDistributable"
        Assert-True $policy.PrivacyGatePassed
    }
    $provenanceEvidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "provenance-evidence")
    $sourceFixture = Join-Path $temp "committed-source"
    New-Item -ItemType Directory -Path $sourceFixture -Force | Out-Null
    $entrySource = Join-Path $sourceFixture "entry.cs"
    $policySource = Join-Path $sourceFixture "policy.cs"
    $detachedWrapperSource = Join-Path $sourceFixture "detached-wrapper.ps1"
    $executingWrapperSource = Join-Path $sourceFixture "executing-wrapper.ps1"
    Set-Content $entrySource "entry" -NoNewline
    Set-Content $policySource "policy" -NoNewline
    Set-Content $detachedWrapperSource "wrapper" -NoNewline
    Set-Content $executingWrapperSource "wrapper" -NoNewline
    $expectation = New-ReleaseEvidenceExpectation -RunId "run" `
        -ArtifactId "artifact" -SourceSha "sha" -SourceTree "tree" `
        -EntrySourcePath $entrySource -PolicySourcePath $policySource `
        -DetachedWrapperSourcePath $detachedWrapperSource `
        -ExecutingWrapperSourcePath $executingWrapperSource
    $metadataFixture = Get-Content $provenanceEvidence.MetadataPath -Raw |
        ConvertFrom-Json
    $metadataFixture.entrySourceSha256 = $expectation.EntrySourceSha256
    $metadataFixture.policySourceSha256 = $expectation.PolicySourceSha256
    $metadataFixture.wrapperSourceSha256 = $expectation.WrapperSourceSha256
    Write-JsonFixture $provenanceEvidence.MetadataPath $metadataFixture
    Copy-Item -LiteralPath $provenanceEvidence.MetadataPath `
        -Destination (Join-Path $payload "build-metadata.json")
    Copy-Item -LiteralPath $provenanceEvidence.SummaryPath `
        -Destination (Join-Path $payload "build-report-summary.json")
    $manifest = New-PayloadManifest $payload
    $buildEvidence = Test-BuildEvidence `
        (Join-Path $payload "build-metadata.json") `
        (Join-Path $payload "build-report-summary.json") `
        $provenanceEvidence.DetailsPath -ExpectedIdentity $expectation
    $payloadPolicy = Prepare-PayloadForAudience $payload "InternalRc"
    $provenance = New-ArtifactProvenance -ArtifactRoot $payload `
        -RunId "run" -ArtifactId "artifact" -SourceSha "sha" -SourceTree "tree" `
        -BuildMetadataPath (Join-Path $payload "build-metadata.json") `
        -BuildReportSummaryPath (Join-Path $payload "build-report-summary.json") `
        -BuildReportDetailsPath $provenanceEvidence.DetailsPath -Manifest $manifest `
        -BuildEvidence $buildEvidence -PayloadPolicy $payloadPolicy `
        -EntrySourceSha256 $expectation.EntrySourceSha256 `
        -PolicySourceSha256 $expectation.PolicySourceSha256 `
        -WrapperSourceSha256 $expectation.WrapperSourceSha256
    Invoke-Case "artifact provenance final binding" {
        Assert-True (Test-ArtifactProvenance -ArtifactRoot $payload `
            -BuildReportDetailsPath $provenanceEvidence.DetailsPath `
            -Expectation $expectation)
        Assert-Equal $manifest.Sha256 $provenance.payloadManifestSha256
        Assert-Equal $manifest.FileCount ([int]$provenance.payloadFileCount)
    }
    Invoke-Case "provenance hash mismatch rejection" {
        $metadataPath = Join-Path $payload "build-metadata.json"
        Add-Content -LiteralPath $metadataPath -Value " "
        Assert-False (Test-ArtifactProvenance -ArtifactRoot $payload `
            -BuildReportDetailsPath $provenanceEvidence.DetailsPath `
            -Expectation $expectation)
        $metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
        Write-JsonFixture $metadataPath $metadata
        $buildEvidence = Test-BuildEvidence $metadataPath `
            (Join-Path $payload "build-report-summary.json") `
            $provenanceEvidence.DetailsPath -ExpectedIdentity $expectation
        $provenance = New-ArtifactProvenance -ArtifactRoot $payload `
            -RunId "run" -ArtifactId "artifact" -SourceSha "sha" -SourceTree "tree" `
            -BuildMetadataPath $metadataPath `
            -BuildReportSummaryPath (Join-Path $payload "build-report-summary.json") `
            -BuildReportDetailsPath $provenanceEvidence.DetailsPath -Manifest $manifest `
            -BuildEvidence $buildEvidence -PayloadPolicy $payloadPolicy `
            -EntrySourceSha256 $expectation.EntrySourceSha256 `
            -PolicySourceSha256 $expectation.PolicySourceSha256 `
            -WrapperSourceSha256 $expectation.WrapperSourceSha256
    }
    Invoke-Case "SUCCESS consistency" {
        New-SuccessControl $payload "run" "artifact" "sha" "tree" $manifest `
            $provenance $buildEvidence $payloadPolicy | Out-Null
        Assert-True (Test-SuccessControl $payload `
            $provenanceEvidence.DetailsPath $expectation)
    }
    Invoke-Case "SUCCESS binds artifact provenance hash" {
        $success = Get-Content (Join-Path $payload "SUCCESS.json") -Raw | ConvertFrom-Json
        Assert-Equal "artifact-provenance.json" $success.artifactProvenanceFile
        Assert-Equal "files.sha256" $success.payloadManifestFile
        Assert-False (Test-JsonProperty $success "payloadManifest")
        Assert-Equal (Get-Sha256 (Join-Path $payload "artifact-provenance.json")) `
            $success.artifactProvenanceSha256
    }
    Invoke-Case "SUCCESS identity mismatch is rejected" {
        $successPath = Join-Path $payload "SUCCESS.json"
        $success = Get-Content $successPath -Raw | ConvertFrom-Json
        $success.artifactId = "wrong"
        Write-JsonFixture $successPath $success
        Assert-False (Test-SuccessControl $payload `
            $provenanceEvidence.DetailsPath $expectation)
        $success.artifactId = "artifact"
        Write-JsonFixture $successPath $success
    }
    Invoke-Case "SUCCESS count mismatch is rejected" {
        $successPath = Join-Path $payload "SUCCESS.json"
        $success = Get-Content $successPath -Raw | ConvertFrom-Json
        $success.totalWarnings = 1
        Write-JsonFixture $successPath $success
        Assert-False (Test-SuccessControl $payload `
            $provenanceEvidence.DetailsPath $expectation)
        $success.totalWarnings = 0
        Write-JsonFixture $successPath $success
    }
    Invoke-Case "detached wrapper source mismatch is rejected" {
        Set-Content $executingWrapperSource "different" -NoNewline
        Assert-False (Test-ArtifactProvenance -ArtifactRoot $payload `
            -BuildReportDetailsPath $provenanceEvidence.DetailsPath `
            -Expectation $expectation)
        Set-Content $executingWrapperSource "wrapper" -NoNewline
    }
    Invoke-Case "SUCCESS-only staging is not success" {
        $stagingOnly = Join-Path $temp ".staging-run"
        New-Item -ItemType Directory -Path $stagingOnly | Out-Null
        Set-Content -LiteralPath (Join-Path $stagingOnly "SUCCESS.json") -Value "{}"
        Assert-False (Test-Path -LiteralPath (Join-Path $temp "run"))
    }
    Invoke-Case "source drift invalidates artifact" {
        Assert-False (Test-SnapshotEquality @{ head = "one" } @{ head = "two" })
    }
    Invoke-Case "Unity nonzero propagation" {
        Assert-Equal 105 (Convert-UnityExitCode 50)
        Assert-Equal 0 (Convert-UnityExitCode 0)
    }
    Invoke-Case "wrapper exit-code uniqueness" {
        $codes = @((Get-ReleaseExitCodes).Values)
        Assert-Equal $codes.Count (@($codes | Select-Object -Unique).Count)
        Assert-True (@($codes | Where-Object { $_ -lt 100 }).Count -eq 0)
    }
    Invoke-Case "failure quarantine" {
        $quarantine = Join-Path $temp "failed\run"
        Write-FailureEvidence $quarantine "manifest" 108 "sha" "run" "private.log"
        $failure = Get-Content (Join-Path $quarantine "FAILURE.json") -Raw | ConvertFrom-Json
        Assert-False $failure.deployable
        Assert-Equal 108 ([int]$failure.exitCode)
        Assert-Equal "manifest" $failure.failureStage
        Assert-Equal "sha" $failure.sourceSha
        Assert-Equal "run" $failure.runId
    }
    Invoke-Case "wrapper CSharp and report exits remain distinct" {
        Assert-Equal 105 (Convert-UnityExitCode 33)
        Assert-Equal 105 (Convert-UnityExitCode 44)
        Assert-True ((Get-ReleaseExitCodes).BuildEvidenceFailure -ge 100)
        Assert-True (33 -lt 100)
        Assert-True (44 -lt 100)
    }
    Invoke-Case "atomic promotion planning" {
        Assert-True (Assert-OutputPlan $stage $final $detached)
        Assert-Equal ([IO.Path]::GetPathRoot($stage)) ([IO.Path]::GetPathRoot($final))
    }
    Invoke-Case "existing final path overwrite rejection" {
        New-Item -ItemType Directory -Path $final | Out-Null
        Assert-False (Assert-OutputPlan $stage $final $detached)
    }
} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

$script:Results | ForEach-Object { Write-Host $_ }
Write-Host "Cases=$($script:Passed + $script:Failed) Passed=$script:Passed Failed=$script:Failed"
if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
    try {
        $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
        $sourceSha = Invoke-GitText -Root $repositoryRoot `
            -Arguments @("rev-parse", "HEAD")
        $sourceTree = Invoke-GitText -Root $repositoryRoot `
            -Arguments @("rev-parse", "HEAD^{tree}")
        $trackedOrStaged = @(Invoke-GitPathList -Root $repositoryRoot `
            -Arguments @("status", "--porcelain=v1", "-z", "-uno"))
        $committedSourceChanges = @(Invoke-GitPathList -Root $repositoryRoot `
            -Arguments @(
                "diff", "--name-only", "HEAD", "--",
                "Tools/Build/Build-WindowsRelease.ps1",
                "Tools/Build/Tests/Build-WindowsRelease.Tests.ps1"
            ))
        $wrapper = Join-Path $PSScriptRoot "..\Build-WindowsRelease.ps1"
        $written = Write-ImmutablePowerShellTestEvidence -Path $EvidencePath `
            -SourceSha $sourceSha -SourceTree $sourceTree `
            -SourceClean (@($trackedOrStaged).Count -eq 0) `
            -CommittedBytesMatched (@($committedSourceChanges).Count -eq 0) `
            -WrapperPath $wrapper -TestScriptPath $PSCommandPath `
            -CommandLine $script:TestCommandLine `
            -StartedUtc $script:TestStartedUtc `
            -CompletedUtc ([DateTime]::UtcNow.ToString("o")) `
            -PowerShellVersion $PSVersionTable.PSVersion.ToString() `
            -Selected ($script:Passed + $script:Failed) -Passed $script:Passed `
            -Failed $script:Failed -Skipped 0 -Results $script:Results
        Write-Host "Evidence=$($written.Path)"
        Write-Host "EvidenceSha256=$($written.Sha256)"
    } catch {
        Write-Error "PowerShell test evidence write failed: $($_.Exception.Message)"
        exit 2
    }
}
if ($script:Failed -ne 0) { exit 1 }
exit 0
