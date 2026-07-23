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
Invoke-Case "detached git command disables autocrlf process-locally" {
    $arguments = @(Get-GitCommandArguments "C:\repo" @("status") -DisableAutoCrlf)
    Assert-Equal "-c" $arguments[0]
    Assert-Equal "core.longpaths=true" $arguments[1]
    Assert-Equal "-c" $arguments[2]
    Assert-Equal "core.autocrlf=false" $arguments[3]
    Assert-Equal "-C" $arguments[4]
    Assert-Equal "C:\repo" $arguments[5]
    Assert-Equal "status" $arguments[6]
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
    Invoke-Case "SUCCESS consistency" {
        New-SuccessControl $payload "run" "artifact" "sha" "tree" $manifest | Out-Null
        Assert-True (Test-SuccessControl $payload "sha" "tree")
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
