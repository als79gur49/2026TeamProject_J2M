[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$UnityExe = "C:\Users\user\Desktop\6000.3.11f1\Editor\Unity.exe",
    [string]$OutputRoot = "C:\Users\user\Documents\VectorQuake-Release-Builds",
    [string]$BuildSourceRoot = "C:\Users\user\Documents\VectorQuake-Release-BuildSources",
    [string]$RunId = ([DateTime]::UtcNow.ToString("yyyyMMddTHHmmssfffZ")),
    [string[]]$AllowUntrackedRoot = @(
        "TestLogs/CampaignLaunchOwnershipE2E",
        "TestLogs/MainReReview",
        "TestLogs/SceneLoadDiagnosticsQA",
        "TestLogs/UndoPreflight",
        "TestLogs/UndoPreflight-MainVfxDuplication"
    )
)

$script:ReleaseExitCodes = [ordered]@{
    GitPreflightFailure = 100
    UnknownUntrackedFailure = 101
    ProcessGateFailure = 102
    OutputCollision = 103
    DetachedSourceFailure = 104
    UnityInvocationFailure = 105
    SourceDriftDetected = 106
    ManifestGenerationFailure = 107
    ManifestVerificationFailure = 108
    ControlFileConsistencyFailure = 109
    PromotionFailure = 110
    ArtifactQuarantined = 111
    WrapperInternalError = 112
}
$script:ConfigurationName = "Windows-x64-NonDevelopment-Mono-RC"
$script:ConfigurationPathName = "Windows-x64-NonDevelopment-Mono"
$script:MetadataSchemaVersion = "1.0"
$script:ControlFileNames = @("files.sha256", "files.sha256.sha256", "SUCCESS.json")

function Get-ReleaseExitCodes { return $script:ReleaseExitCodes }

function Get-NormalizedRelativePath {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Path)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [IO.Path]::GetFullPath($Path)
    if (-not $pathFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside root: $Path"
    }
    return $pathFull.Substring($rootFull.Length + 1).Replace('\', '/')
}

