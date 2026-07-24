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
    param([string]$Root)
    $metadataPath = Join-Path $Root "payload\build-metadata.json"
    $summaryPath = Join-Path $Root "payload\build-report-summary.json"
    $detailsPath = Join-Path $Root "private\build-report-details.json"
    Write-JsonFixture $detailsPath ([ordered]@{
        schemaVersion = "1.0"
        result = "Succeeded"
        totalErrors = 0
        totalWarnings = 0
        errorRecordCount = 0
        warningRecordCount = 0
        steps = @()
    })
    Write-JsonFixture $metadataPath ([ordered]@{
        schemaVersion = "2.0"
        buildResult = "Succeeded"
        errorCount = 0
        warningCount = 0
        zeroErrorGatePassed = $true
        metadataReportCountMatched = $true
        structuredErrorCountMatched = $true
    })
    Write-JsonFixture $summaryPath ([ordered]@{
        schemaVersion = "1.0"
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
    $provenanceEvidence = New-ZeroErrorEvidenceFixture (Join-Path $temp "provenance-evidence")
    Copy-Item -LiteralPath $provenanceEvidence.MetadataPath `
        -Destination (Join-Path $payload "build-metadata.json")
    Copy-Item -LiteralPath $provenanceEvidence.SummaryPath `
        -Destination (Join-Path $payload "build-report-summary.json")
    $manifest = New-PayloadManifest $payload
    $provenance = New-ArtifactProvenance -ArtifactRoot $payload `
        -RunId "run" -ArtifactId "artifact" -SourceSha "sha" -SourceTree "tree" `
        -BuildMetadataPath (Join-Path $payload "build-metadata.json") `
        -BuildReportSummaryPath (Join-Path $payload "build-report-summary.json") `
        -BuildReportDetailsPath $provenanceEvidence.DetailsPath -Manifest $manifest `
        -EntrySourceSha256 ("1" * 64) -PolicySourceSha256 ("2" * 64) `
        -WrapperSourceSha256 ("3" * 64)
    Invoke-Case "artifact provenance final binding" {
        Assert-True (Test-ArtifactProvenance -ArtifactRoot $payload `
            -BuildReportDetailsPath $provenanceEvidence.DetailsPath `
            -ExpectedSourceSha "sha" -ExpectedSourceTree "tree")
        Assert-Equal $manifest.Sha256 $provenance.payloadManifestSha256
        Assert-Equal $manifest.FileCount ([int]$provenance.payloadFileCount)
    }
    Invoke-Case "provenance hash mismatch rejection" {
        $metadataPath = Join-Path $payload "build-metadata.json"
        Add-Content -LiteralPath $metadataPath -Value " "
        Assert-False (Test-ArtifactProvenance -ArtifactRoot $payload `
            -BuildReportDetailsPath $provenanceEvidence.DetailsPath `
            -ExpectedSourceSha "sha" -ExpectedSourceTree "tree")
        $metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
        Write-JsonFixture $metadataPath $metadata
        $provenance = New-ArtifactProvenance -ArtifactRoot $payload `
            -RunId "run" -ArtifactId "artifact" -SourceSha "sha" -SourceTree "tree" `
            -BuildMetadataPath $metadataPath `
            -BuildReportSummaryPath (Join-Path $payload "build-report-summary.json") `
            -BuildReportDetailsPath $provenanceEvidence.DetailsPath -Manifest $manifest `
            -EntrySourceSha256 ("1" * 64) -PolicySourceSha256 ("2" * 64) `
            -WrapperSourceSha256 ("3" * 64)
    }
    Invoke-Case "SUCCESS consistency" {
        New-SuccessControl $payload "run" "artifact" "sha" "tree" $manifest `
            $provenance | Out-Null
        Assert-True (Test-SuccessControl $payload "sha" "tree")
    }
    Invoke-Case "SUCCESS binds artifact provenance hash" {
        $success = Get-Content (Join-Path $payload "SUCCESS.json") -Raw | ConvertFrom-Json
        Assert-Equal "artifact-provenance.json" $success.artifactProvenanceFile
        Assert-Equal (Get-Sha256 (Join-Path $payload "artifact-provenance.json")) `
            $success.artifactProvenanceSha256
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
        Assert-True ((Get-ReleaseExitCodes).BuildEvidenceFailure -ge 100)
        Assert-True (33 -lt 100)
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
if ($script:Failed -ne 0) { exit 1 }
exit 0