function Test-ApprovedUntrackedPaths {
    param([string[]]$Paths, [string[]]$AllowedRoots)
    foreach ($pathValue in @($Paths)) {
        $normalized = $pathValue.Replace('\', '/').TrimEnd('/')
        $accepted = $false
        foreach ($root in @($AllowedRoots)) {
            $allowed = $root.Replace('\', '/').TrimEnd('/')
            if ($normalized -eq $allowed -or
                $normalized.StartsWith($allowed + '/', [StringComparison]::Ordinal)) {
                $accepted = $true
                break
            }
        }
        if (-not $accepted) { return $false }
    }
    return $true
}

function Test-GitState {
    param(
        [Parameter(Mandatory)]$Snapshot,
        [string[]]$AllowedRoots = @(),
        [switch]$RequireNoUntracked
    )
    if (@($Snapshot.Tracked).Count -ne 0 -or @($Snapshot.Staged).Count -ne 0) {
        return $false
    }
    if ($RequireNoUntracked) { return @($Snapshot.Untracked).Count -eq 0 }
    return Test-ApprovedUntrackedPaths -Paths @($Snapshot.Untracked) -AllowedRoots $AllowedRoots
}

function Test-ReleaseProcessGate {
    param(
        [object[]]$Processes,
        [string[]]$RepositoryFamilyPaths,
        [Nullable[int]]$AllowedUnityPid = $null
    )
    $byId = @{}
    foreach ($process in @($Processes)) { $byId[[int]$process.ProcessId] = $process }
    foreach ($process in @($Processes)) {
        $name = [string]$process.Name
        if ($name -ieq "VectorQuake.exe") { return $false }
        if ($name -ieq "Unity.exe") {
            if ($AllowedUnityPid.HasValue -and
                [int]$process.ProcessId -eq $AllowedUnityPid.Value) { continue }
            $command = [string]$process.CommandLine
            $attributed = $false
            foreach ($familyPath in @($RepositoryFamilyPaths)) {
                if ($command.IndexOf($familyPath, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $attributed = $true
                    break
                }
            }
            # Repository-family and unattributed Unity are both fail-closed.
            if ($attributed -or [string]::IsNullOrWhiteSpace($command)) { return $false }
            return $false
        }
        if ($name -ieq "UnityCrashHandler64.exe") {
            $parentId = [int]$process.ParentProcessId
            if (-not $byId.ContainsKey($parentId)) { return $false }
            $parent = $byId[$parentId]
            if ([string]$parent.Name -ieq "Unity.exe") {
                $parentCommand = [string]$parent.CommandLine
                foreach ($familyPath in @($RepositoryFamilyPaths)) {
                    if ($parentCommand.IndexOf(
                            $familyPath, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                        return $false
                    }
                }
            }
        }
    }
    return $true
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-PayloadFiles {
    param([Parameter(Mandatory)][string]$PayloadRoot)
    $items = @(Get-ChildItem -LiteralPath $PayloadRoot -File -Recurse |
        Where-Object { $script:ControlFileNames -notcontains $_.Name } |
        ForEach-Object {
            [pscustomobject]@{
                FullName = $_.FullName
                RelativePath = Get-NormalizedRelativePath -Root $PayloadRoot -Path $_.FullName
            }
        })
    $paths = [string[]]@($items | ForEach-Object { $_.RelativePath })
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $byPath = @{}
    foreach ($item in $items) { $byPath[$item.RelativePath] = $item }
    return @($paths | ForEach-Object { $byPath[$_] })
}

function New-PayloadManifest {
    param([Parameter(Mandatory)][string]$PayloadRoot)
    $manifestPath = Join-Path $PayloadRoot "files.sha256"
    $lines = @(Get-PayloadFiles -PayloadRoot $PayloadRoot | ForEach-Object {
        "$(Get-Sha256 -Path $_.FullName)  $($_.RelativePath)"
    })
    [IO.File]::WriteAllLines(
        $manifestPath, $lines, [Text.UTF8Encoding]::new($false))
    $selfHash = Get-Sha256 -Path $manifestPath
    [IO.File]::WriteAllText(
        (Join-Path $PayloadRoot "files.sha256.sha256"),
        "$selfHash  files.sha256`n",
        [Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{
        Path = $manifestPath
        Sha256 = $selfHash
        FileCount = $lines.Count
    }
}

function Read-PayloadManifest {
    param([Parameter(Mandatory)][string]$ManifestPath)
    $entries = @()
    foreach ($line in @(Get-Content -LiteralPath $ManifestPath)) {
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') {
            throw "Malformed manifest line: $line"
        }
        $entries += [pscustomobject]@{
            Hash = $Matches[1].ToLowerInvariant()
            RelativePath = $Matches[2]
        }
    }
    return $entries
}

function Test-PayloadManifest {
    param([Parameter(Mandatory)][string]$PayloadRoot)
    $manifestPath = Join-Path $PayloadRoot "files.sha256"
    $selfPath = Join-Path $PayloadRoot "files.sha256.sha256"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $selfPath -PathType Leaf)) { return $false }
    try { $entries = @(Read-PayloadManifest -ManifestPath $manifestPath) }
    catch { return $false }
    $actual = @(Get-PayloadFiles -PayloadRoot $PayloadRoot)
    $expectedPaths = @($entries | ForEach-Object { $_.RelativePath })
    $actualPaths = @($actual | ForEach-Object { $_.RelativePath })
    if ((Compare-Object $expectedPaths $actualPaths).Count -ne 0) { return $false }
    $ordinalSorted = [string[]]@($expectedPaths)
    [Array]::Sort($ordinalSorted, [StringComparer]::Ordinal)
    if (($expectedPaths -join "`n") -cne ($ordinalSorted -join "`n")) { return $false }
    foreach ($entry in $entries) {
        $candidate = Join-Path $PayloadRoot $entry.RelativePath.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { return $false }
        if ((Get-Sha256 -Path $candidate) -cne $entry.Hash) { return $false }
    }
    $selfLine = (Get-Content -LiteralPath $selfPath -Raw).Trim()
    return $selfLine -ceq "$(Get-Sha256 -Path $manifestPath)  files.sha256"
}

function New-SuccessControl {
    param(
        [Parameter(Mandatory)][string]$PayloadRoot,
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][string]$ArtifactId,
        [Parameter(Mandatory)][string]$SourceSha,
        [Parameter(Mandatory)][string]$SourceTree,
        [Parameter(Mandatory)]$Manifest
    )
    $control = [ordered]@{
        schemaVersion = $script:MetadataSchemaVersion
        runId = $RunId
        artifactId = $ArtifactId
        sourceSha = $SourceSha
        sourceTree = $SourceTree
        configuration = $script:ConfigurationName
        payloadManifest = "files.sha256"
        payloadManifestSha256 = $Manifest.Sha256
        payloadFileCount = [int]$Manifest.FileCount
        promotionReady = $true
        createdUtc = [DateTime]::UtcNow.ToString("o")
    }
    $control | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $PayloadRoot "SUCCESS.json") -Encoding UTF8
    return $control
}

function Test-SuccessControl {
    param(
        [Parameter(Mandatory)][string]$PayloadRoot,
        [Parameter(Mandatory)][string]$ExpectedSourceSha,
        [Parameter(Mandatory)][string]$ExpectedSourceTree
    )
    $successPath = Join-Path $PayloadRoot "SUCCESS.json"
    if (-not (Test-Path -LiteralPath $successPath -PathType Leaf)) { return $false }
    try { $success = Get-Content -LiteralPath $successPath -Raw | ConvertFrom-Json }
    catch { return $false }
    $manifestPath = Join-Path $PayloadRoot ([string]$success.payloadManifest)
    if ($success.schemaVersion -cne $script:MetadataSchemaVersion -or
        -not $success.promotionReady -or
        $success.sourceSha -cne $ExpectedSourceSha -or
        $success.sourceTree -cne $ExpectedSourceTree -or
        -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { return $false }
    try { $entries = @(Read-PayloadManifest -ManifestPath $manifestPath) }
    catch { return $false }
    return $success.payloadManifestSha256 -ceq (Get-Sha256 -Path $manifestPath) -and
        [int]$success.payloadFileCount -eq $entries.Count -and
        (Test-PayloadManifest -PayloadRoot $PayloadRoot)
}

function Test-SnapshotEquality {
    param([Parameter(Mandatory)]$Before, [Parameter(Mandatory)]$After)
    return (ConvertTo-Json $Before -Depth 8 -Compress) -ceq
        (ConvertTo-Json $After -Depth 8 -Compress)
}

function Assert-OutputPlan {
    param([string]$StagingPath, [string]$FinalPath, [string]$DetachedPath)
    if ((Test-Path -LiteralPath $StagingPath) -or
        (Test-Path -LiteralPath $FinalPath) -or
        (Test-Path -LiteralPath $DetachedPath)) {
        return $false
    }
    return [IO.Path]::GetPathRoot($StagingPath) -eq [IO.Path]::GetPathRoot($FinalPath)
}

function Convert-UnityExitCode {
    param([int]$UnityExitCode)
    if ($UnityExitCode -eq 0) { return 0 }
    return $script:ReleaseExitCodes.UnityInvocationFailure
}

function Invoke-GitText {
    param([string]$Root, [string[]]$Arguments)
    $output = & git -C $Root @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $output" }
    return ($output -join "`n").Trim()
}

function Get-GitSnapshot {
    param(
        [Parameter(Mandatory)][string]$Root,
        [string[]]$CanaryPaths = @(),
        [switch]$Detached
    )
    $status = @(Invoke-GitText -Root $Root -Arguments @("status", "--porcelain=v1", "-uall") -split "`n" |
        Where-Object { $_ })
    $tracked = @($status | Where-Object {
        $_ -notmatch '^\?\?' -and $_.Substring(0, 1) -eq ' '
    } | ForEach-Object { $_.Substring(3).Replace('\', '/') })
    $staged = @($status | Where-Object {
        $_ -notmatch '^\?\?' -and $_.Substring(0, 1) -ne ' '
    } | ForEach-Object { $_.Substring(3).Replace('\', '/') })
    $untracked = @($status | Where-Object { $_ -match '^\?\?' } |
        ForEach-Object { $_.Substring(3).Replace('\', '/') })
    $canaries = [ordered]@{}
    foreach ($relative in $CanaryPaths) {
        $candidate = Join-Path $Root $relative.Replace('/', '\')
        $canaries[$relative] = if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            Get-Sha256 -Path $candidate
        } else { "<missing>" }
    }
    $originMain = Invoke-GitText -Root $Root -Arguments @("rev-parse", "origin/main")
    $counts = (Invoke-GitText -Root $Root -Arguments @(
        "rev-list", "--left-right", "--count", "origin/main...HEAD"
    )) -split '\s+'
    return [ordered]@{
        head = Invoke-GitText -Root $Root -Arguments @("rev-parse", "HEAD")
        tree = Invoke-GitText -Root $Root -Arguments @("rev-parse", "HEAD^{tree}")
        branch = if ($Detached) { "(detached)" } else {
            Invoke-GitText -Root $Root -Arguments @("rev-parse", "--abbrev-ref", "HEAD")
        }
        detached = [bool]$Detached
        originMain = $originMain
        behind = [int]$counts[0]
        ahead = [int]$counts[1]
        tracked = @($tracked)
        staged = @($staged)
        untracked = @($untracked)
        canaries = $canaries
    }
}

function Get-RepositoryFamilyPaths {
    param([string]$Root)
    $lines = @(& git -C $Root worktree list --porcelain)
    return @($lines | Where-Object { $_ -like "worktree *" } |
        ForEach-Object { $_.Substring(9).Replace('/', '\') })
}

function Write-FailureEvidence {
    param([string]$Path, [string]$Stage, [int]$ExitCode, [string]$SourceSha,
        [string]$RunId, [string]$PrivateLogPath)
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    [ordered]@{
        failureStage = $Stage
        exitCode = $ExitCode
        sourceSha = $SourceSha
        runId = $RunId
        privateLogPath = $PrivateLogPath
        timestampUtc = [DateTime]::UtcNow.ToString("o")
        deployable = $false
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Path "FAILURE.json") -Encoding UTF8
}

function Invoke-WindowsReleasePipeline {
    [CmdletBinding()]
    param(
        [string]$RepositoryRoot,
        [string]$UnityExe,
        [string]$OutputRoot,
        [string]$BuildSourceRoot,
        [string]$RunId,
        [string[]]$AllowUntrackedRoot
    )
    $stage = "preflight"
    $sourceSha = ""
    $staging = ""
    $final = ""
    $privateLog = ""
    $exitCode = $script:ReleaseExitCodes.WrapperInternalError
    try {
        if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
            throw "Unity executable not found: $UnityExe"
        }
        $sourceSha = Invoke-GitText -Root $RepositoryRoot -Arguments @("rev-parse", "HEAD")
        $sourceTree = Invoke-GitText -Root $RepositoryRoot -Arguments @("rev-parse", "HEAD^{tree}")
        $canaries = @(
            "ProjectSettings/ProjectVersion.txt",
            "ProjectSettings/ProjectSettings.asset",
            "ProjectSettings/EditorBuildSettings.asset",
            "Assets/Settings/Build Profiles/Windows 1.asset",
            "Packages/manifest.json",
            "Packages/packages-lock.json",
            "Assets/_Features/Stages/Editor/Build/WindowsReleaseBuildCli.cs",
            "Assets/_Features/Stages/Editor/Build/WindowsReleaseBuildPolicy.cs",
            "Tools/Build/Build-WindowsRelease.ps1"
        )
        $invocationPre = Get-GitSnapshot -Root $RepositoryRoot -CanaryPaths $canaries
        if (-not (Test-GitState -Snapshot $invocationPre -AllowedRoots $AllowUntrackedRoot)) {
            $exitCode = if (@($invocationPre.Tracked).Count -or @($invocationPre.Staged).Count) {
                $script:ReleaseExitCodes.GitPreflightFailure
            } else { $script:ReleaseExitCodes.UnknownUntrackedFailure }
            throw "Invocation worktree clean gate failed."
        }
        $family = Get-RepositoryFamilyPaths -Root $RepositoryRoot
        # Keep the complete snapshot so CrashHandler parent attribution is possible.
        $processes = @(Get-CimInstance Win32_Process)
        if (-not (Test-ReleaseProcessGate -Processes $processes -RepositoryFamilyPaths $family)) {
            $exitCode = $script:ReleaseExitCodes.ProcessGateFailure
            throw "Repository process gate failed."
        }

        $artifactId = "$sourceSha-$RunId"
        $parent = Join-Path (Join-Path $OutputRoot $sourceSha) $script:ConfigurationPathName
        $staging = Join-Path $parent ".staging-$RunId"
        $final = Join-Path $parent $RunId
        $failed = Join-Path (Join-Path $parent "failed") $RunId
        $detached = Join-Path (Join-Path $BuildSourceRoot $sourceSha) $RunId
        if ((Test-Path -LiteralPath $staging) -or (Test-Path -LiteralPath $final) -or
            [IO.Path]::GetPathRoot($staging) -ne [IO.Path]::GetPathRoot($final)) {
            $exitCode = $script:ReleaseExitCodes.OutputCollision
            throw "Output collision or cross-volume promotion plan."
        }
        if (Test-Path -LiteralPath $detached) {
            $exitCode = $script:ReleaseExitCodes.DetachedSourceFailure
            throw "Detached source collision."
        }
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        New-Item -ItemType Directory -Path (Split-Path $detached -Parent) -Force | Out-Null
        $stage = "detached-source"
        & git -C $RepositoryRoot worktree add --detach $detached $sourceSha
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $script:ReleaseExitCodes.DetachedSourceFailure
            throw "Detached worktree creation failed."
        }
        $buildPre = Get-GitSnapshot -Root $detached -CanaryPaths $canaries -Detached
        if (-not (Test-GitState -Snapshot $buildPre -RequireNoUntracked)) {
            $exitCode = $script:ReleaseExitCodes.DetachedSourceFailure
            throw "Detached source is not clean."
        }

        $payload = Join-Path $staging "payload"
        New-Item -ItemType Directory -Path $payload -Force | Out-Null
        $privateRoot = Join-Path $parent ".private\$RunId"
        New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
        $privateLog = Join-Path $privateRoot "UnityEditor.log"
        $metadataPath = Join-Path $payload "build-metadata.json"
        $reportPath = Join-Path $payload "build-report-summary.json"
        $exePath = Join-Path $payload "VectorQuake.exe"

        $stage = "unity"
        $unityArguments = @(
            "-batchmode", "-nographics", "-buildTarget", "Win64",
            "-projectPath", $detached,
            "-executeMethod", "WindowsReleaseBuildCli.BuildWindowsX64NonDevelopment",
            "-releaseOutputPath", $exePath,
            "-releaseRunId", $RunId,
            "-releaseArtifactId", $artifactId,
            "-releaseIntermediateMetadataPath", $metadataPath,
            "-releaseBuildReportPath", $reportPath,
            "-logFile", $privateLog
        )
        $unityProcess = Start-Process -FilePath $UnityExe -ArgumentList $unityArguments -PassThru
        $processesAfterStart = @(Get-CimInstance Win32_Process)
        if (-not (Test-ReleaseProcessGate -Processes $processesAfterStart `
                -RepositoryFamilyPaths $family -AllowedUnityPid $unityProcess.Id)) {
            try { Stop-Process -Id $unityProcess.Id -Force -ErrorAction SilentlyContinue } catch {}
            $exitCode = $script:ReleaseExitCodes.ProcessGateFailure
            throw "Process gate changed after Unity start."
        }
        $unityProcess.WaitForExit()
        if ($unityProcess.ExitCode -ne 0) {
            $exitCode = $script:ReleaseExitCodes.UnityInvocationFailure
            throw "Unity returned exit code $($unityProcess.ExitCode)."
        }

        $stage = "drift"
        $invocationPost = Get-GitSnapshot -Root $RepositoryRoot -CanaryPaths $canaries
        $buildPost = Get-GitSnapshot -Root $detached -CanaryPaths $canaries -Detached
        if (-not (Test-SnapshotEquality $invocationPre $invocationPost) -or
            -not (Test-SnapshotEquality $buildPre $buildPost)) {
            $exitCode = $script:ReleaseExitCodes.SourceDriftDetected
            throw "Source or configuration drift detected."
        }

        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        $metadata.sourceSha = $sourceSha
        $metadata.sourceTree = $sourceTree
        $metadata.branch = [string]$invocationPre.branch
        $metadata.headDetached = $true
        $metadata.originMainSha = [string]$invocationPre.originMain
        $metadata.ahead = [int]$invocationPre.ahead
        $metadata.behind = [int]$invocationPre.behind
        $metadata.sourceDirty = $false
        $metadata.entrySourceSha256 = $buildPre.canaries[
            "Assets/_Features/Stages/Editor/Build/WindowsReleaseBuildCli.cs"]
        $metadata.policySourceSha256 = $buildPre.canaries[
            "Assets/_Features/Stages/Editor/Build/WindowsReleaseBuildPolicy.cs"]
        $metadata.wrapperSourceSha256 = $buildPre.canaries[
            "Tools/Build/Build-WindowsRelease.ps1"]
        $metadata | ConvertTo-Json -Depth 10 |
            Set-Content -LiteralPath $metadataPath -Encoding UTF8
        [ordered]@{
            configuration = $script:ConfigurationName
            backend = "Mono"
            managedStrippingLevel = "Disabled"
            development = $false
            playerLogEnabled = $true
            stackTracePolicy = "ScriptOnly"
            scenes = @(
                "Assets/Scenes/MainMenuScene.unity",
                "Assets/Scenes/UIAudioScene.unity"
            )
        } | ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath (Join-Path $payload "configuration-summary.json") -Encoding UTF8

        $stage = "manifest"
        $manifest = New-PayloadManifest -PayloadRoot $staging
        if (-not (Test-PayloadManifest -PayloadRoot $staging)) {
            $exitCode = $script:ReleaseExitCodes.ManifestVerificationFailure
            throw "Payload manifest verification failed."
        }
        $stage = "success-control"
        New-SuccessControl -PayloadRoot $staging -RunId $RunId -ArtifactId $artifactId `
            -SourceSha $sourceSha -SourceTree $sourceTree -Manifest $manifest | Out-Null
        if (-not (Test-SuccessControl -PayloadRoot $staging `
                -ExpectedSourceSha $sourceSha -ExpectedSourceTree $sourceTree)) {
            $exitCode = $script:ReleaseExitCodes.ControlFileConsistencyFailure
            throw "SUCCESS control consistency failed."
        }

        $stage = "promotion"
        if (Test-Path -LiteralPath $final) {
            $exitCode = $script:ReleaseExitCodes.OutputCollision
            throw "Final artifact path already exists."
        }
        Move-Item -LiteralPath $staging -Destination $final
        if (-not (Test-SuccessControl -PayloadRoot $final `
                -ExpectedSourceSha $sourceSha -ExpectedSourceTree $sourceTree)) {
            $exitCode = $script:ReleaseExitCodes.PromotionFailure
            throw "Final verification after promotion failed."
        }
        Write-Host "WINDOWS_X64_NONDEVELOPMENT_MONO_RC_BUILD_PASS"
        Write-Host "FinalArtifact=$final"
        return 0
    }
    catch {
        if ($exitCode -eq $script:ReleaseExitCodes.WrapperInternalError -and
            $stage -eq "preflight") {
            $exitCode = $script:ReleaseExitCodes.GitPreflightFailure
        }
        Write-Error "[$stage][$exitCode] $($_.Exception.Message)"
        $quarantineCandidate = if (-not [string]::IsNullOrWhiteSpace($staging) -and
            (Test-Path -LiteralPath $staging)) { $staging } elseif (
            $stage -eq "promotion" -and
            -not [string]::IsNullOrWhiteSpace($final) -and
            (Test-Path -LiteralPath $final)) { $final } else { "" }
        if (-not [string]::IsNullOrWhiteSpace($quarantineCandidate)) {
            try {
                $failedPath = Join-Path (Join-Path (Split-Path $staging -Parent) "failed") $RunId
                if (-not (Test-Path -LiteralPath $failedPath)) {
                    Write-FailureEvidence -Path $quarantineCandidate -Stage $stage -ExitCode $exitCode `
                        -SourceSha $sourceSha -RunId $RunId -PrivateLogPath $privateLog
                    New-Item -ItemType Directory -Path (Split-Path $failedPath -Parent) `
                        -Force | Out-Null
                    Move-Item -LiteralPath $quarantineCandidate -Destination $failedPath
                }
            } catch {
                Write-Warning "Failure quarantine could not be completed: $($_.Exception.Message)"
            }
        }
        return $exitCode
    }
}

if ($env:VECTORQUAKE_RELEASE_WRAPPER_TEST_MODE -ne "1") {
    $pipelineExit = Invoke-WindowsReleasePipeline `
        -RepositoryRoot $RepositoryRoot `
        -UnityExe $UnityExe `
        -OutputRoot $OutputRoot `
        -BuildSourceRoot $BuildSourceRoot `
        -RunId $RunId `
        -AllowUntrackedRoot $AllowUntrackedRoot
    exit $pipelineExit
}
